module Fable.Tests.SymbolKeyBindings

open Fickl.Core

type IPrimitive =
    [<SymbolKey("Symbol.toPrimitive")>]
    abstract ToPrimitive: hint: string -> int

type IState =
    [<SymbolKey("Symbol.for('fickl.test.state')")>]
    abstract State: int with get, set

type IImportedState =
    [<SymbolKey("state", "./js/symbolKeys.js")>]
    abstract Value: int with get, set

type IChild =
    inherit IPrimitive
    inherit IState
    inherit IImportedState

type IDollarKey =
    [<SymbolKey("Symbol.for('fickl.$0.state')")>]
    abstract State: string
