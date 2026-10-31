module Fable.Tests.NewTarget

open Util.Testing
open Fable.Tests.Util

#if FABLE_COMPILER
open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core

type Shape() =
    do
        if JS.isNewTarget<Shape> then
            failwith "Shape is abstract"

    member _.Sides = 0

type Square() =
    inherit Shape()

type Base() =
    let created: obj = JS.newTarget
    member _.Created = created

type Derived() =
    inherit Base()

type Tagged(target: obj) =
    member _.Target = target

type TaggedChild() =
    inherit Tagged(JS.newTarget)

let mutable private countedSubclasses = 0

type Counted() =
    do
        if not (obj.ReferenceEquals(JS.newTarget, jsConstructor<Counted>)) then
            countedSubclasses <- countedSubclasses + 1

type CountedChild() =
    inherit Counted()
#endif

let tests =
    testList "NewTarget" [
#if FABLE_COMPILER
        testCase "JS.isNewTarget guards an abstract base class" <| fun () ->
            throwsAnyError (fun () -> Shape())
            Square().Sides |> equal 0

        testCase "JS.newTarget is the subclass being constructed" <| fun () ->
            obj.ReferenceEquals(Derived().Created, jsConstructor<Derived>) |> equal true
            obj.ReferenceEquals(Base().Created, jsConstructor<Base>) |> equal true

        testCase "JS.newTarget can be passed to the base constructor" <| fun () ->
            obj.ReferenceEquals(TaggedChild().Target, jsConstructor<TaggedChild>) |> equal true

        testCase "JS.newTarget compares by reference" <| fun () ->
            Counted() |> ignore
            CountedChild() |> ignore
            CountedChild() |> ignore
            countedSubclasses |> equal 2
#endif
    ]
