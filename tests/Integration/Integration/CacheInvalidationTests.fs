module Fable.Tests.CacheInvalidation

open System
open System.IO
open Expecto

/// A project whose compile order lives in an imported .props, so adding a source file
/// leaves the .fsproj timestamp untouched. In a temp dir: these tests mutate the project.
let private createProject (dir: string) =
    Directory.CreateDirectory(dir) |> ignore

    File.WriteAllText(
        Path.Combine(dir, "Test.fsproj"),
        """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RollForward>Major</RollForward>
  </PropertyGroup>
  <Import Project="Tests.props" />
</Project>
"""
    )

    File.WriteAllText(
        Path.Combine(dir, "Tests.props"),
        """<Project>
  <ItemGroup>
    <Compile Include="$(MSBuildThisFileDirectory)Lib.fs" />
    <Compile Include="$(MSBuildThisFileDirectory)Main.fs" />
  </ItemGroup>
</Project>
"""
    )

    File.WriteAllText(Path.Combine(dir, "Lib.fs"), "module Lib\n\nlet greeting = \"hello\"\n")

    File.WriteAllText(Path.Combine(dir, "Main.fs"), "module Main\n\nlet run () = printfn \"%s\" Lib.greeting\n")

/// Cache invalidation compares timestamps, so a file rewritten within the same tick as the
/// previous compilation could look unchanged. Nudge it forward to keep the test from flaking.
let private writeNewer (path: string) (content: string) =
    File.WriteAllText(path, content)
    File.SetLastWriteTime(path, DateTime.Now.AddSeconds(2.0))

let private compile (dir: string) (outDir: string) =
    Fable.Cli.Entry.main
        [|
            Path.Combine(dir, "Test.fsproj")
            "--cwd"
            dir
            "--lang"
            "javascript"
            "--outDir"
            outDir
        |]

let private withTempProject name (f: string -> string -> unit) =
    let unique = Guid.NewGuid().ToString("N")
    let dir = Path.Combine(Path.GetTempPath(), $"fable-cache-tests-%s{name}-%s{unique}")

    try
        createProject dir
        f dir (Path.Combine(dir, "out"))
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()

/// A project where Main.fs imports cfg/vite.config.fs, the file that gets its own extension (#301)
let private createFileExtensionProject (dir: string) =
    Directory.CreateDirectory(Path.Combine(dir, "cfg")) |> ignore

    File.WriteAllText(
        Path.Combine(dir, "Tests.props"),
        """<Project>
  <ItemGroup>
    <Compile Include="$(MSBuildThisFileDirectory)cfg/vite.config.fs" />
    <Compile Include="$(MSBuildThisFileDirectory)Main.fs" />
  </ItemGroup>
</Project>
"""
    )

    File.WriteAllText(
        Path.Combine(dir, "cfg", "vite.config.fs"),
        "module ViteConfig\n\nlet port = 5173\nlet name () = sprintf \"vite on %d\" port\n"
    )

    File.WriteAllText(Path.Combine(dir, "Main.fs"), "module Main\n\nprintfn \"%s\" (ViteConfig.name ())\n")
    File.WriteAllText(Path.Combine(dir, "package.json"), """{"type":"module"}""")

let private compileWith (dir: string) (args: string list) =
    Fable.Cli.Entry.main
        [|
            Path.Combine(dir, "Test.fsproj")
            "--cwd"
            dir
            "--lang"
            "javascript"
            yield! args
        |]

let private runNode (file: string) =
    let info = Diagnostics.ProcessStartInfo("node", [ file ])
    info.RedirectStandardOutput <- true
    info.UseShellExecute <- false
    use child = Diagnostics.Process.Start info
    let output = child.StandardOutput.ReadToEnd()
    child.WaitForExit()
    child.ExitCode, output.Trim()

/// The file names under `dir`, relative to it, without Fable's own folders
let private outputsIn (dir: string) (pattern: string) =
    if Directory.Exists dir then
        Directory.GetFiles(dir, pattern, SearchOption.AllDirectories)
        |> Array.map (fun path -> Path.GetRelativePath(dir, path).Replace('\\', '/'))
        |> Array.filter (fun path ->
            not (
                path.StartsWith("fable_modules/", StringComparison.Ordinal)
                || path.StartsWith("obj/", StringComparison.Ordinal)
                || path.StartsWith("bin/", StringComparison.Ordinal)
            )
        )
        |> Array.sort
    else
        [||]

/// One file with its own extension, imported by another: names, import, up-to-date check and deleted output.
/// `root` is where the outputs go, and `mainName` what Main.fs compiles to there
let private checkFileExtension (dir: string) (root: string) (mainName: string) (ext: string) (args: string list) =
    createFileExtensionProject dir
    let mainOut = Path.Combine(root, mainName)
    let configOut = Path.Combine(root, "cfg", "vite.config" + ext)

    Expect.equal (compileWith dir args) 0 "Compilation without the option should succeed"

    Expect.isFalse
        (File.Exists configOut)
        "Without the option the file should have the extension of the compilation"

    let jsArgs = [ "--fileExtension"; "cfg/vite.config.fs=" + ext ] @ args
    Expect.equal (compileWith dir jsArgs) 0 "First compilation should succeed"
    Expect.isTrue (File.Exists configOut) "The mapped file should have its own extension"

    Expect.stringContains
        (File.ReadAllText mainOut)
        $"from \"./cfg/vite.config%s{ext}\""
        "The import should name the file as it was written"

    Expect.equal (runNode mainOut) (0, "vite on 5173") "The output should run on node"

    let written = File.GetLastWriteTimeUtc configOut
    Expect.equal (compileWith dir jsArgs) 0 "Second compilation should succeed"
    Expect.equal (File.GetLastWriteTimeUtc configOut) written "The second compilation should have been skipped"

    File.Delete configOut
    Expect.equal (compileWith dir jsArgs) 0 "Third compilation should succeed"
    Expect.isTrue (File.Exists configOut) "The deleted output should have been generated again"

    // The import in Main.fs must follow, though neither source changed
    let mjsArgs = [ "--fileExtension"; "cfg/vite.config.fs=.config.mjs" ] @ args
    Expect.equal (compileWith dir mjsArgs) 0 "Compilation with another extension should succeed"

    Expect.stringContains
        (File.ReadAllText mainOut)
        "from \"./cfg/vite.config.config.mjs\""
        "A changed extension should recompile the files importing it"

    Expect.equal (runNode mainOut) (0, "vite on 5173") "The output should still run on node"

let tests =
    testList
        "CacheInvalidation"
        [
            // A `<Compile Include>` arriving through an imported .props touches no .fsproj
            // timestamp, so the options cache used to be reused with a stale source list.
            testCase "Source file added via an imported .props is compiled"
            <| fun () ->
                withTempProject
                    "added"
                    (fun dir outDir ->
                        Expect.equal (compile dir outDir) 0 "First compilation should succeed"

                        writeNewer (Path.Combine(dir, "Scalars.fs")) "module Scalars\n\nlet tests = \"scalars\"\n"

                        writeNewer
                            (Path.Combine(dir, "Tests.props"))
                            """<Project>
  <ItemGroup>
    <Compile Include="$(MSBuildThisFileDirectory)Lib.fs" />
    <Compile Include="$(MSBuildThisFileDirectory)Scalars.fs" />
    <Compile Include="$(MSBuildThisFileDirectory)Main.fs" />
  </ItemGroup>
</Project>
"""

                        Expect.equal (compile dir outDir) 0 "Second compilation should succeed"

                        Expect.isTrue
                            (File.Exists(Path.Combine(outDir, "Scalars.js")))
                            "The file added through the imported .props should have been generated"
                    )

            // A failed compilation still writes its outputs, so the next run used to find them up to date
            testCase "A run after a failed compilation fails again instead of being skipped"
            <| fun () ->
                withTempProject
                    "failed"
                    (fun dir outDir ->
                        let libPath = Path.Combine(dir, "Lib.fs")
                        File.WriteAllText(libPath, "module Lib\n\nlet greeting: string = Undefined.thing\n")

                        Expect.notEqual (compile dir outDir) 0 "First compilation should fail"
                        Expect.notEqual (compile dir outDir) 0 "Second compilation should fail too"

                        writeNewer libPath "module Lib\n\nlet greeting = \"hello\"\n"
                        Expect.equal (compile dir outDir) 0 "Compilation should succeed once the error is fixed"
                        Expect.equal (compile dir outDir) 0 "A run after a successful compilation should succeed"
                    )

            // A missing output used to count as up to date, because a source with only types writes none
            testCase "A deleted output file is generated again"
            <| fun () ->
                withTempProject
                    "deleted"
                    (fun dir outDir ->
                        // Compiles to nothing, so it never has an output file
                        File.WriteAllText(
                            Path.Combine(dir, "Lib.fs"),
                            "module Lib\n\ntype IGreeter =\n    abstract Greet: unit -> string\n"
                        )

                        File.WriteAllText(Path.Combine(dir, "Main.fs"), "module Main\n\nlet run () = printfn \"hello\"\n")
                        File.WriteAllText(Path.Combine(dir, "Other.fs"), "module Other\n\nlet other = 1\n")

                        File.WriteAllText(
                            Path.Combine(dir, "Tests.props"),
                            """<Project>
  <ItemGroup>
    <Compile Include="$(MSBuildThisFileDirectory)Lib.fs" />
    <Compile Include="$(MSBuildThisFileDirectory)Main.fs" />
    <Compile Include="$(MSBuildThisFileDirectory)Other.fs" />
  </ItemGroup>
</Project>
"""
                        )

                        let libOut = Path.Combine(outDir, "Lib.js")
                        let mainOut = Path.Combine(outDir, "Main.js")

                        Expect.equal (compile dir outDir) 0 "First compilation should succeed"
                        Expect.isFalse (File.Exists libOut) "A source with only types should have no output"

                        File.Delete mainOut
                        Expect.equal (compile dir outDir) 0 "Second compilation should succeed"
                        Expect.isTrue (File.Exists mainOut) "The deleted output should have been generated again"

                        writeNewer (Path.Combine(dir, "Lib.fs")) "module Lib\n\nlet greeting = \"hello\"\n"
                        Expect.equal (compile dir outDir) 0 "Third compilation should succeed"
                        Expect.isTrue (File.Exists libOut) "A changed source without output should have been compiled"
                    )

            testCase "Per-file extension: a file gets its own extension next to its source"
            <| fun () -> withTempProject "ext" (fun dir _ -> checkFileExtension dir dir "Main.fs.js" ".js" [])

            testCase "Per-file extension: a file gets its own extension in an outDir"
            <| fun () ->
                withTempProject
                    "ext-outdir"
                    (fun dir outDir -> checkFileExtension dir outDir "Main.js" ".mjs" [ "--outDir"; outDir ])

            testCase "Per-file extension: the source map follows the file name"
            <| fun () ->
                withTempProject
                    "ext-maps"
                    (fun dir outDir ->
                        createFileExtensionProject dir

                        let args =
                            [
                                "--outDir"
                                outDir
                                "--sourceMaps"
                                "--fileExtension"
                                "cfg/vite.config.fs=.mjs"
                            ]

                        Expect.equal (compileWith dir args) 0 "Compilation should succeed"

                        Expect.equal
                            (outputsIn outDir "*.map")
                            [| "Main.js.map"; "cfg/vite.config.mjs.map" |]
                            "The source map should be named after the output"

                        Expect.stringContains
                            (File.ReadAllText(Path.Combine(outDir, "cfg", "vite.config.mjs")))
                            "//# sourceMappingURL=vite.config.mjs.map"
                            "The output should point at its source map"
                    )

            testCase "Per-file extension: two sources compiling to the same file is an error"
            <| fun () ->
                withTempProject
                    "ext-collision"
                    (fun dir outDir ->
                        createFileExtensionProject dir
                        File.WriteAllText(Path.Combine(dir, "cfg", "vite.fs"), "module Vite\n\nlet x = 1\n")

                        File.WriteAllText(
                            Path.Combine(dir, "Tests.props"),
                            """<Project>
  <ItemGroup>
    <Compile Include="$(MSBuildThisFileDirectory)cfg/vite.fs" />
    <Compile Include="$(MSBuildThisFileDirectory)cfg/vite.config.fs" />
    <Compile Include="$(MSBuildThisFileDirectory)Main.fs" />
  </ItemGroup>
</Project>
"""
                        )

                        // cfg/vite.fs -> cfg/vite.config.js, which is where cfg/vite.config.fs goes with an outDir
                        let args = [ "--outDir"; outDir; "--fileExtension"; "cfg/vite.fs=.config.js" ]
                        Expect.notEqual (compileWith dir args) 0 "Compilation should fail"
                        Expect.isEmpty (outputsIn outDir "*.js") "Nothing should have been written"
                    )

            testCase "Per-file extension: a file that is not a source of the project is an error"
            <| fun () ->
                withTempProject
                    "ext-unknown"
                    (fun dir outDir ->
                        createFileExtensionProject dir
                        let args = [ "--outDir"; outDir; "--fileExtension"; "cfg/vite.confg.fs=.js" ]
                        Expect.notEqual (compileWith dir args) 0 "Compilation should fail"
                    )
        ]
