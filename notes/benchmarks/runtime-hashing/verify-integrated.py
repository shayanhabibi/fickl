"""Snapshot coherent runtimes and compare actual generated candidate modules.

Only writes below the repository's ignored temp/runtime-resync directory.
No timings, source overlays, compiler changes, or report updates.
"""

from pathlib import Path
import argparse
import hashlib
import json
import re
import shutil
import subprocess

DEFAULT_NOTES = Path(__file__).resolve().parent
REPO = next(p for p in DEFAULT_NOTES.parents if (p / "src/fable-library-ts").is_dir())
HERE = REPO / "temp/runtime-resync"
HERE.mkdir(parents=True, exist_ok=True)


def digest_tree(path):
    return {
        str(p.relative_to(path)): hashlib.sha256(p.read_bytes()).hexdigest()
        for p in sorted(path.rglob("*"))
        if p.is_file()
    }


def snapshot(source, name):
    source = source.resolve()
    for module in ["Util", "Types", "List", "Seq", "Option", "MutableMap", "MutableSet", "String", "Double"]:
        if not (source / f"{module}.js").is_file():
            raise SystemExit(f"Missing generated runtime module: {source / module}")
    target = HERE / name
    if target.exists():
        raise SystemExit(f"{target} already exists; choose a new --name to preserve evidence")
    shutil.copytree(source, target)
    # Standalone snapshots must keep ES-module interpretation outside the source package.
    (target / "package.json").write_text('{"type":"module"}\n', encoding="utf-8")
    manifest = {"source": str(source), "snapshot": str(target), "files": digest_tree(target)}
    (HERE / f"{name}-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Saved coherent {name}: {len(manifest['files'])} files from {source}")


def prepare_checks(notes, baseline, candidate, run):
    run.mkdir()

    def read(relative):
        return (notes / relative).read_text(encoding="utf-8")

    def write(name, code):
        (run / name).write_text(code, encoding="utf-8")

    def url(path):
        return path.resolve().as_uri()

    # Each historical candidate label now uses the SAME actual combined production output.
    hashing = read("correctness.mjs")
    hashing = "import {writeFileSync} from 'node:fs';\nconst integrationTraces=[];\n" + hashing
    hashing = hashing.replace(
        "function check(a,b,label) { assert.deepEqual(a,b,label); assertions++; }",
        "function check(a,b,label) { assert.deepEqual(a,b,label); assertions++; if (/\\/(snapshot|getters|getter-throws|hash-throws|delete|resize|keys)$/.test(label)) integrationTraces.push({label,baseline:b,candidate:a}); }",
    )
    hashing = hashing.replace("const root = resolve(process.argv[2] ?? 'temp/runtime-hashing');", "")
    hashing = hashing.replace(
        "`${root}/${name}/js/Util.js`",
        f"name === 'baseline' ? {json.dumps(str(baseline / 'Util.js'))} : {json.dumps(str(candidate / 'Util.js'))}",
    )
    hashing = hashing.replace(
        "`${root}/${name}/js/Types.js`",
        f"name === 'baseline' ? {json.dumps(str(baseline / 'Types.js'))} : {json.dumps(str(candidate / 'Types.js'))}",
    )
    hashing += "\nwriteFileSync(new URL('./hashing-traces.json',import.meta.url),JSON.stringify({integration:'all labels use actual combined candidate',assertions,traces:integrationTraces},null,2));\n"
    write("hashing.mjs", hashing)

    # Count actual constructors in module copies; all imports stay local to their own snapshot.
    for variant in [baseline, candidate]:
        code = (variant / "List.js").read_text(encoding="utf-8")
        counted, n = re.subn(
            r"(constructor\(head, tail\)\s*\{\s*super\(\);)",
            r"\1\n globalThis.listAllocations = (globalThis.listAllocations || 0) + 1;",
            code,
        )
        if n != 1:
            raise SystemExit(f"Expected exactly one FSharpList constructor in {variant}; found {n}")
        # Verification artifacts live in run/, and imports resolve back into coherent snapshot.
        counted = re.sub(r"(from\s+[\'\"])(\./[^\'\"]+)([\'\"])", lambda m: m[1] + url(variant / m[2]) + m[3], counted)
        write(f"List.{variant.name}.counted.mjs", counted)

    listing = read("linear-collections/list-correctness.mjs")
    replacements = {
        "./isolated/runtime/List.baseline.js": url(baseline / "List.js"),
        "./isolated/runtime/List.candidate.js": url(candidate / "List.js"),
        "./isolated/runtime/List.baseline.counted.js": f"./List.{baseline.name}.counted.mjs",
        "./isolated/runtime/List.candidate.counted.js": f"./List.{candidate.name}.counted.mjs",
        "./isolated/runtime/Array.js": url(candidate / "Array.js"),
    }
    for old, new in replacements.items():
        listing = listing.replace(old, new)
    write("list.mjs", listing)

    sequence = read("linear-collections/seq-correctness.mjs")
    for old, new in {
        "./isolated/runtime/Seq.baseline.js": url(baseline / "Seq.js"),
        "./isolated/runtime/Seq.candidate.js": url(candidate / "Seq.js"),
        "./isolated/runtime/Option.js": url(baseline / "Option.js"),
        "./isolated/runtime/List.js": url(baseline / "List.js"),
    }.items():
        sequence = sequence.replace(old, new)
    write("seq.mjs", sequence)

    buckets = read("keyed-collections/hash.mjs")
    start = buckets.index("const root=new URL(")
    end = buckets.index("const create=", start)
    imports = (
        "const mods=await Promise.all("
        + json.dumps([url(baseline) + "/", url(candidate) + "/"])
        + ".map(async root=>({map:await import(new URL('MutableMap.js',root)),set:await import(new URL('MutableSet.js',root))})));\n"
    )
    buckets = buckets[:start] + imports + buckets[end:]
    buckets = buckets.replace(
        "traceCalls:a.trace.length,matched:true", "traceCalls:a.trace.length,matched:true,observed:a"
    )
    write("buckets.mjs", buckets)

    # Reuse all semantic cases, but import whole actual runtime modules instead of synthesizing
    # candidate function bodies or extracting source. Exclude every benchmark code path.
    strings = read("string-helpers/experiment.mjs")
    start = strings.index("  let seed=912740;")
    end = strings.index("} else if(mode==='bench')")
    header = "import assert from 'node:assert/strict';\nimport {writeFileSync} from 'node:fs';\nimport {resolve} from 'node:path';\n"
    header += f"const here={json.dumps(str(run))},metadata={{integration:true}},checksStart=0;\n"
    header += f"import {{filter as b}} from {json.dumps(url(baseline / 'String.js'))};\nimport {{filter as c}} from {json.dumps(url(candidate / 'String.js'))};\nlet checks=0;\n"
    write("string.mjs", header + strings[start:end])

    numeric = read("numeric-helpers/experiment.mjs")
    start_inputs = numeric.index("const inputs=")
    end_inputs = numeric.index("const mode=", start_inputs)
    start = numeric.index("  let seed=74253;")
    end = numeric.index("} else if(mode==='bench')")
    body = numeric[start:end]
    body = body.replace(
        "  const {FSharpRef}=await import(pathToFileURL(resolve(main,'temp/fable-library-js/Types.js')));", ""
    )
    body = body.replace(
        "const br=new FSharpRef(987),cr=new FSharpRef(987);", "const br=new BRef(987),cr=new CRef(987);"
    )
    header = "import assert from 'node:assert/strict';\nimport {writeFileSync} from 'node:fs';\nimport {resolve} from 'node:path';\n"
    header += f"const here={json.dumps(str(run))},metadata={{integration:true}};\n"
    header += f"import * as b from {json.dumps(url(baseline / 'Double.js'))};\nimport * as c from {json.dumps(url(candidate / 'Double.js'))};\n"
    header += f"import {{FSharpRef as BRef}} from {json.dumps(url(baseline / 'Types.js'))};\nimport {{FSharpRef as CRef}} from {json.dumps(url(candidate / 'Types.js'))};\n"
    write("numeric.mjs", header + numeric[start_inputs:end_inputs] + body)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    save = commands.add_parser("save")
    save.add_argument("--runtime", type=Path, default=REPO / "temp/fable-library-js")
    save.add_argument("--name", default="baseline")
    check = commands.add_parser("check")
    check.add_argument("--runtime", type=Path, default=REPO / "temp/fable-library-js")
    check.add_argument("--baseline", default="baseline")
    check.add_argument("--name", default="candidate")
    check.add_argument("--notes", type=Path, default=DEFAULT_NOTES)
    args = parser.parse_args()
    for name in [args.name, getattr(args, "baseline", "baseline")]:
        if not re.fullmatch(r"[A-Za-z0-9_-]+", name):
            raise SystemExit("Snapshot names must be simple letters/digits/dash/underscore")
    if args.command == "save":
        snapshot(args.runtime, args.name)
        return
    baseline = HERE / args.baseline
    manifest = json.loads((HERE / f"{args.baseline}-manifest.json").read_text(encoding="utf-8"))
    if digest_tree(baseline) != manifest["files"]:
        raise SystemExit("Saved baseline changed since capture")
    snapshot(args.runtime, args.name)
    candidate = HERE / args.name
    run = HERE / f"checks-{args.name}"
    prepare_checks(args.notes.resolve(), baseline, candidate, run)
    for script, options in [
        ("hashing.mjs", ["--allocation-gate"]),
        ("list.mjs", []),
        ("seq.mjs", []),
        ("buckets.mjs", ["--check-only"]),
        ("string.mjs", []),
        ("numeric.mjs", []),
    ]:
        print(f"Checking {script}", flush=True)
        subprocess.run(["rtk", "node", str(run / script), *options], check=True, cwd=REPO)
    if digest_tree(baseline) != manifest["files"]:
        raise SystemExit("Baseline modified during verification")
    print("PASS: coherent combined runtime integration checks; no timings performed")


if __name__ == "__main__":
    main()
