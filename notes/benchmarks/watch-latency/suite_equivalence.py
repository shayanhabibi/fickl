"""Re-judge suite sessions against fresh variance: a watch file passes when it equals the session's fresh
compile, or when the file is unstable across fresh-vs-fresh runs and the watch digest is one of the
observed fresh variants. No text is normalized. Usage: suite_equivalence.py <runs-dir> <tag> <variance-prefix>"""

import hashlib
import json
from pathlib import Path
import sys


def digests(root):
    return {
        p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
        for p in sorted(root.rglob("*.js"))
        if "fable_modules" not in p.relative_to(root).parts
    }


def main(runs, tag, prefix):
    runs = Path(runs)
    variance = {}
    for variant in ("base", "cand"):
        folder = runs / f"{prefix}-{variant}"
        sets = {}
        for run in sorted(folder.glob("fresh-*")):
            if run.is_dir():
                for name, digest in digests(run).items():
                    sets.setdefault(name, set()).add(digest)
        variance[variant] = {k: v for k, v in sets.items() if len(v) > 1}
    for session in sorted(runs.glob(f"{tag}-*-r*")):
        variant = session.name.split("-")[1]
        watch, fresh = digests(session / "watch"), digests(session / "fresh")
        exact = sorted(k for k in fresh if watch.get(k) != fresh[k])
        unexplained = [k for k in exact if not (k in variance[variant] and watch.get(k) in variance[variant][k])]
        layout = sorted(set(watch) ^ set(fresh))
        print(
            json.dumps(
                {
                    "session": session.name,
                    "exact_differences": exact,
                    "explained_by_fresh_variance": sorted(set(exact) - set(unexplained)),
                    "unexplained": unexplained,
                    "layout_differences": layout,
                }
            )
        )


if __name__ == "__main__":
    main(*sys.argv[1:4])
