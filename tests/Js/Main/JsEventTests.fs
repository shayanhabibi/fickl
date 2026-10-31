module Fable.Tests.JsEvents

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core
open Util.Testing

#if FABLE_COMPILER
type private Store() =
    inherit JS.EventTarget()
    member this.Changed: IEvent<JS.CustomEvent<int>> = JsEvent.ofTarget this "change"
    member this.Set(value: int) = JsEvent.dispatch this "change" value

/// A Node.js-style emitter that counts its listeners
[<AttachMembers>]
type private Emitter() =
    let listeners = ResizeArray<string * (obj -> unit)>()
    member _.on(name: string, listener: obj -> unit) = listeners.Add((name, listener))

    member _.off(name: string, listener: obj -> unit) =
        let index = listeners.FindIndex(fun (n, l) -> n = name && obj.ReferenceEquals(l, listener))
        if index >= 0 then listeners.RemoveAt index

    member _.emit(name: string, value: obj) =
        for n, l in List.ofSeq listeners do
            if n = name then l value

    member _.count = listeners.Count
#endif

let tests =
    testList "JsEvents" [
#if FABLE_COMPILER
        testCase "ofTarget subscribes with addEventListener and Dispose removes the listener" <| fun () ->
            let store = Store()
            let seen = ResizeArray<int>()

            let subscription =
                store.Changed
                |> Observable.map (fun e -> e.detail)
                |> Observable.filter (fun v -> v > 0)
                |> Observable.subscribe seen.Add

            store.Set 1 |> ignore
            store.Set -1 |> ignore
            store.Set 2 |> ignore
            subscription.Dispose()
            store.Set 3 |> ignore
            equal [ 1; 2 ] (List.ofSeq seen)

        testCase "ofTarget handlers receive the target as sender and can be removed" <| fun () ->
            let store = Store()
            let seen = ResizeArray<string>()
            let handler = Handler<JS.CustomEvent<int>>(fun sender e ->
                seen.Add(string e.detail + (if obj.ReferenceEquals(sender, store) then " from store" else "")))
            store.Changed.AddHandler handler
            store.Set 1 |> ignore
            store.Changed.RemoveHandler handler
            store.Set 2 |> ignore
            equal [ "1 from store" ] (List.ofSeq seen)

        testCase "JavaScript listeners see events dispatched from F#" <| fun () ->
            let store = Store()
            let mutable detail = 0
            store.addEventListener ("change", fun e -> detail <- (e :?> JS.CustomEvent<int>).detail)
            store.Set 7 |> ignore
            equal 7 detail

        testCase "dispatch returns false when a listener cancels the event" <| fun () ->
            let target = JS.EventTarget()
            JsEvent.ofTarget<JS.Event> target "change" |> Event.add (fun e -> e.preventDefault ())
            // Not cancelable, so preventDefault has no effect
            equal true (JsEvent.dispatch target "change" 1)
            let event = JS.Event("change", jsOptions<JS.EventInit> (fun o -> o.cancelable <- true))
            equal false (target.dispatchEvent event)

        testCase "An AbortSignal is an event target" <| fun () ->
            let controller = JS.AbortController()
            let mutable aborted = 0
            JsEvent.ofTarget<JS.Event> controller.signal "abort" |> Event.add (fun _ -> aborted <- aborted + 1)
            controller.abort ()
            equal 1 aborted

        testCase "ofTargetWith passes the options to addEventListener" <| fun () ->
            let target = JS.EventTarget()
            let mutable count = 0
            let once = jsOptions<JS.AddEventListenerOptions> (fun o -> o.once <- true)
            JsEvent.ofTargetWith<JS.Event> once target "ping" |> Event.add (fun _ -> count <- count + 1)
            target.dispatchEvent (JS.Event("ping")) |> ignore
            target.dispatchEvent (JS.Event("ping")) |> ignore
            equal 1 count

        testCase "ofEmitter subscribes with on and Dispose calls off" <| fun () ->
            let emitter = Emitter()
            let seen = ResizeArray<string>()
            let data: IEvent<string> = JsEvent.ofEmitter emitter "data"
            equal 0 emitter.count
            let subscription = data |> Observable.subscribe seen.Add
            equal 1 emitter.count
            emitter.emit ("data", "a")
            emitter.emit ("other", "b")
            subscription.Dispose()
            equal 0 emitter.count
            emitter.emit ("data", "c")
            equal [ "a" ] (List.ofSeq seen)
#endif
    ]
