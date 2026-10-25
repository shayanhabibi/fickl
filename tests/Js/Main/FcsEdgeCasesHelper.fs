/// fickl: declarations FcsEdgeCaseTests.fs uses from another file, so inlining crosses a file boundary
module Fable.Tests.FcsEdgeCasesHelper

let internal factor = 10
let mutable internal calls = 0
let internal scaleBy x = x * factor

let internal track x =
    calls <- calls + 1
    x

let internal wrap<'T> (x: 'T) = [ x; x ]

let inline internal scale x = scaleBy x + 1
let inline internal scaleTracked x = track (scaleBy x)
let inline internal scaleTwice x = scale (scale x)
let inline internal wrapInline (x: 'T) = wrap x

type internal Hidden() =
    static member Triple x = x * 3

let inline internal triple x = Hidden.Triple x

type Meter =
    {
        Value: int
    }

    member inline internal this.Scaled = scaleBy this.Value

    static member inline internal Create x = { Value = scaleBy x }

let inline internal addAll (xs: ^T list) : ^T =
    List.fold (fun acc x -> acc + x) LanguagePrimitives.GenericZero xs |> track

#if FABLE_COMPILER
// The .NET compiler rejects this (FS1113), but Fable allows it, as precompiled packages rely on it
let inline publicScale x = scaleBy x + 2
#endif
