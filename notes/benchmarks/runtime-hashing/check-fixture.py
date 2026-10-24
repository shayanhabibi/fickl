"""Run already-compiled F# fixture against each isolated runtime variant."""

import json
from pathlib import Path
import shutil
import subprocess

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
DEST = ROOT / "temp/runtime-hashing"
results = {}
try:
    for variant in ["baseline", "array", "record", "object", "combined"]:
        for name in ["Util.js", "Types.js"]:
            shutil.copy2(DEST / variant / "js" / name, DEST / "full-runtime" / name)
        run = subprocess.run(
            ["rtk", "proxy", "node", str(DEST / "fixture-js/Fixture.js")], text=True, cwd=ROOT, capture_output=True
        )
        results[variant] = {"exit_code": run.returncode, "stdout": run.stdout.strip()}
        print(variant, results[variant])
        if variant != "baseline" and results[variant] != results["baseline"]:
            raise RuntimeError("Candidate changed fixture outcomes: " + variant)
finally:
    for name in ["Util.js", "Types.js"]:
        shutil.copy2(DEST / "baseline/js" / name, DEST / "full-runtime" / name)
(HERE / "fixture-results.json").write_text(json.dumps(results, indent=2))
