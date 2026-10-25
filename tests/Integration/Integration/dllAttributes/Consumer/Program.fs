module Program

open Support
open Fable.Core

[<Emit("typeof $0.map === 'function' && $0.map === $1")>]
let isTheFunction (x: obj) (f: obj) : bool = nativeOnly

let generic =
    { new GenericIdentity with
        member _.Invoke<'T>(value: 'T) : 'T = value
    }

let made = Factory.Create(generic)

if made.map ("text") <> "text" || made.map (42) <> 42 || not (isTheFunction made generic) then
    failwith "generic callable contract"
