"""Driver checks. Preparation tests never start a compiler; the process-tree tests start only
owned Python child trees and verify that exactly those exit."""

import contextlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import time
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import measure


def prepare(scratch, *extra):
    args = [
        "--compiler",
        "compiler.dll",
        "--runtime-lib",
        "missing-runtime",
        "--scratch",
        str(scratch),
        "--prepare-only",
        "--rounds",
        "2",
        *extra,
    ]
    with (
        patch.object(measure.subprocess, "Popen", side_effect=AssertionError("launched a subprocess")),
        patch.object(measure.subprocess, "run", side_effect=AssertionError("launched a subprocess")),
        contextlib.redirect_stdout(io.StringIO()),
    ):
        return measure.main(args)


class PreparationTests(unittest.TestCase):
    def test_plan_pins_identical_configuration_and_launches_nothing(self):
        with tempfile.TemporaryDirectory() as temporary:
            plan = prepare(Path(temporary))
            self.assertEqual([s["delay_ms"] for s in plan["sessions"]], [200, 50, 10, 10, 50, 200])
            cmd = plan["sessions"][0]["watch_command"]
            self.assertEqual(cmd[:3], ["rtk", "proxy", "dotnet"])
            self.assertEqual(cmd[cmd.index("--configuration") + 1], "Release")
            self.assertEqual(plan["fresh_configuration"], "Release")
            self.assertIn("--noPackageCache", cmd)
            fresh = measure.command(
                SimpleNamespace(compiler=Path("c.dll"), runtime_lib=Path("lib"), no_restore=False, compiler_arg=[]),
                Path("p.fsproj"),
                Path("o"),
                "Release",
                fresh=True,
            )
            self.assertEqual(fresh[fresh.index("--configuration") + 1], "Release")
            self.assertNotIn("--watch", fresh)
            self.assertTrue((Path(temporary) / "fixture" / "App.fsproj").exists())
            self.assertFalse((Path(temporary) / "results.json").exists())

    def test_nonpositive_delay_rejected(self):
        for delay in ("0", "-1"):
            with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as raised:
                measure.main(["--compiler", "u", "--runtime-lib", "u", "--scratch", "u", "--delays", delay])
            self.assertEqual(raised.exception.code, 2)

    def test_fixture_distinguishes_configuration_and_states(self):
        state = measure.initial_state()
        self.assertIn("#if DEBUG", measure.render(state)["App.fs"])
        self.assertNotEqual(measure.expected(state, "Debug"), measure.expected(state, "Release"))
        self.assertIsNone(measure.expected({**state, "input_error": True}, "Release"))
        self.assertIsNone(measure.expected({**state, "input_name": "valueRenamed"}, "Release"))
        for case, _, transform, names, mode, _ in measure.EDITS:
            before, after = measure.render(state), measure.render(transform(state))
            if mode == "noop":
                self.assertEqual(before, after, case)
            else:
                self.assertTrue(any(before[n] != after[n] for n in names), case)
            state = transform(state)

    def test_orphans_are_only_outputs_of_removed_sources(self):
        state = measure.initial_state()
        sources = set(measure.project_sources(state))
        self.assertIn("Input.fs", sources)
        self.assertNotIn("Extra.fs", sources)
        self.assertNotIn("Ref/Ref.fs", sources)
        self.assertIn("Ref/Ref.fs", measure.project_sources({**state, "reference": True}))
        self.assertIn("Extra.fs", measure.project_sources({**state, "extra": True}))

    def test_burst_writes_distinct_values_and_final_last(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            texts = [f"let value = {v}\n" for v in (31, 32, 33)]
            writes = measure.write_files(root, root, {}, ["Input.fs"], "burst", [31, 32, 33], texts)
            self.assertEqual([w["value"] for w in writes], [31, 32, 33])
            self.assertEqual((root / "Input.fs").read_text(), texts[-1])


class ReadinessTests(unittest.TestCase):
    def test_transient_writes_do_not_count_as_ready(self):
        raw = [
            {"file": "Input.js", "observed_ns": 10, "sha256": "empty"},
            {"file": "Input.js", "observed_ns": 20, "sha256": "partial"},
            {"file": "Input.js", "observed_ns": 30, "sha256": "final"},
            {"file": "App.js", "observed_ns": 15, "sha256": "same"},
        ]
        ready, detail, rewrites = measure.readiness(
            {"Input.js": "old", "App.js": "same"}, {"Input.js": "final", "App.js": "same"}, raw
        )
        self.assertEqual(ready, 30)
        self.assertEqual(len(detail["Input.js"]["transients"]), 2)
        self.assertEqual(rewrites, ["App.js"])

    def test_final_digest_must_be_stable_and_observed(self):
        raw = [
            {"file": "Input.js", "observed_ns": 10, "sha256": "final"},
            {"file": "Input.js", "observed_ns": 20, "sha256": "other"},
            {"file": "Input.js", "observed_ns": 30, "sha256": "final"},
        ]
        self.assertEqual(measure.readiness({"Input.js": "old"}, {"Input.js": "final"}, raw)[0], 30)
        with self.assertRaises(AssertionError):
            measure.readiness({"Input.js": "old"}, {"Input.js": "never"}, raw)

    def test_real_observer_records_partial_writes_separately(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            target = root / "Input.js"
            target.write_text("export const value = 0;\n")
            with measure.Observer(root, interval=0.001) as observer:
                for text in ("", "export const val", "export const value = 1;\n"):
                    target.write_text(text)
                    time.sleep(0.05)
            final = observer.snapshot_digests()
            ready, detail, _ = measure.readiness(observer.baseline, final, observer.raw)
            self.assertGreaterEqual(len(detail["Input.js"]["transients"]), 2)
            self.assertEqual(ready, max(r["observed_ns"] for r in observer.raw))

    def test_observer_stops_when_edit_throws(self):
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaises(RuntimeError):
                with measure.Observer(Path(temporary)) as observer:
                    raise RuntimeError("edit failed")
            self.assertFalse(observer.thread.is_alive())


class DiagnosticTests(unittest.TestCase):
    line = "./Input.fs(2,12): (2,33) error FSHARP: The value or constructor 'missingWatchProbeName' is not defined. (code 39)"

    def test_expected_undefined_name_is_required(self):
        found = measure.diagnostics_of([self.line])
        measure.check_diagnostics("error", found, [measure.UNDEFINED_INPUT])
        with self.assertRaises(AssertionError):
            measure.check_diagnostics("error", found, [measure.UNDEFINED_VALUE])
        with self.assertRaises(AssertionError):
            measure.check_diagnostics("error", [], [measure.UNDEFINED_INPUT])
        with self.assertRaises(AssertionError):
            measure.check_diagnostics("body", found, [])

    def test_unrelated_error_is_rejected(self):
        found = measure.diagnostics_of([self.line, "./App.fs(1,1): error FSHARP: Something else (code 1)"])
        with self.assertRaises(AssertionError):
            measure.check_diagnostics("error", found, [measure.UNDEFINED_INPUT])

    def test_suggestion_separator_stays_in_one_diagnostic(self):
        output = ".\\App.fs(2,20): (2,25) error FSHARP: not defined. Maybe:\x1d   valueRenamed (code 39)\r\nDone\r\n"
        found = measure.diagnostics_of(line.rstrip("\r") for line in output.split("\n"))
        self.assertEqual(len(found), 1)
        self.assertIn("valueRenamed", found[0]["message"])

    def test_cycle_parser(self):
        events = [
            {"received_ns": n, "line": l}
            for n, l in enumerate(
                [
                    "Started Fable compilation...",
                    "Fable compilation finished in 21ms",
                    "  FCS parse + type check (wall)          15 ms",
                    "  Fable per-file work (summed)            3 ms over 1 files (avg 3.0 ms, max 3.0 ms)",
                    "Watching .",
                ]
            )
        ]
        (cycle,) = measure.parse_cycles(events)
        self.assertEqual((cycle["compile_ms"], cycle["timings"]["files"], cycle["watching_ns"]), (21, 1, 4))


class PathTests(unittest.TestCase):
    def test_escape_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with self.assertRaises(ValueError):
                measure.inside(root / ".." / "outside.fs", root)
            outside = tempfile.mkdtemp()
            try:
                link = root / "link"
                try:
                    os.symlink(outside, link, target_is_directory=True)
                except OSError:
                    # Junctions need no privilege; they are the realistic Windows escape.
                    made = subprocess.run(
                        ["rtk", "proxy", "cmd", "/c", "mklink", "/J", str(link), outside],
                        capture_output=True,
                        text=True,
                    )
                    if made.returncode != 0:
                        self.skipTest(f"no symlink or junction: {made.stdout}{made.stderr}")
                with self.assertRaises(ValueError):
                    measure.inside(link / "file.fs", root)
            finally:
                if (root / "link").exists() or os.path.lexists(root / "link"):
                    os.rmdir(root / "link")  # removes the link itself, never the target's contents
                os.rmdir(outside)


CHILD = r"""
import subprocess, sys, time
if sys.argv[1] == "root":
    child = subprocess.Popen([sys.executable, __file__, "child"])
    print("root", flush=True)
    time.sleep(120)
else:
    time.sleep(120)
"""


@unittest.skipUnless(os.name == "nt", "Windows process-tree cleanup")
class ProcessTreeTests(unittest.TestCase):
    """Owned negative controls: a timed-out command leaves no descendant; a failed kill is an error."""

    def owned_script(self, directory):
        script = Path(directory) / "tree.py"
        script.write_text(CHILD)
        return ["rtk", "proxy", sys.executable, str(script), "root"]

    def test_timeout_kills_whole_owned_tree(self):
        with tempfile.TemporaryDirectory() as temporary:
            cmd = self.owned_script(temporary)
            members = []
            original = measure.kill_tree

            def recording(process, known=(), **kw):
                time.sleep(1.5)  # let the grandchild start
                found = measure.descendants(process.pid)
                members.extend(found)
                self.assertGreaterEqual(len(found), 3, found)  # rtk -> python -> python
                return original(process, known, **kw)

            with patch.object(measure, "kill_tree", recording):
                with self.assertRaises(TimeoutError):
                    measure.run_checked(cmd, temporary, dict(os.environ), timeout=2)
            table = measure._windows_processes()
            self.assertEqual([m for m in members if table.get(m[0], (0, 0))[1] == m[1]], [])

    def test_failed_kill_is_not_ignored(self):
        with tempfile.TemporaryDirectory() as temporary:
            process = subprocess.Popen(self.owned_script(temporary), stdout=subprocess.PIPE, text=True)
            try:
                process.stdout.readline()
                fake = lambda pid: SimpleNamespace(returncode=128, stdout="", stderr="simulated failure")
                with self.assertRaises(RuntimeError):
                    measure.kill_tree(process, kill=fake, timeout=2)
            finally:
                killed = measure.kill_tree(process)
                process.stdout.close()
                self.assertGreaterEqual(len(killed), 3)


if __name__ == "__main__":
    unittest.main()
