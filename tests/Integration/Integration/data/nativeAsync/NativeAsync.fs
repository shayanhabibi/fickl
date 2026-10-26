module NativeAsync

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core

emitJsTopDirectivePrologue "use server"

[<AsyncFunction>]
let load (promise: JS.Promise<string>) = jsAsync {
    let! value = promise
    return value
}

[<AsyncFunction>]
let identity<'T> (value: 'T) = jsAsync { return value }

let expressionIdentity<'T> (value: 'T) : JS.Promise<'T> = jsAsync { return value }
let expressionList<'T> (value: 'T list) : JS.Promise<'T list> = jsAsync { return value }
let expressionArray<'T> (value: 'T array) : JS.Promise<'T array> = jsAsync { return value }
let expressionBind<'T> (promise: JS.Promise<'T>) : JS.Promise<'T> = jsAsync {
    let! value = promise
    return value
}
let expressionReturnFrom<'T> (promise: JS.Promise<'T>) : JS.Promise<'T> = jsAsync { return! promise }
let expressionConditional<'T> choose (first: 'T) (second: 'T) : JS.Promise<'T> = jsAsync {
    if choose then return first else return second
}

let private genericFunction value = value
let private genericCurried first _second = first
let specializedFunction () : JS.Promise<int -> int> = jsAsync { return genericFunction }
let specializedCurriedFunction () : JS.Promise<int -> string -> int> = jsAsync { return genericCurried }
let specializedLocalFunction () : JS.Promise<int -> int> =
    let identity value = value
    jsAsync { return identity }
