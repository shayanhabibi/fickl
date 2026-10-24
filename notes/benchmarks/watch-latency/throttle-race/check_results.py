"""Fail closed on every recorded throttle-race invariant, including duplicates."""

import argparse
import json
from pathlib import Path


def validate(records, expected_runs):
    if len(records) != expected_runs:
        raise ValueError(f"Expected {expected_runs} complete observations, got {len(records)}")
    for index, record in enumerate(records):
        if record.get("mode") == "unit":
            fields = ["lost", "duplicates", "spurious", "overlappingOnNext", "lossyInstances"]
            if record["sent"] <= 0 or record["sent"] != record["delivered"]:
                raise ValueError(f"Observation {index}: sent/delivered mismatch")
        elif record.get("mode") == "fsw":
            fields = ["roundsLosing", "lostWrites"]
            if record["rounds"] <= 0 or record["files"] <= 0:
                raise ValueError(f"Observation {index}: no filesystem workload")
        else:
            raise ValueError(f"Observation {index}: missing or unknown workload")
        for field in fields:
            if record[field] != 0:
                raise ValueError(f"Observation {index}: {field}={record[field]}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("results", type=Path)
    parser.add_argument("--expected-runs", type=int, default=15)
    args = parser.parse_args()
    rows = [json.loads(line) for line in args.results.read_text(encoding="utf-8").splitlines() if line.strip()]
    validate(rows, args.expected_runs)
    print(f"PASS: {len(rows)} race observations, no loss, duplication, spurious or overlapping delivery")
