module NativeDecorators

open Fickl.Core

let classFactory = "import alias collision"

let setterRegistry: obj = Fable.Core.JsInterop.import "setterRegistry" "./decorators.js"

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

[<JsDecorator("subclassDecorator", "./decorators.js")>]
type HiddenBase() =
    [<JsDecorator("identityDecorator", "./decorators.js")>]
    member val Value = 1 with get, set
    [<JsDecorator("identityDecorator", "./decorators.js")>]
    member _.Read() = 1
    [<SymbolKey("Symbol.for('fickl.hidden.method')")>]
    member _.SymbolRead() = 1
    [<JsDecorator("identityDecorator", "./decorators.js")>]
    member _.Apply(callback: int -> int -> int, a: int, b: int) = callback a b
    [<JsDecorator("opaqueMethodDecorator", "./decorators.js")>]
    member _.Opaque(value: int) = value + 1
    [<JsDecorator("identityDecorator", "./decorators.js")>]
    member _.Optional(?value: int) = defaultArg value 5
    [<JsDecorator("identityDecorator", "./decorators.js"); Fable.Core.ParamObject>]
    member _.Named(?left: int, ?right: int) = defaultArg left 1 + defaultArg right 2
    [<JsDecorator("identityDecorator", "./decorators.js")>]
    member _.Sum([<System.ParamArray>] values: int[]) = Array.sum values

type HiddenDerived() =
    inherit HiddenBase()
    [<JsDecorator("identityDecorator", "./decorators.js")>]
    member val Value = 2 with get, set
    [<JsDecorator("identityDecorator", "./decorators.js")>]
    member _.Read() = 2
    [<SymbolKey("Symbol.for('fickl.hidden.method')")>]
    member _.SymbolRead() = 2
    [<JsDecorator("identityDecorator", "./decorators.js")>]
    member _.BaseRead() =
        base.Value <- 3
        base.Value, base.Read(), base.SymbolRead()

let hiddenRead () =
    let value = HiddenDerived()
    let before = (value :> HiddenBase).Value, value.Value
    (value :> HiddenBase).Value <- 11
    before, ((value :> HiddenBase).Value, value.Value), ((value :> HiddenBase).Read(), value.Read()), ((value :> HiddenBase).SymbolRead(), value.SymbolRead())

let hiddenCallback () = HiddenDerived().Apply((fun a b -> a + b), 3, 4)

let opaqueRead () = HiddenDerived().Opaque(4)

let argumentShapes () =
    let value = HiddenDerived()
    value.Optional(), value.Optional(value = 9), value.Named(right = 7), value.Sum([| 2; 3; 4 |])

let nonCallableRead () =
    let mutable calls = 0
    let argument () = calls <- calls + 1; 1
    try
        HiddenDerived().Opaque(argument()) |> ignore
        false, calls
    with _ -> true, calls

let explicitBaseRead () = HiddenDerived().BaseRead()

let hiddenReceiver () =
    let value = HiddenDerived()
    let mutable calls = 0
    let receiver () =
        calls <- calls + 1
        value :> HiddenBase
    receiver().Value <- 7
    let actual = receiver().Value + receiver().Read()
    calls, actual, value.Value

let hiddenNull () =
    let mutable calls = 0
    let argument () = calls <- calls + 1; 1
    try
        Unchecked.defaultof<HiddenBase>.Apply((fun a b -> a + b), argument(), 2) |> ignore
        false, calls
    with _ -> true, calls

let hiddenNullSetter () =
    let mutable calls = 0
    let argument () = calls <- calls + 1; 1
    try
        Unchecked.defaultof<HiddenBase>.Value <- argument()
        false, calls
    with _ -> true, calls

let frozenHidden () =
    let value = HiddenDerived()
    Fable.Core.JsInterop.emitJsStatement value "Object.freeze($0)"
    (value :> HiddenBase).Value <- 9
    (value :> HiddenBase).Value, value.Value

type HiddenFieldBase() =
    [<SymbolKey("Symbol.for('fickl.hidden.baseField')"); JsDecorator("identityDecorator", "./decorators.js", true)>]
    member val Value = 1 with get, set

type HiddenFieldDerived() =
    inherit HiddenFieldBase()
    [<SymbolKey("Symbol.for('fickl.hidden.derivedField')"); JsDecorator("identityDecorator", "./decorators.js", true)>]
    member val Value = 2 with get, set
    [<JsDecorator("identityDecorator", "./decorators.js")>]
    member _.BaseRead() =
        base.Value <- 9
        base.Value

let distinctHiddenFields () =
    let value = HiddenFieldDerived()
    (value :> HiddenFieldBase).Value <- 8
    (value :> HiddenFieldBase).Value, value.Value

let explicitBaseFieldRead () = HiddenFieldDerived().BaseRead()

type OrderedAccessor() =
    let mutable value = 0
    [<SymbolKey("setterRegistry.key")>]
    member _.Value with get() = value and set next = value <- next

let orderedSetter () =
    let value = OrderedAccessor()
    value.Value <- Fable.Core.JsInterop.emitJsExpr () "(setterRegistry.events.push('rhs'), 7)"

type IExplicitState =
    [<SymbolKey("Symbol.for('fickl.explicit.positive')")>]
    abstract State: int with get, set

type ExplicitState() =
    let mutable state = 1
    interface IExplicitState with
        member _.State with get() = state and set value = state <- value

let explicitState () =
    let value = ExplicitState() :> IExplicitState
    value.State <- 8
    value.State
