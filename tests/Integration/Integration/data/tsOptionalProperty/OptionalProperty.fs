module OptionalProperty

open Fable.Core.JsInterop

// An option property of an interface is an optional property, so TypeScript can leave the key out
type IOpts =
    abstract name: string
    abstract size: int option
    abstract vsize: int voption
    abstract label: string option with get, set
    // These options are wrapped at runtime, so they keep the Option type
    abstract nested: int option option
    abstract nothing: unit option

type IMore =
    inherit IOpts
    abstract extra: float option

type Impl() =
    interface IOpts with
        member _.name = "a"
        member _.size = Some 1
        member _.vsize = ValueNone
        member val label = None with get, set
        member _.nested = Some None
        member _.nothing = Some()

let fromObjExpr =
    { new IOpts with
        member _.name = "a"
        member _.size = None
        member _.vsize = ValueSome 1

        member _.label
            with get () = None
            and set _ = ()

        member _.nested = None
        member _.nothing = None
    }

let read (o: IOpts) =
    defaultArg o.size 0 + (defaultArg o.label "").Length

let write (o: IOpts) (label: string option) = o.label <- label

let fromJsOptions = jsOptions<IOpts> (fun o -> o.label <- Some "a")
