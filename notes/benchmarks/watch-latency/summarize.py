"""Summarize watch results.json files: per-case latency medians and invalidation outcomes."""

import json
import statistics
import sys
from pathlib import Path


def median(values):
    values = [v for v in values if v is not None]
    return round(statistics.median(values), 1) if values else None


def main(paths):
    rows = {}
    for path in paths:
        result = json.loads(Path(path).read_text(encoding="utf-8"))
        for session in result["sessions"]:
            print(
                f"{Path(session['folder']).name}: delay {session['delay_ms']} acquisition "
                f"{session.get('watch_acquisition_ms', 0):.0f} ms validated {session.get('validated')} "
                f"killed {len(session.get('killed_tree') or [])}"
            )
            for failure in session.get("invalidation_failures", []):
                print(
                    "  FAIL",
                    failure["case"],
                    failure["kind"],
                    failure.get("differing") or failure.get("detail", "")[-160:],
                )
            for edit in session["edits"]:
                key = (session["delay_ms"], edit["case"])
                rows.setdefault(key, []).append(edit)
    print(
        f"{'delay':>5} {'case':<17} {'n':>2} {'write>watch':>11} {'last>watch':>10} {'write>out':>9} "
        f"{'compile':>7} {'cycles':>6} files  changed"
    )
    for (delay, case), edits in rows.items():
        cycles = edits[-1]["cycles"]
        print(
            f"{delay:>5} {case:<17} {len(edits):>2} "
            f"{median([e['edit_to_watching_ms'] for e in edits]):>11} "
            f"{median([e['last_write_to_watching_ms'] for e in edits]):>10} "
            f"{str(median([e['edit_to_output_ready_ms'] for e in edits])):>9} "
            f"{str(median([sum(c or 0 for c in e['compile_reported_ms']) for e in edits])):>7} "
            f"{median([len(e['cycles']) for e in edits]):>6} "
            f"{[c['timings'].get('files') for c in cycles]} {len(edits[-1]['changed_outputs'])}"
        )


if __name__ == "__main__":
    main(sys.argv[1:])
