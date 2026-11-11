module JsxComponentRef.Components

open Fable.Core
open Fable.Core.JsInterop
open JsxComponentRef.Screens

#nowarn "3884" // A function value in an interpolated string

let Screen: obj = importMember "./screen.js"

[<JSX.Component>]
let NoParams () = JSX.jsx $"""<div>Hi There</div>"""

[<JSX.Component>]
let Curried (name: string) (age: int) = JSX.jsx $"""<div>{name}{age}</div>"""

[<JSX.Component>]
let Tupled (name: string, age: int) = JSX.jsx $"""<div>{name}{age}</div>"""

[<JSX.Component>]
let Generic (item: 'T) = JSX.jsx $"""<div>{item}</div>"""

[<JSX.Component>]
let inline Inlined () = JSX.jsx $"""<div>Inlined</div>"""

// A component used as a value in a JSX template is passed by reference
let templateHoles () =
    JSX.jsx
        $"""<div>
    <Screen component={NoParams} />
    <Screen component={Curried} />
    <Screen component={Tupled} />
    <Screen component={Generic} />
    <Screen component={Imported} />
    <Screen component={Screens.Member} />
</div>"""

// So is one used as the value of a prop, or as the tag
let createProps () =
    JSX.create Screen [ "component" ==> NoParams; "fallback", box Curried ]

let createTag () = JSX.create NoParams []

// These are functions called by position, not components, and keep the arrow function
let wrapped () =
    JSX.jsx
        $"""<div>
    <Screen render={Curried "partial"} />
    <Screen render={fun name age -> Curried name age} />
    <Screen render={fun age name -> Curried name age} />
    <Screen render={Inlined} />
</div>"""

// A call still renders the component as an element
let called () = JSX.jsx $"""<div>{NoParams()}</div>"""

// Outside JSX the type is an F# function, which is called by position
let apply (render: string -> int -> JSX.Element) = render "a" 1
let asFunction: unit -> JSX.Element = NoParams
let asArgument () = apply Curried
