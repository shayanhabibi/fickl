module Fable.Tests.ProjectCracker

open System.IO
open Expecto
open Fable.Compiler.ProjectCracker

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
