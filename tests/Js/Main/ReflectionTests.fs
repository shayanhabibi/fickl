module Fable.Tests.Reflection

open Util.Testing
open FSharp.Data.UnitSystems.SI.UnitSymbols

#if !OPTIMIZE_FCS

type MyDelegate = delegate of int * float -> string
type MyGenDelegate<'a,'b> = delegate of 'b * string -> 'a

// I can declare a type with delegate fields (both C# and F# style). See #1698
type WithDelegates =
    { Del1: MyDelegate
      Del2: MyGenDelegate<int,string>
      Del3: System.Func<int, float, string>
      Del4: System.Func<float>
      Del5: System.Action
      Del6: System.Action<int>
      Del7: System.Action<int, string> }

type TestType =
    | Union1 of string

type TestType2 =
    | Union2 of string

type TestType3 = class end
type TestType4 = class end
type TestType5 = class end

type GenericRecord<'A,'B> = { a: 'A; b: 'B }

type MyEnum =
    | Foo = 1y
    | Bar = 5y
    | Baz = 8y

type MyInterface<'T> =
     abstract Value: 'T

type MyClass() =
    class end

type MyClass2() =
    class end

type MyClass3<'T>(v) =
    interface MyInterface<'T> with
        member _.Value = v

type MyClass<'T1, 'T2>(v1: 'T1, v2: 'T2) =
    inherit MyClass()
    member _.Value1 = v1
    member _.Value2 = v2

[<AbstractClass>]
type SelfBase<'Inheritor when 'Inheritor :> SelfBase<'Inheritor>>() =
    member _.Dummy() = 1

type SelfInheritor() =
    inherit SelfBase<SelfInheritor>()

[<AbstractClass>]
type PairBase<'Other>() =
    member _.Dummy() = 2

type PairPing() =
    inherit PairBase<PairPong>()

and PairPong() =
    inherit PairBase<PairPing>()

let genericTests = [
  testCase "typedefof works" <| fun () ->
    let tdef1 = typedefof<int list>
    let tdef2 = typedefof<string list>
    equal tdef1 tdef2

  testCase "IsGenericType works" <| fun () ->
    typeof<int list>.IsGenericType |> equal true
    typeof<TestType>.IsGenericType |> equal false
    let t1 = typeof<int list>
    let t2 = typeof<TestType>
    let t3 = typeof<string>
    t1.IsGenericType |> equal true
    t2.IsGenericType |> equal false
    t3.IsGenericType |> equal false

  testCase "GetGenericTypeDefinition works" <| fun () ->
    let tdef1 = typedefof<int list>
    let tdef2 = typeof<int list>.GetGenericTypeDefinition()
    let t = typeof<int list>
    let tdef3 = t.GetGenericTypeDefinition()
    equal tdef1 tdef2
    equal tdef1 tdef3
    tdef1 = typeof<int list> |> equal false

  testCase "Comparing generic types works" <| fun () ->
    let t1 = typeof<GenericRecord<string, TestType>>
    let t2 = typeof<GenericRecord<string, TestType>>
    let t3 = typeof<GenericRecord<string, int>>
    t1 = t2 |> equal true
    t1 = t3 |> equal false
]

let inline getName<'t> = function
    | "namespace" -> typedefof<'t>.Namespace
    | "name" -> typedefof<'t>.Name
    | _ -> typedefof<'t>.FullName

let inline getName2 (d:'t) = function
    | "namespace" -> typeof<'t>.Namespace
    | "name" -> typeof<'t>.Name
    | _ -> typeof<'t>.FullName

let getName3 (t:System.Type) = function
    | "namespace" -> t.Namespace
    | "name" -> t.Name
    | _ -> t.FullName

// Fable 2 cannot check types unknown at compile time
// let getName4 (o:obj) = function
//     | "namespace" -> o.GetType().Namespace
//     | "name" -> o.GetType().Name
//     | _ -> o.GetType().FullName

type Firm = { name: string }

let normalize (x: string) =
    #if FABLE_COMPILER
    x
    #else
    x.Replace("+",".")
    #endif

let inline fullname<'T> () = typeof<'T>.FullName |> normalize
// let inline create<'T when 'T:(new : unit -> 'T)> () = new 'T()
let inline create2<'T> (args: obj[]) =
    System.Activator.CreateInstance(typeof<'T>, args) :?> 'T

type R =
  | A of int
  | B of string

type Service<'a> = {
    GetRecord: int -> int64 -> Async<'a>
}
with
    static member inline RouteBuilder _ m =
        let name = typeof<'a>.Name
        sprintf "/api/%s/%s" name m

let typeNameTests = [
  testCase "Type Namespace" <| fun () ->
    let x = typeof<TestType>.Namespace
    #if FABLE_COMPILER
    equal "Fable.Tests.Reflection" x
    #else
    equal "Fable.Tests" x
    #endif

  testCase "Type FullName" <| fun () ->
    let x = typeof<TestType>.FullName
    x |> normalize |> equal "Fable.Tests.Reflection.TestType"

  testCase "Type Name" <| fun () ->
    let x = typeof<TestType>.Name
    equal "TestType" x

  // See https://github.com/fable-compiler/repl/issues/152
  testCase "Type Name of generic argument in inlined function" <| fun () ->
    Service<R>.RouteBuilder "" "Add" |> equal "/api/R/Add"

  testCase "Get fullname of generic types with inline function" <| fun () ->
    fullname<TestType3>() |> equal "Fable.Tests.Reflection.TestType3"
    fullname<TestType4>() |> equal "Fable.Tests.Reflection.TestType4"

  // See https://github.com/thoth-org/Thoth.Json/issues/123
  testCase "FullName of complex array types is printed correctly " <| fun () ->
      let fullName1 = typeof<(string * int)[]>.FullName
      let fullName2 = typeof<(string * float)[]>.FullName
      let fullName3 = typeof<string[]>.FullName
      let fullName4 = typeof<string[][]>.FullName

      let t1 = typeof<(string * int)[]>
      let t2 = typeof<(string * float)[]>
      let t3 = typeof<string[]>
      let t4 = typeof<string[][]>

      equal fullName1 t1.FullName
      equal fullName2 t2.FullName
      equal fullName3 t3.FullName
      equal fullName4 t4.FullName

      t1.FullName = t2.FullName |> equal false
      t1.FullName = t3.FullName |> equal false
      t2.FullName = t3.FullName |> equal false
      t3.FullName = t4.FullName |> equal false

  testCase "Name of complex array types is printed correctly " <| fun () ->
      let name1 = typeof<(string * int)[]>.Name
      let name2 = typeof<(string * float)[]>.Name
      let name3 = typeof<string[]>.Name
      let name4 = typeof<string[][]>.Name

      let t1 = typeof<(string * int)[]>
      let t2 = typeof<(string * float)[]>
      let t3 = typeof<string[]>
      let t4 = typeof<string[][]>

      equal name1 t1.Name
      equal name2 t2.Name
      equal name3 t3.Name
      equal name4 t4.Name

      t1.Name = t2.Name |> equal true
      t1.Name = t3.Name |> equal false
      t2.Name = t3.Name |> equal false
      t3.Name = t4.Name |> equal false

  testCase "Namespace of complex array types is printed correctly " <| fun () ->
      let namespace1 = typeof<(string * int)[]>.Namespace
      let namespace2 = typeof<(string * float)[]>.Namespace
      let namespace3 = typeof<string[]>.Namespace

      let t1 = typeof<(string * int)[]>
      let t2 = typeof<(string * float)[]>
      let t3 = typeof<string[]>

      equal namespace1 t1.Namespace
      equal namespace2 t2.Namespace
      equal namespace3 t3.Namespace

      t1.Namespace = t2.Namespace |> equal true
      t1.Namespace = t3.Namespace |> equal true
      t2.Namespace = t3.Namespace |> equal true

//   testCase "Create new generic objects with inline function" <| fun () ->
//     create<TestType3>().Value |> equal "Hi"
//     create<TestType4>().Value2 |> equal "Bye"
//     // create<TestType5>() // Doesn't compile

//   testCase "Create new generic objects with System.Activator" <| fun () ->
//     (create2<TestType3> [||]).Value |> equal "Hi"
//     (create2<TestType4> [||]).Value2 |> equal "Bye"
//     (create2<TestType5> [|"Yo"|]).Value |> equal "Yo"

  testCase "Create primitive types with System.Activator" <| fun () ->
    create2<obj> [||] |> notEqual Unchecked.defaultof<obj> // It should not be null
    // Value types should zero-init
    create2<char> [||] |> equal Unchecked.defaultof<char>
    create2<bool> [||] |> equal Unchecked.defaultof<bool>
    create2<int8> [||] |> equal Unchecked.defaultof<int8>
    create2<uint8> [||] |> equal Unchecked.defaultof<uint8>
    create2<int16> [||] |> equal Unchecked.defaultof<int16>
    create2<uint16> [||] |> equal Unchecked.defaultof<uint16>
    create2<int32> [||] |> equal Unchecked.defaultof<int32>
    create2<uint32> [||] |> equal Unchecked.defaultof<uint32>
    create2<int64> [||] |> equal Unchecked.defaultof<int64>
    create2<uint64> [||] |> equal Unchecked.defaultof<uint64>
    create2<single> [||] |> equal Unchecked.defaultof<single>
    create2<double> [||] |> equal Unchecked.defaultof<double>
    create2<decimal> [||] |> equal Unchecked.defaultof<decimal>
    // TODO: Fix Unchecked.defaultof<MyEnum> to be 0
    create2<MyEnum> [||] |> equal (LanguagePrimitives.EnumOfValue 0y)

  testCase "Type name is accessible" <| fun () ->
    let x = { name = "" }
    getName<Firm> "name" |> equal "Firm"
    getName2 x "name" |> equal "Firm"
    getName3 typedefof<Firm> "name" |> equal "Firm"
    // getName4 x "name" |> equal "Firm"

  testCase "Type namespace is accessible" <| fun () ->
    let test (x: string) =
        x.StartsWith("Fable.Tests") |> equal true
    let x = { name = "" }
    getName<Firm> "namespace" |> test
    getName2 x "namespace" |> test
    getName3 typedefof<Firm> "namespace" |> test
    // getName4 x "namespace" |> test

  testCase "Type full name is accessible" <| fun () ->
    let test (x: string) =
        x.Replace("+", ".") |> equal "Fable.Tests.Reflection.Firm"
    let x = { name = "" }
    getName<Firm> "fullname" |> test
    getName2 x "fullname" |> test
    getName3 typedefof<Firm> "fullname" |> test
    // getName4 x "fullname" |> test
]

// FSharpType and FSharpValue reflection tests
open FSharp.Reflection

exception TestException of String: string * Int: int

let flip f b a = f a b

type TestRecord = {
    String: string
    Int: int
}

type TestUnion =
    | StringCase of SomeString: string * string
    | IntCase of SomeInt: int

type SingleCaseUnion =
  SingleCaseUnion of string * anotherString: string

type RecordF = { F : int -> string }

type AsyncRecord = {
  asyncProp : Async<string>
}

type MyList<'T> =
| Nil
| Cons of 'T * MyList<'T>

let inline create<'T when 'T: (new: unit->'T)> () = new 'T()

type A() = member _.Value = 5

type B() = member _.Value = 10

type AnonRec1 = {| name: string; child: {| name: string |} |}
type AnonRec2 = {| numbers: int list |}

type RecordGetValueType = {
    Firstname : string
    Age : int
}

type CaseLabelAttribute(label: string) =
    inherit System.Attribute()
    new (number: int) = CaseLabelAttribute(string number)
    new () = CaseLabelAttribute("default")
    member _.Label = label
    member val Priority = 0 with get, set

type CaseSubLabelAttribute(label: string, level: int) =
    inherit CaseLabelAttribute(label)
    member _.Level = level

type CaseMarkerAttribute() =
    inherit System.Attribute()

type CaseDataAttribute(kind: MyEnum, typ: System.Type, names: string[], letter: char, ratio: float, ok: bool) =
    inherit System.Attribute()
    member _.Kind = kind
    member _.Type = typ
    member _.Names = names
    member _.Letter = letter
    member _.Ratio = ratio
    member _.Ok = ok

type UnionWithCaseAttributes =
    | [<CaseLabel("DoThis")>] LabelCase
    | [<CaseLabel("DoThat"); CaseMarker; System.Obsolete("Not built on Fable")>] SeveralCase of int
    | PlainCase
    | [<CaseSubLabel("sub", 3)>] SubLabelCase
    | [<CaseLabel(5)>] NumberCase
    | [<CaseLabel>] DefaultCase
    | [<CaseLabel("named", Priority = 7)>] NamedCase
    | [<CaseData(MyEnum.Bar, typeof<string>, [| "a"; "b" |], 'c', 1.5, true)>] DataCase of string

// .NET also returns the attributes the F# compiler adds to a case
let describeCaseAttribute (attribute: obj) =
    match attribute with
    | :? CaseSubLabelAttribute as att -> Some $"SubLabel({att.Label},{att.Level})"
    | :? CaseLabelAttribute as att -> Some $"Label({att.Label},{att.Priority})"
    | :? CaseMarkerAttribute -> Some "Marker"
    | :? CaseDataAttribute -> Some "Data"
    | _ -> None

let caseAttributesOf name =
    FSharpType.GetUnionCases typeof<UnionWithCaseAttributes>
    |> Array.find (fun case -> case.Name = name)

type CtorClass(x: int, name: string) =
    new() = CtorClass(1, "a")
    new(s: string) = CtorClass(2, s)
    member _.X = x
    member _.Name = name

type CtorParameterless() =
    member _.Value = 1

type CtorPrivate private (x: int) =
    new(a: string, b: float) = CtorPrivate(a.Length + int b)
    member _.X = x

type CtorDerived() =
    inherit CtorClass(5, "d")

[<AbstractClass>]
type CtorAbstract(x: int) =
    abstract Value: int

type CtorGeneric<'T>(value: 'T, count: int) =
    member _.Value = value
    member _.Count = count

type CtorExplicit =
    val A: int
    val B: string
    new(a: int) = { A = a; B = "b" }
    new(a: int, b: string) = { A = a; B = b }

type CtorDependency(service: CtorParameterless, generic: CtorGeneric<string>) =
    member _.Service = service
    member _.Generic = generic

type CtorRecord = { First: int; SecondField: string }
type CtorUnion = CtorCaseA of int | CtorCaseB

[<Struct>]
type CtorStructRecord = { StructField: int }

[<Struct>]
type CtorStructUnion = CtorStructCase of int

[<Struct>]
type CtorStruct(x: int) =
    member _.X = x

type CtorInterface =
    abstract Value: int

let describeConstructors (t: System.Type) =
    t.GetConstructors()
    |> Array.map (fun c ->
        c.GetParameters()
        |> Array.map (fun p -> $"{p.Position}:{p.Name}:{p.ParameterType.FullName}")
        |> String.concat ",")
    |> String.concat " | "

let isClass (t: System.Type) = t.IsClass

let reflectionTests = [
  testCase "Reflection: Array" <| fun () ->
    let arType = typeof<int[]>
    let liType = typeof<int list>
    equal true arType.IsArray
    equal false liType.IsArray
    let elType = arType.GetElementType()
    typeof<int> = elType |> equal true
    typeof<bool> = elType |> equal false
    liType.GetElementType() |> equal null

  // TODO!!!
//   testCase "FSharp.Reflection: Exception" <| fun () ->
//     let typ = typeof<TestException>
//     let ex = TestException("a", 1)
//     let exTypeFields = FSharpType.GetExceptionFields typ
//     let exValueFields = FSharpValue.GetExceptionFields(ex)

//     let expectedExFields =
//         [|
//             "String", box "a"
//             "Int", box 1
//         |]

//     let exFields =
//         exTypeFields
//         |> Array.map (fun field -> field.Name)
//         |> flip Array.zip exValueFields

//     let isExceptionRepresentation = FSharpType.IsExceptionRepresentation typ
//     let matchExFields = exFields = expectedExFields

//     let all = isExceptionRepresentation && matchExFields
//     all |> equal true

  testCase "FSharp.Reflection: Record" <| fun () ->
    let typ = typeof<TestRecord>
    let record = { String = "a"; Int = 1 }
    let recordTypeFields = FSharpType.GetRecordFields typ
    let recordValueFields = FSharpValue.GetRecordFields record

    let expectedRecordFields =
        [|
            "String", box "a"
            "Int", box 1
        |]

    let recordFields =
        recordTypeFields
        |> Array.map (fun field -> field.Name)
        |> flip Array.zip recordValueFields

    let isRecord = FSharpType.IsRecord typ
    let matchRecordFields = recordFields = expectedRecordFields
    let matchIndividualRecordFields =
        Array.zip recordTypeFields recordValueFields
        |> Array.forall (fun (info, value) ->
            FSharpValue.GetRecordField(record, info) = value
        )
    let canMakeSameRecord =
        unbox<TestRecord> (FSharpValue.MakeRecord(typ, recordValueFields)) = record

    let all = isRecord && matchRecordFields && matchIndividualRecordFields && canMakeSameRecord
    all |> equal true

  testCase "FSharpValue.GetRecordFields with anonymous record returns all fields including None" <| fun () ->
    let fields = FSharpValue.GetRecordFields {| a = 3; b = (None: int option); c = Some 89 |}
    fields.Length |> equal 3
    fields.[0] |> equal (box 3)
    fields.[1] |> equal (box None)
    fields.[2] |> equal (box (Some 89))

  testCase "Reflection functions accept allowAccessToPrivateRepresentation" <| fun () ->
    let recordType = typeof<TestRecord>
    let record = { String = "a"; Int = 1 }

    FSharpType.GetRecordFields(recordType, allowAccessToPrivateRepresentation = true).Length |> equal 2

    let values = FSharpValue.GetRecordFields(record, allowAccessToPrivateRepresentation = true)
    let rebuilt =
        FSharpValue.MakeRecord(recordType, values, allowAccessToPrivateRepresentation = true) :?> TestRecord
    rebuilt |> equal record

    let unionType = typeof<TestUnion>
    let intCase = FSharpType.GetUnionCases(unionType).[1]
    let u = FSharpValue.MakeUnion(intCase, [| box 5 |], allowAccessToPrivateRepresentation = true) :?> TestUnion
    let info, fields = FSharpValue.GetUnionFields(u, unionType, allowAccessToPrivateRepresentation = true)
    info.Name |> equal "IntCase"
    fields.[0] |> equal (box 5)

  testCase "PropertyInfo.GetValue works" <| fun () ->
    let value: obj = { Firstname = "Maxime"; Age = 12 } :> obj

    let theType: System.Type = typeof<RecordGetValueType>

    // now we want to print out the fields
    let fieldNameToValue: Map<string, obj> =
        match theType with
        | t when FSharpType.IsRecord t ->
            FSharpType.GetRecordFields(t)
            |> Seq.fold
                (fun acc field ->
                    let fieldValue = field.GetValue value
                    acc.Add (field.Name, fieldValue)
                )
                Map.empty
        | _ -> Map.empty

    let expected = "map [(Age, 12); (Firstname, Maxime)]"

    equal expected (sprintf "%O" fieldNameToValue)

  testCase "Comparing anonymous record types works" <| fun () ->
      let x = {| numbers = [3; 4] |}
      typeof<AnonRec1> = typeof<AnonRec2> |> equal false
      typeof<AnonRec1> = typeof<AnonRec1> |> equal true
      typeof<AnonRec2> = x.GetType() |> equal true
      let generic = typeof<Result<AnonRec2, string>>
      generic = typeof<Result<AnonRec1, string>> |> equal false
      generic = typeof<Result<{| numbers: int list |}, string>> |> equal true

  testCase "Anonymous record types have a name" <| fun () -> // See #275
      let t = {| A = {| B = "content" |} |}.GetType()
      let inner = (FSharpType.GetRecordFields t).[0].PropertyType
      System.String.IsNullOrEmpty t.FullName |> equal false
      System.String.IsNullOrEmpty inner.FullName |> equal false
      t.FullName = inner.FullName |> equal false
      let intA = typeof<{| a: int |}>
      let stringA = typeof<{| a: string |}>
      let intB = typeof<{| b: int |}>
      intA.FullName = typeof<{| a: int |}>.FullName |> equal true
      intA.FullName = stringA.FullName |> equal false
      intA.FullName = intB.FullName |> equal false
      intA.Name.StartsWith("<>f__AnonymousType", System.StringComparison.Ordinal) |> equal true
      intA.FullName.StartsWith(intA.Name, System.StringComparison.Ordinal) |> equal true
      intA.Name = stringA.Name |> equal true
      intA.Name = intB.Name |> equal false
      System.String.IsNullOrEmpty intA.Namespace |> equal true
      typeof<{| a: int |}[]>.FullName = intA.FullName + "[]" |> equal true
      intA.GetHashCode() = typeof<{| a: int |}>.GetHashCode() |> equal true
      intA = stringA |> equal false
      intA = intB |> equal false

  testCase "FSharp.Reflection: Anonymous Record" <| fun () ->
    let typ = typeof<{| String: string; Int: int |}>
    let record = {| String = "a"; Int = 1 |}
    let recordTypeFields = FSharpType.GetRecordFields typ
    let recordValueFields = FSharpValue.GetRecordFields record

    let expectedRecordFields = // Alphabetical order
        [| "Int", box 1
           "String", box "a" |]

    let recordFields =
        recordTypeFields
        |> Array.map (fun field -> field.Name)
        |> flip Array.zip recordValueFields

    FSharpType.IsRecord typ |> equal true
    recordFields |> equal expectedRecordFields

    Array.zip recordTypeFields recordValueFields
    |> Array.forall (fun (info, value) ->
        FSharpValue.GetRecordField(record, info) = value)
    |> equal true

    FSharpValue.MakeRecord(typ, recordValueFields)
    |> unbox<{| String: string; Int: int |}>
    |> equal record

  testCase "FSharp.Reflection Functions" <| fun () ->
    let recordType = typeof<RecordF>
    let fields = FSharpType.GetRecordFields recordType
    let funcProperty = Array.head fields
    let funcType = funcProperty.PropertyType
    let domain, range = FSharpType.GetFunctionElements funcType
    equal domain typeof<int>
    equal range typeof<string>
    equal true (FSharpType.IsFunction funcType)

  testCase "FSharp.Reflection: Tuple" <| fun () ->
    let typ = typeof<string * int>
    let tuple = "a", 1
    let tupleTypeFields = FSharpType.GetTupleElements typ
    let tupleValueFields = FSharpValue.GetTupleFields tuple

    let expectedTupleFields =
        [|
            typeof<string>, box "a"
            typeof<int>, box 1
        |]

    let tupleFields = Array.zip tupleTypeFields tupleValueFields

    let isTuple = FSharpType.IsTuple typ
    let matchTupleFields = tupleFields = expectedTupleFields
    let matchIndividualTupleFields =
        tupleValueFields
        |> Array.mapi (fun i value -> i, value)
        |> Array.forall (fun (i, value) ->
            FSharpValue.GetTupleField(tuple, i) = value
        )
    let canMakeSameTuple =
        unbox<string * int> (FSharpValue.MakeTuple(tupleValueFields, typ)) = tuple

    let all = isTuple && matchTupleFields && matchIndividualTupleFields && canMakeSameTuple
    all |> equal true

  testCase "FSharp.Reflection: Array of tuples is not classified as a tuple" <| fun () ->
    let typ = typeof<(string * int * int)[]>
    equal true typ.IsArray
    FSharpType.IsTuple typ |> equal false

  testCase "FSharp.Reflection: MakeTupleType" <| fun () ->
    let t = FSharpType.MakeTupleType [|typeof<float>; typeof<string>; typeof<int[]>|]
    FSharpValue.MakeTuple([|5.; "foo"; [|2;3|]|], t)
    |> unbox<float * string * int[]>
    |> equal (5., "foo", [|2;3|])

    let real = typeof<float * string * int[]>
    let generated = FSharpType.MakeTupleType [|typeof<float>; typeof<string>; typeof<int[]>|]
    equal real generated

  testCase "FSharp.Reflection: Union" <| fun () ->
    let typ = typeof<TestUnion>
    let unionCase1 = StringCase("a", "b")
    let unionCase2 = IntCase 1
    let unionTypeFields = FSharpType.GetUnionCases typ
    unionTypeFields |> Array.map (fun x -> x.Name) |> equal [| "StringCase"; "IntCase" |]
    let unionCase1Info, unionCase1ValueFields = FSharpValue.GetUnionFields(unionCase1, typ)
    let unionCase2Info, unionCase2ValueFields = FSharpValue.GetUnionFields(unionCase2, typ)
    let unionCaseInfos = [| unionCase1Info; unionCase2Info |]
    let unionCaseValueFields = [| unionCase1ValueFields; unionCase2ValueFields |]

    let expectedUnionCase1Fields = 0, "StringCase", [| typeof<string>; typeof<string> |], [| "SomeString"; "Item2" |], [| box "a"; box "b" |]
    let expectedUnionCase2Fields = 1, "IntCase", [| typeof<int> |], [| "SomeInt" |], [| box 1 |]
    let expectedUnionFields = [| expectedUnionCase1Fields; expectedUnionCase2Fields |]

    let unionFields =
        Array.zip unionCaseInfos unionCaseValueFields
        |> Array.map (fun (info, values) ->
            let types =
                info.GetFields()
                |> Array.map (fun field -> field.PropertyType)
            let names =
                info.GetFields()
                |> Array.map (fun field -> field.Name)
            info.Tag, info.Name, types, names, values)

    let canMakeSameUnionCases =
        unbox<TestUnion> (FSharpValue.MakeUnion(unionCase1Info, unionCase1ValueFields)) = unionCase1
        && unbox<TestUnion> (FSharpValue.MakeUnion(unionCase2Info, unionCase2ValueFields)) = unionCase2

    FSharpType.IsUnion typ |> equal true
    unionFields |> equal expectedUnionFields
    canMakeSameUnionCases |> equal true

  testCase "FSharp.Reflection: Single-Case Union" <| fun () ->
    let typ = typeof<SingleCaseUnion>
    let unionCase1 = SingleCaseUnion("a", "b")
    let unionTypeFields = FSharpType.GetUnionCases typ
    unionTypeFields |> Array.map (fun x -> x.Name) |> equal [| "SingleCaseUnion" |]
    let unionCase1Info, unionCase1ValueFields = FSharpValue.GetUnionFields(unionCase1, typ)
    let unionCaseInfos = [| unionCase1Info |]
    let unionCaseValueFields = [| unionCase1ValueFields |]

    let expectedUnionCase1Fields = 0, "SingleCaseUnion", [| typeof<string>; typeof<string> |], [| "Item1"; "anotherString" |], [| box "a"; box "b" |]
    let expectedUnionFields = [| expectedUnionCase1Fields |]

    let unionFields =
        Array.zip unionCaseInfos unionCaseValueFields
        |> Array.map (fun (info, values) ->
            let types =
                info.GetFields()
                |> Array.map (fun field -> field.PropertyType)
            let names =
                info.GetFields()
                |> Array.map (fun field -> field.Name)
            info.Tag, info.Name, types, names, values)

    let builtUnion = FSharpValue.MakeUnion(unionCase1Info, unionCase1ValueFields) :?> SingleCaseUnion

    FSharpType.IsUnion typ |> equal true
    unionFields |> equal expectedUnionFields
    builtUnion = unionCase1 |> equal true
    builtUnion = SingleCaseUnion("c", "d") |> equal false

  testCase "FSharp.Reflection: Result" <| fun () ->
    let typ = typeof<Result<int,string>>
    let ucis = FSharpType.GetUnionCases typ
    FSharpValue.MakeUnion(ucis.[0], [|box 5|]) |> equal (box (Result<_,string>.Ok 5))
    FSharpValue.MakeUnion(ucis.[1], [|box "foo"|]) |> equal (box (Result<int,_>.Error "foo"))

  // See https://github.com/fable-compiler/Fable/issues/4082
  testCase "FSharp.Reflection: Option is a union type" <| fun () ->
    let typ = typeof<int option>
    FSharpType.IsUnion(typ) |> equal true
    let ucis = FSharpType.GetUnionCases(typ)
    ucis.Length |> equal 2
    ucis.[0].Name |> equal "None"
    ucis.[1].Name |> equal "Some"
    FSharpValue.MakeUnion(ucis.[0], [||]) |> equal (box (None: int option))
    FSharpValue.MakeUnion(ucis.[1], [|box 42|]) |> equal (box (Some 42))
    let noneCase, noneFields = FSharpValue.GetUnionFields(box (None: int option), typ)
    noneCase.Name |> equal "None"
    noneFields.Length |> equal 0
    let someCase, someFields = FSharpValue.GetUnionFields(box (Some 42), typ)
    someCase.Name |> equal "Some"
    someFields.Length |> equal 1
    someFields.[0] |> equal (box 42)

  testCase "FSharp.Reflection: List is a union type" <| fun () ->
    let typ = typeof<int list>
    FSharpType.IsUnion(typ) |> equal true
    let ucis = FSharpType.GetUnionCases(typ)
    ucis.Length |> equal 2
    ucis.[0].Name |> equal "Empty"
    ucis.[1].Name |> equal "Cons"

  testCase "FSharp.Reflection: Option round-trips through Some(None) and Some(Some x)" <| fun () ->
    let typ = typeof<int option option>
    let ucis = FSharpType.GetUnionCases(typ)
    let someCase, someFields = FSharpValue.GetUnionFields(box (Some (None: int option)), typ)
    someCase.Name |> equal "Some"
    someFields.[0] |> equal (box (None: int option))
    let someCase2, someFields2 = FSharpValue.GetUnionFields(box (Some (Some 42)), typ)
    someCase2.Name |> equal "Some"
    someFields2.[0] |> equal (box (Some 42))
    FSharpValue.MakeUnion(ucis.[1], [|box (None: int option)|]) |> equal (box (Some (None: int option)))
    FSharpValue.MakeUnion(ucis.[1], [|box (Some 42)|]) |> equal (box (Some (Some 42)))

  testCase "FSharp.Reflection: Choice" <| fun () ->
    let typ = typeof<Choice<int,string>>
    let ucis = FSharpType.GetUnionCases typ
    FSharpValue.MakeUnion(ucis.[0], [|box 5|]) |> equal (box (Choice<_,string>.Choice1Of2 5))
    FSharpValue.MakeUnion(ucis.[1], [|box "foo"|]) |> equal (box (Choice<int,_>.Choice2Of2 "foo"))

    let typ = typeof<Choice<float,string list,float>>
    let ucis = FSharpType.GetUnionCases typ
    FSharpValue.MakeUnion(ucis.[0], [|box -0.3|]) |> equal (box (Choice<_,string list,float>.Choice1Of3 -0.3))
    FSharpValue.MakeUnion(ucis.[1], [|box ["foo";"bar"]|]) |> equal (box (Choice<float,_,float>.Choice2Of3 ["foo";"bar"]))
    FSharpValue.MakeUnion(ucis.[2], [|box 3.5|]) |> equal (box (Choice<float,string list,_>.Choice3Of3 3.5))
    FSharpValue.MakeUnion(ucis.[2], [|box 3.5|]) |> (=) (box (Choice<float,string list,_>.Choice1Of3 3.5)) |> equal false

  // See reflection part of https://github.com/fable-compiler/Fable/issues/3586
  testCase "FSharp.Reflection: ignored args should not cause typescript error" <| fun () ->
    let typ = typeof<SingleCaseUnion>
    // In the typescript library, these functions only take 1 parameter
    ignore <| FSharpType.GetUnionCases(typ, allowAccessToPrivateRepresentation = true)
    ignore <| FSharpType.IsUnion(typ, allowAccessToPrivateRepresentation = true)
    ignore <| FSharpType.IsRecord(typ, allowAccessToPrivateRepresentation = true)
    ignore <| FSharpType.GetUnionCases(typ, System.Reflection.BindingFlags.Default)
    ignore <| FSharpType.IsUnion(typ, System.Reflection.BindingFlags.Default)
    ignore <| FSharpType.IsRecord(typ, System.Reflection.BindingFlags.Default)
    ()

  testCase "Type.GenericTypeArguments works" <| fun () ->
    let recordType = typeof<AsyncRecord>
    let asyncProp = FSharpType.GetRecordFields recordType |> Array.head
    asyncProp.PropertyType.GenericTypeArguments |> Array.head |> equal typeof<string>

  testCase "Recursive types work" <| fun () ->
    let cons =
        FSharpType.GetUnionCases(typeof<MyList<int>>)
        |> Array.find (fun x -> x.Name = "Cons")
    let fieldTypes = cons.GetFields()
    fieldTypes.[0].PropertyType.FullName |> equal typeof<int>.FullName
    fieldTypes.[1].PropertyType.GetGenericTypeDefinition().FullName |> equal typedefof<MyList<obj>>.FullName

  testCase "Calling constructor of generic type in inline functions works" <| fun () ->
    let a = create<A>()
    let b = create<B>()
    a.Value |> equal 5
    b.Value |> equal 10

  // See https://github.com/Microsoft/visualfsharp/issues/5992
  testCase "Generic numbers type info doesn't get into runtime" <| fun () ->
    let value = 0.7833263478179128134089M
    value.GetType().FullName |> equal "System.Decimal"

  // See https://github.com/thoth-org/Thoth.Json/issues/74
  testCase "Reflection info of int64/decimal with units of measure works" <| fun () ->
    typeof< int64 > = typeof< int64<m> > |> equal true
    typeof< decimal > = typeof< decimal<m> > |> equal true

  testCase "Reflection works with enums" <| fun () ->
      typeof<MyEnum>.IsEnum |> equal true
      typeof<int>.IsEnum |> equal false
      let t = typeof<MyEnum>
      t.IsEnum |> equal true
      t.GetEnumUnderlyingType() |> equal typeof<sbyte>
      System.Enum.GetUnderlyingType(t) |> equal typeof<sbyte>

  testCase "GetEnumUnderlyingType throws for non-enum types" <| fun () ->
      Fable.Tests.Util.throwsAnyError (fun () -> typeof<int>.GetEnumUnderlyingType())
      Fable.Tests.Util.throwsAnyError (fun () -> typeof<MyClass<int, string>>.GetEnumUnderlyingType())

  testCase "Can create generic classes at runtime" <| fun () ->
    let t = typedefof<MyClass<obj, obj>>
    let t = t.MakeGenericType(typeof<int>, typeof<string>)
    let x = System.Activator.CreateInstance(t, 123, "abc")
    x :? MyClass |> equal true
    x :? MyClass2 |> equal false
    let x = x :?> MyClass<int, string>
    x.Value1 |> equal 123
    x.Value2 |> equal "abc"

  testCase "IsSubclassOf works" <| fun () ->
    let t1 = typeof<MyClass>
    let t2 = typeof<MyClass<obj, obj>>
    let t3 = typeof<MyClass2>
    t3.IsSubclassOf(t1) |> equal false
    t2.IsSubclassOf(t1) |> equal true
    t1.IsSubclassOf(t2) |> equal false

  testCase "GetType works when a generic base takes the class itself" <| fun () -> // See #3607
    let t = SelfInheritor().GetType()
    t.Name |> equal "SelfInheritor"
    t = typeof<SelfInheritor> |> equal true
    t.IsSubclassOf(typeof<SelfBase<SelfInheritor>>) |> equal true
    SelfInheritor().Dummy() |> equal 1
    let ping = PairPing().GetType()
    ping.Name |> equal "PairPing"
    ping.IsSubclassOf(typeof<PairBase<PairPong>>) |> equal true
    PairPong().GetType().IsSubclassOf(typeof<PairBase<PairPing>>) |> equal true

  testCase ".GetInterface works when types are know at compile time" <| fun () -> // #2321
    let t = typeof<MyClass3<int>>.GetInterface("MyInterface`1")
    t.GetGenericArguments().[0] = typeof<int> |> equal true
    t.GetGenericArguments().[0] = typeof<string> |> equal false
    typeof<MyClass3<int>>.GetInterface("myInterface`1") |> isNull |> equal true
    typeof<MyClass3<int>>.GetInterface("myInterface`1", true) |> isNull |> equal false
    typeof<MyClass2>.GetInterface("MyInterface`1") |> isNull |> equal true

  testCase "GetType and typeof return the same Type instance for primitive types" <| fun () ->
    let inline getTypeGeneric (x: 'T) =
      x.GetType()

    obj.ReferenceEquals(getTypeGeneric 2, typeof<int>) |> equal true
    obj.ReferenceEquals(getTypeGeneric 2, typeof<uint32>) |> equal false
    obj.ReferenceEquals(getTypeGeneric 2u, typeof<uint32>) |> equal true
    obj.ReferenceEquals(getTypeGeneric 2uy, typeof<byte>) |> equal true
    obj.ReferenceEquals(getTypeGeneric 2s, typeof<int16>) |> equal true

    obj.ReferenceEquals(getTypeGeneric([| 2 |]).GetElementType(), typeof<int>) |> equal true
    obj.ReferenceEquals(getTypeGeneric([| 2 |]).GetElementType(), typeof<uint32>) |> equal false

  testCase "UnionCaseInfo.GetCustomAttributes returns the attributes of a case in order" <| fun () -> // #278
    let describe name =
        (caseAttributesOf name).GetCustomAttributes() |> Array.choose describeCaseAttribute
    describe "LabelCase" |> equal [| "Label(DoThis,0)" |]
    describe "SeveralCase" |> equal [| "Label(DoThat,0)"; "Marker" |]
    describe "PlainCase" |> equal [||]
    describe "SubLabelCase" |> equal [| "SubLabel(sub,3)" |]

  testCase "UnionCaseInfo.GetCustomAttributes filters by attribute type" <| fun () ->
    let describe name (t: System.Type) =
        (caseAttributesOf name).GetCustomAttributes(t) |> Array.choose describeCaseAttribute
    describe "SeveralCase" typeof<CaseLabelAttribute> |> equal [| "Label(DoThat,0)" |]
    describe "SeveralCase" typeof<CaseMarkerAttribute> |> equal [| "Marker" |]
    describe "SeveralCase" typeof<CaseSubLabelAttribute> |> equal [||]
    (caseAttributesOf "LabelCase").GetCustomAttributes(typeof<CaseMarkerAttribute>).Length |> equal 0
    (caseAttributesOf "PlainCase").GetCustomAttributes(typeof<CaseLabelAttribute>).Length |> equal 0
    // A derived attribute is returned for its base type
    describe "SubLabelCase" typeof<CaseLabelAttribute> |> equal [| "SubLabel(sub,3)" |]
    describe "SubLabelCase" typeof<CaseSubLabelAttribute> |> equal [| "SubLabel(sub,3)" |]
    describe "SeveralCase" typeof<System.Attribute> |> equal [| "Label(DoThat,0)"; "Marker" |]

  testCase "UnionCaseInfo.GetCustomAttributes builds attributes with their arguments" <| fun () ->
    let label name =
        (caseAttributesOf name).GetCustomAttributes(typeof<CaseLabelAttribute>).[0] :?> CaseLabelAttribute
    (label "NumberCase").Label |> equal "5"
    (label "DefaultCase").Label |> equal "default"
    (label "NamedCase").Label |> equal "named"
    (label "NamedCase").Priority |> equal 7
    (label "LabelCase").Priority |> equal 0
    let data =
        (caseAttributesOf "DataCase").GetCustomAttributes(typeof<CaseDataAttribute>).[0] :?> CaseDataAttribute
    data.Kind |> equal MyEnum.Bar
    data.Type |> equal typeof<string>
    data.Names |> equal [| "a"; "b" |]
    data.Letter |> equal 'c'
    data.Ratio |> equal 1.5
    data.Ok |> equal true

  testCase "UnionCaseInfo.GetCustomAttributes returns the names of the issue" <| fun () ->
    FSharpType.GetUnionCases typeof<UnionWithCaseAttributes>
    |> Array.take 2
    |> Array.collect (fun case -> case.GetCustomAttributes(typeof<CaseLabelAttribute>))
    |> Array.map (fun att -> (att :?> CaseLabelAttribute).Label)
    |> equal [| "DoThis"; "DoThat" |]

  testCase "Type.GetConstructors gives the public constructors of a class in declaration order" <| fun () -> // #277
    describeConstructors typeof<CtorClass>
    |> equal "0:x:System.Int32,1:name:System.String |  | 0:s:System.String"
    describeConstructors typeof<CtorParameterless> |> equal ""
    typeof<CtorParameterless>.GetConstructors().Length |> equal 1
    describeConstructors typeof<CtorPrivate> |> equal "0:a:System.String,1:b:System.Double"
    describeConstructors typeof<CtorDerived> |> equal ""
    typeof<CtorDerived>.GetConstructors().Length |> equal 1
    describeConstructors typeof<CtorAbstract> |> equal "0:x:System.Int32"
    describeConstructors typeof<CtorExplicit>
    |> equal "0:a:System.Int32 | 0:a:System.Int32,1:b:System.String"
    describeConstructors typeof<CtorStruct> |> equal "0:x:System.Int32"

  testCase "ConstructorInfo.GetParameters gives the parameter types" <| fun () -> // #277
    let parameters = typeof<CtorGeneric<string>>.GetConstructors().[0].GetParameters()
    parameters |> Array.map (fun p -> p.Name) |> equal [| "value"; "count" |]
    parameters.[0].ParameterType = typeof<string> |> equal true
    parameters.[1].ParameterType = typeof<int> |> equal true
    parameters |> Array.map (fun p -> p.Position) |> equal [| 0; 1 |]
    let parameters = typeof<CtorDependency>.GetConstructors().[0].GetParameters()
    parameters.[0].ParameterType = typeof<CtorParameterless> |> equal true
    parameters.[1].ParameterType = typeof<CtorGeneric<string>> |> equal true
    parameters.[1].ParameterType.GetConstructors().[0].GetParameters().[0].ParameterType = typeof<string>
    |> equal true

  testCase "ConstructorInfo.Invoke calls the constructor it describes" <| fun () -> // #277
    let constructors = typeof<CtorClass>.GetConstructors()
    let x = constructors.[0].Invoke([| box 7; box "z" |]) :?> CtorClass
    (x.X, x.Name) |> equal (7, "z")
    let x = constructors.[1].Invoke([||]) :?> CtorClass
    (x.X, x.Name) |> equal (1, "a")
    let x = constructors.[2].Invoke([| box "w" |]) :?> CtorClass
    (x.X, x.Name) |> equal (2, "w")
    let x = typeof<CtorParameterless>.GetConstructors().[0].Invoke([||]) :?> CtorParameterless
    x.Value |> equal 1
    let x = typeof<CtorPrivate>.GetConstructors().[0].Invoke([| box "abc"; box 2. |]) :?> CtorPrivate
    x.X |> equal 5
    let x = typeof<CtorDerived>.GetConstructors().[0].Invoke([||]) :?> CtorDerived
    (x.X, x.Name) |> equal (5, "d")
    let x = typeof<CtorGeneric<string>>.GetConstructors().[0].Invoke([| box "v"; box 2 |]) :?> CtorGeneric<string>
    (x.Value, x.Count) |> equal ("v", 2)
    let constructors = typeof<CtorExplicit>.GetConstructors()
    let x = constructors.[0].Invoke([| box 4 |]) :?> CtorExplicit
    (x.A, x.B) |> equal (4, "b")
    let x = constructors.[1].Invoke([| box 5; box "x" |]) :?> CtorExplicit
    (x.A, x.B) |> equal (5, "x")
    let x = typeof<CtorStruct>.GetConstructors().[0].Invoke([| box 9 |]) :?> CtorStruct
    x.X |> equal 9
    Fable.Tests.Util.throwsAnyError (fun () -> typeof<CtorClass>.GetConstructors().[0].Invoke([| box 7 |]))

  testCase "Constructors can be resolved recursively from their parameter types" <| fun () -> // #277
    let rec resolve (t: System.Type) : obj =
      if t = typeof<string> then box "resolved"
      elif t = typeof<int> then box 3
      else
        let ctor = t.GetConstructors().[0]
        ctor.GetParameters() |> Array.map (fun p -> resolve p.ParameterType) |> ctor.Invoke
    let x = resolve typeof<CtorDependency> :?> CtorDependency
    x.Service.Value |> equal 1
    (x.Generic.Value, x.Generic.Count) |> equal ("resolved", 3)

  testCase "Type.GetConstructor finds a constructor by its parameter types" <| fun () -> // #277
    let ctor = typeof<CtorClass>.GetConstructor([| typeof<string> |])
    ctor.GetParameters().[0].Name |> equal "s"
    (ctor.Invoke([| box "q" |]) :?> CtorClass).X |> equal 2
    (typeof<CtorClass>.GetConstructor([||]).Invoke([||]) :?> CtorClass).X |> equal 1
    typeof<CtorClass>.GetConstructor([| typeof<int>; typeof<string> |]).GetParameters().Length |> equal 2
    typeof<CtorClass>.GetConstructor([| typeof<float> |]) |> isNull |> equal true
    typeof<CtorClass>.GetConstructor([| typeof<string>; typeof<int> |]) |> isNull |> equal true

  testCase "Type.GetConstructors works for records and unions" <| fun () -> // #277
    describeConstructors typeof<CtorRecord>
    |> equal "0:first:System.Int32,1:secondField:System.String"
    typeof<CtorRecord>.GetConstructors().[0].Invoke([| box 1; box "q" |]) :?> CtorRecord
    |> equal { First = 1; SecondField = "q" }
    describeConstructors typeof<CtorStructRecord> |> equal "0:structField:System.Int32"
    typeof<CtorStructRecord>.GetConstructors().[0].Invoke([| box 2 |]) :?> CtorStructRecord
    |> equal { StructField = 2 }
    describeConstructors typeof<{| A: int; B: string |}> |> equal "0:A:System.Int32,1:B:System.String"
    typeof<{| A: int; B: string |}>.GetConstructors().[0].Invoke([| box 1; box "q" |]) :?> {| A: int; B: string |}
    |> equal {| A = 1; B = "q" |}
    typeof<CtorUnion>.GetConstructors().Length |> equal 0
    typeof<CtorStructUnion>.GetConstructors().Length |> equal 0
    typeof<CtorInterface>.GetConstructors().Length |> equal 0
    typeof<MyEnum>.GetConstructors().Length |> equal 0

  testCase "Type.IsClass works for types known when compiling" <| fun () -> // #277
    typeof<CtorClass>.IsClass |> equal true
    typeof<CtorAbstract>.IsClass |> equal true
    typeof<CtorGeneric<int>>.IsClass |> equal true
    typeof<CtorRecord>.IsClass |> equal true
    typeof<CtorUnion>.IsClass |> equal true
    typeof<CtorStructRecord>.IsClass |> equal false
    typeof<CtorStructUnion>.IsClass |> equal false
    typeof<CtorStruct>.IsClass |> equal false
    typeof<CtorInterface>.IsClass |> equal false
    typeof<MyEnum>.IsClass |> equal false
    typeof<int>.IsClass |> equal false
    typeof<string>.IsClass |> equal true
    typeof<obj>.IsClass |> equal true
    typeof<unit>.IsClass |> equal true
    typeof<int[]>.IsClass |> equal true
    typeof<int * string>.IsClass |> equal true
    typeof<struct (int * string)>.IsClass |> equal false
    typeof<int -> int>.IsClass |> equal true
    typeof<int option>.IsClass |> equal true
    typeof<int voption>.IsClass |> equal false
    typeof<System.Nullable<int>>.IsClass |> equal false
    typeof<int list>.IsClass |> equal true
    typeof<{| A: int |}>.IsClass |> equal true
    typeof<struct {| A: int |}>.IsClass |> equal false
    typeof<Result<int, string>>.IsClass |> equal false
    typeof<System.DateTime>.IsClass |> equal false
    typeof<seq<int>>.IsClass |> equal false

  testCase "Type.IsClass works for types known only when running" <| fun () -> // #277
    [ typeof<CtorClass>; typeof<CtorAbstract>; typeof<CtorGeneric<int>>; typeof<CtorDerived>; typeof<CtorRecord>
      typeof<CtorUnion>; typeof<string>; typeof<obj>; typeof<unit>; typeof<int[]>; typeof<int * string>
      typeof<int -> int>; typeof<System.Func<int, int>>; typeof<int option>; typeof<int list>; typeof<{| A: int |}>
      typeof<exn>; typeof<Map<int, int>>; typeof<ResizeArray<int>>; typeof<System.Type>
      typeof<System.Collections.Generic.Dictionary<int, int>> ]
    |> List.filter (isClass >> not)
    |> List.map (fun t -> t.FullName)
    |> equal []
    [ typeof<CtorStructRecord>; typeof<CtorStructUnion>; typeof<CtorStruct>; typeof<CtorInterface>
      typeof<MyInterface<int>>; typeof<MyEnum>; typeof<int>; typeof<int64>; typeof<float>; typeof<bool>; typeof<char>
      typeof<decimal>; typeof<bigint>; typeof<System.DateTime>; typeof<System.DateTimeOffset>; typeof<System.Guid>
      typeof<System.TimeSpan>; typeof<Result<int, string>>; typeof<seq<int>>; typeof<System.IDisposable>
      typeof<System.Collections.Generic.KeyValuePair<int, int>>; typeof<System.Threading.CancellationToken> ]
    |> List.filter isClass
    |> List.map (fun t -> t.FullName)
    |> equal []
    typeof<CtorRecord>.GetConstructors().[0].GetParameters() |> Array.map (fun p -> p.ParameterType.IsClass)
    |> equal [| false; true |]
]

#if FABLE_COMPILER
open Fable.Core
open Fable.Core.Reflection

type R1 = { x: int }
type R2 = { y: int }

type Maybe<'t> =
    | Just of 't
    | Nothing

type RecWithGenDU<'t> = { Other: 't; Value : Maybe<string> }
type GenericTestRecord<'t> = { Other: 't; Value : Maybe<string> }

// helper class to extract the name of a type argument
type Types() =
    static member inline getNameOf<'t> () : string =
        typeof<'t>.Name

    static member inline get<'t> () : System.Type =
        typeof<'t>

type MyUnion20 =
    | Union20_A
    | Union20_B of int * string

type MyRecord20 =
    { FieldA: int
      FieldB: string }

type ReactiveNode<'T>(value: 'T) =
    member _.Value = value

[<Struct; Erase>]
type ErasedView<'T> =
    | ErasedView of node: ReactiveNode<'T>

type ModelWithErasedField = { Item: ErasedView<int> }

type DuWithErasedField =
    | Wrapped of ErasedView<int>
    | Empty

let fableTests = [
    testCase "Recursively reading generic arguments of nested generic types works" <| fun () ->
        let typeInfo = Types.get<Maybe<Maybe<int>>>()

        // recursively reads the generic arguments
        let rec getGenericArgs (typeDef: System.Type) : string list =
            [ yield typeDef.Name
              for genericTypeArg in typeDef.GetGenericArguments() do
                yield! getGenericArgs genericTypeArg ]

        getGenericArgs typeInfo
        |> equal ["Maybe`1"; "Maybe`1"; "Int32"]

    testCase "Name can be extracted from RecWithGenDU" <| fun () ->
        let name = Types.getNameOf<Maybe<list<RecWithGenDU<string>>>>()
        equal false (name = "")

    testCase "Name can be extracted from GenericTestRecord" <| fun () ->
        let name = Types.getNameOf<Maybe<list<GenericTestRecord<string>>>>()
        equal false (name = "")

    testCase "Can check unions and records without type" <| fun () ->
        let x = box Union20_A
        let y = box { FieldA = 5; FieldB = "foo" }
        isUnion x |> equal true
        isRecord x |> equal false
        isUnion y |> equal false
        isRecord y |> equal true

    testCase "Can get union values without type" <| fun () ->
        let x = box Union20_A
        let y = Union20_B(5, "foo") |> box
        getCaseTag x |> equal 0
        getCaseTag y |> equal 1
        getCaseName x |> equal "Union20_A"
        getCaseName y |> equal "Union20_B"
        getCaseFields x |> equal [||]
        getCaseFields y |> equal [|5; "foo"|]

    testCase "Reflection works for a record with an erased generic union field" <| fun () ->
        // The record's generated reflection must resolve the erased union's generic
        // parameter; ErasedView<int> erases to (and reflects as) ReactiveNode<int>.
        let typ = typeof<ModelWithErasedField>
        FSharpType.IsRecord typ |> equal true
        let fields = FSharpType.GetRecordFields typ
        fields.Length |> equal 1
        fields.[0].PropertyType.GetGenericArguments().[0] |> equal typeof<int>
        // Reading the field back confirms the runtime value is the erased node itself.
        let model = { Item = ErasedView(ReactiveNode 42) }
        (FSharpValue.GetRecordFields model |> Array.head :?> ReactiveNode<int>).Value |> equal 42

    testCase "Reflection works for a union with an erased generic union field" <| fun () ->
        // Same fix as records: a union case field reflection must resolve the
        // erased union's generic parameter.
        let typ = typeof<DuWithErasedField>
        let case = (FSharpType.GetUnionCases typ).[0]
        case.GetFields().[0].PropertyType.GetGenericArguments().[0] |> equal typeof<int>
        let value = Wrapped(ErasedView(ReactiveNode 7))
        let _, fields = FSharpValue.GetUnionFields(value, typ)
        (fields.[0] :?> ReactiveNode<int>).Value |> equal 7
]

#else
let fableTests = []
#endif

// fickl#292: type arguments passed at runtime to a non-inline function
type PassGenericsRecord = { PassedX: int }

module PassGenericsFns =
    open Fickl.Core

    [<PassGenerics>]
    let nameOf<'T> () = typeof<'T>.Name

    [<PassGenerics>]
    let typeFunction<'T> = typeof<'T>.Name

    [<PassGenerics>]
    let fullNameOfArg (x: 'T) = typeof<'T>.FullName

    [<PassGenerics>]
    let typesOf<'A, 'B> (a: 'A) (b: 'B) = typeof<'A>, typeof<'B list>, typeof<'A * 'B>

    [<PassGenerics>]
    let forward<'T> (x: 'T) = fullNameOfArg x, typesOf x [ x ]

    let inline viaInline<'T> (x: 'T) = fullNameOfArg x

    [<PassGenerics>]
    let isInt<'T> () = typeof<'T> = typeof<int>

    [<PassGenerics>]
    let definitionOf<'T> () = typedefof<'T>

    [<PassGenerics>]
    let genericArgNames<'T> () = typeof<'T>.GetGenericArguments() |> Array.map (fun t -> t.Name)

    [<PassGenerics>]
    let inClosure<'T> (xs: int list) = xs |> List.map (fun x -> typeof<'T>.Name + string x)

    [<PassGenerics>]
    let rec repeatName<'T> (n: int) (acc: string) : string =
        if n = 0 then acc else repeatName<'T> (n - 1) (acc + typeof<'T>.Name)

    [<PassGenerics>]
    let fieldNames<'T> () = FSharpType.GetRecordFields typeof<'T> |> Array.map (fun p -> p.Name)

    [<PassGenerics>]
    let withOptionArg<'T> (x: int) (y: int option) = typeof<'T>.Name + string (x + defaultArg y 0)

    let inline inlineName<'U> () = typeof<'U>.Name

    let inline inlineIsString<'U> () = typeof<'U> = typeof<string>

    [<PassGenerics>]
    let viaInlineBody<'T> () = inlineName<'T> (), inlineIsString<'T> (), inlineName<'T option> ()

    [<PassGenerics>]
    let localFunction<'T> (x: int) =
        let describe (y: int) = typeof<'T>.Name + string (x + y)
        describe 1 + describe 2

    type Describer() =
        [<PassGenerics>]
        member _.Name<'U>() = typeof<'U>.Name

        [<PassGenerics>]
        member _.NameWith<'U>(prefix: string, ?suffix: string) = prefix + typeof<'U>.Name + defaultArg suffix ""

        [<PassGenerics>]
        static member StaticName<'U>(x: int, ?y: int) = typeof<'U>.Name + string (x + defaultArg y 0)

    type GenericDescriber<'T>() =
        [<PassGenerics>]
        member _.Names<'U>(_u: 'U) = typeof<'T>.Name + typeof<'U>.Name

let passGenericsTests =
    testList "PassGenerics" [
        testCase "typeof of a type parameter works in a function with PassGenerics" <| fun () ->
            PassGenericsFns.nameOf<int> () |> equal "Int32"
            PassGenericsFns.nameOf<string> () |> equal "String"
            PassGenericsFns.nameOf<PassGenericsRecord> () |> equal "PassGenericsRecord"
            PassGenericsFns.fullNameOfArg 2.5 |> equal "System.Double"
            PassGenericsFns.typeFunction<int64> |> equal "Int64"

        testCase "PassGenerics passes every type parameter and types built on them" <| fun () ->
            let a, b, c = PassGenericsFns.typesOf 1 "x"
            a |> equal typeof<int>
            b |> equal typeof<string list>
            c |> equal typeof<int * string>

        testCase "A function with PassGenerics forwards its type arguments" <| fun () ->
            let name, (a, b, c) = PassGenericsFns.forward true
            name |> equal "System.Boolean"
            a |> equal typeof<bool>
            b |> equal typeof<bool list list>
            c |> equal typeof<bool * bool list>

        testCase "An inline function resolves the type arguments of PassGenerics" <| fun () ->
            PassGenericsFns.viaInline 1L |> equal "System.Int64"

        testCase "Type tests of a passed type parameter happen at runtime" <| fun () ->
            PassGenericsFns.isInt<int> () |> equal true
            PassGenericsFns.isInt<string> () |> equal false
            PassGenericsFns.definitionOf<int list> () |> equal typedefof<int list>
            PassGenericsFns.definitionOf<int> () |> equal typeof<int>
            PassGenericsFns.genericArgNames<Map<string, int>> () |> equal [| "String"; "Int32" |]
            PassGenericsFns.fieldNames<PassGenericsRecord> () |> equal [| "PassedX" |]

        testCase "PassGenerics works in closures and recursive functions" <| fun () ->
            PassGenericsFns.inClosure<PassGenericsRecord> [ 1; 2 ]
            |> equal [ "PassGenericsRecord1"; "PassGenericsRecord2" ]
            PassGenericsFns.repeatName<string> 3 "" |> equal "StringStringString"

        testCase "PassGenerics functions can be partially applied and used as values" <| fun () ->
            [ 1; 2 ] |> List.map PassGenericsFns.fullNameOfArg |> equal [ "System.Int32"; "System.Int32" ]
            let partial = PassGenericsFns.typesOf 1
            let a, b, _ = partial "x"
            a |> equal typeof<int>
            b |> equal typeof<string list>
            let f: unit -> string = PassGenericsFns.nameOf<float>
            f () |> equal "Double"
            let g = PassGenericsFns.withOptionArg<bool> 1
            g None |> equal "Boolean1"
            g (Some 2) |> equal "Boolean3"

        testCase "Inline functions called by a PassGenerics function see its type arguments" <| fun () ->
            PassGenericsFns.viaInlineBody<string> () |> equal ("String", true, "FSharpOption`1")
            PassGenericsFns.viaInlineBody<int> () |> equal ("Int32", false, "FSharpOption`1")
            PassGenericsFns.localFunction<bool> 1 |> equal "Boolean2Boolean3"

        testCase "PassGenerics works on class members" <| fun () ->
            let d = PassGenericsFns.Describer()
            d.Name<int>() |> equal "Int32"
            d.NameWith<string>("a") |> equal "aString"
            d.NameWith<string>("a", "b") |> equal "aStringb"
            d.NameWith<string>("a", suffix = "c") |> equal "aStringc"
            PassGenericsFns.Describer.StaticName<string>(1) |> equal "String1"
            PassGenericsFns.Describer.StaticName<string>(1, 2) |> equal "String3"
            PassGenericsFns.GenericDescriber<int>().Names("s") |> equal "Int32String"
    ]

// Type.GetMethod, see #293
type GetMethodShape =
    abstract Area: scale: float -> float

[<AbstractClass>]
type GetMethodAnimal() =
    abstract Speak: unit -> string
    member _.Kind() = "animal"

type GetMethodDog() =
    inherit GetMethodAnimal()
    override _.Speak() = "woof"

type GetMethodGreeter() =
    abstract Greet: string -> string
    default _.Greet(name) = "hi " + name

type GetMethodLoudGreeter() =
    inherit GetMethodGreeter()
    override _.Greet(name) = "HI " + name

type GetMethodCalc(k: int) =
    static member Add(a: int, b: int) = a + b
    static member Hello() = "hello"
    static member Curried (a: int) (b: int) = a * b
    static member Log(msg: string) : unit = ignore msg
    static member Apply(f: int -> int -> int, x: int) = f x 2
    static member Opt(a: int, ?b: int) = a + defaultArg b 10
    static member Over(a: int) = a + 1
    static member Over(a: string) = a + "!"
    static member Fail() : int = failwith "boom"
    member _.Scale(x: int) = x * k
    member _.Name = "calc"
    member _.Mixed (a: int, b: int) (c: int) = a + b + c + k
    member private _.Hidden() = 1
    interface GetMethodShape with
        member _.Area scale = scale * float k

[<Fable.Core.AttachMembers>]
type GetMethodAttached(k: int) =
    member _.Twice(x: int) = x * 2 * k
    static member Make(x: int) = GetMethodAttached(x)

type GetMethodRecord =
    { X: int }
    member this.Plus(y: int) = this.X + y
    static member Zero() = { X = 0 }

type GetMethodUnion =
    | GetMethodA of int
    | GetMethodB
    member this.Describe() =
        match this with
        | GetMethodA i -> "A" + string i
        | GetMethodB -> "B"

type GetMethodBox<'T>(v: 'T) =
    member _.Value() = v
    member _.Same(x: 'T) = x
    static member wrap(x: 'T) = "wrapped " + string (box x)

type GetMethodIntBox() =
    inherit GetMethodBox<int>(5)

let inline getMethodName<'T> (name: string) = typeof<'T>.GetMethod(name).Name
let inline invokeStaticMethod<'T> (name: string) (args: obj[]) = typeof<'T>.GetMethod(name).Invoke(null, args)

let getMethodTests = [
    testCase "Type.GetMethod gives the name, parameters and return type of a static method" <| fun () ->
        let add = typeof<GetMethodCalc>.GetMethod("Add")
        add.Name |> equal "Add"
        add.IsStatic |> equal true
        add.GetParameters() |> Array.map (fun p -> p.Name) |> equal [| "a"; "b" |]
        add.GetParameters() |> Array.map (fun p -> p.ParameterType) |> equal [| typeof<int>; typeof<int> |]
        add.ReturnType |> equal typeof<int>

    testCase "MethodInfo.Invoke calls a static method" <| fun () ->
        let add = typeof<GetMethodCalc>.GetMethod("Add")
        add.Invoke(null, [| box 1; box 2 |]) |> unbox<int> |> equal 3
        add.Invoke(null, [| box 10; box 20 |]) |> unbox<int> |> equal 30

    testCase "MethodInfo.Invoke calls a method without parameters" <| fun () ->
        let hello = typeof<GetMethodCalc>.GetMethod("Hello")
        hello.GetParameters().Length |> equal 0
        hello.Invoke(null, [||]) |> unbox<string> |> equal "hello"
        hello.Invoke(null, null) |> unbox<string> |> equal "hello"

    testCase "MethodInfo.Invoke takes curried parameters one after another" <| fun () ->
        let curried = typeof<GetMethodCalc>.GetMethod("Curried")
        curried.GetParameters().Length |> equal 2
        curried.Invoke(null, [| box 3; box 4 |]) |> unbox<int> |> equal 12
        let mixed = typeof<GetMethodCalc>.GetMethod("Mixed")
        mixed.GetParameters().Length |> equal 3
        mixed.Invoke(GetMethodCalc(100), [| box 1; box 2; box 3 |]) |> unbox<int> |> equal 106

    testCase "MethodInfo.Invoke gives null for a method returning unit" <| fun () ->
        typeof<GetMethodCalc>.GetMethod("Log").Invoke(null, [| box "x" |]) |> isNull |> equal true

    testCase "MethodInfo.Invoke passes functions and optional arguments" <| fun () ->
        let apply = typeof<GetMethodCalc>.GetMethod("Apply")
        apply.Invoke(null, [| box (fun (a: int) (b: int) -> a - b); box 10 |]) |> unbox<int> |> equal 8
        let opt = typeof<GetMethodCalc>.GetMethod("Opt")
        opt.Invoke(null, [| box 1; box (Some 2) |]) |> unbox<int> |> equal 3
        opt.Invoke(null, [| box 1; null |]) |> unbox<int> |> equal 11

    testCase "MethodInfo.Invoke calls an instance method on the target" <| fun () ->
        let scale = typeof<GetMethodCalc>.GetMethod("Scale")
        scale.IsStatic |> equal false
        scale.Invoke(GetMethodCalc(3), [| box 5 |]) |> unbox<int> |> equal 15
        scale.Invoke(GetMethodCalc(4), [| box 5 |]) |> unbox<int> |> equal 20
        typeof<GetMethodCalc>.GetMethod("get_Name").Invoke(GetMethodCalc(3), [||]) |> unbox<string> |> equal "calc"

    testCase "Type.GetMethod gives null when the type has no such public method" <| fun () ->
        typeof<GetMethodCalc>.GetMethod("Missing") |> isNull |> equal true
        typeof<GetMethodCalc>.GetMethod("Hidden") |> isNull |> equal true
        // A property is not a method, and an explicit interface implementation is not public
        typeof<GetMethodCalc>.GetMethod("Name") |> isNull |> equal true
        typeof<GetMethodCalc>.GetMethod("Area") |> isNull |> equal true
        typeof<GetMethodCalc>.GetMethod(".ctor") |> isNull |> equal true
        typeof<GetMethodRecord>.GetMethod("Missing") |> isNull |> equal true
        // The static methods of a base class are not found
        typeof<GetMethodIntBox>.GetMethod("wrap") |> isNull |> equal true

    testCase "Type.GetMethod finds interface, abstract and inherited methods" <| fun () ->
        typeof<GetMethodShape>.GetMethod("Area").Invoke(GetMethodCalc(2), [| box 1.5 |]) |> unbox<float> |> equal 3.
        typeof<GetMethodAnimal>.GetMethod("Speak").Invoke(GetMethodDog(), [||]) |> unbox<string> |> equal "woof"
        typeof<GetMethodDog>.GetMethod("Speak").Invoke(GetMethodDog(), [||]) |> unbox<string> |> equal "woof"
        typeof<GetMethodDog>.GetMethod("Kind").Invoke(GetMethodDog(), [||]) |> unbox<string> |> equal "animal"
        let greet = typeof<GetMethodGreeter>.GetMethod("Greet")
        greet.Invoke(GetMethodGreeter(), [| box "a" |]) |> unbox<string> |> equal "hi a"
        greet.Invoke(GetMethodLoudGreeter(), [| box "a" |]) |> unbox<string> |> equal "HI a"
        typeof<GetMethodLoudGreeter>.GetMethod("Greet").Invoke(GetMethodLoudGreeter(), [| box "a" |])
        |> unbox<string>
        |> equal "HI a"

    testCase "Type.GetMethod works with AttachMembers classes" <| fun () ->
        typeof<GetMethodAttached>.GetMethod("Twice").Invoke(GetMethodAttached(2), [| box 5 |]) |> unbox<int> |> equal 20
        let made = typeof<GetMethodAttached>.GetMethod("Make").Invoke(null, [| box 3 |]) :?> GetMethodAttached
        made.Twice(1) |> equal 6

    testCase "Type.GetMethod works with records and unions" <| fun () ->
        typeof<GetMethodRecord>.GetMethod("Plus").Invoke({ X = 1 }, [| box 2 |]) |> unbox<int> |> equal 3
        typeof<GetMethodRecord>.GetMethod("Zero").Invoke(null, [||]) |> unbox<GetMethodRecord> |> equal { X = 0 }
        typeof<GetMethodRecord>.GetMethod("Zero").ReturnType |> equal typeof<GetMethodRecord>
        typeof<GetMethodUnion>.GetMethod("Describe").Invoke(GetMethodA 1, [||]) |> unbox<string> |> equal "A1"

    testCase "Type.GetMethod works with generic types" <| fun () ->
        let wrap = typeof<GetMethodBox<int>>.GetMethod("wrap")
        wrap.Invoke(null, [| box 5 |]) |> unbox<string> |> equal "wrapped 5"
        wrap.GetParameters().[0].ParameterType |> equal typeof<int>
        let value = typeof<GetMethodBox<string>>.GetMethod("Value")
        value.ReturnType |> equal typeof<string>
        value.Invoke(GetMethodBox("s"), [||]) |> unbox<string> |> equal "s"
        let inherited = typeof<GetMethodIntBox>.GetMethod("Value")
        inherited.ReturnType |> equal typeof<int>
        inherited.Invoke(GetMethodIntBox(), [||]) |> unbox<int> |> equal 5

    testCase "Type.GetMethod picks an overload by the types of its parameters" <| fun () ->
        typeof<GetMethodCalc>.GetMethod("Over", [| typeof<string> |]).Invoke(null, [| box "s" |])
        |> unbox<string>
        |> equal "s!"
        typeof<GetMethodCalc>.GetMethod("Over", [| typeof<int> |]).Invoke(null, [| box 1 |]) |> unbox<int> |> equal 2
        typeof<GetMethodCalc>.GetMethod("Add", [| typeof<string> |]) |> isNull |> equal true
        typeof<GetMethodCalc>.GetMethod("Hello", [||]).Name |> equal "Hello"
        typeof<GetMethodIntBox>.GetMethod("Same", [| typeof<int> |]).Invoke(GetMethodIntBox(), [| box 4 |])
        |> unbox<int>
        |> equal 4

    testCase "Type.GetMethod filters by binding flags" <| fun () ->
        let flags = System.Reflection.BindingFlags.Static ||| System.Reflection.BindingFlags.Public
        typeof<GetMethodCalc>.GetMethod("Add", flags).Name |> equal "Add"
        typeof<GetMethodCalc>.GetMethod("Scale", System.Reflection.BindingFlags.Static ||| System.Reflection.BindingFlags.Public)
        |> isNull
        |> equal true
        typeof<GetMethodCalc>.GetMethod("Add", System.Reflection.BindingFlags.Instance ||| System.Reflection.BindingFlags.Public)
        |> isNull
        |> equal true

    testCase "Type.GetMethod works with the type parameter of an inline function" <| fun () ->
        getMethodName<GetMethodUnion> "Describe" |> equal "Describe"
        invokeStaticMethod<GetMethodCalc> "Hello" [||] |> unbox<string> |> equal "hello"
        invokeStaticMethod<GetMethodCalc> "Add" [| box 2; box 3 |] |> unbox<int> |> equal 5

    testCase "Type.GetMethod works with a type and a name bound to values" <| fun () ->
        let t = typeof<GetMethodCalc>
        let name = "Add"
        t.GetMethod(name).Invoke(null, [| box 5; box 6 |]) |> unbox<int> |> equal 11

    testCase "Type.GetMethod finds a method declared in another file" <| fun () ->
        typeof<Fable.Tests.Util2.Helper>.GetMethod("Format").Invoke(null, [| box "{0}-{1}"; box [| box 1; box 2 |] |])
        |> unbox<string>
        |> equal "1-2"

    testCase "MethodInfo.Invoke checks the target and the number of arguments" <| fun () ->
        let add = typeof<GetMethodCalc>.GetMethod("Add")
        Fable.Tests.Util.throwsAnyError (fun () -> add.Invoke(null, [| box 1 |]))
        Fable.Tests.Util.throwsAnyError (fun () -> typeof<GetMethodCalc>.GetMethod("Scale").Invoke(null, [| box 1 |]))
        Fable.Tests.Util.throwsAnyError (fun () -> typeof<GetMethodCalc>.GetMethod("Fail").Invoke(null, [||]))
]

let genericTypeNamesTests =
    testList "Naming generic types"
      [  ]

let tests =
    testList "Reflection tests" (
        genericTests
        @ typeNameTests
        @ reflectionTests
        @ getMethodTests
        @ fableTests
        @ [ passGenericsTests ]
    )

#else // OPTIMIZE_FCS
let tests = testList "Reflection tests" []
#endif
