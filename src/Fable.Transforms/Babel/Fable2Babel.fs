module rec Fable.Transforms.Fable2Babel

open System
open Fable
open Fable.AST
open Fable.AST.Babel
open System.Collections.Generic
open System.Text.RegularExpressions

type ReturnStrategy =
    | Return
    | ReturnUnit
    | Assign of Expression
    | Target of Identifier

[<Struct>]
type ConstructorRef =
    | Annotation
    | ActualConsRef
    | Reflection

type Import =
    {
        Selector: string
        LocalIdent: string option
        Path: string
        /// Only requested from type annotations, so TypeScript can print it as `import type`
        IsTypeOnly: bool
    }

type ITailCallOpportunity =
    abstract Label: string
    abstract Args: string list
    abstract IsRecursiveRef: Fable.Expr -> bool

type UsedNames =
    {
        RootScope: HashSet<string>
        DeclarationScopes: HashSet<string>
        CurrentDeclarationScope: HashSet<string>
    }

type Context =
    {
        File: Fable.File
        UsedNames: UsedNames
        DecisionTargets: (Fable.Ident list * Fable.Expr) list
        HoistVars: Fable.Ident list -> bool
        TailCallOpportunity: ITailCallOpportunity option
        OptimizeTailCall: unit -> unit
        ScopedTypeParams: Set<string>
        BoundTypes: Map<string, Fable.Type>
        ForcedIdents: Set<string>
        IsNativeAsync: bool
        NativeGeneratorType: Fable.Type option
        /// <summary>Caches resolved symbol access keys within one emitted module.</summary>
        SymbolAccessCache: Dictionary<string, Expression option>
        /// <summary>Native fields replacing decorated auto-property backing storage in the current class.</summary>
        NativeAutoProperties: Map<string, ClassMember>
    }

type ModuleDecl(name, ?isPublic, ?isMutable, ?typ, ?doc) =
    member _.Name: string = name
    member _.IsPublic = defaultArg isPublic false
    member _.IsMutable = defaultArg isMutable false
    member _.Type = defaultArg typ Fable.Any
    member _.JsDoc: string option = doc

type IBabelCompiler =
    inherit Compiler
    abstract IsTypeScript: bool
    abstract GetAllImports: unit -> Import seq
    /// <summary>Resolves a symbol key, preserving imported and cross-file unique-symbol identity.</summary>
    abstract GetSymbolKey: Context * expression: string * path: string * sourceFile: string option -> Expression
    /// <summary>Returns generated symbol bindings to emit before declarations that reference them.</summary>
    abstract GetSymbolDeclarations: unit -> ModuleDeclaration list

    abstract GetImportExpr:
        Context * selector: string * path: string * range: SourceLocation option * ?noMangle: bool -> Expression

    /// <summary>Runs an annotation builder; imports it requests are type-only unless also requested as values.</summary>
    abstract InTypePosition: (unit -> 'T) -> 'T

    /// <summary>Marks the import bound to a local name as a value, e.g. when a class extends it.</summary>
    abstract MarkValueImport: localName: string -> unit

    abstract TransformAsExpr: Context * Fable.Expr -> Expression

    abstract TransformAsStatements: Context * ReturnStrategy option * Fable.Expr -> Statement array

    abstract TransformImport: Context * selector: string * path: string -> Expression

    abstract TransformFunction:
        Context * string option * Fable.Ident list * Fable.Expr -> (Parameter array) * BlockStatement

    abstract WarnOnlyOnce: string * ?range: SourceLocation -> unit

    abstract SaveTopDirectivePrologue: string -> unit

    abstract GetAllAllTopDirectivesPrologue: unit -> string seq

module Lib =

    let libCall (com: IBabelCompiler) ctx r moduleName memberName genArgs args =
        let typeArguments =
            Annotation.makeTypeParamInstantiationIfTypeScript com ctx genArgs

        let callee = com.TransformImport(ctx, memberName, getLibPath com moduleName)

        Expression.callExpression (callee, List.toArray args, ?typeArguments = typeArguments, ?loc = r)

    let libValue (com: IBabelCompiler) ctx moduleName memberName =
        com.TransformImport(ctx, memberName, getLibPath com moduleName)

    let tryJsConstructorWithSuffix (com: IBabelCompiler) ctx ent (suffix: string) =
        match JS.Replacements.tryConstructor com ent with
        | Some(Fable.Import(info, typ, range)) when suffix.Length > 0 ->
            let consExpr =
                Fable.Import({ info with Selector = info.Selector + suffix }, typ, range)

            com.TransformAsExpr(ctx, consExpr) |> Some
        | Some(Fable.IdentExpr ident) when suffix.Length > 0 ->
            let consExpr = Fable.IdentExpr { ident with Name = ident.Name + suffix }

            com.TransformAsExpr(ctx, consExpr) |> Some
        | consExpr -> consExpr |> Option.map (fun e -> com.TransformAsExpr(ctx, e))

    let tryJsConstructorFor purpose (com: IBabelCompiler) ctx (ent: Fable.Entity) =
        let isErased =
            match purpose with
            | Annotation ->
                ent.IsMeasure
                || (ent.IsInterface && not com.IsTypeScript)
                || (FSharp2Fable.Util.isErasedOrStringEnumEntity ent && not ent.IsFSharpUnion)
            // Historically we have used interfaces to represent JS classes in bindings,
            // so we allow explicit type references (e.g. for type testing) when the interface is global or imported.
            // But just in case we avoid referencing interfaces for reflection (as the type may not exist in actual code)
            | ActualConsRef ->
                if ent.IsInterface then
                    not (FSharp2Fable.Util.isGlobalOrImportedEntity ent)
                else
                    ent.IsMeasure || FSharp2Fable.Util.isErasedOrStringEnumEntity ent
            | Reflection ->
                ent.IsInterface
                || ent.IsMeasure
                || FSharp2Fable.Util.isErasedOrStringEnumEntity ent

        if isErased then
            None
        else
            let suffix =
                match purpose with
                | Reflection
                | ActualConsRef -> ""
                | Annotation when
                    com.IsTypeScript
                    && ent.IsFSharpUnion
                    && List.isMultiple ent.UnionCases
                    && not (Util.hasAnyAttribute [ Atts.stringEnum; Atts.erase; Atts.tsTaggedUnion ] ent.Attributes)
                    ->
                    Util.UnionHelpers.UNION_SUFFIX
                | Annotation -> ""

            tryJsConstructorWithSuffix com ctx ent suffix

    /// Cannot be used for annotations (use `tryJsConstructorFor Annotation` instead)
    let jsConstructor (com: IBabelCompiler) ctx (ent: Fable.Entity) =
        tryJsConstructorFor ActualConsRef com ctx ent
        |> Option.defaultWith (fun () ->
            $"Cannot find %s{ent.FullName} constructor" |> addError com [] None
            Expression.nullLiteral ()
        )

    let sanitizeMemberName memberName =
        match memberName with
        // Assigning `__proto__` (also as an object literal key) replaces the object's prototype
        | "constructor"
        | "__proto__" -> memberName + "$"
        | _ -> memberName

module private NativeDecorators =
    let attributes (attributes: Fable.Attribute seq) =
        attributes
        |> Seq.filter (fun att -> att.Entity.FullName = Atts.ficklJsDecorator)
        |> Seq.toList

    let private parse (att: Fable.Attribute) =
        match att.ConstructorArgs with
        | (:? string as expression) :: rest ->
            let path =
                match rest with
                | (:? string as path) :: _ -> path
                | _ -> ""

            let isField =
                att.ConstructorArgs
                |> List.tryPick (
                    function
                    | :? bool as flag -> Some flag
                    | _ -> None
                )
                |> Option.defaultValue false

            expression, path, isField
        | _ -> "", "", false

    let private expression (com: IBabelCompiler) ctx sourceFile att =
        let text, path, _ = parse att

        if String.IsNullOrWhiteSpace text then
            addError com [] None "JsDecorator requires a non-empty decorator expression"
            Expression.nullLiteral ()
        elif String.IsNullOrEmpty path then
            RawExpression(text, None)
        else
            let root =
                Regex.Match(text, @"^\s*([A-Za-z_$][\w$]*)(.*)$", RegexOptions.Singleline)

            if not root.Success then
                addError com [] None "An imported JsDecorator expression must begin with an exported identifier"
                Expression.nullLiteral ()
            else
                let path =
                    match sourceFile with
                    | Some file when path.StartsWith(".", StringComparison.Ordinal) ->
                        FSharp2Fable.Util.fixImportedRelativePath com path file
                    | _ -> path

                match com.TransformImport(ctx, root.Groups[1].Value, path) with
                | Expression.Identifier(Identifier(name, _)) -> RawExpression(name + root.Groups[2].Value, None)
                | _ ->
                    addError com [] None "Cannot resolve an imported decorator identifier"
                    Expression.nullLiteral ()

    let memberAttributes (info: Fable.MemberFunctionOrValue) =
        match info with
        | :? FSharp2Fable.FsMemberFunctionOrValue as info ->
            let attributes, isProperty, hasGetter, _ =
                FSharp2Fable.FsMemberFunctionOrValue.JsDecoratorAttributes info.FSharpMemberOrFunctionOrValue

            if
                isProperty
                && hasGetter
                && info.FSharpMemberOrFunctionOrValue.IsPropertySetterMethod
            then
                []
            else
                attributes |> List.map (fun att -> FSharp2Fable.FsAtt(att) :> Fable.Attribute)
        | _ -> attributes info.Attributes

    let memberExpressions (com: IBabelCompiler) ctx (info: Fable.MemberFunctionOrValue) =
        if
            (memberAttributes info |> List.exists (parse >> fun (_, _, isField) -> isField))
            && (FSharp2Fable.FsMemberFunctionOrValue.JsDecoratedAutoProperty info).IsNone
        then
            addError com [] None "JsDecorator field mode requires a mutable auto-property"

        memberAttributes info
        |> List.map (expression com ctx (info.DeclaringEntity |> Option.bind _.SourcePath))
        |> List.toArray

    let decorateMember com ctx info memberDeclaration =
        match memberExpressions com ctx info with
        | [||] -> memberDeclaration
        | decorators -> DecoratedClassMember(decorators, memberDeclaration)

    let decorateClass (com: IBabelCompiler) ctx (ent: Fable.Entity) declaration =
        if
            (attributes ent.Attributes
             |> List.exists (parse >> fun (_, _, isField) -> isField))
        then
            addError com [] None "JsDecorator field mode requires a mutable auto-property"

        let decorators =
            attributes ent.Attributes
            |> List.map (expression com ctx ent.Ref.SourcePath)
            |> List.toArray

        if decorators.Length = 0 then
            declaration
        else
            DecoratedModuleDeclaration(decorators, declaration)

    let prepareClass (com: IBabelCompiler) ctx (decl: Fable.ClassDecl) =
        let properties =
            decl.AttachedMembers
            |> List.choose (fun memberDecl ->
                let info = com.GetMember memberDecl.MemberRef

                if info.IsGetter then
                    FSharp2Fable.FsMemberFunctionOrValue.JsDecoratedAutoProperty info
                    |> Option.map (fun (backingName, propertyName) -> backingName, (propertyName, info, memberDecl))
                else
                    None
            )
            |> Map.ofList

        if properties.IsEmpty then
            decl, Map.empty
        else
            let initializers = Dictionary<string, Fable.Expr>()

            let rec isLiteral =
                function
                | Fable.Value((Fable.UnitConstant | Fable.BoolConstant _ | Fable.CharConstant _ | Fable.StringConstant _ | Fable.NumberConstant _ | Fable.Null _),
                              _) -> true
                | Fable.TypeCast(value, _) -> isLiteral value
                | Fable.Value(Fable.NewOption(None, _, _), _) -> true
                | _ -> false

            let rec constructorHasEffects =
                function
                | Fable.Sequential expressions -> List.exists constructorHasEffects expressions
                | Fable.Let(_, value, body) -> constructorHasEffects value || constructorHasEffects body
                | Fable.TypeCast(value, _) -> constructorHasEffects value
                | Fable.ObjectExpr([], _, None) -> false
                | expression -> canHaveSideEffects com expression

            let rec rewrite hadEffects body =
                match body with
                | Fable.Sequential expressions ->
                    let mutable hadEffects = hadEffects

                    expressions
                    |> List.map (fun original ->
                        let rewritten = rewrite hadEffects original
                        hadEffects <- hadEffects || constructorHasEffects rewritten
                        rewritten
                    )
                    |> Fable.Sequential
                | Fable.Let(ident, value, body) ->
                    Fable.Let(ident, value, rewrite (hadEffects || constructorHasEffects value) body)
                | Fable.Set(_, Fable.FieldSet name, _, value, range) when
                    properties.ContainsKey name && not (initializers.ContainsKey name)
                    ->
                    if hadEffects || not (isLiteral value) then
                        addError
                            com
                            []
                            range
                            "Decorated auto-properties require literal initializers before constructor side effects"

                    initializers.Add(name, value)
                    Fable.Value(Fable.UnitConstant, range)
                | body -> body

            let constructor =
                decl.Constructor
                |> Option.map (fun constructor -> { constructor with Body = rewrite false constructor.Body })

            let members =
                properties
                |> Map.map (fun backingName (_, info, memberDecl) ->
                    if not info.IsInstance then
                        addError com [] memberDecl.Body.Range "Decorated auto-properties must be instance properties"

                    let value =
                        match initializers.TryGetValue backingName with
                        | true, value -> com.TransformAsExpr(ctx, value) |> Some
                        | _ ->
                            addError com [] memberDecl.Body.Range "Cannot locate a decorated auto-property initializer"
                            None

                    let modes =
                        memberAttributes info
                        |> List.map (parse >> fun (_, _, mode) -> mode)
                        |> List.distinct

                    if modes.Length > 1 then
                        addError
                            com
                            []
                            memberDecl.Body.Range
                            "Decorators on one auto-property must agree on field mode"

                    let key, computed = SymbolKeys.attachedKey com ctx info memberDecl

                    let annotation =
                        if com.IsTypeScript then
                            Some(Annotation.makeFieldAnnotation com ctx info.ReturnParameter.Type)
                        else
                            None

                    let memberDeclaration =
                        if modes = [ true ] then
                            ClassMember.classProperty (
                                key,
                                ?value = value,
                                isComputed = computed,
                                ?typeAnnotation = annotation
                            )
                        else
                            ClassAutoAccessor(
                                key,
                                value,
                                computed,
                                false,
                                Util.hidesBaseMember com info,
                                annotation,
                                memberDecl.Body.Range
                            )

                    decorateMember com ctx info memberDeclaration
                )

            { decl with Constructor = constructor }, members

module private SymbolKeys =
    let isWellKnownMember name (memberInfo: Fable.MemberFunctionOrValue) =
        memberInfo.Attributes
        |> Seq.exists (fun att ->
            att.Entity.FullName = Atts.ficklSymbolKey
            && (
                match att.ConstructorArgs with
                | (:? string as expression) :: rest ->
                    expression.Trim() = name
                    && (rest
                        |> List.forall (
                            function
                            | :? string as path -> String.IsNullOrEmpty path
                            | _ -> false
                        ))
                | _ -> false
            )
        )

    let private expression (com: IBabelCompiler) ctx sourceFile (att: Fable.Attribute) =
        match att.ConstructorArgs with
        | (:? string as expression) :: rest ->
            let path =
                match rest with
                | (:? string as path) :: _ -> path
                | _ -> ""

            com.GetSymbolKey(ctx, expression, path, sourceFile)
        | _ ->
            addError com [] None "SymbolKey requires a symbol expression"
            Expression.nullLiteral ()

    let tryMember (com: IBabelCompiler) ctx (memberInfo: Fable.MemberFunctionOrValue) =
        memberInfo.Attributes
        |> Seq.tryFind (fun att -> att.Entity.FullName = Atts.ficklSymbolKey)
        |> Option.map (expression com ctx (memberInfo.DeclaringEntity |> Option.bind _.SourcePath))

    let tryField com ctx field =
        FSharp2Fable.FsField.TrySymbolKey field
        |> Option.map (fun (att, file) -> expression com ctx (Some file) att)

    /// Interop slots such as `JS.AsyncIterable.asyncIterator` are bound with `[<Emit("$0[Symbol.asyncIterator]()")>]`,
    /// so implementations must use the same computed key for JS protocols like `for await` to find them.
    let private tryEmittedSymbol (memberInfo: Fable.MemberFunctionOrValue) =
        memberInfo.Attributes
        |> Seq.tryPick (fun att ->
            match att.ConstructorArgs with
            | (:? string as macro) :: _ when att.Entity.FullName = Atts.emitAttr ->
                let m = Regex.Match(macro, @"^\$0\[(Symbol\.[A-Za-z_$][\w$]*)\]\((?:\$1\.\.\.)?\)$")

                if m.Success then
                    Some(RawExpression(m.Groups[1].Value, None))
                else
                    None
            | _ -> None
        )

    let private trySlotKey com ctx info =
        tryMember com ctx info |> Option.orElseWith (fun () -> tryEmittedSymbol info)

    let memberKey com ctx info name =
        match trySlotKey com ctx info with
        | Some key -> key, true
        | None -> Util.memberFromName name

    let attachedKey (com: IBabelCompiler) ctx info (memb: Fable.MemberDecl) =
        let key =
            memb.ImplementedSignatureRef
            |> Option.bind com.TryGetMember
            |> Option.bind (trySlotKey com ctx)

        match key with
        | Some key -> key, true
        | None -> memberKey com ctx info memb.Name

    let fieldKey com ctx field =
        match tryField com ctx field with
        | Some key -> key, true
        | None -> Util.memberFromName field.Name

    let fieldAccess com ctx range field target =
        match tryField com ctx field with
        | Some key -> Expression.memberExpression (target, key, true, ?loc = range)
        | None -> Util.get range target field.Name

    let tryAccessKey (com: IBabelCompiler) (ctx: Context) typ name =
        let rec loop visited typ =
            match typ with
            | Fable.DeclaredType(entRef, _) when not (Set.contains entRef.FullName visited) ->
                let visited = Set.add entRef.FullName visited
                let ent = com.GetEntity entRef

                (ent.FSharpFields
                 |> List.tryPick (fun field ->
                     if field.Name = name then
                         tryField com ctx field
                     else
                         None
                 ))
                |> Option.orElseWith (fun () ->
                    ent.MembersFunctionsAndValues
                    |> Seq.tryPick (fun memberInfo ->
                        if memberInfo.CompiledName = name || memberInfo.DisplayName = name then
                            tryMember com ctx memberInfo
                        else
                            None
                    )
                )
                |> Option.orElseWith (fun () ->
                    ent.BaseType
                    |> Option.bind (fun baseType ->
                        loop visited (Fable.DeclaredType(baseType.Entity, baseType.GenericArgs))
                    )
                )
                |> Option.orElseWith (fun () ->
                    ent.AllInterfaces
                    |> Seq.tryPick (fun ifc -> loop visited (Fable.DeclaredType(ifc.Entity, ifc.GenericArgs)))
                )
            | Fable.GenericParam(_, _, constraints) ->
                constraints
                |> List.tryPick (
                    function
                    | Fable.Constraint.CoercesTo typ -> loop visited typ
                    | _ -> None
                )
            | _ -> None

        match typ with
        | Fable.DeclaredType(ent, _) ->
            let cacheKey = ent.FullName + string (char 31) + name

            match ctx.SymbolAccessCache.TryGetValue cacheKey with
            | true, key -> key
            | _ ->
                let key = loop Set.empty typ
                ctx.SymbolAccessCache.Add(cacheKey, key)
                key
        | _ -> loop Set.empty typ

    let access com ctx range typ target name =
        match tryAccessKey com ctx typ name with
        | Some key -> Expression.memberExpression (target, key, true, ?loc = range)
        | None -> Util.get range target name

module Reflection =
    open Lib

    let private libReflectionCall (com: IBabelCompiler) ctx r memberName args =
        libCall com ctx r "Reflection" (memberName + "_type") [] args

    let private transformRecordReflectionInfo com ctx r (ent: Fable.Entity) generics =
        // TODO: Refactor these three bindings to reuse in transformUnionReflectionInfo
        let fullname = ent.FullName
        let fullnameExpr = Expression.stringLiteral (fullname)

        let genMap =
            let genParamNames =
                ent.GenericParameters |> List.mapToArray (fun x -> x.Name) |> Seq.toArray

            Array.zip genParamNames generics |> Map |> Some

        let fields =
            ent.FSharpFields
            |> List.choose (fun fi ->
                if fi.IsStatic then
                    None
                else
                    // The F# name: the runtime maps it to the escaped property (see fieldKey in Util.ts)
                    let fieldName = fi.Name |> Expression.stringLiteral

                    let typeInfo = transformTypeInfoFor Reflection com ctx r genMap fi.FieldType

                    let fields =
                        match SymbolKeys.tryField com ctx fi with
                        | Some key -> [| fieldName; typeInfo; key |]
                        | None -> [| fieldName; typeInfo |]

                    Some(Expression.arrayExpression fields)
            )
            |> List.toArray

        let fields =
            Expression.arrowFunctionExpression ([||], Expression.arrayExpression (fields))

        [
            fullnameExpr
            Expression.arrayExpression (generics)
            jsConstructor com ctx ent
            fields
        ]
        |> libReflectionCall com ctx None "record"

    let private transformUnionReflectionInfo com ctx r (ent: Fable.Entity) generics =
        let fullname = ent.FullName
        let fullnameExpr = Expression.stringLiteral (fullname)

        let genMap =
            let genParamNames =
                ent.GenericParameters |> List.map (fun x -> x.Name) |> Seq.toArray

            Array.zip genParamNames generics |> Map |> Some

        let cases =
            ent.UnionCases
            |> Seq.map (fun uci ->
                uci.UnionCaseFields
                |> List.mapToArray (fun fi ->
                    Expression.arrayExpression (
                        [|
                            fi.Name |> Expression.stringLiteral
                            transformTypeInfoFor Reflection com ctx r genMap fi.FieldType
                        |]
                    )
                )
                |> Expression.arrayExpression
            )
            |> Seq.toArray

        let cases =
            Expression.arrowFunctionExpression ([||], Expression.arrayExpression (cases))

        [
            fullnameExpr
            Expression.arrayExpression (generics)
            jsConstructor com ctx ent
            cases
        ]
        |> libReflectionCall com ctx None "union"

    let transformTypeInfoFor
        purpose
        (com: IBabelCompiler)
        ctx
        r
        (genMap: Map<string, Expression> option)
        typ
        : Expression
        =
        let primitiveTypeInfo name =
            libValue com ctx "Reflection" (name + "_type")

        let numberInfo kind =
            getNumberKindName kind |> primitiveTypeInfo

        let nonGenericTypeInfo fullname =
            [ Expression.stringLiteral (fullname) ]
            |> libReflectionCall com ctx None "class"

        let resolveGenerics generics : Expression list =
            generics |> List.map (transformTypeInfoFor purpose com ctx r genMap)

        let genericTypeInfo name genArgs =
            let resolved = resolveGenerics genArgs
            libReflectionCall com ctx None name resolved

        let genericEntity (fullname: string) generics =
            libReflectionCall
                com
                ctx
                None
                "class"
                [
                    Expression.stringLiteral (fullname)
                    if not (Array.isEmpty generics) then
                        Expression.arrayExpression (generics)
                ]

        let genericGlobalOrImportedEntity generics (ent: Fable.Entity) =
            libReflectionCall
                com
                ctx
                None
                "class"
                [
                    yield Expression.stringLiteral (ent.FullName)
                    match generics with
                    | [||] -> yield Util.undefined None None
                    | generics -> yield Expression.arrayExpression (generics)
                    match tryJsConstructorFor purpose com ctx ent with
                    | Some cons -> yield cons
                    | None -> ()
                ]

        match typ with
        | Fable.Measure _
        | Fable.Any -> primitiveTypeInfo "obj"
        | Fable.GenericParam(name = name) ->
            match genMap with
            | None -> [ Expression.stringLiteral (name) ] |> libReflectionCall com ctx None "generic"
            | Some genMap ->
                match Map.tryFind name genMap with
                | Some t -> t
                | None ->
                    Replacements.Util.genericTypeInfoError name |> addError com [] r

                    Expression.nullLiteral ()
        | Fable.Unit -> primitiveTypeInfo "unit"
        | Fable.Boolean -> primitiveTypeInfo "bool"
        | Fable.Char -> primitiveTypeInfo "char"
        | Fable.String -> primitiveTypeInfo "string"
        | Fable.Number(kind, info) ->
            match info with
            | Fable.NumberInfo.IsEnum entRef ->
                let ent = com.GetEntity(entRef)

                let cases =
                    ent.FSharpFields
                    |> List.choose (fun fi ->
                        match fi.Name with
                        | "value__" -> None
                        | name ->
                            let value =
                                match fi.LiteralValue with
                                | Some v -> System.Convert.ToDouble v
                                | None -> 0.

                            Expression.arrayExpression (
                                [| Expression.stringLiteral (name); Expression.numericLiteral (value) |]
                            )
                            |> Some
                    )
                    |> Seq.toArray
                    |> Expression.arrayExpression

                [ Expression.stringLiteral (entRef.FullName); numberInfo kind; cases ]
                |> libReflectionCall com ctx None "enum"
            | _ -> numberInfo kind
        | Fable.LambdaType(argType, returnType) -> genericTypeInfo "lambda" [ argType; returnType ]
        | Fable.DelegateType(argTypes, returnType) -> genericTypeInfo "delegate" [ yield! argTypes; yield returnType ]
        | Fable.Tuple(genArgs, _) -> genericTypeInfo "tuple" genArgs
        | Fable.Nullable(genArg, true) -> genericTypeInfo "option" [ genArg ]
        | Fable.Nullable(genArg, false) -> transformTypeInfoFor purpose com ctx r genMap genArg
        | Fable.Option(genArg, _) -> genericTypeInfo "option" [ genArg ]
        | Fable.Array(genArg, _) -> genericTypeInfo "array" [ genArg ]
        | Fable.List genArg -> genericTypeInfo "list" [ genArg ]
        | Fable.Regex -> nonGenericTypeInfo Types.regex
        | Fable.MetaType -> nonGenericTypeInfo Types.type_
        | Fable.AnonymousRecordType(fieldNames, genArgs, _isStruct) ->
            let genArgs = resolveGenerics genArgs

            List.zip (fieldNames |> Array.toList) genArgs
            |> List.map (fun (k, t) -> Expression.arrayExpression [| Expression.stringLiteral (k); t |])
            |> libReflectionCall com ctx None "anonRecord"
        | Fable.DeclaredType(entRef, genArgs) ->
            let fullName = entRef.FullName

            match fullName, genArgs with
            | Replacements.Util.BuiltinEntity kind ->
                match kind with
                | Replacements.Util.BclGuid
                | Replacements.Util.BclTimeSpan
                | Replacements.Util.BclDateTime
                | Replacements.Util.BclDateTimeOffset
                | Replacements.Util.BclDateOnly
                | Replacements.Util.BclTimeOnly
                | Replacements.Util.BclRune
                | Replacements.Util.BclTimer -> genericEntity fullName [||]
                | Replacements.Util.BclHashSet gen
                | Replacements.Util.FSharpSet gen ->
                    genericEntity fullName [| transformTypeInfoFor purpose com ctx r genMap gen |]
                | Replacements.Util.BclDictionary(key, value)
                | Replacements.Util.BclKeyValuePair(key, value)
                | Replacements.Util.FSharpMap(key, value) ->
                    genericEntity
                        fullName
                        [|
                            transformTypeInfoFor purpose com ctx r genMap key
                            transformTypeInfoFor purpose com ctx r genMap value
                        |]
                | Replacements.Util.FSharpResult(ok, err) ->
                    let ent = com.GetEntity(entRef)

                    transformUnionReflectionInfo
                        com
                        ctx
                        r
                        ent
                        [|
                            transformTypeInfoFor purpose com ctx r genMap ok
                            transformTypeInfoFor purpose com ctx r genMap err
                        |]
                | Replacements.Util.FSharpChoice gen ->
                    let ent = com.GetEntity(entRef)

                    let gen = List.map (transformTypeInfoFor purpose com ctx r genMap) gen

                    List.toArray gen |> transformUnionReflectionInfo com ctx r ent
                | Replacements.Util.FSharpReference gen ->
                    let ent = com.GetEntity(entRef)

                    [| transformTypeInfoFor purpose com ctx r genMap gen |]
                    |> transformRecordReflectionInfo com ctx r ent
            | _ ->
                let generics =
                    genArgs
                    |> List.map (transformTypeInfoFor purpose com ctx r genMap)
                    |> List.toArray

                match com.GetEntity(entRef) with
                | Patterns.Try (Util.tryFindAnyEntAttribute [ Atts.stringEnum
                                                              Atts.erase
                                                              Atts.tsTaggedUnion
                                                              Atts.pojoDefinedByConsArgs ]) (att, _) as ent ->
                    match att with
                    | Atts.stringEnum -> primitiveTypeInfo "string"
                    | Atts.erase ->
                        match ent.UnionCases with
                        | [ uci ] when List.isSingle uci.UnionCaseFields ->
                            // Map the union's own generic parameters to the concrete use-site args
                            // so the wrapped field type (e.g. `Node<'T>` in `View<int>`) resolves.
                            let fieldGenMap =
                                Seq.zip (ent.GenericParameters |> Seq.map (fun p -> p.Name)) generics
                                |> Map
                                |> Some

                            transformTypeInfoFor purpose com ctx r fieldGenMap uci.UnionCaseFields[0].FieldType
                        | cases when cases |> List.forall (fun c -> List.isEmpty c.UnionCaseFields) ->
                            primitiveTypeInfo "string"
                        | _ -> genericEntity ent.FullName generics
                    | _ -> genericEntity ent.FullName generics
                | ent ->
                    if FSharp2Fable.Util.isGlobalOrImportedEntity ent then
                        genericGlobalOrImportedEntity generics ent
                    elif ent.IsInterface || FSharp2Fable.Util.isReplacementCandidate entRef then
                        genericEntity ent.FullName generics
                    elif ent.IsMeasure then
                        [ Expression.stringLiteral (ent.FullName) ]
                        |> libReflectionCall com ctx None "measure"
                    else
                        let reflectionMethodExpr =
                            FSharp2Fable.Util.entityIdentWithSuffix com entRef Naming.reflectionSuffix

                        let callee = com.TransformAsExpr(ctx, reflectionMethodExpr)

                        Expression.callExpression (callee, generics)

    let transformReflectionInfo com ctx r (ent: Fable.Entity) generics =
        if ent.IsFSharpRecord then
            transformRecordReflectionInfo com ctx r ent generics
        elif ent.IsFSharpUnion then
            transformUnionReflectionInfo com ctx r ent generics
        else
            [
                yield Expression.stringLiteral (ent.FullName)
                match generics with
                | [||] -> yield Util.undefined None None
                | generics -> yield Expression.arrayExpression (generics)
                match tryJsConstructorFor Reflection com ctx ent with
                | Some cons when not (com.IsTypeScript && ent.IsAbstractClass) -> yield cons
                | _ -> yield Util.undefined None None
                match ent.BaseType with
                | Some d ->
                    let genMap =
                        Seq.zip ent.GenericParameters generics
                        |> Seq.map (fun (p, e) -> p.Name, e)
                        |> Map
                        |> Some

                    yield
                        Fable.DeclaredType(d.Entity, d.GenericArgs)
                        |> transformTypeInfoFor Reflection com ctx r genMap
                | None -> ()
            ]
            |> libReflectionCall com ctx r "class"

    // Keep in sync with FSharp2Fable.Util.tryRuntimeTypeTestKey, which predicts these tests
    let transformTypeTest (com: IBabelCompiler) ctx range expr typ : Expression =
        let warnAndEvalToFalse msg =
            WarningCodes.typeTestEvalsToFalse msg |> addWarningWithCode com [] range

            Expression.booleanLiteral (false)

        let jsTypeof (primitiveType: string) (Util.TransformExpr com ctx expr) : Expression =
            let typeof = Expression.unaryExpression ("typeof", expr)

            Expression.binaryExpression (BinaryEqual, typeof, Expression.stringLiteral (primitiveType), ?loc = range)

        let jsInstanceof consExpr (Util.TransformExpr com ctx expr) : Expression =
            BinaryExpression(expr, consExpr, "instanceof", range)

        match typ with
        | Fable.Measure _ // Dummy, shouldn't be possible to test against a measure type
        | Fable.Any -> Expression.booleanLiteral (true)
        | Fable.Unit -> com.TransformAsExpr(ctx, expr) |> Util.makeNullCheck range true
        | Fable.Boolean -> jsTypeof "boolean" expr
        | Fable.Char
        | Fable.String -> jsTypeof "string" expr
        | Fable.Number(Decimal, _) -> jsInstanceof (libValue com ctx "Decimal" "default") expr
        | Fable.Number(JS.Replacements.BigIntegers _, _) -> jsTypeof "bigint" expr
        | Fable.Number _ -> jsTypeof "number" expr
        | Fable.Regex -> jsInstanceof (Expression.identifier ("RegExp")) expr
        | Fable.LambdaType _
        | Fable.DelegateType _ -> jsTypeof "function" expr
        | Fable.Array _
        | Fable.Tuple _ -> libCall com ctx None "Util" "isArrayLike" [] [ com.TransformAsExpr(ctx, expr) ]
        | Fable.List _ -> jsInstanceof (libValue com ctx "List" "FSharpList") expr
        | Fable.AnonymousRecordType _ -> warnAndEvalToFalse "anonymous records"
        | Fable.MetaType -> jsInstanceof (libValue com ctx "Reflection" "TypeInfo") expr
        | Fable.Option _ -> warnAndEvalToFalse "options" // TODO
        | Fable.GenericParam _ -> warnAndEvalToFalse "generic parameters"
        | Fable.Nullable(genArg, _isStruct) -> transformTypeTest com ctx range expr genArg
        | Fable.DeclaredType(entRef, genArgs) ->
            match entRef.FullName with
            | Types.idisposable ->
                match expr with
                | MaybeCasted(ExprType(Fable.DeclaredType(entRef2, _))) when
                    com.GetEntity(entRef2) |> FSharp2Fable.Util.hasInterface Types.idisposable
                    ->
                    Expression.booleanLiteral (true)
                | _ ->
                    [ com.TransformAsExpr(ctx, expr) ]
                    |> libCall com ctx range "Util" "isDisposable" []
            | Types.ienumerable ->
                [ com.TransformAsExpr(ctx, expr) ]
                |> libCall com ctx range "Util" "isIterable" []
            | Types.array ->
                [ com.TransformAsExpr(ctx, expr) ]
                |> libCall com ctx range "Util" "isArrayLike" []
            | Types.exception_ ->
                [ com.TransformAsExpr(ctx, expr) ]
                |> libCall com ctx range "Util" "isException" []
            | _ ->
                match com.GetEntity(entRef) with
                | Patterns.Try (Util.tryFindAnyEntAttribute [ Atts.stringEnum
                                                              Atts.erase
                                                              Atts.tsTaggedUnion
                                                              Atts.pojoDefinedByConsArgs ]) (att, _) as ent ->
                    match att with
                    | Atts.stringEnum -> jsTypeof "string" expr
                    | Atts.erase when ent.IsFSharpUnion ->
                        match ent.UnionCases with
                        | [ uci ] when List.isSingle uci.UnionCaseFields ->
                            transformTypeTest com ctx range expr uci.UnionCaseFields[0].FieldType
                        | cases when cases |> List.forall (fun c -> List.isEmpty c.UnionCaseFields) ->
                            jsTypeof "string" expr
                        | _ -> warnAndEvalToFalse (ent.FullName + " (erased)")
                    | Atts.pojoDefinedByConsArgs -> warnAndEvalToFalse (ent.FullName + " (pojo)")
                    | _ -> warnAndEvalToFalse (ent.FullName + " (erased)")

                | Patterns.Try (tryJsConstructorFor ActualConsRef com ctx) cons ->
                    if not (List.isEmpty genArgs) then
                        com.WarnOnlyOnce("Generic args are ignored in type testing", ?range = range)

                    jsInstanceof cons expr

                | _ -> warnAndEvalToFalse entRef.FullName

module Annotation =
    let isByRefOrAnyType (com: IBabelCompiler) =
        function
        | Replacements.Util.IsByRefType com _ -> true
        | Fable.Any -> true
        | _ -> false

    let makeTypeParamDecl (com: IBabelCompiler) (ctx: Context) genArgs =
        // Maybe there's a way to represent measurements in TypeScript
        genArgs
        |> List.chooseToArray (
            function
            | Fable.GenericParam(name, isMeasure, constraints) when not isMeasure ->
                // TODO: Other constraints? comparison, nullable
                let bound =
                    constraints
                    |> List.choose (
                        function
                        | Fable.Constraint.CoercesTo t -> makeTypeAnnotation com ctx t |> Some
                        | Fable.Constraint.IsEnum _ -> Some NumberTypeAnnotation
                        | _ -> None
                    )
                    |> function
                        | [] -> None
                        | [ t ] -> Some t
                        | ts -> ts |> List.toArray |> IntersectionTypeAnnotation |> Some

                TypeParameter.typeParameter (name, ?bound = bound) |> Some
            | _ -> None
        )
        |> Array.distinct

    let makeTypeParamInstantiation (com: IBabelCompiler) ctx genArgs =
        if List.isEmpty genArgs then
            [||]
        else
            genArgs
            |> List.chooseToArray (fun t ->
                if isUnitOfMeasure t then
                    None
                else
                    makeTypeAnnotation com ctx t |> Some
            )

    let makeTypeParamInstantiationIfTypeScript (com: IBabelCompiler) ctx genArgs =
        if com.IsTypeScript then
            makeTypeParamInstantiation com ctx genArgs |> Some
        else
            None

    let getGenericTypeAnnotation com ctx name genArgs =
        let typeParamInst = makeTypeParamInstantiation com ctx genArgs

        TypeAnnotation.aliasTypeAnnotation (Identifier.identifier (name), typeArguments = typeParamInst)

    let makeTypeAnnotation com ctx typ : TypeAnnotation =
        match typ with
        | Fable.Measure _
        | Fable.MetaType
        | Fable.Any -> AnyTypeAnnotation
        | Fable.Unit -> VoidTypeAnnotation
        | Fable.Boolean -> BooleanTypeAnnotation
        | Fable.Char -> StringTypeAnnotation
        | Fable.String -> StringTypeAnnotation
        | Fable.Regex -> makeAliasTypeAnnotation com ctx "RegExp"
        | Fable.Number(BigInt, _) -> makeAliasTypeAnnotation com ctx "bigint"
        | Fable.Number(_, Fable.NumberInfo.IsEnum(Patterns.Try (tryTsEnumTypeAnnotation com ctx) ta)) -> ta
        | Fable.Number(Int32, Fable.NumberInfo.IsEnum ent) when ent.FullName = "System.DateTimeKind" ->
            makeFableLibImportTypeAnnotation com ctx [] "Util" "DateTimeKind"
        | Fable.Number(kind, _) -> makeNumericTypeAnnotation com ctx kind
        | Fable.Nullable(genArg, isStruct) -> makeNullableTypeAnnotation com ctx isStruct genArg
        | Fable.Option(genArg, isStruct) -> makeOptionTypeAnnotation com ctx isStruct genArg
        | Fable.Tuple(genArgs, isStruct) -> makeTupleTypeAnnotation com ctx isStruct genArgs
        | Fable.Array(genArg, kind) -> makeArrayTypeAnnotation com ctx genArg kind
        | Fable.List genArg -> makeListTypeAnnotation com ctx genArg
        | Fable.GenericParam(name = name) -> makeAliasTypeAnnotation com ctx name
        | Fable.LambdaType(argType, returnType) -> makeFunctionTypeAnnotation com ctx typ [ argType ] returnType
        | Fable.DelegateType(argTypes, returnType) -> makeFunctionTypeAnnotation com ctx typ argTypes returnType
        | Fable.AnonymousRecordType(fieldNames, fieldTypes, _isStruct) ->
            makeAnonymousRecordTypeAnnotation com ctx fieldNames fieldTypes
        | Replacements.Util.Builtin kind -> makeBuiltinTypeAnnotation com ctx typ kind
        | Fable.DeclaredType(entRef, genArgs) -> com.GetEntity(entRef) |> makeEntityTypeAnnotation com ctx genArgs

    let makeArgTypeAnnotation (com: IBabelCompiler) ctx (id: Fable.Ident) =
        if com.IsTypeScript then
            match id.Type, id with
            | Replacements.Util.IsByRefType com typ, id when id.IsThisArgument -> makeTypeAnnotation com ctx typ |> Some
            | _ -> makeTypeAnnotation com ctx id.Type |> Some
        else
            None

    let makeTypeAnnotationIfTypeScript (com: IBabelCompiler) ctx typ expr =
        if com.IsTypeScript then
            match typ, expr with
            | Fable.Option _, _ -> makeTypeAnnotation com ctx typ |> Some
            // Use type annotation for NullLiteral and enum cases
            | _, Some(Literal(Literal.StringLiteral _))
            | _, Some(Literal(StringTemplate _))
            | _, Some(Literal(BooleanLiteral _))
            | _, Some(Literal(NumericLiteral _))
            | _, Some(Literal(RegExp _))
            | _, Some(FunctionExpression _)
            | _, Some(AsyncFunctionExpression _)
            | _, Some(GeneratorFunctionExpression _)
            | _, Some(ArrowFunctionExpression _) -> None
            | _, Some(AsExpression _) -> None
            | _ -> makeTypeAnnotation com ctx typ |> Some
        else
            None

    // Fields are uncurried in the AST but not the declaration
    let makeFieldAnnotation (com: IBabelCompiler) ctx (fieldType: Fable.Type) =
        FableTransforms.uncurryType fieldType |> makeTypeAnnotation com ctx

    /// fickl: a field marked [<Curried>] holds its function curried, so its annotation is not uncurried
    let makeFieldAnnotationOf (com: IBabelCompiler) ctx (field: Fable.Field) =
        if FSharp2Fable.FsField.IsCurriedField field then
            makeTypeAnnotation com ctx field.FieldType
        else
            makeFieldAnnotation com ctx field.FieldType

    let makeFieldAnnotationIfTypeScript (com: IBabelCompiler) ctx (fieldType: Fable.Type) =
        if com.IsTypeScript then
            makeFieldAnnotation com ctx fieldType |> Some
        else
            None

    let makeTypeAnnotationWithParametersIfTypeScript (com: IBabelCompiler) ctx typ expr =
        match makeTypeAnnotationIfTypeScript com ctx typ expr with
        | Some(FunctionTypeAnnotation _) as annotation ->
            let _, typeParams =
                match typ with
                | Fable.LambdaType(argType, returnType) -> [ argType; returnType ]
                | Fable.DelegateType(argTypes, returnType) -> argTypes @ [ returnType ]
                | _ -> []
                |> Util.getTypeParameters ctx

            annotation, makeTypeParamDecl com ctx typeParams
        | annotation -> annotation, [||]

    let makeAliasTypeAnnotation _com _ctx name =
        TypeAnnotation.aliasTypeAnnotation (Identifier.identifier (name))

    let makeGenericTypeAnnotation com ctx genArgs id =
        let typeParamInst = makeTypeParamInstantiation com ctx genArgs
        TypeAnnotation.aliasTypeAnnotation (id, typeArguments = typeParamInst)

    let makeNativeTypeAnnotation com ctx genArgs typeName =
        Identifier.identifier (typeName) |> makeGenericTypeAnnotation com ctx genArgs

    let makeFableLibImportTypeId (com: IBabelCompiler) ctx moduleName typeName =
        let expr =
            com.InTypePosition(fun () -> com.GetImportExpr(ctx, typeName, getLibPath com moduleName, None))

        match expr with
        | Expression.Identifier(id) -> id
        | _ -> Identifier.identifier (typeName)

    let makeFableLibImportTypeAnnotation com ctx genArgs moduleName typeName =
        let id = makeFableLibImportTypeId com ctx moduleName typeName
        makeGenericTypeAnnotation com ctx genArgs id

    let makeNumericTypeAnnotation com ctx kind =
        let moduleName =
            match kind with
            | Decimal -> "Decimal"
            | JS.Replacements.BigIntegers _ -> "BigInt"
            | _ -> "Int32"

        let typeName = getNumberKindName kind
        makeFableLibImportTypeAnnotation com ctx [] moduleName typeName

    let makeNullableTypeAnnotation com ctx isStruct genArg =
        if isStruct then
            makeFableLibImportTypeAnnotation com ctx [ genArg ] "Util" "Nullable"
        else
            makeTypeAnnotation com ctx genArg // nullable reference types are erased

    let makeOptionTypeAnnotation com ctx _isStruct genArg =
        makeFableLibImportTypeAnnotation com ctx [ genArg ] "Option" "Option"

    let makeTupleTypeAnnotation com ctx _isStruct genArgs =
        List.map (makeTypeAnnotation com ctx) genArgs
        |> List.toArray
        |> TupleTypeAnnotation

    let makeArrayTypeAnnotation com ctx genArg kind =
        match kind with
        | Fable.ResizeArray ->
            // makeNativeTypeAnnotation com ctx [genArg] "Array"
            makeTypeAnnotation com ctx genArg |> ArrayTypeAnnotation
        | Fable.MutableArray
        | Fable.ImmutableArray -> makeFableLibImportTypeAnnotation com ctx [ genArg ] "Util" "MutableArray"

    let makeListTypeAnnotation com ctx genArg =
        makeFableLibImportTypeAnnotation com ctx [ genArg ] "List" "FSharpList"

    let makeUnionTypeAnnotation com ctx genArgs =
        List.map (makeTypeAnnotation com ctx) genArgs
        |> List.toArray
        |> UnionTypeAnnotation

    let makeBuiltinTypeAnnotation com ctx typ kind =
        match kind with
        | Replacements.Util.BclGuid -> StringTypeAnnotation
        | Replacements.Util.BclTimeSpan ->
            if com.Options.JsTemporal then
                makeFableLibImportTypeAnnotation com ctx [] "TimeSpanTemporal" "Duration"
            else
                NumberTypeAnnotation
        | Replacements.Util.BclDateTime ->
            if com.Options.JsTemporal then
                makeFableLibImportTypeAnnotation com ctx [] "DateTimeTemporal" "PlainDateTime"
            else
                makeAliasTypeAnnotation com ctx "Date"
        | Replacements.Util.BclDateTimeOffset ->
            if com.Options.JsTemporal then
                makeFableLibImportTypeAnnotation com ctx [] "DateTimeOffsetTemporal" "ZonedDateTime"
            else
                makeAliasTypeAnnotation com ctx "Date"
        | Replacements.Util.BclDateOnly ->
            if com.Options.JsTemporal then
                makeFableLibImportTypeAnnotation com ctx [] "DateOnlyTemporal" "PlainDate"
            else
                makeAliasTypeAnnotation com ctx "Date"
        | Replacements.Util.BclTimeOnly ->
            if com.Options.JsTemporal then
                makeFableLibImportTypeAnnotation com ctx [] "TimeOnlyTemporal" "PlainTime"
            else
                NumberTypeAnnotation
        | Replacements.Util.BclRune -> NumberTypeAnnotation
        | Replacements.Util.BclTimer -> makeFableLibImportTypeAnnotation com ctx [] "Timer" "Timer"
        | Replacements.Util.BclHashSet key -> makeFableLibImportTypeAnnotation com ctx [ key ] "Util" "ISet"
        | Replacements.Util.BclDictionary(key, value) ->
            makeFableLibImportTypeAnnotation com ctx [ key; value ] "Util" "IMap"
        | Replacements.Util.BclKeyValuePair(key, value) -> makeTupleTypeAnnotation com ctx true [ key; value ]
        | Replacements.Util.FSharpSet key -> makeFableLibImportTypeAnnotation com ctx [ key ] "Set" "FSharpSet"
        | Replacements.Util.FSharpMap(key, value) ->
            makeFableLibImportTypeAnnotation com ctx [ key; value ] "Map" "FSharpMap"
        | Replacements.Util.FSharpResult(ok, err) ->
            $"FSharpResult$2%s{Util.UnionHelpers.UNION_SUFFIX}"
            |> makeFableLibImportTypeAnnotation com ctx [ ok; err ] "Result"
        | Replacements.Util.FSharpChoice genArgs ->
            $"FSharpChoice$%d{List.length genArgs}%s{Util.UnionHelpers.UNION_SUFFIX}"
            |> makeFableLibImportTypeAnnotation com ctx genArgs "Choice"
        // An inref parameter gets a reference cell like a byref (struct receivers are annotated as the struct)
        | Replacements.Util.FSharpReference genArg ->
            makeFableLibImportTypeAnnotation com ctx [ genArg ] "Types" "FSharpRef"

    let makeFunctionTypeAnnotation com ctx _typ argTypes returnType =
        let funcTypeParams =
            match argTypes with
            | [ Fable.Unit ] -> []
            | _ -> argTypes
            |> List.mapi (fun i argType ->
                FunctionTypeParam.functionTypeParam (
                    Identifier.identifier ($"arg%d{i}"),
                    makeTypeAnnotation com ctx argType
                )
            )
            |> List.toArray

        let returnType = makeTypeAnnotation com ctx returnType
        TypeAnnotation.functionTypeAnnotation (funcTypeParams, returnType)

    // Move this to Replacements.tryEntity?
    let tryNativeOrFableLibraryInterface com ctx genArgs (ent: Fable.Entity) =
        match ent.FullName with
        | Types.fsharpAsyncGeneric -> makeFableLibImportTypeAnnotation com ctx genArgs "AsyncBuilder" "Async" |> Some
        | Types.task
        | "System.Threading.Tasks.ValueTask" -> makeNativeTypeAnnotation com ctx [ Fable.Unit ] "Promise" |> Some
        | Types.taskGeneric -> makeNativeTypeAnnotation com ctx genArgs "Promise" |> Some
        | "Fickl.Core.JsAsyncGenerator`3" -> makeNativeTypeAnnotation com ctx genArgs "AsyncGenerator" |> Some
        | "Fickl.Core.JsYieldRequest`2" -> makeTypeAnnotation com ctx genArgs.Head |> Some
        | "Fickl.Core.JsGenerator`3" -> makeNativeTypeAnnotation com ctx genArgs "Generator" |> Some
        | "Fickl.Core.JsGeneratorResult`2" -> makeNativeTypeAnnotation com ctx genArgs "IteratorResult" |> Some
        | "Fickl.Core.JsYieldResult`1" -> makeNativeTypeAnnotation com ctx genArgs "IteratorYieldResult" |> Some
        | "Fickl.Core.JsReturnResult`1" -> makeNativeTypeAnnotation com ctx genArgs "IteratorReturnResult" |> Some
        | _ when not ent.IsInterface -> None
        // everything below is an interface
        | Types.icollection ->
            // makeFableLibImportTypeAnnotation com ctx [Fable.Any] "Util" "ICollection" |> Some
            makeNativeTypeAnnotation com ctx [ Fable.Any ] "Iterable" |> Some
        | Types.icollectionGeneric ->
            // makeFableLibImportTypeAnnotation com ctx genArgs "Util" "ICollection" |> Some
            makeNativeTypeAnnotation com ctx genArgs "Iterable" |> Some
        // | Types.idictionary
        // fickl: read-only views accept what F# can pass as them, including readonly arrays and ReadonlyMap
        | Types.ireadonlydictionary -> makeFableLibImportTypeAnnotation com ctx genArgs "Util" "IReadOnlyMap" |> Some
        | Types.ireadonlycollection
        | "System.Collections.Generic.IReadOnlyList`1" -> makeNativeTypeAnnotation com ctx genArgs "Iterable" |> Some
        | Types.idisposable -> makeFableLibImportTypeAnnotation com ctx genArgs "Util" "IDisposable" |> Some
        | Types.iasyncdisposable ->
            makeFableLibImportTypeAnnotation com ctx genArgs "Util" "IAsyncDisposable"
            |> Some
        | Types.ienumerable ->
            // makeFableLibImportTypeAnnotation com ctx [Fable.Any] "Util" "IEnumerable" |> Some
            makeNativeTypeAnnotation com ctx [ Fable.Any ] "Iterable" |> Some
        | Types.ienumerableGeneric ->
            // makeFableLibImportTypeAnnotation com ctx genArgs "Util" "IEnumerable" |> Some
            makeNativeTypeAnnotation com ctx genArgs "Iterable" |> Some
        | Types.ienumerator ->
            makeFableLibImportTypeAnnotation com ctx [ Fable.Any ] "Util" "IEnumerator"
            |> Some
        | Types.ienumeratorGeneric -> makeFableLibImportTypeAnnotation com ctx genArgs "Util" "IEnumerator" |> Some
        | Types.icomparable ->
            makeFableLibImportTypeAnnotation com ctx [ Fable.Any ] "Util" "IComparable"
            |> Some
        | Types.icomparableGeneric
        | Types.iStructuralComparable -> makeFableLibImportTypeAnnotation com ctx genArgs "Util" "IComparable" |> Some
        | Types.iequatableGeneric
        | Types.iStructuralEquatable -> makeFableLibImportTypeAnnotation com ctx genArgs "Util" "IEquatable" |> Some
        | Types.icomparer ->
            makeFableLibImportTypeAnnotation com ctx [ Fable.Any ] "Util" "IComparer"
            |> Some
        | Types.icomparerGeneric -> makeFableLibImportTypeAnnotation com ctx genArgs "Util" "IComparer" |> Some
        | Types.iequalityComparerGeneric ->
            makeFableLibImportTypeAnnotation com ctx genArgs "Util" "IEqualityComparer"
            |> Some
        | Types.iobserverGeneric ->
            makeFableLibImportTypeAnnotation com ctx genArgs "Observable" "IObserver"
            |> Some
        | Types.iobservableGeneric ->
            makeFableLibImportTypeAnnotation com ctx genArgs "Observable" "IObservable"
            |> Some
        | Types.ilistGeneric -> makeFableLibImportTypeAnnotation com ctx genArgs "Util" "MutableArray" |> Some
        | "Microsoft.FSharp.Control.IEvent`1" ->
            makeFableLibImportTypeAnnotation com ctx genArgs "Event" "IEvent" |> Some
        | Types.ievent2 -> makeFableLibImportTypeAnnotation com ctx genArgs "Event" "IEvent$2" |> Some
        | "Fable.Core.JS.Set`1" -> makeFableLibImportTypeAnnotation com ctx genArgs "Util" "ISet" |> Some
        | "Fable.Core.JS.Map`2" -> makeFableLibImportTypeAnnotation com ctx genArgs "Util" "IMap" |> Some
        | "Fable.Core.JS.Uint8ClampedArray" -> makeNativeTypeAnnotation com ctx [] "Uint8ClampedArray" |> Some
        // FCS erases the Fable.Core aliases, so only TypedArray<element> reaches here
        | "Fable.Core.JS.TypedArray`1" ->
            match genArgs with
            | [ Fable.Number(kind, _) ] ->
                match kind with
                | Int8 -> Some "Int8Array"
                | UInt8 -> Some "Uint8Array"
                | Int16 -> Some "Int16Array"
                | UInt16 -> Some "Uint16Array"
                | Int32 -> Some "Int32Array"
                | UInt32 -> Some "Uint32Array"
                | Int64
                | BigInt -> Some "BigInt64Array"
                | UInt64 -> Some "BigUint64Array"
                | Float32 -> Some "Float32Array"
                | Float64 -> Some "Float64Array"
                | _ -> None
            | _ -> None
            |> Option.map (makeNativeTypeAnnotation com ctx [])
        | _ -> None

    /// The literal type of a union case's `[<CompiledValue>]`, which is the value emitted for the case
    let tryCompiledValueLiteral (uci: Fable.UnionCase) =
        uci.Attributes
        |> Seq.tryFind (fun att -> att.Entity.FullName = Atts.compiledValue)
        |> Option.bind (fun att ->
            match att.ConstructorArgs with
            | (:? int as value) :: _ -> Literal.numericLiteral (float value) |> Some
            | (:? float as value) :: _ -> Literal.numericLiteral value |> Some
            | (:? bool as value) :: _ -> Literal.booleanLiteral value |> Some
            | (:? Enum as value) :: _ when Enum.GetUnderlyingType(value.GetType()) = typeof<int> ->
                Literal.numericLiteral (float (box value :?> int)) |> Some
            | _ -> None
        )

    let makeStringEnumTypeAnnotation (ent: Fable.Entity) (attArgs: obj list) =
        let rule =
            match List.tryHead attArgs with
            | Some(:? int as rule) -> enum<Core.CaseRules>(rule)
            | _ -> Core.CaseRules.LowerFirst

        ent.UnionCases
        |> List.mapToArray (fun uci ->
            // Same precedence as FSharp2Fable.Util.transformStringEnum, which emits the values
            match uci.CompiledName, tryCompiledValueLiteral uci with
            | Some name, _ -> Literal.stringLiteral name |> LiteralTypeAnnotation
            | None, Some value -> LiteralTypeAnnotation value
            | None, None when Util.hasAttribute Atts.emitAttr uci.Attributes -> AnyTypeAnnotation
            | None, None ->
                Naming.applyCaseRule rule uci.Name
                |> Literal.stringLiteral
                |> LiteralTypeAnnotation
        )
        |> UnionTypeAnnotation

    let makeErasedUnionTypeAnnotation com ctx genArgs (ent: Fable.Entity) =
        let transformSingleFieldType (uci: Fable.UnionCase) =
            List.tryHead uci.UnionCaseFields
            |> Option.map (fun fi -> fi.FieldType |> resolveInlineType genArgs |> makeFieldAnnotation com ctx)
            |> Option.defaultValue VoidTypeAnnotation

        match ent.UnionCases with
        | [ uci ] when List.isMultiple uci.UnionCaseFields ->
            uci.UnionCaseFields
            |> List.mapToArray (fun fi -> fi.FieldType |> resolveInlineType genArgs |> makeFieldAnnotation com ctx)
            |> TupleTypeAnnotation
        | [ uci ] -> transformSingleFieldType uci
        | ucis -> ucis |> List.mapToArray transformSingleFieldType |> UnionTypeAnnotation

    let makeTypeScriptTaggedUnionTypeAnnotation com ctx genArgs (ent: Fable.Entity) (attArgs: obj list) =
        let tag, rule =
            match attArgs with
            | (:? string as tag) :: (:? int as rule) :: _ -> tag, enum<Core.CaseRules>(rule)
            | (:? string as tag) :: _ -> tag, Core.CaseRules.LowerFirst
            | _ -> "kind", Core.CaseRules.LowerFirst

        ent.UnionCases
        |> List.mapToArray (fun uci ->
            let tagMember =
                // Same precedence as FSharp2Fable's tag value: CompiledValue, then CompiledName, then the case rule
                let tagType =
                    match tryCompiledValueLiteral uci, uci.CompiledName with
                    | Some value, _ -> LiteralTypeAnnotation value
                    | None, Some name -> Literal.stringLiteral name |> LiteralTypeAnnotation
                    | None, None ->
                        Naming.applyCaseRule rule uci.Name
                        |> Literal.stringLiteral
                        |> LiteralTypeAnnotation

                let prop, isComputed = Util.memberFromName tag

                AbstractMember.abstractProperty (prop, tagType, isComputed = isComputed)

            let inlinedType =
                if hasExplicitInlineInterface ent then
                    tryInlinedInterface com ent uci |> Result.defaultValue None
                else
                    tryInlinedTaggedUnionCaseType com uci

            match inlinedType with
            | Some fieldType ->
                IntersectionTypeAnnotation
                    [|
                        fieldType |> resolveInlineType genArgs |> makeFieldAnnotation com ctx
                        ObjectTypeAnnotation [| tagMember |]
                    |]
            | None ->
                let names, types =
                    uci.UnionCaseFields |> List.map (fun fi -> fi.Name, fi.FieldType) |> List.unzip

                makeAnonymousRecordTypeAnnotation com ctx (List.toArray names) types
                |> function
                    | ObjectTypeAnnotation members -> ObjectTypeAnnotation(Array.append [| tagMember |] members)
                    | t -> t // Unexpected
        )
        |> UnionTypeAnnotation

    /// `extends` evaluates its class, so the import behind it cannot be type-only
    let makeSuperType (com: IBabelCompiler) (annotation: TypeAnnotation) =
        match annotation with
        | AliasTypeAnnotation(Identifier(name, _), _) -> com.MarkValueImport(name)
        | _ -> ()

        SuperType annotation

    let private annotationConstructor (com: IBabelCompiler) ctx ent =
        com.InTypePosition(fun () -> Lib.tryJsConstructorFor Annotation com ctx ent)

    /// fickl: the identifier an `EmitType` text starts with, which its module path imports
    let private emitTypeHead =
        Regex(@"^\s*((?!\$\d)[A-Za-z_$][\w$]*)", RegexOptions.Compiled)

    /// fickl: `[<EmitType>]` writes the annotation as its text. With a module path, the leading identifier
    /// becomes one more argument, so the import it names is renamed and tracked like any other type reference.
    let tryEmitTypeAnnotation (com: IBabelCompiler) ctx genArgs (ent: Fable.Entity) =
        ent.Attributes
        |> Seq.tryFind (fun att -> att.Entity.FullName = Atts.ficklEmitType)
        |> Option.map (fun att ->
            let args = genArgs |> List.mapToArray (makeTypeAnnotation com ctx)

            match att.ConstructorArgs with
            | (:? string as text) :: (:? string as from) :: _ when not (String.IsNullOrEmpty from) ->
                let head = emitTypeHead.Match(text)

                if head.Success then
                    let name = head.Groups[1]

                    let path =
                        if Path.isRelativePath from then
                            ent.Ref.SourcePath
                            |> Option.map (FSharp2Fable.Util.fixImportedRelativePath com from)
                            |> Option.defaultValue from
                        else
                            from

                    let id =
                        match com.InTypePosition(fun () -> com.GetImportExpr(ctx, name.Value, path, None)) with
                        | Expression.Identifier id -> id
                        | _ -> Identifier.identifier name.Value

                    let text =
                        text.Substring(0, name.Index)
                        + "$"
                        + string args.Length
                        + text.Substring(name.Index + name.Length)

                    EmitTypeAnnotation(text, Array.append args [| AliasTypeAnnotation(id, [||]) |])
                else
                    $"EmitType on {ent.FullName} imports from '{from}', but its text does not start with the name to import"
                    |> addError com [] None

                    EmitTypeAnnotation(text, args)
            | (:? string as text) :: _ -> EmitTypeAnnotation(text, args)
            | _ -> AnyTypeAnnotation
        )

    /// fickl: a `[<TsEnum>]` enum is typed by its name, which TypeScript resolves to the enum's ambient declaration
    let tryTsEnumTypeAnnotation (com: IBabelCompiler) ctx (entRef: Fable.EntityRef) =
        match com.TryGetEntity entRef with
        | Some ent when FSharp2Fable.Util.isTsEnumEntity com ent ->
            match annotationConstructor com ctx ent with
            | Some(Expression.Identifier id) -> AliasTypeAnnotation(id, [||]) |> Some
            | _ -> None
        | _ -> None

    /// fickl: property keys, written here rather than by their EmitType text so arguments get parentheses
    /// and `keyof<'T, 'R>` gets the library's name for the mapped type
    let tryKeyTypeAnnotation (com: IBabelCompiler) ctx genArgs (ent: Fable.Entity) =
        match ent.FullName, genArgs with
        | "Fickl.Core.keyof`1", [ target ] ->
            IntersectionTypeAnnotation
                [|
                    KeyofTypeAnnotation(makeTypeAnnotation com ctx target)
                    StringTypeAnnotation
                |]
            |> Some
        | "Fickl.Core.keyof`2", _ -> makeFableLibImportTypeAnnotation com ctx genArgs "Util" "KeysOfType" |> Some
        | _ -> None

    let makeEntityTypeAnnotation com ctx genArgs (ent: Fable.Entity) =
        match genArgs, ent with
        | _, Patterns.Try (tryKeyTypeAnnotation com ctx genArgs) ta -> ta
        | _, Patterns.Try (tryEmitTypeAnnotation com ctx genArgs) ta -> ta
        | _, Patterns.Try (tryNativeOrFableLibraryInterface com ctx genArgs) ta -> ta

        | _, Patterns.Try (annotationConstructor com ctx) entRef ->
            match entRef with
            | Literal(Literal.StringLiteral(StringLiteral(str, _))) ->
                match str with
                | "number" -> NumberTypeAnnotation
                | "boolean" -> BooleanTypeAnnotation
                | "string" -> StringTypeAnnotation
                | _ -> AnyTypeAnnotation
            | Expression.Identifier(id) -> makeGenericTypeAnnotation com ctx genArgs id
            // TODO: Resolve references to types in nested modules
            | _ -> AnyTypeAnnotation

        | _,
          Patterns.Try (Util.tryFindAnyEntAttribute [ Atts.erase; Atts.stringEnum; Atts.tsTaggedUnion ]) (attFullName,
                                                                                                          attArgs) when
            ent.IsFSharpUnion
            ->

            let genArgs =
                List.zip ent.GenericParameters genArgs
                |> List.map (fun (p, a) -> p.Name, a)
                |> Map

            match attFullName with
            | Atts.stringEnum -> makeStringEnumTypeAnnotation ent attArgs
            | Atts.erase -> makeErasedUnionTypeAnnotation com ctx genArgs ent
            | _ -> makeTypeScriptTaggedUnionTypeAnnotation com ctx genArgs ent attArgs

        | _ -> AnyTypeAnnotation

    let unwrapOptionalType t =
        match t with
        | Fable.Option(t, _) when not (mustWrapOption t) -> t
        | _ -> t

    /// Getters returning an erased option are typed `T | undefined` in TypeScript. Unlike `unwrapOptionalArg`,
    /// the unwrapped body keeps the option type so the return is not coerced like a plain `T` (e.g. `| 0` turning
    /// `undefined` into `0` for `int option`). Returns the unwrapped type to use for the return annotation.
    let unwrapOptionalReturn com (body: Fable.Expr) =
        match body.Type with
        | Fable.Option(t, isStruct) when not (mustWrapOption t || Util.isStrictOption com isStruct t) ->
            match body with
            | Fable.Value(Fable.NewOption(Some arg, _, _), _) -> Some t, arg
            | Fable.Value(Fable.NewOption(None, _, _), _) -> Some t, body
            | _ -> Some t, Replacements.Util.Helper.LibCall(com, "Option", "unwrap", body.Type, [ body ])
        | _ -> None, body

    let unwrapOptionalArg com (arg: Fable.Expr) =
        match arg.Type with
        | Fable.Option(t, _) when not (mustWrapOption t) ->
            match arg with
            | Fable.Value(Fable.NewOption(Some arg, _, _), _) -> true, arg
            | Fable.Value(Fable.NewOption(None, _, _), _) -> true, Fable.TypeCast(arg, t)
            | _ -> true, Replacements.Util.Helper.LibCall(com, "Option", "unwrap", t, [ arg ])
        | _ -> false, arg

    // In TypeScript we don't need to type optional properties or arguments as Option (e.g. `{ foo?: string }` so we try to unwrap the option.
    // But in some situations this may conflict with TS type cheking, usually when we assing an Option ident directly to the field (e.g. `fun (i: int option) -> {| foo = i |})
    // If we find problems we may need to disable this, or make sure somehow the values assigned to the fields are not `Some`.
    let makeAbstractPropertyAnnotation com ctx typ =
        let isOptional, typ =
            match typ with
            | Fable.Option(genArg, isStruct) ->
                if mustWrapOption genArg || Util.isStrictOption com isStruct genArg then
                    true, typ
                else
                    true, genArg
            | typ -> false, typ

        let annotation = makeFieldAnnotation com ctx typ

        let annotation =
            if isOptional then
                UnionTypeAnnotation [| annotation; UndefinedTypeAnnotation |]
            else
                annotation

        isOptional, annotation

    let makeAnonymousRecordTypeAnnotation com ctx fieldNames fieldTypes : TypeAnnotation =
        Seq.zip fieldNames fieldTypes
        |> Seq.mapToArray (fun (name, typ) ->
            let prop, isComputed = Util.memberFromName name
            let isOptional, typ = makeAbstractPropertyAnnotation com ctx typ

            AbstractMember.abstractProperty (prop, typ, isComputed = isComputed, isOptional = isOptional)
        )
        |> ObjectTypeAnnotation

    /// Like `transformFunctionWithAnnotations` but the return type annotation
    /// is made from `returnType` instead of the type of `body`
    let transformFunctionWithReturnType
        (com: IBabelCompiler)
        ctx
        name
        typeParams
        (args: Fable.Ident list)
        (returnType: Fable.Type)
        (body: Fable.Expr)
        =
        if com.IsTypeScript then
            let argTypes = args |> List.map (fun id -> id.Type)

            let scopedTypeParams, genParams =
                match typeParams with
                | Some typeParams -> ctx.ScopedTypeParams, typeParams
                | None -> Util.getTypeParameters ctx (argTypes @ [ returnType ])

            let ctx = { ctx with ScopedTypeParams = scopedTypeParams }
            let args', body' = com.TransformFunction(ctx, name, args, body)
            let returnType = makeTypeAnnotation com ctx returnType
            let typeParamDecl = makeTypeParamDecl com ctx genParams
            args', body', Some returnType, Some typeParamDecl
        else
            let args', body' = com.TransformFunction(ctx, name, args, body)
            args', body', None, None

    let transformFunctionWithAnnotations
        (com: IBabelCompiler)
        ctx
        name
        typeParams
        (args: Fable.Ident list)
        (body: Fable.Expr)
        =
        transformFunctionWithReturnType
            com
            { ctx with
                IsNativeAsync = false
                NativeGeneratorType = None
            }
            name
            typeParams
            args
            body.Type
            body

module Util =
    open Lib
    open Reflection
    open Annotation

    module UnionHelpers =
        [<Literal>]
        let CASES_SUFFIX = "_$cases"

        [<Literal>]
        let UNION_SUFFIX = "_$union"

    let IMPORT_REGEX =
        Regex("""^import\b\s*(\{?.*?\}?)\s*\bfrom\s+["'](.*?)["'](?:\s*;)?(?:\s*//.*)?$""")

    let IMPORT_SELECTOR_REGEX = Regex(@"^(\*|\w+)(?:\s+as\s+(\w+))?$")

    let stripImports (com: IBabelCompiler) ctx r (str: string) =
        str.Split('\n')
        |> Array.skipWhile (fun line ->
            match line.Trim() with
            | s when System.String.IsNullOrEmpty(s) -> true
            | Naming.Regex IMPORT_REGEX (_ :: selector :: path :: _) ->
                if selector.StartsWith("{", StringComparison.Ordinal) then
                    for selector in selector.TrimStart('{').TrimEnd('}').Split(',') do
                        com.GetImportExpr(ctx, selector, path, r, noMangle = true) |> ignore

                    true
                else
                    let selector =
                        if selector.StartsWith("*", StringComparison.Ordinal) then
                            selector
                        else
                            $"default as %s{selector}"

                    com.GetImportExpr(ctx, selector, path, r, noMangle = true) |> ignore

                    true
            | _ -> false
        )
        |> String.concat "\n"

    let (|TransformExpr|) (com: IBabelCompiler) ctx e = com.TransformAsExpr(ctx, e)

    [<return: Struct>]
    let (|Function|_|) =
        function
        | Fable.Lambda(arg, body, _) -> ValueSome([ arg ], body)
        | Fable.Delegate(args, body, _, []) -> ValueSome(args, body)
        | _ -> ValueNone

    [<return: Struct>]
    let (|Lets|_|) =
        function
        | Fable.Let(ident, value, body) -> ValueSome([ ident, value ], body)
        | Fable.LetRec(bindings, body) -> ValueSome(bindings, body)
        | _ -> ValueNone

    let getUniqueNameInRootScope (ctx: Context) name =
        let name =
            (name, Naming.NoMemberPart)
            ||> Naming.sanitizeJsIdent (fun name ->
                ctx.UsedNames.RootScope.Contains(name)
                || ctx.UsedNames.DeclarationScopes.Contains(name)
            )

        ctx.UsedNames.RootScope.Add(name) |> ignore
        name

    let getUniqueNameInDeclarationScope (ctx: Context) name =
        let name =
            (name, Naming.NoMemberPart)
            ||> Naming.sanitizeJsIdent (fun name ->
                ctx.UsedNames.RootScope.Contains(name)
                || ctx.UsedNames.CurrentDeclarationScope.Contains(name)
            )

        ctx.UsedNames.CurrentDeclarationScope.Add(name) |> ignore
        name

    type NamedTailCallOpportunity(_com: Compiler, ctx, name, args: Fable.Ident list) =
        // Capture the current argument values to prevent delayed references from getting corrupted,
        // for that we use block-scoped ES2015 variable declarations. See #681, #1859
        let argIds =
            args
            |> FSharp2Fable.Util.discardUnitArg
            |> List.map (fun arg -> getUniqueNameInDeclarationScope ctx (arg.Name + "_mut"))

        interface ITailCallOpportunity with
            member _.Label = name
            member _.Args = argIds

            member _.IsRecursiveRef(e) =
                match e with
                | Fable.IdentExpr id -> name = id.Name
                | _ -> false

    let getDecisionTarget (ctx: Context) targetIndex =
        match List.tryItem targetIndex ctx.DecisionTargets with
        | None -> failwith $"Cannot find DecisionTree target %i{targetIndex}"
        | Some(idents, target) -> idents, target

    let rec isJsStatement ctx preferStatement (expr: Fable.Expr) =
        match expr with
        | Fable.Unresolved _
        | Fable.Value _
        | Fable.Import _
        | Fable.IdentExpr _
        | Fable.Lambda _
        | Fable.Delegate _
        | Fable.ObjectExpr _
        | Fable.Call _
        | Fable.CurriedApply _
        | Fable.Operation _
        | Fable.Get _
        | Fable.Quote _
        | Fable.Test _ -> false

        | Fable.TypeCast(e, _) -> isJsStatement ctx preferStatement e

        | Fable.TryCatch _
        | Fable.Sequential _
        | Fable.Let _
        | Fable.LetRec _
        | Fable.Set _
        | Fable.ForLoop _
        | Fable.WhileLoop _ -> true

        | Fable.Extended(kind, _) ->
            match kind with
            | Fable.Throw _
            | Fable.Debugger -> true
            | Fable.Curry _ -> false

        // TODO: If IsJsSatement is false, still try to infer it? See #2414
        // /^\s*(break|continue|debugger|while|for|switch|if|try|let|const|var)\b/
        | Fable.Emit(i, _, _) -> i.IsStatement

        | Fable.DecisionTreeSuccess(targetIndex, _, _) ->
            getDecisionTarget ctx targetIndex |> snd |> isJsStatement ctx preferStatement

        // Make it also statement if we have more than, say, 3 targets?
        // That would increase the chances to convert it into a switch
        | Fable.DecisionTree(_, targets) -> preferStatement || List.exists (snd >> (isJsStatement ctx false)) targets

        | Fable.IfThenElse(_, thenExpr, elseExpr, _) ->
            preferStatement
            || isJsStatement ctx false thenExpr
            || isJsStatement ctx false elseExpr

    let addErrorAndReturnNull (com: Compiler) (range: SourceLocation option) (error: string) =
        addError com [] range error
        Expression.nullLiteral ()

    let identAsIdent (id: Fable.Ident) =
        Identifier.identifier (id.Name, ?loc = id.Range)

    let identAsExpr (id: Fable.Ident) =
        Expression.identifier (id.Name, ?loc = id.Range)

    let thisExpr = Expression.thisExpression ()

    let ofInt i = Expression.numericLiteral (float i)

    let ofString s = Expression.stringLiteral (s)

    let memberFromNameComputeStrings computeStrings (memberName: string) : Expression * bool =
        match memberName with
        | "ToString" -> Expression.identifier ("toString"), false
        | n when n.StartsWith("Symbol.", StringComparison.Ordinal) ->
            Expression.memberExpression (Expression.identifier ("Symbol"), Expression.identifier (n[7..]), false), true
        | n when Naming.hasIdentForbiddenChars Naming.isJsIdentChar n -> Expression.stringLiteral (n), computeStrings
        | n -> Expression.identifier (n |> sanitizeMemberName), false

    let memberFromName (memberName: string) : Expression * bool =
        memberFromNameComputeStrings false memberName

    let get r left memberName =
        let expr, isComputed = memberFromNameComputeStrings true memberName
        Expression.memberExpression (left, expr, isComputed, ?loc = r)

    let getExpr r (object: Expression) (expr: Expression) =
        let expr, isComputed =
            match expr with
            | Literal(Literal.StringLiteral(StringLiteral(value, _))) -> memberFromNameComputeStrings true value
            | e -> e, true

        Expression.memberExpression (object, expr, isComputed, ?loc = r)

    let rec getParts (parts: string list) (expr: Expression) =
        match parts with
        | [] -> expr
        | m :: ms -> get None expr m |> getParts ms

    // Use non-strict equality for null checks
    let makeNullCheck r isNull e =
        let op =
            if isNull then
                "=="
            else
                "!="

        BinaryExpression(e, Expression.nullLiteral (), op, r)

    let makeArray (com: IBabelCompiler) ctx exprs =
        List.mapToArray (fun e -> com.TransformAsExpr(ctx, e)) exprs
        |> Expression.arrayExpression

    let makeTypedArray (com: IBabelCompiler) ctx t kind (args: Fable.Expr list) =
        match t with
        | JS.Replacements.TypedArrayCompatible com kind jsName ->
            let args = [| makeArray com ctx args |]
            Expression.newExpression (Expression.identifier (jsName), args)
        | _ -> makeArray com ctx args

    let getArrayCons com t kind =
        match t with
        | JS.Replacements.TypedArrayCompatible com kind name -> Expression.identifier name
        | _ -> Expression.identifier ("Array")

    let makeArrayAllocated (com: IBabelCompiler) ctx typ kind (size: Fable.Expr) =
        let cons = getArrayCons com typ kind
        let size = com.TransformAsExpr(ctx, size)
        Expression.newExpression (cons, [| size |])

    let makeArrayFrom (com: IBabelCompiler) ctx typ kind (fableExpr: Fable.Expr) =
        match fableExpr with
        | Replacements.Util.ArrayOrListLiteral(exprs, _) -> makeTypedArray com ctx typ kind exprs
        | _ ->
            let cons = getArrayCons com typ kind
            let expr = com.TransformAsExpr(ctx, fableExpr)
            Expression.callExpression (get None cons "from", [| expr |])

    let makeStringArray strings =
        strings
        |> List.mapToArray (fun x -> Expression.stringLiteral (x))
        |> Expression.arrayExpression

    let makeJsObject pairs =
        pairs
        |> Seq.map (fun (name, value) ->
            let prop, isComputed = memberFromName name
            ObjectMember.objectProperty (prop, value, isComputed = isComputed)
        )
        |> Seq.toArray
        |> Expression.objectExpression

    /// <summary>Identifies constant undefined option-bag values whose evaluation has no effects.</summary>
    let rec isUndefinedPropertyValue com body value =
        match value with
        | Undefined _ -> true
        | AsExpression(value, _) -> isUndefinedPropertyValue com body value
        | UnaryExpression(_, "void", false, _) -> not (canHaveSideEffects com body)
        | _ -> false

    let assign range left right =
        Expression.assignmentExpression (AssignEqual, left, right, ?loc = range)

    let rec private isPromiseLike =
        function
        | Fable.DeclaredType(ent, _) ->
            ent.FullName = "Fable.Core.JS.Promise`1"
            || ent.FullName = "System.Threading.Tasks.Task"
            || ent.FullName = "System.Threading.Tasks.Task`1"
            || ent.FullName = "System.Threading.Tasks.ValueTask"
        | Fable.GenericParam(_, _, constraints) ->
            constraints
            |> List.exists (
                function
                | Fable.Constraint.CoercesTo typ -> isPromiseLike typ
                | _ -> false
            )
        | _ -> false

    let private validateAsyncGeneratorPayload (com: IBabelCompiler) range generatorType =
        match generatorType with
        | Fable.DeclaredType(ent, [ yieldType; returnType; _ ]) when
            ent.FullName = "Fickl.Core.JsAsyncGenerator`3"
            && (isPromiseLike yieldType || isPromiseLike returnType)
            ->
            addError
                com
                []
                range
                "Async generator yield and terminal types must be resolved values; await promises with let! before yielding or returning"
        | _ -> ()

    let private hasNativeAsyncPromisePayload =
        function
        | Fable.DeclaredType(ent, [ payload ]) -> ent.FullName = "Fable.Core.JS.Promise`1" && isPromiseLike payload
        | _ -> false

    let private validateNativeAsyncPayload (com: IBabelCompiler) range typ =
        match typ with
        | _ when hasNativeAsyncPromisePayload typ ->
            addError
                com
                []
                range
                "Native async computations must return resolved values; await promises with let! or return! before returning"
        | _ -> ()

    /// <summary>Checks that a generator with a non-unit terminal type cannot fall through without returning.</summary>
    let validateGeneratorReturn (com: IBabelCompiler) generatorType (body: Fable.Expr) =
        validateAsyncGeneratorPayload com body.Range generatorType

        let rec finishes targets visited =
            function
            | Fable.Emit(info, _, _) when List.contains "fickl-generator-return" info.CallInfo.Tags -> true
            | Fable.Extended(Fable.Throw _, _) -> true
            | Fable.Sequential expressions -> List.exists (finishes targets visited) expressions
            | Fable.Let(_, _, body)
            | Fable.TypeCast(body, _) -> finishes targets visited body
            | Fable.IfThenElse(_, first, second, _) -> finishes targets visited first && finishes targets visited second
            | Fable.TryCatch(body, handler, finalizer, _) ->
                Option.exists (finishes targets visited) finalizer
                || (finishes targets visited body
                    && (handler |> Option.forall (snd >> finishes targets visited)))
            | Fable.DecisionTree(tree, targets) -> finishes targets Set.empty tree
            | Fable.DecisionTreeSuccess(index, _, _) when not (Set.contains index visited) ->
                targets
                |> List.tryItem index
                |> Option.exists (snd >> finishes targets (Set.add index visited))
            | Fable.WhileLoop(Fable.Value(Fable.BoolConstant true, _), _, _) -> true
            | _ -> false

        match generatorType with
        | Fable.DeclaredType(_, [ _; (Fable.Unit | Fable.Any); _ ]) -> ()
        | _ when finishes [] Set.empty body -> ()
        | _ ->
            addError com [] body.Range "A jsGenerator with a non-unit terminal type must return a value on every path"

    /// <summary>Whether a generator invocation captures the containing function's receiver.</summary>
    let rec usesGeneratorReceiver =
        function
        | Fable.Call(_, info, _, _) when List.contains "fickl-generator-run" info.Tags -> true
        | Fable.Delegate(_, _, _, tags) when
            List.contains "not-arrow" tags
            && not (List.contains "fickl-async-function" tags)
            ->
            false
        | Fable.ObjectExpr _ -> false
        | expression -> getSubExpressions expression |> List.exists usesGeneratorReceiver

    /// Immediately Invoked Function Expression
    let iife (com: IBabelCompiler) ctx (expr: Fable.Expr) =
        let rec containsAwait =
            function
            | Fable.Emit(info, _, _) when List.contains "fickl-native-await" info.CallInfo.Tags -> true
            | Fable.Lambda _
            | Fable.Delegate _
            | Fable.ObjectExpr _ -> false
            | expression -> getSubExpressions expression |> List.exists containsAwait


        let rec containsYield =
            function
            | Fable.Emit(info, _, _) when List.contains "fickl-native-yield" info.CallInfo.Tags -> true
            | Fable.Lambda _
            | Fable.Delegate _
            | Fable.ObjectExpr _ -> false
            | expression -> getSubExpressions expression |> List.exists containsYield

        let isGenerator = ctx.NativeGeneratorType.IsSome && containsYield expr
        let isAsync = ctx.IsNativeAsync && (isGenerator || containsAwait expr)

        let _, body =
            com.TransformFunction(
                { ctx with
                    IsNativeAsync = isAsync
                    NativeGeneratorType =
                        if isGenerator then
                            ctx.NativeGeneratorType
                        else
                            None
                },
                None,
                [],
                expr
            )

        if isGenerator then
            let returnType =
                ctx.NativeGeneratorType
                |> Option.map (
                    function
                    | Fable.DeclaredType(ent, [ yieldType; _; nextType ]) ->
                        Fable.DeclaredType(ent, [ yieldType; expr.Type; nextType ])
                    | typ -> typ
                )
                |> Option.bind (fun typ ->
                    if com.IsTypeScript then
                        Some(makeTypeAnnotation com ctx typ)
                    else
                        None
                )

            let parameters =
                if com.IsTypeScript then
                    [| Parameter.parameter ("this", typeAnnotation = AnyTypeAnnotation) |]
                else
                    [||]

            let callback =
                Expression.generatorFunctionExpression (parameters, body, ?returnType = returnType, isAsync = isAsync)

            let call =
                Expression.callExpression (
                    Expression.memberExpression (callback, Expression.identifier "call"),
                    [| Expression.thisExpression () |]
                )

            emitExpression None "(yield* $0)" [ call ]
        elif isAsync then
            let returnType =
                if com.IsTypeScript then
                    Some(makeNativeTypeAnnotation com ctx [ expr.Type ] "Promise")
                else
                    None

            let callback =
                Expression.asyncFunctionExpression ([||], body, ?returnType = returnType, isArrow = true)

            let call = Expression.callExpression (callback, [||])

            emitExpression None "(await $0)" [ call ]
        else
            Expression.callExpression (Expression.arrowFunctionExpression ([||], body), [||])

    let multiVarDeclaration (com: IBabelCompiler) ctx kind (variables: (Fable.Ident * Expression option) seq) =
        let varDeclarators =
            // TODO: Log error if there're duplicated non-empty var declarations
            variables
            |> Seq.distinctBy (fun (id, _) -> id.Name)
            |> Seq.map (fun (id, value) ->
                let idType = Replacements.Util.getIdentValueType com id id.Type
                let ta, tp = makeTypeAnnotationWithParametersIfTypeScript com ctx idType value

                let value =
                    if com.IsTypeScript && Option.isNone value then
                        // initialize un-initialized variables to "undefined as any"
                        let expr = Expression.Undefined(?loc = None)
                        let ta = makeTypeAnnotation com ctx Fable.Type.Any
                        AsExpression(expr, ta) |> Some
                    else
                        value

                VariableDeclarator.variableDeclarator (
                    id.Name,
                    ?annotation = ta,
                    typeParameters = tp,
                    ?init = value,
                    ?loc = id.Range
                )
            )
            |> Seq.toArray

        Statement.variableDeclaration (kind, varDeclarators)

    let callSuper (args: Expression list) =
        Expression.callExpression (Super(None), List.toArray args)

    let callSuperAsStatement (args: Expression list) = ExpressionStatement(callSuper args)

    let callFunction com ctx r funcExpr genArgs (args: Expression list) =
        let genArgs = makeTypeParamInstantiationIfTypeScript com ctx genArgs

        Expression.callExpression (funcExpr, List.toArray args, ?typeArguments = genArgs, ?loc = r)

    let callFunctionWithThisContext r funcExpr (args: Expression list) =
        let args = thisExpr :: args |> List.toArray
        Expression.callExpression (get None funcExpr "call", args, ?loc = r)

    let emitExpression range (txt: string) args =
        EmitExpression(txt, List.toArray args, ?loc = range)

    let undefined range eOpt =
        match eOpt with
        | None -> Expression.Undefined(?loc = range)
        | Some e ->
            // let e = defaultArg eOpt (Expression.numericLiteral (0.))
            Expression.unaryExpression ("void", e, ?loc = range)

    let private getConstraintTypes (c: Fable.Constraint) =
        match c with
        | Fable.Constraint.CoercesTo target -> [ target ]
        | Fable.Constraint.IsDelegate(argsType, retType) -> [ argsType; retType ]
        | Fable.Constraint.IsEnum baseType -> [ baseType ]
        | Fable.Constraint.SimpleChoice types -> types
        | _ -> []

    let getTypeParameters (ctx: Context) (types: Fable.Type list) =
        // A type parameter can appear only in another one's constraint (e.g. `$b :> Lazy<$c>`) and still be referenced by the body.
        let rec collect state types =
            (state, FSharp2Fable.Util.getGenParamTypes types)
            ||> List.fold (fun (scopedTypeParams, typeParams) typ ->
                match typ with
                | Fable.GenericParam(name = name; constraints = constraints) when
                    not (Set.contains name scopedTypeParams)
                    ->
                    constraints
                    |> List.collect getConstraintTypes
                    |> collect (Set.add name scopedTypeParams, typ :: typeParams)
                | _ -> scopedTypeParams, typeParams
            )

        let scopedTypeParams, typeParams = collect (ctx.ScopedTypeParams, []) types

        scopedTypeParams, List.rev typeParams

    [<Struct>]
    type MemberKind =
        | ClassConstructor
        | NonAttached of funcName: string
        | Attached of isStatic: bool

    let getMemberArgsAndBodyWithReturnType
        (com: IBabelCompiler)
        ctx
        kind
        (entOpt: Fable.Entity option)
        (info: Fable.MemberFunctionOrValue)
        (args: Fable.Ident list)
        (returnType: Fable.Type)
        (body: Fable.Expr)
        =
        let isAsyncGenerator = hasAttribute Atts.ficklAsyncGeneratorFunction info.Attributes

        let isNativeAsync =
            isAsyncGenerator || hasAttribute Atts.ficklAsyncFunction info.Attributes

        if
            isNativeAsync
            && not (
                info.DeclaringEntity
                |> Option.bind com.TryGetEntity
                |> Option.exists _.IsFSharpModule
            )
        then
            addError
                com
                []
                body.Range
                (if isAsyncGenerator then
                     "AsyncGeneratorFunction is only supported on module functions"
                 else
                     "AsyncFunction is only supported on module functions")

        let isGenerator =
            isAsyncGenerator || hasAttribute Atts.ficklGeneratorFunction info.Attributes

        if
            isGenerator
            && not isAsyncGenerator
            && not (
                info.DeclaringEntity
                |> Option.bind com.TryGetEntity
                |> Option.exists _.IsFSharpModule
            )
        then
            addError com [] body.Range "GeneratorFunction is only supported on module functions"

        if isNativeAsync && isGenerator && not isAsyncGenerator then
            addError com [] body.Range "AsyncFunction and GeneratorFunction cannot be combined"

        let ctx =
            { ctx with
                IsNativeAsync = isNativeAsync
                NativeGeneratorType =
                    if isGenerator then
                        Some info.ReturnParameter.Type
                    else
                        None
            }

        let funcName, args, body =
            match kind, args with
            | Attached(isStatic = false), (thisArg :: args) ->
                let body =
                    // TODO: If ident is not captured maybe we can just replace it with "this"
                    if isIdentUsed thisArg.Name body then
                        let thisArgType =
                            match thisArg.Type with
                            | Replacements.Util.IsByRefType com typ -> typ
                            | typ -> typ

                        let thisArg = { thisArg with Type = thisArgType }
                        let thisIdent = Fable.IdentExpr { thisArg with Name = "this" }

                        let thisIdent =
                            if com.IsTypeScript then
                                match entOpt with
                                | Some ent when ent.IsFSharpUnion && List.isMultiple ent.UnionCases ->
                                    Replacements.Util.Helper.LibCall(
                                        com,
                                        "Util",
                                        "downcast",
                                        thisArg.Type,
                                        [ thisIdent ]
                                    )
                                    |> Replacements.Util.withTag "downcast"
                                | _ -> thisIdent
                            else
                                thisIdent

                        Fable.Let(thisArg, thisIdent, body)
                    else
                        body

                None, args, body
            | Attached(isStatic = true), _
            | Attached _, _ -> None, args, body
            | ClassConstructor, _ -> None, args, body
            | NonAttached funcName, _ -> Some funcName, args, body

        let ctx, typeParams =
            if com.IsTypeScript then
                let isAttached, entGenParams =
                    match entOpt with
                    | None ->
                        match info.DeclaringEntity with
                        | Some entRef ->
                            let ent = com.GetEntity(entRef)

                            if ent.IsFSharpModule then
                                false, []
                            else
                                false, ent.GenericParameters
                        | None -> false, []
                    | Some ent -> true, ent.GenericParameters

                let scopedTypeParams =
                    List.append entGenParams info.GenericParameters
                    |> List.map (fun g -> g.Name)
                    |> set
                    // Keep type parameters already in scope (e.g. from an enclosing generic
                    // function) so nested members/lambdas don't redeclare and shadow them.
                    |> Set.union ctx.ScopedTypeParams

                let declaredTypeParams =
                    if isAttached && kind <> Attached(isStatic = true) then
                        // Instance attached methods inherit class type params; only declare method-specific ones
                        info.GenericParameters
                        |> List.filter (fun g -> entGenParams |> List.forall (fun e -> e.Name <> g.Name))
                    else
                        entGenParams @ info.GenericParameters
                    |> List.map (fun g -> Fable.GenericParam(g.Name, g.IsMeasure, g.Constraints))
                    |> Some

                { ctx with ScopedTypeParams = scopedTypeParams }, declaredTypeParams
            else
                ctx, None

        let parameters = List.concat info.CurriedParameterGroups
        let mutable strictOptionalBindings = []

        let argsWithParams (args: Fable.Ident list) =
            List.zip args parameters
            |> List.map (fun (a, p) ->
                match p.Name with
                | Some name when p.IsNamed && a.Name <> name ->
                    $"Argument {name} is marked as named but conflicts with another name in scope"
                    |> addWarning com [] a.Range
                | _ -> ()

                let rawArg =
                    if p.IsOptional then
                        { a with Type = unwrapOptionalType a.Type }
                    else
                        a

                match a.Type with
                | Fable.Option(t, isStruct) when
                    p.IsOptional && isStrictOption com isStruct t && not (mustWrapOption t)
                    ->
                    let optionIdent =
                        { a with Name = getUniqueNameInDeclarationScope ctx (a.Name + "_option") }

                    let rawValue = Fable.IdentExpr rawArg
                    let missing = emitExpr a.Range Fable.Boolean [ rawValue ] "$0 === undefined"
                    let none = Fable.Value(Fable.NewOption(None, t, isStruct), a.Range)
                    let some = Fable.Value(Fable.NewOption(Some rawValue, t, isStruct), a.Range)

                    strictOptionalBindings <-
                        (a.Name, optionIdent, Fable.IfThenElse(missing, none, some, a.Range))
                        :: strictOptionalBindings
                | _ -> ()

                rawArg,
                ParameterFlags(
                    isNamed = p.IsNamed,
                    isOptional = (p.IsOptional && com.IsTypeScript),
                    ?defVal = (p.DefaultValue |> Option.map (transformAsExpr com ctx))
                )
            )

        let argsWithFlags =
            match args with
            | [] -> []

            | args when info.HasSpread ->
                let args, lastArg = List.splitLast args
                let args = args |> List.map (fun a -> a, ParameterFlags())

                let lastArg =
                    match lastArg.Type with
                    | Fable.Array(genArg, Fable.MutableArray) ->
                        // modify the last argument's type if it's a spread parameter
                        { lastArg with Type = Fable.Array(genArg, Fable.ResizeArray) }
                    | _ -> lastArg

                args @ [ lastArg, ParameterFlags(isSpread = true) ]

            | thisArg :: args when info.IsInstance && List.sameLength args parameters ->
                (thisArg, ParameterFlags()) :: (argsWithParams args)
            | args when List.sameLength args parameters -> argsWithParams args
            | _ -> args |> List.map (fun a -> a, ParameterFlags())

        let body =
            let replacements =
                strictOptionalBindings
                |> List.map (fun (name, ident, _) -> name, Fable.IdentExpr ident)
                |> Map.ofList

            let body = FableTransforms.replaceValues replacements body

            (List.rev strictOptionalBindings, body)
            ||> List.foldBack (fun (_, ident, value) body -> Fable.Let(ident, value, body))

        let args, body, returnType, typeParamDecl =
            transformFunctionWithReturnType com ctx funcName typeParams (List.map fst argsWithFlags) returnType body

        let args =
            if Array.isEmpty args then
                args
            else
                Seq.zip args argsWithFlags
                |> Seq.mapToArray (fun (a, (_, flags)) -> a.WithFlags(flags))

        let returnType =
            match FSharp2Fable.TypeGuards.tryGetFromInfo com info with
            | Some guard when com.IsTypeScript ->
                let index = guard.ParameterIndex + max 0 (args.Length - parameters.Length)

                match Array.tryItem index args with
                | Some(Parameter.Parameter(name, _, _)) ->
                    let guardType = makeTypeAnnotation com ctx guard.GuardType
                    TypePredicateTypeAnnotation(name, Some guardType, guard.Asserts) |> Some
                | None -> returnType
            | _ -> returnType

        args, body, returnType, typeParamDecl

    let getMemberArgsAndBody
        (com: IBabelCompiler)
        ctx
        kind
        (entOpt: Fable.Entity option)
        (info: Fable.MemberFunctionOrValue)
        (args: Fable.Ident list)
        (body: Fable.Expr)
        =
        getMemberArgsAndBodyWithReturnType com ctx kind entOpt info args body.Type body

    let getUnionCaseName (uci: Fable.UnionCase) =
        match uci.CompiledName with
        | Some cname -> cname
        | None -> uci.Name

    let getUnionExprTag (com: IBabelCompiler) ctx r (fableExpr: Fable.Expr) =
        let expr = com.TransformAsExpr(ctx, fableExpr)
        getExpr r expr (Expression.stringLiteral ("tag"))

    /// Wrap int expressions with `| 0` to help optimization of JS VMs
    let wrapIntExpression typ (e: Expression) =
        match e, typ with
        | Literal(NumericLiteral(_)), _ -> e
        // TODO: Unsigned ints seem to cause problems, should we check only Int32 here?
        | _, Fable.Number((Int8 | Int16 | Int32), Fable.NumberInfo.Empty) ->
            Expression.binaryExpression (BinaryOrBitwise, e, Expression.numericLiteral (0.))
        | _ -> e

    let wrapExprInBlockWithReturn e =
        BlockStatement([| Statement.returnStatement (e) |])

    let makeArrowFunctionExpression _name (args, (body: BlockStatement), returnType, typeParamDecl) : Expression =
        Expression.arrowFunctionExpression (args, body, ?returnType = returnType, ?typeParameters = typeParamDecl)

    let makeFunctionExpression name (args, (body: Expression), returnType, typeParamDecl) : Expression =
        let id = name |> Option.map Identifier.identifier
        let body = wrapExprInBlockWithReturn body

        Expression.functionExpression (args, body, ?id = id, ?returnType = returnType, ?typeParameters = typeParamDecl)

    let optimizeTailCall (com: IBabelCompiler) (ctx: Context) range (tc: ITailCallOpportunity) args =
        let rec checkCrossRefs tempVars allArgs =
            function
            | [] -> tempVars
            | (argId, _arg) :: rest ->
                let found =
                    allArgs
                    |> List.exists (
                        deepExists (
                            function
                            | Fable.IdentExpr i -> argId = i.Name
                            | _ -> false
                        )
                    )

                let tempVars =
                    if found then
                        let tempVarName = getUniqueNameInDeclarationScope ctx (argId + "_tmp")

                        Map.add argId tempVarName tempVars
                    else
                        tempVars

                checkCrossRefs tempVars allArgs rest

        ctx.OptimizeTailCall()
        let zippedArgs = List.zip tc.Args args
        let tempVars = checkCrossRefs Map.empty args zippedArgs

        let tempVarReplacements = tempVars |> Map.map (fun _ v -> makeIdentExpr v)

        [|
            // First declare temp variables
            for (KeyValue(argId, tempVar)) in tempVars do
                yield Statement.variableDeclaration (Const, tempVar, init = Expression.identifier (argId))
            // Then assign argument expressions to the original argument identifiers
            // See https://github.com/fable-compiler/Fable/issues/1368#issuecomment-434142713
            for (argId, arg) in zippedArgs do
                let arg = FableTransforms.replaceValues tempVarReplacements arg
                let arg = com.TransformAsExpr(ctx, arg)

                yield assign None (Expression.identifier (argId)) arg |> ExpressionStatement
            yield Statement.continueStatement (Identifier.identifier (tc.Label), ?loc = range)
        |]

    let transformImport (com: IBabelCompiler) ctx r (selector: string) (path: string) =
        let selector, parts =
            let parts = Array.toList (selector.Split('.'))
            parts.Head, parts.Tail

        com.GetImportExpr(ctx, selector, path, r) |> getParts parts

    let transformCast (com: IBabelCompiler) (ctx: Context) typ e : Expression =
        match typ with
        // fickl: TypeScript rejects a number literal outside a `[<TsEnum>]` enum, `enum 5`, unless it is widened
        | Fable.Number(_, Fable.NumberInfo.IsEnum entRef) when com.IsTypeScript ->
            match com.TransformAsExpr(ctx, e), Annotation.tryTsEnumTypeAnnotation com ctx entRef with
            | Literal _ as literal, Some ta -> AsExpression(AsExpression(literal, NumberTypeAnnotation), ta)
            | jsExpr, _ -> jsExpr
        // Optimization for (numeric) array or list literals casted to seq
        // Done at the very end of the compile pipeline to get more opportunities
        // of matching cast and literal expressions after resolving pipes, inlining...
        | Fable.DeclaredType(entRef, _) ->
            match entRef.FullName with
            | Types.ienumerableGeneric
            | Types.ienumerable ->
                match e with
                | ExprType Fable.String ->
                    // Convert to array to get 16-bit code units, see #1279
                    let e = JS.Replacements.stringToCharArray e
                    com.TransformAsExpr(ctx, e) |> Some
                | Replacements.Util.ArrayOrListLiteral(exprs, _) -> makeArray com ctx exprs |> Some
                | _ -> None
            | _ -> None
            |> Option.defaultWith (fun () ->
                let jsExpr = com.TransformAsExpr(ctx, e)

                // The interface value of an inlined case is not assignable to the literal tag type,
                // and a constructed case would otherwise narrow the declared union on assignment
                let toTaggedUnion =
                    com.IsTypeScript
                    && com.TryGetEntity(entRef)
                       |> Option.exists (fun ent ->
                           hasAttribute Atts.tsTaggedUnion ent.Attributes
                           // fickl: an EmitType annotation is usually narrower than the value cast to it
                           || (hasAttribute Atts.ficklEmitType ent.Attributes
                               && (
                                   match e.Type with
                                   | Fable.DeclaredType(source, _) -> source.FullName <> entRef.FullName
                                   | _ -> true
                               ))
                       )

                match e.Type with
                | Fable.DeclaredType(sourceEntRef, _) when com.IsTypeScript ->
                    let sourceEnt = com.GetEntity(sourceEntRef)
                    // Because we use a wrapper type for multi-case unions, TypeScript
                    // won't automatically cast them to implementing interfaces
                    let fromUnion = sourceEnt.IsFSharpUnion && List.isMultiple sourceEnt.UnionCases

                    // fickl: TypeScript cannot relate an EmitType annotation to the F# type it is cast to
                    let fromEmitType =
                        sourceEntRef.FullName <> entRef.FullName
                        && hasAttribute Atts.ficklEmitType sourceEnt.Attributes

                    if fromUnion || toTaggedUnion || fromEmitType then
                        AsExpression(jsExpr, makeTypeAnnotation com ctx typ)
                    else
                        jsExpr
                // fickl: an F# array may be a typed array, which TypeScript does not relate to an EmitType such as ReadonlyArray
                | Fable.Array _ when toTaggedUnion ->
                    AsExpression(AsExpression(jsExpr, UnknownTypeAnnotation), makeTypeAnnotation com ctx typ)
                | _ when toTaggedUnion -> AsExpression(jsExpr, makeTypeAnnotation com ctx typ)
                | _ -> jsExpr
            )
        | Fable.Unit -> com.TransformAsExpr(ctx, e) |> Some |> undefined e.Range
        | _ -> com.TransformAsExpr(ctx, e)

    let transformCurry (com: IBabelCompiler) (ctx: Context) expr arity : Expression =
        com.TransformAsExpr(ctx, Replacements.Api.curryExprAtRuntime com arity expr)

    let transformNewUnion (com: IBabelCompiler) (ctx: Context) r (ent: Fable.Entity) genArgs (tag: int) values =
        let values = values |> List.mapToArray (transformAsExpr com ctx)

        // A case with no fields is exposed as a static singleton (see makeUnionCaseSingletonMember),
        // so use that instead of allocating a new instance on every reference.
        let singletonCase =
            List.tryItem tag ent.UnionCases
            |> Option.filter (fun case -> List.isEmpty case.UnionCaseFields)

        match singletonCase with
        | Some case -> get r (jsConstructor com ctx ent) (sanitizeName case.Name)
        | None ->
            if List.isSingle ent.UnionCases then
                let typeParamInst = makeTypeParamInstantiationIfTypeScript com ctx genArgs

                Expression.newExpression (jsConstructor com ctx ent, values, ?typeArguments = typeParamInst, ?loc = r)
            else
                let callConstructor (case: Fable.UnionCase option) =
                    let tagExpr =
                        match case with
                        | Some case -> CommentedExpression(case.Name, ofInt tag)
                        | None -> ofInt tag

                    let consRef = jsConstructor com ctx ent

                    let typeParamInst =
                        makeTypeParamInstantiationIfTypeScript com ctx genArgs
                        |> Option.map (fun typeParams ->
                            Array.append typeParams [| LiteralTypeAnnotation(Literal.numericLiteral (tag)) |]
                        )

                    Expression.newExpression (
                        consRef,
                        [| tagExpr; Expression.arrayExpression values |],
                        ?typeArguments = typeParamInst,
                        ?loc = r
                    )

                if com.IsTypeScript then
                    match List.tryItem tag ent.UnionCases with
                    | Some case ->
                        match tryJsConstructorWithSuffix com ctx ent ("_" + sanitizeName case.Name) with
                        | Some helperRef ->
                            let typeParams = makeTypeParamInstantiation com ctx genArgs

                            Expression.callExpression (helperRef, values, typeArguments = typeParams)
                        | None -> callConstructor (Some case)
                    | None ->
                        $"Unmatched union case tag: %d{tag} for %s{ent.FullName}" |> addWarning com [] r

                        callConstructor None
                else
                    callConstructor (List.tryItem tag ent.UnionCases)

    /// fickl: under --strictOptions an option, not a value option, of a type that can be null is built with
    /// `some` and read with `value`, so `Some null` stays apart from `None` as on .NET. Value options stay erased.
    let isStrictOption (com: IBabelCompiler) isStruct (t: Fable.Type) =
        let canBeNull =
            match t with
            | Fable.String
            | Fable.Array _
            | Fable.List _
            | Fable.LambdaType _
            | Fable.DelegateType _
            | Fable.Regex
            | Fable.MetaType
            | Fable.Nullable _ -> true
            | Fable.Tuple(_, isStruct)
            | Fable.AnonymousRecordType(_, _, isStruct) -> not isStruct
            | Fable.DeclaredType(entRef, _) -> not (com.GetEntity(entRef).IsValueType)
            | _ -> false

        not isStruct
        && canBeNull
        && List.contains Fickl.Options.strictOptionsDefine com.Options.Define

    /// fickl: values that are never null, so an option of them needs no wrapping even under --strictOptions
    let isNeverNull (expr: Fable.Expr) =
        match expr with
        | Fable.Value((Fable.StringConstant _ | Fable.StringTemplate _ | Fable.RegexConstant _ | Fable.TypeInfo _ | Fable.NewArray _ | Fable.NewList _ | Fable.NewTuple _ | Fable.NewRecord _ | Fable.NewAnonymousRecord _ | Fable.NewUnion _),
                      _)
        | Fable.Lambda _
        | Fable.Delegate _
        | Fable.ObjectExpr _ -> true
        | _ -> false

    let transformValue (com: IBabelCompiler) (ctx: Context) r value : Expression =
        match value with
        | Fable.BaseValue(None, _) -> Super(None)
        | Fable.BaseValue(Some boundIdent, _) -> identAsExpr boundIdent
        | Fable.ThisValue _ -> Expression.thisExpression ()
        | Fable.TypeInfo(t, tags) ->
            if com.Options.NoReflection then
                addErrorAndReturnNull com r "Reflection is disabled"
            else
                let genMap =
                    if List.contains "allow-generics" tags then
                        None
                    else
                        Some Map.empty

                transformTypeInfoFor ActualConsRef com ctx r genMap t
        | Fable.Null _t ->
            // if com.IsTypeScript
            //     let ta = makeTypeAnnotation com ctx t |> TypeAnnotation |> Some
            //     upcast Identifier("null", ?typeAnnotation=ta, ?loc=r)
            // else
            Expression.nullLiteral (?loc = r)
        | Fable.UnitConstant -> undefined r None
        | Fable.BoolConstant x -> Expression.booleanLiteral (x, ?loc = r)
        | Fable.CharConstant x -> Expression.stringLiteral (string<char> x, ?loc = r)
        | Fable.StringConstant x -> Expression.stringLiteral (x, ?loc = r)
        | Fable.StringTemplate(tag, parts, values) ->
            let values =
                match tag with
                | Some _ -> values
                // An interpolated float is written as Double.ToString() and Single.ToString() write it
                | None ->
                    values
                    |> List.map (fun value ->
                        match value with
                        // The compiler runs on .NET: format a constant here
                        | MaybeCasted(Fable.Value(Fable.NumberConstant(Fable.NumberValue.Float64 v, _), r)) ->
                            Fable.Value(
                                Fable.StringConstant(v.ToString(Globalization.CultureInfo.InvariantCulture)),
                                r
                            )
                        | MaybeCasted(Fable.Value(Fable.NumberConstant(Fable.NumberValue.Float32 v, _), r)) ->
                            Fable.Value(
                                Fable.StringConstant(v.ToString(Globalization.CultureInfo.InvariantCulture)),
                                r
                            )
                        | MaybeCasted(ExprType(Fable.Number(Float64, _)) as number) ->
                            Replacements.Util.Helper.LibCall(com, "Util", "doubleToString", Fable.String, [ number ])
                        | MaybeCasted(ExprType(Fable.Number(Float32, _)) as number) ->
                            Replacements.Util.Helper.LibCall(com, "Util", "singleToString", Fable.String, [ number ])
                        | _ -> value
                    )

            let tag = tag |> Option.map (fun e -> com.TransformAsExpr(ctx, e))

            let values = values |> List.mapToArray (fun e -> com.TransformAsExpr(ctx, e))

            StringTemplate(tag, List.toArray parts, values, r) |> Literal
        | Fable.NumberConstant(v, _) ->
            match v with
            | Fable.NumberValue.Int8 x -> Expression.numericLiteral (float x, ?loc = r)
            | Fable.NumberValue.UInt8 x -> Expression.numericLiteral (float x, ?loc = r)
            | Fable.NumberValue.Int16 x -> Expression.numericLiteral (float x, ?loc = r)
            | Fable.NumberValue.UInt16 x -> Expression.numericLiteral (float x, ?loc = r)
            | Fable.NumberValue.Int32 x -> Expression.numericLiteral (float x, ?loc = r)
            | Fable.NumberValue.UInt32 x -> Expression.numericLiteral (float x, ?loc = r)
            | Fable.NumberValue.Int64 x -> Expression.bigintLiteral (string<int64> x, ?loc = r)
            | Fable.NumberValue.UInt64 x -> Expression.bigintLiteral (string<uint64> x, ?loc = r)
            // | Fable.NumberValue.Int128(u,l) -> Expression.bigintLiteral(string System.Int128(u,l), ?loc=r)
            // | Fable.NumberValue.UInt128(u,l) -> Expression.bigintLiteral(string System.UInt128(u,l), ?loc=r)
            | Fable.NumberValue.BigInt x -> Expression.bigintLiteral (string<bigint> x, ?loc = r)
            | Fable.NumberValue.NativeInt x -> Expression.bigintLiteral (string<nativeint> x, ?loc = r)
            | Fable.NumberValue.UNativeInt x -> Expression.bigintLiteral (string<unativeint> x, ?loc = r)
            | Fable.NumberValue.Float16 x -> Expression.numericLiteral (float x, ?loc = r)
            | Fable.NumberValue.Float32 x -> Expression.numericLiteral (float x, ?loc = r)
            | Fable.NumberValue.Float64 x -> Expression.numericLiteral (float x, ?loc = r)
            | Fable.NumberValue.Decimal x -> JS.Replacements.makeDecimal com r value.Type x |> transformAsExpr com ctx
            | _ -> addErrorAndReturnNull com r $"Numeric literal is not supported: %A{v}"
        | Fable.RegexConstant(source, flags) -> Expression.regExpLiteral (source, flags, ?loc = r)
        | Fable.NewArray(newKind, typ, kind) ->
            match newKind with
            | Fable.ArrayValues values -> makeTypedArray com ctx typ kind values
            | Fable.ArrayAlloc size -> makeArrayAllocated com ctx typ kind size
            | Fable.ArrayFrom expr -> makeArrayFrom com ctx typ kind expr
        | Fable.NewTuple(vals, _) ->
            let tup = makeArray com ctx vals

            if com.IsTypeScript then
                AsExpression(tup, makeTypeAnnotation com ctx value.Type)
            else
                tup
        // | Fable.NewList (headAndTail, _) when List.contains "FABLE_LIBRARY" com.Options.Define ->
        //     makeList com ctx r headAndTail
        // Optimization for bundle size: compile list literals as List.ofArray
        | Fable.NewList(headAndTail, typ) ->
            let rec getItems acc =
                function
                | None -> List.rev acc, None
                | Some(head, Fable.Value(Fable.NewList(tail, _), _)) -> getItems (head :: acc) tail
                | Some(head, tail) -> List.rev (head :: acc), Some tail

            match getItems [] headAndTail with
            | [], None -> libCall com ctx r "List" "empty" [ typ ] []
            | [ TransformExpr com ctx expr ], None -> libCall com ctx r "List" "singleton" [] [ expr ]
            | exprs, None -> [ makeArray com ctx exprs ] |> libCall com ctx r "List" "ofArray" []
            | [ TransformExpr com ctx head ], Some(TransformExpr com ctx tail) ->
                libCall com ctx r "List" "cons" [] [ head; tail ]
            | exprs, Some(TransformExpr com ctx tail) ->
                [ makeArray com ctx exprs; tail ]
                |> libCall com ctx r "List" "ofArrayWithTail" []
        | Fable.NewOption(value, t, isStruct) ->
            match value with
            | Some value ->
                let e = com.TransformAsExpr(ctx, value)

                if mustWrapOption t || (isStrictOption com isStruct t && not (isNeverNull value)) then
                    libCall com ctx r "Option" "some" [] [ e ]
                else
                    e
            | None -> undefined r None
        | Fable.NewRecord(values, ent, genArgs) ->
            let ent = com.GetEntity(ent)

            let values = List.mapToArray (fun x -> com.TransformAsExpr(ctx, x)) values

            let consRef = ent |> jsConstructor com ctx

            let typeParamInst =
                if com.IsTypeScript && (ent.FullName = Types.refCell) then
                    makeTypeParamInstantiation com ctx genArgs |> Some
                else
                    None

            Expression.newExpression (consRef, values, ?typeArguments = typeParamInst, ?loc = r)
        | Fable.NewAnonymousRecord(values, fieldNames, _genArgs, _isStruct) ->
            let values =
                List.mapToArray (unwrapOptionalReturn com >> snd >> transformAsExpr com ctx) values

            Array.zip fieldNames values |> makeJsObject
        | Fable.NewUnion(values, tag, entRef, genArgs) ->
            let ent = com.GetEntity(entRef)
            transformNewUnion com ctx r ent genArgs tag values

    let enumerableThisToIterator com ctx =
        let enumerator =
            libCall com ctx None "Util" "getEnumerator" [] [ Expression.identifier ("this") ]

        BlockStatement(
            [|
                Statement.returnStatement (libCall com ctx None "Util" "toIterator" [] [ enumerator ])
            |]
        )

    /// <summary>Creates a native disposal method that forwards to the F# disposal member.</summary>
    let nativeDisposalBridge com ctx isAsync =
        let symbol, invocation, returnType =
            if isAsync then
                "asyncDisposeSymbol",
                "Promise.resolve(this.DisposeAsync()).then(() => {})",
                makeNativeTypeAnnotation com ctx [ Fable.Unit ] "Promise"
            else
                "disposeSymbol", "this.Dispose()", VoidTypeAnnotation

        let key = libValue com ctx "Util" symbol

        let body =
            BlockStatement [| emitExpression None invocation [] |> Statement.returnStatement |]

        key,
        body,
        (if com.IsTypeScript then
             Some returnType
         else
             None)

    let extractSuperClassFromBaseCall (com: IBabelCompiler) (ctx: Context) (baseType: Fable.Type option) baseCall =
        match baseCall, baseType with
        | Some(Fable.Call(baseRef, info, _, _)), _ ->
            let baseExpr =
                match com.Options.Language, baseType, baseRef with
                | TypeScript, Some typ, _ -> makeTypeAnnotation com ctx typ |> makeSuperType com
                | TypeScript, None, Fable.IdentExpr id -> makeTypeAnnotation com ctx id.Type |> makeSuperType com
                | _ -> transformAsExpr com ctx baseRef |> SuperExpression

            let args = transformCallArgs com ctx info

            Some(baseExpr, args)
        | Some(Fable.Value _), Some(Fable.DeclaredType(entRef, genArgs)) ->
            // let baseEnt = com.GetEntity(entRef)
            // let entityName = FSharp2Fable.Helpers.getEntityDeclarationName com entRef
            // let entityType = FSharp2Fable.Util.getEntityType baseEnt
            // let baseRefId = makeTypedIdent entityType entityName
            // let baseExpr = (baseRefId |> typedIdent com ctx) :> Expression
            // Some (baseExpr, []) // default base constructor
            let range = baseCall |> Option.bind (fun x -> x.Range)

            WarningCodes.baseCallIgnored entRef.FullName |> addWarningWithCode com [] range

            None
        | Some _, _ ->
            let range = baseCall |> Option.bind (fun x -> x.Range)

            "Unexpected base call expression, please report" |> addError com [] range

            None
        | None, _ -> None

    let private transformOrdinaryObjectExpr
        (com: IBabelCompiler)
        ctx
        typ
        (members: Fable.ObjectExprMember list)
        baseCall
        : Expression
        =

        let members = members |> List.map (fun memb -> memb, com.GetMember(memb.MemberRef))
        let isOptionBag = members |> List.forall (fun (_, info) -> info.IsValue)

        // fickl: jsOptions and partial build option bags from generated members
        let isGeneratedBag =
            isOptionBag
            && not members.IsEmpty
            && members
               |> List.forall (fun (memb, _) ->
                   match memb.MemberRef with
                   | Fable.GeneratedMemberRef _ -> true
                   | Fable.MemberRef _ -> false
               )

        // A nested setter makes a nested object, which a shallow Partial would check as complete
        let isFlat =
            members
            |> List.forall (fun (memb, _) ->
                match memb.Body with
                | Fable.ObjectExpr _ -> false
                | _ -> true
            )


        let hasExplicitDisposal name =
            members
            |> List.exists (fun (memb, info) -> memb.Name = name || SymbolKeys.isWellKnownMember name info)

        // Optimization: Object literals with getters and setters are very slow in V8
        // so use a class expression instead. See https://github.com/fable-compiler/Fable/pull/2165#issuecomment-695835444
        let compileAsClass =
            (Option.isSome baseCall, members)
            ||> List.fold (fun compileAsClass (memb, info) ->
                compileAsClass
                || (not memb.IsMangled
                    && (info.IsSetter || (info.IsGetter && canHaveSideEffects com memb.Body)))
            )

        // Members of a base class the object expression overrides
        let overrideNames =
            members
            |> List.choose (fun (memb, info) ->
                match info.DeclaringEntity |> Option.bind com.TryGetEntity with
                | Some ent when not ent.IsInterface && ent.FullName <> Types.object ->
                    match SymbolKeys.memberKey com ctx info memb.Name with
                    | Expression.Identifier(Identifier(name, _)), false
                    | Expression.Literal(Literal.StringLiteral(StringLiteral(name, _))), false -> Some name
                    | _ -> None
                | _ -> None
            )
            |> List.append objectPrototypeNames
            |> set

        let members =
            members
            |> List.collect (fun (memb, info) ->
                let entOpt = info.DeclaringEntity |> Option.bind (fun e -> com.TryGetEntity(e))

                let prop, isComputed = SymbolKeys.memberKey com ctx info memb.Name

                let makeMethod kind =
                    let args = memb.Args

                    let unwrappedType, body =
                        match kind with
                        | ObjectGetter -> unwrapOptionalReturn com memb.Body
                        | _ -> None, memb.Body

                    let args, body, returnType, typeParams =
                        getMemberArgsAndBodyWithReturnType
                            com
                            ctx
                            (Attached(isStatic = false))
                            entOpt
                            info
                            args
                            (defaultArg unwrappedType body.Type)
                            body

                    let returnType =
                        if Option.isSome unwrappedType then
                            returnType
                            |> Option.map (fun t -> UnionTypeAnnotation [| t; UndefinedTypeAnnotation |])
                        else
                            returnType

                    ObjectMember.objectMethod (
                        kind,
                        prop,
                        args,
                        body,
                        isComputed = isComputed,
                        ?returnType = returnType,
                        ?typeParameters = typeParams
                    )

                // If compileAsClass is false, it means getters don't have side effects
                // and can be compiled as object fields (see condition above)
                if not memb.IsMangled && (info.IsValue || (not compileAsClass && info.IsGetter)) then
                    let _, body = unwrapOptionalReturn com memb.Body
                    let value = com.TransformAsExpr(ctx, body)

                    if info.IsValue && isUndefinedPropertyValue com body value then
                        []
                    else
                        [ ObjectMember.objectProperty (prop, value, isComputed = isComputed) ]

                elif not memb.IsMangled && info.IsGetter then
                    [ makeMethod ObjectGetter ]

                elif not memb.IsMangled && info.IsSetter then
                    [ makeMethod ObjectSetter ]

                elif
                    (info.FullName = "System.IDisposable.Dispose"
                     && not (hasExplicitDisposal "Symbol.dispose"))
                    || (info.FullName = "System.IAsyncDisposable.DisposeAsync"
                        && not (hasExplicitDisposal "Symbol.asyncDispose"))
                then
                    let key, body, returnType =
                        nativeDisposalBridge com ctx (info.FullName = "System.IAsyncDisposable.DisposeAsync")

                    [
                        makeMethod ObjectMeth
                        ObjectMember.objectMethod (
                            ObjectMeth,
                            key,
                            [||],
                            body,
                            isComputed = true,
                            ?returnType = returnType
                        )
                    ]

                elif info.FullName = "System.Collections.Generic.IEnumerable.GetEnumerator" then
                    let method = makeMethod ObjectMeth

                    let iterator =
                        let prop, isComputed = memberFromName "Symbol.iterator"
                        let body = enumerableThisToIterator com ctx

                        ObjectMember.objectMethod (ObjectMeth, prop, [||], body, isComputed = isComputed)

                    [ method; iterator ]

                else
                    [ makeMethod ObjectMeth ]
            )

        if not compileAsClass then
            let expr = Expression.objectExpression (List.toArray members)

            match typ with
            | Fable.DeclaredType(entRef, _) when com.IsTypeScript ->
                let ta = makeTypeAnnotation com ctx typ

                if isGeneratedBag && entRef.FullName = "Fickl.Core.Partial`1" then
                    // `partial`: the literal is checked against Partial<T>, and keeps its own type
                    SatisfiesExpression(expr, ta)
                elif isGeneratedBag && isFlat then
                    // jsOptions: misspelled keys and mistyped values are caught before the cast
                    let partial = AliasTypeAnnotation(Identifier.identifier "Partial", [| ta |])

                    AsExpression(SatisfiesExpression(expr, partial), ta)
                elif entRef.FullName = Types.ienumerableGeneric || isOptionBag then
                    // Interop option bags can intentionally contain only part of the declared interface.
                    AsExpression(expr, ta)
                else
                    expr
            | _ -> expr
        else
            let classMembers =
                members
                |> List.choose (
                    function
                    | ObjectProperty(key, value, isComputed, doc) ->
                        ClassMember.classProperty (key, value, isComputed = isComputed, ?doc = doc)
                        |> Some
                    | ObjectMethod(kind, key, parameters, body, isComputed, returnType, typeParameters, _, doc) ->
                        let kind =
                            match kind with
                            | ObjectGetter -> ClassGetter(key, isComputed)
                            | ObjectSetter -> ClassSetter(key, isComputed)
                            | _ -> ClassFunction(key, isComputed)

                        ClassMember.classMethod (
                            kind,
                            parameters,
                            Some body,
                            ?returnType = returnType,
                            typeParameters = typeParameters,
                            ?doc = doc
                        )
                        |> Some
                )

            let baseExpr, classMembers =
                baseCall
                |> extractSuperClassFromBaseCall com ctx (Some typ)
                |> Option.map (fun (baseExpr, baseArgs) ->
                    let consBody = BlockStatement([| callSuperAsStatement baseArgs |])

                    let cons =
                        ClassMember.classMethod (ClassPrimaryConstructor [||], [||], Some consBody)

                    Some baseExpr, cons :: classMembers
                )
                |> Option.defaultValue (None, classMembers)

            let classMembers =
                List.toArray classMembers
                |> markOverrides com overrideNames (Option.isSome baseExpr)

            let classExpr = Expression.classExpression (classMembers, ?superClass = baseExpr)

            Expression.newExpression (classExpr, [||])

    let transformObjectExpr (com: IBabelCompiler) ctx typ (members: Fable.ObjectExprMember list) baseCall : Expression =
        match typ, members, baseCall with
        | Fable.DeclaredType(entityRef, _), [ memberInfo ], None when
            hasAttribute Atts.callableInterface (com.GetEntity(entityRef).Attributes)
            ->
            let entity = com.GetEntity(entityRef)
            let info = com.GetMember(memberInfo.MemberRef)

            let name, args, body =
                match memberInfo.Args with
                | self :: args when self.IsThisArgument ->
                    let self = { self with IsThisArgument = false }

                    let body =
                        memberInfo.Body
                        |> visitFromInsideOut (
                            function
                            | Fable.IdentExpr ident when ident.Name = self.Name -> Fable.IdentExpr self
                            | expression -> expression
                        )

                    self.Name, args, body
                | args -> "Invoke", args, memberInfo.Body

            let capturesReceiver = usesGeneratorReceiver body

            let args, body, returnType, typeParameters =
                getMemberArgsAndBody com ctx (NonAttached name) (Some entity) info args body

            let args =
                if com.IsTypeScript && capturesReceiver then
                    Array.append [| Parameter.parameter ("this", typeAnnotation = AnyTypeAnnotation) |] args
                else
                    args

            Expression.functionExpression (
                args,
                body,
                id = Identifier.identifier name,
                ?returnType = returnType,
                ?typeParameters = typeParameters
            )
        | _ -> transformOrdinaryObjectExpr com ctx typ members baseCall

    let transformCallArgsWithNamedArgs (com: IBabelCompiler) ctx (callInfo: Fable.CallInfo) =
        let memberInfo = callInfo.MemberRef |> Option.bind com.TryGetMember
        let paramsInfo = Option.map getParamsInfo memberInfo

        let args =
            FSharp2Fable.Util.dropUnitCallArg com callInfo.Args callInfo.SignatureArgTypes callInfo.MemberRef

        let args =
            match paramsInfo with
            | Some info when args.Length <= info.Parameters.Length ->
                List.zipSafe args info.Parameters
                |> List.map (fun (a, p) ->
                    if p.IsOptional then
                        unwrapOptionalArg com a |> snd
                    else
                        a
                )
            | _ -> args

        let args, objArg =
            paramsInfo
            |> Option.map (splitNamedArgs args)
            |> function
                | None
                | Some(_, None) -> args, None
                // If there are named arguments but none is passed, pass an empty object
                // See https://github.com/fable-compiler/Fable/issues/3480
                | Some(args, Some []) -> args, Some(makeJsObject [])
                | Some(args, Some namedArgs) ->
                    let objArg =
                        namedArgs
                        |> List.choose (fun (p, v) ->
                            match p.Name, v with
                            | Some k, Fable.Value(Fable.NewOption(value, _, _), _) ->
                                value |> Option.map (fun v -> k, v)
                            | Some k, v -> Some(k, v)
                            | None, _ -> None
                        )
                        |> List.choose (fun (k, v) ->
                            let value = com.TransformAsExpr(ctx, v)

                            if isUndefinedPropertyValue com v value then
                                None
                            else
                                Some(k, value)
                        )
                        |> makeJsObject

                    args, Some objArg

        let hasSpread =
            paramsInfo |> Option.map (fun i -> i.HasSpread) |> Option.defaultValue false

        let args =
            if hasSpread then
                match List.rev args with
                | [] -> []
                | (Replacements.Util.ArrayOrListLiteral(spreadArgs, _)) :: rest ->
                    let rest = List.rev rest |> List.map (fun e -> com.TransformAsExpr(ctx, e))

                    rest @ (List.map (fun e -> com.TransformAsExpr(ctx, e)) spreadArgs)
                | last :: rest ->
                    let rest = List.rev rest |> List.map (fun e -> com.TransformAsExpr(ctx, e))

                    rest @ [ Expression.spreadElement (com.TransformAsExpr(ctx, last)) ]
            else
                List.map (fun e -> com.TransformAsExpr(ctx, e)) args

        args, objArg

    let transformCallArgs com ctx callInfo =
        match transformCallArgsWithNamedArgs com ctx callInfo with
        | args, None -> args
        | args, Some objArg -> args @ [ objArg ]

    let resolveExpr t strategy babelExpr : Statement =
        match strategy with
        | None
        | Some ReturnUnit -> ExpressionStatement(babelExpr)
        // TODO: Where to put these int wrappings? Add them also for function arguments?
        | Some Return -> Statement.returnStatement (wrapIntExpression t babelExpr, ?loc = babelExpr.Location)
        | Some(Assign left) -> ExpressionStatement(assign babelExpr.Location left babelExpr)
        | Some(Target left) -> ExpressionStatement(assign babelExpr.Location (left |> Expression.Identifier) babelExpr)

    let transformOperation com ctx range opKind : Expression =
        match opKind with
        | Fable.Unary(op, TransformExpr com ctx expr) -> Expression.unaryExpression (op, expr, ?loc = range)

        | Fable.Binary(op, left, right) ->
            match op, left, right with
            | (BinaryEqual | BinaryUnequal), e1, e2 ->
                match e1, e2 with
                | Fable.Value(Fable.Null _, _), e
                | e, Fable.Value(Fable.Null _, _) ->
                    com.TransformAsExpr(ctx, e) |> makeNullCheck range (op = BinaryEqual)
                | ExprType(Fable.Nullable _), ExprType(Fable.Nullable _) ->
                    Replacements.Util.Helper.LibCall(com, "Util", "equals", Fable.Boolean, [ e1; e2 ], ?loc = range)
                    |> transformAsExpr com ctx
                | ExprType(Fable.MetaType), _ ->
                    let e =
                        Replacements.Util.Helper.LibCall(
                            com,
                            "Reflection",
                            "equals",
                            Fable.Boolean,
                            [ e1; e2 ],
                            ?loc = range
                        )

                    let e =
                        if op = BinaryEqual then
                            e
                        else
                            makeUnOp None Fable.Boolean e UnaryNot

                    transformAsExpr com ctx e
                | TransformExpr com ctx left, TransformExpr com ctx right ->
                    Expression.binaryExpression (op, left, right, ?loc = range)

            | _, TransformExpr com ctx left, TransformExpr com ctx right ->
                Expression.binaryExpression (op, left, right, ?loc = range)

        | Fable.Logical(op, TransformExpr com ctx left, TransformExpr com ctx right) ->
            Expression.logicalExpression (left, op, right, ?loc = range)

    let isTypedArrayConstructor (expr: Fable.Expr) =
        match expr with
        | Fable.IdentExpr ident ->
            match ident.Name with
            | "Int8Array"
            | "Uint8Array"
            | "Uint8ClampedArray"
            | "Int16Array"
            | "Uint16Array"
            | "Int32Array"
            | "Uint32Array"
            | "Float32Array"
            | "Float64Array"
            | "BigInt64Array"
            | "BigUint64Array" -> true
            | _ -> false
        | _ -> false

    // Typed array constructors copy and convert their argument, so the literal does not need to be a typed array itself
    let unwrapTypedArrayConstructorArg (macro: string) (info: Fable.CallInfo) =
        match macro, info.ThisArg, info.Args with
        | "new $0($1...)",
          Some cons,
          [ MaybeCasted(Fable.Value(Fable.NewArray(Fable.ArrayValues _ as values, typ, _), r)) ] when
            isTypedArrayConstructor cons
            ->
            { info with Args = [ Fable.Value(Fable.NewArray(values, typ, Fable.ResizeArray), r) ] }
        | _ -> info

    let transformEmit (com: IBabelCompiler) ctx range (info: Fable.EmitInfo) =
        match info.CallInfo.Tags with
        | Fable.Tags.Contains "fickl-native-yield"
        | Fable.Tags.Contains "fickl-generator-return" when ctx.NativeGeneratorType.IsNone ->
            addErrorAndReturnNull com range "Native yields and returns must be inside a jsGenerator computation"
        | Fable.Tags.Contains "fickl-native-await" when not ctx.IsNativeAsync ->
            addErrorAndReturnNull com range "Native promise bindings must be inside a jsAsync computation"
        | Fable.Tags.Contains "topDirectiveProloge" ->
            com.SaveTopDirectivePrologue(info.Macro)
            // Return empty sequence expression so it can be erased at print time
            Expression.sequenceExpression ([||])
        | _ ->
            let info =
                { info with CallInfo = unwrapTypedArrayConstructorArg info.Macro info.CallInfo }

            let macro = stripImports com ctx range info.Macro
            let info = info.CallInfo

            let thisArg =
                info.ThisArg
                |> Option.map (fun e -> com.TransformAsExpr(ctx, e))
                |> Option.toList

            transformCallArgs com ctx info
            |> List.append thisArg
            |> emitExpression range macro

    [<RequireQualifiedAccess>]
    type JsxPropExpr =
        | KeyValue of key: string * value: Fable.Expr
        | Spread of value: Fable.Expr

    let transformJsxProps (com: IBabelCompiler) props =
        (Some([], []), props)
        ||> List.fold (fun propsAndChildren prop ->
            match propsAndChildren, prop with
            | None, _ -> None
            | Some(props, children), MaybeCasted(Fable.Value(Fable.NewTuple([ StringConst key; value ], _), _)) ->
                if key = "children" then
                    match value with
                    | Replacements.Util.ArrayOrListLiteral(children, _) -> Some(props, children)
                    | value -> Some(props, [ value ])
                else
                    Some(JsxPropExpr.KeyValue(key, value) :: props, children)
            // When the string value exceeds ~100 chars, Fable wraps it in a Let binding.
            // The Let body still contains the tuple with the prop key and a reference to the bound var;
            // we use the bound value (letValue) as the actual prop value.
            | Some(props, children),
              MaybeCasted(Fable.Let(_, letValue, MaybeCasted(Fable.Value(Fable.NewTuple([ StringConst key; _ ], _), _)))) ->
                if key = "children" then
                    match letValue with
                    | Replacements.Util.ArrayOrListLiteral(children, _) -> Some(props, children)
                    | _ -> Some(props, [ letValue ])
                else
                    Some(JsxPropExpr.KeyValue(key, letValue) :: props, children)
            | Some(props, children),
              MaybeCasted(Fable.Call(_,
                                     {
                                         Tags = Fable.Tags.Contains "jsx-spread"
                                         Args = [ value ]
                                     },
                                     _,
                                     _)) -> Some(JsxPropExpr.Spread value :: props, children)
            | Some _, e ->
                addError com [] e.Range "Cannot detect JSX prop key at compile time"

                None
        )

    module Jsx =

        (***

For JSX, we want to rewrite the default output in order to remove all the code coming from list CEs

By default, this code

```fs
Html.div
    [
        yield! [
            Html.div "Test 1"
            Html.div "Test 2"
        ]
    ]
```

generates something like

```jsx
<div>
    {toList(delay(() => [<div>
        Test 1
    </div>, <div>
        Test 2
    </div>]))}
</div>;
```

but thanks to the optimisation done below we get

```jsx
<div>
    <div>
        Test 1
    </div>
    <div>
        Test 2
    </div>
</div>
```

        Initial implementation of this optimiser comes from https://github.com/shayanhabibi/Partas.Solid/blob/master/Partas.Solid.FablePlugin/Plugin.fs

        ***)

        // A sequence lambda can only be flattened away when its body is a single `return`
        // (handled by the ArrowFunctionExpression cases of the Unroller below).
        let isFlattenableSeqLambdaBody (body: Statement array) =
            match body with
            | [| ReturnStatement _ |] -> true
            | _ -> false

        // Check if the provided expression is equal to the expected identiferText (as a string)
        [<return: Struct>]
        let rec (|IdentifierIs|_|) (identifierText: string) expression =
            match expression with
            | Expression.Identifier(Identifier(currentCallerText, _)) when identifierText = currentCallerText ->
                ValueSome()
            | _ -> ValueNone

        // Make it easy to check if we are calling the expected function

        and [<return: Struct>] (|CalledExpression|_|) (callerText: string) value =
            match value with
            | CallExpression(IdentifierIs callerText, UnrollerFromArray exprs, _, _) -> ValueSome exprs
            | _ -> ValueNone

        and (|UnrollerFromSingleton|) (expr: Expression) : Expression list =
            [ expr ]
            |> function
                | Unroller exprs -> exprs

        and (|UnrollerFromArray|) (arrayExpr: Expression array) : Expression list =
            arrayExpr
            |> Array.toList
            |> function
                | Unroller exprs -> exprs

        and (|Unroller|): Expression list -> Expression list =
            function
            | [] -> []
            | expr :: Unroller rest ->
                match expr with
                | CalledExpression "toList" exprs -> exprs @ rest
                // A `delay(fun () -> ...)` whose lambda body cannot be flattened (e.g. a `match`
                // arm that binds a value before a `for` loop) must not be stripped down to a bare
                // lambda, which React rejects ("Functions are not valid as a React child"). The
                // `delay` only adds laziness, which is pointless for a JSX child, so invoke the
                // lambda directly and let React render the produced sequence.
                | CallExpression(IdentifierIs "delay",
                                 [| (ArrowFunctionExpression([||], BlockStatement body, _, _, _)) as lambda |],
                                 _,
                                 _) when not (isFlattenableSeqLambdaBody body) ->
                    CallExpression(lambda, [||], [||], None) :: rest
                | CalledExpression "delay" exprs -> exprs @ rest
                | ArrowFunctionExpression([||],
                                          BlockStatement [| ReturnStatement(ArrayExpression(UnrollerFromArray exprs, _),
                                                                            _) |],
                                          _,
                                          _,
                                          _) -> exprs @ rest
                | ArrowFunctionExpression([||],
                                          BlockStatement [| ReturnStatement(UnrollerFromSingleton exprs, _) |],
                                          _,
                                          _,
                                          _) -> exprs @ rest
                | CalledExpression "append" exprs -> exprs @ rest
                | CalledExpression "singleton" exprs -> exprs @ rest
                | CalledExpression "empty" _ -> [ Expression.nullLiteral () ] @ rest
                // Note: Should we guard this unwrapper by checking that all the elements in the array are JsxElements?
                | ArrayExpression(UnrollerFromArray exprs, _) -> exprs @ rest
                | ConditionalExpression(testExpr, UnrollerFromSingleton thenExprs, UnrollerFromSingleton elseExprs, loc) ->
                    ConditionalExpression(
                        testExpr,
                        SequenceExpression(thenExprs |> List.toArray, None),
                        SequenceExpression(elseExprs |> List.toArray, None),
                        loc
                    )
                    :: rest
                | expr ->
                    // Tips 💡
                    // If a pattern is not optimized, you can put a debug point here to capture it
                    expr :: rest

    let transformJsxEl (com: IBabelCompiler) ctx componentOrTag props =
        match transformJsxProps com props with
        | None -> Expression.nullLiteral ()
        | Some(props, children) ->
            let componentOrTag = transformAsExpr com ctx componentOrTag

            let children =
                children
                |> List.map (transformAsExpr com ctx)
                |> function
                    // Because of call optimizations, it may happen a list has been transformed to an array in JS
                    | [ ArrayExpression(children, _) ] -> Array.toList children
                    | children -> children
                // Optimize AST, removing F# CEs from the output (see documentation in the JSX module)
                |> List.map (fun child ->
                    match child with
                    | Jsx.UnrollerFromSingleton expr -> expr
                )
                |> List.concat

            let props =
                props
                |> List.rev
                |> List.map (
                    function
                    | JsxPropExpr.KeyValue(key, value) -> JsxProp.KeyValue(key, transformAsExpr com ctx value)
                    | JsxPropExpr.Spread value -> JsxProp.Spread(transformAsExpr com ctx value)
                )

            Expression.jsxElement (componentOrTag, props, children)

    let transformJsxCall (com: IBabelCompiler) ctx callee (args: Fable.Expr list) (info: Fable.MemberFunctionOrValue) =
        let names =
            info.CurriedParameterGroups |> List.concat |> List.choose (fun p -> p.Name)

        let props =
            List.zipSafe names args
            |> List.map (fun (key, value) ->
                Fable.Value(Fable.NewTuple([ Fable.Value(Fable.StringConstant key, None); value ], false), None)
            )

        transformJsxEl com ctx callee props

    let optimizeCall (com: IBabelCompiler) ctx range typ callee (callInfo: Fable.CallInfo) =
        // Try to optimize some patterns after FableTransforms
        match callInfo.Tags, callInfo.Args with
        | Fable.Tags.Contains "fickl-generator-run", [] ->
            let callback = transformAsExpr com ctx callee

            Expression.callExpression (
                Expression.memberExpression (callback, Expression.identifier "call"),
                [| Expression.thisExpression () |]
            )
            |> Some
        | Fable.Tags.Contains "downcast", [ e ] ->
            let e = transformAsExpr com ctx e

            if com.IsTypeScript then
                let typ = makeTypeAnnotation com ctx typ
                AsExpression(e, typ) |> Some
            else
                Some e
        | Fable.Tags.Contains "array", [ maybeList ] ->
            match maybeList with
            | Replacements.Util.ArrayOrListLiteral(vals, _) ->
                Fable.Value(Fable.NewArray(Fable.ArrayValues vals, Fable.Any, Fable.MutableArray), range)
                |> transformAsExpr com ctx
                |> Some
            | Fable.Call(Fable.Import({
                                          Selector = "toList"
                                          Path = Naming.EndsWith "/Seq.js" _
                                          Kind = Fable.LibraryImport _
                                      },
                                      _,
                                      _),
                         callInfo,
                         _,
                         _) ->
                List.head callInfo.Args
                |> Replacements.Util.toArray range typ
                |> transformAsExpr com ctx
                |> Some
            | _ -> None
        | Fable.Tags.Contains "pojo", keyValueList :: caseRule :: _ ->
            JS.Replacements.makePojo com (Some caseRule) keyValueList
            |> Option.map (transformAsExpr com ctx)
        | Fable.Tags.Contains "pojo", keyValueList :: _ ->
            JS.Replacements.makePojo com None keyValueList
            |> Option.map (transformAsExpr com ctx)
        | Fable.Tags.Contains "jsx", componentOrTag :: Replacements.Util.ArrayOrListLiteral(props, _) :: _ ->
            transformJsxEl com ctx componentOrTag props |> Some
        | Fable.Tags.Contains "jsx", _ ->
            "Expecting a static list or array literal (no generator) for JSX props"
            |> addErrorAndReturnNull com range
            |> Some
        | Fable.Tags.Contains "jsx-spread", _ ->
            "JSX.spread can only be used as an element of the props list passed to JSX.create"
            |> addErrorAndReturnNull com range
            |> Some
        | Fable.Tags.Contains "jsx-template", args ->
            match args with
            | StringConst template :: _ ->
                let template = stripImports com ctx range template
                Expression.jsxTemplate (template) |> Some
            | MaybeCasted(Fable.Value(Fable.StringTemplate(_, parts, values), _)) :: _ ->
                let parts =
                    match parts with
                    | head :: parts -> (stripImports com ctx range head) :: parts
                    | parts -> parts

                let values = values |> List.mapToArray (transformAsExpr com ctx)
                Expression.jsxTemplate (List.toArray parts, values) |> Some
            | MaybeCasted(Fable.Call(Fable.Import({
                                                      Selector = "concat"
                                                      Path = Naming.EndsWith "/String.js" _
                                                      Kind = Fable.LibraryImport _
                                                  },
                                                  _,
                                                  _),
                                     callInfo,
                                     _,
                                     _)) :: _ ->
                // The F# compiler lowers a simple interpolated string (no format specifiers)
                // to String.Concat(part1, value, part2, ...). Convert back to a StringTemplate.
                // StringTemplate requires parts.Length = values.Length + 1, strictly alternating,
                // starting and ending with a string part.
                let rec buildParts currentPart parts values args =
                    match args with
                    | [] -> List.rev (currentPart :: parts), List.rev values
                    | Fable.Value(Fable.StringConstant s, _) :: rest -> buildParts (currentPart + s) parts values rest
                    | expr :: rest -> buildParts "" (currentPart :: parts) (expr :: values) rest

                let parts, values = buildParts "" [] [] callInfo.Args

                let parts =
                    match parts with
                    | head :: tail -> (stripImports com ctx range head) :: tail
                    | parts -> parts

                let values = values |> List.mapToArray (transformAsExpr com ctx)
                Expression.jsxTemplate (List.toArray parts, values) |> Some
            | _ ->
                "Expecting a string literal or interpolation without formatting"
                |> addErrorAndReturnNull com range
                |> Some
        | _ -> None

    let private transformNativeDeclarationCall (com: IBabelCompiler) ctx range callee (callInfo: Fable.CallInfo) =
        let info = callInfo.MemberRef |> Option.bind com.TryGetMember |> Option.get
        let owner = com.TransformAsExpr(ctx, callee)

        let receiver =
            match callInfo.ThisArg |> Option.get with
            | MaybeCasted(Fable.Value(Fable.BaseValue(None, _), _)) -> Expression.thisExpression ()
            | value -> com.TransformAsExpr(ctx, value)

        let receiverName = getUniqueNameInDeclarationScope ctx "nativeReceiver"
        let receiverIdent = Expression.identifier receiverName

        let parameter name =
            Parameter.parameter (
                name,
                ?typeAnnotation =
                    (if com.IsTypeScript then
                         Some AnyTypeAnnotation
                     else
                         None)
            )

        let key =
            SymbolKeys.tryMember com ctx info
            |> Option.defaultWith (fun () ->
                Expression.stringLiteral (
                    if info.IsGetter || info.IsSetter then
                        info.DisplayName
                    else
                        info.CompiledName
                )
            )

        let body =
            if info.IsGetter then
                emitExpression range "Reflect.get($0.prototype, $1, $2)" [ owner; key; receiverIdent ]
            elif info.IsSetter then
                let valueName = getUniqueNameInDeclarationScope ctx "nativeValue"
                let value = Expression.identifier valueName
                let targetName = getUniqueNameInDeclarationScope ctx "nativeTarget"
                let keyName = getUniqueNameInDeclarationScope ctx "nativeKey"

                let body =
                    emitExpression
                        range
                        "($3 == null ? (() => { throw new TypeError('Cannot assign native member on null'); })() : Reflect.set($0, $1, $2, $3) ? void 0 : (() => { throw new TypeError('Cannot assign native member'); })())"
                        [
                            Expression.identifier targetName
                            Expression.identifier keyName
                            value
                            receiverIdent
                        ]

                let assign = Expression.arrowFunctionExpression ([| parameter valueName |], body)

                Expression.callExpression (
                    Expression.arrowFunctionExpression ([| parameter targetName; parameter keyName |], assign),
                    [| emitExpression range "$0.prototype" [ owner ]; key |]
                )
            else
                let methodName = getUniqueNameInDeclarationScope ctx "nativeMethod"
                let argsName = getUniqueNameInDeclarationScope ctx "nativeArgs"

                let argsParameter =
                    Parameter
                        .parameter(
                            argsName,
                            ?typeAnnotation =
                                (if com.IsTypeScript then
                                     Some(ArrayTypeAnnotation AnyTypeAnnotation)
                                 else
                                     None)
                        )
                        .WithFlags(ParameterFlags(isSpread = true))

                let invoke =
                    emitExpression
                        range
                        "Reflect.apply($0, $1, $2)"
                        [
                            Expression.identifier methodName
                            receiverIdent
                            Expression.identifier argsName
                        ]

                let invoke =
                    Expression.arrowFunctionExpression (
                        [| argsParameter |],
                        invoke,
                        ?returnType =
                            (if com.IsTypeScript then
                                 Some AnyTypeAnnotation
                             else
                                 None)
                    )

                let selectedMethod =
                    emitExpression range "Reflect.get($0.prototype, $1, $2)" [ owner; key; receiverIdent ]

                Expression.callExpression (
                    Expression.arrowFunctionExpression ([| parameter methodName |], invoke),
                    [| selectedMethod |]
                )

        let body =
            if info.IsSetter then
                body
            else
                emitExpression
                    range
                    "($0 == null ? (() => { throw new TypeError('Cannot access native member on null'); })() : $1)"
                    [ receiverIdent; body ]

        let selected =
            Expression.callExpression (
                Expression.arrowFunctionExpression ([| parameter receiverName |], body),
                [| receiver |]
            )

        if info.IsGetter then
            selected
        else
            let args = transformCallArgs com ctx callInfo |> List.toArray
            Expression.callExpression (selected, args, ?loc = range)

    let transformCall (com: IBabelCompiler) ctx range typ callee (callInfo: Fable.CallInfo) =
        validateAsyncGeneratorPayload com range typ

        if hasNativeAsyncPromisePayload typ then
            let isNativeAsync =
                List.contains "fickl-async-run" callInfo.Tags
                || (callInfo.MemberRef
                    |> Option.bind com.TryGetMember
                    |> Option.exists (fun info ->
                        hasAttribute Atts.ficklAsyncFunction info.Attributes
                        || FSharp2Fable.Util.isNativeAsyncFactory com info
                    ))
                || isLocalNativeAsyncCallee ctx callee callInfo.MemberRef

            if isNativeAsync then
                validateNativeAsyncPayload com range typ

        // Try to optimize some patterns after FableTransforms
        match
            if List.contains "native-declaration" callInfo.Tags then
                Some(transformNativeDeclarationCall com ctx range callee callInfo)
            else
                optimizeCall com ctx range typ callee callInfo
        with
        | Some e -> e
        | None ->
            match callInfo.MemberRef |> Option.bind com.TryGetMember with
            | Some memberInfo when hasAttribute Atts.jsxComponent memberInfo.Attributes ->
                transformJsxCall com ctx callee callInfo.Args memberInfo
            | memberInfo ->
                let callee =
                    match memberInfo |> Option.bind (SymbolKeys.tryMember com ctx), callee with
                    | Some key, Fable.Get(target, Fable.FieldGet _, _, _) ->
                        Expression.memberExpression (com.TransformAsExpr(ctx, target), key, true)
                    | _ -> com.TransformAsExpr(ctx, callee)

                let nonNamedArgs, namedArgs = transformCallArgsWithNamedArgs com ctx callInfo

                let args = nonNamedArgs @ Option.toList namedArgs

                match callInfo.ThisArg with
                | None when List.contains "new" callInfo.Tags ->
                    let pojo =
                        match nonNamedArgs, namedArgs, memberInfo with
                        | [], Some namedArgs, Some memberInfo ->
                            match memberInfo.DeclaringEntity |> Option.bind com.TryGetEntity with
                            | Some e when FSharp2Fable.Util.isPojoDefinedByConsArgsEntity e -> Some namedArgs
                            | _ -> None
                        | _ -> None

                    match pojo with
                    | Some pojo -> pojo
                    | None ->
                        let typeParamInst =
                            match typ with
                            | Fable.DeclaredType(_entRef, genArgs) ->
                                makeTypeParamInstantiationIfTypeScript com ctx genArgs
                            | _ -> None

                        Expression.newExpression (
                            callee,
                            List.toArray args,
                            ?typeArguments = typeParamInst,
                            ?loc = range
                        )
                | None -> callFunction com ctx range callee callInfo.GenericArgs args
                | Some(TransformExpr com ctx thisArg) ->
                    callFunction com ctx range callee callInfo.GenericArgs (thisArg :: args)

    let transformCurriedApply (com: IBabelCompiler) ctx range (applied: Fable.Expr) args =
        let resultType =
            (applied.Type, args)
            ||> List.fold (fun typ _ ->
                match typ with
                | Fable.LambdaType(_, returnType) -> returnType
                | _ -> Fable.Any
            )

        validateAsyncGeneratorPayload com range resultType

        if
            hasNativeAsyncPromisePayload resultType
            && isLocalNativeAsyncCallee ctx applied None
        then
            validateNativeAsyncPayload com range resultType

        // decision: declaration types preserve generic unit arguments despite specialized identifier types at call sites
        let appliedType =
            match applied with
            // invariant: FSharp2Fable makes local names unique within a declaration (getIdentUniqueName)
            | MaybeCasted(Fable.IdentExpr id) -> Map.tryFind id.Name ctx.BoundTypes |> Option.defaultValue id.Type
            | _ -> applied.Type

        let applied = com.TransformAsExpr(ctx, applied)

        ((applied, appliedType), args)
        ||> List.fold (fun (expr, calleeType) arg ->
            let argType, returnType =
                match calleeType with
                | Fable.LambdaType(argType, returnType) -> Some argType, returnType
                | _ -> None, Fable.Any

            let args =
                match arg, argType with
                | _, Some argType when argType <> Fable.Unit -> [ com.TransformAsExpr(ctx, arg) ]
                // TODO: If arg type is unit but it's an expression with potential
                // side-effects, we need to extract it and execute it before the call
                | Fable.Value(Fable.UnitConstant, _), _ -> []
                | Fable.IdentExpr ident, _ when ident.Type = Fable.Unit -> []
                | TransformExpr com ctx arg, _ -> [ arg ]

            callFunction com ctx range expr [] args, returnType
        )
        |> fst

    /// fickl: a call to a member declared to return `unknown` is `any` inside Fickl's own output, as `obj` is
    let anyIfReturnsUnknown (com: IBabelCompiler) (info: Fable.CallInfo) (expr: Expression) =
        match info.MemberRef with
        | Some memberRef when com.IsTypeScript ->
            match com.TryGetMember(memberRef) with
            | Some memb when FSharp2Fable.Util.TsUnknown.returnsUnknown memb -> AsExpression(expr, AnyTypeAnnotation)
            | _ -> expr
        | _ -> expr

    let transformCallAsStatements com ctx range typ returnStrategy callee callInfo =
        let argsLen (i: Fable.CallInfo) =
            List.length i.Args
            + (if Option.isSome i.ThisArg then
                   1
               else
                   0)
        // Warn when there's a recursive call that couldn't be optimized?
        match returnStrategy, ctx.TailCallOpportunity with
        | Some(Return | ReturnUnit), Some tc when tc.IsRecursiveRef(callee) && argsLen callInfo = List.length tc.Args ->
            let args =
                match callInfo.ThisArg with
                | Some thisArg -> thisArg :: callInfo.Args
                | None -> callInfo.Args

            optimizeTailCall com ctx range tc args
        | _ ->
            [|
                transformCall com ctx range typ callee callInfo
                |> anyIfReturnsUnknown com callInfo
                |> resolveExpr typ returnStrategy
            |]

    let transformCurriedApplyAsStatements com ctx range typ returnStrategy callee args =
        // Warn when there's a recursive call that couldn't be optimized?
        match returnStrategy, ctx.TailCallOpportunity with
        | Some(Return | ReturnUnit), Some tc when tc.IsRecursiveRef(callee) && List.sameLength args tc.Args ->
            optimizeTailCall com ctx range tc args
        | _ ->
            [|
                transformCurriedApply com ctx range callee args
                |> resolveExpr typ returnStrategy
            |]

    // When expecting a block, it's usually not necessary to wrap it
    // in a lambda to isolate its variable context
    let transformBlock (com: IBabelCompiler) ctx ret expr : BlockStatement =
        com.TransformAsStatements(ctx, ret, expr) |> BlockStatement

    let transformBlockWithVarHoisting (com: IBabelCompiler) ctx ret expr : BlockStatement =
        let declaredVars = ResizeArray()

        let ctx =
            { ctx with
                HoistVars =
                    fun ids ->
                        declaredVars.AddRange(ids)
                        true
            }

        let body = transformBlock com ctx ret expr

        if declaredVars.Count = 0 then
            body
        else
            let varDeclStatement =
                declaredVars |> Seq.map (fun v -> v, None) |> multiVarDeclaration com ctx Let

            BlockStatement(Array.append [| varDeclStatement |] body.Body)

    let transformTryCatch com ctx r returnStrategy (body, catch, finalizer) =
        // try .. catch statements cannot be tail call optimized
        let ctx = { ctx with TailCallOpportunity = None }

        let handler =
            catch
            |> Option.map (fun (param: Fable.Ident, body) ->
                let ta = makeTypeAnnotationIfTypeScript com ctx Fable.Any None // intentionally set catch type to 'any'

                CatchClause.catchClause (
                    param.Name,
                    ?annotation = ta,
                    body = transformBlock com ctx returnStrategy body
                )
            )

        let finalizer = finalizer |> Option.map (transformBlock com ctx None)

        [|
            Statement.tryStatement (
                transformBlock com ctx returnStrategy body,
                ?handler = handler,
                ?finalizer = finalizer,
                ?loc = r
            )
        |]

    let rec transformIfStatement (com: IBabelCompiler) ctx r ret guardExpr thenStmnt elseStmnt =
        match com.TransformAsExpr(ctx, guardExpr) with
        // This optimization is already in FableTransforms so we can remove it
        // or try to check if the value is JS truthy or falsy
        | Literal(BooleanLiteral(value = value)) ->
            let e =
                if value then
                    thenStmnt
                else
                    elseStmnt

            com.TransformAsStatements(ctx, ret, e)

        | jsGuardExpr ->
            match tryTransformIfThenElseAsSwitch guardExpr thenStmnt elseStmnt with
            | Some(evalExpr, cases, defaultCase) -> transformSwitch com ctx ret evalExpr cases (Some defaultCase)
            | _ ->
                let thenStmnt = transformBlock com ctx ret thenStmnt

                match com.TransformAsStatements(ctx, ret, elseStmnt) with
                | [||] -> Statement.ifStatement (jsGuardExpr, thenStmnt, ?loc = r)
                | [| elseStmnt |] -> Statement.ifStatement (jsGuardExpr, thenStmnt, elseStmnt, ?loc = r)
                | statements ->
                    Statement.ifStatement (jsGuardExpr, thenStmnt, Statement.blockStatement (statements), ?loc = r)
                |> Array.singleton

    /// fickl: TypeScript cannot resolve `T[KeysOfType<T, R>]` to `R` while `T` or `R` is generic, but it can
    /// index `Record<KeysOfType<T, R>, R>`, so the object is cast to that. `globalThis` passes the import of
    /// Fable's own `Record` class
    let private keyedAccessTarget (com: IBabelCompiler) ctx (key: Fable.Expr) (expr: Expression) =
        match key.Type with
        | Fable.DeclaredType(entRef, [ _; valueType ]) when
            com.IsTypeScript
            && entRef.FullName = "Fickl.Core.keyof`2"
            && not (List.isEmpty (FSharp2Fable.Util.getGenParamNames key.Type.Generics))
            ->
            AsExpression(expr, makeNativeTypeAnnotation com ctx [ key.Type; valueType ] "globalThis.Record")
        | _ -> expr

    let transformGet (com: IBabelCompiler) ctx range typ fableExpr kind =
        match kind with
        | Fable.ExprGet key ->
            let prop = com.TransformAsExpr(ctx, key)
            let expr = com.TransformAsExpr(ctx, fableExpr) |> keyedAccessTarget com ctx key
            getExpr range expr prop

        | Fable.FieldGet info ->
            let fableExpr =
                match fableExpr with
                // If we're accessing a virtual member with default implementation (see #701)
                // from base class, we can use `super` in JS so we don't need the bound this arg
                | Fable.Value(Fable.BaseValue(_, t), r) -> Fable.Value(Fable.BaseValue(None, t), r)
                | _ -> fableExpr

            let expr = com.TransformAsExpr(ctx, fableExpr)
            SymbolKeys.access com ctx range fableExpr.Type expr info.Name

        | Fable.ListHead ->
            // get range (com.TransformAsExpr(ctx, fableExpr)) "head"
            libCall com ctx range "List" "head" [] [ com.TransformAsExpr(ctx, fableExpr) ]

        | Fable.ListTail ->
            // get range (com.TransformAsExpr(ctx, fableExpr)) "tail"
            libCall com ctx range "List" "tail" [] [ com.TransformAsExpr(ctx, fableExpr) ]

        | Fable.TupleIndex index ->
            match fableExpr with
            // TODO: Check the erased expressions don't have side effects?
            | Fable.Value(Fable.NewTuple(exprs, _), _) -> com.TransformAsExpr(ctx, List.item index exprs)
            | TransformExpr com ctx expr -> getExpr range expr (ofInt index)

        | Fable.OptionValue ->
            let expr = com.TransformAsExpr(ctx, fableExpr)

            let isStruct =
                match fableExpr.Type with
                | Fable.Option(_, isStruct) -> isStruct
                | _ -> false

            // Functions are stored uncurried, so their annotation would not match the value
            if
                mustWrapOption typ
                || (com.IsTypeScript && containsFunctionType typ)
                || isStrictOption com isStruct typ
            then
                libCall com ctx None "Option" "value" [] [ expr ]
            elif com.IsTypeScript then
                AsExpression(expr, makeTypeAnnotation com ctx typ)
            else
                expr

        | Fable.UnionTag -> getUnionExprTag com ctx range fableExpr

        | Fable.UnionField info ->
            let expr = com.TransformAsExpr(ctx, fableExpr)

            let expr =
                if com.IsTypeScript then
                    match fableExpr with
                    | Fable.IdentExpr _ -> expr
                    | _ ->
                        // If this is not an ident, chances are TypeScript cannot infer
                        // the actual case, so we use a cast to prevent errors
                        let ent = com.GetEntity(info.Entity)

                        if List.isSingle ent.UnionCases then
                            expr
                        else
                            match Lib.tryJsConstructorFor ActualConsRef com ctx ent with
                            | Some(Expression.Identifier(id)) ->
                                let typeParams = makeTypeParamInstantiation com ctx info.GenericArgs

                                let typeParams =
                                    Array.append
                                        typeParams
                                        [| LiteralTypeAnnotation(Literal.numericLiteral (info.CaseIndex)) |]

                                AsExpression(expr, AliasTypeAnnotation(id, typeParams))
                            | _ -> expr
                else
                    expr

            let expr =
                getExpr range (getExpr None expr (Expression.stringLiteral ("fields"))) (ofInt info.FieldIndex)

            if com.IsTypeScript then
                let ta = FableTransforms.uncurryType typ |> makeTypeAnnotation com ctx
                AsExpression(expr, ta)
            else
                expr

    let transformSet (com: IBabelCompiler) ctx range fableExpr typ (value: Fable.Expr) kind =
        let expr = com.TransformAsExpr(ctx, fableExpr)
        let value = com.TransformAsExpr(ctx, value) |> wrapIntExpression typ

        let ret =
            match kind with
            | Fable.ValueSet -> expr
            | Fable.ExprSet key -> getExpr None (keyedAccessTarget com ctx key expr) (com.TransformAsExpr(ctx, key))
            | Fable.FieldSet(fieldName) -> SymbolKeys.access com ctx None fableExpr.Type expr fieldName

        // If this is an arry setter, we need to use the `setItem` method
        // as it allows to check the bounds of the array
        match fableExpr with
        | Fable.IdentExpr ident ->
            match ident.Type with
            | Fable.Type.Array _ ->
                match kind with
                | Fable.ExprSet(TransformExpr com ctx index) ->
                    libCall com ctx range "Array" "setItem" [] [ expr; index; value ]

                | _ -> assign range ret value
            | _ -> assign range ret value
        | _ -> assign range ret value

    let transformBindingExprBody (com: IBabelCompiler) (ctx: Context) (var: Fable.Ident) (value: Fable.Expr) =
        match value with
        | Function(args, body) ->
            let name = Some var.Name

            transformFunctionWithAnnotations com ctx name None args body
            |> makeArrowFunctionExpression name
        | _ ->
            if var.IsMutable then
                com.TransformAsExpr(ctx, value)
            else
                com.TransformAsExpr(ctx, value) |> wrapIntExpression value.Type

    let transformBindingAsExpr (com: IBabelCompiler) ctx (var: Fable.Ident) (value: Fable.Expr) =
        transformBindingExprBody com ctx var value |> assign var.Range (identAsExpr var)

    let transformBindingAsStatements (com: IBabelCompiler) ctx (var: Fable.Ident) (value: Fable.Expr) =
        let varType = Replacements.Util.getIdentValueType com var var.Type

        if isJsStatement ctx false value then
            let ta, tp = makeTypeAnnotationWithParametersIfTypeScript com ctx varType None

            let decl =
                Statement.variableDeclaration (Let, var.Name, ?annotation = ta, typeParameters = tp, ?loc = var.Range)

            let body = com.TransformAsStatements(ctx, Some(Assign(identAsExpr var)), value)

            Array.append [| decl |] body
        else
            let value = transformBindingExprBody com ctx var value

            let ta, tp =
                makeTypeAnnotationWithParametersIfTypeScript com ctx varType (Some value)

            let kind =
                if var.IsMutable then
                    Let
                else
                    Const

            [|
                Statement.variableDeclaration (
                    kind,
                    var.Name,
                    ?annotation = ta,
                    typeParameters = tp,
                    init = value,
                    ?loc = var.Range
                )
            |]

    let transformUnionCaseTag (com: IBabelCompiler) range typ tag =
        let caseName =
            match typ with
            | Fable.DeclaredType(entRef, _) when com.IsTypeScript ->
                let ent = com.GetEntity(entRef)

                match List.tryItem tag ent.UnionCases with
                | Some case -> Some case.Name
                | None ->
                    $"Unmatched union case tag: %d{tag} for %s{ent.FullName}"
                    |> addWarning com [] range

                    None
            | _ -> None

        match caseName with
        | Some name -> CommentedExpression(name, ofInt tag)
        | None -> ofInt tag

    let transformTest (com: IBabelCompiler) ctx range kind expr : Expression =
        match kind with
        | Fable.TypeTest t -> transformTypeTest com ctx range expr t
        | Fable.OptionTest isSome -> com.TransformAsExpr(ctx, expr) |> makeNullCheck range (not isSome)
        | Fable.ListTest nonEmpty ->
            let expr = com.TransformAsExpr(ctx, expr)
            let expr = libCall com ctx range "List" "isEmpty" [] [ expr ]

            if nonEmpty then
                Expression.unaryExpression (UnaryNot, expr, ?loc = range)
            else
                expr
        | Fable.UnionCaseTest tag ->
            let expected = transformUnionCaseTag com range expr.Type tag

            let unionExprTag = getUnionExprTag com ctx None expr

            let actual =
                if com.IsTypeScript then
                    // In TypeScript, we need to cast the union case tag to a number
                    // otherwise TypeScript compiler is too smart and test against the literal value (0, 1, 2, etc.)
                    let number = Fable.Number(Int32, Fable.NumberInfo.Empty)
                    (AsExpression(unionExprTag, makeTypeAnnotation com ctx number))
                else
                    unionExprTag

            Expression.binaryExpression (BinaryEqual, actual, expected, ?loc = range)

    let transformSwitch
        (com: IBabelCompiler)
        ctx
        returnStrategy
        (evalExpr: Fable.Expr)
        cases
        defaultCase
        : Statement array
        =
        let transformGuard =
            function
            | Fable.Test(expr, Fable.UnionCaseTest tag, range) -> transformUnionCaseTag com range expr.Type tag
            | TransformExpr com ctx e -> e

        let cases =
            cases
            |> List.collect (fun (guards, expr) ->
                // Remove empty branches
                match returnStrategy, expr, guards with
                | None, Fable.Value(Fable.UnitConstant, _), _
                | _, _, [] -> []
                | _, _, guards ->
                    let guards, lastGuard = List.splitLast guards

                    let guards = guards |> List.map (fun e -> SwitchCase.switchCase (transformGuard e))

                    let caseBody = com.TransformAsStatements(ctx, returnStrategy, expr)

                    let caseBody =
                        match returnStrategy with
                        | Some Return -> caseBody
                        | _ -> Array.append caseBody [| Statement.breakStatement () |]

                    guards
                    @ [
                        SwitchCase.switchCase (transformGuard lastGuard, [| Statement.blockStatement (caseBody) |])
                    ]
            )

        let cases = cases |> List.toArray
        let switchGuard = transformAsExpr com ctx evalExpr

        let defaultCase =
            defaultCase |> Option.map (transformAsStatements com ctx returnStrategy)

        match cases, defaultCase with
        | [||], Some defaultCase when not (canHaveSideEffects com evalExpr) -> defaultCase
        | cases, Some defaultCase ->
            let cases =
                Array.append
                    cases
                    [|
                        SwitchCase.switchCase (body = [| Statement.blockStatement (defaultCase) |])
                    |]

            [| Statement.switchStatement (switchGuard, cases) |]
        | cases, None -> [| Statement.switchStatement (switchGuard, cases) |]

    let matchTargetIdentAndValues idents values =
        if List.isEmpty idents then
            []
        elif List.sameLength idents values then
            List.zip idents values
        else
            failwith "Target idents/values lengths differ"

    let getDecisionTargetAndBoundValues (com: IBabelCompiler) (ctx: Context) targetIndex boundValues =
        let idents, target = getDecisionTarget ctx targetIndex
        let identsAndValues = matchTargetIdentAndValues idents boundValues

        if not com.Options.DebugMode then
            let bindings, replacements =
                (([], Map.empty), identsAndValues)
                ||> List.fold (fun (bindings, replacements) (ident, expr) ->
                    if canHaveSideEffects com expr then
                        (ident, expr) :: bindings, replacements
                    else
                        bindings, Map.add ident.Name expr replacements
                )

            let target = FableTransforms.replaceValues replacements target
            target, List.rev bindings
        else
            target, identsAndValues

    let transformDecisionTreeSuccessAsExpr (com: IBabelCompiler) (ctx: Context) targetIndex boundValues =
        let target, bindings =
            getDecisionTargetAndBoundValues com ctx targetIndex boundValues

        match bindings with
        | [] -> com.TransformAsExpr(ctx, target)
        | bindings ->
            let target =
                List.rev bindings |> List.fold (fun e (i, v) -> Fable.Let(i, v, e)) target

            com.TransformAsExpr(ctx, target)

    let transformDecisionTreeSuccessAsStatements
        (com: IBabelCompiler)
        (ctx: Context)
        returnStrategy
        targetIndex
        boundValues
        : Statement array
        =
        match returnStrategy with
        | Some(Target targetId) ->
            let idents, _ = getDecisionTarget ctx targetIndex

            let assignments =
                matchTargetIdentAndValues idents boundValues
                |> List.mapToArray (fun (id, TransformExpr com ctx value) ->
                    assign None (identAsExpr id) value |> ExpressionStatement
                )

            if System.String.IsNullOrEmpty targetId.Name then
                assignments
            else
                let targetAssignment =
                    assign None (targetId |> Expression.Identifier) (ofInt targetIndex)
                    |> ExpressionStatement

                Array.append [| targetAssignment |] assignments
        | ret ->
            let target, bindings =
                getDecisionTargetAndBoundValues com ctx targetIndex boundValues

            let bindings =
                bindings
                |> Seq.collect (fun (i, v) -> transformBindingAsStatements com ctx i v)
                |> Seq.toArray

            Array.append bindings (com.TransformAsStatements(ctx, ret, target))

    let tryTransformIfThenElseAsSwitch
        guardExpr
        thenExpr
        elseExpr
        : (Fable.Expr * (Fable.Expr list * Fable.Expr) list * Fable.Expr) option
        =
        let (|Equals|_|) =
            function
            | Fable.Operation(Fable.Binary(BinaryEqual, left, right), _, _, _) ->
                match left, right with
                | _, Fable.Value((Fable.CharConstant _ | Fable.StringConstant _ | Fable.NumberConstant _), _) ->
                    Some(left, right)
                | Fable.Value((Fable.CharConstant _ | Fable.StringConstant _ | Fable.NumberConstant _), _), _ ->
                    Some(right, left)
                | _ -> None
            | Fable.Test(expr, Fable.UnionCaseTest _, _) as right ->
                let evalExpr =
                    Fable.Get(expr, Fable.UnionTag, Fable.Number(Int32, Fable.NumberInfo.Empty), None)

                Some(evalExpr, right)
            | _ -> None

        let rec sameEvalExprs evalExpr1 evalExpr2 =
            match evalExpr1, evalExpr2 with
            | Fable.IdentExpr i1, Fable.IdentExpr i2 -> i1.Name = i2.Name
            | Fable.Get(e1, Fable.UnionTag, _, _), Fable.Get(e2, Fable.UnionTag, _, _)
            | Fable.Get(e1, Fable.ListHead, _, _), Fable.Get(e2, Fable.ListHead, _, _)
            | Fable.Get(e1, Fable.ListTail, _, _), Fable.Get(e2, Fable.ListTail, _, _)
            | Fable.Get(e1, Fable.OptionValue, _, _), Fable.Get(e2, Fable.OptionValue, _, _) -> sameEvalExprs e1 e2
            | Fable.Get(e1, Fable.TupleIndex i1, _, _), Fable.Get(e2, Fable.TupleIndex i2, _, _) ->
                i1 = i2 && sameEvalExprs e1 e2
            | Fable.Get(e1, Fable.FieldGet f1, _, _), Fable.Get(e2, Fable.FieldGet f2, _, _) ->
                f1.Name = f2.Name && sameEvalExprs e1 e2
            | Fable.Get(e1, Fable.UnionField f1, _, _), Fable.Get(e2, Fable.UnionField f2, _, _) ->
                f1.CaseIndex = f2.CaseIndex
                && f1.FieldIndex = f2.FieldIndex
                && sameEvalExprs e1 e2
            | _ -> false

        let rec checkInner cases evalExpr =
            function
            | Fable.IfThenElse(Equals(evalExpr2, caseExpr), thenExpr, elseExpr, _) when sameEvalExprs evalExpr evalExpr2 ->
                checkInner ((caseExpr, thenExpr) :: cases) evalExpr elseExpr
            | defaultCase when List.isMultiple cases -> Some(evalExpr, List.rev cases, defaultCase)
            | _ -> None

        match guardExpr with
        | Equals(evalExpr, caseExpr) ->
            match checkInner [ caseExpr, thenExpr ] evalExpr elseExpr with
            | Some(evalExpr, cases, defaultCase) ->
                let cases = groupSwitchCases cases defaultCase
                Some(evalExpr, cases, defaultCase)
            | None -> None
        | _ -> None

    let tryTransformAsSwitch =
        function
        | Fable.IfThenElse(guardExpr, thenExpr, elseExpr, _) ->
            tryTransformIfThenElseAsSwitch guardExpr thenExpr elseExpr
        | _ -> None

    let transformDecisionTreeAsExpr (com: IBabelCompiler) (ctx: Context) targets expr : Expression =
        // TODO: Check if some targets are referenced multiple times
        let ctx = { ctx with DecisionTargets = targets }
        com.TransformAsExpr(ctx, expr)

    let groupSwitchCases (cases: (Fable.Expr * Fable.Expr) list) defaultCase =
        let canBeGrouped, cannotBeGrouped =
            cases
            |> List.partition (
                function
                | _, Fable.DecisionTreeSuccess(_, [], _) -> true
                | _ -> false
            )

        let grouped =
            canBeGrouped
            |> List.groupBy (
                function
                | _, Fable.DecisionTreeSuccess(idx, _, _) -> idx
                | _ -> failwith "unexpected group candidate"
            )
            |> List.map (fun (_, cases) ->
                let caseExprs = cases |> List.map fst
                caseExprs, List.head cases |> snd
            )

        let cases =
            if grouped |> List.exists (fst >> List.isMultiple) then
                grouped @ List.map (fun (e, b) -> [ e ], b) cannotBeGrouped
            else
                List.map (fun (e, b) -> [ e ], b) cases

        match defaultCase with
        // Remove cases that can be grouped with the default branch, see #2357
        | Fable.DecisionTreeSuccess(defaultIndex, [], _) ->
            cases
            |> List.filter (
                function
                | _, Fable.DecisionTreeSuccess(idx, [], _) -> idx <> defaultIndex
                | _ -> true
            )
        | _ -> cases

    let getTargetsWithMultipleReferences expr =
        let rec findSuccess (targetRefs: Map<int, int>) =
            function
            | Fable.DecisionTreeSuccess(idx, _, _) ->
                let count = Map.tryFind idx targetRefs |> Option.defaultValue 0
                Map.add idx (count + 1) targetRefs
            | Fable.Let(_, _, body) -> List.fold findSuccess targetRefs [ body ]
            | Fable.IfThenElse(_cond, thenExpr, elseExpr, _) -> List.fold findSuccess targetRefs [ thenExpr; elseExpr ]
            // | Fable.LetRec(_, body) -> List.fold findSuccess targetRefs [body]
            // | Fable.Sequential exprs -> exprs |> List.tryLast |> Option.toList |> List.fold findSuccess targetRefs
            // | Fable.TryCatch(body, catch, _finalizer, _) -> body::(catch |> Option.map snd |> Option.toList) |> List.fold findSuccess targetRefs
            | _ -> targetRefs

        findSuccess Map.empty expr
        |> Seq.chooseToList (fun kv ->
            if kv.Value > 1 then
                Some kv.Key
            else
                None
        )

    /// When several branches share target, first get the target index and bound values
    /// and then add a switch to execute the actual targets
    let transformDecisionTreeWithExtraSwitch
        (com: IBabelCompiler)
        ctx
        returnStrategy
        (targets: (Fable.Ident list * Fable.Expr) list)
        treeExpr
        =
        // Declare target and bound idents
        let targetId =
            getUniqueNameInDeclarationScope ctx "matchResult"
            |> makeTypedIdent (Fable.Number(Int32, Fable.NumberInfo.Empty))

        let boundIdents =
            targets
            |> List.collect (fun (idents, _) -> idents |> List.map (fun id -> id, None))

        // Transform targets as switch
        let singleCase, switch2 =
            let ctx =
                { ctx with ForcedIdents = boundIdents |> List.map (fun (id, _) -> id.Name) |> set }

            let cases = targets |> List.mapi (fun i (_, target) -> [ makeIntConst i ], target)

            match cases with
            | [ (_, caseBody) ] -> true, transformAsStatements com ctx returnStrategy caseBody
            | cases ->
                let cases, defaultCase =
                    match returnStrategy with
                    | None
                    | Some ReturnUnit -> cases, None
                    | _ ->
                        let cases, (_, defaultCase) = List.splitLast cases
                        cases, Some defaultCase

                false, transformSwitch com ctx returnStrategy (targetId |> Fable.IdentExpr) cases defaultCase

        let targetId, multiVarDecl =
            if singleCase then
                { targetId with Name = "" }, multiVarDeclaration com ctx Let boundIdents
            else
                targetId, multiVarDeclaration com ctx Let ((targetId, None) :: boundIdents)

        // Transform decision tree
        let targetAssign = Target(identAsIdent targetId)
        let ctx = { ctx with DecisionTargets = targets }

        let decisionTree = com.TransformAsStatements(ctx, Some targetAssign, treeExpr)

        [| yield multiVarDecl; yield! decisionTree; yield! switch2 |]

    let transformDecisionTreeAsStatements
        (com: IBabelCompiler)
        (ctx: Context)
        returnStrategy
        (targets: (Fable.Ident list * Fable.Expr) list)
        (treeExpr: Fable.Expr)
        : Statement array
        =

        let doesNotNeedExtraSwitch cases defaultCase =
            (Some Map.empty, defaultCase :: (cases |> List.map snd))
            ||> List.fold (fun map case ->
                match map, case with
                | Some map, Fable.DecisionTreeSuccess(_, [], _) -> Some map
                | Some map, Fable.DecisionTreeSuccess(idx, _, _) ->
                    map |> Map.change idx (fun i -> (defaultArg i 0) + 1 |> Some) |> Some
                | _ -> None
            )
            |> function
                | Some map -> Map.forall (fun _ count -> count = 1) map
                | _ -> false

        match getTargetsWithMultipleReferences treeExpr, treeExpr with
        | [], _ ->
            let ctx = { ctx with DecisionTargets = targets }
            com.TransformAsStatements(ctx, returnStrategy, treeExpr)

        // If we can compile as switch and there are no bound values, we don't need an extra switch
        | _, Patterns.Try tryTransformAsSwitch (evalExpr, cases, defaultCase) when
            doesNotNeedExtraSwitch cases defaultCase
            ->
            let ctx = { ctx with DecisionTargets = targets }

            transformSwitch com ctx returnStrategy evalExpr cases (Some defaultCase)

        | _ -> transformDecisionTreeWithExtraSwitch com ctx returnStrategy targets treeExpr

    let transformIdent (com: IBabelCompiler) ctx id =
        let e = identAsExpr id

        if com.IsTypeScript && ctx.ForcedIdents.Contains id.Name then
            Expression.unaryExpression (UnaryNot, e, isSuffix = true)
        else
            e

    let rec transformAsExpr (com: IBabelCompiler) ctx (expr: Fable.Expr) : Expression =
        match expr with
        | Fable.Unresolved(_, _, r) -> addErrorAndReturnNull com r "Unexpected unresolved expression"

        | Fable.TypeCast(e, t) -> transformCast com ctx t e

        | Fable.Value(kind, r) -> transformValue com ctx r kind

        | Fable.IdentExpr id -> transformIdent com ctx id

        | Fable.Import({
                           Selector = selector
                           Path = path
                       },
                       _,
                       r) -> transformImport com ctx r selector path

        | Fable.Test(expr, kind, range) -> transformTest com ctx range kind expr

        | Fable.Lambda(arg, body, name) ->
            transformFunctionWithAnnotations com ctx name None [ arg ] body
            |> makeArrowFunctionExpression name

        | Fable.Delegate(args, body, name, tags) ->
            if
                List.contains "fickl-generator-function" tags
                || List.contains "fickl-async-generator-function" tags
            then
                match args with
                | [ metadata ] ->
                    validateGeneratorReturn com metadata.Type body

                    let ctx =
                        { ctx with
                            IsNativeAsync = List.contains "fickl-async-generator-function" tags
                            NativeGeneratorType = Some metadata.Type
                        }

                    let parameters, block, returnType, typeParameters =
                        transformFunctionWithReturnType com ctx name None args metadata.Type body

                    let parameters = Array.skip 1 parameters

                    let parameters =
                        if com.IsTypeScript then
                            Array.append
                                [| Parameter.parameter ("this", typeAnnotation = AnyTypeAnnotation) |]
                                parameters
                        else
                            parameters

                    Expression.generatorFunctionExpression (
                        parameters,
                        block,
                        ?returnType = returnType,
                        ?typeParameters = typeParameters,
                        isAsync = ctx.IsNativeAsync
                    )
                | _ -> addErrorAndReturnNull com body.Range "Invalid native generator metadata"
            elif List.contains "fickl-generator-code" tags then
                addErrorAndReturnNull com body.Range "Deferred jsGenerator code must be consumed by jsGenerator.Run"
            elif List.contains "fickl-async-function" tags then
                let parameters, block, _, typeParameters =
                    transformFunctionWithReturnType
                        com
                        { ctx with
                            IsNativeAsync = true
                            NativeGeneratorType = None
                        }
                        name
                        None
                        args
                        body.Type
                        body

                let returnType =
                    if com.IsTypeScript then
                        Some(makeNativeTypeAnnotation com ctx [ body.Type ] "Promise")
                    else
                        None

                Expression.asyncFunctionExpression (
                    parameters,
                    block,
                    ?returnType = returnType,
                    ?typeParameters = typeParameters,
                    isArrow = true
                )
            elif List.contains "fickl-async-code" tags then
                addErrorAndReturnNull com body.Range "Deferred jsAsync code must be consumed by jsAsync.Run"
            elif List.contains "not-arrow" tags then
                let id = name |> Option.map Identifier.identifier
                let capturesReceiver = usesGeneratorReceiver body

                let args, body, returnType, typeParamDecl =
                    transformFunctionWithAnnotations com ctx name None args body

                let args =
                    if List.contains "fickl-this-function" tags && not com.IsTypeScript then
                        Array.skip 1 args
                    elif List.contains "fickl-js-function" tags && com.IsTypeScript then
                        Array.append [| Parameter.parameter ("this", typeAnnotation = AnyTypeAnnotation) |] args
                    elif
                        com.IsTypeScript
                        && capturesReceiver
                        && not (List.contains "fickl-this-function" tags)
                    then
                        Array.append [| Parameter.parameter ("this", typeAnnotation = AnyTypeAnnotation) |] args
                    else
                        args

                Expression.functionExpression (
                    args,
                    body,
                    ?id = id,
                    ?returnType = returnType,
                    ?typeParameters = typeParamDecl
                )
            else
                transformFunctionWithAnnotations com ctx name None args body
                |> makeArrowFunctionExpression name

        | Fable.ObjectExpr(members, typ, baseCall) -> transformObjectExpr com ctx typ members baseCall

        | Fable.Call(callee, info, typ, range) ->
            transformCall com ctx range typ callee info |> anyIfReturnsUnknown com info

        | Fable.CurriedApply(callee, args, _, range) -> transformCurriedApply com ctx range callee args

        | Fable.Operation(kind, _, _, range) -> transformOperation com ctx range kind

        | Fable.Get(expr, kind, typ, range) -> transformGet com ctx range typ expr kind

        | Fable.IfThenElse(TransformExpr com ctx guardExpr,
                           TransformExpr com ctx thenExpr,
                           TransformExpr com ctx elseExpr,
                           r) -> Expression.conditionalExpression (guardExpr, thenExpr, elseExpr, ?loc = r)

        | Fable.DecisionTree(expr, targets) -> transformDecisionTreeAsExpr com ctx targets expr

        | Fable.DecisionTreeSuccess(idx, boundValues, _) -> transformDecisionTreeSuccessAsExpr com ctx idx boundValues

        | Fable.Set(expr, kind, typ, value, range) -> transformSet com ctx range expr typ value kind

        | Fable.Let(ident, value, body) ->
            if ctx.HoistVars [ ident ] then
                let assignment = transformBindingAsExpr com ctx ident value
                let ctx = { ctx with BoundTypes = Map.add ident.Name ident.Type ctx.BoundTypes }

                Expression.sequenceExpression ([| assignment; com.TransformAsExpr(ctx, body) |])
            else
                iife com ctx expr

        | Fable.LetRec(bindings, body) ->
            if ctx.HoistVars(List.map fst bindings) then
                let ctx =
                    { ctx with
                        BoundTypes =
                            (ctx.BoundTypes, bindings)
                            ||> List.fold (fun types (id, _) -> Map.add id.Name id.Type types)
                    }

                let values =
                    bindings
                    |> List.mapToArray (fun (id, value) -> transformBindingAsExpr com ctx id value)

                Expression.sequenceExpression (Array.append values [| com.TransformAsExpr(ctx, body) |])
            else
                iife com ctx expr

        | Fable.Sequential exprs ->
            List.mapToArray (fun e -> com.TransformAsExpr(ctx, e)) exprs
            |> Expression.sequenceExpression

        | Fable.Emit(info, _, range) ->
            if info.IsStatement then
                iife com ctx expr
            else
                transformEmit com ctx range info

        // These cannot appear in expression position in JS, must be wrapped in a lambda
        | Fable.WhileLoop _
        | Fable.ForLoop _
        | Fable.TryCatch _ -> iife com ctx expr

        | Fable.Extended(instruction, _) ->
            match instruction with
            | Fable.Curry(e, arity) -> transformCurry com ctx e arity
            | Fable.Throw _
            | Fable.Debugger -> iife com ctx expr

        | Fable.Quote(quotedExpr, _isTyped, _r) ->
            let emitted = QuotationEmitter.emitQuotedExpr com quotedExpr
            transformAsExpr com ctx emitted

    let rec transformAsStatements (com: IBabelCompiler) ctx returnStrategy (expr: Fable.Expr) : Statement array =
        match expr with
        | Fable.Unresolved(_, _, r) ->
            addError com [] r "Unexpected unresolved expression"
            [||]

        | Fable.Extended(kind, r) ->
            match kind with
            | Fable.Curry(e, arity) -> [| transformCurry com ctx e arity |> resolveExpr e.Type returnStrategy |]
            | Fable.Throw(Some(TransformExpr com ctx e), _) -> [| Statement.throwStatement (e, ?loc = r) |]
            | Fable.Throw(None, _) -> [| Statement.throwStatement (Expression.nullLiteral (), ?loc = r) |]
            | Fable.Debugger -> [| Statement.debuggerStatement (?loc = r) |]

        | Fable.TypeCast(e, t) -> [| transformCast com ctx t e |> resolveExpr t returnStrategy |]

        | Fable.Value(kind, r) -> [| transformValue com ctx r kind |> resolveExpr kind.Type returnStrategy |]

        | Fable.IdentExpr id -> [| transformIdent com ctx id |> resolveExpr id.Type returnStrategy |]

        | Fable.Import({
                           Selector = selector
                           Path = path
                       },
                       t,
                       r) -> [| transformImport com ctx r selector path |> resolveExpr t returnStrategy |]

        | Fable.Test(expr, kind, range) ->
            [|
                transformTest com ctx range kind expr
                |> resolveExpr Fable.Boolean returnStrategy
            |]

        | Fable.Lambda(arg, body, name) ->
            [|
                transformFunctionWithAnnotations com ctx name None [ arg ] body
                |> makeArrowFunctionExpression name
                |> resolveExpr expr.Type returnStrategy
            |]

        | Fable.Delegate _ -> [| transformAsExpr com ctx expr |> resolveExpr expr.Type returnStrategy |]

        | Fable.ObjectExpr(members, typ, baseCall) ->
            [|
                transformObjectExpr com ctx typ members baseCall
                |> resolveExpr typ returnStrategy
            |]

        | Fable.Call(callee, info, typ, range) -> transformCallAsStatements com ctx range typ returnStrategy callee info

        | Fable.CurriedApply(callee, args, typ, range) ->
            transformCurriedApplyAsStatements com ctx range typ returnStrategy callee args

        | Fable.Emit(info, _, range) when List.contains "fickl-for-await" info.CallInfo.Tags ->
            if not ctx.IsNativeAsync then
                addError com [] range "for await requires a native async computation"
                [||]
            else
                match info.CallInfo.Args with
                | [ values; Fable.Delegate([ arg ], body, _, _) ] ->
                    let right = com.TransformAsExpr(ctx, values)
                    let ctx = { ctx with BoundTypes = Map.add arg.Name arg.Type ctx.BoundTypes }
                    let statements = com.TransformAsStatements(ctx, None, body)

                    [|
                        ForOfStatement(Identifier.identifier arg.Name, right, BlockStatement statements, true, range)
                    |]
                | _ ->
                    addError com [] range "Invalid native asynchronous iteration"
                    [||]

        | Fable.Emit(info, _, range) when List.contains "fickl-generator-return" info.CallInfo.Tags ->
            if ctx.NativeGeneratorType.IsNone then
                addError com [] range "Native yields and returns must be inside a jsGenerator computation"
                [||]
            else
                match info.CallInfo.Args with
                | [ value ] -> com.TransformAsStatements(ctx, Some Return, value)
                | _ ->
                    addError com [] range "Invalid native generator return"
                    [||]

        | Fable.Emit(info, t, range) ->
            let e = transformEmit com ctx range info

            if info.IsStatement then
                [| ExpressionStatement(e) |] // Ignore the return strategy
            else
                [| resolveExpr t returnStrategy e |]

        | Fable.Operation(kind, _, t, range) ->
            [| transformOperation com ctx range kind |> resolveExpr t returnStrategy |]

        | Fable.Get(expr, kind, t, range) ->
            [| transformGet com ctx range t expr kind |> resolveExpr t returnStrategy |]

        | Fable.Let(ident, value, body) ->
            let binding = transformBindingAsStatements com ctx ident value
            let ctx = { ctx with BoundTypes = Map.add ident.Name ident.Type ctx.BoundTypes }

            Array.append binding (transformAsStatements com ctx returnStrategy body)

        | Fable.LetRec(bindings, body) ->
            let ctx =
                { ctx with
                    BoundTypes =
                        (ctx.BoundTypes, bindings)
                        ||> List.fold (fun types (id, _) -> Map.add id.Name id.Type types)
                }

            let bindings =
                bindings
                |> Seq.collect (fun (i, v) -> transformBindingAsStatements com ctx i v)
                |> Seq.toArray

            Array.append bindings (transformAsStatements com ctx returnStrategy body)

        | Fable.Set(expr, kind, typ, value, range) ->
            [|
                transformSet com ctx range expr typ value kind
                |> resolveExpr expr.Type returnStrategy
            |]

        | Fable.IfThenElse(guardExpr, thenExpr, elseExpr, r) ->
            let asStatement =
                match returnStrategy with
                | None
                | Some ReturnUnit -> true
                | Some(Target _) -> true // Compile as statement so values can be bound
                | Some(Assign _) -> (isJsStatement ctx false thenExpr) || (isJsStatement ctx false elseExpr)
                | Some Return ->
                    Option.isSome ctx.TailCallOpportunity
                    || (isJsStatement ctx false thenExpr)
                    || (isJsStatement ctx false elseExpr)

            if asStatement then
                transformIfStatement com ctx r returnStrategy guardExpr thenExpr elseExpr
            else
                let guardExpr' = transformAsExpr com ctx guardExpr
                let thenExpr' = transformAsExpr com ctx thenExpr
                let elseExpr' = transformAsExpr com ctx elseExpr

                [|
                    Expression.conditionalExpression (guardExpr', thenExpr', elseExpr', ?loc = r)
                    |> resolveExpr thenExpr.Type returnStrategy
                |]

        | Fable.Sequential statements ->
            let lasti = (List.length statements) - 1

            statements
            |> List.mapiToArray (fun i statement ->
                let ret =
                    if i < lasti then
                        None
                    else
                        returnStrategy

                com.TransformAsStatements(ctx, ret, statement)
            )
            |> Array.concat

        | Fable.TryCatch(body, catch, finalizer, r) ->
            transformTryCatch com ctx r returnStrategy (body, catch, finalizer)

        | Fable.DecisionTree(expr, targets) -> transformDecisionTreeAsStatements com ctx returnStrategy targets expr

        | Fable.DecisionTreeSuccess(idx, boundValues, _) ->
            transformDecisionTreeSuccessAsStatements com ctx returnStrategy idx boundValues

        | Fable.WhileLoop(TransformExpr com ctx guard, body, range) ->
            // Hoist vars within the loop so if a closure captures the variable
            // it doesn't change in the next iteration, see #4031
            let body = transformBlockWithVarHoisting com ctx None body
            [| Statement.whileStatement (guard, body, ?loc = range) |]

        | Fable.ForLoop(var, TransformExpr com ctx start, TransformExpr com ctx limit, body, isUp, range) ->
            let op1, op2 =
                if isUp then
                    BinaryOperator.BinaryLessOrEqual, UpdateOperator.UpdatePlus
                else
                    BinaryOperator.BinaryGreaterOrEqual, UpdateOperator.UpdateMinus

            [|
                Statement.forStatement (
                    // Hoist vars within the loop so if a closure captures the variable
                    // it doesn't change in the next iteration, see #4031
                    transformBlockWithVarHoisting com ctx None body,
                    VariableDeclaration.variableDeclaration (
                        Let,
                        var.Name,
                        init = start,
                        ?annotation = makeTypeAnnotationIfTypeScript com ctx var.Type (Some start)
                    ),
                    Expression.binaryExpression (op1, identAsExpr var, limit),
                    Expression.updateExpression (op2, false, identAsExpr var),
                    ?loc = range
                )
            |]

        | Fable.Quote(quotedExpr, _isTyped, _r) ->
            let emitted = QuotationEmitter.emitQuotedExpr com quotedExpr
            [| transformAsExpr com ctx emitted |> resolveExpr Fable.Any returnStrategy |]

    let transformFunction com ctx name (args: Fable.Ident list) (body: Fable.Expr) : Parameter array * BlockStatement =
        let tailcallChance =
            Option.map (fun name -> NamedTailCallOpportunity(com, ctx, name, args) :> ITailCallOpportunity) name

        let args = FSharp2Fable.Util.discardUnitArg args
        let mutable isTailCallOptimized = false

        let ctx =
            { ctx with
                TailCallOpportunity = tailcallChance
                OptimizeTailCall = fun () -> isTailCallOptimized <- true
                BoundTypes =
                    (ctx.BoundTypes, args)
                    ||> List.fold (fun types id -> Map.add id.Name id.Type types)
            }

        let returnStrategy =
            if body.Type = Fable.Unit then
                ReturnUnit
            else
                Return

        let body = transformBlockWithVarHoisting com ctx (Some returnStrategy) body

        let args, body =
            match isTailCallOptimized, tailcallChance with
            | true, Some tc ->
                // Replace args, see NamedTailCallOpportunity constructor
                let args' =
                    List.zip args tc.Args
                    |> List.map (fun (id, tcArg) ->
                        let ta = makeArgTypeAnnotation com ctx id
                        Parameter.parameter (tcArg, ?typeAnnotation = ta)
                    )

                let varDecls =
                    List.zip args tc.Args
                    |> List.map (fun (id, tcArg) -> id, Some(Expression.identifier (tcArg)))
                    |> multiVarDeclaration com ctx Const

                let body = Array.append [| varDecls |] body.Body
                // Make sure we don't get trapped in an infinite loop, see #1624
                let body = BlockStatement(Array.append body [| Statement.breakStatement () |])

                let body =
                    Statement.labeledStatement (
                        Identifier.identifier (tc.Label),
                        Statement.whileStatement (Expression.booleanLiteral (true), body)
                    )
                    |> Array.singleton
                    |> BlockStatement

                args', body
            | _ ->
                args
                |> List.map (fun a ->
                    let ta = makeArgTypeAnnotation com ctx a
                    Parameter.parameter (a.Name, ?typeAnnotation = ta)
                ),
                body

        args |> List.toArray, body

    let declareEntryPoint (com: IBabelCompiler) _ctx (funcExpr: Expression) =
        let argv =
            match com.Options.Language with
            // Don't refer to `process` directly, its declaration needs @types/node
            | TypeScript -> emitExpression None "(globalThis as any).process?.argv?.slice(2) ?? []" []
            | _ -> emitExpression None "typeof process === 'object' ? process.argv.slice(2) : []" []

        let main = Expression.callExpression (funcExpr, [| argv |])
        // Don't exit the process after leaving main, as there may be a server running
        // ExpressionStatement(emitExpression funcExpr.loc "process.exit($0)" [main], ?loc=funcExpr.loc)
        PrivateModuleDeclaration(ExpressionStatement(main))

    let asModuleDeclaration isPublic decl =
        if not isPublic then
            PrivateModuleDeclaration(decl |> Declaration)
        else
            ExportNamedDeclaration(decl)

    let declareModuleMember com ctx (expr: Expression) (info: ModuleDecl) =
        match expr with
        | ClassExpression(members, _id, isAbstract, superClass, implements, typeParameters, _loc) ->
            Declaration.classDeclaration (
                members,
                id = Identifier.identifier (info.Name),
                ?isAbstract = isAbstract,
                ?superClass = superClass,
                typeParameters = typeParameters,
                implements = implements,
                ?doc = info.JsDoc
            )
        | FunctionExpression(_, parameters, body, returnType, typeParameters, _) ->
            Declaration.functionDeclaration (
                parameters,
                body,
                id = Identifier.identifier (info.Name),
                ?returnType = returnType,
                typeParameters = typeParameters,
                ?doc = info.JsDoc
            )
        | _ ->
            let kind =
                if info.IsMutable then
                    Let
                else
                    Const

            let annotation =
                // Public mutable values are compiled as functions so we omit the type in those cases
                if info.IsMutable && info.IsPublic then
                    None
                else
                    makeTypeAnnotationIfTypeScript com ctx info.Type (Some expr)

            Declaration.variableDeclaration (kind, info.Name, init = expr, ?annotation = annotation)

        |> asModuleDeclaration info.IsPublic

    let sanitizeName fieldName =
        fieldName |> Naming.sanitizeJsIdentForbiddenChars |> Naming.checkJsKeywords

    [<RequireQualifiedAccess>]
    module DefaultValue =

        open Replacements.Util
        open Fable.Transforms

        /// Mirrors `Fable.Transforms.JS.Replacements.defaultof` for the types we can handle
        /// without the full FSharp2Fable compiler context (which that function requires).
        let rec forType (com: IBabelCompiler) ctx (t: Fable.Type) : Expression =
            match t with
            | Fable.Nullable _ -> Expression.nullLiteral ()
            // Struct tuples are value types — initialize each element to its zero
            | Fable.Tuple(args, true) -> Expression.arrayExpression (args |> List.map (forType com ctx) |> List.toArray)
            | Fable.Boolean -> Expression.booleanLiteral false
            | Fable.Char -> Expression.stringLiteral "\u0000"
            | Fable.Number(kind, uom) ->
                com.TransformAsExpr(ctx, Fable.NumberConstant(Fable.NumberValue.GetZero kind, uom) |> makeValue None)
            | Builtin BclTimeOnly when com.Options.JsTemporal ->
                libCall com ctx None "TimeOnlyTemporal" "minValue" [] []
            | Builtin BclTimeSpan when com.Options.JsTemporal -> libCall com ctx None "TimeSpanTemporal" "zero" [] []
            | Builtin(BclTimeSpan | BclTimeOnly) -> Expression.numericLiteral 0
            | Builtin BclGuid -> Expression.stringLiteral "00000000-0000-0000-0000-000000000000"
            | Builtin BclDateTime -> libCall com ctx None (JS.Replacements.dateTimeModule com) "minValue" [] []
            | Builtin BclDateTimeOffset ->
                libCall com ctx None (JS.Replacements.dateTimeOffsetModule com) "minValue" [] []
            | Builtin BclDateOnly -> libCall com ctx None (JS.Replacements.dateOnlyModule com) "minValue" [] []
            | _ -> libCall com ctx None "Util" "defaultOf" [] []

    let getEntityFieldsAsIdents (ent: Fable.Entity) =
        ent.FSharpFields
        |> List.choose (fun field ->
            if field.IsStatic then
                None
            else
                let name = sanitizeName field.Name
                let typ = field.FieldType
                Some { makeTypedIdent typ name with IsMutable = field.IsMutable }
        )

    /// The base type an explicit class extends: another explicit class (#87) or a class with a primary constructor (#96)
    let tryExtendedBaseType (ent: Fable.Entity) =
        FSharp2Fable.Util.tryExplicitBaseType ent
        |> Option.orElseWith (fun () -> FSharp2Fable.Util.tryPrimaryBaseType ent |> Option.map fst)

    /// The parameters an explicit class's constructor takes before its own fields and passes to `super`: the fields of
    /// its explicit base classes (#87), after the parameters of a primary constructor at the root (#96). Each has its
    /// type in terms of the class's generic parameters and, if it is a base field, the field
    let rec getExplicitBaseParams
        (com: IBabelCompiler)
        (ent: Fable.Entity)
        : (string * Fable.Type * Fable.Field option) list
        =
        let resolve (baseType: Fable.DeclaredType) (baseEnt: Fable.Entity) parameters =
            let genArgs =
                let names = baseEnt.GenericParameters |> List.map _.Name

                if List.sameLength names baseType.GenericArgs then
                    List.zip names baseType.GenericArgs |> Map
                else
                    Map.empty

            parameters
            |> List.map (fun (name, typ, field) -> name, resolveInlineType genArgs typ, field)

        match FSharp2Fable.Util.tryExplicitBaseType ent with
        | Some baseType ->
            let baseEnt = com.GetEntity(baseType.Entity)

            let ownFields =
                baseEnt.FSharpFields
                |> List.filter (fun field -> not field.IsStatic)
                |> List.map (fun field -> field.Name, field.FieldType, Some field)

            getExplicitBaseParams com baseEnt @ ownFields |> resolve baseType baseEnt
        | None ->
            match FSharp2Fable.Util.tryPrimaryBaseType ent with
            | Some(baseType, parameters) ->
                parameters
                |> List.map (fun (name, typ) -> name, typ, None)
                |> resolve baseType (com.GetEntity(baseType.Entity))
            | None -> []

    /// An explicit constructor of a class that extends its base, `{ inherit Base(...); Field = ... }`, calls the base
    /// constructor and then builds the object, but the class's constructor also takes the parameters it passes to
    /// `super`. When the base constructor is the primary constructor of the base, pass its arguments instead of calling
    /// it (#96), unless computing the class's own fields could have side effects, which must follow the base
    /// constructor's. When every parameter is a base field, bind the base object and pass its fields on (#87).
    /// Otherwise, as when the class extends an explicit class with a primary constructor at the root, the arguments of
    /// that constructor are not at hand, so make the base object an instance of the class and set its own fields
    let passExplicitBaseArgs (com: IBabelCompiler) ctx (ent: Fable.Entity) (body: Fable.Expr) =
        match tryExtendedBaseType ent with
        | None -> body
        | Some baseType ->
            let baseParams = getExplicitBaseParams com ent

            let baseFields =
                baseParams
                |> List.choose (fun (_, typ, field) -> field |> Option.map (fun field -> field, typ))

            let tryBaseConsCall expr =
                match expr with
                | Fable.Call(_, ({ MemberRef = Some memberRef } as info), _, _) ->
                    match com.TryGetMember(memberRef) with
                    | Some memb when
                        memb.IsConstructor
                        && memb.DeclaringEntity
                           |> Option.exists (fun e -> e.FullName = baseType.Entity.FullName)
                        ->
                        Some(memb, info)
                    | _ -> None
                | _ -> None

            let replaceNewObj makeExpr expr =
                expr
                |> visitFromOutsideIn (
                    function
                    | Fable.Value(Fable.NewRecord(args, entRef, genArgs), r) when entRef.FullName = ent.FullName ->
                        makeExpr args entRef genArgs r |> Some
                    | _ -> None
                )

            let rec isPure expr =
                match expr with
                | Fable.Operation((Fable.Binary(_, left, right) | Fable.Logical(_, left, right)), _, _, _) ->
                    isPure left && isPure right
                | Fable.Operation(Fable.Unary(_, operand), _, _, _) -> isPure operand
                | expr -> not (canHaveSideEffects com expr)

            let hasPureOwnFields rest =
                rest
                |> deepExists (
                    function
                    | Fable.Value(Fable.NewRecord(args, entRef, _), _) when entRef.FullName = ent.FullName ->
                        not (List.forall isPure args)
                    | _ -> false
                )
                |> not

            let passPrimaryArgs (info: Fable.CallInfo) rest =
                let args =
                    if List.isEmpty baseParams then
                        []
                    else
                        info.Args

                let bindings, args =
                    args
                    |> List.map (fun arg ->
                        if canHaveSideEffects com arg then
                            let ident = getUniqueNameInDeclarationScope ctx "arg" |> makeTypedIdent arg.Type
                            Some(ident, arg), Fable.IdentExpr ident
                        else
                            None, arg
                    )
                    |> List.unzip

                let rest =
                    rest
                    |> replaceNewObj (fun ownArgs entRef genArgs r ->
                        Fable.Value(Fable.NewRecord(args @ ownArgs, entRef, genArgs), r)
                    )

                List.foldBack (fun (ident, value) rest -> Fable.Let(ident, value, rest)) (List.choose id bindings) rest

            let passFields (baseIdent: Fable.Ident) rest =
                rest
                |> replaceNewObj (fun ownArgs entRef genArgs r ->
                    let baseArgs =
                        baseFields
                        |> List.map (fun (field, typ) ->
                            let kind = Fable.FieldInfo.Create(field.Name, typ, field.IsMutable)
                            Fable.Get(Fable.IdentExpr baseIdent, kind, typ, None)
                        )

                    Fable.Value(Fable.NewRecord(baseArgs @ ownArgs, entRef, genArgs), r)
                )

            let adoptBase (baseIdent: Fable.Ident) rest =
                rest
                |> replaceNewObj (fun ownArgs entRef genArgs r ->
                    let fields =
                        ent.FSharpFields
                        |> List.filter (fun field -> not field.IsStatic)
                        |> List.mapi (fun i field ->
                            let name = field.Name.Replace("\\", "\\\\").Replace("\"", "\\\"")
                            $"\"%s{name}\": $%i{i + 2}"
                        )
                        |> String.concat ", "

                    let classRef = FSharp2Fable.Util.entityIdent com entRef

                    $"Object.assign(Object.setPrototypeOf($0, $1.prototype), {{ %s{fields} }})"
                    |> emitExpr
                        r
                        (Fable.DeclaredType(entRef, genArgs))
                        (Fable.IdentExpr baseIdent :: classRef :: ownArgs)
                )

            let rec rewrite expr =
                match expr with
                | Fable.Let(ident, value, body) -> Fable.Let(ident, value, rewrite body)
                | Fable.Sequential exprs ->
                    let baseConsCall =
                        exprs
                        |> List.indexed
                        |> List.tryPick (fun (i, e) -> tryBaseConsCall e |> Option.map (fun call -> i, call))

                    match baseConsCall with
                    | Some(i, (memb, info)) ->
                        let baseCall = exprs[i]

                        let rest =
                            match List.skip (i + 1) exprs with
                            | [ e ] -> e
                            | rest -> Fable.Sequential rest

                        let bindBase build =
                            let baseIdent =
                                getUniqueNameInDeclarationScope ctx "base" |> makeTypedIdent baseCall.Type

                            Fable.Let(baseIdent, baseCall, build baseIdent rest)

                        let rest =
                            if
                                FSharp2Fable.Util.isPrimaryConstructor memb
                                && Option.isSome (FSharp2Fable.Util.tryPrimaryBaseType ent)
                                && hasPureOwnFields rest
                            then
                                passPrimaryArgs info rest
                            elif List.sameLength baseFields baseParams then
                                bindBase passFields
                            else
                                bindBase adoptBase

                        Fable.Sequential(List.take i exprs @ [ rest ])
                    | None ->
                        match List.rev exprs with
                        | last :: init -> Fable.Sequential(List.rev init @ [ rewrite last ])
                        | [] -> expr
                | expr -> expr

            rewrite body

    /// Whether a member overrides a member of a base class, rather than implementing an interface. `info` is the
    /// member the declaration implements, if any. System.Object members are left out: TypeScript's base type only has
    /// them when the base class declares them (see `overrideNames`)
    let isBaseClassOverride
        (com: IBabelCompiler)
        (ent: Fable.Entity)
        (info: Fable.MemberFunctionOrValue)
        (memb: Fable.MemberDecl)
        =
        com.IsTypeScript
        && Option.isSome memb.ImplementedSignatureRef
        && info.DeclaringEntity
           // A default implementation implements its own class's abstract member
           |> Option.filter (fun entRef -> entRef.FullName <> Types.object && entRef.FullName <> ent.FullName)
           |> Option.bind com.TryGetEntity
           |> Option.exists (fun ent -> not ent.IsInterface)

    /// fickl: whether an instance member hides a base class member of the same name and key instead of overriding it.
    /// TypeScript's `noImplicitOverride` needs `override` on it all the same
    let hidesBaseMember (com: IBabelCompiler) (info: Fable.MemberFunctionOrValue) =
        let isProperty (memb: Fable.MemberFunctionOrValue) =
            memb.IsProperty || memb.IsGetter || memb.IsSetter

        let symbolKey (memb: Fable.MemberFunctionOrValue) =
            memb.Attributes
            |> Seq.tryFind (fun att -> att.Entity.FullName = Atts.ficklSymbolKey)
            |> Option.map _.ConstructorArgs

        let hides (memb: Fable.MemberFunctionOrValue) =
            memb.IsInstance
            && not memb.IsInline
            && not memb.IsDispatchSlot
            && memb.DisplayName = info.DisplayName
            && isProperty memb = isProperty info
            && symbolKey memb = symbolKey info

        let rec baseHides (ent: Fable.Entity) =
            match ent.BaseType |> Option.bind (fun baseType -> com.TryGetEntity(baseType.Entity)) with
            | Some baseEnt when baseEnt.FullName <> Types.object ->
                Seq.exists hides baseEnt.MembersFunctionsAndValues || baseHides baseEnt
            | _ -> false

        com.IsTypeScript
        && info.IsInstance
        && info.DeclaringEntity |> Option.bind com.TryGetEntity |> Option.exists baseHides

    /// Object.prototype members are in the type of any base class
    let objectPrototypeNames = [ "toString"; "toLocaleString"; "valueOf" ]

    /// Names of the members a class's emitted base class declares, apart from F# overrides `isBaseClassOverride` finds
    let overrideNames (com: IBabelCompiler) (ent: Fable.Entity) =
        let rec ancestorOverrides (ent: Fable.Entity) =
            match ent.BaseType |> Option.bind (fun baseType -> com.TryGetEntity(baseType.Entity)) with
            | Some baseEnt when baseEnt.FullName <> Types.object ->
                [
                    for memb in baseEnt.MembersFunctionsAndValues do
                        if memb.IsOverrideOrExplicitInterfaceImplementation then
                            match memb.CompiledName with
                            | "ToString" -> "toString"
                            | name -> name
                    yield! ancestorOverrides baseEnt
                ]
            | _ -> []

        set
            [
                yield! objectPrototypeNames
                // Members of Record, Union and FSharpException in fable-library
                if
                    ent.IsFSharpRecord
                    || ent.IsValueType
                    || ent.IsFSharpUnion
                    || ent.IsFSharpExceptionDeclaration
                then
                    yield! [ "toJSON"; "GetHashCode"; "Equals"; "CompareTo" ]
                yield! ancestorOverrides ent
            ]

    /// TypeScript's `noImplicitOverride` needs `override` on a member the base class declares,
    /// and rejects it in a class without a base class
    let markOverrides (com: IBabelCompiler) (names: Set<string>) hasBaseClass (members: ClassMember array) =
        if not com.IsTypeScript then
            members
        else
            let rec mark memb =
                match memb with
                | DecoratedClassMember(decorators, memb) -> DecoratedClassMember(decorators, mark memb)
                | ClassMethod(kind,
                              parameters,
                              body,
                              isStatic,
                              isAbstract,
                              isOverride,
                              returnType,
                              typeParameters,
                              loc,
                              doc) when not isStatic && not isAbstract ->
                    let isOverride =
                        match kind with
                        | ClassFunction(key, false)
                        | ClassGetter(key, false)
                        | ClassSetter(key, false) ->
                            match key with
                            | Expression.Identifier(Identifier(name, _))
                            | Expression.Literal(Literal.StringLiteral(StringLiteral(name, _))) ->
                                hasBaseClass && (isOverride || names.Contains name)
                            | _ -> hasBaseClass && isOverride
                        | _ -> hasBaseClass && isOverride

                    ClassMethod(
                        kind,
                        parameters,
                        body,
                        isStatic,
                        isAbstract,
                        isOverride,
                        returnType,
                        typeParameters,
                        loc,
                        doc
                    )
                | memb -> memb

            Array.map mark members

    let declareClassWithParams
        (com: IBabelCompiler)
        ctx
        (ent: Fable.Entity)
        entName
        (doc: string option)
        (consArgs: Parameter array)
        (consArgsModifiers: AccessModifier array)
        (consBody: BlockStatement)
        (superClass: SuperClass option)
        classMembers
        typeParamDecl
        =
        let implements =
            if com.IsTypeScript then
                let isUnion = ent.IsFSharpUnion

                ent.AllInterfaces
                |> Seq.choose (fun ifc ->
                    match ifc.Entity.FullName with
                    // Discard non-generic versions of IEquatable & IComparable
                    | Types.iequatable
                    | Types.iStructuralEquatable
                    | Types.iequalityComparer
                    | Types.icomparable
                    | Types.iStructuralComparable
                    | Types.ienumerable
                    | Types.ienumerator
                    // Marker base of JS.AsyncIterable<'T>; TypeScript's AsyncIterable needs a type argument
                    | "Fable.Core.JS.AsyncIterable" -> None
                    | Types.ienumerableGeneric ->
                        // makeFableLibImportTypeAnnotation com ctx ifc.GenericArgs "Util" "IEnumerable" |> Some
                        makeNativeTypeAnnotation com ctx ifc.GenericArgs "Iterable" |> Some
                    | Types.ienumeratorGeneric ->
                        makeFableLibImportTypeAnnotation com ctx ifc.GenericArgs "Util" "IEnumerator"
                        |> Some
                    | Types.iequatableGeneric
                    | Types.icomparableGeneric when isUnion -> None
                    | _ ->
                        com.GetEntity(ifc.Entity)
                        |> makeEntityTypeAnnotation com ctx ifc.GenericArgs
                        |> Some
                )
                |> Seq.toArray
                |> fun interfaces ->
                    [|
                        yield! interfaces
                        if FSharp2Fable.Util.hasInterface Types.idisposable ent then
                            yield makeNativeTypeAnnotation com ctx [] "Disposable"
                        if FSharp2Fable.Util.hasInterface Types.iasyncdisposable ent then
                            yield makeNativeTypeAnnotation com ctx [] "AsyncDisposable"
                    |]
                |> Some
            else
                None

        let classCons =
            ClassMember.classMethod (ClassPrimaryConstructor consArgsModifiers, consArgs, Some consBody)

        let classFields =
            if com.IsTypeScript && not ent.IsFSharpUnion then
                ent.FSharpFields
                |> List.filter (fun field -> not (ctx.NativeAutoProperties.ContainsKey field.Name))
                |> List.mapToArray (fun field ->
                    let prop, isComputed = SymbolKeys.fieldKey com ctx field
                    let ta = makeFieldAnnotationOf com ctx field
                    // Static fields need to be initialized by static constructor
                    let am =
                        if field.IsMutable || field.IsStatic then
                            None
                        else
                            Some Readonly

                    ClassMember.classProperty (
                        prop,
                        isComputed = isComputed,
                        isStatic = field.IsStatic,
                        typeAnnotation = ta,
                        ?accessModifier = am
                    )
                )
            else
                Array.empty

        let isAbstract =
            if com.IsTypeScript then
                Some ent.IsAbstractClass
            else
                None

        let hasExplicitDisposal name =
            ent.MembersFunctionsAndValues
            |> Seq.exists (fun memb ->
                (memb.CompiledName = name
                 && (hasAttribute Atts.attachMembers ent.Attributes
                     || memb.IsOverrideOrExplicitInterfaceImplementation))
                || SymbolKeys.isWellKnownMember name memb
            )

        let members =
            [|
                yield! classFields
                if
                    (ent.IsFSharpRecord || ent.IsValueType)
                    && (ent.FSharpFields
                        |> List.exists (FSharp2Fable.FsField.TrySymbolKey >> Option.isSome))
                then
                    let fields =
                        ent.FSharpFields
                        |> List.filter (fun field -> not field.IsStatic)
                        |> List.map (fun field ->
                            let key =
                                match SymbolKeys.tryField com ctx field with
                                | Some key -> key
                                | None -> Expression.stringLiteral (sanitizeMemberName field.Name)

                            Expression.arrayExpression [| Expression.stringLiteral field.Name; key |]
                        )
                        |> List.toArray
                        |> Expression.arrayExpression

                    yield
                        ClassMember.classProperty (
                            libValue com ctx "Types" "recordFieldsSymbol",
                            value = fields,
                            isStatic = true,
                            isComputed = true
                        )
                yield classCons
                yield! classMembers
                for interfaceName, symbolName, isAsync in
                    [
                        Types.idisposable, "Symbol.dispose", false
                        Types.iasyncdisposable, "Symbol.asyncDispose", true
                    ] do
                    if
                        FSharp2Fable.Util.hasInterface interfaceName ent
                        && not (hasExplicitDisposal symbolName)
                    then
                        let key, body, returnType = nativeDisposalBridge com ctx isAsync

                        yield
                            ClassMember.classMethod (
                                ClassFunction(key, true),
                                [||],
                                Some body,
                                ?returnType = returnType
                            )
            |]

        let members =
            markOverrides com (overrideNames com ent) (Option.isSome superClass) members

        let classExpr =
            Expression.classExpression (
                members,
                ?isAbstract = isAbstract,
                ?superClass = superClass,
                ?typeParameters = typeParamDecl,
                ?implements = implements
            )

        ModuleDecl(entName, isPublic = ent.IsPublic, ?doc = doc)
        |> declareModuleMember com ctx classExpr
        |> NativeDecorators.decorateClass com ctx ent

    let declareClass
        (com: IBabelCompiler)
        ctx
        (ent: Fable.Entity)
        entName
        doc
        consArgs
        consBody
        superClass
        classMembers
        =
        if com.IsTypeScript then
            FSharp2Fable.Util.getEntityGenArgs ent |> makeTypeParamDecl com ctx |> Some
        else
            None
        |> declareClassWithParams com ctx ent entName doc consArgs [||] consBody superClass classMembers

    /// Emits an IIFE that zero-initializes all static [<DefaultValue>] fields of a class.
    ///
    /// ```
    /// [<DefaultValue>]
    /// static val mutable MyField: int
    /// ```
    ///
    /// This mirrors the pattern used for `static let mutable` bindings, which FCS compiles
    /// to a `.cctor` that Fable emits as an IIFE.
    let declareDefaultValueStaticFieldInits
        (com: IBabelCompiler)
        ctx
        (ent: Fable.Entity)
        (entName: string)
        : ModuleDeclaration list
        =
        let defaultValueFields =
            ent.FSharpFields
            |> List.filter (fun f -> f.IsStatic && f.HasDefaultValueAttribute)

        if defaultValueFields.IsEmpty then
            []
        else
            let classIdent = Expression.identifier entName

            let assignments =
                defaultValueFields
                |> List.map (fun field ->
                    let left = SymbolKeys.fieldAccess com ctx None field classIdent
                    let right = DefaultValue.forType com ctx field.FieldType
                    assign None left right |> ExpressionStatement
                )
                |> List.toArray

            let iife =
                Expression.callExpression (Expression.functionExpression ([||], BlockStatement(assignments)), [||])

            [ iife |> ExpressionStatement |> PrivateModuleDeclaration ]

    let declareTypeReflection (com: IBabelCompiler) ctx (ent: Fable.Entity) entName : ModuleDeclaration =
        let ta =
            if com.IsTypeScript then
                makeFableLibImportTypeAnnotation com ctx [] "Reflection" "TypeInfo" |> Some
            else
                None

        let genArgs =
            Array.init (ent.GenericParameters.Length) (fun i -> "gen" + string<int> i |> makeIdent)

        let generics = genArgs |> Array.map identAsExpr
        let body = transformReflectionInfo com ctx None ent generics

        let args =
            genArgs
            |> Array.map (fun x -> Parameter.parameter (x.Name, ?typeAnnotation = ta))

        let returnType = ta
        let fnExpr = makeFunctionExpression None (args, body, returnType, None)

        ModuleDecl(entName + Naming.reflectionSuffix, isPublic = ent.IsPublic)
        |> declareModuleMember com ctx fnExpr

    let hasClassMethod name (classMembers: ClassMember array) =
        classMembers
        |> Array.exists (
            function
            | ClassMethod(ClassFunction(Expression.Identifier(Identifier(key, _)), _), _, _, _, _, _, _, _, _, _) ->
                key = name
            | _ -> false
        )

    let withReferenceEquality (com: IBabelCompiler) ctx (ent: Fable.Entity) (classMembers: ClassMember array) =
        let isReferenceEqualityType =
            (ent.IsFSharpRecord || ent.IsFSharpUnion)
            && not ent.IsValueType
            && not (FSharp2Fable.Util.hasStructuralEquality ent)

        if not isReferenceEqualityType then
            classMembers
        else
            let ifTypeScript annotation =
                if com.IsTypeScript then
                    Some annotation
                else
                    None

            let makeMethod name parameters returnExpr returnType =
                ClassMember.classMethod (
                    ClassFunction(Expression.identifier name, false),
                    parameters,
                    Some(BlockStatement [| Statement.returnStatement returnExpr |]),
                    ?returnType = ifTypeScript returnType
                )

            [|
                yield! classMembers
                if not (hasClassMethod "Equals" classMembers) then
                    let other =
                        Parameter.parameter ("other", ?typeAnnotation = ifTypeScript AnyTypeAnnotation)

                    makeMethod
                        "Equals"
                        [| other |]
                        (Expression.binaryExpression (BinaryEqual, thisExpr, Expression.identifier other.Name))
                        BooleanTypeAnnotation
                if not (hasClassMethod "GetHashCode" classMembers) then
                    makeMethod
                        "GetHashCode"
                        [||]
                        (libCall com ctx None "Util" "physicalHash" [] [ thisExpr ])
                        NumberTypeAnnotation
            |]

    let declareType
        (com: IBabelCompiler)
        ctx
        (ent: Fable.Entity)
        entName
        doc
        (consArgs: Parameter array)
        (consBody: BlockStatement)
        baseExpr
        classMembers
        : ModuleDeclaration list
        =
        let typeDeclaration =
            declareClass com ctx ent entName doc consArgs consBody baseExpr classMembers

        let fieldInits = declareDefaultValueStaticFieldInits com ctx ent entName

        if com.Options.NoReflection then
            [ typeDeclaration; yield! fieldInits ]
        else
            let reflectionDeclaration = declareTypeReflection com ctx ent entName

            [ typeDeclaration; reflectionDeclaration; yield! fieldInits ]

    let hasAttribute fullName (atts: Fable.Attribute seq) =
        atts |> Seq.exists (fun att -> att.Entity.FullName = fullName)

    let hasAnyAttribute fullNames (atts: Fable.Attribute seq) =
        let fullNames = set fullNames

        atts |> Seq.exists (fun att -> Set.contains att.Entity.FullName fullNames)

    let tryFindAnyAttribute fullNames (atts: Fable.Attribute seq) : (string * obj list) option =
        let fullNames = set fullNames

        atts
        |> Seq.tryPick (fun att ->
            let fullName = att.Entity.FullName

            if Set.contains fullName fullNames then
                Some(fullName, att.ConstructorArgs)
            else
                None
        )

    let tryFindAnyEntAttribute fullNames (ent: Fable.Entity) : (string * obj list) option =
        tryFindAnyAttribute fullNames ent.Attributes

    let rec private tryNativeAsyncBody =
        function
        | Fable.Call(Fable.Delegate([], body, _, tags), info, _, _) when
            List.contains "fickl-async-function" tags && info.Args.IsEmpty
            ->
            Some body
        | Fable.TypeCast(body, _) -> tryNativeAsyncBody body
        | Fable.Let(ident, value, body) ->
            tryNativeAsyncBody body
            |> Option.map (fun body -> Fable.Let(ident, value, body))
        | Fable.Sequential expressions ->
            match List.rev expressions with
            | last :: rest ->
                tryNativeAsyncBody last
                |> Option.map (fun body -> Fable.Sequential(List.rev rest @ [ body ]))
            | [] -> None
        | Fable.IfThenElse(guard, thenBody, elseBody, range) ->
            match tryNativeAsyncBody thenBody, tryNativeAsyncBody elseBody with
            | Some thenBody, Some elseBody -> Some(Fable.IfThenElse(guard, thenBody, elseBody, range))
            | _ -> None
        | _ -> None

    // Also recognize local callees without a member reference using their transformed bodies.
    let private isLocalNativeAsyncCallee ctx callee memberRef =
        let rec find declarations =
            declarations
            |> List.exists (
                function
                | Fable.ModuleDeclaration declaration -> find declaration.Members
                | Fable.MemberDeclaration declaration ->
                    let matches =
                        match memberRef, callee with
                        | Some memberRef, _ -> memberRef = declaration.MemberRef
                        | None, MaybeCasted(Fable.IdentExpr ident) -> ident.Name = declaration.Name
                        | _ -> false

                    matches && Option.isSome (tryNativeAsyncBody declaration.Body)
                | _ -> false
            )

        find ctx.File.Declarations

    let rec private tryNativeGeneratorBody isAsync =
        function
        | Fable.Call(Fable.Delegate([ _ ], body, _, tags), info, _, _) when
            List.contains
                (if isAsync then
                     "fickl-async-generator-function"
                 else
                     "fickl-generator-function")
                tags
            && info.Args.IsEmpty
            ->
            Some body
        | Fable.TypeCast(body, _) -> tryNativeGeneratorBody isAsync body
        | Fable.Let(ident, value, body) ->
            tryNativeGeneratorBody isAsync body
            |> Option.map (fun body -> Fable.Let(ident, value, body))
        | Fable.Sequential expressions ->
            match List.rev expressions with
            | last :: rest ->
                tryNativeGeneratorBody isAsync last
                |> Option.map (fun body -> Fable.Sequential(List.rev rest @ [ body ]))
            | [] -> None
        | Fable.IfThenElse(guard, first, second, range) ->
            match tryNativeGeneratorBody isAsync first, tryNativeGeneratorBody isAsync second with
            | Some first, Some second -> Some(Fable.IfThenElse(guard, first, second, range))
            | _ -> None
        | _ -> None

    /// fickl: the TypeScript signature of a module function that takes or returns `unknown`. It is declared before
    /// the implementation, which keeps `any`, so callers see `unknown` while the body type-checks as before.
    let tryUnknownSignature
        (com: IBabelCompiler)
        ctx
        (memb: Fable.MemberFunctionOrValue)
        (args: Fable.Ident list)
        (expr: Expression)
        =
        let unknown = FSharp2Fable.Util.TsUnknown.tryFSharpMember memb

        // A unit argument has no parameter
        let args =
            match args, expr with
            | [ arg ], FunctionExpression(_, [||], _, _, _, _) when arg.Type = Fable.Unit -> []
            | _ -> args

        match unknown, expr with
        | Some fsMemb, FunctionExpression(_, parameters, _, returnType, typeParameters, _) when
            com.IsTypeScript && parameters.Length = List.length args
            ->
            let argsWithoutThis = args |> List.filter (fun arg -> not arg.IsThisArgument)

            FSharp2Fable.Util.TsUnknown.tryParameterTypes fsMemb argsWithoutThis.Length
            |> Option.bind (fun fsTypes ->
                let fsTypes =
                    Dictionary<Fable.Ident, FSharp.Compiler.Symbols.FSharpType>(HashIdentity.Reference)
                    |> fun d ->
                        List.iter2 (fun arg t -> d[arg] <- t) argsWithoutThis fsTypes
                        d

                let mark fsType typ =
                    FSharp2Fable.Util.TsUnknown.markType fsType typ |> makeTypeAnnotation com ctx

                let parameters =
                    Array.zip parameters (List.toArray args)
                    |> Array.map (fun (Parameter.Parameter(name, annotation, flags), arg) ->
                        match fsTypes.TryGetValue(arg) with
                        | true, fsType when FSharp2Fable.Util.TsUnknown.contains fsType ->
                            Parameter.Parameter(name, Some(mark fsType arg.Type), flags), true
                        | _ -> Parameter.Parameter(name, annotation, flags), false
                    )

                let fsReturn = fsMemb.ReturnParameter.Type
                let returnsUnknown = FSharp2Fable.Util.TsUnknown.contains fsReturn

                if returnsUnknown || parameters |> Array.exists snd then
                    let returnType =
                        if returnsUnknown then
                            mark fsReturn memb.ReturnParameter.Type
                        else
                            defaultArg returnType AnyTypeAnnotation

                    Some(Array.map fst parameters, returnType, typeParameters)
                else
                    None
            )
        | _ -> None

    let transformModuleFunction com ctx (info: Fable.MemberFunctionOrValue) membName (args: Fable.Ident list) body =
        let args, body =
            if info.Attributes |> hasAttribute Atts.jsxComponent then
                // SolidJS requires values being accessed directly from the props object for reactivity to work properly
                // https://www.solidjs.com/guides/rendering#props
                let propsArg = makeIdent "$props"
                let propsExpr = Fable.IdentExpr propsArg

                let replacements =
                    args
                    |> List.map (fun a -> a.Name, getFieldWith None a.Type propsExpr a.Name)
                    |> Map

                [ propsArg ], FableTransforms.replaceValues replacements body
            else
                args, body

        if hasAttribute Atts.ficklAsyncFunction info.Attributes then
            validateNativeAsyncPayload com body.Range info.ReturnParameter.Type

            match tryNativeAsyncBody body with
            | Some body ->
                let args, body, returnType, typeParameters =
                    getMemberArgsAndBodyWithReturnType
                        com
                        ctx
                        (NonAttached membName)
                        None
                        info
                        args
                        info.ReturnParameter.Type
                        body

                let args =
                    if com.IsTypeScript then
                        Array.append [| Parameter.parameter ("this", typeAnnotation = AnyTypeAnnotation) |] args
                    else
                        args

                Expression.asyncFunctionExpression (
                    args,
                    body,
                    ?returnType = returnType,
                    ?typeParameters = typeParameters
                )
            | None -> addErrorAndReturnNull com body.Range "AsyncFunction requires a jsAsync computation"
        elif
            hasAttribute Atts.ficklGeneratorFunction info.Attributes
            || hasAttribute Atts.ficklAsyncGeneratorFunction info.Attributes
        then
            let isAsync = hasAttribute Atts.ficklAsyncGeneratorFunction info.Attributes

            match tryNativeGeneratorBody isAsync body with
            | Some body ->
                validateGeneratorReturn com info.ReturnParameter.Type body

                let args, body, returnType, typeParameters =
                    getMemberArgsAndBodyWithReturnType
                        com
                        ctx
                        (NonAttached membName)
                        None
                        info
                        args
                        info.ReturnParameter.Type
                        body

                let args =
                    if com.IsTypeScript then
                        Array.append [| Parameter.parameter ("this", typeAnnotation = AnyTypeAnnotation) |] args
                    else
                        args

                Expression.generatorFunctionExpression (
                    args,
                    body,
                    ?returnType = returnType,
                    ?typeParameters = typeParameters,
                    isAsync = isAsync
                )
            | None ->
                addErrorAndReturnNull
                    com
                    body.Range
                    (if isAsync then
                         "AsyncGeneratorFunction requires a jsAsyncGenerator computation"
                     else
                         "GeneratorFunction requires a jsGenerator computation")
        else
            let capturesReceiver = usesGeneratorReceiver body

            let args, body, returnType, typeParamDecl =
                getMemberArgsAndBody com ctx (NonAttached membName) None info args body

            let args =
                if com.IsTypeScript && capturesReceiver then
                    Array.append [| Parameter.parameter ("this", typeAnnotation = AnyTypeAnnotation) |] args
                else
                    args

            Expression.functionExpression (args, body, ?returnType = returnType, ?typeParameters = typeParamDecl)

    let transformAction (com: IBabelCompiler) ctx expr =
        let statements = transformAsStatements com ctx None expr

        let hasVarDeclarations =
            statements
            |> Array.exists (
                function
                | Declaration(Declaration.VariableDeclaration(_)) -> true
                | _ -> false
            )

        if hasVarDeclarations then
            [
                Expression.callExpression (Expression.functionExpression ([||], BlockStatement(statements)), [||])
                |> ExpressionStatement
                |> PrivateModuleDeclaration
            ]
        else
            statements |> Array.mapToList (fun x -> PrivateModuleDeclaration(x))

    let private transformOrdinaryAttachedProperty
        (com: IBabelCompiler)
        ctx
        (ent: Fable.Entity)
        (info: Fable.MemberFunctionOrValue)
        (memb: Fable.MemberDecl)
        =
        let isStatic = not info.IsInstance
        let key, isComputed = SymbolKeys.attachedKey com ctx info memb

        let args = memb.Args

        let kind, (unwrappedType, body) =
            if info.IsGetter then
                ClassGetter(key, isComputed), unwrapOptionalReturn com memb.Body
            else
                ClassSetter(key, isComputed), (None, memb.Body)

        let args, body, returnType, _typeParamDecl =
            getMemberArgsAndBodyWithReturnType
                com
                ctx
                (Attached isStatic)
                (Some ent)
                info
                args
                (defaultArg unwrappedType body.Type)
                body

        let returnType =
            if Option.isSome unwrappedType then
                returnType
                |> Option.map (fun t -> UnionTypeAnnotation [| t; UndefinedTypeAnnotation |])
            else
                returnType

        ClassMember.classMethod (
            kind,
            args,
            Some body,
            isStatic = isStatic,
            isOverride = (isBaseClassOverride com ent info memb || hidesBaseMember com info),
            ?returnType = returnType,
            ?doc = memb.XmlDoc
        ) //, ?typeParameters=typeParamDecl)
        |> NativeDecorators.decorateMember com ctx (com.GetMember memb.MemberRef)
        |> Array.singleton

    let transformAttachedProperty
        (com: IBabelCompiler)
        (ctx: Context)
        ent
        (info: Fable.MemberFunctionOrValue)
        (memb: Fable.MemberDecl)
        =
        let declaredInfo = com.GetMember memb.MemberRef

        match FSharp2Fable.FsMemberFunctionOrValue.JsDecoratedAutoProperty declaredInfo with
        | Some(backingName, _) when ctx.NativeAutoProperties.ContainsKey backingName ->
            if info.IsGetter then
                [| ctx.NativeAutoProperties[backingName] |]
            else
                [||]
        | _ -> transformOrdinaryAttachedProperty com ctx ent info memb

    let transformAttachedMethod
        (com: IBabelCompiler)
        ctx
        (ent: Fable.Entity)
        (info: Fable.MemberFunctionOrValue)
        (memb: Fable.MemberDecl)
        =
        let isStatic = not info.IsInstance

        let makeMethod name args body returnType typeParamDecl =
            let key, isComputed =
                if name = memb.Name then
                    SymbolKeys.attachedKey com ctx info memb
                else
                    memberFromName name

            ClassMember.classMethod (
                ClassFunction(key, isComputed),
                args,
                Some body,
                isStatic = isStatic,
                isOverride =
                    (name = memb.Name
                     && (isBaseClassOverride com ent info memb || hidesBaseMember com info)),
                ?returnType = returnType,
                ?typeParameters = typeParamDecl,
                ?doc = memb.XmlDoc
            )
            |> fun declaration ->
                if name = memb.Name then
                    NativeDecorators.decorateMember com ctx (com.GetMember memb.MemberRef) declaration
                else
                    declaration

        let args, body, returnType, typeParamDecl =
            getMemberArgsAndBody com ctx (Attached isStatic) (Some ent) info memb.Args memb.Body

        [|
            yield makeMethod memb.Name args body returnType typeParamDecl
            if info.FullName = "System.Collections.Generic.IEnumerable.GetEnumerator" then
                let returnType =
                    match returnType with
                    | Some(AliasTypeAnnotation(_, typeArguments)) ->
                        TypeAnnotation.aliasTypeAnnotation (
                            Identifier.identifier ("Iterator"),
                            typeArguments = typeArguments
                        )
                        |> Some
                    | _ -> None

                yield makeMethod "Symbol.iterator" [||] (enumerableThisToIterator com ctx) returnType None
        |]

    /// fsc represents a union case with no fields as a single shared instance, so e.g.
    /// `LanguagePrimitives.PhysicalEquality X.A X.A` is true. Mirror that by exposing the
    /// case as a static, precomputed instance instead of constructing a new one on every access.
    let makeUnionCaseSingletonMember
        (com: IBabelCompiler)
        (entName: string)
        genArgsCount
        (tag: int option)
        (case: Fable.UnionCase)
        : ClassMember
        =
        let typeArguments =
            if com.IsTypeScript then
                let anyArgs = Array.create genArgsCount AnyTypeAnnotation

                match tag with
                | Some tag ->
                    Array.append anyArgs [| LiteralTypeAnnotation(Literal.numericLiteral (tag)) |]
                    |> Some
                | None when Array.isEmpty anyArgs -> None
                | None -> Some anyArgs
            else
                None

        let consArgs =
            match tag with
            | Some tag -> [| ofInt tag; Expression.arrayExpression [||] |]
            | None -> [||]

        let value =
            Expression.newExpression (Expression.identifier entName, consArgs, ?typeArguments = typeArguments)

        ClassMember.classProperty (
            Expression.identifier (sanitizeName case.Name),
            value = value,
            isStatic = true,
            ?typeAnnotation =
                (if com.IsTypeScript then
                     Some AnyTypeAnnotation
                 else
                     None),
            ?accessModifier =
                (if com.IsTypeScript then
                     Some Readonly
                 else
                     None)
        )

    let transformUnion
        (com: IBabelCompiler)
        ctx
        (ent: Fable.Entity)
        (entName: string)
        (doc: string option)
        classMembers
        =
        let classMembers = withReferenceEquality com ctx ent classMembers
        let isPublic = ent.IsPublic
        let tagArgName = "Tag"
        let tagArgTa = makeAliasTypeAnnotation com ctx tagArgName

        let union_cases = entName + UnionHelpers.CASES_SUFFIX |> Identifier.identifier

        let entParamsDecl =
            FSharp2Fable.Util.getEntityGenArgs ent |> makeTypeParamDecl com ctx

        let entParamsInst =
            entParamsDecl
            |> Array.map (fun (TypeParameter(name = name)) -> makeAliasTypeAnnotation com ctx name)

        let union_cases_alias = AliasTypeAnnotation(union_cases, entParamsInst)

        let baseExpr =
            let id = makeFableLibImportTypeId com ctx "Types" "Union"

            let typeParamInst =
                match ent.UnionCases with
                | _ when not com.IsTypeScript -> [||]
                | [ singleCase ] ->
                    [|
                        LiteralTypeAnnotation(Literal.numericLiteral (0))
                        LiteralTypeAnnotation(Literal.stringLiteral (singleCase.Name))
                    |]
                | _ ->
                    [|
                        tagArgTa
                        IndexedTypeAnnotation(
                            IndexedTypeAnnotation(union_cases_alias, tagArgTa),
                            LiteralTypeAnnotation(Literal.numericLiteral (0))
                        )
                    |]

            TypeAnnotation.aliasTypeAnnotation (id, typeArguments = typeParamInst)
            |> makeSuperType com
            |> Some

        let cases =
            let body =
                ent.UnionCases
                |> List.map (getUnionCaseName >> makeStrConst)
                |> makeArray com ctx
                |> Statement.returnStatement
                |> Array.singleton
                |> BlockStatement

            ClassMember.classMethod (ClassFunction(Expression.identifier ("cases"), false), [||], Some body)

        // Don't emit helpers for single-case unions but make constructor with typed arguments
        match ent.UnionCases with
        | [ singleCase ] ->
            let fieldAnnotations =
                if com.IsTypeScript then
                    singleCase.UnionCaseFields
                    |> List.mapToArray (fun fi -> makeFieldAnnotation com ctx fi.FieldType)
                else
                    [||]

            let args =
                if com.IsTypeScript then
                    Seq.zip singleCase.UnionCaseFields fieldAnnotations
                    |> Seq.mapToArray (fun (fi, ta) -> Parameter.parameter (sanitizeName fi.Name, typeAnnotation = ta))
                else
                    singleCase.UnionCaseFields
                    |> List.mapToArray (fun fi -> Parameter.parameter (sanitizeName fi.Name))

            let fieldsExpr =
                args
                |> Array.map (fun a -> Expression.identifier (a.Name))
                |> Expression.arrayExpression

            let consBody =
                BlockStatement
                    [|
                        callSuperAsStatement []
                        assign None (get None thisExpr "tag") (Expression.numericLiteral (0.))
                        |> ExpressionStatement
                        assign None (get None thisExpr "fields") fieldsExpr |> ExpressionStatement
                    |]

            declareType
                com
                ctx
                ent
                entName
                doc
                args
                consBody
                baseExpr
                [|
                    if com.IsTypeScript then
                        ClassMember.classProperty (
                            Expression.identifier "tag",
                            typeAnnotation = LiteralTypeAnnotation(Literal.numericLiteral (0)),
                            accessModifier = Readonly
                        )

                        ClassMember.classProperty (
                            Expression.identifier "fields",
                            typeAnnotation = TupleTypeAnnotation fieldAnnotations,
                            accessModifier = Readonly
                        )
                    cases
                    if List.isEmpty singleCase.UnionCaseFields then
                        makeUnionCaseSingletonMember com entName entParamsDecl.Length None singleCase
                    yield! classMembers
                |]

        | _ when com.IsTypeScript ->
            let union_cons = entName |> Identifier.identifier

            let union_ta, union_cases_ta =
                ent.UnionCases
                |> List.mapiToArray (fun i uci ->
                    let typeParams =
                        Array.append entParamsInst [| LiteralTypeAnnotation(Literal.numericLiteral (i)) |]

                    let case_ta = TypeAnnotation.aliasTypeAnnotation (union_cons, typeParams)

                    let fields_ta =
                        uci.UnionCaseFields
                        |> List.mapToArray (fun fi -> makeFieldAnnotation com ctx fi.FieldType)
                        |> TupleTypeAnnotation

                    case_ta,
                    AbstractMember.abstractProperty (
                        Expression.numericLiteral (i),
                        TupleTypeAnnotation [| LiteralTypeAnnotation(Literal.stringLiteral (uci.Name)); fields_ta |]
                    )
                )
                |> Array.unzip

            let fieldsArgTa =
                IndexedTypeAnnotation(
                    IndexedTypeAnnotation(union_cases_alias, tagArgTa),
                    LiteralTypeAnnotation(Literal.numericLiteral (1))
                )

            let consArgs =
                [|
                    Parameter.parameter ("tag", typeAnnotation = tagArgTa)
                    Parameter.parameter ("fields", typeAnnotation = fieldsArgTa)
                |]

            let consArgsModifiers = [||]

            let consBody =
                BlockStatement
                    [|
                        callSuperAsStatement []
                        yield!
                            [ "tag"; "fields" ]
                            |> List.map (fun name ->
                                let left = get None thisExpr name
                                let right = Expression.identifier (name)
                                assign None left right |> ExpressionStatement
                            )
                    |]

            let classMembers =
                [|
                    ClassMember.classProperty (
                        Expression.identifier "tag",
                        typeAnnotation = tagArgTa,
                        accessModifier = Readonly
                    )

                    ClassMember.classProperty (
                        Expression.identifier "fields",
                        typeAnnotation = fieldsArgTa,
                        accessModifier = Readonly
                    )
                    cases
                    yield!
                        ent.UnionCases
                        |> List.mapi (fun i case -> i, case)
                        |> List.choose (fun (i, case) ->
                            if List.isEmpty case.UnionCaseFields then
                                makeUnionCaseSingletonMember com entName entParamsDecl.Length (Some i) case
                                |> Some
                            else
                                None
                        )
                    yield! classMembers
                |]

            let unionConsTypeParams =
                Some(
                    Array.append
                        entParamsDecl
                        [|
                            TypeParameter.typeParameter (tagArgName, bound = KeyofTypeAnnotation(union_cases_alias))
                        |]
                )

            [
                TypeAliasDeclaration(entName + UnionHelpers.UNION_SUFFIX, entParamsDecl, UnionTypeAnnotation union_ta)
                |> asModuleDeclaration isPublic
                TypeAliasDeclaration(union_cases.Name, entParamsDecl, ObjectTypeAnnotation union_cases_ta)
                |> asModuleDeclaration isPublic

                // Helpers to instantiate union
                yield!
                    ent.UnionCases
                    |> List.mapi (fun i case ->
                        let tag = Literal.numericLiteral (i)

                        let passedArgs =
                            case.UnionCaseFields
                            |> List.mapToArray (fun fi -> Expression.identifier (sanitizeName fi.Name))
                            |> Expression.arrayExpression

                        let consTypeArgs = Array.append entParamsInst [| LiteralTypeAnnotation tag |]

                        let body =
                            BlockStatement
                                [|
                                    (if List.isEmpty case.UnionCaseFields then
                                         get None (Expression.Identifier union_cons) (sanitizeName case.Name)
                                     else
                                         Expression.newExpression (
                                             Expression.Identifier union_cons,
                                             [| Expression.Literal tag; passedArgs |],
                                             typeArguments = consTypeArgs
                                         ))
                                    |> Statement.returnStatement
                                |]

                        let parameters =
                            case.UnionCaseFields
                            |> List.mapToArray (fun fi ->
                                Parameter.parameter (
                                    sanitizeName fi.Name,
                                    typeAnnotation = makeFieldAnnotation com ctx fi.FieldType
                                )
                            )

                        let fnId = entName + "_" + sanitizeName case.Name |> Identifier.identifier
                        // Don't use return type, TypeScript will infer it and sometimes we want to use
                        // the actual constructor type in case it implements an interface
                        // let returnType = AliasTypeAnnotation(Identifier.identifier(entName + UnionHelpers.UNION_SUFFIX), entParamsInst)
                        Declaration.functionDeclaration (parameters, body, fnId, typeParameters = entParamsDecl)
                        |> asModuleDeclaration isPublic
                    )

                // Actual class
                declareClassWithParams
                    com
                    ctx
                    ent
                    union_cons.Name
                    doc
                    consArgs
                    consArgsModifiers
                    consBody
                    baseExpr
                    classMembers
                    unionConsTypeParams
                if not com.Options.NoReflection then
                    declareTypeReflection com ctx ent entName
            ]

        // Multiple cases, no-TypeScript
        | _ ->
            let args = [| Parameter.parameter ("tag"); Parameter.parameter ("fields") |]

            let body =
                BlockStatement
                    [|
                        callSuperAsStatement []
                        yield!
                            [ "tag"; "fields" ]
                            |> List.map (fun name ->
                                let left = get None thisExpr name
                                let right = Expression.identifier (name)
                                assign None left right |> ExpressionStatement
                            )
                    |]

            let singletonMembers =
                ent.UnionCases
                |> List.mapi (fun i case -> i, case)
                |> List.choose (fun (i, case) ->
                    if List.isEmpty case.UnionCaseFields then
                        makeUnionCaseSingletonMember com entName entParamsDecl.Length (Some i) case
                        |> Some
                    else
                        None
                )
                |> List.toArray

            let classMembers =
                Array.append [| cases |] (Array.append singletonMembers classMembers)

            declareType com ctx ent entName doc args body baseExpr classMembers

    let transformClassWithCompilerGeneratedConstructor
        (com: IBabelCompiler)
        ctx
        (ent: Fable.Entity)
        (decl: Fable.ClassDecl)
        classMembers
        =
        let classMembers = withReferenceEquality com ctx ent classMembers
        let fieldIds = getEntityFieldsAsIdents ent |> List.toArray
        let args = fieldIds |> Array.map identAsExpr

        let baseParams = getExplicitBaseParams com ent

        let baseParamIds =
            let ownNames = fieldIds |> Array.map _.Name |> set

            (Set.empty, baseParams)
            ||> List.mapFold (fun usedNames (name, typ, _) ->
                let name = sanitizeName name

                let name =
                    if ownNames.Contains name || usedNames.Contains name then
                        Seq.initInfinite (fun i ->
                            name
                            + "$base"
                            + (if i = 0 then
                                   ""
                               else
                                   string<int> i)
                        )
                        |> Seq.find (fun name -> not (ownNames.Contains name || usedNames.Contains name))
                    else
                        name

                makeTypedIdent typ name, usedNames.Add name
            )
            |> fst
            |> List.toArray

        let baseExpr =
            if ent.IsFSharpExceptionDeclaration then
                // fickl: [<JsError>] extends a native Error subclass, see FSharpError in fable-library
                let baseName =
                    if hasAttribute Atts.ficklJsError ent.Attributes then
                        "FSharpError"
                    else
                        "FSharpException"

                libValue com ctx "Types" baseName |> SuperExpression |> Some
            elif ent.IsFSharpRecord || ent.IsValueType then
                libValue com ctx "Types" "Record" |> SuperExpression |> Some
            else
                match tryExtendedBaseType ent with
                | Some baseType when com.IsTypeScript ->
                    Fable.DeclaredType(baseType.Entity, baseType.GenericArgs)
                    |> makeTypeAnnotation com ctx
                    |> makeSuperType com
                    |> Some
                | Some baseType ->
                    com.GetEntity(baseType.Entity)
                    |> jsConstructor com ctx
                    |> SuperExpression
                    |> Some
                | None -> None

        let body =
            BlockStatement(
                [|
                    if Option.isSome baseExpr then
                        yield baseParamIds |> Array.map identAsExpr |> Array.toList |> callSuperAsStatement
                    yield!
                        ent.FSharpFields
                        |> List.filter (fun field -> not field.IsStatic)
                        |> List.mapi (fun i field ->
                            let left = SymbolKeys.fieldAccess com ctx None field thisExpr

                            let right = wrapIntExpression field.FieldType args[i]

                            assign None left right |> ExpressionStatement
                        )
                        |> List.toArray
                |]
            )

        let fields =
            ent.FSharpFields
            |> List.filter (fun field -> not field.IsStatic)
            |> List.toArray

        let args =
            Array.map2
                (fun (fi: Fable.Ident) field ->
                    Parameter.parameter (
                        fi.Name,
                        ?typeAnnotation =
                            (if com.IsTypeScript then
                                 Some(makeFieldAnnotationOf com ctx field)
                             else
                                 None)
                    )
                )
                fieldIds
                fields

        let baseArgs =
            Array.map2
                (fun (fi: Fable.Ident) (_, typ, field: Fable.Field option) ->
                    Parameter.parameter (
                        fi.Name,
                        ?typeAnnotation =
                            (if not com.IsTypeScript then
                                 None
                             else
                                 match field with
                                 | Some field when not (FSharp2Fable.FsField.IsCurriedField field) ->
                                     Some(makeFieldAnnotation com ctx typ)
                                 | _ -> Some(makeTypeAnnotation com ctx typ))
                    )
                )
                baseParamIds
                (List.toArray baseParams)

        let args = Array.append baseArgs args

        declareType com ctx ent decl.Name decl.XmlDoc args body baseExpr classMembers

    let transformPojoDefinedByConsArgsToInterface
        (com: IBabelCompiler)
        ctx
        (ent: Fable.Entity)
        (decl: Fable.ClassDecl)
        =

        let constructors =
            ent.MembersFunctionsAndValues
            |> Seq.filter _.IsConstructor
            |> Seq.choose (fun constructor ->
                let parameters = List.concat constructor.CurriedParameterGroups

                if parameters.Length = 0 then
                    None
                else

                    parameters
                    |> List.mapi (fun index arg ->
                        let name = defaultArg arg.Name $"arg%i{index}"

                        /// Try to find getter/setter in F# syntax for POJOs. If found propagate its xml doc to interface.
                        let tryXmlDoc =
                            ent.MembersFunctionsAndValues
                            |> Seq.tryFind (fun s -> s.DisplayName = name)
                            |> Option.bind (fun tgs -> tgs.XmlDoc)

                        let typeAnnotation =
                            if arg.IsOptional then
                                unwrapOptionalType arg.Type
                            else
                                arg.Type
                            |> FableTransforms.uncurryType
                            |> makeTypeAnnotation com ctx

                        AbstractMember.abstractProperty (
                            name |> Identifier.identifier |> Expression.Identifier,
                            typeAnnotation,
                            isOptional = arg.IsOptional,
                            ?doc = tryXmlDoc
                        )
                    )
                    |> Array.ofSeq
                    |> Some
            )
            |> Seq.toList

        let typeParameters =
            ent.GenericParameters
            |> List.map (fun g -> Fable.GenericParam(g.Name, g.IsMeasure, g.Constraints))
            |> makeTypeParamDecl com ctx

        let extends =
            ent.BaseType
            |> Option.bind (fun d ->
                com.TryGetEntity(d.Entity)
                |> Option.filter FSharp2Fable.Util.isPojoDefinedByConsArgsEntity
                |> Option.map (makeEntityTypeAnnotation com ctx d.GenericArgs)
            )
            |> Option.toArray

        match constructors with
        | [] ->
            addError
                com
                []
                None
                "Unable to find a valid constructor for generating interface via ParamObject, please make sure the constructor has at least one parameter."

            []
        | [ members ] ->
            Declaration.interfaceDeclaration (
                Identifier.identifier decl.Name,
                members,
                extends,
                typeParameters,
                ?doc = decl.XmlDoc
            )
            |> asModuleDeclaration ent.IsPublic
            |> List.singleton
        | _ ->
            let unionTyp =
                List.map ObjectTypeAnnotation constructors
                |> Array.ofList
                |> UnionTypeAnnotation

            let typ =
                match extends with
                | [| parentTyp |] -> IntersectionTypeAnnotation [| unionTyp; parentTyp |]
                | _ -> unionTyp

            TypeAliasDeclaration(decl.Name, typeParameters, typ)
            |> asModuleDeclaration ent.IsPublic
            |> List.singleton

    let getOverloadSuffix (ent: Fable.Entity) (memb: Fable.MemberFunctionOrValue) =
        let entityGenericParameters = ent.GenericParameters |> List.map (fun g -> g.Name)

        if memb.IsGetter || memb.IsSetter then
            ""
        else
            memb.CurriedParameterGroups
            |> List.map (fun groups -> groups |> List.map (fun group -> group.Type))
            |> OverloadSuffix.getHash entityGenericParameters

    let getMangledMemberName (ent: Fable.Entity) (memb: Fable.MemberFunctionOrValue) =
        let overloadSuffix = getOverloadSuffix ent memb

        let genericSuffix =
            if ent.GenericParameters.Length > 0 then
                $"`{ent.GenericParameters.Length}"
            else
                ""

        let fullNamePath =
            let lastDotIndex = memb.FullName.LastIndexOf('.')
            memb.FullName.Substring(0, lastDotIndex)

        let name = fullNamePath + genericSuffix + "." + memb.CompiledName + overloadSuffix
        name

    let transformAbstractMember com ctx (ent: Fable.Entity) (memb: Fable.MemberFunctionOrValue) =
        let isCallable =
            hasAttribute Atts.callableInterface ent.Attributes
            && memb.CompiledName = "Invoke"

        // fickl: the mangling logic of FSharp2Fable.Util.getAbstractMemberInfo, so the members are named as
        // the JavaScript calls them
        let isMangled = FSharp2Fable.Util.isMangledAbstractFableEntity ent

        let isGetter = memb.IsGetter
        let isSetter = memb.IsSetter

        let name =
            if isMangled then
                getMangledMemberName ent memb
            elif isGetter || isSetter then
                memb.DisplayName
            else
                memb.CompiledName

        // EmitMethod and EmitProperty rename the member at call sites, so the declaration uses the same name
        let emittedName =
            memb.Attributes
            |> Seq.tryPick (fun att ->
                match att.ConstructorArgs with
                | (:? string as name) :: _ when
                    att.Entity.FullName = Atts.emitMethod || att.Entity.FullName = Atts.emitProperty
                    ->
                    Some name
                | _ -> None
            )

        let key, isComputed =
            match emittedName with
            | Some name -> memberFromName name
            | None -> SymbolKeys.memberKey com ctx memb name

        let args =
            memb.CurriedParameterGroups
            |> List.concat
            |> fun args ->
                match args with
                | [ arg ] when isCallable && arg.Type = Fable.Unit -> []
                | _ -> args
            // |> FSharp2Fable.Util.discardUnitArg
            |> List.toArray

        let argsLen = Array.length args

        // fickl: the F# types of the parameters, where `unknown` is written; accessors keep `any`, as reads of
        // a property are not calls that Fickl's output can cast back to `any`
        let fsMemb =
            if isGetter || isSetter then
                None
            else
                FSharp2Fable.Util.TsUnknown.tryFSharpMember memb

        let markUnknown =
            let fsTypes =
                fsMemb
                |> Option.bind (fun m -> FSharp2Fable.Util.TsUnknown.tryParameterTypes m argsLen)
                |> Option.map List.toArray

            fun index (argType: Fable.Type) ->
                match fsTypes with
                | Some fsTypes -> FSharp2Fable.Util.TsUnknown.markType fsTypes[index] argType
                | None -> argType

        let args =
            args
            |> Array.mapi (fun index arg ->
                let argName = defaultArg arg.Name $"arg%i{index}"
                let argIsSpread = (index = argsLen - 1 && memb.HasSpread)

                let argType =
                    match markUnknown index arg.Type with
                    | Fable.Array(genArg, Fable.MutableArray) when argIsSpread ->
                        // modify the last argument's type if it's a spread parameter
                        Fable.Array(genArg, Fable.ResizeArray)
                    | argType -> argType

                let ta =
                    if arg.IsOptional then
                        unwrapOptionalType argType
                    else
                        argType
                    |> FableTransforms.uncurryType
                    |> makeTypeAnnotation com ctx

                Parameter
                    .parameter(argName, ta)
                    .WithFlags(
                        ParameterFlags(isOptional = arg.IsOptional, isSpread = argIsSpread, isNamed = arg.IsNamed)
                    )
            )

        let typeParams =
            memb.GenericParameters
            |> List.map (fun g -> Fable.GenericParam(g.Name, g.IsMeasure, g.Constraints))
            |> makeTypeParamDecl com ctx

        let returnType =
            match fsMemb with
            | Some m -> FSharp2Fable.Util.TsUnknown.markType m.ReturnParameter.Type memb.ReturnParameter.Type
            | None -> memb.ReturnParameter.Type
            |> FableTransforms.uncurryType
            |> makeTypeAnnotation com ctx

        let kind =
            if not isMangled && isGetter then
                ObjectGetter
            elif not isMangled && isSetter then
                ObjectSetter
            else
                ObjectMeth

        if isCallable then
            AbstractCallSignature(args, returnType, typeParams, memb.XmlDoc) |> Some
        elif hasAttribute Atts.emitConstructor memb.Attributes then
            AbstractConstructSignature(args, returnType, typeParams, memb.XmlDoc) |> Some
        elif hasAttribute Atts.emitIndexer memb.Attributes then
            // Indexer accessors are not flagged as getters or setters; the macro `$0[$1]{{=$2}}` assigns given a value
            let keyAndValue =
                match memb.CurriedParameterGroups |> List.concat with
                | [ key; value ] -> Some(key, value.Type, false)
                | [ key ] -> Some(key, memb.ReturnParameter.Type, true)
                | _ -> None

            // TypeScript index signatures only take string, number or symbol keys
            let keyType (key: Fable.Parameter) =
                match key.Type with
                | Fable.String -> Some StringTypeAnnotation
                | Fable.Number((Int8 | UInt8 | Int16 | UInt16 | Int32 | UInt32 | Float16 | Float32 | Float64), _) ->
                    Some NumberTypeAnnotation
                | _ -> None

            match keyAndValue with
            | Some(key, valueType, isReadonly) ->
                match keyType key with
                | Some keyType ->
                    let valueType =
                        valueType |> FableTransforms.uncurryType |> makeTypeAnnotation com ctx

                    AbstractIndexSignature(defaultArg key.Name "key", keyType, valueType, isReadonly, memb.XmlDoc)
                    |> Some
                | None ->
                    $"%s{ent.FullName}.%s{memb.DisplayName} is left out of the TypeScript declaration: an index signature key must be a string or a number"
                    |> addWarning com [] None

                    None
            | None -> None
        else
            AbstractMember.abstractMethod (
                kind,
                key,
                args,
                returnType = returnType,
                typeParameters = typeParams,
                isComputed = isComputed,
                ?doc = memb.XmlDoc
            )
            |> Some

    /// The getter and setter of one EmitIndexer property make a single writable index signature
    let private mergeIndexSignatures (members: AbstractMember seq) =
        let members = Seq.toArray members

        let writableKeys =
            members
            |> Array.choose (
                function
                | AbstractIndexSignature(keyType = keyType; isReadonly = false) -> Some keyType
                | _ -> None
            )

        members
        |> Array.filter (
            function
            | AbstractIndexSignature(keyType = keyType; isReadonly = true) -> not (Array.contains keyType writableKeys)
            | _ -> true
        )
        |> Array.distinctBy (
            function
            | AbstractIndexSignature(keyType = keyType) -> Choice1Of2 keyType
            | m -> Choice2Of2 m
        )

    let transformAbstractClassMembers com ctx (ent: Fable.Entity) =
        let memberImplNames =
            ent.MembersFunctionsAndValues
            |> Seq.filter (fun memb -> memb.IsOverrideOrExplicitInterfaceImplementation)
            |> Seq.map (fun memb -> getMangledMemberName ent memb)
            |> Set.ofSeq

        ent.MembersFunctionsAndValues
        |> Seq.filter (fun memb ->
            memb.IsDispatchSlot
            && not (memb.IsProperty)
            && memb.IsInstance
            // A plain Emit has no shape to declare
            && not (hasAttribute Atts.emitAttr memb.Attributes)
            && not (Set.contains (getMangledMemberName ent memb) memberImplNames)
        )
        |> Seq.choose (fun memb ->
            let abstractMember = transformAbstractMember com ctx ent memb

            match abstractMember with
            | Some(AbstractMethod(kind, key, parameters, returnType, typeParameters, isComputed, doc)) ->
                let kind =
                    match kind with
                    | ObjectMeth -> ClassFunction(key, isComputed)
                    | ObjectGetter -> ClassGetter(key, isComputed)
                    | ObjectSetter -> ClassSetter(key, isComputed)

                let body = None
                let isStatic = not memb.IsInstance
                let isAbstract = true

                let abstractClassMember =
                    ClassMember.classMethod (
                        kind,
                        parameters,
                        body,
                        isStatic = isStatic,
                        isAbstract = isAbstract,
                        returnType = returnType,
                        typeParameters = typeParameters,
                        ?doc = doc
                    )

                Some abstractClassMember
            | _ -> None
        )
        |> Seq.toArray

    let transformClassWithPrimaryConstructor
        (com: IBabelCompiler)
        ctx
        (ent: Fable.Entity)
        (classDecl: Fable.ClassDecl)
        classMembers
        (cons: Fable.MemberDecl)
        =
        let consInfo = com.GetMember(cons.MemberRef)
        let classIdent = Expression.identifier (classDecl.Name)

        let consArgs, consBody, returnType, _typeParamDecl =
            getMemberArgsAndBody com ctx ClassConstructor (Some ent) consInfo cons.Args cons.Body

        let returnType, typeParamDecl =
            // change constructor's return type from void to entity type
            if com.IsTypeScript then
                let genArgs = FSharp2Fable.Util.getEntityGenArgs ent
                let returnType = getGenericTypeAnnotation com ctx classDecl.Name genArgs
                let typeParamDecl = makeTypeParamDecl com ctx genArgs |> Some
                Some returnType, typeParamDecl
            else
                returnType, None

        let abstractClassMembers =
            if com.IsTypeScript && ent.IsAbstractClass then
                transformAbstractClassMembers com ctx ent
            else
                [||]

        let classMembers = Array.append abstractClassMembers classMembers

        let exposedCons =
            let argExprs = consArgs |> Array.map (fun p -> Expression.identifier (p.Name))
            let exposedConsBody = Expression.newExpression (classIdent, argExprs)
            makeFunctionExpression None (consArgs, exposedConsBody, returnType, typeParamDecl)

        let baseType =
            ent.BaseType
            |> Option.map (fun d -> Fable.DeclaredType(d.Entity, d.GenericArgs))

        let baseExpr, consBody =
            classDecl.BaseCall
            |> extractSuperClassFromBaseCall com ctx baseType
            |> Option.orElseWith (fun () ->
                if ent.IsValueType then
                    Some(libValue com ctx "Types" "Record" |> SuperExpression, [])
                else
                    None
            )
            |> Option.map (fun (baseExpr, baseArgs) ->
                let consBody =
                    consBody.Body
                    |> Array.append [| callSuperAsStatement baseArgs |]
                    |> BlockStatement

                Some baseExpr, consBody
            )
            |> Option.defaultValue (None, consBody)

        [
            yield! declareType com ctx ent classDecl.Name classDecl.XmlDoc consArgs consBody baseExpr classMembers

            if not ent.IsAbstractClass then
                yield
                    ModuleDecl(cons.Name, isPublic = consInfo.IsPublic)
                    |> declareModuleMember com ctx exposedCons
        ]

    let transformInterfaceDeclaration (com: IBabelCompiler) ctx (ent: Fable.Entity) (decl: Fable.ClassDecl) =

        let members =
            ent.MembersFunctionsAndValues
            |> Seq.filter (fun memb ->
                not (memb.IsProperty)
                // Static abstract members are not part of an instance's shape, see #78
                && memb.IsInstance
                // A plain Emit has no shape to declare
                && not (hasAttribute Atts.emitAttr memb.Attributes)
            )
            |> Seq.choose (transformAbstractMember com ctx ent)
            |> mergeIndexSignatures

        let extends =
            ent.DeclaredInterfaces
            |> Seq.map (fun parent ->
                com.GetEntity(parent.Entity)
                |> makeEntityTypeAnnotation com ctx parent.GenericArgs
            )
            |> Seq.toArray

        let typeParameters =
            FSharp2Fable.Util.getEntityGenArgs ent |> makeTypeParamDecl com ctx

        Declaration.interfaceDeclaration (
            Identifier.identifier decl.Name,
            members,
            extends,
            typeParameters,
            ?doc = decl.XmlDoc
        )
        |> asModuleDeclaration ent.IsPublic

    /// fickl: `[<TsEnum>]` emits `const Color = Object.freeze({ Red: 0, 0: "Red" })`, the object a TypeScript enum
    /// compiles to. In TypeScript, an ambient `declare enum Color_$enum` types it, so the declaration stays erasable,
    /// and `type Color = Color_$enum` annotates the enum's values
    let transformTsEnumDeclaration (com: IBabelCompiler) ctx (ent: Fable.Entity) (decl: Fable.ClassDecl) =
        let underlyingKind =
            ent.FSharpFields
            |> List.tryPick (fun fi ->
                match fi.Name, fi.FieldType with
                | "value__", Fable.Number(kind, _) -> Some kind
                | _ -> None
            )

        match underlyingKind with
        | Some(Int64 | UInt64) ->
            $"TsEnum does not support %s{ent.FullName}, whose underlying type is 64-bit"
            |> addError com [] None

            []
        | _ ->
            let members =
                ent.FSharpFields
                |> List.choose (fun fi ->
                    match fi.Name, fi.LiteralValue with
                    | "value__", _ -> None
                    | name, Some value -> Some(name, System.Convert.ToDouble value)
                    | _, None -> None
                )

            let property (key: Expression, isComputed) value =
                ObjectMember.objectProperty (key, value, isComputed = isComputed)

            let reverseMapping =
                members
                |> List.map (fun (name, value) ->
                    property (Expression.numericLiteral value, false) (Expression.stringLiteral name)
                )

            let enumObject =
                Expression.callExpression (
                    Expression.memberExpression (Expression.identifier "Object", Expression.identifier "freeze"),
                    [|
                        (members
                         |> List.map (fun (name, value) ->
                             property (memberFromName name) (Expression.numericLiteral value)
                         ))
                        @ reverseMapping
                        |> List.toArray
                        |> Expression.objectExpression
                    |]
                )

            if com.IsTypeScript then
                let ambientName = decl.Name + "_$enum"

                let ambientEnum =
                    Declaration.enumDeclaration (
                        ambientName,
                        members
                        |> List.mapToArray (fun (name, value) -> name, Expression.numericLiteral value),
                        isDeclare = true
                    )

                let enumObjectType = TypeofTypeAnnotation(Expression.identifier ambientName)

                [
                    PrivateModuleDeclaration(Statement.Declaration ambientEnum)
                    Declaration.variableDeclaration (
                        Const,
                        decl.Name,
                        annotation = enumObjectType,
                        init = AsExpression(enumObject, enumObjectType)
                    )
                    |> asModuleDeclaration ent.IsPublic
                    TypeAliasDeclaration(decl.Name, [||], AliasTypeAnnotation(Identifier.identifier ambientName, [||]))
                    |> asModuleDeclaration ent.IsPublic
                ]
            else
                [
                    Declaration.variableDeclaration (Const, decl.Name, init = enumObject)
                    |> asModuleDeclaration ent.IsPublic
                ]

    let transformStringEnumDeclaration (ent: Fable.Entity) (attArgs: obj list) (decl: Fable.ClassDecl) =
        let ta = makeStringEnumTypeAnnotation ent attArgs

        TypeAliasDeclaration(decl.Name, [||], ta) |> asModuleDeclaration ent.IsPublic

    let transformErasedUnionDeclaration (com: IBabelCompiler) ctx (ent: Fable.Entity) (decl: Fable.ClassDecl) =
        let ta = makeErasedUnionTypeAnnotation com ctx Map.empty ent

        let entParams = FSharp2Fable.Util.getEntityGenArgs ent |> makeTypeParamDecl com ctx

        TypeAliasDeclaration(decl.Name, entParams, ta)
        |> asModuleDeclaration ent.IsPublic

    let transformTypeScriptTaggedUnionDeclaration
        com
        ctx
        (ent: Fable.Entity)
        (attArgs: obj list)
        (decl: Fable.ClassDecl)
        =
        let ta = makeTypeScriptTaggedUnionTypeAnnotation com ctx Map.empty ent attArgs

        let entParams = FSharp2Fable.Util.getEntityGenArgs ent |> makeTypeParamDecl com ctx

        TypeAliasDeclaration(decl.Name, entParams, ta)
        |> asModuleDeclaration ent.IsPublic

    let rec transformDeclaration (com: IBabelCompiler) ctx decl =
        let withCurrentScope ctx (usedNames: Set<string>) f =
            let ctx =
                { ctx with UsedNames = { ctx.UsedNames with CurrentDeclarationScope = HashSet usedNames } }

            let result = f ctx

            ctx.UsedNames.DeclarationScopes.UnionWith(ctx.UsedNames.CurrentDeclarationScope)

            result

        match decl with
        | Fable.ModuleDeclaration decl -> decl.Members |> List.collect (transformDeclaration com ctx)

        | Fable.ActionDeclaration decl ->
            withCurrentScope ctx decl.UsedNames
            <| fun ctx -> transformAction com ctx decl.Body

        | Fable.MemberDeclaration decl ->
            withCurrentScope ctx decl.UsedNames
            <| fun ctx ->
                let memb = com.GetMember(decl.MemberRef)

                let decl =
                    match memb.DeclaringEntity with
                    | Some entRef when memb.IsConstructor ->
                        { decl with Body = passExplicitBaseArgs com ctx (com.GetEntity entRef) decl.Body }
                    | _ -> decl

                let valueExpr =
                    match decl.Body with
                    | body when memb.IsValue -> transformAsExpr com ctx body |> Some
                    // Some calls with special attributes (like React lazy or memo) can turn the surrounding function into a value
                    | Fable.Call(callee,
                                 ({
                                      ThisArg = None
                                      MemberRef = Some m
                                  } as callInfo),
                                 _,
                                 r) as body ->
                        match com.TryGetMember(m), callInfo.Args with
                        | Some m, _ when hasAttribute "Fable.Core.JS.RemoveSurroundingArgsAttribute" m.Attributes ->
                            transformAsExpr com ctx body |> Some
                        | Some m, arg :: restArgs when
                            hasAttribute "Fable.Core.JS.WrapSurroundingFunctionAttribute" m.Attributes
                            ->
                            let arg = transformModuleFunction com ctx memb decl.Name decl.Args arg

                            let callee = com.TransformAsExpr(ctx, callee)

                            let restArgs = List.map (fun e -> com.TransformAsExpr(ctx, e)) restArgs

                            callFunction com ctx r callee [] (arg :: restArgs) |> Some
                        | _ -> None
                    | _ -> None

                let decls =
                    match valueExpr with
                    | Some value ->
                        ModuleDecl(
                            decl.Name,
                            isPublic = memb.IsPublic,
                            isMutable = memb.IsMutable,
                            typ = decl.Body.Type,
                            ?doc = decl.XmlDoc
                        )
                        |> declareModuleMember com ctx value
                        |> List.singleton
                    | None ->
                        let expr = transformModuleFunction com ctx memb decl.Name decl.Args decl.Body

                        if hasAttribute Atts.entryPoint memb.Attributes then
                            [ declareEntryPoint com ctx expr ]
                        else
                            match tryUnknownSignature com ctx memb decl.Args expr with
                            | Some(parameters, returnType, typeParameters) ->
                                [
                                    FunctionSignatureDeclaration(
                                        parameters,
                                        Identifier.identifier decl.Name,
                                        returnType,
                                        typeParameters,
                                        decl.XmlDoc
                                    )
                                    |> asModuleDeclaration memb.IsPublic
                                    ModuleDecl(decl.Name, isPublic = memb.IsPublic)
                                    |> declareModuleMember com ctx expr
                                ]
                            | None ->
                                [
                                    ModuleDecl(decl.Name, isPublic = memb.IsPublic, ?doc = decl.XmlDoc)
                                    |> declareModuleMember com ctx expr
                                ]

                let isDefaultExport =
                    List.contains "export-default" decl.Tags
                    || (com.TryGetMember(decl.MemberRef)
                        |> Option.map (fun m -> hasAttribute Atts.exportDefault m.Attributes)
                        |> Option.defaultValue false)

                if List.contains "remove-declaration" decl.Tags then
                    []
                elif not isDefaultExport then
                    decls
                else
                    decls
                    @ [ ExportDefaultDeclaration(Choice2Of2(Expression.identifier (decl.Name))) ]

        | Fable.ClassDeclaration decl ->
            match com.GetEntity(decl.Entity) with
            // fickl: annotations write the EmitType text, so a type with no JS value has nothing to declare
            | ent when
                hasAttribute Atts.ficklEmitType ent.Attributes
                && (ent.IsInterface || FSharp2Fable.Util.isErasedOrStringEnumEntity ent)
                ->
                []
            | Patterns.Try (tryFindAnyEntAttribute [ Atts.stringEnum; Atts.erase; Atts.tsTaggedUnion ]) (att, attArgs) as ent ->
                match com.IsTypeScript, ent.IsFSharpUnion, att with
                | true, true, Atts.stringEnum -> [ transformStringEnumDeclaration ent attArgs decl ]
                | true, true, Atts.erase -> [ transformErasedUnionDeclaration com ctx ent decl ]
                | true, true, Atts.tsTaggedUnion ->
                    [ transformTypeScriptTaggedUnionDeclaration com ctx ent attArgs decl ]
                | _ -> []
            | ent when FSharp2Fable.Util.isTsEnumEntity com ent -> transformTsEnumDeclaration com ctx ent decl
            | ent when ent.IsInterface ->
                if com.IsTypeScript then
                    [ transformInterfaceDeclaration com ctx ent decl ]
                else
                    []
            | ent ->
                if FSharp2Fable.Util.isPojoDefinedByConsArgsEntity ent then
                    if com.Options.Language = TypeScript then
                        transformPojoDefinedByConsArgsToInterface com ctx ent decl
                    else
                        []
                else
                    let decl, nativeProperties = NativeDecorators.prepareClass com ctx decl
                    let ctx = { ctx with NativeAutoProperties = nativeProperties }

                    let classMembers =
                        decl.AttachedMembers
                        |> List.toArray
                        |> Array.collect (fun memb ->
                            withCurrentScope ctx memb.UsedNames
                            <| fun ctx ->
                                memb.ImplementedSignatureRef
                                |> Option.bind (com.TryGetMember)
                                |> Option.orElseWith (fun () -> com.TryGetMember(memb.MemberRef))
                                |> function
                                    | None -> [||]
                                    | Some info ->
                                        if not memb.IsMangled && (info.IsGetter || info.IsSetter) then
                                            transformAttachedProperty com ctx ent info memb
                                        else
                                            transformAttachedMethod com ctx ent info memb
                        )

                    let tryKeyName =
                        function
                        | Expression.Identifier(Identifier(name, _)) -> Some name
                        | _ -> None

                    classMembers
                    |> Array.choose (
                        function
                        | ClassMethod(ClassFunction(key, _), _, _, _, _, _, _, _, _, _) ->
                            tryKeyName key |> Option.map (fun n -> n, false, false)
                        | ClassMethod(ClassGetter(key, _), _, _, _, _, _, _, _, _, _) ->
                            tryKeyName key |> Option.map (fun n -> n, true, false)
                        | ClassMethod(ClassSetter(key, _), _, _, _, _, _, _, _, _, _) ->
                            tryKeyName key |> Option.map (fun n -> n, false, true)
                        | _ -> None
                    )
                    |> Array.groupBy (fun (name, _, _) -> name)
                    |> Array.iter (fun (name, entries) ->
                        let isValidGetterSetterPair =
                            entries.Length = 2
                            && entries |> Array.exists (fun (_, isGetter, _) -> isGetter)
                            && entries |> Array.exists (fun (_, _, isSetter) -> isSetter)

                        if entries.Length > 1 && not isValidGetterSetterPair then
                            $"Overloads are not supported when using [<AttachMembers>]: '{name}' in {decl.Name} is defined more than once. Rename one of the members."
                            |> addWarning com [] None
                    )

                    match decl.Constructor with
                    | Some cons ->
                        withCurrentScope ctx cons.UsedNames
                        <| fun ctx -> transformClassWithPrimaryConstructor com ctx ent decl classMembers cons
                    | None ->
                        if ent.IsFSharpUnion then
                            transformUnion com ctx ent decl.Name decl.XmlDoc classMembers
                        else
                            transformClassWithCompilerGeneratedConstructor com ctx ent decl classMembers

    let transformImports isTypeScript (imports: Import seq) : ModuleDeclaration list =
        let statefulImports = ResizeArray()

        imports
        |> Seq.map (fun import ->
            let specifier =
                import.LocalIdent
                |> Option.map (fun localId ->
                    let localId = Identifier.identifier (localId)

                    match import.Selector with
                    | "*" -> ImportNamespaceSpecifier(localId)
                    | "default" -> ImportDefaultSpecifier(localId)
                    | memb ->
                        ImportMemberSpecifier(
                            localId,
                            Identifier.identifier (memb),
                            isTypeScript && import.IsTypeOnly
                        )
                )

            import.Path, specifier
        )
        |> Seq.groupBy fst
        |> Seq.collect (fun (path, specifiers) ->
            let mems, defs, alls =
                (([], [], []), Seq.choose snd specifiers)
                ||> Seq.fold (fun (mems, defs, alls) x ->
                    match x with
                    | ImportNamespaceSpecifier(_) -> mems, defs, x :: alls
                    | ImportDefaultSpecifier(_) -> mems, x :: defs, alls
                    | _ -> x :: mems, defs, alls
                )
            // We used to have trouble when mixing member, default and namespace imports,
            // issue an import statement for each kind just in case
            [ mems; defs; alls ]
            |> List.choose (
                function
                | [] -> None
                | specifiers ->
                    ImportDeclaration(List.toArray specifiers, StringLiteral.stringLiteral (path))
                    |> Some
            )
            |> function
                | [] ->
                    // If there are no specifiers, this is just an import for side effects,
                    // put it after the other ones to match standard JS practices, see #2228
                    ImportDeclaration([||], StringLiteral.stringLiteral (path))
                    |> statefulImports.Add

                    []
                | decls -> decls
        )
        |> fun staticImports -> [ yield! staticImports; yield! statefulImports ]

    let transformDirectivesPrologue (directives: string seq) =
        directives
        |> Seq.map (DirectivePrologue >> DirectivePrologueDeclaration)
        |> Seq.toList

    let getIdentForImport (com: IBabelCompiler) (ctx: Context) noMangle (path: string) (selector: string) =
        if System.String.IsNullOrEmpty selector then
            selector, None
        else
            let selector, alias =
                match selector with
                | Naming.Regex IMPORT_SELECTOR_REGEX (_ :: selector :: alias :: _) ->
                    let alias =
                        if alias.Length = 0 then
                            if selector = "*" || selector = "default" then
                                Path.GetFileNameWithoutExtension(path).Replace("-", "_")
                            else
                                selector
                        else
                            alias

                    selector, alias
                | _ -> selector, selector

            let alias =
                if noMangle then
                    let noConflict = ctx.UsedNames.RootScope.Add(alias)

                    if not noConflict then
                        com.WarnOnlyOnce($"Import %s{alias} conflicts with existing identifier in root scope")

                    alias
                else
                    getUniqueNameInRootScope ctx alias

            selector, Some alias

module Compiler =
    open Util

    type BabelCompiler(com: Compiler) =
        let onlyOnceWarnings = HashSet<string>()
        let imports = Dictionary<string, Import>()
        let mutable typePositionDepth = 0
        let symbolKeys = Dictionary<string, Expression>()
        let symbolDeclarations = ResizeArray<ModuleDeclaration>()
        let topDirectivesPrologue = HashSet<string>()
        let isTypeScript = com.Options.Language = TypeScript

        interface IBabelCompiler with
            member _.IsTypeScript = isTypeScript

            member _.WarnOnlyOnce(msg, ?range) =
                if onlyOnceWarnings.Add(msg) then
                    addWarning com [] range msg

            member com.GetImportExpr(ctx, selector, path, r, noMangle) =
                let noMangle = defaultArg noMangle false
                let selector = selector.Trim()
                let path = path.Trim()
                let cachedName = path + "::" + selector

                let isTypeOnly = typePositionDepth > 0

                match imports.TryGetValue(cachedName) with
                | true, i ->
                    if i.IsTypeOnly && not isTypeOnly then
                        imports[cachedName] <- { i with IsTypeOnly = false }

                    match i.LocalIdent with
                    | Some localIdent -> Expression.identifier (localIdent)
                    | None -> Expression.nullLiteral ()
                | false, _ ->
                    let selector, localId = getIdentForImport com ctx noMangle path selector

                    if selector = Naming.placeholder then
                        "`importMember` must be assigned to a variable" |> addError com [] r

                    let i =
                        {
                            Selector = selector
                            Path = path
                            LocalIdent = localId
                            IsTypeOnly = isTypeOnly
                        }

                    imports.Add(cachedName, i)

                    match localId with
                    | Some localId -> Expression.identifier (localId)
                    | None -> Expression.nullLiteral ()

            member bcom.GetSymbolKey(ctx, expression, path, sourceFile) =
                if String.IsNullOrWhiteSpace expression then
                    addError com [] None "SymbolKey requires a non-empty symbol expression"
                    Expression.nullLiteral ()
                elif not (String.IsNullOrEmpty path) then
                    let path =
                        match sourceFile with
                        | Some file when path.StartsWith(".", StringComparison.Ordinal) ->
                            FSharp2Fable.Util.fixImportedRelativePath com path file
                        | _ -> path

                    (bcom :> IBabelCompiler).TransformImport(ctx, expression, path)
                elif Regex.IsMatch(expression, @"^[A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*)*$") then
                    RawExpression(expression, None)
                else
                    let name = "$ficklSymbol$" + (uint32 (Naming.stringHash expression)).ToString("x8")

                    match sourceFile with
                    | Some file when Path.normalizePathAndEnsureFsExtension file <> com.CurrentFile ->
                        let path = Path.getRelativeFileOrDirPath false com.CurrentFile false file
                        (bcom :> IBabelCompiler).GetImportExpr(ctx, name, path, None)
                    | _ ->
                        match symbolKeys.TryGetValue expression with
                        | true, key -> key
                        | _ ->
                            if ctx.UsedNames.RootScope.Contains name then
                                addError com [] None "A declaration conflicts with a generated SymbolKey binding"

                            ctx.UsedNames.RootScope.Add name |> ignore
                            let key = Expression.identifier name
                            let value = RawExpression(expression, None)

                            symbolDeclarations.Add(
                                Declaration.variableDeclaration (
                                    Const,
                                    name,
                                    init = value,
                                    ?annotation =
                                        (if isTypeScript then
                                             Some UniqueSymbolTypeAnnotation
                                         else
                                             None)
                                )
                                |> ExportNamedDeclaration
                            )

                            symbolKeys.Add(expression, key)
                            key

            member _.GetSymbolDeclarations() = List.ofSeq symbolDeclarations
            member _.GetAllImports() = imports.Values :> _
            member bcom.TransformAsExpr(ctx, e) = transformAsExpr bcom ctx e

            member bcom.TransformAsStatements(ctx, ret, e) = transformAsStatements bcom ctx ret e

            member bcom.TransformFunction(ctx, name, args, body) =
                transformFunction bcom ctx name args body

            member _.InTypePosition(build) =
                typePositionDepth <- typePositionDepth + 1

                try
                    build ()
                finally
                    typePositionDepth <- typePositionDepth - 1

            member _.MarkValueImport(localName) =
                imports
                |> Seq.tryFind (fun kv -> kv.Value.IsTypeOnly && kv.Value.LocalIdent = Some localName)
                |> Option.iter (fun kv -> imports[kv.Key] <- { kv.Value with IsTypeOnly = false })

            member bcom.TransformImport(ctx, selector, path) =
                transformImport bcom ctx None selector path

            member _.SaveTopDirectivePrologue(text: string) =
                topDirectivesPrologue.Add text |> ignore

            member _.GetAllAllTopDirectivesPrologue() = topDirectivesPrologue |> Seq.cast

        interface Compiler with
            member _.Options = com.Options
            member _.Plugins = com.Plugins
            member _.LibraryDir = com.LibraryDir
            member _.CurrentFile = com.CurrentFile
            member _.OutputDir = com.OutputDir
            member _.OutputType = com.OutputType
            member _.ProjectFile = com.ProjectFile
            member _.ProjectOptions = com.ProjectOptions
            member _.SourceFiles = com.SourceFiles
            member _.IncrementCounter() = com.IncrementCounter()

            member _.IsPrecompilingInlineFunction = com.IsPrecompilingInlineFunction

            member _.WillPrecompileInlineFunction(file) = com.WillPrecompileInlineFunction(file)

            member _.GetImplementationFile(fileName) = com.GetImplementationFile(fileName)

            member _.GetRootModule(fileName) = com.GetRootModule(fileName)
            member _.TryGetEntity(entRef) = com.TryGetEntity(entRef)
            member _.GetInlineExpr(fullName) = com.GetInlineExpr(fullName)

            member _.AddWatchDependency(fileName) = com.AddWatchDependency(fileName)
            member _.AddSignatureDependency(fileName) = com.AddSignatureDependency(fileName)

            member _.AddLog(msg, severity, ?range, ?fileName: string, ?tag: string, ?code: string) =
                com.AddLog(msg, severity, ?range = range, ?fileName = fileName, ?tag = tag, ?code = code)

    let makeCompiler com = BabelCompiler(com)

    let transformFile (com: Compiler) (file: Fable.File) =
        let com = makeCompiler com :> IBabelCompiler

        let declScopes =
            let hs = HashSet()

            for decl in file.Declarations do
                hs.UnionWith(decl.UsedNames)

            hs

        let ctx =
            {
                File = file
                UsedNames =
                    {
                        RootScope = HashSet file.UsedNamesInRootScope
                        DeclarationScopes = declScopes
                        CurrentDeclarationScope = Unchecked.defaultof<_>
                    }
                DecisionTargets = []
                HoistVars = fun _ -> false
                TailCallOpportunity = None
                OptimizeTailCall = fun () -> ()
                ScopedTypeParams = Set.empty
                BoundTypes = Map.empty
                ForcedIdents = Set.empty
                IsNativeAsync = false
                NativeGeneratorType = None
                SymbolAccessCache = Dictionary()
                NativeAutoProperties = Map.empty
            }

        let registerSymbolKey (att: Fable.Attribute) =
            match att.ConstructorArgs with
            | (:? string as expression) :: rest when
                not (String.IsNullOrWhiteSpace expression)
                && (rest
                    |> List.forall (
                        function
                        | :? string as path -> String.IsNullOrEmpty path
                        | _ -> false
                    ))
                && not (Regex.IsMatch(expression, @"^[A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*)*$"))
                ->
                com.GetSymbolKey(ctx, expression, "", Some com.CurrentFile) |> ignore
            | _ -> ()

        let rec registerSymbolKeys declarations =
            for declaration in declarations do
                match declaration with
                | Fable.ModuleDeclaration moduleDecl -> registerSymbolKeys moduleDecl.Members
                | Fable.ClassDeclaration classDecl ->
                    let ent = com.GetEntity classDecl.Entity

                    for field in ent.FSharpFields do
                        FSharp2Fable.FsField.TrySymbolKey field
                        |> Option.iter (fst >> registerSymbolKey)

                    for memberInfo in ent.MembersFunctionsAndValues do
                        memberInfo.Attributes
                        |> Seq.filter (fun att -> att.Entity.FullName = Atts.ficklSymbolKey)
                        |> Seq.iter registerSymbolKey
                | _ -> ()

        registerSymbolKeys file.Declarations
        let rootDecls = List.collect (transformDeclaration com ctx) file.Declarations

        // fickl: --nativeErrors switches the library's Exception to a native Error before this file's code runs
        let rootDecls =
            if List.contains Fickl.Options.nativeErrorsDefine com.Options.Define then
                (Lib.libCall com ctx None "Util" "enableNativeErrors" [] []
                 |> ExpressionStatement
                 |> PrivateModuleDeclaration)
                :: rootDecls
            else
                rootDecls

        let topDirectivesPrologue =
            com.GetAllAllTopDirectivesPrologue() |> transformDirectivesPrologue

        let importDecls = com.GetAllImports() |> transformImports com.IsTypeScript

        let body =
            topDirectivesPrologue @ importDecls @ com.GetSymbolDeclarations() @ rootDecls
            |> List.toArray

        Program(body)
