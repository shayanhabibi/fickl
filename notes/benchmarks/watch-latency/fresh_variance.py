"""Fresh-vs-fresh nondeterminism: compile the same project N times (fresh outDir, --noCache) and report
per-file digest variants. Usage: fresh_variance.py <compiler> <runtime-lib> <fsproj> <scratch> <n> [compiler args]"""

import collections
import json
import os
from pathlib import Path
import sys
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
import measure  # noqa: E402


def main(compiler, runtime_lib, project, scratch, n, *extra):
    scratch = Path(scratch).resolve()
    scratch.mkdir(parents=True, exist_ok=False)
    args = SimpleNamespace(
        compiler=Path(compiler).resolve(),
        runtime_lib=Path(runtime_lib).resolve(),
        no_restore=False,
        compiler_arg=list(extra),
    )
    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1")
    runs = []
    for i in range(int(n)):
        out = scratch / f"fresh-{i:02d}"
        code, log = measure.run_checked(
            measure.command(args, Path(project).resolve(), out, "Release", fresh=True),
            Path(project).resolve().parent,
            env,
            900,
            expect_success=False,
        )
        (scratch / f"fresh-{i:02d}.log").write_text(log, encoding="utf-8")
        if code != 0:
            raise SystemExit(f"fresh {i} failed")
        runs.append(measure.js_hashes(out))
    files = sorted(set().union(*runs))
    variants = {f: collections.Counter(r.get(f, "<missing>")[:12] for r in runs) for f in files}
    unstable = {f: dict(c) for f, c in variants.items() if len(c) > 1}
    layouts = collections.Counter(tuple(sorted(r)) for r in runs)
    report = {"runs": len(runs), "files": len(files), "unstable": unstable, "layouts": len(layouts)}
    (scratch / "variance.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=1))


if __name__ == "__main__":
    main(*sys.argv[1:])
