module TaggedTemplates

open System
open System.Runtime.CompilerServices
open Fable.Core
open Fickl.Core

type Template =
    abstract strings: JS.ReadonlyArray<string>
    abstract values: obj[]

[<Import("html", "./tags.js"); TaggedTemplate>]
let html (template: FormattableString) : Template = jsNative

[<Emit("String.raw"); TaggedTemplate>]
let raw (template: FormattableString) : string = jsNative

let view (name: string) (count: int) = html $"<p>Hello {name}, {count} items</p>"

let noHoles () = html $"<br>"

let path (dir: string) = raw $"/{dir}/file"

let ofValue (template: FormattableString) = html template

let ofCall (name: string) =
    html (FormattableStringFactory.Create("<b>{0}</b>", name))
