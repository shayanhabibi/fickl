"""Join harness timestamps (perf_counter_ns) with a FICKL_WATCH_TRACE CSV (Stopwatch ticks).

Both are QueryPerformanceCounter on Windows; ticks are converted with the given frequency.
Prints per-case medians of each segment from the first write to stdout receipt of `Watching`."""

import json
import statistics
import sys
from pathlib import Path

SEGMENTS = [
    ("write->fsw", "write", "fsw"),
    ("fsw->throttle-fire", "fsw", "throttle-fire"),
    ("fire->agent-receive", "throttle-fire", "agent-receive"),
    ("receive->cycle-start", "agent-receive", "cycle-start"),
    ("cycle-start->fable-start", "cycle-start", "fable-start"),
    ("fable-start->fcs-start", "fable-start", "fcs-start"),
    ("fcs-start->fcs-end", "fcs-start", "fcs-end"),
    ("fcs-end->compile-reply", "fcs-end", "compile-reply"),
    ("reply->fable-done", "compile-reply", "fable-done"),
    ("fable-done->logs-done", "fable-done", "logs-done"),
    ("logs-done->watching-log", "logs-done", "watching-log"),
    ("watching-log->logged", "watching-log", "watching-logged"),
    ("logged->stdout-received", "watching-logged", "received"),
    ("TOTAL write->received", "write", "received"),
    ("output-ready - received", "received", "output"),
]


def load_trace(path, frequency):
    events = []
    for line in Path(path).read_text(encoding="utf-8").splitlines():
        ticks, name, thread, detail = line.split(",", 3)
        events.append((int(ticks) * 1_000_000_000 // frequency, name, detail))
    return sorted(events)


def main(results_path, frequency=10_000_000):
    result = json.loads(Path(results_path).read_text(encoding="utf-8"))
    rows = {}
    for session in result["sessions"]:
        trace = Path(session["folder"]) / "trace.csv"
        if not trace.exists():
            continue
        events = load_trace(trace, frequency)
        for edit in session["edits"]:
            start = edit["writes"][0]["start_ns"]
            end = edit["cycles"][0]["watching_ns"]
            window = [e for e in events if start - 1_000_000 <= e[0] <= end]

            def first(name, after=start):
                return next((t for t, n, _ in window if n == name and t >= after), None)

            marks = {
                "write": start,
                "received": end,
                "output": edit["edit_to_output_ready_ms"] and start + int(edit["edit_to_output_ready_ms"] * 1e6),
            }
            marks["fsw"] = first("fsw")
            # The batch that starts the measured cycle: the last fire before the first cycle start.
            cycle = first("cycle-start")
            fires = [t for t, n, _ in window if n == "throttle-fire" and cycle and t <= cycle]
            marks["throttle-fire"] = fires[-1] if fires else None
            receive = [t for t, n, _ in window if n == "agent-receive" and cycle and t <= cycle]
            marks["agent-receive"] = receive[-1] if receive else None
            marks["cycle-start"] = cycle
            for name in (
                "fable-start",
                "fcs-start",
                "fcs-end",
                "compile-reply",
                "fable-done",
                "logs-done",
                "watching-log",
                "watching-logged",
            ):
                marks[name] = first(name, cycle or start)
            key = (session["delay_ms"], edit["case"])
            values = rows.setdefault(key, {s[0]: [] for s in SEGMENTS})
            for label, a, b in SEGMENTS:
                if marks.get(a) and marks.get(b):
                    values[label].append((marks[b] - marks[a]) / 1e6)
    labels = [s[0] for s in SEGMENTS]
    for key, values in rows.items():
        print(f"delay {key[0]} {key[1]}")
        for label in labels:
            v = values[label]
            if v:
                print(
                    f"   {label:<28} {statistics.median(v):8.2f} ms  (n={len(v)}, min {min(v):.2f}, max {max(v):.2f})"
                )


if __name__ == "__main__":
    main(*sys.argv[1:2])
