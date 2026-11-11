module Fable.Tests.ProjectCracker

open System
open System.IO
open Expecto
open Fable.Compiler.ProjectCracker


/// #416: projects whose multi-targeting libraries compile Modern.fs for net10.0 and Standard.fs for netstandard2.0, and
/// take their NETSTANDARD2_0 branch only for the latter, so the emitted files say which framework was cracked.
module private MultiTarget =

    let private write (dir: string) (path: string) (content: string) =
        let path = Path.Combine(dir, path)
        Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
        File.WriteAllText(path, content)

    let private project (frameworks: string) (properties: string) (compile: string list) (references: string list) =
        let property = if frameworks.Contains ";" then "TargetFrameworks" else "TargetFramework"
        let lines (items: string list) = items |> List.map (fun item -> "    " + item) |> String.concat "\n"

        $"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <%s{property}>%s{frameworks}</%s{property}>
    <RollForward>Major</RollForward>
    %s{properties}
  </PropertyGroup>
  <ItemGroup>
%s{lines compile}
  </ItemGroup>
  <ItemGroup>
%s{lines references}
  </ItemGroup>
</Project>
"""

    let private reference (name: string) = $"""<ProjectReference Include="../%s{name}/%s{name}.fsproj" />"""

    let private lib (dir: string) (name: string) (frameworks: string) =
        write
            dir
            $"%s{name}/%s{name}.fsproj"
            (project
                frameworks
                ""
                [
                    """<Compile Include="Modern.fs" Condition="'$(TargetFramework)' == 'net10.0'" />"""
                    """<Compile Include="Standard.fs" Condition="'$(TargetFramework)' == 'netstandard2.0'" />"""
                    """<Compile Include="Lib.fs" />"""
                ]
                [])

        write dir $"%s{name}/Modern.fs" $"module %s{name}.Flavour\n\nlet name () = \"net10.0\"\n"
        write dir $"%s{name}/Standard.fs" $"module %s{name}.Flavour\n\nlet name () = \"netstandard2.0\"\n"

        write
            dir
            $"%s{name}/Lib.fs"
            $"""module %s{name}.Shared

let flavour () =
#if NETSTANDARD2_0
    %s{name}.Flavour.name () + " NETSTANDARD2_0"
#else
    %s{name}.Flavour.name ()
#endif
"""

    let private mid (dir: string) (name: string) (frameworks: string) (libName: string) =
        write dir $"%s{name}/%s{name}.fsproj" (project frameworks "" [ """<Compile Include="Mid.fs" />""" ] [ reference libName ])
        write dir $"%s{name}/Mid.fs" $"module %s{name}\n\nlet flavour () = %s{libName}.Shared.flavour ()\n"

    let private app (dir: string) (name: string) (properties: string) (references: string list) (calls: string list) =
        write dir $"%s{name}/%s{name}.fsproj" (project "net10.0" properties [ """<Compile Include="App.fs" />""" ] (List.map reference references))

        let calls = calls |> List.map (fun call -> $"printfn \"%%s\" (%s{call})") |> String.concat "\n"
        write dir $"%s{name}/App.fs" $"module %s{name}\n\n%s{calls}\n"

    let private create (dir: string) =
        write dir "Directory.Build.props" "<Project />"
        write dir "Directory.Build.targets" "<Project />"

        lib dir "LibLast" "netstandard2.0;net10.0"
        lib dir "LibFirst" "net10.0;netstandard2.0"
        lib dir "LibDeep" "netstandard2.0;net10.0"
        lib dir "LibShared" "netstandard2.0;net10.0"
        lib dir "LibIsolated" "net10.0;netstandard2.0"
        mid dir "MidMulti" "netstandard2.0;net10.0" "LibDeep"
        mid dir "MidShared" "netstandard2.1" "LibShared"
        mid dir "MidIsolated" "netstandard2.1" "LibIsolated"

        app
            dir
            "App"
            ""
            [ "LibLast"; "LibFirst"; "MidMulti"; "MidShared" ]
            [ "LibLast.Shared.flavour ()"; "LibFirst.Shared.flavour ()"; "MidMulti.flavour ()"; "MidShared.flavour ()" ]

        // Without transitive references the main project does not reference LibIsolated itself
        app
            dir
            "AppIsolated"
            "<DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences>"
            [ "MidIsolated" ]
            [ "MidIsolated.flavour ()" ]

    let private compile (dir: string) (appName: string) =
        let exitCode =
            Fable.Cli.Entry.main
                [|
                    Path.Combine(dir, appName, appName + ".fsproj")
                    "--cwd"
                    dir
                    "--lang"
                    "javascript"
                    "--outDir"
                    Path.Combine(dir, "out", appName)
                |]

        Expect.equal exitCode 0 $"Compilation of %s{appName} should succeed"

    /// The framework a library was compiled for, from the files emitted for it: its file, and whether NETSTANDARD2_0 was defined
    let private frameworkOf (dir: string) (appName: string) (libName: string) =
        let emitted (file: string) =
            Directory.GetFiles(Path.Combine(dir, "out", appName), file, SearchOption.AllDirectories)
            |> Array.filter (fun path -> Path.GetFileName(Path.GetDirectoryName path) = libName)

        let defined =
            emitted "Lib.js"
            |> Array.exists (fun path -> File.ReadAllText(path).Contains "NETSTANDARD2_0")

        match emitted "Modern.js", emitted "Standard.js", defined with
        | [| _ |], [||], false -> "net10.0"
        | [||], [| _ |], true -> "netstandard2.0"
        | modern, standard, defined -> $"Modern.js: %d{modern.Length}, Standard.js: %d{standard.Length}, NETSTANDARD2_0: %b{defined}"

    let private dir =
        lazy
            let dir = Path.Combine(Path.GetTempPath(), "fable-cracker-tests-" + Guid.NewGuid().ToString("N"))
            create dir

            AppDomain.CurrentDomain.ProcessExit.Add(fun _ ->
                try
                    Directory.Delete(dir, true)
                with _ ->
                    ()
            )

            dir

    /// The frameworks of the libraries of App as first compiled: the cache test compiles it again
    let private firstCompilation =
        lazy
            let dir = dir.Force()
            compile dir "App"

            [ "LibLast"; "LibFirst"; "LibDeep"; "LibShared" ]
            |> List.map (fun libName -> libName, frameworkOf dir "App" libName)
            |> Map.ofList

    let private nearestCase (name: string) (libName: string) =
        testCase name <| fun () ->
            Expect.equal (firstCompilation.Force()[libName]) "net10.0" $"%s{libName} should be compiled for net10.0"

    let tests =
        testList "ProjectCracker multi-target references" [
            nearestCase "Nearest framework listed last" "LibLast"
            nearestCase "Nearest framework listed first" "LibFirst"
            nearestCase "The chosen framework carries to its own references" "LibDeep"
            // The main project references LibShared too, transitively, and its choice is the assembly `dotnet run` loads
            nearestCase "The main project decides for a transitive reference" "LibShared"

            testCase "Nearest to the project referencing it when the main project does not reference it" <| fun () ->
                let dir = dir.Force()
                compile dir "AppIsolated"
                Expect.equal (frameworkOf dir "AppIsolated" "LibIsolated") "netstandard2.0" "LibIsolated is referenced from netstandard2.1"

            testCase "SetTargetFramework on the reference decides, and the changed choice is not served from the cache" <| fun () ->
                let dir = dir.Force()
                firstCompilation.Force() |> ignore
                let app = Path.Combine(dir, "App", "App.fsproj")

                File.WriteAllText(
                    app,
                    File.ReadAllText(app).Replace("LibFirst.fsproj\" ", "LibFirst.fsproj\" SetTargetFramework=\"TargetFramework=netstandard2.0\" ")
                )

                File.SetLastWriteTime(app, DateTime.Now.AddSeconds 2.0)

                for stale in Directory.GetFiles(Path.Combine(dir, "out", "App"), "Modern.js", SearchOption.AllDirectories) do
                    File.Delete stale

                compile dir "App"
                Expect.equal (frameworkOf dir "App" "LibFirst") "netstandard2.0" "LibFirst should be compiled for the framework the reference names"
                // NETSTANDARD2_0 is now defined for LibLast too: the defines of every project apply to the whole compilation
                Expect.stringStarts (frameworkOf dir "App" "LibLast") "Modern.js: 1, Standard.js: 0" "LibLast should still be compiled from its net10.0 files"
        ]

/// Run with the sequential tests: each compilation runs MSBuild design-time builds, which starve the timing-sensitive
/// tests when they run alongside them.
let multiTargetTests = MultiTarget.tests

let tests =
  testList "ProjectCracker" [

    testCase "Package assemblies are found under lib or ref" <| fun () ->
        let root = Path.Combine("nuget", "packages", "fable.lib", "1.2.3")
        Expect.isTrue (isPackageAssemblyPath (Path.Combine(root, "lib", "netstandard2.0", "Fable.Lib.dll"))) "lib"
        Expect.isTrue (isPackageAssemblyPath (Path.Combine(root, "ref", "net10.0", "Fable.Lib.dll"))) "ref"

    // #137: packing leaves obj/Release/Lib.1.2.3.nuspec and obj/Release/fable/Lib.fsproj two levels above the reference assembly
    testCase "Project output is not a package assembly" <| fun () ->
        let obj = Path.Combine("Lib", "obj", "Release")
        Expect.isFalse (isPackageAssemblyPath (Path.Combine(obj, "net10.0", "ref", "Lib.dll"))) "reference assembly"
        Expect.isFalse (isPackageAssemblyPath (Path.Combine(obj, "net10.0", "Lib.dll"))) "intermediate assembly"
        Expect.isFalse (isPackageAssemblyPath (Path.Combine("Lib", "bin", "Release", "net10.0", "Lib.dll"))) "output assembly"

    // #255: the checker drops the project's references, without a diagnostic, on a language version it rejects
    testCase "Only language versions the checker accepts are supported" <| fun () ->
        for version in [ "8.0"; "10.0"; "11"; "latest"; "default"; "preview"; "Preview" ] do
            Expect.isTrue (isLangVersionSupported version) version

        for version in [ "5.0"; "7.0"; "99.0"; "bogus" ] do
            Expect.isFalse (isLangVersionSupported version) version
  ]
