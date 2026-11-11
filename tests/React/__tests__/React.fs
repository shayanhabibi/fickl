module Fable.Tests.React

open Fable.Core.JsInterop
open Feliz
open Fable.Jester
open Fable.ReactTestingLibrary

// Necessary for JSX compilation
let React: obj = importAll "react"

[<ReactComponent>]
let ComponentAcceptingCurriedFunction fn _oneMoreParam =
    let value = fn 1 2
    Html.p [
        prop.testId "text"
        prop.text (value.ToString())
    ]

Jest.describe("React tests", (fun () ->

    Jest.test("Counter renders correctly", (fun () ->
        let elem = RTL.render(Counter.Counter(5))
        Jest.expect(elem.container).toMatchSnapshot()
    ))

    Jest.test("Counter state works", (fun () ->
        let elem = RTL.render(Counter.Counter(5))
        let header = elem.getByTestId "header"
        let button = elem.getByTestId "button-increment"
        Jest.expect(header).toHaveTextContent("5")
        RTL.fireEvent.click(button)
        Jest.expect(header).toHaveTextContent("6")
    ))

    Jest.test("Counter JSX renders correctly", (fun () ->
        let elem = RTL.render(Counter.CounterJSX(5) |> unbox)
        Jest.expect(elem.container).toMatchSnapshot()
    ))

    Jest.test("Counter JSX state works", (fun () ->
        let elem = RTL.render(Counter.CounterJSX(5) |> unbox)
        let header = elem.getByTestId "header-jsx"
        let button = elem.getByTestId "button-increment-jsx"
        Jest.expect(header).toHaveTextContent("5")
        RTL.fireEvent.click(button)
        Jest.expect(header).toHaveTextContent("6")
    ))

    Jest.test("JSX EmptyElem render self-closing tags", (fun () ->
        let elem = Counter.CounterJSX(5)
        Jest.expect(elem).toStrictEqual(Fable.Core.JSX.jsx "<CounterJSX init={5} />")
    ))

    Jest.test("SpreadSheet renders correctly", (fun () ->
        let elem = RTL.render(SpreadSheet.SpreadSheet() |> unbox)
        Jest.expect(elem.container).toMatchSnapshot()
    ))

    Jest.test("SpreadSheet parser works", (fun () ->
        let spreadsheet = RTL.render(SpreadSheet.SpreadSheet() |> unbox)
        let cells = spreadsheet.getAllByRole("cell")
        let cell = cells.[5]
        RTL.fireEvent.click(cell)
        let input = spreadsheet.getByAltText("cell editor")
        RTL.fireEvent.input(input, [
            event.target [
                Feliz.prop.value "=4+3"
            ]
        ])
        // Click another cell to remove the editor
        RTL.fireEvent.click(cells.[8])
        Jest.expect(cell).toHaveTextContent("7")
    ))

    // See https://github.com/fable-compiler/Fable/issues/3999
    Jest.test("JSX single string hole renders correctly", (fun () ->
        let elem = RTL.render(Counter.SingleHoleJSX("hello") |> unbox)
        let node = elem.getByTestId "single-hole"
        Jest.expect(node).toHaveTextContent("hello")
    ))

    Jest.test("JSX multiple string holes render correctly", (fun () ->
        let elem = RTL.render(Counter.MultiHoleJSX("John") ("Doe") |> unbox)
        let node = elem.getByTestId "multi-hole"
        Jest.expect(node).toHaveTextContent("John Doe")
    ))

    Jest.test("JSX format specifier hole renders correctly", (fun () ->
        let elem = RTL.render(Counter.FormatSpecifierJSX(3.14159) |> unbox)
        let node = elem.getByTestId "format-specifier"
        Jest.expect(node).toHaveTextContent("3.142")
    ))

    Jest.test("JSX component passed by reference keeps its state when the parent renders again", (fun () ->
        let elem = RTL.render(Counter.ComponentByReferenceJSX() |> unbox)
        Jest.expect(elem.getByTestId "header-jsx").toHaveTextContent("5")
        RTL.fireEvent.click(elem.getByTestId "button-increment-jsx")
        Jest.expect(elem.getByTestId "header-jsx").toHaveTextContent("6")
        let header = elem.getByTestId "header-jsx"

        RTL.fireEvent.click(elem.getByTestId "button-rerender-jsx")
        Jest.expect(elem.getByTestId "button-rerender-jsx").toHaveTextContent("Rendered 2 times")
        // The same DOM node with the same state: the counter was not mounted again
        Jest.expect(elem.getByTestId "header-jsx").toBe(header)
        Jest.expect(elem.getByTestId "header-jsx").toHaveTextContent("6")
    ))

    Jest.test("A function called in a template keeps its state in the caller", (fun () ->
        Counter.plainCounterHostRenders <- 0
        let elem = RTL.render(Counter.PlainCounterCalled(5) |> unbox)
        let button = elem.getByTestId "plain-called"
        Jest.expect(Counter.plainCounterHostRenders).toBe(1)
        RTL.fireEvent.click(button)
        Jest.expect(button).toHaveTextContent("6")
        Jest.expect(Counter.plainCounterHostRenders).toBe(2)
    ))

    Jest.test("JSX.el renders an undecorated function as a component with its own state", (fun () ->
        Counter.plainCounterHostRenders <- 0
        let elem = RTL.render(Counter.PlainCounterRendered(5) |> unbox)
        let first = elem.getByTestId "plain-rendered-1"
        let second = elem.getByTestId "plain-rendered-2"
        Jest.expect(elem.getByTestId "plain-label").toHaveTextContent("label")
        Jest.expect(first).toHaveTextContent("5")
        Jest.expect(second).toHaveTextContent("15")
        RTL.fireEvent.click(first)
        RTL.fireEvent.click(first)
        RTL.fireEvent.click(second)
        Jest.expect(first).toHaveTextContent("7")
        Jest.expect(second).toHaveTextContent("16")
        // The state belongs to the counters, so the component rendering them is not rendered again
        Jest.expect(Counter.plainCounterHostRenders).toBe(1)
    ))

    Jest.test("JSX.el compiles to a tag with the fields as props", (fun () ->
        let elem = Fable.Core.JSX.el Counter.PlainCounter {| init = 1; testId = "t" |}
        Jest.expect(elem).toStrictEqual(Fable.Core.JSX.jsx """<PlainCounter init={1} testId="t" />""")
    ))

))
