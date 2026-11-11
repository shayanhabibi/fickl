module SrtpGenericStaticMember

// A snapshot and not a test in tests/Js/Main: the F# compiler of the .NET 10 SDK fails with FS0073
// on the type parameters these calls leave unsolved, so the code is only compiled by Fable

type Container<'T> =
    | Container of 'T
    // 'T shadows the type parameter of the union on purpose
    static member CodecInner<'T, 'E>(t: int) : int = t + 1
    static member CodecOuter<'A, 'E>(t: int) : int = t + 2

type Plain =
    | Plain
    static member CodecPlain<'E>(t: int) : int = t + 3

let inline codecInner< ^a when ^a: (static member CodecInner: int -> int)> () = 'a.CodecInner 41
let inline codecOuter< ^a when ^a: (static member CodecOuter: int -> int)> () = 'a.CodecOuter 41
let inline codecPlain< ^a when ^a: (static member CodecPlain: int -> int)> () = 'a.CodecPlain 41

// The trait signature cannot fix the type parameters of the members, which are passed as `any`
let inner () = codecInner<Container<string>> ()
let outer () = codecOuter<Container<string>> ()
let plain () = codecPlain<Plain> ()
