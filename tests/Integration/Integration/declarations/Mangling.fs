/// Abstract members are named in a declaration as the JavaScript calls them
module Mangling

open Fable.Core

[<Mangle>]
type IMangled =
    abstract Foo: int -> int
    abstract Foo: string -> string
    abstract Bar: string

[<Mangle(false)>]
type INotMangled =
    abstract Foo: int -> int
    abstract Bar: string

[<Mangle>]
type IGeneric<'T> =
    abstract Get: 'T -> 'T
    abstract Value: 'T

type IPlain =
    abstract Foo: int -> int
    abstract Baz: int with get, set

module Inner =
    [<Mangle>]
    type INested =
        abstract Run: unit -> int

[<AbstractClass>]
type Base() =
    abstract M: int -> int
    abstract P: int
    abstract D: int -> int
    default _.D x = x

[<AbstractClass>]
type GenericBase<'T>() =
    abstract M: 'T -> 'T

[<AbstractClass; AttachMembers>]
type Attached() =
    abstract M: int -> int
    abstract P: int

let useAll
    (a: IMangled)
    (b: INotMangled)
    (c: IGeneric<int>)
    (d: IPlain)
    (e: Inner.INested)
    (f: Base)
    (g: GenericBase<int>)
    (h: Attached)
    =
    d.Baz <- 1

    a.Foo 1, a.Foo "s", a.Bar, b.Foo 1, b.Bar, c.Get 1, c.Value, d.Foo 1, d.Baz, e.Run(), f.M 1, f.P, f.D 1, g.M 1, h.M 1, h.P
