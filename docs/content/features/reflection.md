---
title: Reflection
description: The .NET reflection that Fickl adds for JavaScript and TypeScript, and the limits of each part.
---

Fable compiles a subset of .NET reflection: `typeof`, `FSharpType` and `FSharpValue` over records, unions and tuples, and the names and generic arguments of types. Fickl adds methods, constructors and union case attributes. This page covers what each addition supports and where it stops, so you can tell before you write the code whether it compiles.

JavaScript has no metadata tables to look members up in, so each addition works in one of two ways: resolved while compiling, which needs the type and the name in the source, or carried in the type's reflection info, which costs bytes in the output.

## `Type.GetMethod` and `MethodInfo.Invoke`

`typeof<T>.GetMethod("Name")`, with a type known at compile time and a literal name, is resolved while compiling. It gives a `MethodInfo` that carries a call to that one member, so `Invoke`, `Name`, `GetParameters`, `ReturnType` and `IsStatic` work ([#293](https://github.com/shayanhabibi/fickl/issues/293)).

```fsharp fickl
type Calculator(offset: int) =
    member _.Add(a: int, b: int) = a + b + offset
    static member Twice(x: int) = x * 2

let twice = typeof<Calculator>.GetMethod("Twice") |> nonNull

let callTwice () = twice.Invoke(null, [| box 21 |]) :?> int

let callAdd () =
    match typeof<Calculator>.GetMethod("Add") with
    | null -> 0
    | add -> add.Invoke(Calculator(1), [| box 2; box 3 |]) :?> int

let describe () =
    let parameters = twice.GetParameters() |> Array.map (fun p -> p.Name)
    parameters, twice.ReturnType.Name, twice.IsStatic
```

Each `GetMethod` call compiles to a `new MethodInfo(...)` with the method's name, parameters and return type, and a function that calls the compiled member. A missing or private method gives `null`, as on .NET. With `<Nullable>enable</Nullable>`, F# types the result as nullable, so match on `null` or use `nonNull`.

The type must be a class, record, union or interface compiled from F# sources. These are compile errors:

- a name that is not a literal;
- a type that is not known at compile time, including the result of `MakeGenericType`; `typeof<Box<int>>.GetMethod(...)` works;
- a name with more than one public method: pass the parameter types, `GetMethod("Add", [| typeof<int>; typeof<int> |])`;
- a generic method, or an `inline` member;
- binding flags other than `Public`, `Instance` and `Static`; `Public` is required.

An exception thrown by the invoked method propagates as it is, where .NET wraps it in a `TargetInvocationException`. `GetMethods` and `GetProperty(name).GetValue` are not implemented.

## Constructors and `Type.IsClass`

`Type.GetConstructors()`, `GetConstructor(Type[])`, `ConstructorInfo.GetParameters`, `Invoke` and `Name`, `ParameterInfo.Position`, and `Type.IsClass` compile for JavaScript and TypeScript ([#277](https://github.com/shayanhabibi/fickl/issues/277)). Unlike `GetMethod`, these work on a `Type` that is only known at run time, such as `p.ParameterType.GetConstructors()`.

```fsharp fickl
type Point(x: int, y: int) =
    new() = Point(0, 0)
    member _.X = x
    member _.Y = y

let signatures () =
    typeof<Point>.GetConstructors()
    |> Array.map (fun ctor ->
        ctor.GetParameters() |> Array.map (fun p -> $"{p.Position}:{p.Name}"))

let make () =
    let ctor = typeof<Point>.GetConstructor([| typeof<int>; typeof<int> |]) |> nonNull
    ctor.Invoke([| box 1; box 2 |]) :?> Point

let isClass = typeof<Point>.IsClass
```

The constructors are listed in declaration order, in the class's reflection info: `Point_$reflection` gains `[[[["x", int32_type], ["y", int32_type]]], [[], Point_$ctor]]`. `GetConstructor` returns `null` when no constructor has the given parameter types. Records and anonymous records report their constructor too; unions, interfaces and enums report none. `IsClass` of a type known at compile time is a constant, `export const isClass = true;` here.

Because any `Type` can reach `GetConstructors` at run time, the compiler cannot tell which classes need the information, so every class carries it:

- A class whose public constructors are not a single parameterless one carries about 25 bytes, plus about 20 per constructor parameter. A class with no constructor, only static members, carries `() => []`.
- A class with a single parameterless constructor, records and unions carry nothing extra.
- The JavaScript runtime library grew by about 1.4%.
- Nothing is emitted under `--noReflection`. The information sits inside the type's `$reflection` function, so a bundler that drops unused reflection removes it with it.

The limits:

- Types the runtime represents natively, such as tuples, options, lists, strings, arrays and functions, report no constructors.
- At run time, `IsClass` is `true` for struct tuples, `voption` and `Nullable<T>`, which cannot be told apart from their reference counterparts.
- An F# exception reports one constructor, where .NET reports two.
- Invoking the constructor of an abstract class constructs an object, where .NET throws.

## `UnionCaseInfo.GetCustomAttributes`

`GetCustomAttributes()` and `GetCustomAttributes(typeof<T>)` work on union cases, for attributes defined in your project ([#278](https://github.com/shayanhabibi/fickl/issues/278)).

```fsharp fickl
open Microsoft.FSharp.Reflection

type LabelAttribute(text: string) =
    inherit System.Attribute()
    member _.Text = text

type Status =
    | [<Label("Waiting")>] Pending
    | [<Label("Done")>] Complete

let labels () =
    FSharpType.GetUnionCases(typeof<Status>)
    |> Array.map (fun case ->
        match case.GetCustomAttributes(typeof<LabelAttribute>) with
        | [| :? LabelAttribute as label |] -> label.Text
        | _ -> case.Name)
```

The union's reflection info gains a `Status_$caseAttributes` function that constructs the attributes of each case, with their arguments. A union without such attributes emits nothing extra.

The parameterless call returns only the attributes that can be constructed, where .NET also returns compiler-generated and BCL ones. Only union cases are covered: attributes on types, record fields and properties are still not supported.
