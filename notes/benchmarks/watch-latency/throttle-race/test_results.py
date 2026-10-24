import copy
import unittest
from check_results import validate


class RaceResultControls(unittest.TestCase):
    def setUp(self):
        self.row = dict(
            mode="unit",
            sent=100,
            delivered=100,
            lost=0,
            duplicates=0,
            spurious=0,
            overlappingOnNext=0,
            lossyInstances=0,
        )

    def test_good_control(self):
        validate([self.row], 1)

    def test_every_bad_counter_is_rejected(self):
        for field in ["lost", "duplicates", "spurious", "overlappingOnNext", "lossyInstances"]:
            with self.subTest(field=field):
                changed = copy.copy(self.row)
                changed[field] = 1
                with self.assertRaises(ValueError):
                    validate([changed], 1)

    def test_missing_observation_or_field_cannot_pass(self):
        with self.assertRaises(ValueError):
            validate([], 1)
        del self.row["duplicates"]
        with self.assertRaises(KeyError):
            validate([self.row], 1)

    def test_delivery_mismatch_cannot_pass(self):
        self.row["delivered"] = 99
        with self.assertRaises(ValueError):
            validate([self.row], 1)


if __name__ == "__main__":
    unittest.main()
