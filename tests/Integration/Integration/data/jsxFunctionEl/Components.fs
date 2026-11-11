module JsxFunctionEl.Components

open Fable.Core
open Fable.Core.JsInterop

// Necessary for JSX compilation
let React: obj = importAll "react"

type RowProps = {| name: string; count: int |}

type ILabelProps =
    abstract label: string

[<JS.Pojo>]
type BadgeProps(text: string) =
    member val text = text

// None of these is decorated with JSX.Component: they are compiled as plain functions

let Logo () = JSX.jsx "<span>logo</span>"

let Row (props: RowProps) =
    JSX.jsx $"<li>{props.name}: {props.count}</li>"

let Label (props: ILabelProps) = JSX.jsx $"<label>{props.label}</label>"

let Badge (props: BadgeProps) = JSX.jsx $"<b>{props.text}</b>"

let Item (props: {| key: string; ``data-testid``: string; title: string option |}) =
    JSX.jsx $"<li>{props.title}</li>"

type Views =
    static member Footer(props: RowProps) = JSX.jsx $"<footer>{props.name}</footer>"

// Calling the function is not a component boundary
let Called (row: RowProps) =
    JSX.jsx
        $"""<ul>
            {Logo()}
            {Row row}
        </ul>"""

let Rendered (row: RowProps) (label: ILabelProps) =
    JSX.jsx
        $"""<ul>
            {JSX.el Logo ()}
            {JSX.el Row row}
            {JSX.el Row {| name = "literal"; count = row.count + 1 |}}
            {row |> JSX.el Row}
            {JSX.el Label label}
            {JSX.el Badge (BadgeProps "new")}
            {JSX.el Views.Footer row}
            {JSX.el Item {| key = "k"; ``data-testid`` = "item"; title = Some "t" |}}
        </ul>"""

// A component received as an argument is rendered as it is
let RenderWith (Render: RowProps -> JSX.Element) (row: RowProps) = JSX.el Render row

type IComponents =
    abstract Row: (RowProps -> JSX.Element)

let RenderMember (components: IComponents) (row: RowProps) = JSX.el components.Row row

let InCreate (row: RowProps) =
    JSX.create "ul" [ "children", [ JSX.el Row row; JSX.el Logo () ] ]
