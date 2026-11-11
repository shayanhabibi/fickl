module Fable.Tests.RecordTypes

open Util.Testing
#if FABLE_COMPILER
open Fable.Core
#endif

type RecursiveRecord =
    { things : RecursiveRecord list }

type IObjectShape =
    abstract name: string option
    abstract size: float option
    abstract label: string
    abstract empty: unit

type IOmittedOptions =
    abstract missing: int option with get, set

type Person =
    { name: string; mutable luckyNumber: int }
    member x.LuckyDay = x.luckyNumber % 30
    member x.SignDoc str = str + " by " + x.name

type JSKiller =
   { ``for`` : float; ``class`` : float }

type JSKiller2 =
   { ``s p a c e`` : float; ``s*y*m*b*o*l`` : float }

type PrototypeFields =
   { __proto__: int; constructor: int; other: string }

type Child =
    { a: string; b: int }
    member x.Sum() = (int x.a) + x.b

type Parent =
    { children: Child[] }
    member x.Sum() = x.children |> Seq.sumBy (fun c -> c.Sum())

type MutatingRecord =
    { uniqueA: int; uniqueB: int }

type Id = Id of string

let inline replaceById< ^t when ^t : (member Id : Id)> (newItem : ^t) (ar: ^t[]) =
    Array.map (fun (x: ^t) -> if (^t : (member Id : Id) newItem) = (^t : (member Id : Id) x) then newItem else x) ar

let makeAnonRec() =
    {| X = 5; Y = "Foo"; F = fun x y -> x + y |}

type Time =
    static member inline duration(value: {| from: int; until: int |}) = value.until - value.from
    static member inline duration(value: {| from: int |}) = Time.duration {| value with until = 10 |}

type CarInterior = { Seats: int }
type Car = { Interior: CarInterior }

// Issue #3923 - static let on a record shifts constructor arg indices
type RecordWithStaticLet =
    { id: int }
    static let _a = 2
    static member a = _a

// Issue #4091 - static member val on a record causes shared mutable state
type RecordWithStaticMemberVal =
    { xs: ResizeArray<int> }
    static member val empty = { xs = ResizeArray() }

[<Struct>]
type StructRecord = { Size: int; Seed: int; Path: int list }

type StructRecordHolder = { Tag: int; Data: StructRecord }

let updateStructRecord (data: StructRecord) (path: int list) = { data with Path = List.rev path }

let holdStructRecord (data: StructRecord) (path: int list) =
    Some
        {
            Tag = 1
            Data = { data with Path = List.rev path }
        }

let mutable mutableStructRecord: StructRecord = { Size = 1; Seed = 2; Path = [ 3 ] }

// fickl#304: a file that copies a record often enough declares a copier for it, so these are each copied several times

type IDescribed =
    abstract Describe: unit -> string

type WideRecord =
    {
        Alpha: int
        Beta: string
        Gamma: float
        Delta: int option
        Epsilon: int list
        ``for``: int
        ``s p a c e``: string
        constructor: int
        Eta: int64
        Theta: string option
    }
    member x.Total = x.Alpha + x.``for`` + x.constructor
    member x.WithBeta(beta: string) = { x with Beta = beta }
    interface IDescribed with
        member x.Describe() = x.Beta + string x.Alpha

let wideRecord =
    {
        Alpha = 1
        Beta = "b"
        Gamma = 2.5
        Delta = Some 3
        Epsilon = [ 4; 5 ]
        ``for`` = 6
        ``s p a c e`` = "s"
        constructor = 7
        Eta = 8L
        Theta = None
    }

let makeWideRecord () = { wideRecord with Alpha = 10 }

type WideHandlers =
    {
        Name: string
        Add: int -> int -> int
        Twice: int -> int
        Label: unit -> string
        Width: int
        Height: int
        Depth: int
        Weight: int
        Length: int
        Volume: int
    }

let wideHandlers =
    {
        Name = "handlers"
        Add = fun x y -> x + y
        Twice = fun x -> x * 2
        Label = fun () -> "label"
        Width = 1
        Height = 2
        Depth = 3
        Weight = 4
        Length = 5
        Volume = 6
    }

type WideMutable =
    {
        mutable Counter: int
        Title: string
        First: int
        Second: int
        Third: int
        Fourth: int
        Fifth: int
        mutable Last: int
    }

let makeWideMutable () =
    {
        Counter = 0
        Title = "t"
        First = 1
        Second = 2
        Third = 3
        Fourth = 4
        Fifth = 5
        Last = 6
    }

type WideGeneric<'T> =
    {
        Value: 'T
        Maybe: 'T option
        Items: 'T list
        Caption: string
        Size: int
        Enabled: bool
        Ratio: float
        Note: string
    }

let makeWideGeneric (value: 'T) =
    {
        Value = value
        Maybe = None
        Items = [ value ]
        Caption = "c"
        Size = 1
        Enabled = false
        Ratio = 0.5
        Note = "n"
    }

[<Struct>]
type WideStruct =
    {
        North: int
        South: int
        East: int
        West: int
        Up: string
        Down: string
        Front: float
        Back: float
    }

let wideStruct =
    {
        North = 1
        South = 2
        East = 3
        West = 4
        Up = "u"
        Down = "d"
        Front = 0.5
        Back = 1.5
    }

let tests =
  testList "RecordTypes" [

#if FABLE_COMPILER
    testCase "Declared object shapes retain undefined properties" <| fun () -> // fickl#110
        let shape =
            { new IObjectShape with
                member _.name = None
                member _.size = None
                member _.label = "present"
                member _.empty = () }
        Fable.Core.JsInterop.emitJsExpr shape "Object.keys($0).sort()"
        |> equal [| "empty"; "label"; "name"; "size" |]
        Fable.Core.JsInterop.emitJsExpr shape "Object.prototype.hasOwnProperty.call($0, 'name')" |> equal true
        shape.name |> equal None
        let anonymous = {| a = (None: int option); b = 1; c = () |}
        Fable.Core.JsInterop.emitJsExpr anonymous "Object.keys($0).sort()"
        |> equal [| "a"; "b"; "c" |]
        Fable.Core.JsInterop.emitJsExpr anonymous "Object.keys({...$0}).sort()"
        |> equal [| "a"; "b"; "c" |]

    testCase "Option bags still omit constant undefined properties" <| fun () ->
        let options = Fable.Core.JsInterop.jsOptions<IOmittedOptions>(fun o ->
            o.missing <- None)
        Fable.Core.JsInterop.emitJsExpr options "Object.keys($0)" |> equal [||]
        let created = Fable.Core.JsInterop.createObj [
            "missing", box (None: int option)
            "empty", box ()
            "kept", box 1 ]
        Fable.Core.JsInterop.emitJsExpr created "Object.keys($0)" |> equal [| "kept" |]
#endif

    testCase "Anonymous records work" <| fun () ->
        let r = makeAnonRec()
        sprintf "Tell me %s %i times" r.Y (r.F r.X 3)
        |> equal "Tell me Foo 8 times"
        let x = {| Foo = "baz"; Bar = 23 |}
        let y = {| Foo = "baz" |}
        x = {| y with Bar = 23 |} |> equal true
        // x = {| y with Baz = 23 |} |> equal true // Doesn't compile
        x = {| y with Bar = 14 |} |> equal false

    testCase "Anonymous records can have optional fields" <| fun () ->
        let add (o: {| bar: int option; zas: string option; foo: int option option |}) =
            let bar = o.bar |> Option.map string |> Option.defaultValue "-"
            let zas = defaultArg o.zas ""
            let foo = match o.foo with Some(Some i) -> string i | Some None -> "xx" | None -> "x"
            bar + zas + foo

        {| bar = Some 3; zas = Some "ooooo"; foo = Some None |} |> add |> equal "3oooooxx"
        {| bar = Some 22; zas = Some ""; foo = Some(Some 999) |} |> add |> equal "22999"
        {| bar = None; zas = None; foo = None |} |> add |> equal "-x"
        {| foo = Some None; bar = None; zas = None |} |> add |> equal "-xx"

    testCase "Anonymous records can have optional function fields" <| fun () ->
        let add (o: {| bar: (int -> int -> int) option; foo: int -> int -> int |}) =
            let fn = o.bar
            let f1 = fn |> Option.map (fun f -> f 6 9) |> Option.defaultValue -3
            let f2 = match fn with Some f -> f 1 8 | None -> -5
            o.foo 3 4 + f1 + f2

        {| bar = Some (+); foo = (*) |} |> add |> equal 36
        {| bar = None; foo = (+) |} |> add |> equal -1

    testCase "SRTP works with anonymous records" <| fun () ->
        let ar = [| {|Id=Id"foo"; Name="Sarah"|}; {|Id=Id"bar"; Name="James"|} |]
        replaceById {|Id=Id"ja"; Name="Voll"|} ar |> Seq.head |> fun x -> equal "Sarah" x.Name
        replaceById {|Id=Id"foo"; Name="Anna"|} ar |> Seq.head |> fun x -> equal "Anna" x.Name

    testCase "Overloads with anonymous record arguments don't have same mangled name" <| fun () ->
        Time.duration {| from = 1 |} |> equal 9
        Time.duration {| from = 1; until = 5 |} |> equal 4

    testCase "Anonymous record execution order" <| fun () ->
        let mutable x = 2
        let record =
            {|
                C = (x <- x * 3; x)
                B = (x <- x + 5; x)
                A = (x <- x / 2; x)
            |}
        record.A |> equal 5
        record.B |> equal 11
        record.C |> equal 6

    testCase "Copy-and-update of a struct record argument works" <| fun () ->
        let data: StructRecord = { Size = 1; Seed = 2; Path = [ 3; 4 ] }
        let updated = updateStructRecord data [ 5; 6 ]
        equal 1 updated.Size
        equal 2 updated.Seed
        equal [ 6; 5 ] updated.Path
        let held = holdStructRecord data [ 7; 8 ] |> Option.get
        equal 1 held.Data.Size
        equal 2 held.Data.Seed
        equal [ 8; 7 ] held.Data.Path

    testCase "Copy-and-update of a mutable struct record works" <| fun () -> // See #254
        let mutable local: StructRecord = { Size = 1; Seed = 2; Path = [ 3 ] }
        local <- { local with Seed = 5 }
        let updated = { local with Path = [ 4 ] }
        equal 1 updated.Size
        equal 5 updated.Seed
        equal [ 4 ] updated.Path
        mutableStructRecord <- { mutableStructRecord with Seed = 6 }
        let updated = { mutableStructRecord with Size = 7 }
        equal 7 updated.Size
        equal 6 updated.Seed
        equal [ 3 ] updated.Path

    testCase "Fields of a struct record can be read through a byref local" <| fun () ->
        let mutable local: StructRecord = { Size = 1; Seed = 2; Path = [ 3 ] }
        let address = &local
        address <- { Size = 8; Seed = 9; Path = [] }
        equal 8 address.Size
        equal 9 local.Seed

    testCase "Recursive record does not cause issues" <| fun () ->
        let r = { things = [ { things = [] } ] }
        equal r.things.Length 1

    testCase "Record property access can be generated" <| fun () ->
        let x = { name = "Alfonso"; luckyNumber = 7 }
        equal "Alfonso" x.name
        equal 7 x.luckyNumber
        x.luckyNumber <- 14
        equal 14 x.luckyNumber

    testCase "Record methods can be generated" <| fun () ->
        let x = { name = "Alfonso"; luckyNumber = 54 }
        equal 24 x.LuckyDay
        x.SignDoc "Hello World!"
        |> equal "Hello World! by Alfonso"

    testCase "Record expression constructors can be generated" <| fun () ->
        let x = { name = "Alfonso"; luckyNumber = 7 }
        let y = { x with luckyNumber = 14 }
        equal "Alfonso" y.name
        equal 14 y.luckyNumber

    testCase "Records with key/reserved words are mapped correctly" <| fun () ->
        let x = { ``for`` = 1.0; ``class`` = 2.0 }
        equal 2. x.``class``

    testCase "Records with fields named __proto__ and constructor keep them" <| fun () -> // #51
        let x = { __proto__ = 1; constructor = 2; other = "o" }
        let y = { x with __proto__ = 9 }
        equal 1 x.__proto__
        equal 2 x.constructor
        equal 9 y.__proto__
        equal 2 y.constructor
        equal true (x = { __proto__ = 1; constructor = 2; other = "o" })
        equal (-1) (compare x y)
        let a = {| __proto__ = 3; x = 4 |}
        equal 3 a.__proto__
        equal true (a = {| __proto__ = 3; x = 4 |})
        let printed = sprintf "%A" x
        equal true (printed.Contains "__proto__ = 1" && printed.Contains "constructor = 2" && not (printed.Contains "$"))

    testCase "Reflection uses the F# names of fields named __proto__ and constructor" <| fun () -> // #51
        let x = { __proto__ = 1; constructor = 2; other = "o" }
        let fields = Reflection.FSharpType.GetRecordFields(typeof<PrototypeFields>)
        fields |> Array.map (fun f -> f.Name) |> equal [| "__proto__"; "constructor"; "other" |]
        fields |> Array.map (fun f -> Reflection.FSharpValue.GetRecordField(x, f) |> string) |> equal [| "1"; "2"; "o" |]
        let made = Reflection.FSharpValue.MakeRecord(typeof<PrototypeFields>, [| box 4; box 5; box "m" |]) :?> PrototypeFields
        equal 4 made.__proto__
        equal 5 made.constructor
        Reflection.FSharpValue.GetRecordFields(box {| __proto__ = 3; x = 4 |}) |> Array.map string |> equal [| "3"; "4" |]

    testCase "Records with special characters are mapped correctly" <| fun () ->
        let x = { ``s p a c e`` = 1.0; ``s*y*m*b*o*l`` = 2.0 }
        equal 1. x.``s p a c e``
        equal 2. x.``s*y*m*b*o*l``

    testCase "Mutating records work" <| fun () ->
        let x = { uniqueA = 10; uniqueB = 20 }
        equal 10 x.uniqueA
        equal 20 x.uniqueB
        let uniqueB' = -x.uniqueB
        let x' = { x with uniqueB = uniqueB' }
        equal 10 x.uniqueA
        equal 10 x'.uniqueA
        equal -20 x'.uniqueB
        let x'' = { x' with uniqueA = -10 }
        equal -10 x''.uniqueA
        equal -20 x''.uniqueB

    testCase "Nested record field copy and update works for records" <| fun () ->
        let car =
            { Interior = { Seats = 4 } }
        let car2 =
            { car with Interior.Seats = 5 }
        equal 5 car2.Interior.Seats

    testCase "Nested record field copy and update works for anonymous records" <| fun () ->
        let car =
            {| Interior = {| Seats = 4 |} |}
        let car2 =
            {| car with Interior.Seats = 5 |}
        equal 5 car2.Interior.Seats

    // Issue #3923
    testCase "Record with static let has correct constructor" <| fun () ->
        let x = { id = 1 }
        equal 1 x.id
        equal 2 RecordWithStaticLet.a

    // Issue #4091
    testCase "Record with static member val does not share state across instances" <| fun () ->
        let a = RecordWithStaticMemberVal.empty
        let b = RecordWithStaticMemberVal.empty
        a.xs.Add 1
        equal 1 a.xs.Count
        equal 1 b.xs.Count

    // fickl#304
    testCase "Copying a wide record changes the given field alone" <| fun () ->
        let r = wideRecord
        { r with Alpha = 11 } |> equal { Alpha = 11; Beta = "b"; Gamma = 2.5; Delta = Some 3; Epsilon = [ 4; 5 ]; ``for`` = 6; ``s p a c e`` = "s"; constructor = 7; Eta = 8L; Theta = None }
        { r with Beta = "x" } |> equal { Alpha = 1; Beta = "x"; Gamma = 2.5; Delta = Some 3; Epsilon = [ 4; 5 ]; ``for`` = 6; ``s p a c e`` = "s"; constructor = 7; Eta = 8L; Theta = None }
        { r with Gamma = 0.5 } |> equal { Alpha = 1; Beta = "b"; Gamma = 0.5; Delta = Some 3; Epsilon = [ 4; 5 ]; ``for`` = 6; ``s p a c e`` = "s"; constructor = 7; Eta = 8L; Theta = None }
        { r with Epsilon = [] } |> equal { Alpha = 1; Beta = "b"; Gamma = 2.5; Delta = Some 3; Epsilon = []; ``for`` = 6; ``s p a c e`` = "s"; constructor = 7; Eta = 8L; Theta = None }
        { r with ``for`` = 60 } |> equal { Alpha = 1; Beta = "b"; Gamma = 2.5; Delta = Some 3; Epsilon = [ 4; 5 ]; ``for`` = 60; ``s p a c e`` = "s"; constructor = 7; Eta = 8L; Theta = None }
        { r with ``s p a c e`` = "t" } |> equal { Alpha = 1; Beta = "b"; Gamma = 2.5; Delta = Some 3; Epsilon = [ 4; 5 ]; ``for`` = 6; ``s p a c e`` = "t"; constructor = 7; Eta = 8L; Theta = None }
        { r with constructor = 70 } |> equal { Alpha = 1; Beta = "b"; Gamma = 2.5; Delta = Some 3; Epsilon = [ 4; 5 ]; ``for`` = 6; ``s p a c e`` = "s"; constructor = 70; Eta = 8L; Theta = None }
        { r with Eta = 80L } |> equal { Alpha = 1; Beta = "b"; Gamma = 2.5; Delta = Some 3; Epsilon = [ 4; 5 ]; ``for`` = 6; ``s p a c e`` = "s"; constructor = 7; Eta = 80L; Theta = None }
        // the source is left as it was
        r |> equal { Alpha = 1; Beta = "b"; Gamma = 2.5; Delta = Some 3; Epsilon = [ 4; 5 ]; ``for`` = 6; ``s p a c e`` = "s"; constructor = 7; Eta = 8L; Theta = None }

    testCase "Copying a wide record sets and clears optional fields" <| fun () ->
        let r = wideRecord
        let cleared = { r with Delta = None }
        cleared.Delta |> equal None
        cleared.Theta |> equal None
        cleared.Alpha |> equal 1
        let set = { cleared with Theta = Some "t" }
        set.Theta |> equal (Some "t")
        set.Delta |> equal None
        { set with Delta = Some 3; Theta = None } |> equal r

    testCase "Copying a wide record changes two fields at once" <| fun () ->
        let r = { wideRecord with Alpha = 2; Beta = "c" }
        r.Alpha |> equal 2
        r.Beta |> equal "c"
        r.constructor |> equal 7
        { r with Alpha = 1; Beta = "b" } |> equal wideRecord
        // three fields are copied through the constructor
        { r with Alpha = 1; Beta = "b"; Gamma = 1. } |> equal { wideRecord with Gamma = 1. }

    testCase "Copies of a wide record can be chained" <| fun () ->
        let r = { { { makeWideRecord () with Beta = "c" } with Gamma = 3.5 } with Eta = 9L }
        r |> equal { Alpha = 10; Beta = "c"; Gamma = 3.5; Delta = Some 3; Epsilon = [ 4; 5 ]; ``for`` = 6; ``s p a c e`` = "s"; constructor = 7; Eta = 9L; Theta = None }
        let copies = [ 1; 2; 3 ] |> List.map (fun i -> { wideRecord with Alpha = i })
        copies |> List.map (fun r -> r.Alpha + r.``for``) |> equal [ 7; 8; 9 ]
        let folded = (wideRecord, [ 1; 2; 3 ]) ||> List.fold (fun r i -> { r with Epsilon = i :: r.Epsilon })
        folded.Epsilon |> equal [ 3; 2; 1; 4; 5 ]

    testCase "A copy of a wide record keeps its members, interfaces and type" <| fun () ->
        let r = { wideRecord with Alpha = 5 }
        r.Total |> equal 18
        (r.WithBeta "z").Beta |> equal "z"
        (r.WithBeta "z").Total |> equal 18
        (r :> IDescribed).Describe() |> equal "b5"
        match box r with
        | :? WideRecord as r -> r.Alpha |> equal 5
        | _ -> failwith "not a WideRecord"
        hash r = hash { wideRecord with Alpha = 5 } |> equal true
        compare r wideRecord |> equal 1
        compare wideRecord r |> equal -1
        r = wideRecord |> equal false
        let fields = Reflection.FSharpType.GetRecordFields(typeof<WideRecord>) |> Array.map (fun f -> f.Name)
        fields |> equal [| "Alpha"; "Beta"; "Gamma"; "Delta"; "Epsilon"; "for"; "s p a c e"; "constructor"; "Eta"; "Theta" |]
        Reflection.FSharpValue.GetRecordFields(box r) |> Array.map string |> Array.take 3 |> equal [| "5"; "b"; "2.5" |]

#if FABLE_COMPILER
    testCase "A copy of a wide record has the shape of the record" <| fun () ->
        let r = { wideRecord with Theta = Some "t" }
        Fable.Core.JsInterop.emitJsExpr (r, wideRecord) "Object.getPrototypeOf($0) === Object.getPrototypeOf($1)" |> equal true
        Fable.Core.JsInterop.emitJsExpr (r, wideRecord) "Object.keys($0).join() === Object.keys($1).join()" |> equal true
        Fable.Core.JsInterop.emitJsExpr r "Object.keys($0).length" |> equal 10
#endif

    testCase "Copying a wide record changes function fields" <| fun () ->
        let r = { wideHandlers with Add = fun x y -> x * y }
        r.Add 3 4 |> equal 12
        r.Twice 3 |> equal 6
        r.Label() |> equal "label"
        wideHandlers.Add 3 4 |> equal 7
        let add = fun x y -> x - y
        let r = { r with Add = add }
        r.Add 3 4 |> equal -1
        let r = { r with Twice = (+) 1 }
        r.Twice 3 |> equal 4
        let r = { r with Label = fun () -> r.Name }
        r.Label() |> equal "handlers"
        let r = { r with Name = "n"; Width = 10 }
        r.Label() |> equal "handlers"
        r.Name |> equal "n"
        r.Width + r.Height + r.Depth + r.Weight |> equal 19
        let partial = r.Add 10
        partial 4 |> equal 6
        let r = { r with Volume = r.Twice r.Volume }
        r.Volume + r.Length |> equal 12
        let r = { r with Add = r.Add }
        r.Add 5 1 |> equal 4

    testCase "Copying a wide record with mutable fields reads the source in order" <| fun () ->
        let r = makeWideMutable ()
        let copy = { r with Title = "u" }
        copy.Counter <- 5
        r.Counter |> equal 0
        copy.Counter |> equal 5
        r.Last <- 60
        copy.Last |> equal 6
        { r with First = 10 }.Last |> equal 60
        { r with Second = 20 }.Second |> equal 20
        { r with Third = 30 }.Third |> equal 30
        { r with Fourth = 40; Fifth = 50 }.Fifth |> equal 50
        // the new values are evaluated before the source is read
        let r = makeWideMutable ()
        let copy = { r with Title = (r.Counter <- 7; r.Last <- 8; "v") }
        copy.Counter |> equal 7
        copy.Last |> equal 8
        copy.Title |> equal "v"
        r.Counter |> equal 7
        let bump () = r.Counter <- r.Counter + 1; r.Counter
        let copy = { r with First = bump (); Second = bump () }
        copy.Counter |> equal 9
        copy.First |> equal 8
        copy.Second |> equal 9

    testCase "Copying a wide generic record works" <| fun () ->
        let r = makeWideGeneric 5
        { r with Value = 6 }.Value |> equal 6
        { r with Maybe = Some 7 }.Maybe |> equal (Some 7)
        { r with Items = [] }.Items |> equal []
        { r with Caption = "d" }.Caption |> equal "d"
        { r with Size = 2; Enabled = true } |> equal { Value = 5; Maybe = None; Items = [ 5 ]; Caption = "c"; Size = 2; Enabled = true; Ratio = 0.5; Note = "n" }
        let s = makeWideGeneric "a"
        { s with Value = "b" } |> equal { Value = "b"; Maybe = None; Items = [ "a" ]; Caption = "c"; Size = 1; Enabled = false; Ratio = 0.5; Note = "n" }
        let nested = makeWideGeneric (makeWideGeneric 1)
        { nested with Value = { nested.Value with Note = "m" } }.Value.Note |> equal "m"
        let copyValue (value: 'T) (r: WideGeneric<'T>) = { r with Value = value; Maybe = Some value }
        (copyValue 9 r).Maybe |> equal (Some 9)
        (copyValue None (makeWideGeneric (Some 1))).Maybe |> equal (Some None)

    testCase "Copying a wide struct record works" <| fun () ->
        let r = wideStruct
        { r with North = 10 } |> equal { North = 10; South = 2; East = 3; West = 4; Up = "u"; Down = "d"; Front = 0.5; Back = 1.5 }
        { r with South = 20 }.South |> equal 20
        { r with Up = "v" }.Up |> equal "v"
        { r with Down = "e" }.Down |> equal "e"
        { r with East = 30 }.West |> equal 4
        { r with Back = 0. }.Front |> equal 0.5
        { r with Front = 2.5; Back = 3.5 } |> equal { North = 1; South = 2; East = 3; West = 4; Up = "u"; Down = "d"; Front = 2.5; Back = 3.5 }
        let mutable m = wideStruct
        let copy = { m with East = 30 }
        m <- { m with West = 40 }
        copy.West |> equal 4
        m.West |> equal 40
        m.East |> equal 3
        copy.East |> equal 30
        r |> equal wideStruct

  ]
