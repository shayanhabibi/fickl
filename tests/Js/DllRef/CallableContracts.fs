module Fable.Tests.DllRef.CallableContracts

[<Fickl.Core.CallableInterface>]
type Identity =
    abstract Invoke<'T>: value: 'T -> 'T

[<Fickl.Core.CallableInterface>]
type DerivedIdentity =
    inherit Identity

[<Fickl.Core.CallableInterface>]
type OtherIdentity =
    inherit Identity

[<Fickl.Core.CallableInterface>]
type DiamondIdentity =
    inherit DerivedIdentity
    inherit OtherIdentity
