module Fable.Tests.CompilationTests

open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.RegularExpressions
open Expecto
open Fable.Compiler.Util

let private data = Path.Combine(__SOURCE_DIRECTORY__, "data")

/// `fable precompile` writes each chunk of inline expressions twice: the compact copy the CLI
/// reads, and the browser copy fable-standalone reads. This asks four things of every chunk:
///
/// 1. it is there at all, so a writer that silently stopped is caught
/// 2. it decodes, through the very code fable-standalone runs in a browser - this is what catches
///    a type the codec has no coder for
/// 3. re-encoding what was decoded gives the file back, so reading it lost nothing
/// 4. every expression in it matches the one the CLI reads from the compact copy, so the two
///    cannot drift apart
///
/// This runs on .NET, so it covers the codec and its coders but not the JavaScript backend.
/// It relies on Thoth.Json being cross-platform and implemented "correctly".
///
/// The worker harness under src/fable-standalone/test/worker covers that side.
let private checkBrowserInlineExprs (precompiledDir: string) =
    let fableModulesDir = Path.Combine(precompiledDir, "fable_modules")
    let compact = PrecompiledInfoImpl.Load fableModulesDir :> Fable.Transforms.State.PrecompiledInfo

    let chunks =
        Seq.initInfinite id
        |> Seq.map (fun i -> PrecompiledInfoImpl.GetBrowserInlineExprsPath(fableModulesDir, i))
        |> Seq.takeWhile File.Exists
        |> Seq.toList

    // 1. written at all
    Expect.isNonEmpty chunks "Expected precompile to write a browser copy of the inline expressions"

    for path in chunks do
        let json = File.ReadAllText path

        // 2. readable by the browser codec
        match Fable.BrowserInlineExprs.fromString json with
        | Error error -> failtestf "Cannot read %s: %s" path error
        | Ok exprs ->
            Expect.isNonEmpty exprs $"No inline expression in {path}"

            // 3. nothing lost on the way in
            Expect.equal (Fable.BrowserInlineExprs.toString exprs) json $"Reading {path} back lost something"

            // 4. the same expressions the CLI reads
            for name, expr in exprs do
                match compact.TryGetInlineExpr name with
                | None -> failtestf "%s is in %s but not in the compact copy" name path
                | Some fromCompact ->
                    Expect.equal
                        (Fable.BrowserInlineExprs.toString [| name, expr |])
                        (Fable.BrowserInlineExprs.toString [| name, fromCompact |])
                        $"%s{name} differs between the two copies"

/// fickl: the snapshots end in `.ts.actual`, which tsc does not read, so the case is copied to temp/ with
/// them renamed to `.ts`, then type-checked under the TypeScript suite's own tsconfig
let private typeCheckTypeScript (testCaseDir: string) =
    let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../../.."))
    let caseDir = Path.Combine(root, "temp/tests/Integration/typecheck", Path.GetFileName testCaseDir)

    if Directory.Exists caseDir then
        Directory.Delete(caseDir, true)

    for source in Directory.EnumerateFiles(testCaseDir, "*", SearchOption.AllDirectories) do
        let relative = Path.GetRelativePath(testCaseDir, source)
        let topDir = relative.Split([| '/'; '\\' |])[0]

        if topDir <> "bin" && topDir <> "obj" && not (relative.EndsWith ".expected") then
            let target =
                Path.Combine(caseDir, Regex.Replace(relative, @"\.ts\.actual$", ".ts"))

            Directory.CreateDirectory(Path.GetDirectoryName target) |> ignore
            File.Copy(source, target)

    let config = Path.Combine(caseDir, "tsconfig.json")

    // A case can add options of its own in a tsconfig.json beside its project, to cover a flag the suite leaves off
    let compilerOptions =
        let own = Path.Combine(testCaseDir, "tsconfig.json")

        if File.Exists own then
            let options = JsonDocumentOptions(CommentHandling = JsonCommentHandling.Skip)
            let parsed = JsonNode.Parse(File.ReadAllText own, documentOptions = options)
            parsed["compilerOptions"].DeepClone().AsObject()
        else
            JsonObject()

    compilerOptions["rootDir"] <- JsonValue.Create root
    compilerOptions["noEmit"] <- JsonValue.Create true

    let settings = JsonObject()
    settings["extends"] <- JsonValue.Create(Path.Combine(root, "tests/TypeScript/tsconfig.json"))
    settings["compilerOptions"] <- compilerOptions
    settings["include"] <- JsonArray(JsonValue.Create "*.ts")

    File.WriteAllText(config, settings.ToJsonString())

    let info =
        System.Diagnostics.ProcessStartInfo(
            "node",
            [ Path.Combine(root, "node_modules/typescript/lib/tsc.js"); "-p"; config ],
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        )

    use child = System.Diagnostics.Process.Start info
    let stdout = child.StandardOutput.ReadToEndAsync()
    let stderr = child.StandardError.ReadToEndAsync()
    child.WaitForExit()
    Expect.equal child.ExitCode 0 $"tsc rejected {Path.GetFileName testCaseDir}:\n{stdout.Result}{stderr.Result}"

/// Compiles the project in one snapshot case and compares each `.expected` file with what it produced
let private compileCase (testCaseDir: string) =
    async {
        let project =
            Directory.GetFileSystemEntries(testCaseDir, "*.fsproj") |> Seq.exactlyOne
        // clean up old actual files
        for f in Directory.GetFileSystemEntries(testCaseDir, "*.actual") do
            File.Delete f

        let libraryDir = Path.Combine(testCaseDir, "library")
        let precompiledDir = Path.Combine(testCaseDir, "precompiled")

        let precompiledLibArgs =
            // If we have a library folder, it means we want to use Fable precompiledLib feature for the test
            if Directory.Exists libraryDir then
                let libraryProject =
                    Directory.GetFileSystemEntries(libraryDir, "*.fsproj") |> Seq.exactlyOne

                if Directory.Exists precompiledDir then
                    Directory.Delete(precompiledDir, true)

                let exitCode =
                    Fable.Cli.Entry.main
                        [| "precompile"; libraryProject; "--outDir"; precompiledDir; "--noCache" |]

                Expect.equal exitCode 0 "Expected precompile exit code to be 0"
                checkBrowserInlineExprs precompiledDir
                [| "--precompiledLib"; precompiledDir |]
            else
                [||]

        let outputArgs =
            if Directory.GetFileSystemEntries(testCaseDir, "*.ts.expected") |> Array.isEmpty then
                [| "-e"; ".jsx.actual" |]
            else
                [| "--lang"; "ts"; "-e"; ".ts.actual" |]

        // Compile project
        let exitCode =
            Array.concat
                [
                    [| project; "--cwd"; $"'%s{testCaseDir}'" |]
                    outputArgs
                    [| "--exclude"; "Fable.Core" |]
                    precompiledLibArgs
                ]
            |> Fable.Cli.Entry.main

        Expect.equal exitCode 0 "Expected exit code to be 0"

        let normalize content =
            Regex.Replace(content, @"(/fable-library-(?:js|ts))[^/]+", "$1")
            |> fun c -> Regex.Replace(c, @"[^""]*/precompiled/", "precompiled/")
            |> _.ReplaceLineEndings()
            |> _.Trim()

        for expected in Directory.GetFileSystemEntries(testCaseDir, "*.expected") do
            let actual = Path.ChangeExtension(expected, ".actual")
            Expect.isTrue (File.Exists actual) $"No actual file was produced for {expected}"
            let expectedContent = File.ReadAllText expected |> normalize
            let actualContent = File.ReadAllText actual |> normalize
            Expect.equal actualContent expectedContent "The expected content differs from the actual content"

        if Array.contains "ts" outputArgs then
            typeCheckTypeScript testCaseDir

        return ()
    }

let tests =
    Directory.EnumerateDirectories(data)
    |> Seq.map (fun testCaseDir -> testCaseAsync testCaseDir (compileCase testCaseDir))
    |> Seq.toList
    |> testList "Compilation"

/// fickl: a snapshot case for a project with `<Nullable>enable</Nullable>`. Nullness checking is a process-wide
/// switch, so Main runs this after the parallel compilations rather than among them.
let nullableReferenceTests =
    testCaseAsync "Nullable reference types" (compileCase (Path.Combine(__SOURCE_DIRECTORY__, "nullableReference")))

/// fickl: `--declaration` writes a .d.ts next to each JavaScript file. Each `<file>.d.ts.expected` here is
/// compared with the declaration file compiled from the project in the same folder.
/// The switch is process-wide, so Main runs this after the parallel compilations rather than among them.
let declarationTests =
    testCaseAsync
        "Declarations"
        (async {
            let dir = Path.Combine(__SOURCE_DIRECTORY__, "declarations")
            let outDir = Path.Combine(Path.GetTempPath(), "fickl-declarations-test")

            if Directory.Exists outDir then
                Directory.Delete(outDir, true)

            let exitCode =
                Fable.Cli.Entry.main
                    [|
                        Path.Combine(dir, "declarations.fsproj")
                        "--cwd"
                        $"'%s{dir}'"
                        "--outDir"
                        outDir
                        "--exclude"
                        "Fable.Core"
                        "--declaration"
                        "--noCache"
                    |]

            Expect.equal exitCode 0 "Expected exit code to be 0"

            let normalize content =
                Regex.Replace(content, @"(/fable-library-js)[^/]+", "$1").ReplaceLineEndings().Trim()

            for expected in Directory.GetFileSystemEntries(dir, "*.d.ts.expected") do
                let actual = Path.Combine(outDir, Path.GetFileNameWithoutExtension expected)
                Expect.isTrue (File.Exists actual) $"No declaration file was produced for {expected}"

                Expect.equal
                    (File.ReadAllText actual |> normalize)
                    (File.ReadAllText expected |> normalize)
                    $"{Path.GetFileName actual} differs from {Path.GetFileName expected}"

            // Mangled members are the quoted keys: the declarations must name those the JavaScript calls
            let mangledKeys pattern file =
                Regex.Matches(File.ReadAllText(Path.Combine(outDir, file)), pattern)
                |> Seq.map _.Groups[1].Value
                |> Set.ofSeq

            Expect.equal
                (mangledKeys "\"(Mangling\\.[^\"]+)\"\\(" "Mangling.d.ts")
                (mangledKeys "\\[\"(Mangling\\.[^\"]+)\"\\]" "Mangling.js")
                "Mangling.d.ts names members other than those Mangling.js calls"
        })

/// fickl: a compiled assembly that uses Fickl.Core attributes needs Fickl.Core as a reference. Compiled from its
/// sources into the project instead, the assembly's attributes could not be resolved and the compiler crashed (#34).
/// The consumer in `dllAttributes` references the compiled Support.dll, and Fickl.Core as a project.
let dllAttributeTests =
    testCaseAsync
        "Fickl.Core attributes in a compiled assembly"
        (async {
            let dir = Path.Combine(__SOURCE_DIRECTORY__, "dllAttributes")

            let run (workingDir: string) (fileName: string) (args: string list) =
                let info =
                    System.Diagnostics.ProcessStartInfo(
                        fileName,
                        args,
                        WorkingDirectory = workingDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    )

                use p = System.Diagnostics.Process.Start info
                let output = p.StandardOutput.ReadToEndAsync()
                let error = p.StandardError.ReadToEndAsync()
                p.WaitForExit()
                p.ExitCode, output.Result + error.Result

            let exitCode, log =
                run dir "dotnet" [ "build"; Path.Combine("Support", "Support.fsproj"); "-c"; "Release" ]

            Expect.equal exitCode 0 $"Building Support.dll failed:{System.Environment.NewLine}{log}"

            let outDir = Path.Combine(Path.GetTempPath(), "fickl-dll-attributes-test")

            if Directory.Exists outDir then
                Directory.Delete(outDir, true)

            let exitCode =
                Fable.Cli.Entry.main
                    [|
                        Path.Combine(dir, "Consumer", "Consumer.fsproj")
                        "--outDir"
                        outDir
                        "--exclude"
                        "Fable.Core"
                        "--noCache"
                    |]

            Expect.equal exitCode 0 "Expected exit code to be 0"
            File.WriteAllText(Path.Combine(outDir, "package.json"), """{ "type": "module" }""")
            let exitCode, log = run outDir "node" [ "Program.js" ]
            Expect.equal exitCode 0 $"The compiled program failed:{System.Environment.NewLine}{log}"
        })

/// <summary>Checks strict optional parameters and getters on .NET, JavaScript and TypeScript.</summary>
let strictOptionsTests =
    testCase "Strict options preserve nullable optional parameters" (fun () ->
        let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../../.."))
        let source = Path.Combine(__SOURCE_DIRECTORY__, "strictOptions")
        let output = Path.Combine(root, "temp/tests/Integration/strictOptions")

        let run (executable: string) (arguments: string list) =
            let info =
                System.Diagnostics.ProcessStartInfo(
                    executable,
                    arguments,
                    WorkingDirectory = root,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                )
            use child = System.Diagnostics.Process.Start info
            let stdout = child.StandardOutput.ReadToEndAsync()
            let stderr = child.StandardError.ReadToEndAsync()
            child.WaitForExit()
            Expect.equal child.ExitCode 0 (stdout.Result + stderr.Result)

        run "dotnet" [ "run"; "--project"; source; "-c"; "Release" ]

        for language in [ "javascript"; "typescript" ] do
            let destination = Path.Combine(output, language)
            let exitCode =
                Fable.Cli.Entry.main
                    [| source; "--outDir"; destination; "--lang"; language
                       "--strictOptions"; "--exclude"; "Fable.Core"; "--noCache" |]
            Expect.equal exitCode 0 $"Compiling strict options for {language} failed"

            if language = "javascript" then
                run "node" [ Path.Combine(destination, "Program.js") ]
            else
                let config = Path.Combine(destination, "tsconfig.json")
                let compiled = Path.Combine(destination, "compiled")
                let settings =
                    {| compilerOptions =
                        {| target = "es2020"
                           lib = [| "es2020"; "dom"; "esnext.temporal" |]
                           ``module`` = "es2020"
                           moduleResolution = "bundler"
                           strict = true
                           allowJs = true
                           skipLibCheck = true
                           rewriteRelativeImportExtensions = true
                           noEmitOnError = true
                           rootDir = root
                           outDir = compiled |}
                       ``include`` = [| "Program.ts"; "fable_modules/**/*.ts"; "fable_modules/**/*.js" |] |}
                File.WriteAllText(config, System.Text.Json.JsonSerializer.Serialize settings)
                run "node" [ Path.Combine(root, "node_modules/typescript/lib/tsc.js"); "-p"; config ]
                run "node" [ Path.Combine(compiled, Path.GetRelativePath(root, destination), "Program.js") ])

/// <summary>Executes native decorator output with standard and legacy TypeScript transforms.</summary>
let nativeDecoratorTests =
    testCase "Native decorators execute in JavaScript and TypeScript" (fun () ->
        let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../../.."))
        let source = Path.Combine(__SOURCE_DIRECTORY__, "data/nativeDecorators")
        let output = Path.Combine(root, "temp/tests/Integration/nativeDecorators")
        let run (executable: string) (arguments: string list) =
            let info = System.Diagnostics.ProcessStartInfo(executable, arguments,
                            WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true)
            use child = System.Diagnostics.Process.Start info
            let stdout = child.StandardOutput.ReadToEndAsync()
            let stderr = child.StandardError.ReadToEndAsync()
            child.WaitForExit()
            Expect.equal child.ExitCode 0 (stdout.Result + stderr.Result)

        for language in [ "javascript"; "typescript" ] do
            let destination = Path.Combine(output, language)
            let extension = if language = "javascript" then ".js" else ".ts"
            let arguments =
                [| Path.Combine(source, "nativeDecorators.fsproj"); "--outDir"; destination
                   "--lang"; language; "-e"; extension; "--exclude"; "Fable.Core"; "--noCache" |]
            let arguments = if language = "javascript" then Array.append arguments [|"--declaration"|] else arguments
            Expect.equal (Fable.Cli.Entry.main arguments) 0 $"Compiling native decorators for {language} failed"
            File.WriteAllText(Path.Combine(destination, "Consumer.ts"), """
import { Counter, create, read } from './NativeDecorators.js';
const value: Counter = create();
const result: [string, string, number, number] = read(value);
value.Count = 42;
// @ts-expect-error readonly getters remain readonly
value.ReadOnly = 'invalid';
// @ts-expect-error decorated methods preserve their F# parameter types
value.Message(42);
            """)
            if language = "typescript" then
                File.AppendAllText(Path.Combine(destination, "Consumer.ts"), "\n// @ts-expect-error native fields preserve their F# types\nvalue.Field = 'invalid';\n")
            for mode in [ "standard"; "legacy" ] do
                let compiled = Path.Combine(destination, "compiled", mode)
                let config = Path.Combine(destination, "tsconfig.json")
                let settings =
                    {| compilerOptions =
                        {| target = "es2020"
                           lib = [| "es2020"; "dom"; "esnext.temporal" |]
                           ``module`` = "es2020"
                           moduleResolution = "bundler"
                           strict = true
                           allowJs = true
                           skipLibCheck = true
                           rewriteRelativeImportExtensions = true
                           experimentalDecorators = (mode = "legacy")
                           noEmitOnError = true
                           rootDir = root
                           outDir = compiled |}
                       ``include`` = [| "NativeDecorators" + extension; "Consumer.ts"
                                        "fable_modules/**/*.ts"; "fable_modules/**/*.js"
                                        Path.Combine(source, "decorators.js") |] |}
                File.WriteAllText(config, System.Text.Json.JsonSerializer.Serialize settings)
                run "node" [ Path.Combine(root, "node_modules/typescript/lib/tsc.js"); "-p"; config ]
                let modulePath = Path.Combine(compiled, Path.GetRelativePath(root, destination), "NativeDecorators.js")
                let helperPath = Path.Combine(compiled, Path.GetRelativePath(root, source), "decorators.js")
                run "node" [ Path.Combine(source, "assertions.mjs"); modulePath; helperPath; mode ])
