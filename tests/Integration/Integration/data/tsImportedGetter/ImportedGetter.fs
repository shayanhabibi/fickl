module ImportedGetter

open Fable.Core

type Marker =
    interface
    end

[<ImportMember("./sentinel.js")>]
let nothing: Marker = jsNative

// A getter returning an imported value is typed with the property's type, not as a function
type Api =
    static member Nothing: Marker = nothing
    static member NothingUnboxed: Marker = unbox nothing

let useGetters () : Marker list = [ Api.Nothing; Api.NothingUnboxed ]
