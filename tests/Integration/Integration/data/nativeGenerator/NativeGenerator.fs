module NativeGenerator

open Fickl.Core

[<GeneratorFunction>]
let conversation (request: string) : JsGenerator<string, string, string> = jsGenerator {
    let! reply = request
    yield reply
    return "done:" + reply
}
