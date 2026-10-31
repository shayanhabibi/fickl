module WeakRef

open System
open Fickl.Core

type Node(name: string) =
    member _.Name = name

let track (node: Node) : WeakReference<Node> = WeakReference<Node>(node)

let tryName (reference: WeakReference<Node>) =
    match reference.TryGetTarget() with
    | true, node -> Some node.Name
    | _ -> None

let trackAny<'T when 'T: not struct> (value: 'T) = WeakReference<'T>(value)

let tryTarget<'T when 'T: not struct> (reference: WeakReference<'T>) : 'T option =
    match reference.TryGetTarget() with
    | true, value -> Some value
    | _ -> None

let registry = JS.FinalizationRegistry(fun (held: string) -> printfn "collected %s" held)

let watch (node: Node) = registry.register (node, node.Name, node)

let native (node: Node) : JS.WeakRef<Node> = JS.WeakRef(node)
