module GenericDelegateInvoke

open System
open Fable.Core

type Template =
    abstract strings: string[]
    abstract values: obj[]

type JsTag<'T> = delegate of strs: string[] * [<ParamArray>] args: obj[] -> 'T

// The type argument belongs to the delegate type, so the imported function is called without one
[<ImportMember("./tags.js")>]
let html: JsTag<Template> = jsNative

let render (name: string) = html.Invoke([| "<b>"; "</b>" |], name)

type Mapper<'T, 'U> = delegate of 'T -> 'U

let applyMapper (mapper: Mapper<'T, 'U>) (x: 'T) = mapper.Invoke x
