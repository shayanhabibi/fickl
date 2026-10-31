module Fable.Tests.WeakReferences

open System
open Fickl.Core
open Util.Testing

type private Node(name: string) =
    member _.Name = name

let tests =
    testList "WeakReferences" [
        testCase "WeakReference.TryGetTarget gives a live target" <| fun () ->
            let node = Node("a")
            let reference = WeakReference<Node>(node)

            match reference.TryGetTarget() with
            | true, target -> equal true (obj.ReferenceEquals(node, target))
            | false, _ -> failwith "the target is alive"

        testCase "WeakReference.TryGetTarget writes to an out argument" <| fun () ->
            let node = Node("b")
            let reference = WeakReference<Node>(node, false)
            let mutable target = Unchecked.defaultof<Node>
            equal true (reference.TryGetTarget(&target))
            equal "b" target.Name

        testCase "WeakReference works in generic code" <| fun () ->
            let tryTarget (reference: WeakReference<'T>) =
                match reference.TryGetTarget() with
                | true, value -> Some value
                | _ -> None

            let values = ResizeArray [ 1; 2 ]
            equal (Some values) (tryTarget (WeakReference<_>(values)))

#if FABLE_COMPILER
        testCase "WeakReference is a JS WeakRef" <| fun () ->
            let reference = WeakReference<Node>(Node("c"))
            equal true (box reference :? JS.WeakRef<Node>)

        testCase "JS.WeakRef.deref gives a live target" <| fun () ->
            let node = Node("d")
            let reference = JS.WeakRef(node)
            equal (Some "d") (reference.deref () |> Option.map _.Name)

        testCase "JS.FinalizationRegistry registers and unregisters" <| fun () ->
            let registry = JS.FinalizationRegistry(fun (_: string) -> ())
            let node = Node("e")
            let token = obj ()
            registry.register (node, "e", token)
            registry.register (Node("f"), "f")
            equal true (registry.unregister token)
            equal false (registry.unregister token)
#endif
    ]
