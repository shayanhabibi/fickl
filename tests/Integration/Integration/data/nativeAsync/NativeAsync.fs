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
