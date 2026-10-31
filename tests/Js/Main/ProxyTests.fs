module Fable.Tests.Proxies

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core
open Util.Testing

#if FABLE_COMPILER
type private Point = { mutable X: int; mutable Y: int }

let private nameOf (key: JS.PropertyKey) =
    match key with
    | U3.Case1 name -> name
    | U3.Case2 n -> string n
    | U3.Case3 _ -> "symbol"
#endif

let tests =
    testList "Proxies" [
#if FABLE_COMPILER
        testCase "A get trap intercepts property reads" <| fun () ->
            let reads = ResizeArray<string>()
            let point = { X = 1; Y = 2 }

            let proxy =
                JS.Proxy.create point (JS.ProxyHandler.create (fun h ->
                    h.get <- fun target key receiver ->
                        reads.Add(nameOf key)
                        JS.Reflect.get (target, key, receiver)))

            equal 1 proxy.X
            equal 2 proxy.Y
            equal [ "X"; "Y" ] (List.ofSeq reads)

        testCase "A handler holds only the traps that are set" <| fun () ->
            let handler = JS.ProxyHandler.create<obj> (fun h -> h.has <- fun _ _ -> true)
            equal [| "has" |] (JS.Constructors.Object.keys handler |> Seq.toArray)

        testCase "Unset traps forward to the target" <| fun () ->
            let point = { X = 1; Y = 2 }
            let proxy = JS.Proxy.create point (JS.ProxyHandler.create (fun h -> h.has <- fun _ _ -> true))
            proxy.X <- 5
            equal 5 point.X
            equal true (emitJsExpr proxy "'anything' in $0")

        testCase "A set trap can reject or transform writes" <| fun () ->
            let target = obj ()

            let proxy =
                JS.Proxy.create target (JS.ProxyHandler.create (fun h ->
                    h.set <- fun t key value receiver ->
                        match value with
                        | :? int as n -> JS.Reflect.set (t, key, box (n * 10), receiver)
                        | _ -> false))

            equal true (JS.Reflect.set (proxy, !^ "n", box 4))
            equal 40 (target?n)
            equal false (JS.Reflect.set (proxy, !^ "s", box "text"))

        testCase "An apply trap intercepts calls" <| fun () ->
            let add = System.Func<int, int, int>(fun a b -> a + b)

            let proxy =
                JS.Proxy.create add (JS.ProxyHandler.create (fun h ->
                    h.apply <- fun target thisArg args -> box (unbox<int> (JS.Reflect.apply (target, thisArg, args)) + 100)))

            equal 103 (proxy.Invoke(1, 2))

        testCase "A revoked proxy throws" <| fun () ->
            let revocable = JS.Proxy.revocable (createObj [ "a" ==> 1 ]) (JS.ProxyHandler.create ignore)
            equal 1 (revocable.proxy?a)
            revocable.revoke ()

            let threw =
                try
                    revocable.proxy?a |> ignore
                    false
                with :? JS.TypeError ->
                    true

            equal true threw

        testCase "Reflect reads keys, prototypes and extensibility" <| fun () ->
            let tag = JS.Symbol.create "tag"
            let o = createObj [ "a" ==> 1 ]
            JS.Reflect.set (o, !^ tag, box 2) |> ignore
            let keys = JS.Reflect.ownKeys o
            equal 2 keys.Length
            equal "a" (nameOf keys[0])
            equal "symbol" (nameOf keys[1])
            equal true (JS.Reflect.has (o, !^ "a"))
            equal true (JS.Reflect.deleteProperty (o, !^ "a"))
            equal false (JS.Reflect.has (o, !^ "a"))
            equal true (JS.Reflect.isExtensible o)
            equal true (JS.Reflect.preventExtensions o)
            equal false (JS.Reflect.isExtensible o)
            equal false (JS.Reflect.set (o, !^ "b", box 3))
            equal true (obj.ReferenceEquals(JS.Reflect.getPrototypeOf (obj ()), JS.Reflect.getPrototypeOf (createObj [])))

        testCase "Symbol.for shares a symbol by key" <| fun () ->
            equal true (obj.ReferenceEquals(JS.Symbol.``for`` "fickl.test", JS.Symbol.``for`` "fickl.test"))
            equal (Some "fickl.test") (JS.Symbol.keyFor (JS.Symbol.``for`` "fickl.test"))
            equal None (JS.Symbol.keyFor (JS.Symbol.create "local"))
            equal (Some "local") (JS.Symbol.create "local").description
            equal false (obj.ReferenceEquals(JS.Symbol.create "x", JS.Symbol.create "x"))
#endif
    ]
