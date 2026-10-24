"""Prepare isolated generated-runtime experiments; never edits source/runtime inputs."""

from pathlib import Path
import difflib, hashlib, json, shutil, sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[5]
RUNTIME = ROOT / "temp/fable-library-js"
OUT = HERE / "temp"
area = sys.argv[1]
files = ["Map", "Set"] if area == "ordered" else ["MutableMap", "MutableSet"]
manifest = {"area": area, "inputs": {}}
for variant in ["baseline", "candidate"]:
    dest = OUT / area / variant
    shutil.copytree(RUNTIME, dest, dirs_exist_ok=True)
    (dest / "package.json").write_text('{"type":"module"}')
    for name in files:
        src = (RUNTIME / (name + ".js")).read_text(encoding="utf-8")
        original = src
        fs = ROOT / "src/fable-library-ts" / (name + ".fs")
        manifest["inputs"][name] = {
            "generatedSha256": hashlib.sha256(src.encode()).hexdigest(),
            "sourceSha256": hashlib.sha256(fs.read_bytes()).hexdigest(),
        }
        if variant == "candidate":
            if area == "ordered":
                old = "        this.tree = tree;"
                assert src.count(old) == 1
                src = src.replace(old, old + "\n        this.countCache = -1;")
                arg, expr = (
                    ("m", "MapTreeModule_size(m.tree)")
                    if name == "Map"
                    else ("s", "SetTreeModule_count(FSharpSet__get_Tree(s))")
                )
                old = f"export function FSharp{name}__get_Count({arg}) {{\n    return {expr} | 0;\n}}"
                new = f"export function FSharp{name}__get_Count({arg}) {{\n    if ({arg}.countCache < 0) {arg}.countCache = {expr} | 0;\n    return {arg}.countCache | 0;\n}}"
                assert src.count(old) == 1
                src = src.replace(old, new)
            else:
                prefix = "Dictionary" if name == "MutableMap" else "HashSet"
                start = src.index(f"function {prefix}__TryFindIndex_2B595(")
                end = src.index("\n}", start) + 2
                old = src[start:end]
                element = "pair[0]" if name == "MutableMap" else "pair"
                new = f"""function {prefix}__TryFindIndex_2B595(this$, k) {{
    const h = this$.comparer.GetHashCode(k) | 0;
    const bucket = this$.hashMap.get(h);
    if (bucket !== undefined) {{
        return [true, h, bucket.findIndex((pair) => this$.comparer.Equals(k, {element}))];
    }}
    return [false, h, -1];
}}"""
                assert "tryGetValue" in old and ".findIndex(" in old
                src = src[:start] + new + src[end:]
            (dest / (name + ".js")).write_text(src, encoding="utf-8")
            (HERE / (name + ".generated.patch")).write_text(
                "".join(
                    difflib.unified_diff(
                        original.splitlines(True),
                        src.splitlines(True),
                        fromfile="baseline/" + name + ".js",
                        tofile="candidate/" + name + ".js",
                    )
                ),
                encoding="utf-8",
            )
(HERE / (area + "-manifest.json")).write_text(json.dumps(manifest, indent=2))
print(OUT / area)
