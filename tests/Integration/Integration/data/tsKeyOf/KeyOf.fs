module KeyOf

open Fickl.Core

type Config =
    {
        Port: int
        Host: string
        mutable Retries: int
    }

type IConfig =
    abstract Port: int
    abstract Name: string with get, set

let port = keyof (fun (c: Config) -> c.Port)
let retries = keyof (fun (c: Config) -> c.Retries)
let name = keyof (fun (c: IConfig) -> c.Name)

let readPort (c: Config) : int = KeyOf.get port c
let bump (c: Config) = KeyOf.set retries c (c.Retries + 1)
let tryRead (key: keyof<Config>) (c: Config) = KeyOf.tryGet key c
let ofString (s: string) : keyof<Config, int> = KeyOf.unsafeOfString s
let widened: keyof<Config> = KeyOf.widen port

let describe (key: keyof<Config>) =
    match key with
    | Key "Port" -> "port"
    | _ -> key.Value

// Generic code indexes through Record<KeysOfType<T, R>, R>, which TypeScript can resolve
let read (key: keyof<'T, 'R>) (target: 'T) : 'R = KeyOf.get key target
let write (key: keyof<'T, 'R>) (target: 'T) (value: 'R) = KeyOf.set key target value
