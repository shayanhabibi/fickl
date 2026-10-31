module Fable.Tests.LiteTsInterop

open System
open Util.Testing
open Fable.Core

#if FABLE_COMPILER
open Fable.Core.JsInterop
open Fable.Core.DynamicExtensions
open Fable.Core.Experimental
#endif

type JsOptions =
    abstract foo: string with get, set
    abstract bar: int with get, set

#if FABLE_COMPILER
module PojoInterface =
    [<JS.Pojo; AllowNullLiteral>]
    type Animal(name: string) =
        member val name = name

    [<JS.Pojo; AllowNullLiteral>]
    type Dog(name: string, breed: string, ?age: int) =
        inherit Animal(name)
        member val name = name
        member val breed = breed
        member val age = age with get, set

module TaggedUnion =
    type Take =
        abstract ``type``: string
        abstract fromIndex: int
        abstract toIndex: int

    type Edit =
        abstract text: string

    [<RequireQualifiedAccess; TypeScriptTaggedUnion("type")>]
    type Command =
        | Take of Take
        | Edit of edit: Edit
        | Save

[<StringEnum; RequireQualifiedAccess>]
type Level =
    | [<CompiledValue(1)>] One
    | [<CompiledValue(2.5)>] Two
    | [<CompiledValue(true)>] Yes
    | [<CompiledName("auto")>] Auto
    | Plain

let private pickLevel (level: Level) = level

[<TypeScriptTaggedUnion("ok")>]
type Res =
    | [<CompiledValue(true)>] Good of value: int
    | [<CompiledValue(false)>] Bad of error: string

[<TypeScriptTaggedUnion("kind"); RequireQualifiedAccess>]
type Msg =
    | [<CompiledValue(0)>] Text of text: string
    | [<CompiledValue(1)>] Code of code: int
// Top level values get TS annotations, so tsc checks the typed array type names exist
module TypedArrays =
    let int8s = JS.Constructors.Int8Array.Create [| 1y; -2y |]
    let uint8s = JS.Constructors.Uint8Array.Create [| 1uy; 2uy |]
    let uint8Clamped = JS.Constructors.Uint8ClampedArray.Create [| 1; 300 |]
    let int16s = JS.Constructors.Int16Array.Create [| 1s; -2s |]
    let uint16s = JS.Constructors.Uint16Array.Create [| 1us; 2us |]
    let int32s = JS.Constructors.Int32Array.Create [| 1; -2 |]
    let uint32s = JS.Constructors.Uint32Array.Create [| 1u; 2u |]
    let float32s = JS.Constructors.Float32Array.Create [| 1.5; -2.5 |]
    let float64s = JS.Constructors.Float64Array.Create [| 1.5; -2.5 |]
    let bigInt64s = JS.Constructors.BigInt64Array.Create 2
    let makeFloat32s () = JS.Constructors.Float32Array.Create [| 0.0; 1.0 |]

// tsc checks the values against the annotations the EmitType texts write
module EmitTypes =
    [<Fickl.Core.EmitType("`data-${string}`"); Erase>]
    type DataAttr = DataAttr of string

    [<Fickl.Core.EmitType("Record<string, $0>")>]
    type Dict<'T> = interface end

    [<Fickl.Core.EmitType("Parameters<typeof JSON.stringify>[2]")>]
    type Indent = interface end

    let dataAttr (name: string) = DataAttr("data-" + name)
    let readAttr (DataAttr value) = value
    let attrs = [| DataAttr "data-a"; dataAttr "b" |]
    let dict (key: string) (value: 'T) : Dict<'T> = !!(createObj [ key ==> value ])
    let get (d: Dict<'T>) (key: string) : 'T = d?(key)
    let indent (spaces: int) : Indent = unbox spaces
    let stringify (value: obj) (indent: Indent) : string = emitJsExpr (value, indent) "JSON.stringify($0, null, $1)"

module Partials =
    open Fickl.Core
    open Fickl.Core.JsInterop

    type Counter =
        abstract count: int with get, set
        abstract label: string with get, set

    let mutable current = jsOptions<Counter> (fun c -> c.count <- 0; c.label <- "start")
    let patch (changes: Partial<Counter>) = current <- !!(JS.Constructors.Object.assign (createEmpty, current, changes))
    let bump () = patch (partial (fun c -> c.count <- current.count + 1))
    let relabel (label: string) : Partial<Counter> = partial (fun c -> c.label <- label)
    let freeze (counter: Counter) : Readonly<Counter> = !!(JS.Constructors.Object.freeze counter)

// TypeScript readonly arrays and maps must be accepted where F# only reads
module Readonlys =
    open System.Collections.Generic
    open Fickl.Core

    [<Emit("Object.freeze([1, 2, 3])")>]
    let frozenNumbers () : JS.ReadonlyArray<int> = jsNative

    [<Emit("new Map([['a', 1]])")>]
    let nativeMap () : IReadOnlyDictionary<string, int> = jsNative

    let sum (xs: JS.ReadonlyArray<int>) = Seq.sum xs
    let last (xs: JS.ReadonlyArray<int>) = xs.[xs.length - 1]
    let countAll (xs: IReadOnlyCollection<int>) = xs.Count
    let firstTwo (xs: IReadOnlyList<int>) = xs |> Seq.truncate 2 |> Seq.toList
    let find (d: IReadOnlyDictionary<string, int>) key =
        match d.TryGetValue key with
        | true, v -> v
        | _ -> -1

// The signatures say unknown; the bodies and the callers here still use the values as any
module Unknowns =
    open Fickl.Core

    let parse (text: string) : unknown = JS.JSON.parse text
    let field (value: unknown) (name: string) : unknown = value?(name)
    let lengthOf (value: unknown) = (unbox<string> value).Length
    let describe (value: unknown) =
        match value with
        | :? string as s -> "string " + s
        | _ -> "other"

    type IBox =
        abstract Get: unit -> unknown

    let box' (value: obj) = { new IBox with member _.Get() = value }
#endif

type HidingBase() =
    member _.Read() = 1
    member _.Name = "base"

type HidingDerived() =
    inherit HidingBase()
    member _.Read() = 2
    member _.Name = "derived"

let tests =
  testList "LiteTsInterop" [
    testCase "Members hiding base class members compile under noImplicitOverride" <| fun () ->
        let value = HidingDerived()
        (value.Read(), value.Name) |> equal (2, "derived")
        ((value :> HidingBase).Read(), (value :> HidingBase).Name) |> equal (1, "base")
    #if FABLE_COMPILER
    testCase "jsOptions works" <| fun _ ->
        let opts = jsOptions<JsOptions>(fun o ->
            o.foo <- "bar"
            o.bar <- 5
        )
        opts.foo |> equal "bar"
        opts.bar |> equal 5

    testCase "jsOptions works when it is directly replaced as a POJO" <| fun () ->
        let opts = jsOptions<JsOptions>(fun o ->
            // This function call avoid the optimization to literal POJO
            let foo () = "foo"
            o.foo <- foo()
            o.bar <- 5
        )
        opts.foo |> equal "foo"
        opts.bar |> equal 5

    testCase "[<Pojo>] generates interface and can be constructed" <| fun () ->
        let dog = PojoInterface.Dog("Buddy", "Labrador")
        dog.name |> equal "Buddy"
        dog.breed |> equal "Labrador"
        dog.age |> equal None

    testCase "[<Pojo>] optional field can be mutated" <| fun () ->
        let dog = PojoInterface.Dog("Max", "Beagle")
        dog.age |> equal None
        dog.age <- Some 5
        dog.age |> equal (Some 5)

    testCase "TS tagged union case with an interface field compiles to the value itself" <| fun () ->
        let take =
            { new TaggedUnion.Take with
                member _.``type`` = "take"
                member _.fromIndex = 1
                member _.toIndex = 3 }
        let describe = function
            | TaggedUnion.Command.Take t -> sprintf "take: %d-%d" t.fromIndex t.toIndex
            | TaggedUnion.Command.Edit e -> sprintf "edit: %s" e.text
            | TaggedUnion.Command.Save -> "save"
        let command = TaggedUnion.Command.Take take
        obj.ReferenceEquals(command, take) |> equal true
        command |> describe |> equal "take: 1-3"
        TaggedUnion.Command.Edit { new TaggedUnion.Edit with member _.text = "hi" } |> describe |> equal "edit: hi"
        TaggedUnion.Command.Save |> describe |> equal "save"
        !!{| ``type`` = "take"; fromIndex = 2; toIndex = 5 |} |> describe |> equal "take: 2-5"

    testCase "StringEnum CompiledValue cases are typed as their values" <| fun () ->
        pickLevel Level.One |> box |> equal (box 1)
        pickLevel Level.Two |> box |> equal (box 2.5)
        pickLevel Level.Yes |> box |> equal (box true)
        pickLevel Level.Auto |> box |> equal (box "auto")
        pickLevel Level.Plain |> box |> equal (box "plain")

    testCase "TS tagged union with boolean tags compiles and matches" <| fun () ->
        let describe = function
            | Good value -> sprintf "good %d" value
            | Bad error -> sprintf "bad %s" error
        Good 1 |> describe |> equal "good 1"
        Bad "no" |> describe |> equal "bad no"
        (Good 1)?ok |> equal true
        !!{| ok = false; error = "raw" |} |> describe |> equal "bad raw"

    testCase "TS tagged union with numeric tags compiles and matches" <| fun () ->
        let describe = function
            | Msg.Text text -> text
            | Msg.Code code -> string code
        Msg.Text "hi" |> describe |> equal "hi"
        Msg.Code 42 |> describe |> equal "42"
        (Msg.Code 42)?kind |> equal 1
    testCase "Fable.Core typed arrays are annotated with native TS types" <| fun () ->
        TypedArrays.int8s.[1] |> equal -2y
        TypedArrays.uint8s.[1] |> equal 2uy
        TypedArrays.uint8Clamped.[1] |> equal 255uy
        TypedArrays.int16s.[1] |> equal -2s
        TypedArrays.uint16s.[1] |> equal 2us
        TypedArrays.int32s.[1] |> equal -2
        TypedArrays.uint32s.[1] |> equal 2u
        TypedArrays.float32s.[1] |> equal -2.5f
        TypedArrays.float64s.[1] |> equal -2.5
        TypedArrays.bigInt64s.length |> equal 2
        TypedArrays.makeFloat32s().[1] |> equal 1.0f

    testCase "EmitType changes only the TypeScript annotation" <| fun () ->
        EmitTypes.dataAttr "x" |> EmitTypes.readAttr |> equal "data-x"
        EmitTypes.attrs |> Array.map EmitTypes.readAttr |> equal [| "data-a"; "data-b" |]
        EmitTypes.get (EmitTypes.dict "a" 1) "a" |> equal 1
        EmitTypes.stringify (box [| 1 |]) (EmitTypes.indent 1) |> equal "[\n 1\n]"

    testCase "partial builds a Partial<T> that TypeScript checks with satisfies" <| fun () ->
        Partials.bump ()
        Partials.bump ()
        Partials.patch (Partials.relabel "two")
        Partials.current.count |> equal 2
        Partials.current.label |> equal "two"
        let full = Fickl.Core.JsInterop.full (Partials.relabel "only")
        full.label |> equal "only"
        let frozen = Partials.freeze Partials.current
        JS.Constructors.Object.isFrozen frozen |> equal true

    testCase "Read-only collections accept readonly arrays and maps" <| fun () ->
        Readonlys.sum (Readonlys.frozenNumbers ()) |> equal 6
        Readonlys.last (Readonlys.frozenNumbers ()) |> equal 3
        Readonlys.sum (Fickl.Core.JS.ReadonlyArray.ofArray [| 4; 5 |]) |> equal 9
        Readonlys.last (Fickl.Core.JS.ReadonlyArray.ofResizeArray (ResizeArray [ 7; 8 ])) |> equal 8
        Fickl.Core.JS.ReadonlyArray.toArray (Readonlys.frozenNumbers ()) |> equal [| 1; 2; 3 |]
        (Readonlys.frozenNumbers ()).includes 2 |> equal true
        Readonlys.countAll [| 1; 2 |] |> equal 2
        Readonlys.countAll (System.Collections.Generic.HashSet [ 1 ]) |> equal 1
        Readonlys.firstTwo [ 1; 2; 3 ] |> equal [ 1; 2 ]
        Readonlys.find (Readonlys.nativeMap ()) "a" |> equal 1
        Readonlys.find (Map [ ("b", 2) ]) "b" |> equal 2
        Readonlys.find (dict [ ("c", 3) ] |> System.Collections.Generic.Dictionary) "z" |> equal -1

    testCase "unknown is obj in F# and unknown in TypeScript signatures" <| fun () ->
        let parsed = Unknowns.parse "{\"name\":\"fickl\"}"
        Unknowns.field parsed "name" |> Unknowns.lengthOf |> equal 5
        Unknowns.field parsed "name" |> Unknowns.describe |> equal "string fickl"
        Unknowns.describe (box 1) |> equal "other"
        (Unknowns.box' "abc").Get() |> Unknowns.lengthOf |> equal 3
        (Unknowns.parse "[1,2]")?length |> equal 2
    #endif
  ]
