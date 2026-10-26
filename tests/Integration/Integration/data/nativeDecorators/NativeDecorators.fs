module NativeDecorators

open Fickl.Core

let classFactory = "import alias collision"

[<JsDecorator("classFactory('class')", "./decorators.js")>]
type Counter() =
    [<JsDecorator("methodFactory('outer')", "./decorators.js")>]
    [<JsDecorator("methodFactory('inner')", "./decorators.js")>]
    member _.Message(value: string) = value

    [<JsDecorator("accessorFactory('count')", "./decorators.js")>]
    member val Count = 0 with get, set

    [<JsDecorator("getterFactory('label')", "./decorators.js")>]
    member _.Label = "label"

    [<JsDecorator("fieldFactory('field')", "./decorators.js", true)>]
    member val Field = 1 with get, set

    [<JsDecorator("getterFactory('readonly')", "./decorators.js")>]
    member val ReadOnly = "readonly" with get

    [<JsDecorator("methodFactory('static')", "./decorators.js")>]
    static member StaticMessage(value: string) = value

    [<SymbolKey("Symbol.toPrimitive"); JsDecorator("methodFactory('symbol')", "./decorators.js")>]
    member _.ToPrimitive(_hint: string) = "primitive"

    [<JsDecorator("methodFactory('$0')", "./decorators.js")>]
    member _.DollarLiteral(value: string) = value

[<JsDecorator("Reflect.get(Object, 'freeze')")>]
type Frozen() = class end

[<JsDecorator("Reflect.get(globalThis, '$0')")>]
type DollarGlobal() = class end

type ManualAccessors() =
    let mutable value = ""
    [<JsDecorator("getterFactory('manual')", "./decorators.js")>]
    member _.Value with get() = value and set next = value <- next
    [<JsDecorator("setterFactory('sink')", "./decorators.js")>]
    member _.Sink with set next = value <- next

[<AbstractClass>]
type Base() =
    abstract Message: string -> string
    default _.Message(value) = "base" + value

type Derived() =
    inherit Base()
    [<JsDecorator("methodFactory('override')", "./decorators.js")>]
    override _.Message(value) = value

type Optional<'T>() =
    [<JsDecorator("identityDecorator", "./decorators.js")>]
    member val Value: 'T option = None with get, set

let create () = Counter()
let read (value: Counter) = value.Message("hello"), value.Label, value.Count, value.Field
let staticRead () = Counter.StaticMessage("hello")
let manualRead (value: string) =
    let accessors = ManualAccessors()
    accessors.Value <- value
    let first = accessors.Value
    accessors.Sink <- "sink"
    first, accessors.Value

let overrideRead () =
    let value = Derived()
    value.Message("hello"), (value :> Base).Message("hello")

let optionalRead () =
    let value = Optional<string>()
    let wasNone = value.Value.IsNone
    value.Value <- Some "optional"
    wasNone, value.Value.Value
