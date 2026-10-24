from pathlib import Path
import subprocess, json, hashlib, difflib

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[5]
WORKTREE = HERE.parents[3]
compiler = WORKTREE / "temp/release-0.5.3/verified-package/tools/net10.0/win-x64/fable.exe"
dest = HERE / "temp/baseline"
run = subprocess.run(
    ["rtk", "dotnet", "fsi", str(HERE / "check.fsx")], cwd=ROOT, text=True, capture_output=True, check=True
)
(HERE / "dotnet-results.json").write_text(
    json.dumps({"exitCode": run.returncode, "stdout": run.stdout, "stderr": run.stderr}, indent=2)
)
run = subprocess.run(
    [
        "rtk",
        "proxy",
        str(compiler),
        str(HERE / "StructFixture.fsproj"),
        "--outDir",
        str(dest),
        "--fableLib",
        str(ROOT / "temp/fable-library-js"),
        "--noCache",
        "--noPackageCache",
    ],
    cwd=ROOT,
    text=True,
    capture_output=True,
    check=True,
)
(HERE / "compile-results.json").write_text(
    json.dumps({"exitCode": run.returncode, "stdout": run.stdout, "stderr": run.stderr}, indent=2)
)
source = (dest / "StructFixture.js").read_text(encoding="utf-8")
(HERE / "Baseline.generated.js").write_text(source, encoding="utf-8")
old = """        const p = new Point(checksum + i, checksum - i);
        const q = new Point(p.Y + 3, p.Y);
        checksum = (((q.X ^ p.X) + q.Y) | 0);"""
new = """        const pX = (checksum + i) | 0;
        const pY = (checksum - i) | 0;
        const qX = (pY + 3) | 0;
        const qY = pY;
        checksum = (((qX ^ pX) + qY) | 0);"""
assert source.count(old) == 1
candidate = source.replace(old, new)
(HERE / "scalar.generated.patch").write_text(
    "".join(
        difflib.unified_diff(
            source.splitlines(True),
            candidate.splitlines(True),
            fromfile="baseline/StructFixture.js",
            tofile="candidate/StructFixture.js",
        )
    )
)
out = HERE / "temp/candidate"
out.mkdir(parents=True, exist_ok=True)
(out / "StructFixture.js").write_text(candidate, encoding="utf-8")
for folder in [dest, out]:
    (folder / "package.json").write_text('{"type":"module"}')
paths = [
    HERE / "StructFixture.fs",
    compiler,
    ROOT / "src/Fable.Transforms/FSharp2Fable.Util.fs",
    ROOT / "src/Fable.Transforms/Babel/Fable2Babel.fs",
]
(HERE / "manifest.json").write_text(
    json.dumps({str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}, indent=2)
)
print("Compiled actual baseline; isolated scalar candidate prepared.")
