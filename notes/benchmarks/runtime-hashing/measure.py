"""Sequential fresh-process measurements; rotate order and check every checksum."""

import argparse
import json
import os
from pathlib import Path
import platform
import statistics
import subprocess
import time

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
DEST = ROOT / "temp/runtime-hashing"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--rounds", type=int, default=7)
args = parser.parse_args()


def run(*command):
    return json.loads(subprocess.check_output(["rtk", "proxy", *map(str, command)], text=True, cwd=ROOT))


config = run("node", "--expose-gc", HERE / "benchmark.mjs", DEST, "baseline", "--calibrate")
config_path = DEST / "iterations.json"
config_path.write_text(json.dumps(config, indent=2))
variants = ["baseline", "array", "record", "object", "combined"]
rows = []
expected = {}
for round in range(args.rounds):
    order = variants[round % len(variants) :] + variants[: round % len(variants)]
    if round % 2:
        order = list(reversed(order))
    for variant in order:
        result = run("node", "--expose-gc", HERE / "benchmark.mjs", DEST, variant, config_path)
        for row in result["results"]:
            key = row["name"]
            if key in expected and expected[key] != row["checksum"]:
                raise RuntimeError(f"Hash checksum differs for {variant}/{key}")
            expected[key] = row["checksum"]
            rows.append(dict(round=round + 1, variant=variant, **row))
        (DEST / "measurements.json").write_text(json.dumps(rows, indent=2))
    print(f"Completed measured round {round + 1}/{args.rounds}", flush=True)
summary = []
for name in config:
    values = {v: [r["nsPerOp"] for r in rows if r["name"] == name and r["variant"] == v] for v in variants}
    medians = {v: statistics.median(x) for v, x in values.items()}
    summary.append(
        dict(
            name=name,
            median_ns=medians,
            min_ns={v: min(x) for v, x in values.items()},
            max_ns={v: max(x) for v, x in values.items()},
            percent_change={v: 100 * (medians[v] / medians["baseline"] - 1) for v in variants if v != "baseline"},
        )
    )
(HERE / "results.json").write_text(
    json.dumps(
        dict(
            timestamp=time.strftime("%Y-%m-%dT%H:%M:%S%z"),
            platform=platform.platform(),
            cpu_count=os.cpu_count(),
            node=result["node"],
            v8=result["v8"],
            rounds=args.rounds,
            iterations=config,
            summary=summary,
            raw=rows,
        ),
        indent=2,
    )
)
print(json.dumps(summary, indent=2))
