from pathlib import Path
import hashlib, json

HERE = Path(__file__).resolve().parent
ROOT = HERE / "isolated/runtime"
s = (ROOT / "Seq.js").read_text()
old = """export function filter(f, xs) {
    return choose((x) => {
        if (f(x)) {
            return some(x);
        }
        else {
            return undefined;
        }
    }, xs);
}"""
new = """export function filter(f, xs) {
    return generate(() => ofSeq(xs), (e) => {
        while (e["System.Collections.IEnumerator.MoveNext"]()) {
            const x = e["System.Collections.Generic.IEnumerator`1.get_Current"]();
            if (f(x)) {
                return some(x);
            }
        }
        return undefined;
    }, (e) => { disposeSafe(e); });
}"""
assert s.count(old) == 1
(ROOT / "Seq.baseline.js").write_text(s)
(ROOT / "Seq.candidate.js").write_text(s.replace(old, new))
(HERE / "seq-candidate.js.txt").write_text(new + "\n")
MAIN = next(p for p in HERE.parents if p.name == "fable" and (p / "src").exists())
(HERE / "seq-manifest.json").write_text(
    json.dumps(
        {
            str(p.relative_to(MAIN)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in [MAIN / "src/fable-library-ts/Seq.fs", MAIN / "temp/fable-library-js/Seq.js"]
        },
        indent=2,
    )
)
print("Prepared sequence variants from isolated actual generated runtime.")
