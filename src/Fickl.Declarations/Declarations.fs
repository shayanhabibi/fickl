/// TypeScript declaration files (.d.ts) for Fable's JavaScript output, from the same compilation.
/// The file's Fable AST goes through Fable2Babel a second time in TypeScript mode, and the result is
/// reduced to the shapes of what the JavaScript actually exports: TypeScript-only helpers, such as the
/// union case functions, never appear in a declaration because the JavaScript has no such value.
module Fickl.Declarations

open System
open System.Collections.Generic
open FSharp.Compiler.Symbols
open Fable
open Fable.AST
open Fable.AST.Babel
open Fable.Transforms
open Fable.Transforms.FSharp2Fable
open Fable.Transforms.Printer
open Fable.Transforms.BabelPrinter.PrinterExtensions

let mutable private enabled = false

/// Turns declaration output on or off for the compilations that follow (`--declaration`).
let setEnabled value = enabled <- value

let isEnabled () = enabled

/// The declaration file TypeScript looks up for a JavaScript file.
let getPath (jsPath: string) =
    let replace (ext: string) (dtsExt: string) =
        jsPath.Substring(0, jsPath.Length - ext.Length) + dtsExt

    if jsPath.EndsWith(".mjs", StringComparison.Ordinal) then
        replace ".mjs" ".d.mts"
    elif jsPath.EndsWith(".cjs", StringComparison.Ordinal) then
        replace ".cjs" ".d.cts"
    elif jsPath.EndsWith(".js", StringComparison.Ordinal) then
        replace ".js" ".d.ts"
    else
        jsPath + ".d.ts"

/// The compiler as the TypeScript pass sees it. That pass only produces declarations, so its errors are
/// reported as warnings, and its other messages are dropped because the JavaScript pass already gave them.
let private typeScriptView (com: Compiler) : Compiler =
    let options = { com.Options with Language = TypeScript }

    { new Compiler with
        member _.CurrentFile = com.CurrentFile
        member _.LibraryDir = com.LibraryDir
        member _.Options = options
        member _.Plugins = com.Plugins
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
            match severity with
            | Severity.Error ->
                com.AddLog(
                    "TypeScript declarations: " + msg,
                    Severity.Warning,
                    ?range = range,
                    ?fileName = fileName,
                    ?tag = tag,
                    ?code = code
                )
            | Severity.Warning
            | Severity.Info -> ()
    }

/// Erased, string-enum and TypeScriptTaggedUnion unions are only types, so FSharp2Fable leaves them out
/// of a JavaScript compilation. Rebuild the declarations it would have made for TypeScript.
let private typeOnlyUnionDeclarations (com: Compiler) =
    let rec collect (decls: FSharpImplementationFileDeclaration list) =
        decls
        |> List.collect (
            function
            | FSharpImplementationFileDeclaration.Entity(fsEnt, []) when fsEnt.IsFSharpUnion ->
                let entRef = FsEnt.Ref fsEnt
                let ent = com.GetEntity(entRef)

                if Util.isErasedOrStringEnumEntity ent && not (Util.isGlobalOrImportedEntity ent) then
                    match Helpers.getEntityDeclarationName com entRef with
                    | name when String.IsNullOrEmpty(name) -> []
                    | name ->
                        [
                            Fable.ClassDeclaration
                                {
                                    Name = name
                                    Entity = entRef
                                    Constructor = None
                                    BaseCall = None
                                    AttachedMembers = []
                                    Tags = Fable.Tags.empty
                                    XmlDoc = None
                                }
                        ]
                else
                    []
            | FSharpImplementationFileDeclaration.Entity(_, sub) -> collect sub
            | _ -> []
        )

    com.GetImplementationFile(com.CurrentFile) |> collect

let rec private keyName (key: Expression) =
    match key with
    | Expression.Identifier(Identifier(name, _)) -> name
    | Literal(Literal.StringLiteral(StringLiteral(value, _))) -> value
    | Literal(NumericLiteral(value, _)) -> string<float> value
    | MemberExpression(object, property, _, _) -> keyName object + "." + keyName property
    | _ -> string<Expression> key

let private declarationNames (decl: Declaration) =
    match decl with
    | ClassDeclaration(id = Some(Identifier(name, _)))
    | FunctionDeclaration(id = Identifier(name, _))
    | FunctionSignatureDeclaration(id = Identifier(name, _))
    | InterfaceDeclaration(id = Identifier(name, _)) -> [ name ]
    | EnumDeclaration(name = name)
    | TypeAliasDeclaration(name = name) -> [ name ]
    | Declaration.VariableDeclaration(VariableDeclaration(declarations, _, _)) ->
        declarations
        |> Array.map (fun (VariableDeclarator(name, _, _, _, _)) -> name)
        |> List.ofArray
    | ClassDeclaration(id = None) -> []

let rec private undecoratedModule =
    function
    | DecoratedModuleDeclaration(_, declaration) -> undecoratedModule declaration
    | declaration -> declaration

let rec private classMemberKey (memb: ClassMember) =
    match memb with
    | DecoratedClassMember(_, memberDeclaration) -> classMemberKey memberDeclaration
    | ClassAutoAccessor(key, _, _, isStatic, _, _, _) -> Some(isStatic, keyName key)
    | ClassMethod(kind, _, _, isStatic, _, _, _, _, _, _) ->
        match kind with
        | ClassPrimaryConstructor _ -> None
        | ClassFunction(key, _)
        | ClassGetter(key, _)
        | ClassSetter(key, _) -> Some(isStatic, keyName key)
    | ClassProperty(key, _, _, isStatic, _, _, _, _, _) -> Some(isStatic, keyName key)

/// What the JavaScript output exports, and the members of each class it declares.
type private JsSurface =
    {
        Exports: HashSet<string>
        ClassMembers: Dictionary<string, HashSet<bool * string>>
    }

let private jsSurface (Program body) =
    let exports = HashSet<string>()
    let classMembers = Dictionary<string, HashSet<bool * string>>()

    let addClass (decl: Declaration) =
        match decl with
        | ClassDeclaration(members, Some(Identifier(name, _)), _, _, _, _, _, _) ->
            classMembers[name] <- HashSet(Seq.choose classMemberKey members)
        | _ -> ()

    for md in body do
        match undecoratedModule md with
        | ExportNamedDeclaration decl ->
            exports.UnionWith(declarationNames decl)
            addClass decl
        | ExportNamedReferences(specifiers, _) ->
            for ExportSpecifier(_, Identifier(exported, _)) in specifiers do
                exports.Add(exported) |> ignore
        | ExportDefaultDeclaration declaration ->
            exports.Add("default") |> ignore

            match declaration with
            | Choice1Of2 decl -> addClass decl
            | Choice2Of2 _ -> ()
        | PrivateModuleDeclaration(Statement.Declaration decl) -> addClass decl
        | _ -> ()

    {
        Exports = exports
        ClassMembers = classMembers
    }

// Names a declaration refers to, so the declaration file imports only what it uses.

let rec private addExpressionRoot (names: HashSet<string>) (expr: Expression) =
    match expr with
    | Expression.Identifier(Identifier(name, _)) -> names.Add(name) |> ignore
    | MemberExpression(object, _, _, _) -> addExpressionRoot names object
    | CallExpression(callee, _, typeArguments, _) ->
        addExpressionRoot names callee
        typeArguments |> Array.iter (addTypeNames names)
    | _ -> ()

and private addTypeNames (names: HashSet<string>) (typ: TypeAnnotation) =
    let add = addTypeNames names

    match typ with
    | AliasTypeAnnotation(Identifier(name, _), typeArguments) ->
        names.Add(name.Split('.')[0]) |> ignore
        typeArguments |> Array.iter add
    | UnionTypeAnnotation types
    | IntersectionTypeAnnotation types
    | TupleTypeAnnotation types -> types |> Array.iter add
    | LabelledTupleTypeAnnotation elements ->
        for TupleElement(_, t, _) in elements do
            add t
    | ObjectTypeAnnotation members -> members |> Array.iter (addAbstractMemberNames names)
    | FunctionTypeAnnotation(parameters, returnType, spread) ->
        for FunctionTypeParam(_, t, _) in parameters do
            add t

        spread |> Option.iter (fun (FunctionTypeParam(_, t, _)) -> add t)
        add returnType
    | ArrayTypeAnnotation t
    | KeyofTypeAnnotation t -> add t
    | IndexedTypeAnnotation(t, prop) ->
        add t
        add prop
    | TypeofTypeAnnotation expr -> addExpressionRoot names expr
    | EmitTypeAnnotation(_, args) -> args |> Array.iter add
    | TypePredicateTypeAnnotation(_, t, _) -> t |> Option.iter add
    | AnyTypeAnnotation
    | UnknownTypeAnnotation
    | VoidTypeAnnotation
    | UndefinedTypeAnnotation
    | NullTypeAnnotation
    | StringTypeAnnotation
    | UniqueSymbolTypeAnnotation
    | NumberTypeAnnotation
    | BooleanTypeAnnotation
    | LiteralTypeAnnotation _ -> ()

and private addParameterNames names (parameters: Parameter array) =
    for Parameter.Parameter(_, annotation, _) in parameters do
        annotation |> Option.iter (addTypeNames names)

and private addTypeParameterNames names (typeParameters: TypeParameter array) =
    for TypeParameter(_, bound, defaultType) in typeParameters do
        bound |> Option.iter (addTypeNames names)
        defaultType |> Option.iter (addTypeNames names)

and private addAbstractMemberNames names (memb: AbstractMember) =
    match memb with
    | AbstractIndexSignature(_, keyType, valueType, _, _) ->
        addTypeNames names keyType
        addTypeNames names valueType
    | AbstractCallSignature(parameters, returnType, typeParameters, _)
    | AbstractConstructSignature(parameters, returnType, typeParameters, _) ->
        addParameterNames names parameters
        addTypeNames names returnType
        addTypeParameterNames names typeParameters
    | AbstractProperty(_, returnType, _, _, _) -> addTypeNames names returnType
    | AbstractMethod(_, _, parameters, returnType, typeParameters, _, _) ->
        addParameterNames names parameters
        addTypeNames names returnType
        addTypeParameterNames names typeParameters

let rec private addClassMemberNames names memberDeclaration =
    match memberDeclaration with
    | DecoratedClassMember(_, memberDeclaration) -> addClassMemberNames names memberDeclaration
    | ClassMethod(_, parameters, _, _, _, _, returnType, typeParameters, _, _) ->
        addParameterNames names parameters
        returnType |> Option.iter (addTypeNames names)
        addTypeParameterNames names typeParameters
    | ClassProperty(typeAnnotation = annotation)
    | ClassAutoAccessor(typeAnnotation = annotation) -> annotation |> Option.iter (addTypeNames names)

let private addDeclarationNames names (decl: Declaration) =
    match decl with
    | ClassDeclaration(members, _, _, superClass, implements, typeParameters, _, _) ->
        match superClass with
        | Some(SuperType t) -> addTypeNames names t
        | Some(SuperExpression expr) -> addExpressionRoot names expr
        | None -> ()

        implements |> Array.iter (addTypeNames names)
        addTypeParameterNames names typeParameters

        members |> Array.iter (addClassMemberNames names)
    | FunctionDeclaration(parameters, _, _, returnType, typeParameters, _, _) ->
        addParameterNames names parameters
        returnType |> Option.iter (addTypeNames names)
        addTypeParameterNames names typeParameters
    | FunctionSignatureDeclaration(parameters, _, returnType, typeParameters, _) ->
        addParameterNames names parameters
        addTypeNames names returnType
        addTypeParameterNames names typeParameters
    | InterfaceDeclaration(_, members, extends, typeParameters, _) ->
        members |> Array.iter (addAbstractMemberNames names)
        extends |> Array.iter (addTypeNames names)
        addTypeParameterNames names typeParameters
    | TypeAliasDeclaration(_, typeParameters, alias) ->
        addTypeParameterNames names typeParameters
        addTypeNames names alias
    | Declaration.VariableDeclaration(VariableDeclaration(declarations, _, _)) ->
        for VariableDeclarator(_, annotation, _, _, _) in declarations do
            annotation |> Option.iter (addTypeNames names)
    | EnumDeclaration _ -> ()

// Reducing TypeScript declarations to their shapes. Where the TypeScript output leaves a type to be
// inferred from code, the declaration says `any`.

let private orAny (typ: TypeAnnotation option) = defaultArg typ AnyTypeAnnotation

/// A declaration has no default values: a parameter with one is optional.
let private declarationParameter (Parameter.Parameter(name, annotation, flags) as p) =
    match flags.DefVal with
    | Some _ ->
        Parameter.Parameter(
            name,
            Some(orAny annotation),
            ParameterFlags(isOptional = true, isSpread = flags.IsSpread, isNamed = flags.IsNamed)
        )
    | None when flags.IsNamed -> p
    | None -> Parameter.Parameter(name, Some(orAny annotation), flags)

let private functionType (parameters: Parameter array) returnType =
    let isSimple (Parameter.Parameter(_, _, flags)) =
        not flags.IsSpread && not flags.IsNamed && Option.isNone flags.DefVal

    if Array.forall isSimple parameters then
        let parameters =
            parameters
            |> Array.map (fun (Parameter.Parameter(name, annotation, flags)) ->
                FunctionTypeParam(Identifier.identifier name, orAny annotation, flags.IsOptional)
            )

        Some(FunctionTypeAnnotation(parameters, returnType, None))
    else
        None

/// The type of a value whose declaration has no annotation, from its initializer where it says.
let private initializerType (init: Expression option) =
    match init with
    | Some(AsExpression(_, typ)) -> Some typ
    | Some(Literal(NumericLiteral _)) -> Some NumberTypeAnnotation
    | Some(Literal(Literal.StringLiteral _)) -> Some StringTypeAnnotation
    | Some(Literal(BooleanLiteral _)) -> Some BooleanTypeAnnotation
    | Some(NewExpression(Expression.Identifier id, _, typeArguments, _)) -> Some(AliasTypeAnnotation(id, typeArguments))
    // A mutable module value is an atom: called with no argument it reads the value, with one it sets it
    | Some(CallExpression(Expression.Identifier(Identifier("createAtom", _)), _, [| typ |], _)) ->
        let setter = Parameter.Parameter("value", Some typ, ParameterFlags())

        ObjectTypeAnnotation
            [|
                AbstractCallSignature([||], typ, [||], None)
                AbstractCallSignature([| setter |], VoidTypeAnnotation, [||], None)
            |]
        |> Some
    | Some(ArrowFunctionExpression(parameters, _, Some returnType, [||], _))
    | Some(FunctionExpression(_, parameters, _, Some returnType, [||], _)) -> functionType parameters returnType
    | _ -> None

/// The type a declaration gives a value: its annotation, unless that is `any` and the initializer says more.
let private valueType (annotation: TypeAnnotation option) (init: Expression option) =
    match annotation with
    | Some AnyTypeAnnotation
    | None -> initializerType init |> Option.orElse annotation |> orAny
    | Some annotation -> annotation

let rec private declarationClassMembers
    (isUnion: bool)
    (jsMembers: HashSet<bool * string> option)
    (members: ClassMember array)
    =
    let inJs memb =
        match memb, jsMembers, classMemberKey memb with
        // An abstract member has no JavaScript, but subclasses and callers still have it
        | ClassMethod(isAbstract = true), _, _ -> true
        | _, Some jsMembers, Some key -> jsMembers.Contains(key)
        | _, None, Some _ -> true
        | _, _, None -> true

    members
    |> Array.collect (fun memb ->
        match memb with
        | DecoratedClassMember(_, memberDeclaration) ->
            declarationClassMembers isUnion jsMembers [| memberDeclaration |]
        | ClassAutoAccessor(key, value, computed, isStatic, _, annotation, loc) ->
            declarationClassMembers
                isUnion
                jsMembers
                [|
                    ClassProperty(key, value, computed, isStatic, false, annotation, None, loc, None)
                |]
        // Parameter properties are not allowed in a declaration: declare the properties instead
        | ClassMethod(ClassPrimaryConstructor accessModifiers,
                      parameters,
                      _,
                      isStatic,
                      isAbstract,
                      _,
                      _,
                      typeParameters,
                      loc,
                      doc) ->
            let properties =
                parameters
                |> Array.mapi (fun i (Parameter.Parameter(name, annotation, flags)) ->
                    match Array.tryItem i accessModifiers with
                    | Some modifier ->
                        let modifier =
                            match modifier with
                            | AccessModifier.Public -> None
                            | modifier -> Some modifier

                        Some(
                            ClassProperty(
                                Expression.identifier name,
                                None,
                                false,
                                false,
                                flags.IsOptional,
                                Some(orAny annotation),
                                modifier,
                                None,
                                None
                            )
                        )
                    | None -> None
                )
                |> Array.choose id

            let constructor =
                ClassMethod(
                    ClassPrimaryConstructor [||],
                    Array.map declarationParameter parameters,
                    None,
                    isStatic,
                    isAbstract,
                    false,
                    None,
                    typeParameters,
                    loc,
                    doc
                )

            Array.append properties [| constructor |]
        // Fields assigned in the constructor have no member in the JavaScript class
        | ClassProperty(key, None, isComputed, false, isOptional, typeAnnotation, accessModifier, loc, doc) ->
            [|
                ClassProperty(
                    key,
                    None,
                    isComputed,
                    false,
                    isOptional,
                    Some(orAny typeAnnotation),
                    accessModifier,
                    loc,
                    doc
                )
            |]
        | memb when not (inJs memb) -> [||]
        | ClassProperty(key, value, isComputed, isStatic, isOptional, typeAnnotation, accessModifier, loc, doc) ->
            let typeAnnotation = valueType typeAnnotation value

            [|
                ClassProperty(
                    key,
                    None,
                    isComputed,
                    isStatic,
                    isOptional,
                    Some typeAnnotation,
                    accessModifier,
                    loc,
                    doc
                )
            |]
        | ClassMethod(kind, parameters, _, isStatic, isAbstract, isOverride, returnType, typeParameters, loc, doc) ->
            let returnType =
                match kind, returnType with
                | ClassSetter _, _ -> None
                // Union.cases() names the cases
                | ClassFunction(Expression.Identifier(Identifier("cases", _)), false), None when
                    isUnion && Array.isEmpty parameters
                    ->
                    Some(ArrayTypeAnnotation StringTypeAnnotation)
                | _ -> Some(orAny returnType)

            [|
                ClassMethod(
                    kind,
                    Array.map declarationParameter parameters,
                    None,
                    isStatic,
                    isAbstract,
                    isOverride,
                    returnType,
                    typeParameters,
                    loc,
                    doc
                )
            |]
    )

/// A declaration reduced to its shape, or None when it declares a value the JavaScript doesn't have.
let private declarationOf (js: JsSurface) (signed: HashSet<string>) (isValueKnown: string -> bool) (decl: Declaration) =
    match decl with
    | InterfaceDeclaration _
    | TypeAliasDeclaration _
    | EnumDeclaration(isDeclare = true) -> Some decl
    | EnumDeclaration(name = name)
    | ClassDeclaration(id = Some(Identifier(name, _)))
    | FunctionSignatureDeclaration(id = Identifier(name, _))
    | FunctionDeclaration(id = Identifier(name, _)) when not (isValueKnown name) -> None
    // The signature is what callers see; its implementation is declared by it
    | FunctionSignatureDeclaration(parameters, id, returnType, typeParameters, doc) ->
        FunctionDeclaration(
            Array.map declarationParameter parameters,
            BlockStatement [||],
            id,
            Some returnType,
            typeParameters,
            None,
            doc
        )
        |> Some
    | FunctionDeclaration(id = Identifier(name, _)) when signed.Contains(name) -> None
    | EnumDeclaration _ -> Some decl
    | ClassDeclaration(members, id, isAbstract, superClass, implements, typeParameters, loc, doc) ->
        let jsMembers =
            id
            |> Option.bind (fun (Identifier(name, _)) ->
                match js.ClassMembers.TryGetValue(name) with
                | true, members -> Some members
                | false, _ -> None
            )

        // A declared class extends a named class only
        let superClass =
            match superClass with
            | Some(SuperExpression(Expression.Identifier _ | MemberExpression _))
            | Some(SuperType(AliasTypeAnnotation _)) -> superClass
            | _ -> None

        let isUnion =
            match superClass with
            | Some(SuperType(AliasTypeAnnotation(Identifier(("Union" | "Union_1"), _), _)))
            | Some(SuperExpression(Expression.Identifier(Identifier(("Union" | "Union_1"), _)))) -> true
            | _ -> false

        ClassDeclaration(
            declarationClassMembers isUnion jsMembers members,
            id,
            isAbstract,
            superClass,
            implements,
            typeParameters,
            loc,
            doc
        )
        |> Some
    | FunctionDeclaration(parameters, body, id, returnType, typeParameters, loc, doc) ->
        FunctionDeclaration(
            Array.map declarationParameter parameters,
            body,
            id,
            Some(orAny returnType),
            typeParameters,
            loc,
            doc
        )
        |> Some
    | Declaration.VariableDeclaration(VariableDeclaration(declarations, kind, loc)) ->
        match
            declarations
            |> Array.filter (fun (VariableDeclarator(name, _, _, _, _)) -> isValueKnown name)
            |> Array.map (fun (VariableDeclarator(name, annotation, typeParameters, init, loc)) ->
                VariableDeclarator(name, Some(valueType annotation init), typeParameters, None, loc)
            )
        with
        | [||] -> None
        | declarations ->
            Declaration.VariableDeclaration(VariableDeclaration(declarations, kind, loc))
            |> Some

/// The declaration file for a file's JavaScript output: run Fable2Babel over the same Fable AST in
/// TypeScript mode and keep only the shapes of what `jsProgram` exports.
let transformFile (com: Compiler) (jsProgram: Program) (file: Fable.File) : Program =
    let js = jsSurface jsProgram
    let libraryPrefix = com.LibraryDir + "/"
    let com = typeScriptView com

    let declarations = typeOnlyUnionDeclarations com @ file.Declarations

    // A JavaScript compilation declares no interfaces or type-only unions, so their names are missing from
    // the root scope; imports must still be renamed around them, as in TypeScript output
    let rec typeNames (decls: Fable.Declaration list) =
        decls
        |> List.collect (
            function
            | Fable.ClassDeclaration decl -> [ decl.Name ]
            | Fable.ModuleDeclaration decl -> typeNames decl.Members
            | _ -> []
        )

    let file =
        Fable.File(declarations, Set.union file.UsedNamesInRootScope (set (typeNames declarations)))

    let (Program body) = Fable2Babel.Compiler.transformFile com file

    // Locals re-exported under another name must be declared even though they are not exported themselves
    let reexported = HashSet<string>()

    for md in body do
        match undecoratedModule md with
        | ExportNamedReferences(specifiers, None) ->
            for ExportSpecifier(Identifier(local, _), _) in specifiers do
                reexported.Add(local) |> ignore
        | _ -> ()

    let isExportedValue name = js.Exports.Contains(name)
    // Private classes stay declared: exported signatures can refer to them
    let isPrivateValue name =
        reexported.Contains(name) || js.ClassMembers.ContainsKey(name)

    let signed = HashSet<string>()

    for md in body do
        match undecoratedModule md with
        | ExportNamedDeclaration(FunctionSignatureDeclaration(id = Identifier(name, _)))
        | PrivateModuleDeclaration(Statement.Declaration(FunctionSignatureDeclaration(id = Identifier(name, _)))) ->
            signed.Add(name) |> ignore
        | _ -> ()

    let declarationOf = declarationOf js signed

    let declarations =
        body
        |> Array.choose (fun md ->
            match undecoratedModule md with
            | ExportNamedDeclaration decl -> declarationOf isExportedValue decl |> Option.map ExportNamedDeclaration
            | ExportDefaultDeclaration(Choice1Of2 decl) when js.Exports.Contains("default") ->
                declarationOf (fun _ -> true) decl
                |> Option.map (Choice1Of2 >> ExportDefaultDeclaration)
            | PrivateModuleDeclaration(Statement.Declaration decl) ->
                declarationOf isPrivateValue decl
                |> Option.map (Statement.Declaration >> PrivateModuleDeclaration)
            | ExportNamedReferences _
            | ExportAllDeclaration _ -> Some md
            | _ -> None
        )

    let used = HashSet<string>(reexported)

    for md in declarations do
        match undecoratedModule md with
        | ExportNamedDeclaration decl
        | ExportDefaultDeclaration(Choice1Of2 decl)
        | PrivateModuleDeclaration(Statement.Declaration decl) -> addDeclarationNames used decl
        | _ -> ()

    let imports =
        body
        |> Array.choose (
            function
            | ImportDeclaration(specifiers, source, attributes) ->
                specifiers
                |> Array.filter (
                    function
                    | ImportMemberSpecifier(Identifier(local, _), _, _)
                    | ImportDefaultSpecifier(Identifier(local, _))
                    | ImportNamespaceSpecifier(Identifier(local, _)) -> used.Contains(local)
                )
                |> function
                    | [||] -> None
                    | specifiers ->
                        // TypeScript output imports fable-library's .ts files; the JavaScript imports its .js files
                        let (StringLiteral(path, loc)) = source

                        let source =
                            if
                                path.StartsWith(libraryPrefix, StringComparison.Ordinal)
                                && path.EndsWith(".ts", StringComparison.Ordinal)
                            then
                                StringLiteral(path.Substring(0, path.Length - 3) + ".js", loc)
                            else
                                source

                        Some(ImportDeclaration(specifiers, source, attributes))
            | _ -> None
        )

    Program(Array.append imports declarations)

/// `declare` marks a value as ambient; `export default` takes none.
let private printDeclaration (printer: Printer) isDefault (decl: Declaration) =
    let declare () =
        if not isDefault then
            printer.Print("declare ")

    match decl with
    | InterfaceDeclaration _
    | TypeAliasDeclaration _
    | EnumDeclaration(isDeclare = true) -> printer.PrintDeclaration(decl)
    | EnumDeclaration _ ->
        declare ()
        printer.PrintDeclaration(decl)
    | ClassDeclaration(members, id, isAbstract, superClass, implements, typeParameters, loc, _) ->
        declare ()
        printer.PrintClass(id, isAbstract, superClass, typeParameters, implements, members, loc)
    | FunctionSignatureDeclaration _ -> printer.PrintDeclaration(decl)
    | FunctionDeclaration(parameters, _, id, returnType, typeParameters, _, _) ->
        declare ()
        printer.Print("function ")
        printer.PrintIdent(id)
        printer.Print(typeParameters)
        printer.Print("(")
        printer.PrintParameters(parameters)
        printer.Print(")")
        printer.PrintOptional(returnType, ": ")
    | Declaration.VariableDeclaration(VariableDeclaration(declarations, kind, _)) ->
        let kind =
            match kind with
            | Const -> "const "
            | Let -> "let "
            | Var -> "var "

        declare ()
        printer.Print(kind)

        printer.PrintArray(
            declarations,
            (fun p (VariableDeclarator(name, annotation, _, _, _)) ->
                p.Print(name)
                p.PrintOptional(annotation, ": ")
            ),
            (fun p -> p.Print(", "))
        )

/// Prints a declaration program made by `transformFile`.
let print (writer: Writer) (Program body) : Async<unit> =
    async {
        use printer = new PrinterImpl(writer)
        let p = printer :> Printer

        let endDeclaration () =
            if p.Column > 0 then
                p.Print(";")
                p.PrintNewLine()

        for md in body do
            match undecoratedModule md with
            | ImportDeclaration _
            | ExportNamedReferences _
            | ExportAllDeclaration _ -> p.Print(md)
            | ExportNamedDeclaration decl ->
                p.PrintJsDoc(decl.JsDoc)
                p.Print("export ")
                printDeclaration p false decl
            | ExportDefaultDeclaration(Choice1Of2 decl) ->
                p.PrintJsDoc(decl.JsDoc)
                p.Print("export default ")
                printDeclaration p true decl
            | PrivateModuleDeclaration(Statement.Declaration decl) ->
                p.PrintJsDoc(decl.JsDoc)
                printDeclaration p false decl
            | _ -> ()

            endDeclaration ()

        // A file that declares nothing is still a module
        if Array.isEmpty body then
            p.Print("export {};")
            p.PrintNewLine()

        do! printer.Flush()
    }
