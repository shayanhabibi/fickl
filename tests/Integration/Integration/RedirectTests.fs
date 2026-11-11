module Fable.Tests.Redirect

open System
open System.IO
open Expecto

let private fsproj (outputType: string) (items: string) =
    $"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>%s{outputType}</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RollForward>Major</RollForward>
  </PropertyGroup>
  <ItemGroup>
%s{items}
  </ItemGroup>
</Project>
"""

/// An app and the library it references. The library has a nested source, an inline function and a type the app
/// tests values against, so the app needs the library's layout, its inline bodies and its classes.
let private createProjects (dir: string) =
    let lib = Path.Combine(dir, "Lib")
    let app = Path.Combine(dir, "App")
    Directory.CreateDirectory(Path.Combine(lib, "Sub")) |> ignore
    Directory.CreateDirectory(app) |> ignore

    File.WriteAllText(
        Path.Combine(lib, "Lib.fsproj"),
        fsproj "Library" "    <Compile Include=\"Module.fs\" />\n    <Compile Include=\"Sub/Deep.fs\" />"
    )

    File.WriteAllText(
        Path.Combine(lib, "Module.fs"),
        """module Lib.Module

type Shape =
    | Circle of float
    | Square of float

let area shape =
    match shape with
    | Circle r -> 3.0 * r * r
    | Square s -> s * s

let inline twice ([<InlineIfLambda>] f: 'a -> 'a) (x: 'a) = f (f x)
let inline squareArea (side: float) = area (Square side)
let wrapNone () : int option option = Some None
let isShape (o: obj) =
    match o with
    | :? Shape -> true
    | _ -> false
"""
    )

    File.WriteAllText(Path.Combine(lib, "Sub", "Deep.fs"), "module Lib.Sub.Deep\n\nlet greet name = \"hello \" + name\n")

    File.WriteAllText(
        Path.Combine(app, "App.fsproj"),
        fsproj
            "Exe"
            "    <Compile Include=\"Program.fs\" />\n    <ProjectReference Include=\"../Lib/Lib.fsproj\" />"
    )

    File.WriteAllText(
        Path.Combine(app, "Program.fs"),
        """module App.Program

open Lib.Module

printfn "%s" (Lib.Sub.Deep.greet "world")
printfn "%d" (twice (fun x -> x + 1) 5)
printfn "%.1f" (squareArea 2.0)
printfn "%b" (isShape (box (Circle 1.0)))

match wrapNone () with
| Some None -> printfn "Some None"
| _ -> printfn "not Some None"
"""
    )

let private compile (dir: string) (project: string) (outDir: string) (args: string list) =
    Fable.Cli.Entry.main
        [|
            Path.Combine(dir, project)
            "--cwd"
            dir
            "--outDir"
            outDir
            yield! args
        |]

let private withProjects name (f: string -> unit) =
    let dir =
        Path.Combine(Path.GetTempPath(), $"fable-redirect-tests-%s{name}-{Guid.NewGuid():N}")

    try
        createProjects dir
        f dir
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()

let private node (workingDir: string) (script: string) =
    let info =
        Diagnostics.ProcessStartInfo(
            "node",
            [ script ],
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        )

    use child = Diagnostics.Process.Start info
    let stdout = child.StandardOutput.ReadToEndAsync()
    let stderr = child.StandardError.ReadToEndAsync()
    child.WaitForExit()
    child.ExitCode, stdout.Result.Replace("\r\n", "\n"), stderr.Result

let private fableLibrary = "@fable-org/fable-library-js"

let tests =
    testList
        "Redirect"
        [
            testCase "A redirected project is imported from its package and not emitted"
            <| fun () ->
                withProjects
                    "imports"
                    (fun dir ->
                        let outDir = Path.Combine(dir, "App", "out")
                        let program = Path.Combine(outDir, "Program.js")

                        let exitCode =
                            compile dir "App" outDir [ "--redirect"; "Lib=@scope/lib/dist"; "--fableLib"; fableLibrary ]

                        Expect.equal exitCode 0 "Compilation should succeed"
                        let js = File.ReadAllText program
                        Expect.stringContains js "from \"@scope/lib/dist/Module.js\"" "Module is imported from the package"
                        Expect.stringContains js "from \"@scope/lib/dist/Sub/Deep.js\"" "A nested file keeps its folder"
                        Expect.isFalse (js.Contains "./Lib/") "Nothing is imported from the library's output"
                        Expect.isFalse (Directory.Exists(Path.Combine(outDir, "Lib"))) "The library is not emitted"

                        // The option is part of the cached project: dropping it compiles the library again
                        let exitCode = compile dir "App" outDir [ "--fableLib"; fableLibrary ]
                        Expect.equal exitCode 0 "Compilation without the option should succeed"
                        let js = File.ReadAllText program
                        Expect.stringContains js "from \"./Lib/Module.js\"" "Module is imported from the output again"
                        Expect.isTrue (File.Exists(Path.Combine(outDir, "Lib", "Module.js"))) "The library is emitted"
                    )

            testCase "A redirected project is imported from its package in TypeScript"
            <| fun () ->
                withProjects
                    "typescript"
                    (fun dir ->
                        let outDir = Path.Combine(dir, "App", "out")

                        let exitCode =
                            compile
                                dir
                                "App"
                                outDir
                                [ "--lang"; "typescript"; "--redirect"; "Lib=@scope/lib"; "--fableLib"; fableLibrary ]

                        Expect.equal exitCode 0 "Compilation should succeed"
                        let ts = File.ReadAllText(Path.Combine(outDir, "Program.ts"))
                        Expect.stringContains ts "from \"@scope/lib/Module." "Module is imported from the package"
                        Expect.stringContains ts "from \"@scope/lib/Sub/Deep." "A nested file keeps its folder"
                        Expect.isFalse (Directory.Exists(Path.Combine(outDir, "Lib"))) "The library is not emitted"
                    )

            // The package's extensions are told with the options that set this output's: --extension, or
            // --fileExtension for one source
            testCase "A redirected source is imported with its per-file extension"
            <| fun () ->
                withProjects
                    "extension"
                    (fun dir ->
                        let outDir = Path.Combine(dir, "App", "out")

                        let exitCode =
                            compile
                                dir
                                "App"
                                outDir
                                [
                                    "--redirect"
                                    "Lib=@scope/lib"
                                    "--fileExtension"
                                    "Lib/Sub/Deep.fs=.mjs"
                                    "--fableLib"
                                    fableLibrary
                                ]

                        Expect.equal exitCode 0 "Compilation should succeed"
                        let js = File.ReadAllText(Path.Combine(outDir, "Program.js"))
                        Expect.stringContains js "from \"@scope/lib/Sub/Deep.mjs\"" "The source given an extension uses it"
                        Expect.stringContains js "from \"@scope/lib/Module.js\"" "The other one uses --extension's"
                        Expect.isFalse (Directory.Exists(Path.Combine(outDir, "Lib"))) "The library is not emitted"
                    )

            testCase "The output runs against the library's own output in node_modules"
            <| fun () ->
                withProjects
                    "runs"
                    (fun dir ->
                        let app = Path.Combine(dir, "App")
                        let modules = Path.Combine(app, "node_modules")
                        let package = Path.Combine(modules, "@scope", "lib")
                        let shared = [ "--fableLib"; fableLibrary ]

                        // The package: the library compiled on its own, importing fable-library from its npm package
                        Expect.equal (compile dir "Lib" package shared) 0 "The library should compile"

                        File.WriteAllText(
                            Path.Combine(package, "package.json"),
                            """{ "name": "@scope/lib", "version": "1.0.0", "type": "module" }"""
                        )

                        // fable-library as that npm package: the copy a compilation without --fableLib writes
                        let scratch = Path.Combine(dir, "scratch")
                        Expect.equal (compile dir "Lib" scratch []) 0 "The library should compile with its own fable-library"

                        let libraryCopy =
                            Directory.GetDirectories(Path.Combine(scratch, "fable_modules"), "fable-library-js*")
                            |> Array.exactlyOne

                        Directory.CreateDirectory(Path.Combine(modules, "@fable-org")) |> ignore
                        Directory.Move(libraryCopy, Path.Combine(modules, "@fable-org", "fable-library-js"))
                        File.WriteAllText(Path.Combine(app, "package.json"), """{ "type": "module" }""")

                        let outDir = Path.Combine(app, "out")

                        Expect.equal
                            (compile dir "App" outDir ("--redirect" :: "Lib=@scope/lib" :: shared))
                            0
                            "The app should compile"

                        Expect.isFalse (Directory.Exists(Path.Combine(outDir, "Lib"))) "The library is not emitted"

                        let exitCode, stdout, stderr = node app (Path.Combine(outDir, "Program.js"))
                        Expect.equal exitCode 0 $"node should run the output:\n{stderr}"
                        Expect.equal stdout "hello world\n7\n4.0\ntrue\nSome None\n" "The output uses the package"
                    )

            testCase "A redirect that cannot work is rejected"
            <| fun () ->
                withProjects
                    "rejected"
                    (fun dir ->
                        let outDir = Path.Combine(dir, "App", "out")
                        let fails args reason = Expect.notEqual (compile dir "App" outDir args) 0 reason

                        fails [ "--redirect"; "Missing=@scope/lib" ] "A name that is no reference"
                        fails [ "--redirect"; "Lib" ] "No package"
                        fails [ "--redirect"; "Lib=../lib" ] "A relative path"
                        fails [ "--redirect"; "Lib=a"; "--redirect"; "lib=b" ] "The same reference twice"
                        fails [ "--lang"; "python"; "--redirect"; "Lib=lib" ] "Another target"
                    )
        ]
