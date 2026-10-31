// Change: `keyof<'T>` and `keyof<'T, 'R>` property keys
// Fickl: `keyof _.Port` is the property's JavaScript name, typed `keyof T & string` or `KeysOfType<T, R>` in TypeScript, and `KeyOf.get`/`KeyOf.set` index an object by it.
// Issues: 174
// Args: --declaration
module Main

open Fable.Core
open Fickl.Core

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: obj, encoding: string) : string = jsNative

[<Emit("new URL($0, import.meta.url)")>]
let besideThisFile (name: string) : obj = jsNative

type Server =
    {
        Port: int
        Host: string
        mutable Retries: int
    }

let port = keyof (fun (s: Server) -> s.Port)
let retries = keyof (fun (s: Server) -> s.Retries)

let read (key: keyof<'T, 'R>) (target: 'T) : 'R = KeyOf.get key target
let describe (key: keyof<Server>) = key.Value

let server = { Port = 80; Host = "localhost"; Retries = 0 }
KeyOf.set retries server 2

let declaration = readFileSync (besideThisFile "Main.d.ts", "utf8")

Check.equal "Port" port.Value
Check.equal 80 (KeyOf.get port server)
Check.equal 2 (read retries server)
Check.equal "Port" (describe (KeyOf.widen port))
Check.equal true (declaration.Contains "export declare function read<T, R>(key: KeysOfType<T, R>, target: T): R")
Check.equal true (declaration.Contains "export declare function describe(key: (keyof Server) & string): string")
