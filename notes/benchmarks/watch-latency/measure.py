"""Watch edit-to-done probe. Preparation never launches compilers; all subprocesses use RTK.

Version 2: explicit identical configuration for watch and fresh compiles, a conditional-define
fixture, final-digest output readiness with raw transient observations kept separately,
semantically distinct burst saves, per-edit fresh-compile equivalence and verified process-tree
cleanup. See README.md.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import queue
import re
import shutil
import subprocess
import threading
import time

HERE = Path(__file__).resolve().parent
ANSI = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")
COMPILE = re.compile(r"Fable compilation finished in\s+(\d+)ms")
TIMING = re.compile(
    r"^\s*(Project cracking|FCS parse \+ type check|Fable per-file|Compilation total \(wall\)|Allocated|JIT \(process\)|CPU time \(process\))\D*?([\d.]+)"
)
FILES = re.compile(r"over (\d+) files")
DIAGNOSTIC = re.compile(r"\b(error|warning)\b\s+(\S+):\s*(.*)$")
BROAD = 12
UNDEFINED_INPUT = r"'missingWatchProbeName' is not defined"
UNDEFINED_VALUE = r"'value' is not defined"


def stamp():
    # Windows: QueryPerformanceCounter, the same clock as .NET Stopwatch.GetTimestamp in any process.
    return time.perf_counter_ns()


def inside(path, root):
    """Resolve symlinks/junctions and require the result inside root (also resolved)."""
    root = Path(root).resolve()
    path = Path(path).resolve()
    if not path.is_relative_to(root):
        raise ValueError(f"Task file must be inside scratch: {path}")
    return path


# ---------------------------------------------------------------------------- process trees


def _windows_processes():
    """(pid -> (parent, creation)) for every process, via CIM. Failure is an error, never ignored."""
    script = (
        "Get-CimInstance Win32_Process | ForEach-Object { "
        '"$($_.ProcessId) $($_.ParentProcessId) $($_.CreationDate.ToFileTimeUtc())" }'
    )
    listed = subprocess.run(
        ["rtk", "proxy", "powershell", "-NoProfile", "-Command", script], capture_output=True, text=True, timeout=60
    )
    if listed.returncode != 0:
        raise RuntimeError(f"Process listing failed: {listed.stderr}")
    table = {}
    for line in listed.stdout.splitlines():
        parts = line.split()
        if len(parts) == 3 and parts[0].isdigit():
            table[int(parts[0])] = (int(parts[1]), int(parts[2]))
    if not table:
        raise RuntimeError("Process listing returned nothing")
    return table


def descendants(root_pid, table=None):
    """Live (pid, creation) pairs of root_pid and its descendants; creation guards pid reuse."""
    table = table if table is not None else _windows_processes()
    if root_pid not in table:
        return []
    found = [(root_pid, table[root_pid][1])]
    frontier = [found[0]]
    while frontier:
        parent, parent_created = frontier.pop()
        for pid, (ppid, created) in table.items():
            # A child cannot predate its parent; older processes with a reused parent id are unrelated.
            if ppid == parent and created >= parent_created and (pid, created) not in found:
                found.append((pid, created))
                frontier.append((pid, created))
    return found


def kill_tree(process, known=(), kill=None, timeout=20):
    """Kill the tree rooted at a process we launched and verify every member exited.

    `known` carries descendants recorded earlier (they may have been orphaned since). Only
    processes whose (pid, creation time) match a recorded/current member are touched."""
    kill = kill or (
        lambda pid: subprocess.run(
            ["rtk", "proxy", "taskkill", "/PID", str(pid), "/T", "/F"], capture_output=True, text=True, timeout=60
        )
    )
    if os.name != "nt":
        process.kill()
        process.wait(timeout)
        return []
    members = set(known) | set(descendants(process.pid))
    failures = []
    for pid, created in sorted(members):
        table = _windows_processes()
        if table.get(pid, (None, None))[1] != created:
            continue  # already gone (or the id now belongs to someone else: never touch it)
        result = kill(pid)
        if result.returncode != 0:
            table = _windows_processes()
            if table.get(pid, (None, None))[1] == created:
                failures.append((pid, result.returncode, (result.stdout + result.stderr).strip()))
    deadline = time.monotonic() + timeout
    while True:
        table = _windows_processes()
        alive = [(pid, c) for pid, c in members if table.get(pid, (None, None))[1] == c]
        if not alive:
            break
        if time.monotonic() > deadline:
            raise RuntimeError(f"Process tree members still alive after kill: {alive}; failures {failures}")
        time.sleep(0.2)
    if failures:
        # Every member is gone, but a kill call reported failure for a process that was then alive.
        raise RuntimeError(f"taskkill failed for live members: {failures}")
    try:
        process.wait(timeout)
    except subprocess.TimeoutExpired:
        raise RuntimeError(f"Launched process {process.pid} did not report exit") from None
    return sorted(members)


def run_checked(cmd, cwd, env, timeout, expect_success=True):
    """subprocess.run replacement whose timeout kills and verifies the whole tree."""
    process = subprocess.Popen(
        cmd,
        cwd=cwd,
        env=env,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    try:
        output, _ = process.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        killed = kill_tree(process)
        process.communicate()
        raise TimeoutError(f"Timed out after {timeout}s; killed and verified tree {killed}: {cmd}") from None
    if expect_success and process.returncode != 0:
        raise subprocess.CalledProcessError(process.returncode, cmd, output)
    return process.returncode, output


# ---------------------------------------------------------------------------- fixture


def initial_state():
    return {
        "input": 0,
        "input_sig": False,
        "input_name": "value",
        "input_error": False,
        "shared": 0,
        "shared_sig": False,
        "lib": 0,
        "lib_sig": False,
        "ref": 0,
        "define": False,
        "extra": False,
        "reference": False,
        "app": 0,
        "import_define": False,
        "reorder": False,
    }


def render(state):
    """All fixture files for a state. The driver only ever writes these task-owned files."""
    files = {}
    compile_items = ["Lib.fsi", "Lib.fs", "Shared.fs", "Input.fs"]
    broad_items = [f"Broad{i}.fs" for i in range(1, BROAD + 1)]
    # reorder: Narrow.fs moves after the Broad files (still after Input.fs, so still valid).
    compile_items += broad_items + ["Narrow.fs"] if state["reorder"] else ["Narrow.fs"] + broad_items
    if state["extra"]:
        compile_items.append("Extra.fs")
    compile_items.append("App.fs")
    define = (
        "\n  <PropertyGroup><DefineConstants>$(DefineConstants);PROBE_DEFINE</DefineConstants></PropertyGroup>"
        if state["define"]
        else ""
    )
    reference = '\n  <ItemGroup><ProjectReference Include="Ref/Ref.fsproj" /></ItemGroup>' if state["reference"] else ""
    items = "\n".join(f'    <Compile Include="{name}" />' for name in compile_items)
    files["App.fsproj"] = (
        '<Project Sdk="Microsoft.NET.Sdk">\n'
        "  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>"
        f'{define}\n  <Import Project="Probe.props" />\n  <ItemGroup>\n{items}\n  </ItemGroup>{reference}\n</Project>\n'
    )
    # An MSBuild import: the watcher filters *.fs/*.fsi/*.fsx/*.fsproj, so edits here are not seen.
    files["Probe.props"] = (
        "<Project>\n"
        + (
            "  <PropertyGroup><DefineConstants>$(DefineConstants);PROBE_DEFINE</DefineConstants></PropertyGroup>\n"
            if state["import_define"]
            else ""
        )
        + "</Project>\n"
    )
    files["Ref/Ref.fsproj"] = (
        '<Project Sdk="Microsoft.NET.Sdk">\n'
        "  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>\n"
        '  <ItemGroup><Compile Include="Ref.fs" /></ItemGroup>\n</Project>\n'
    )
    files["Ref/Ref.fs"] = f"module WatchProbe.Ref\nlet refValue = {state['ref']}\n"
    files["Lib.fsi"] = "module WatchProbe.Lib\nval libValue: int\n" + (
        "val libExtra: int\n" if state["lib_sig"] else ""
    )
    files["Lib.fs"] = f"module WatchProbe.Lib\nlet libValue = {state['lib']}\n" + (
        "let libExtra = 5\n" if state["lib_sig"] else ""
    )
    files["Shared.fs"] = f"module WatchProbe.Shared\nlet shared = {state['shared']}\n" + (
        "let sharedAdded = 7\n" if state["shared_sig"] else ""
    )
    value = "missingWatchProbeName" if state["input_error"] else str(state["input"])
    # The annotation keeps the error local to Input.fs (no cascading obj/int errors in dependents).
    files["Input.fs"] = f"module WatchProbe.Input\nlet {state['input_name']}: int = {value}\n" + (
        "let addedSignatureMember = 42\n" if state["input_sig"] else ""
    )
    files["Narrow.fs"] = "module WatchProbe.Narrow\nlet narrow = Input.value * 2\n"
    for i in range(1, BROAD + 1):
        files[f"Broad{i}.fs"] = f"module WatchProbe.Broad{i}\nlet b = Shared.shared + {i}\n"
    files["Extra.fs"] = "module WatchProbe.Extra\nlet extra = 9\n"
    broad = " + ".join(f"Broad{i}.b" for i in range(1, BROAD + 1))
    files["App.fs"] = (
        "module WatchProbe.App\n"
        "let result = Input.value + 100\n"
        f"let app = {state['app']}\n"
        "let narrow = Narrow.narrow\n"
        f"let broad = {broad}\n"
        "let lib = Lib.libValue\n"
        '#if DEBUG\nlet configuration = "Debug"\n#else\nlet configuration = "Release"\n#endif\n'
        "#if PROBE_DEFINE\nlet probe = 1\n#else\nlet probe = 0\n#endif\n"
        + ("let extra = Extra.extra\n" if state["extra"] else "let extra = 0\n")
        + ("let reference = Ref.refValue\n" if state["reference"] else "let reference = 0\n")
    )
    return files


def project_sources(state):
    """Source files compiled for a state, relative to the fixture (outputs mirror these paths)."""
    project = render(state)["App.fsproj"]
    sources = re.findall(r'<Compile Include="([^"]+)"', project)
    if state["reference"]:
        sources.append("Ref/Ref.fs")
    return sources


def expected(state, configuration):
    if state["input_error"] or state["input_name"] != "value":
        return None
    return {
        "result": state["input"] + 100,
        "narrow": state["input"] * 2,
        "broad": sum(state["shared"] + i for i in range(1, BROAD + 1)),
        "lib": state["lib"],
        "configuration": configuration,
        "probe": 1 if state["define"] or state["import_define"] else 0,
        "extra": 9 if state["extra"] else 0,
        "app": state["app"],
        "reference": state["ref"] if state["reference"] else 0,
    }


def change(**updates):
    return lambda state: {**state, **updates}


# (case, edit class, state transform, write order, mode, expected diagnostic patterns)
EDITS = [
    ("body-input", "body", change(input=1), ["Input.fs"], "write", []),
    ("body-input-2", "body", change(input=2), ["Input.fs"], "write", []),
    ("burst", "body-burst", change(input=33), ["Input.fs"], "burst", []),
    # Saves 80 ms apart: longer than a 50 ms debounce, shorter than 200 ms.
    ("burst-slow", "body-burst-slow", change(input=34), ["Input.fs"], "burst-slow", []),
    ("atomic", "body-atomic", change(input=4), ["Input.fs"], "atomic", []),
    ("noop", "noop-write", change(), ["Input.fs"], "noop", []),
    ("sig-narrow", "signature-narrow", change(input=5, input_sig=True), ["Input.fs"], "write", []),
    ("body-shared", "body-broad-graph", change(shared=1), ["Shared.fs"], "write", []),
    ("sig-broad", "signature-broad", change(shared=2, shared_sig=True), ["Shared.fs"], "write", []),
    ("error", "error", change(input_error=True), ["Input.fs"], "write", [UNDEFINED_INPUT]),
    ("repair", "repair", change(input_error=False, input=7), ["Input.fs"], "write", []),
    (
        "dependent-error",
        "error-dependents",
        change(input_name="valueRenamed"),
        ["Input.fs"],
        "write",
        [UNDEFINED_VALUE],
    ),
    ("dependent-repair", "repair-dependents", change(input_name="value", input=8), ["Input.fs"], "write", []),
    ("fsi-body", "fsi-body", change(lib=3), ["Lib.fs"], "write", []),
    ("fsi-sig", "fsi-signature", change(lib=4, lib_sig=True), ["Lib.fs", "Lib.fsi"], "write", []),
    # Two body edits in one save: FCS checks up to App.fs, so the 12 Broad files in between are
    # where the signature cutoff (FICKL_FCS_SIGNATURE_CUTOFF) reuses or rechecks results.
    ("multi-body", "body-multi-file", change(shared=3, app=1), ["Shared.fs", "App.fs"], "write", []),
    ("proj-define", "project-define", change(define=True), ["App.fsproj"], "write", []),
    ("proj-add-file", "project-add-file", change(extra=True), ["Extra.fs", "App.fsproj", "App.fs"], "write", []),
    (
        "proj-reference",
        "project-reference",
        change(reference=True, ref=6),
        ["Ref/Ref.fs", "App.fsproj", "App.fs"],
        "write",
        [],
    ),
    ("ref-body", "reference-body", change(ref=11), ["Ref/Ref.fs"], "write", []),
    # Unwatched by design: records whether any cycle starts; the next edit reveals stale options.
    ("proj-import", "project-import", change(import_define=True), ["Probe.props"], "unwatched", []),
    ("proj-define-off", "project-define-off", change(define=False), ["App.fsproj"], "write", []),
    ("proj-reorder", "project-reorder", change(reorder=True), ["App.fsproj"], "write", []),
    ("proj-remove-file", "project-remove-file", change(extra=False), ["App.fsproj", "App.fs"], "write", []),
    (
        "proj-reference-remove",
        "project-reference-remove",
        change(reference=False),
        ["App.fsproj", "App.fs"],
        "write",
        [],
    ),
    ("body-final", "body", change(input=9), ["Input.fs"], "write", []),
]


# ---------------------------------------------------------------------------- compiler sessions


def command(args, project, output, configuration, delay=None, fresh=False):
    launch = ["dotnet", str(args.compiler)] if args.compiler.suffix.lower() == ".dll" else [str(args.compiler)]
    result = [
        "rtk",
        "proxy",
        *launch,
        str(project),
        "--outDir",
        str(output),
        "--fableLib",
        str(args.runtime_lib),
        "--configuration",
        configuration,
        "--noPackageCache",
        "--timings",
    ]
    if args.no_restore:
        result.append("--noRestore")
    if fresh:
        result.append("--noCache")
    if delay is not None:
        result += ["--watch"]
        if not getattr(args, "omit_watch_delay", False):
            result += ["--watchDelay", str(delay)]
    result += args.compiler_arg
    return result


class Watch:
    def __init__(self, cmd, cwd, log, env):
        self.events = queue.Queue()
        self.records = []
        self.log = log.open("w", encoding="utf-8")
        self.started_ns = stamp()
        self.process = subprocess.Popen(
            cmd,
            cwd=cwd,
            env=env,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
            errors="replace",
            bufsize=1,
        )
        self.tree = []
        self.thread = threading.Thread(target=self.read, daemon=True)
        self.thread.start()

    def read(self):
        for line in self.process.stdout:
            event = {"received_ns": stamp(), "line": ANSI.sub("", line.rstrip())}
            self.records.append(event)
            self.log.write(json.dumps(event) + "\n")
            self.log.flush()
            self.events.put(event)
        self.events.put({"received_ns": stamp(), "eof": True})

    def ready(self, timeout):
        deadline = time.monotonic() + timeout
        cycle = []
        while True:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise TimeoutError("No Watching boundary; inspect the session JSONL")
            try:
                event = self.events.get(timeout=remaining)
            except queue.Empty:
                raise TimeoutError("No Watching boundary; inspect the session JSONL") from None
            if event.get("eof"):
                raise RuntimeError(f"Compiler exited before watch readiness: {self.process.poll()}")
            cycle.append(event)
            if event["line"].startswith("Watching "):
                return cycle

    def record_tree(self):
        """Remember descendants while alive, so orphaned ones are still found on close."""
        if os.name == "nt":
            self.tree = sorted(set(self.tree) | set(descendants(self.process.pid)))

    def close(self):
        try:
            if self.process.poll() is None or self.tree:
                self.killed = kill_tree(self.process, self.tree)
        finally:
            self.thread.join(timeout=10)
            self.log.close()


class Observer:
    """Samples every generated .js file; raw transitions are kept, readiness is derived later."""

    def __init__(self, root, interval=0.002):
        self.root = root
        self.interval = interval
        self.stop_event = threading.Event()
        self.ready_event = threading.Event()
        self.raw = []
        self.baseline = self.snapshot_digests()
        self.thread = threading.Thread(target=self.run, daemon=True)

    def snapshot_digests(self):
        result = {}
        for path in sorted(self.root.rglob("*.js")):
            try:
                result[path.relative_to(self.root).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
            except (FileNotFoundError, PermissionError):
                pass
        return result

    def run(self):
        stats = {}
        for path in self.root.rglob("*.js"):
            try:
                st = path.stat()
                stats[path] = (st.st_mtime_ns, st.st_size)
            except OSError:
                pass
        self.ready_event.set()
        # Event.wait(0.002) sleeps a whole 15.6 ms Windows timer tick; time.sleep uses a
        # high-resolution waitable timer (Python 3.11+), measured at ~1.5 ms per 1 ms request.
        while not self.stop_event.is_set():
            time.sleep(self.interval)
            for path in self.root.rglob("*.js"):
                try:
                    st = path.stat()
                except OSError:
                    continue
                key = (st.st_mtime_ns, st.st_size)
                if stats.get(path) == key:
                    continue
                stats[path] = key
                try:
                    content = path.read_bytes()
                except (FileNotFoundError, PermissionError):
                    content = None
                self.raw.append(
                    {
                        "observed_ns": stamp(),
                        "file": path.relative_to(self.root).as_posix(),
                        "size": st.st_size,
                        "mtime_ns": st.st_mtime_ns,
                        "sha256": None if content is None else hashlib.sha256(content).hexdigest(),
                    }
                )

    def __enter__(self):
        self.thread.start()
        if not self.ready_event.wait(30):
            self.stop_event.set()
            raise TimeoutError("Output observer did not initialize")
        return self

    def __exit__(self, *_):
        self.stop_event.set()
        self.thread.join()


def readiness(baseline, final, raw):
    """Per changed file, the first observation of its final settled digest (no later deviation).

    Returns (ready_ns or None, per-file detail, rewrites-with-identical-content)."""
    detail = {}
    for name, digest in final.items():
        if baseline.get(name) == digest:
            continue
        hits = [r for r in raw if r["file"] == name]
        first = None
        for index, r in enumerate(hits):
            if r["sha256"] == digest and all(h["sha256"] == digest for h in hits[index:]):
                first = r["observed_ns"]
                break
        detail[name] = {
            "final_sha256": digest,
            "ready_ns": first,
            "transients": [r for r in hits if r["sha256"] != digest],
        }
    missing = [name for name, d in detail.items() if d["ready_ns"] is None]
    if missing:
        raise AssertionError(f"Final digest never observed for {missing}")
    rewrites = sorted({r["file"] for r in raw if baseline.get(r["file"]) == r["sha256"] and r["file"] not in detail})
    ready = max((d["ready_ns"] for d in detail.values()), default=None)
    return ready, detail, rewrites


def parse_cycles(events):
    cycles, current = [], None
    for event in events:
        line = event["line"]
        if line.startswith("Started Fable compilation"):
            current = {"started_ns": event["received_ns"], "timings": {}}
        elif (match := COMPILE.search(line)) and current is not None:
            current.update(finished_ns=event["received_ns"], compile_ms=int(match[1]))
        elif (match := TIMING.match(line)) and current is not None:
            current["timings"][match[1]] = float(match[2])
            if files := FILES.search(line):
                current["timings"]["files"] = int(files[1])
        elif line.startswith("Watching "):
            if current is None:
                current = {"timings": {}}
            current["watching_ns"] = event["received_ns"]
            cycles.append(current)
            current = None
    return cycles


def diagnostics_of(lines):
    found = []
    for line in lines:
        if match := DIAGNOSTIC.search(line):
            if "Fable compilation" in line:
                continue
            found.append(
                {
                    "severity": match[1],
                    "code": match[2],
                    "message": match[3].strip(),
                    "file": line.split("(")[0].strip().replace("\\", "/") if "(" in line else None,
                }
            )
    return found


def diagnostic_key(d):
    return (d["severity"], d["code"], d["file"], d["message"])


def check_diagnostics(case, diagnostics, patterns):
    errors = [d for d in diagnostics if d["severity"] == "error"]
    if not patterns:
        if errors:
            raise AssertionError(f"{case}: unexpected errors {errors}")
        return
    unmatched = [d for d in errors if not any(re.search(p, d["message"]) for p in patterns)]
    absent = [p for p in patterns if not any(re.search(p, d["message"]) for d in errors)]
    if unmatched or absent:
        raise AssertionError(f"{case}: diagnostics {errors} unmatched {unmatched} absent {absent}")


def write_files(project_dir, scratch, files, names, mode, burst_values=None, render_burst=None):
    writes = []
    for name in names:
        target = inside(project_dir / name, scratch)
        if mode.startswith("burst"):
            # Three complete saves of semantically distinct values, 10 (slow: 80) ms apart; only the last is expected.
            for index, text in enumerate(render_burst):
                start = stamp()
                target.write_text(text, encoding="utf-8")
                writes.append({"file": name, "start_ns": start, "end_ns": stamp(), "value": burst_values[index]})
                if index < len(render_burst) - 1:
                    time.sleep(0.08 if mode == "burst-slow" else 0.01)
        elif mode == "atomic":
            temporary = inside(target.with_suffix(".watch-save"), scratch)
            temporary.write_text(files[name], encoding="utf-8")
            start = stamp()
            os.replace(temporary, target)
            writes.append({"file": name, "start_ns": start, "end_ns": stamp()})
        else:
            start = stamp()
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(files[name], encoding="utf-8")
            writes.append({"file": name, "start_ns": start, "end_ns": stamp()})
    return writes


def materialize(project_dir, scratch, state):
    files = render(state)
    for name, text in files.items():
        target = inside(project_dir / name, scratch)
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists() or target.read_text(encoding="utf-8") != text:
            target.write_text(text, encoding="utf-8")
    return files


def js_hashes(root):
    return {
        p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
        for p in sorted(root.rglob("*.js"))
        if "fable_modules" not in p.relative_to(root).parts
    }


def verify_output(args, output, values, env):
    code, out = run_checked(
        ["rtk", "node", str(args.verify_script.resolve()), str(output), json.dumps(values)],
        output.parent,
        env,
        args.timeout,
        expect_success=False,
    )
    return {"expected": values, "exit_code": code, "stdout": out.strip()}


def run_session(args, scratch, project_dir, session, env, delays_edits, record):
    folder = inside(Path(session["folder"]), scratch)
    folder.mkdir(parents=True)
    watch_dir = folder / "watch"
    state = initial_state()
    materialize(project_dir, scratch, state)
    # "@FOLDER@" in a value names this session folder (e.g. FICKL_WATCH_TRACE=@FOLDER@/trace.csv); braces get lost through rtk.
    session_env = dict(env, **{k: v.replace("@FOLDER@", str(folder)) for k, v in session.get("env", {}).items()})
    watch = Watch(session["watch_command"], project_dir, folder / "stdout.jsonl", session_env)
    try:
        acquired = watch.ready(args.timeout)
        watch.record_tree()
        record["initial_watch_ready_ns"] = acquired[-1]["received_ns"]
        record["watch_acquisition_ms"] = (acquired[-1]["received_ns"] - watch.started_ns) / 1e6
        record["initial_cycles"] = parse_cycles(acquired)
        initial_expected = expected(state, args.configuration)
        record["initial_semantics"] = verify_output(args, watch_dir / "App.js", initial_expected, env)
        if record["initial_semantics"]["exit_code"] != 0:
            raise AssertionError(f"Initial watch output wrong: {record['initial_semantics']}")
        previous_hashes = js_hashes(watch_dir)
        for index, (case, klass, transform, names, mode, patterns) in enumerate(delays_edits):
            new_state = transform(state)
            files = render(new_state)
            burst_values, render_burst = None, None
            if mode.startswith("burst"):
                burst_values = [31, 32, new_state["input"]]
                render_burst = [render({**new_state, "input": v})["Input.fs"] for v in burst_values]
            if mode not in ("noop", "burst", "burst-slow") and all(
                (project_dir / n).exists() and (project_dir / n).read_text(encoding="utf-8") == files[n] for n in names
            ):
                raise AssertionError(f"{case}: edit does not change any file")
            with Observer(watch_dir) as observer:
                boundaries = []
                writes = write_files(
                    project_dir,
                    scratch,
                    files if mode != "noop" else render(state),
                    names,
                    mode,
                    burst_values,
                    render_burst,
                )
                if mode == "unwatched":
                    time.sleep(max(2.0, 3 * session["delay_ms"] / 1000))
                    while not watch.events.empty():
                        boundaries.append(watch.events.get())
                else:
                    boundaries.extend(watch.ready(args.timeout))
                # Later cycles (bursts, multi-file writes) are drained until quiet, outside measured latency.
                quiet = max(0.4, session["delay_ms"] / 1000 + 0.3)
                while True:
                    try:
                        event = watch.events.get(timeout=quiet)
                    except queue.Empty:
                        break
                    if event.get("eof"):
                        raise RuntimeError(f"{case}: compiler exited during settling")
                    boundaries.append(event)
                    if not event["line"].startswith("Watching "):
                        boundaries.extend(watch.ready(args.timeout))
            settled_ns = stamp()
            watch.record_tree()
            state = new_state
            final = observer.snapshot_digests()
            events = [e for e in boundaries if e["received_ns"] >= writes[0]["start_ns"]]
            cycles = parse_cycles(events)
            diagnostics = diagnostics_of(e["line"] for e in events)
            check_diagnostics(case, diagnostics, patterns)
            if mode == "unwatched":
                record.setdefault("unwatched", []).append(
                    {"case": case, "cycles_started": len(cycles), "lines": [e["line"] for e in events][:20]}
                )
                state = {**state, "_unwatched_pending": True}
                edit_record = {
                    "index": index,
                    "case": case,
                    "class": klass,
                    "mode": mode,
                    "writes": writes,
                    "cycles": cycles,
                    "diagnostics": diagnostics,
                    "state": state,
                    "edit_to_watching_ms": None,
                    "last_write_to_watching_ms": None,
                    "edit_to_output_ready_ms": None,
                    "compile_reported_ms": [],
                    "changed_outputs": [],
                    "unwatched": True,
                }
                snapshot = folder / f"snap-{index:02d}"
                shutil.copytree(watch_dir, snapshot, ignore=shutil.ignore_patterns("fable_modules"))
                record["edits"].append(edit_record)
                previous_hashes = js_hashes(watch_dir)
                continue
            if not cycles:
                raise AssertionError(f"{case}: no post-edit watch boundary")
            ready_ns, detail, rewrites = readiness(observer.baseline, final, observer.raw)
            first, last = writes[0]["start_ns"], writes[-1]["end_ns"]
            edit_record = {
                "index": index,
                "case": case,
                "class": klass,
                "mode": mode,
                "writes": writes,
                "cycles": cycles,
                "diagnostics": diagnostics,
                "raw_output_observations": observer.raw,
                "changed_outputs": sorted(detail),
                "identical_rewrites": rewrites,
                "output_detail": detail,
                "edit_to_watching_ms": (cycles[-1]["watching_ns"] - first) / 1e6,
                "last_write_to_watching_ms": (cycles[-1]["watching_ns"] - last) / 1e6,
                "edit_to_output_ready_ms": None if ready_ns is None else (ready_ns - first) / 1e6,
                "last_write_to_output_ready_ms": None if ready_ns is None else (ready_ns - last) / 1e6,
                "compile_reported_ms": [c.get("compile_ms") for c in cycles],
                "settled_ns": settled_ns,
            }
            values = expected(state, args.configuration)
            current_hashes = js_hashes(watch_dir)
            if values is not None:
                edit_record["semantics"] = verify_output(args, watch_dir / "App.js", values, env)
                if edit_record["semantics"]["exit_code"] != 0:
                    # Stale or wrong watch output is an invalidation result, not a harness failure:
                    # record it, keep going so later edit classes still get an outcome.
                    record["invalidation_failures"].append(
                        {"case": case, "kind": "watch-semantics", "detail": edit_record["semantics"]["stdout"][-400:]}
                    )
                if mode.startswith("burst"):
                    # Red check: the first burst value must be rejected, proving the verifier discriminates.
                    stale = verify_output(args, watch_dir / "App.js", {**values, "result": 131}, env)
                    edit_record["burst_stale_control"] = stale
                    if stale["exit_code"] == 0:
                        raise AssertionError("burst: verifier accepted the first burst value")
            snapshot = folder / f"snap-{index:02d}"
            shutil.copytree(watch_dir, snapshot, ignore=shutil.ignore_patterns("fable_modules"))
            sources = folder / f"src-{index:02d}"
            sources.mkdir()
            for name, text in render(state).items():
                (sources / name).parent.mkdir(parents=True, exist_ok=True)
                (sources / name).write_text(text, encoding="utf-8")
            edit_record["state"] = state
            previous_hashes = current_hashes
            record["edits"].append(edit_record)
    finally:
        watch.close()
        record["killed_tree"] = getattr(watch, "killed", None)


def fresh_checks(args, scratch, project_dir, record, env):
    """After the watcher exited: restore each edit's sources in place and compile fresh."""
    folder = Path(record["folder"])
    for edit in record["edits"]:
        index = edit["index"]
        state = edit["state"]
        materialize(project_dir, scratch, state)
        fresh_dir = inside(folder / f"fresh-{index:02d}", scratch)
        cmd = command(args, project_dir / "App.fsproj", fresh_dir, args.fresh_configuration, fresh=True)
        code, output = run_checked(cmd, project_dir, env, args.timeout, expect_success=False)
        (folder / f"fresh-{index:02d}.log").write_text(output, encoding="utf-8")
        # Split on newlines only, like the watch reader: FCS separates suggestions with \x1d,
        # which str.splitlines would treat as a line break.
        fresh_diag = diagnostics_of(ANSI.sub("", l.rstrip("\r")) for l in output.split("\n"))
        watch_diag = edit["diagnostics"]
        edit["fresh_exit_code"] = code
        edit["fresh_diagnostics"] = fresh_diag
        expected_values = expected(state, args.configuration)
        if expected_values is None:
            if code == 0:
                raise AssertionError(f"{edit['case']}: fresh compile accepted an error state")
        elif code != 0:
            raise AssertionError(f"{edit['case']}: fresh compile failed; see fresh-{index:02d}.log")
        if sorted(map(diagnostic_key, fresh_diag)) != sorted(map(diagnostic_key, watch_diag)):
            record["invalidation_failures"].append(
                {"case": edit["case"], "kind": "diagnostics", "watch": watch_diag, "fresh": fresh_diag}
            )
        if expected_values is not None:
            watch_hashes = js_hashes(folder / f"snap-{index:02d}")
            fresh_hashes = js_hashes(fresh_dir)
            differing = sorted(k for k in fresh_hashes if watch_hashes.get(k) != fresh_hashes[k])
            extra = sorted(set(watch_hashes) - set(fresh_hashes))
            edit["fresh_equivalence"] = {"files": len(fresh_hashes), "differing": differing, "watch_only": extra}
            if not fresh_hashes:
                raise AssertionError(f"{edit['case']}: fresh compile produced no JS")
            # Fable never deletes outputs (watch or not): outputs of sources no longer in the project
            # are reported as orphans, not as invalidation failures. Anything else must match exactly.
            current = set(project_sources(state))
            orphans = [k for k in extra if k[:-3] + ".fs" not in current]
            extra = [k for k in extra if k not in orphans]
            edit["fresh_equivalence"]["orphans"] = orphans
            if differing or extra:
                record["invalidation_failures"].append(
                    {"case": edit["case"], "kind": "fresh-equivalence", "differing": differing, "watch_only": extra}
                )
            edit["fresh_semantics"] = verify_output(args, fresh_dir / "App.js", expected_values, env)
            if edit["fresh_semantics"]["exit_code"] != 0:
                raise AssertionError(f"{edit['case']}: fresh semantics {edit['fresh_semantics']}")
    record["validated"] = not record["invalidation_failures"]


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--compiler", type=Path, required=True)
    parser.add_argument("--runtime-lib", type=Path, required=True)
    parser.add_argument("--scratch", type=Path, required=True)
    parser.add_argument("--configuration", default="Release", choices=["Debug", "Release"])
    parser.add_argument(
        "--fresh-configuration",
        choices=["Debug", "Release"],
        help="Negative control only: a different fresh configuration must fail equivalence",
    )
    parser.add_argument("--delays", type=int, nargs="+", default=[200, 50, 10])
    parser.add_argument("--rounds", type=int, default=1)
    parser.add_argument("--timeout", type=float, default=180)
    parser.add_argument("--cases", nargs="+", help="Subset of edit cases (in order)")
    parser.add_argument(
        "--session-env",
        action="append",
        default=[],
        help="KEY=VALUE for the watch process only, e.g. FICKL_FCS_SIGNATURE_CUTOFF=0",
    )
    parser.add_argument("--skip-fresh", action="store_true", help="Timing-only; session not validated")
    parser.add_argument(
        "--omit-watch-delay",
        action="store_true",
        help="Exercise the compiler's default debounce; --delays then only labels sessions and "
        "sizes the settling window, so give the expected default",
    )
    parser.add_argument("--prepare-only", "--dry-run", action="store_true")
    parser.add_argument("--no-restore", action="store_true")
    parser.add_argument("--compiler-arg", action="append", default=[])
    parser.add_argument("--verify-script", type=Path, default=HERE / "verify.mjs")
    args = parser.parse_args(argv)
    if any(d <= 0 for d in args.delays) or args.rounds < 1:
        parser.error("Delays and rounds must be positive (Timers.Timer rejects zero)")
    args.fresh_configuration = args.fresh_configuration or args.configuration
    args.compiler = args.compiler.resolve()
    args.runtime_lib = args.runtime_lib.resolve()
    scratch = args.scratch.resolve()
    scratch.mkdir(parents=True, exist_ok=True)
    if (scratch / "results.json").exists():
        parser.error("Scratch already contains results; choose a fresh directory")
    by_case = {e[0]: e for e in EDITS}
    # --cases runs in the given order, so project sessions can sequence on/off and error transitions.
    edits = [by_case[c] for c in args.cases if c in by_case] if args.cases else list(EDITS)
    if args.cases and len(edits) != len(args.cases):
        parser.error(f"Unknown case in {args.cases}")
    project_dir = inside(scratch / "fixture", scratch)
    project_dir.mkdir(exist_ok=True)
    session_env = dict(kv.split("=", 1) for kv in args.session_env)
    if not (scratch / "package.json").exists():
        (scratch / "package.json").write_text('{"type":"module"}\n', encoding="utf-8")
    sessions = []
    for round_index in range(args.rounds):
        delays = args.delays if round_index % 2 == 0 else list(reversed(args.delays))
        for index, delay in enumerate(delays):
            folder = scratch / f"round-{round_index}-delay-{delay}-{index}"
            sessions.append(
                {
                    "round": round_index,
                    "delay_ms": delay,
                    "folder": str(folder),
                    "env": session_env,
                    "watch_command": command(
                        args, project_dir / "App.fsproj", folder / "watch", args.configuration, delay
                    ),
                }
            )
    plan = {
        "compiler": str(args.compiler),
        "runtime_lib": str(args.runtime_lib),
        "configuration": args.configuration,
        "fresh_configuration": args.fresh_configuration,
        "project": str(project_dir / "App.fsproj"),
        "cases": [e[0] for e in edits],
        "sessions": sessions,
        "environment": {"DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_USE_POLLING_FILE_WATCHER": "0"},
    }
    (scratch / "plan.json").write_text(json.dumps(plan, indent=2), encoding="utf-8")
    if args.prepare_only:
        materialize(project_dir, scratch, initial_state())
        print(json.dumps(plan, indent=2))
        return plan
    if not args.compiler.is_file() or not args.runtime_lib.is_dir():
        parser.error("Compiler must be a file and runtime library an existing directory")
    env = dict(os.environ, **plan["environment"])
    result = {"plan": plan, "clock": "perf_counter_ns (QPC on Windows)", "sessions": [], "completed": False}
    try:
        for session in sessions:
            record = {**session, "edits": [], "validated": False, "invalidation_failures": []}
            result["sessions"].append(record)
            run_session(args, scratch, project_dir, session, env, edits, record)
            (scratch / "results.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
            if not args.skip_fresh:
                fresh_checks(args, scratch, project_dir, record, env)
            (scratch / "results.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
        result["completed"] = True
        failures = [(s["folder"], f["case"], f["kind"]) for s in result["sessions"] for f in s["invalidation_failures"]]
        if failures:
            print(json.dumps({"invalidation_failures": failures}, indent=1))
            raise SystemExit(1)
    except BaseException as error:
        result["error"] = f"{type(error).__name__}: {error}"
        raise
    finally:
        (scratch / "results.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    return result


if __name__ == "__main__":
    main()
