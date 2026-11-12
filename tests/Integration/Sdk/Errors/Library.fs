module Library

// FS0025, reported by the .NET compile; Fickl must not report it a second time
let describe (x: int) =
    match x with
    | 1 -> "one"

#if FABLE_COMPILER
// FS0001, reported by Fickl only
let broken: int = "not an int"
#endif
