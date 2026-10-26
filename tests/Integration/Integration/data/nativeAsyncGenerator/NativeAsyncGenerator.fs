module NativeAsyncGenerator

open Fable.Core
open Fickl.Core

[<AsyncGeneratorFunction>]
let conversation (request: JS.Promise<string>) : JsAsyncGenerator<string, string, string> = jsAsyncGenerator {
    let! ready = request
    let! reply = jsYield ready
    yield reply
    return "done:" + reply
}

[<AsyncFunction>]
let consume (source: JS.AsyncIterable<string>) = jsAsync {
    let mutable result = ""
    for value in source do
        result <- result + value
    return result
}
