// Change: functions with more than 20 curried arguments
// Fickl: A function value with more than 20 curried arguments can be passed, called and partially applied.
// Issues: 291
module Main

type Fn22 =
    int -> int -> int -> int -> int -> int -> int -> int -> int -> int -> int -> int -> int -> int -> int -> int -> int -> int -> int -> int -> int -> int -> int list

let list22
    (a1: int) (a2: int) (a3: int) (a4: int) (a5: int) (a6: int) (a7: int) (a8: int) (a9: int) (a10: int) (a11: int)
    (a12: int) (a13: int) (a14: int) (a15: int) (a16: int) (a17: int) (a18: int) (a19: int) (a20: int) (a21: int)
    (a22: int)
    : int list
    =
    [ a1; a2; a3; a4; a5; a6; a7; a8; a9; a10; a11; a12; a13; a14; a15; a16; a17; a18; a19; a20; a21; a22 ]

let getFn22 (direct: bool) : Fn22 =
    if direct then list22 else (fun a1 -> list22 a1)

let apply22 (f: Fn22) =
    f 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22

Check.equal [ 1..22 ] (apply22 (getFn22 true))
Check.equal [ 1..22 ] (apply22 (getFn22 false))
