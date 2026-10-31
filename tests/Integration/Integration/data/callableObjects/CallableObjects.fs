module CallableObjects

open Fickl.Core

[<CallableInterface>]
type Logger =
    abstract Invoke: message: string -> unit
    abstract enabled: bool with get, set
    abstract ns: string

[<CallableInterface>]
type Middleware =
    abstract Invoke: request: string -> string
    abstract path: string

[<CallableInterface>]
type Router =
    abstract Invoke: request: string -> string
    abstract routes: int
    abstract handle: request: string -> string

let makeLogger (ns: string) =
    let mutable enabled = true

    { new Logger with
        member this.Invoke message =
            if this.enabled then
                printfn "%s: %s" this.ns message
        member _.enabled
            with get () = enabled
            and set value = enabled <- value
        member _.ns = ns }

let middleware =
    { new Middleware with
        member _.Invoke request = request + "!"
        member _.path = "/" }

let router =
    { new Router with
        member _.Invoke request = request
        member _.routes = 1
        member this.handle request = this.Invoke request }
