"""Capture the .NET correctness reference without PowerShell pipeline exit ambiguity."""

from pathlib import Path
import json
import subprocess

here = Path(__file__).resolve().parent
run = subprocess.run(
    ["rtk", "proxy", "dotnet", "fsi", "--exec", str(here / "Records.fsx")], capture_output=True, text=True
)
(here / "records-dotnet-results.json").write_text(
    json.dumps({"exit_code": run.returncode, "stdout": run.stdout, "stderr": run.stderr}, indent=2)
)
print(run.stdout)
raise SystemExit(run.returncode)
