// Change: read-only collections in TypeScript annotations
// Fickl: `IReadOnlyCollection`, `IReadOnlyList` and `IReadOnlyDictionary` are annotated as types a TypeScript readonly array or `ReadonlyMap` satisfies instead of `any`, and `Fickl.Core.JS.ReadonlyArray` binds `ReadonlyArray<T>`.
// Issues: 146
// Args: --declaration
module Main

open System.Collections.Generic
open Fable.Core
open Fickl.Core.JS

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: obj, encoding: string) : string = jsNative

[<Emit("new URL($0, import.meta.url)")>]
let besideThisFile (name: string) : obj = jsNative

[<Emit("Object.freeze([1, 2, 3])")>]
let frozen () : ReadonlyArray<int> = jsNative

let sum (xs: ReadonlyArray<int>) = Seq.sum xs
let size (xs: IReadOnlyCollection<int>) = xs.Count
let firstOf (xs: IReadOnlyList<int>) = Seq.head xs
let find (d: IReadOnlyDictionary<string, int>) (key: string) = d.[key]

let declaration = readFileSync (besideThisFile "Main.d.ts", "utf8")

Check.equal 6 (sum (frozen ()))
Check.equal 3 (sum (ReadonlyArray.ofArray [| 1; 2 |]))
Check.equal 2 (size [| 1; 2 |])
Check.equal 1 (firstOf [ 1; 2 ])
Check.equal 5 (find (Map [ ("a", 5) ]) "a")
Check.equal true (declaration.Contains "export declare function sum(xs: ReadonlyArray<int32>): int32")
Check.equal true (declaration.Contains "export declare function size(xs: Iterable<int32>): int32")
Check.equal true (declaration.Contains "export declare function firstOf(xs: Iterable<int32>): int32")
Check.equal true (declaration.Contains "export declare function find(d: IReadOnlyMap<string, int32>, key: string): int32")
