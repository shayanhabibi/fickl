module ReExportImport

open Fable.Core
open Fable.Core.JsInterop

type Counter =
    abstract value: int

let callLog () =
    let log: obj -> unit = import "log" "./logger.js"
    log "y"

// Not used in this file: re-exported straight from the module
let SlBreadcrumb: obj = importMember "@shoelace-style/shoelace/dist/react"
let renamed: int = import "count" "./lib.js"
let Default: obj = importDefault "./lib.js"
let Ns: obj = importAll "./lib.js"
let ``my-name``: int = import "count" "./lib.js"
let casted: Counter = unbox (import "counter" "./lib.js")
let add (x: int) (y: int) : int = importMember "./lib.js"
let rooted: string = import "FOO" "/env.js"
let generated: obj = import "thing" "${outDir}/gen/thing.js"

module Nested =
    let inner: int = import "count" "./lib.js"

// Used in this file: imported under its own name, then exported
let bump: unit -> unit = importMember "./lib.js"
let live: int = importMember "./lib.js"

let readLive () =
    bump ()
    live

// The same import under another local name
let logAgain: obj -> unit = import "log" "./logger.js"
let useLog () = logAgain "x"

// Named in an Emit macro
let emitUser: int = importMember "./lib.js"

[<Emit("emitUser + 1")>]
let emitted: int = jsNative

let useEmitted () = emitted

// Not exactly an import: bound to a constant, as before
let mutable private mutableCount: int = import "count" "./lib.js"
let computed: int = (import "count" "./lib.js") + 1
let dotted: int = import "nested.inner" "./lib.js"
