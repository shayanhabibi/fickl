module Counter

let mutable calls = 0

let track x =
    calls <- calls + 1
    x
