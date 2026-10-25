/// Compiled first in every case. A failed check throws, so node exits with an error.
module Check

let equal (expected: 'T) (actual: 'T) =
    if actual <> expected then
        failwith $"expected {expected}, got {actual}"
