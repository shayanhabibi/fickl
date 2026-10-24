"""Non-watch parity for orphan outputs: compile a state with Extra.fs and a project reference into an
outDir, then a state without them into the same outDir (no watch, no cache), and report leftovers.
Usage: orphan_parity.py <compiler> <runtime-lib> <scratch>"""

import json
import os
from pathlib import Path
import sys
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
import measure  # noqa: E402


def main(compiler, runtime_lib, scratch):
    scratch = Path(scratch).resolve()
    scratch.mkdir(parents=True, exist_ok=False)
    (scratch / "package.json").write_text('{"type":"module"}\n', encoding="utf-8")
    project = scratch / "fixture"
    project.mkdir()
    args = SimpleNamespace(
        compiler=Path(compiler).resolve(), runtime_lib=Path(runtime_lib).resolve(), no_restore=False, compiler_arg=[]
    )
    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1")
    out = scratch / "out"
    with_all = {**measure.initial_state(), "extra": True, "reference": True}
    results = {}
    for label, state in (("with", with_all), ("without", measure.initial_state())):
        measure.materialize(project, scratch, state)
        code, log = measure.run_checked(
            measure.command(args, project / "App.fsproj", out, "Release", fresh=True),
            project,
            env,
            600,
            expect_success=False,
        )
        (scratch / f"{label}.log").write_text(log, encoding="utf-8")
        results[label] = {"exit": code, "files": sorted(measure.js_hashes(out))}
    leftover = sorted(
        set(results["with"]["files"])
        & set(results["without"]["files"]) - {f[:-3] + ".js" for f in measure.project_sources(measure.initial_state())}
    )
    report = {**results, "orphans_left_by_non_watch_compile": leftover}
    (scratch / "orphan-parity.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"exits": [results["with"]["exit"], results["without"]["exit"]], "orphans": leftover}))


if __name__ == "__main__":
    main(*sys.argv[1:4])
