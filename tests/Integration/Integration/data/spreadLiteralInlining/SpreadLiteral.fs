module SpreadLiteral

open System
open Fable.Core
open Fable.Core.JsInterop

// A list or array literal that is bound to a name and only read by the spread of a call is passed as
// direct arguments, when moving its elements into the call cannot change the order of what is observed (#311)

type IReact =
    abstract createElement: comp: obj * props: obj * [<ParamList>] children: obj list -> obj
    abstract createElementArr: comp: obj * props: obj * [<ParamArray>] children: obj[] -> obj

type IHolder =
    abstract Factory: IReact

type Lib =
    static member Make(tag: string, [<ParamArray>] children: obj[]) : obj = box (tag, children)

[<Import("default", "react")>]
let react: IReact = jsNative

let inline domEl (tag: string) (props: (string * obj) list) (children: obj list) : obj =
    react.createElement (tag, createObj props, children)

let inline div props children = domEl "div" props children
let inline span props children = domEl "span" props children

let mutable counter = 0

let eff (s: string) : obj =
    counter <- counter + 1
    box s

// ---- Inlined ----

// The binding comes from inlining `div`
let nestedInlineFunctions () =
    div [] [ div [ "id" ==> "a" ] []; span [] [] ]

let arrayOfValues (r: IReact) (props: obj) (a: obj) (b: obj) =
    let children = [| a; b |]
    r.createElementArr ("div", props, children)

let listOfCalls (r: IReact) (props: obj) =
    let children = [ eff "a"; eff "b" ]
    r.createElement ("div", props, children)

let arrayOfCalls (props: obj) =
    let children = [| eff "a"; eff "b" |]
    react.createElementArr ("div", props, children)

let nullProps () =
    let children = [ eff "a"; eff "b" ]
    react.createElement ("div", null, children)

let pipeline (props: obj) =
    [ eff "a"; eff "b" ] |> fun children -> react.createElement ("div", props, children)

// The elements are values, so it does not matter that the tag is computed before them
let valuesAfterCall (a: obj) (b: obj) =
    let children = [| a; b |]
    Lib.Make(string (eff "tag"), children)

// ---- Not inlined ----

// The tag would be computed before the elements
let callBeforeCalls () =
    let children = [ eff "a"; eff "b" ]
    react.createElement (eff "tag", null, children)

// The element would read the counter after the tag has changed it
let mutableRead () =
    let children = [ box counter ]

    react.createElement (
        (counter <- counter + 1
         "div"),
        null,
        children
    )

// A property is read between the binding and the call
let getterCallee (h: IHolder) =
    let children = [ eff "a" ]
    h.Factory.createElement ("div", null, children)

let callBetween () =
    let children = [ eff "a" ]
    let props = eff "props"
    react.createElement ("div", props, children)

let usedTwice (a: obj) (b: obj) =
    let children = [ a; b ]
    react.createElement ("div", children, children)

let capturedByClosure () =
    let children = [ eff "a" ]
    fun () -> react.createElement ("div", null, children)

let notLiteral (xs: obj list) =
    let children = eff "a" :: xs
    react.createElement ("div", null, children)
