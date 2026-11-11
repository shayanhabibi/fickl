module Counter

open Fable.Core
open Fable.Core.JsInterop
open Fable.React
open Feliz

// Necessary for JSX compilation
let React: obj = importAll "react"

[<ReactComponent>]
let Counter(init: int) =
    let (count, setCount) = Feliz.React.useState(init)
    Html.div [
        prop.style [ style.padding 10 ]
        prop.children [
            Html.h1 [
                prop.testId "header"
                prop.text count
            ]
            Html.button [
                prop.testId "button-increment"
                prop.text "Increment"
                prop.onClick (fun _ -> setCount(count + 1))
            ]

            // Check that fluent-ui bindings work, see #2709
            !!(Fable.FluentUI.Button.defaultButton [] [ str "A fluent button" ])
        ]
    ]

[<JSX.Component>]
let CounterJSX(init: int) =
    let (count, setCount) = Feliz.React.useState(init)
    JSX.html
        $"""
        <div style={createObj ["padding" ==> 10]}>
            <h1 data-testid="header-jsx">{count}</h1>
            <button data-testid="button-increment-jsx"
                onClick={fun _ -> setCount(count + 1)}>
                Increment
            </button>
        </div>
        """

// A function without JSX.Component that keeps its own state
let PlainCounter (props: {| init: int; testId: string |}) =
    let (count, setCount) = Feliz.React.useState (props.init)

    JSX.html
        $"""
        <button data-testid={props.testId} onClick={fun _ -> setCount (count + 1)}>{count}</button>
        """

let PlainLabel () = JSX.html $"""<span data-testid="plain-label">label</span>"""

let mutable plainCounterHostRenders = 0

// Calls PlainCounter, which makes its state part of this component
[<JSX.Component>]
let PlainCounterCalled (init: int) =
    plainCounterHostRenders <- plainCounterHostRenders + 1
    JSX.html $"""<div>{PlainCounter {| init = init; testId = "plain-called" |}}</div>"""

// Renders PlainCounter as a tag, which makes it a component of its own
[<JSX.Component>]
let PlainCounterRendered (init: int) =
    plainCounterHostRenders <- plainCounterHostRenders + 1
    let second = {| init = init + 10; testId = "plain-rendered-2" |}

    JSX.html
        $"""
        <div>
            {JSX.el PlainLabel ()}
            {JSX.el PlainCounter {| init = init; testId = "plain-rendered-1" |}}
            {JSX.el PlainCounter second}
        </div>
        """

// Regression test for https://github.com/fable-compiler/Fable/issues/3999
// Single string hole: F# compiler lowers to String.Concat rather than printf
[<JSX.Component>]
let SingleHoleJSX(text: string) =
    JSX.html
        $"""
        <div data-testid="single-hole">{text}</div>
        """

// Multiple string holes with no format specifier
[<JSX.Component>]
let MultiHoleJSX(first: string) (last: string) =
    JSX.html
        $"""
        <div data-testid="multi-hole">{first} {last}</div>
        """

// Hole with a format specifier (the original issue #3999)
[<JSX.Component>]
let FormatSpecifierJSX(value: float) =
    JSX.html
        $"""
        <div data-testid="format-specifier">%.3f{value}</div>
        """

// Renders the component it is given, the way a router or a navigator does
[<JSX.Component>]
let ComponentHostJSX(screen: obj) : JSX.Element =
    React?createElement(screen, createObj [ "init" ==> 5 ])

// A component used as a value is passed by reference, so that it is the same function
// on every render and React keeps its state instead of mounting it again
[<JSX.Component>]
let ComponentByReferenceJSX() =
    let (renders, setRenders) = Feliz.React.useState(1)
    JSX.html
        $"""
        <div>
            <button data-testid="button-rerender-jsx"
                onClick={fun _ -> setRenders(renders + 1)}>
                Rendered {renders} times
            </button>
            <ComponentHostJSX screen={CounterJSX} />
        </div>
        """
