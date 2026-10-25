namespace Support

open Fable.Core

/// A callable interface declared in a compiled assembly: the consumer reads its Fickl.Core attribute from metadata
[<Fickl.Core.CallableInterface>]
type GenericIdentity =
    abstract Invoke<'T> : value: 'T -> 'T

[<Erase>]
type ObjectWithMap =
    abstract map<'T> : value: 'T -> 'T

[<Erase>]
type Factory =
    [<Emit("({map:$0})")>]
    static member Create(map: GenericIdentity) : ObjectWithMap = nativeOnly
