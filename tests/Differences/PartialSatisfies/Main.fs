// Change: `partial` and `Partial<'T>`
// Fickl: `partial` builds an object with some of a type's properties, typed `Partial<T>` and checked with `satisfies` in TypeScript instead of cast to the full type.
// Issues: 151
// Args: --declaration
module Main

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core
open Fickl.Core.JsInterop

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: obj, encoding: string) : string = jsNative

[<Emit("new URL($0, import.meta.url)")>]
let besideThisFile (name: string) : obj = jsNative

type State =
    abstract count: int with get, set
    abstract name: string with get, set

let mutable state = jsOptions<State> (fun s -> s.count <- 0; s.name <- "start")

let setState (patch: Partial<State>) =
    state <- !!(JS.Constructors.Object.assign (createEmpty, state, patch))

let rename (name: string) : Partial<State> = partial (fun s -> s.name <- name)

let snapshot () : Readonly<State> = !!(JS.Constructors.Object.freeze (full (partial (fun (s: State) -> s.count <- state.count))))

setState (partial (fun s -> s.count <- 1))
setState (rename "renamed")

let declaration = readFileSync (besideThisFile "Main.d.ts", "utf8")

Check.equal 1 state.count
Check.equal "renamed" state.name
Check.equal 1 (unbox<State> (snapshot ())).count
Check.equal true (declaration.Contains "export declare function setState(patch: Partial<State>): void")
Check.equal true (declaration.Contains "export declare function rename(name: string): Partial<State>")
Check.equal true (declaration.Contains "export declare function snapshot(): Readonly<State>")
