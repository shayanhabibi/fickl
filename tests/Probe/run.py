"""Runs the probe on .NET and as JavaScript, then lists the checks whose results differ.

    python tests/Probe/run.py [--compiler <fable.dll or command>] [--out <dir>]

Each check prints `name=value`. `True`/`False` against `true`/`false` (bool ToString, #17) and a specific
exception type against a plain JS `Error` are reported separately, not as differences.
"""

import argparse
import json
import pathlib
import shutil
import subprocess
import sys

here = pathlib.Path(__file__).resolve().parent
root = here.parents[1]


def run(cmd, **kw):
    return subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace", **kw)


def load(text):
    results = {}
    for line in text.splitlines():
        if "=" in line:
            key, value = line.split("=", 1)
            results.setdefault(key, value)
    return results


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser()
    parser.add_argument("--compiler", default=str(root / "src/Fable.Cli/bin/Release/net10.0/fable.dll"))
    parser.add_argument("--out", default=str(root / "temp/probe"))
    args = parser.parse_args()
    out = pathlib.Path(args.out)
    shutil.rmtree(out, ignore_errors=True)
    out.mkdir(parents=True)

    build = run(["dotnet", "build", "-c", "Release", "-v", "q", str(here / "Probe.fsproj")])
    if build.returncode != 0:
        sys.exit(build.stdout + build.stderr)
    dll = next(here.glob("bin/Release/*/Probe.dll"))
    dotnet = load(run(["dotnet", str(dll)]).stdout)

    compiler = ["dotnet", args.compiler] if args.compiler.endswith(".dll") else [args.compiler]
    compiled = run(compiler + [str(here), "-o", str(out / "js"), "--noCache"])
    (out / "compile.log").write_text(compiled.stdout + compiled.stderr, encoding="utf-8")
    if compiled.returncode != 0:
        print(f"compilation failed, see {out / 'compile.log'}")
    (out / "js" / "package.json").write_text(json.dumps({"type": "module"}))
    node = run(["node", str(out / "js" / "Main.js")])
    js = load(node.stdout)
    if node.returncode != 0:
        print(node.stderr.strip().splitlines()[-1] if node.stderr.strip() else "node failed")

    differences, bools, exceptions = [], [], []
    for key, expected in dotnet.items():
        actual = js.get(key)
        if actual == expected:
            continue
        if actual == expected.replace("True", "true").replace("False", "false"):
            bools.append(key)
        elif expected.startswith("throws ") and actual == "throws Exception":
            exceptions.append(key)
        else:
            differences.append(f"{key}: .NET {expected!r}, JS {actual!r}")
    print("\n".join(differences))
    print(f"\n{len(differences)} differences of {len(dotnet)} checks")
    print(f"bool formatting only: {len(bools)}; exception type only: {' '.join(exceptions)}")


main()
