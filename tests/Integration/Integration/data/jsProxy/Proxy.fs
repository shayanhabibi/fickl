module Proxy

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core

type Point = { X: int; Y: int }

let reads = ResizeArray<string>()

let logged (point: Point) : Point =
    JS.Proxy.create
        point
        (JS.ProxyHandler.create (fun h ->
            h.get <-
                fun target key receiver ->
                    match key with
                    | U3.Case1 name -> reads.Add name
                    | _ -> ()

                    JS.Reflect.get (target, key, receiver)
        ))

let readOnly (point: Point) : JS.RevocableProxy<Point> =
    JS.Proxy.revocable point (JS.ProxyHandler.create (fun h -> h.set <- fun _ _ _ _ -> false))

let keys (o: obj) : JS.PropertyKey[] = JS.Reflect.ownKeys o

let tag: JS.Symbol = JS.Symbol.create "tag"

let tagKey: JS.PropertyKey = !^tag
