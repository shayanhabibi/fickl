module Fable.Tests.TypeGuards

open Util.Testing
open Fable.Tests.Util
open Fickl.Core

type Point = { X: int; Y: int }

type Named =
    abstract name: string

[<TypePredicate("value", typeof<string>)>]
let isText (value: obj) : bool = value :? string

[<TypePredicate("value", typeof<Point>)>]
let tryPoint (value: obj) : Point option =
    match value with
    | :? Point as point -> Some point
    | _ -> None

[<TypePredicate("value", typeof<string>)>]
let tryText (prefix: string) (value: obj) : string voption =
    match value with
    | :? string as text when text.StartsWith prefix -> ValueSome text
    | _ -> ValueNone

[<TypeAssertion("value", typeof<Point>)>]
let assertPoint (value: obj) : unit =
    if (tryPoint value).IsNone then
        failwith "not a point"

[<TypeAssertion("value", typeof<Point>)>]
let checkPoint (value: obj) : Result<Point, exn> =
    match tryPoint value with
    | Some point -> Ok point
    | None -> Error(exn "not a point")

type Shapes(minX: int) =
    [<TypePredicate("value", typeof<Point>)>]
    member _.TryPoint(value: obj) : Point option =
        tryPoint value |> Option.filter (fun point -> point.X >= minX)

    [<TypePredicate("value", typeof<string>)>]
    static member TryText(value: obj) : string option =
        match value with
        | :? string as text -> Some text
        | _ -> None

#if FABLE_COMPILER
[<Fable.Core.Import("isNamed", "./js/typeGuards.js"); TypePredicate("value", typeof<Named>)>]
let tryNamed (value: obj) : Named option = Fable.Core.Util.jsNative

[<Fable.Core.Import("assertNamed", "./js/typeGuards.js"); TypeAssertion("value", typeof<Named>)>]
let checkNamed (value: obj) : Result<Named, exn> = Fable.Core.Util.jsNative
#endif

let tests =
    testList "TypeGuards" [
        testCase "predicates returning bool are called as written" <| fun () ->
            isText (box "a") |> equal true
            isText (box 1) |> equal false

        testCase "option predicates give Some of the argument when they hold" <| fun () ->
            let point = { X = 1; Y = 2 }
            tryPoint (box point) |> equal (Some point)
            tryPoint (box "point") |> equal None

        testCase "voption predicates give ValueSome of the argument when they hold" <| fun () ->
            tryText "a" (box "abc") |> equal (ValueSome "abc")
            tryText "b" (box "abc") |> equal ValueNone
            tryText "a" (box 1) |> equal ValueNone

        testCase "option predicates evaluate their arguments once and in order" <| fun () ->
            let calls = ResizeArray()

            let record name (value: 'T) =
                calls.Add name
                value

            tryText (record "prefix" "a") (record "value" (box "abc")) |> equal (ValueSome "abc")
            List.ofSeq calls |> equal [ "prefix"; "value" ]

        testCase "option predicates can be passed as functions" <| fun () ->
            let point = { X = 3; Y = 4 }
            [ box 1; box point; box "x" ] |> List.choose tryPoint |> equal [ point ]

        testCase "option predicates work in patterns" <| fun () ->
            let describe (value: obj) =
                match tryPoint value with
                | Some point -> string (point.X + point.Y)
                | None -> "none"

            describe (box { X = 1; Y = 2 }) |> equal "3"
            describe (box 5) |> equal "none"

        testCase "class members can be option predicates" <| fun () ->
            let shapes = Shapes(2)
            shapes.TryPoint(box { X = 2; Y = 0 }) |> equal (Some { X = 2; Y = 0 })
            shapes.TryPoint(box { X = 1; Y = 0 }) |> equal None
            Shapes.TryText(box "t") |> equal (Some "t")
            Shapes.TryText(box 1) |> equal None

        testCase "unit assertions throw when they fail" <| fun () ->
            assertPoint (box { X = 0; Y = 0 })

            throwsAnyError (fun () -> assertPoint (box "point"))

        testCase "Result assertions give Ok of the argument or Error of what was thrown" <| fun () ->
            let point = { X = 5; Y = 6 }
            checkPoint (box point) |> equal (Ok point)

            match checkPoint (box "point") with
            | Error e -> e.Message |> equal "not a point"
            | Ok _ -> failwith "expected an error"

#if FABLE_COMPILER
        testCase "imported option predicates give Some of the argument" <| fun () ->
            let named = Fable.Core.JsInterop.createObj [ "name", box "Ada" ] :?> Named
            tryNamed (box named) |> Option.map _.name |> equal (Some "Ada")
            tryNamed (box 1) |> equal None

        testCase "imported Result assertions catch what the guard throws" <| fun () ->
            let named = Fable.Core.JsInterop.createObj [ "name", box "Ada" ] :?> Named

            match checkNamed (box named) with
            | Ok value -> value.name |> equal "Ada"
            | Error _ -> failwith "expected Ok"

            match checkNamed (box 1) with
            | Error e -> e.Message |> equal "expected a name"
            | Ok _ -> failwith "expected an error"
#endif
    ]
