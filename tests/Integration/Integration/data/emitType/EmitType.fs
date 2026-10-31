module EmitType

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core

[<EmitType("Parameters<typeof fetch>[1]")>]
type FetchInit = interface end

[<EmitType("`data-${string}`"); Erase>]
type DataAttr = DataAttr of string

[<EmitType("{ [K in keyof $0 as `get${Capitalize<string & K>}`]: () => $0[K] }")>]
type Getters<'T> = interface end

[<EmitType("Brand<$0, \"user\">", "./Types.ts")>]
type UserId<'T> = interface end

[<EmitType("Box<$0>", "./Types.ts")>]
type Boxed<'T> = interface end

type Point = { x: float; y: float }

let request (init: FetchInit) = init

let dataAttr (name: string) = DataAttr("data-" + name)

let attrs: DataAttr list = [ DataAttr "data-a"; dataAttr "b" ]

let getters (p: Point) : Getters<Point> =
    !!(createObj [ "getX" ==> (fun () -> p.x); "getY" ==> (fun () -> p.y) ])

let userId (id: int) : UserId<int> = unbox id

let boxed (value: 'T) : Boxed<'T> = !!(createObj [ "value" ==> value ])

let maybeInit (init: FetchInit option) = init
