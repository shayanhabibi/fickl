from pathlib import Path
import hashlib, json, shutil

HERE = Path(__file__).resolve().parent
MAIN = next(p for p in HERE.parents if (p / "src/fable-library-ts/List.fs").exists() and p.name == "fable")
OUT = HERE / "isolated"
OUT.mkdir(exist_ok=True)
source = MAIN / "temp/fable-library-js"
shutil.copytree(source, OUT / "runtime", dirs_exist_ok=True)
original = (source / "List.js").read_text()
old = """export function append(xs, ys) {
    return fold((acc, x) => FSharpList_Cons_305B8EAC(x, acc), ys, reverse(xs));
}"""
new = """export function append(xs, ys) {
    const root = FSharpList_get_Empty();
    let node = root;
    let rest = xs;
    while (!FSharpList__get_IsEmpty(rest)) {
        const next = new FSharpList(head(rest), undefined);
        node.tail = next;
        node = next;
        rest = FSharpList__get_Tail(rest);
    }
    node.tail = ys;
    return FSharpList__get_Tail(root);
}"""
assert original.count(old) == 1
candidate = original.replace(old, new)
for name, code in [("List.baseline", original), ("List.candidate", candidate)]:
    (OUT / "runtime" / (name + ".js")).write_text(code)
    counted = code.replace(
        "constructor(head, tail) {\n        super();",
        "constructor(head, tail) {\n        super();\n        globalThis.listAllocations = (globalThis.listAllocations || 0) + 1;",
    )
    assert counted != code
    (OUT / "runtime" / (name + ".counted.js")).write_text(counted)
(HERE / "manifest.json").write_text(
    json.dumps(
        {
            "files": {
                str(p.relative_to(MAIN)): hashlib.sha256(p.read_bytes()).hexdigest()
                for p in [
                    MAIN / "src/fable-library-ts/List.fs",
                    MAIN / "src/fable-library-ts/Array.fs",
                    source / "List.js",
                    source / "Array.js",
                ]
            },
            "runtimeFiles": {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in source.glob("*.js")},
        },
        indent=2,
    )
)
(HERE / "list-candidate.js.txt").write_text(new + "\n")
print("Prepared isolated list baseline/candidate and allocation-instrumented variants.")
