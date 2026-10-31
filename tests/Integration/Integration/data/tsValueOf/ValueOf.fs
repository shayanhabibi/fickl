module ValueOf

open Fickl.Core

type Config = { Port: int; Host: string }

type Pair<'A, 'B> = { First: 'A; Second: 'B }

let port = keyof (fun (c: Config) -> c.Port)

let anyValue (c: Config) : valueof<Config> = ValueOf.ofKey port c
let portOf (c: Config) : int = ValueOf.unsafeAs<Config, int> (anyValue c)
let asConfig (v: valueof<Pair<Config, int>>) : Config = ValueOf.unsafeAs v

// Generic code casts through the indexed access type too
let pick (key: keyof<'T, 'R>) (target: 'T) : valueof<'T> = ValueOf.ofKey key target
let read (value: valueof<'T>) : 'R = ValueOf.unsafeAs value
