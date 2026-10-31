module NewTarget

open Fickl.Core

// `new.target === Shape`: Shape can only be built as the base of a subclass
type Shape() =
    do
        if JS.isNewTarget<Shape> then
            failwith "Shape is abstract"

type Square() =
    inherit Shape()

type Base() =
    let created: obj = JS.newTarget
    member _.Created = created

type Tagged(target: obj) =
    member _.Target = target

type TaggedChild() =
    inherit Tagged(JS.newTarget)
