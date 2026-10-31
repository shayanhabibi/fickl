module EventTarget

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core

type Store() =
    inherit JS.EventTarget()
    member this.Changed: IEvent<JS.CustomEvent<int>> = JsEvent.ofTarget this "change"
    member this.Set(value: int) = JsEvent.dispatch this "change" value

let details (store: Store) : System.IObservable<int> =
    store.Changed |> Observable.map (fun e -> e.detail)

let aborts (signal: JS.AbortSignal) : IEvent<JS.Event> = JsEvent.ofTarget signal "abort"

let passive (target: JS.EventTarget) : IEvent<JS.Event> =
    JsEvent.ofTargetWith (jsOptions<JS.AddEventListenerOptions> (fun o -> o.passive <- true)) target "scroll"

let data (emitter: obj) : IEvent<string> = JsEvent.ofEmitter emitter "data"
