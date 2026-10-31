module ImportAttributes

open Fable.Core
open Fickl.Core

type Config =
    abstract name: string
    abstract port: int

[<ImportDefault("./config.json"); ImportWith("type", "json")>]
let config: Config = jsNative

// The same path with other attributes is a separate declaration
[<Import("default", "./config.json"); ImportWith("type", "json"); ImportWith("x-note", "kept")>]
let annotated: Config = jsNative

let describe () = $"{config.name}:{config.port}:{annotated.name}"

let load () =
    JsInterop.importDynamicWith<{| ``default``: Config |}> "./config.json" [ "type", "json" ]

let loadWith (kind: string) =
    JsInterop.importDynamicWith<obj> "./config.json" [ "type", kind ]

// Only used as a type: `import type` takes no attributes
[<Import("Theme", "./theme.js"); ImportWith("type", "css")>]
type Theme() =
    member _.name: string = jsNative

let themeName (theme: Theme) = theme.name
