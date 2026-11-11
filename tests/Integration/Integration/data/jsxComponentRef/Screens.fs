module JsxComponentRef.Screens

open Fable.Core

[<JSX.Component>]
let Imported () = JSX.jsx $"""<div>Imported</div>"""

type Screens =
    [<JSX.Component>]
    static member Member(name: string, age: int) = JSX.jsx $"""<div>{name}{age}</div>"""
