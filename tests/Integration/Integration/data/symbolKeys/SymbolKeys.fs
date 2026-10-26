module SymbolKeys

open Fickl.Core

type ILabel =
    [<SymbolKey("Symbol.toStringTag")>]
    abstract Label: string
    [<SymbolKey("Symbol.for('fickl.snapshot.state')")>]
    abstract State: string with get, set

let create (label: string) : ILabel =
    let mutable state = label
    { new ILabel with
        member _.Label = label
        member _.State with get() = state and set value = state <- value }

let update (value: ILabel) state =
    value.State <- state
    value.State
