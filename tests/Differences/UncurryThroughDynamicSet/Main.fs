// Change: a function parameter passed to `==>` or `?<-`
// Fickl: A two-argument function that reaches `==>` or `?<-` through a member parameter is uncurried, so JavaScript can call it with two arguments.
// Issues: 289
module Main

open Fable.Core
open Fable.Core.JsInterop

type HandlerProps =
    static member onConnect(handler: int -> string -> string) = "onConnect" ==> handler
    static member setOnConnect(o: obj, handler: int -> string -> string) = o?onConnect <- handler

[<Emit("$0.onConnect(1, 'x')")>]
let callOnConnect (o: obj) : string = jsNative

Check.equal "member 1 x" (createObj [ HandlerProps.onConnect (fun a b -> $"member {a} {b}") ] |> callOnConnect)

let o = createObj []
HandlerProps.setOnConnect (o, (fun a b -> $"set {a} {b}"))
Check.equal "set 1 x" (callOnConnect o)
