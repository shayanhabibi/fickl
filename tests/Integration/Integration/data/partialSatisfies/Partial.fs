module Partial

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core
open Fickl.Core.JsInterop

type State =
    abstract count: int with get, set
    abstract name: string with get, set

type Settings =
    abstract state: State with get, set
    abstract label: string with get, set

type Store() =
    let mutable state = jsOptions<State> (fun s -> s.count <- 0; s.name <- "")
    member _.State = state
    member _.SetState(patch: Partial<State>) = state <- !!(JS.Constructors.Object.assign (createEmpty, state, patch))

let bump (store: Store) = store.SetState(partial (fun s -> s.count <- store.State.count + 1))

let rename (name: string) : Partial<State> = partial (fun s -> s.name <- name)

let options () = jsOptions<State> (fun s -> s.count <- 1; s.name <- "one")

let nestedOptions () = jsOptions<Settings> (fun s -> s.state.count <- 2; s.label <- "two")

let complete (patch: Partial<State>) : State = full patch

let freeze (state: State) : Readonly<State> = !!(JS.Constructors.Object.freeze state)

let filled (state: Required<State>) = state
