"""Build isolated runtime variants from the current source; never edits production files."""

import argparse
import difflib
import hashlib
import json
from pathlib import Path
import subprocess

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--source", type=Path, default=ROOT.parent.parent)
parser.add_argument("--baseline-only", action="store_true")
args = parser.parse_args()
source = args.source.resolve()
dest = ROOT / "temp/runtime-hashing"
original = {
    name: (source / "src/fable-library-ts" / name).read_text(encoding="utf-8") for name in ["Util.ts", "Types.ts"]
}

array_old = """  const hashes: number[] = new Array(len);
  for (let i = 0; i < len; i++) {
    hashes[i] = structuralHash(x[i]);
  }
  return combineHashCodes(hashes);"""
array_new = """  let hash = 0;
  for (let i = 0; i < len; i++) {
    const elementHash = structuralHash(x[i]);
    hash = ((hash << 5) + hash) ^ elementHash;
  }
  return hash;"""
record_old = """  const hashes = Object.values(self as any).map((v) => structuralHash(v));
  return combineHashCodes(hashes);"""
record_new = """  // Snapshot all values before calling any user-defined hash functions.
  const values = Object.values(self as any);
  let hash = 0;
  for (let i = 0; i < values.length; i++) {
    const fieldHash = structuralHash(values[i]);
    hash = ((hash << 5) + hash) ^ fieldHash;
  }
  return hash;"""
object_old = """        const hashes = Object.values(x).map((v) => structuralHash(v));
        return combineHashCodes(hashes);"""
object_new = """        // Snapshot values before user hashing can mutate the object.
        const values = Object.values(x);
        let hash = 0;
        for (let i = 0; i < values.length; i++) {
          const fieldHash = structuralHash(values[i]);
          hash = ((hash << 5) + hash) ^ fieldHash;
        }
        return hash;"""


def replace(text, old, new):
    if text.count(old) != 1:
        raise RuntimeError("Expected exactly one source match; inspect changed baseline")
    return text.replace(old, new)


for variant in ["baseline", "array", "record", "object", "combined"]:
    files = dict(original)
    if not args.baseline_only:
        if variant in ["array", "combined"]:
            files["Util.ts"] = replace(files["Util.ts"], array_old, array_new)
        if variant in ["record", "combined"]:
            files["Types.ts"] = replace(files["Types.ts"], record_old, record_new)
        if variant in ["object", "combined"]:
            files["Util.ts"] = replace(files["Util.ts"], object_old, object_new)
    directory = dest / variant
    directory.mkdir(parents=True, exist_ok=True)
    for name, content in files.items():
        (directory / name).write_text(content, encoding="utf-8")
    (directory / "package.json").write_text('{"type":"module"}')
    (directory / "tsconfig.json").write_text(
        json.dumps(
            dict(
                compilerOptions=dict(
                    target="es2020",
                    module="es2020",
                    moduleResolution="bundler",
                    strict=True,
                    types=[],
                    rewriteRelativeImportExtensions=True,
                    outDir="js",
                    noEmitOnError=True,
                ),
                files=["Util.ts", "Types.ts"],
            )
        )
    )
    subprocess.run(
        [
            "rtk",
            "proxy",
            "node",
            str(source / "node_modules/typescript/bin/tsc"),
            "-p",
            str(directory / "tsconfig.json"),
        ],
        check=True,
    )
    if variant != "baseline" and not args.baseline_only:
        patch = "".join(
            "".join(
                difflib.unified_diff(
                    original[name].splitlines(True),
                    files[name].splitlines(True),
                    fromfile="a/src/fable-library-ts/" + name,
                    tofile="b/src/fable-library-ts/" + name,
                )
            )
            for name in files
            if files[name] != original[name]
        )
        (HERE / f"{variant}.patch").write_text(patch, encoding="utf-8")
if not args.baseline_only:
    manifest = dict(
        source=str(source),
        commit=subprocess.check_output(["rtk", "proxy", "git", "rev-parse", "HEAD"], cwd=source, text=True).strip(),
        source_sha256={k: hashlib.sha256(v.encode()).hexdigest() for k, v in original.items()},
        node=subprocess.check_output(["rtk", "proxy", "node", "--version"], text=True).strip(),
    )
    (HERE / "manifest.json").write_text(json.dumps(manifest, indent=2))
print(f"Prepared variants under {dest}")
