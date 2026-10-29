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
  ]
