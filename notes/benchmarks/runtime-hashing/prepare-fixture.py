"""Prepare the integration fixture using the existing generated full JS runtime."""

from pathlib import Path
import json
import shutil
import subprocess

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
SOURCE = ROOT.parent.parent
DEST = ROOT / "temp/runtime-hashing"
runtime = DEST / "full-runtime"
shutil.copytree(SOURCE / "temp/fable-library-js", runtime, dirs_exist_ok=True)
(runtime / "package.json").write_text('{"type":"module"}')
compiler = ROOT / "temp/release-0.5.3/verified-package/tools/net10.0/win-x64/fable.exe"
subprocess.run(
    [
        "rtk",
        "proxy",
        str(compiler),
        str(HERE / "Fixture.fsproj"),
        "--outDir",
        str(DEST / "fixture-js"),
        "--fableLib",
        str(runtime),
        "--noCache",
        "--noPackageCache",
        "--noRestore",
    ],
    cwd=ROOT,
    check=True,
)
run = subprocess.run(
    ["rtk", "proxy", "node", str(DEST / "fixture-js/Fixture.js")], cwd=ROOT, text=True, capture_output=True
)
(HERE / "original-runtime-fixture.json").write_text(
    json.dumps(
        {"exit_code": run.returncode, "stdout": run.stdout.strip(), "runtime": str(SOURCE / "temp/fable-library-js")},
        indent=2,
    )
)
print("Original generated runtime:", run.returncode, run.stdout.strip())
