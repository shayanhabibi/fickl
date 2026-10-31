// Change: `valueof<'T>` for `T[keyof T]`
// Fickl: `Fickl.Core.valueof<'T>` is the value of some property of `'T`, typed `T[keyof T]` in TypeScript, built with `ValueOf.ofKey` and read as a type with a visible cast.
// Issues: 177
// Args: --declaration
module Main

open Fable.Core
open Fickl.Core

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: obj, encoding: string) : string = jsNative

[<Emit("new URL($0, import.meta.url)")>]
let besideThisFile (name: string) : obj = jsNative

type Server = { Port: int; Host: string }

let anyValue (key: keyof<Server, 'R>) (target: Server) : valueof<Server> = ValueOf.ofKey key target

let server = { Port = 80; Host = "localhost" }
let port = anyValue (keyof (fun (s: Server) -> s.Port)) server

let declaration = readFileSync (besideThisFile "Main.d.ts", "utf8")

Check.equal 80 (ValueOf.unsafeAs<Server, int> port)
Check.equal true (declaration.Contains "export declare function anyValue<R>(key: KeysOfType<Server, R>, target: Server): Server[keyof Server]")
