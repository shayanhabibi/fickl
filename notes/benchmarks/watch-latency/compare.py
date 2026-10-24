"""Paired comparison of watch runs: compare.py <runs-dir> <tag> (folders <tag>-base-rN, <tag>-cand-rN).

Per case: median edit->Watching and edit->final-output over rounds, per-round paired differences,
validation and cycle counts. Warm-up: the first edit of each session (JIT) is reported, not excluded."""

import json
import statistics
import sys
from pathlib import Path


def load(root):
    result = json.loads((root / "results.json").read_text(encoding="utf-8"))
    session = result["sessions"][0]
    return session, {e["case"]: e for e in session["edits"]}


def main(runs, tag):
    runs = Path(runs)
    rounds = sorted({p.name.rsplit("-r", 1)[1] for p in runs.glob(f"{tag}-base-r*") if p.is_dir()}, key=int)
    pairs = []
    for r in rounds:
        pairs.append((load(runs / f"{tag}-base-r{r}"), load(runs / f"{tag}-cand-r{r}")))
    print("rounds", len(pairs), "validated", [(b[0]["validated"], c[0]["validated"]) for b, c in pairs])
    print(
        "acquisition ms base",
        [round(b[0]["watch_acquisition_ms"]) for b, _ in pairs],
        "cand",
        [round(c[0]["watch_acquisition_ms"]) for _, c in pairs],
    )
    cases = list(pairs[0][0][1])
    print(f"{'case':<17}{'base':>8}{'cand':>8}{'diff':>8}  {'paired diffs':<40} {'out b':>6}{'out c':>6} cycles b/c")
    all_diffs = []
    for case in cases:
        b = [p[0][1][case]["edit_to_watching_ms"] for p in pairs]
        c = [p[1][1][case]["edit_to_watching_ms"] for p in pairs]
        d = [y - x for x, y in zip(b, c)]
        ob = [p[0][1][case]["edit_to_output_ready_ms"] for p in pairs if p[0][1][case]["edit_to_output_ready_ms"]]
        oc = [p[1][1][case]["edit_to_output_ready_ms"] for p in pairs if p[1][1][case]["edit_to_output_ready_ms"]]
        cyc = (sum(len(p[0][1][case]["cycles"]) for p in pairs), sum(len(p[1][1][case]["cycles"]) for p in pairs))
        all_diffs += d
        print(
            f"{case:<17}{statistics.median(b):8.1f}{statistics.median(c):8.1f}{statistics.median(d):8.1f}  "
            f"{str([round(x) for x in d]):<40} {statistics.median(ob) if ob else 0:6.0f}{statistics.median(oc) if oc else 0:6.0f} {cyc}"
        )
    print(
        "all cases: median paired diff",
        round(statistics.median(all_diffs), 1),
        "ms; max",
        round(max(all_diffs), 1),
        "min",
        round(min(all_diffs), 1),
        "n",
        len(all_diffs),
    )


if __name__ == "__main__":
    main(*sys.argv[1:3])
