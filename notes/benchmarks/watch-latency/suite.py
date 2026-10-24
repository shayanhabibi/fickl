"""External-workload watch probe: body edits of one file in an isolated copy of a real project.

suite.py --compiler <fable.dll> --runtime-lib <lib> --project <fsproj inside --sandbox> --sandbox <root>
         --edit-file <file> --anchor <exact line> --scratch <fresh> [--edits 4] [--omit-watch-delay]
         [--compiler-arg ...]
Each edit appends --suffix (with @N@ = edit number) to the anchor: keep it body-only with an unchanged
signature, and make it emit code (Fable erases `ignore <literal>`). After the watcher exits, the final source state is compiled fresh and every generated .js
must match. The edit file is restored in a finally block."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import sys
import time

sys.path.insert(0, str(Path(__file__).resolve().parent))
import measure  # noqa: E402


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--compiler", type=Path, required=True)
    parser.add_argument("--runtime-lib", type=Path, required=True)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--sandbox", type=Path, required=True, help="Task-owned tree containing project and edit file")
    parser.add_argument("--edit-file", type=Path, required=True)
    parser.add_argument("--anchor", required=True)
    parser.add_argument(
        "--suffix",
        default="; ignore @N@",
        help="Appended to the anchor; @N@ is the edit number. Must change generated JS",
    )
    parser.add_argument("--scratch", type=Path, required=True)
    parser.add_argument("--edits", type=int, default=4)
    parser.add_argument("--delay", type=int, default=200, help="Passed unless --omit-watch-delay; sizes settling")
    parser.add_argument("--omit-watch-delay", action="store_true")
    parser.add_argument("--configuration", default="Release")
    parser.add_argument("--timeout", type=float, default=600)
    parser.add_argument("--no-restore", action="store_true")
    parser.add_argument("--compiler-arg", action="append", default=[])
    args = parser.parse_args(argv)
    scratch = args.scratch.resolve()
    scratch.mkdir(parents=True, exist_ok=False)
    sandbox = args.sandbox.resolve()
    project = measure.inside(args.project, sandbox)
    edit_file = measure.inside(args.edit_file, sandbox)
    args.compiler, args.runtime_lib = args.compiler.resolve(), args.runtime_lib.resolve()
    original = edit_file.read_bytes()
    text = original.decode("utf-8")
    if text.count(args.anchor) != 1:
        parser.error("Anchor must occur exactly once")
    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_USE_POLLING_FILE_WATCHER="0")
    watch_dir = scratch / "watch"
    cmd = measure.command(args, project, watch_dir, args.configuration, args.delay)
    result = {"command": cmd, "edits": [], "completed": False}
    try:
        watch = measure.Watch(cmd, project.parent, scratch / "stdout.jsonl", env)
        try:
            acquired = watch.ready(args.timeout)
            watch.record_tree()
            result["acquisition_ms"] = (acquired[-1]["received_ns"] - watch.started_ns) / 1e6
            for n in range(1, args.edits + 1):
                new_text = text.replace(args.anchor, args.anchor + args.suffix.replace("@N@", str(n)))
                with measure.Observer(watch_dir) as observer:
                    start = measure.stamp()
                    edit_file.write_text(new_text, encoding="utf-8", newline="")
                    end = measure.stamp()
                    events = watch.ready(args.timeout)
                    time.sleep(max(0.5, args.delay / 1000 + 0.3))
                    while not watch.events.empty():
                        events.append(watch.events.get())
                final = observer.snapshot_digests()
                ready, detail, rewrites = measure.readiness(observer.baseline, final, observer.raw)
                cycles = measure.parse_cycles(events)
                errors = [d for d in measure.diagnostics_of(e["line"] for e in events) if d["severity"] == "error"]
                if errors:
                    raise AssertionError(f"edit {n}: errors {errors}")
                result["edits"].append(
                    {
                        "n": n,
                        "write_ns": start,
                        "write_end_ns": end,
                        "cycles": cycles,
                        "edit_to_watching_ms": (cycles[0]["watching_ns"] - start) / 1e6,
                        "edit_to_output_ready_ms": None if ready is None else (ready - start) / 1e6,
                        "changed_outputs": sorted(detail),
                        "identical_rewrites": len(rewrites),
                        "compile_ms": cycles[0].get("compile_ms"),
                        "timings": cycles[0]["timings"],
                    }
                )
                if not detail:
                    raise AssertionError(f"edit {n}: no changed output observed")
        finally:
            watch.close()
            result["killed_tree"] = getattr(watch, "killed", None)
        fresh_dir = scratch / "fresh"
        fresh_cmd = measure.command(args, project, fresh_dir, args.configuration, fresh=True)
        code, output = measure.run_checked(fresh_cmd, project.parent, env, args.timeout, expect_success=False)
        (scratch / "fresh.log").write_text(output, encoding="utf-8")
        if code != 0:
            raise AssertionError("fresh compile failed")
        watch_hashes, fresh_hashes = measure.js_hashes(watch_dir), measure.js_hashes(fresh_dir)
        differing = sorted(k for k in fresh_hashes if watch_hashes.get(k) != fresh_hashes[k])
        extra = sorted(set(watch_hashes) - set(fresh_hashes))
        result["fresh_equivalence"] = {"files": len(fresh_hashes), "differing": differing, "watch_only": extra}
        result["validated"] = bool(fresh_hashes) and not differing and not extra
        result["completed"] = True
    finally:
        edit_file.write_bytes(original)
        (scratch / "results.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    if not result.get("validated"):
        raise SystemExit(f"watch/fresh differ: {result.get('fresh_equivalence')}")
    print(
        json.dumps(
            {
                "acquisition_ms": round(result["acquisition_ms"]),
                "edit_to_watching_ms": [round(e["edit_to_watching_ms"], 1) for e in result["edits"]],
                "compile_ms": [e["compile_ms"] for e in result["edits"]],
                "files": [e["timings"].get("files") for e in result["edits"]],
                "fresh_files": result["fresh_equivalence"]["files"],
            }
        )
    )


if __name__ == "__main__":
    main()
