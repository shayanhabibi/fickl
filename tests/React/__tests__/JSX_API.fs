module Fable.Tests.JSX_API

open Fable.Core
open Fable.Core.JsInterop
open Fable.Jester
open Fable.ReactTestingLibrary
open JsxListOptimisation

// Necessary for JSX compilation
let React: obj = importAll "react"

// Expose a render version to work directly with JSX.Element
type RTL with

    static member inline render (jsxElement: JSX.Element) =
        RTL.render(unbox<Feliz.ReactElement> jsxElement)

    static member inline render (jsxElement: Components.JSX_ReactElement) =
        RTL.render(unbox<Feliz.ReactElement> jsxElement)

let private matchSnapshot (element : Components.JSX_ReactElement)=
    element
    |> RTL.render
    |> _.container
    |> Jest.expect
    |> _.toMatchSnapshot()

Jest.describe("JSX API tests (using React)", fun () ->

    Jest.test("Element can have text directly in them", fun () ->
        matchSnapshot Components.divWithText
    )

    Jest.test("Element can have nested list", fun () ->
        matchSnapshot Components.divWithNestedList
    )

    Jest.test("Element can have multiple nested list", fun () ->
        matchSnapshot Components.divWithMultipleNestedList
    )

    Jest.test("Element can have mix of element and list", fun () ->
        matchSnapshot Components.divWithMixOfElementAndList
    )

    Jest.test("Element can have multi level mix of element and list", fun () ->
        matchSnapshot Components.multiLevelMixOfElementAndList
    )

    Jest.test("Test that optimised condition works for the true branch", fun () ->
        matchSnapshot Components.divWithOptimisedTrueCondition
    )

    Jest.test("Test that optimised condition works for the false branch", fun () ->
        matchSnapshot Components.divWithOptimisedFalseCondition
    )

    Jest.test("Test that non optimised condition works", fun () ->
        let mutable myCondition = "a"
        myCondition |> ignore

        matchSnapshot (Components.divWithConditionalChildren myCondition)
    )

    Jest.test("Element can have for loops", fun () ->
        matchSnapshot Components.divWithForLoop
    )

    Jest.test("Element can have a match arm that binds a value before a for loop", fun () ->
        matchSnapshot (Components.divWithMatchContainingForLoop 2)
    )

    Jest.test("Element can have a sibling before a match arm that binds a value before a for loop", fun () ->
        matchSnapshot (Components.divWithElementBeforeMatchContainingForLoop 2)
    )

    Jest.test("Fragments are supported", fun () ->
        matchSnapshot Components.divWithFragment
    )

    Jest.test("Elements can have attributes", fun () ->
        matchSnapshot Components.divWithAttributes
    )

    Jest.test("Test that condition without else branch works if condition is True", fun () ->
        matchSnapshot (Components.divWithConditionalWithoutElseBranchWorks true)
    )

    Jest.test("Test that condition without else branch works if condition is False", fun () ->
        matchSnapshot (Components.divWithConditionalWithoutElseBranchWorks false)
    )

    Jest.test("Test that properties can be defined using `unbox`", fun () ->
        matchSnapshot Components.propsCanUseUnbox
    )

    Jest.test("Spread props are applied to the element", fun () ->
        let elem = RTL.render Components.divWithSpread
        let target = elem.getByTestId "spread-target"
        Jest.expect(target).toHaveAttribute("title", "from-spread")
    )

    Jest.test("Props after a spread override the spread props", fun () ->
        let elem = RTL.render Components.divWithSpreadBetweenProps
        let target = elem.getByTestId "spread-target"
        Jest.expect(target).toHaveClass("before")
        Jest.expect(target).toHaveAttribute("title", "after")
    )

    Jest.test("Spread props override the props before the spread", fun () ->
        let elem = RTL.render Components.divWithSpreadOverridingProps
        let target = elem.getByTestId "spread-target"
        Jest.expect(target).toHaveAttribute("title", "from-spread")
    )

    Jest.test("Several spreads are applied in order", fun () ->
        let elem = RTL.render Components.divWithTwoSpreads
        let target = elem.getByTestId "spread-target"
        Jest.expect(target).toHaveAttribute("title", "second")
    )

    Jest.test("Spread props can be combined with children", fun () ->
        let elem = RTL.render Components.divWithSpreadAndChildren
        let target = elem.getByTestId "spread-target"
        Jest.expect(target).toHaveAttribute("title", "from-spread")
        Jest.expect(target).toHaveTextContent("Child")
    )

    Jest.test("A conditional prop is set when the condition is true", fun () ->
        let target = (RTL.render (Components.divWithConditionalProp true)).getByTestId "target"
        Jest.expect(target).toHaveClass("active")
        Jest.expect(target).toHaveAttribute("title", "always")
    )

    Jest.test("A conditional prop is left out when the condition is false", fun () ->
        let target = (RTL.render (Components.divWithConditionalProp false)).getByTestId "target"
        Jest.expect(target).not.toHaveAttribute("class")
        Jest.expect(target).toHaveAttribute("title", "always")
    )

    Jest.test("A conditional prop overrides the same prop set before it", fun () ->
        let target = (RTL.render (Components.divWithConditionalPropAfterProp true)).getByTestId "target"
        Jest.expect(target).toHaveAttribute("title", "active")
        RTL.cleanup ()
        let target = (RTL.render (Components.divWithConditionalPropAfterProp false)).getByTestId "target"
        Jest.expect(target).toHaveAttribute("title", "always")
    )

    Jest.test("A conditional prop can have an else branch", fun () ->
        let target = (RTL.render (Components.divWithConditionalPropWithElseBranch true)).getByTestId "target"
        Jest.expect(target).toHaveClass("active")
        Jest.expect(target).not.toHaveAttribute("title")
        RTL.cleanup ()
        let target = (RTL.render (Components.divWithConditionalPropWithElseBranch false)).getByTestId "target"
        Jest.expect(target).not.toHaveAttribute("class")
        Jest.expect(target).toHaveAttribute("title", "inactive")
    )

    Jest.test("Several props can be set under one condition, next to children", fun () ->
        let target = (RTL.render (Components.divWithSeveralConditionalProps true)).getByTestId "target"
        Jest.expect(target).toHaveClass("active")
        Jest.expect(target).toHaveAttribute("title", "active")
        Jest.expect(target).toHaveTextContent("Child")
        RTL.cleanup ()
        let target = (RTL.render (Components.divWithSeveralConditionalProps false)).getByTestId "target"
        Jest.expect(target).not.toHaveAttribute("class")
        Jest.expect(target).not.toHaveAttribute("title")
        Jest.expect(target).toHaveTextContent("Child")
    )

    Jest.test("A condition can hold a spread and another condition", fun () ->
        let target = (RTL.render (Components.divWithNestedConditionalProps true true)).getByTestId "spread-target"
        Jest.expect(target).toHaveClass("active")
        Jest.expect(target).toHaveAttribute("title", "first")
        RTL.cleanup ()
        let target = (RTL.render (Components.divWithNestedConditionalProps true false)).getByTestId "spread-target"
        Jest.expect(target).toHaveClass("active")
        Jest.expect(target).toHaveAttribute("title", "from-spread")
        RTL.cleanup ()
        let target = (RTL.render (Components.divWithNestedConditionalProps false true)).getByTestId "target"
        Jest.expect(target).not.toHaveAttribute("class")
        Jest.expect(target).not.toHaveAttribute("title")
    )

    Jest.test("A spread can be conditional", fun () ->
        let target = (RTL.render (Components.divWithConditionalSpread true)).getByTestId "spread-target"
        Jest.expect(target).toHaveAttribute("title", "from-spread")
        RTL.cleanup ()
        let target = (RTL.render (Components.divWithConditionalSpread false)).getByTestId "target"
        Jest.expect(target).toHaveAttribute("title", "before")
    )

    Jest.test("Props can be yielded from a list literal", fun () ->
        let target = (RTL.render Components.divWithYieldBangProps).getByTestId "target"
        Jest.expect(target).toHaveClass("header")
        Jest.expect(target).toHaveAttribute("title", "yielded")
        Jest.expect(target).toHaveAttribute("id", "myid")
    )
)
