module ConstructorTypes

open Fable.Core
open Fickl.Core
open Fickl.Core.JsInterop

type MyEl(name: string) =
    member _.Name = name

[<AbstractClass>]
type Shape() =
    abstract Area: float

type Circle(radius: float) =
    inherit Shape()
    override _.Area = radius * radius * 3.

type Box<'T>(value: 'T) =
    member _.Value = value

[<Global("Date")>]
type JsDate(ms: float) =
    member _.getTime() : float = nativeOnly

[<Import("EventEmitter", "node:events")>]
type EventEmitter() =
    member _.eventNames() : obj[] = nativeOnly

// The class itself, typed as a constructor of its instances rather than `any`
let elCtor = jsConstructorOf<MyEl>
let boxCtor = jsConstructorOf<Box<int>>
let dateCtor = jsConstructorOf<JsDate>
let emitterCtor = jsConstructorOf<EventEmitter>

// An interface that binds a global class
let mapCtor = jsConstructorOf<JS.Map<string, int>>

// An abstract class is only assignable to an abstract construct signature
let shapeCtor = jsConstructorOf<Shape>

// A parameter that takes a class, and `new` on it
let create (ctor: JsConstructor<'T>) (arg: obj) : 'T = ctor.Create(arg)

let register (name: string) (ctor: JsConstructor<MyEl>) : MyEl = ctor.Create(name)

// A class stored in a record field, a mutable class field, an array and an option
type Registration = { Tag: string; Ctor: JsConstructor<MyEl> }

type Registry() =
    member val Current: JsConstructor<MyEl> option = None with get, set

let registration = { Tag = "my-el"; Ctor = elCtor }
let ctors = [| elCtor; elCtor |]
let registry = Registry()
registry.Current <- Some elCtor

let el = create elCtor "a"
let registered = register "b" registration.Ctor
let box = create boxCtor 5
let date = create dateCtor 0.
let emitter = emitterCtor.Create()
let map = mapCtor.Create()

// An untyped constructor is cast, which TypeScript sees
let untyped: JsConstructor<Circle> = unbox (Fable.Core.JsInterop.jsConstructor<Circle>)
let circle = untyped.Create(2.)
