module Fable.Tests.CompilationTests

open System.IO
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

let tests =
    Directory.EnumerateDirectories(data)
    |> Seq.map (fun testCaseDir -> //
        testCaseAsync
            testCaseDir
            (async {
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

                return ()
            }))

    |> Seq.toList
    |> testList "Compilation"

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
