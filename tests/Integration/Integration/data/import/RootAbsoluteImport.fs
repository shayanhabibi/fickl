module RootAbsoluteImport

open Fable.Core
open Fable.Core.JsInterop

let foo: string = import "FOO" "/env.js"

[<Import("BAR", "/lib/bar.js")>]
let bar: string = jsNative

let barLength = bar.Length

let lazyEnv: JS.Promise<obj> = importDynamic "/env.js"
