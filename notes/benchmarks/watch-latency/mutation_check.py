"""Red-capability check: reverting each preflight fix must make its driver test fail."""

import importlib
import io
from pathlib import Path
import unittest

import measure
import test_driver


def passes(name):
    suite = unittest.defaultTestLoader.loadTestsFromName(name, test_driver)
    return unittest.TextTestRunner(stream=io.StringIO()).run(suite).wasSuccessful()


def first_bytes(baseline, final, raw):
    # Version 1 behaviour: any observed bytes count as output readiness.
    return min((r["observed_ns"] for r in raw), default=None), {k: {"transients": []} for k in final}, []


def no_resolve(path, root):
    return Path(path).absolute()


original_command = measure.command


def no_configuration(args, project, output, configuration, delay=None, fresh=False):
    cmd = original_command(args, project, output, configuration, delay, fresh)
    index = cmd.index("--configuration")
    return cmd[:index] + cmd[index + 2 :]


def single_write(path_root, scratch, files, names, mode, burst_values=None, render_burst=None):
    # Version 1 behaviour: identical semantic values in every burst save.
    return original_write(path_root, scratch, files, names, mode, [burst_values[-1]] * 3, [render_burst[-1]] * 3)


original_write = measure.write_files

MUTATIONS = {
    "readiness=first-bytes": (
        "readiness",
        first_bytes,
        [
            "ReadinessTests.test_transient_writes_do_not_count_as_ready",
            "ReadinessTests.test_real_observer_records_partial_writes_separately",
        ],
    ),
    "inside=no-resolve": ("inside", no_resolve, ["PathTests.test_escape_rejected"]),
    "command=no-configuration": (
        "command",
        no_configuration,
        ["PreparationTests.test_plan_pins_identical_configuration_and_launches_nothing"],
    ),
    "diagnostics=accept-any": (
        "check_diagnostics",
        lambda case, found, patterns: None,
        ["DiagnosticTests.test_expected_undefined_name_is_required"],
    ),
    "burst=identical-values": (
        "write_files",
        single_write,
        ["PreparationTests.test_burst_writes_distinct_values_and_final_last"],
    ),
}

if __name__ == "__main__":
    for name, (attribute, replacement, tests) in MUTATIONS.items():
        setattr(measure, attribute, replacement)
        try:
            outcome = ["green (NOT red-capable)" if passes(t) else "red" for t in tests]
        finally:
            importlib.reload(measure)
            test_driver.measure = measure
        print(name, outcome)
    print(
        "baseline",
        "green" if passes("PreparationTests") and passes("ReadinessTests") and passes("DiagnosticTests") else "red (!)",
    )
