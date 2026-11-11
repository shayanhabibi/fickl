[<RequireQualifiedAccess>]
module rec Fable.Transforms.OverloadSuffix

open Fable
open Fable.AST
open System.Collections.Generic

type ParamTypes = Fable.Type list

let private tryFindAttributeArgs fullName (atts: Fable.Attribute seq) =
    atts
    |> Seq.tryPick (fun att ->
        if att.Entity.FullName = fullName then
            Some att.ConstructorArgs
        else
            None
    )

// -------- End of helper functions

let private hashToString (i: int) =
    if i < 0 then
        "Z" + (abs i).ToString("X")
    else
        i.ToString("X")

// Not perfect but hopefully covers most of the cases
// Using only common constrains from https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/constraints
let private getConstraintHash strict genParams =
    function
    // TODO: Full member signature hash?
    | Fable.Constraint.HasMember(name, isStatic) ->
        (if isStatic then
             "static "
         else
             "")
        + "member "
        + name
    | Fable.Constraint.CoercesTo t -> ":>" + (getTypeFastFullName strict genParams t)
    | Fable.Constraint.IsNullable -> "null"
    | Fable.Constraint.IsNotNullable -> "not null"
    | Fable.Constraint.IsValueType -> "struct"
    | Fable.Constraint.IsReferenceType -> "not struct"
    | Fable.Constraint.HasDefaultConstructor -> "new"
    | Fable.Constraint.HasAllowsRefStruct -> "allows ref struct"
    | Fable.Constraint.HasComparison -> "comparison"
    | Fable.Constraint.HasEquality -> "equality"
    | Fable.Constraint.IsUnmanaged -> "unmanaged"
    | Fable.Constraint.IsDelegate(at, rt) ->
        "delegate"
        + ([ at; rt ]
           |> List.map (getTypeFastFullName strict genParams)
           |> String.concat "|")
    | Fable.Constraint.IsEnum t -> "enum" + (getTypeFastFullName strict genParams t)
    | Fable.Constraint.SimpleChoice types ->
        "simple choice"
        + (types |> List.map (getTypeFastFullName strict genParams) |> String.concat "|")

let rec private getTypeFastFullName (strict: bool) (genParams: IDictionary<_, _>) (t: Fable.Type) =
    match t with
    | Fable.Nullable(genArg, isStruct) ->
        let typeName = getTypeFastFullName strict genParams genArg

        if isStruct then
            $"System.Nullable<%s{typeName}>"
        else
            // there is no overload distinction for nullable reference types,
            // so the name and overload suffix are the same as the inner type
            typeName
    | Fable.Measure fullname -> fullname
    | Fable.GenericParam(name, isMeasure, constraints) ->
        if isMeasure then
            "measure"
        else
            match genParams.TryGetValue(name) with
            | true, i -> i
            | false, _ ->
                constraints
                |> List.map (getConstraintHash strict genParams)
                |> String.concat ","
    | Fable.Tuple(genArgs, isStruct) ->
        let genArgs =
            genArgs |> Seq.map (getTypeFastFullName strict genParams) |> String.concat " * "

        if isStruct then
            "struct " + genArgs
        else
            genArgs
    | Fable.Array(genArg, kind) ->
        let name =
            match kind with
            | Fable.ResizeArray -> "array"
            | Fable.MutableArray -> "resizearray"
            | Fable.ImmutableArray -> "immutablearray"

        getTypeFastFullName strict genParams genArg + " " + name
    | Fable.List genArg -> getTypeFastFullName strict genParams genArg + " list"
    | Fable.Option(genArg, isStruct) ->
        (if isStruct then
             "struct "
         else
             "")
        + (getTypeFastFullName strict genParams genArg)
        + " option"
    | Fable.LambdaType(argType, returnType) ->
        [ argType; returnType ]
        |> List.map (getTypeFastFullName strict genParams)
        |> String.concat " -> "
    // TODO: Use Func` instead?
    | Fable.DelegateType(argTypes, returnType) ->
        let name =
            argTypes @ [ returnType ]
            |> List.map (getTypeFastFullName strict genParams)
            |> String.concat " -> "

        if strict then
            "delegate(" + name + ")"
        else
            name
    | Fable.AnonymousRecordType(fieldNames, genArgs, isStruct) ->
        let fields =
            Seq.zip fieldNames genArgs
            |> Seq.map (fun (key, typ) -> key + " : " + getTypeFastFullName strict genParams typ)
            |> String.concat "; "

        (if isStruct then
             "struct "
         else
             "")
        + "{|"
        + fields
        + "|}"
    | Fable.DeclaredType(tdef, genArgs) ->
        let genArgs = genArgs |> Seq.mapToList (getTypeFastFullName strict genParams)
        // Not sure why, but when precompiling F# changes measure types to MeasureProduct<'M, MeasureOne>
        match tdef.FullName, genArgs with
        | Types.measureProduct2, [ measure; Types.measureOne ] ->
            // TODO: generalize it to support aggregate units such as <m/s> or more complex
            measure
        | _ ->
            let genArgs = String.concat "," genArgs

            let genArgs =
                if System.String.IsNullOrEmpty(genArgs) then
                    ""
                else
                    "[" + genArgs + "]"

            tdef.FullName + genArgs
    | Fable.MetaType -> Types.type_
    | Fable.Any -> Types.object
    | Fable.Unit -> Types.unit
    | Fable.Boolean -> Types.bool
    | Fable.Char -> Types.char
    | Fable.String -> Types.string
    | Fable.Regex -> Types.regex
    | Fable.Number(kind, detail) -> getNumberFullName false kind detail

// From https://stackoverflow.com/a/37449594
let private combineHashCodes (hashes: int seq) =
    let hashes = Seq.toArray hashes

    if hashes.Length = 0 then
        0
    else
        hashes |> Array.reduce (fun h1 h2 -> ((h1 <<< 5) + h1) ^^^ h2)

let private getHashWith strict (paramTypes: ParamTypes) genParams =
    paramTypes
    |> List.map (getTypeFastFullName strict genParams >> Naming.stringHash)
    |> combineHashCodes
    |> hashToString

// A delegate and an F# function have the same name here, and the hashes of existing members depend on it
let private getHashPrivate (paramTypes: ParamTypes) genParams = getHashWith false paramTypes genParams

let hasEmptyOverloadSuffix (curriedParamTypes: ParamTypes) =
    // Don't use overload suffix for members without arguments
    match curriedParamTypes with
    | [] -> true
    | [ Fable.Unit ] -> true
    | _ -> false

let getHash (entityGenericParams: string list) (curriedParamTypeGroups: Fable.Type list list) =
    match curriedParamTypeGroups with
    | [ paramTypes ] ->
        if hasEmptyOverloadSuffix paramTypes then
            ""
        else
            // Generics can have different names in signature
            // and implementation files, use the position instead
            let genParams =
                entityGenericParams |> List.mapi (fun i p -> p, string<int> i) |> dict

            getHashPrivate paramTypes genParams
    // Members with curried params cannot be overloaded in F#
    // TODO: Also private methods defined with `let` cannot be overloaded
    // but I don't know how to identify them in the AST
    | _ -> ""

/// fickl: as `getHash`, except that a member taking a delegate gets a hash of its own when `getHash` gives it
/// the hash of another overload, as with `'T -> 'U` and `Func<'T, 'U>`. `overloads` holds the curried
/// parameter types of the members sharing the member's name, the member included.
let getHashAmongOverloads
    (entityGenericParams: string list)
    (overloads: Fable.Type list list seq)
    (curriedParamTypeGroups: Fable.Type list list)
    =
    let hash = getHash entityGenericParams curriedParamTypeGroups

    match curriedParamTypeGroups with
    | [ paramTypes ] when hash <> "" ->
        let genParams =
            entityGenericParams |> List.mapi (fun i p -> p, string<int> i) |> dict

        let strictHash = getHashWith true paramTypes genParams

        let isShared () =
            overloads
            |> Seq.filter (fun overload -> getHash entityGenericParams overload = hash)
            |> Seq.truncate 2
            |> Seq.length
            |> (<) 1

        if strictHash <> hash && isShared () then
            strictHash
        else
            hash
    | _ -> hash

/// For op_Implicit/op_Explicit, the return type is part of the overload signature.
/// This function includes the return type in the hash to distinguish overloads that differ only by return type.
let getHashWithReturnType
    (entityGenericParams: string list)
    (curriedParamTypeGroups: Fable.Type list list)
    (returnType: Fable.Type)
    =
    match curriedParamTypeGroups with
    | [ paramTypes ] ->
        let genParams =
            entityGenericParams |> List.mapi (fun i p -> p, string<int> i) |> dict

        getHashPrivate (paramTypes @ [ returnType ]) genParams
    | _ -> ""

/// Used for extension members
let getExtensionHash (curriedParamTypeGroups: Fable.Type list list) =
    match curriedParamTypeGroups with
    | [ paramTypes ] ->
        if hasEmptyOverloadSuffix paramTypes then
            ""
        else
            // Type resolution in extension member seems to be different
            // and doesn't take generics into account
            dict [] |> getHashPrivate paramTypes
    // Members with curried params cannot be overloaded in F#
    | _ -> ""
