/// fickl: packs Fickl.Sdk and builds projects that use it, as a package reference and as a project SDK
module Build.Test.Sdk

open System
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions
open Build.Utils

let private version = "0.0.0-integration"

let private fixturesDir = Path.Resolve("tests", "Integration", "Sdk")
let private workDir = Path.Resolve("temp", "tests", "Integration", "Sdk")
let private packagesDir = workDir </> "packages"
let private projectsDir = workDir </> "projects"

/// Runs a command to the end and returns its exit code with stdout and stderr together
let private run (workingDirectory: string) (fileName: string) (arguments: string list) =
    let info =
        ProcessStartInfo(
            fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        )

    for argument in arguments do
        info.ArgumentList.Add argument

    // English messages to match, and no build server keeping the package's files open
    info.Environment["DOTNET_CLI_UI_LANGUAGE"] <- "en"
    info.Environment["MSBUILDDISABLENODEREUSE"] <- "1"
    info.Environment["DOTNET_NOLOGO"] <- "1"

    let output = Text.StringBuilder()
    use child = new Process(StartInfo = info)

    let append (e: DataReceivedEventArgs) =
        if not (isNull e.Data) then
            lock output (fun () -> output.AppendLine e.Data |> ignore)

    child.OutputDataReceived.Add append
    child.ErrorDataReceived.Add append
    child.Start() |> ignore
    child.BeginOutputReadLine()
    child.BeginErrorReadLine()
    child.WaitForExit()
    child.ExitCode, output.ToString()

let private fail (step: string) (output: string) =
    failwith $"Fickl.Sdk integration test: %s{step}{Environment.NewLine}%s{output}"

let private dotnet (projectDir: string) (arguments: string list) =
    run projectDir "dotnet" (arguments @ [ "-nr:false"; "-tl:off"; "-v:n" ])

let private build (project: string) =
    let projectDir = projectsDir </> project
    let exitCode, output = dotnet projectDir [ "build" ]

    if exitCode <> 0 then
        fail $"dotnet build of %s{project} failed" output

    output

let private expectFile (path: string) =
    if not (File.Exists path) then
        failwith $"Fickl.Sdk integration test: expected %s{path}"

let private expectNode (projectDir: string) (script: string) (expected: string) =
    let exitCode, output = run projectDir "node" [ script ]

    if exitCode <> 0 || output.Trim() <> expected then
        fail $"node %s{script} should print \"%s{expected}\"" output

/// The restore must take this run's package, not one an earlier run left in the global packages folder
let private forgetCachedPackage () =
    let _, output =
        run workDir "dotnet" [ "nuget"; "locals"; "global-packages"; "--list" ]

    let globalPackages =
        output.Split('\n')
        |> Array.tryPick (fun line ->
            let line = line.Trim()
            let prefix = "global-packages:"

            if line.StartsWith(prefix, StringComparison.Ordinal) then
                Some(line.Substring(prefix.Length).Trim())
            else
                None
        )

    match globalPackages with
    | Some dir ->
        let cached = dir </> "fickl.sdk" </> version

        if Directory.Exists cached then
            Directory.Delete(cached, true)
    | None -> fail "cannot find the global packages folder" output

let private writeNuGetConfig () =
    File.WriteAllText(
        projectsDir </> "NuGet.config",
        $"""<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="fickl-sdk" value="%s{packagesDir}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"""
    )

let private copyFixtures () =
    for file in Directory.GetFiles(fixturesDir, "*", SearchOption.AllDirectories) do
        let target = projectsDir </> Path.GetRelativePath(fixturesDir, file)
        Directory.CreateDirectory(Path.GetDirectoryName target) |> ignore
        File.Copy(file, target)

/// Mode (a): compiles after Build, skips a build with nothing changed, and cleans only what it wrote
let private testPackageReference () =
    let projectDir = projectsDir </> "PackageRef"
    let outDir = projectDir </> "fickl-out"

    build "PackageRef" |> ignore
    expectFile (outDir </> "Program.js")
    expectFile (outDir </> "Lib" </> "Lib.js")
    expectNode projectDir (outDir </> "Program.js") "Hello, package reference"

    let output = build "PackageRef"

    if not (output.Contains "Skipping target \"FicklCompile\" because all output files are up-to-date") then
        fail "a build with nothing changed should skip FicklCompile" output

    let handWritten = outDir </> "handwritten.js"
    File.WriteAllText(handWritten, "// not generated")
    let exitCode, output = dotnet projectDir [ "clean" ]

    if exitCode <> 0 then
        fail "dotnet clean failed" output

    if
        File.Exists(outDir </> "Program.js")
        || Directory.Exists(outDir </> "fable_modules")
    then
        fail "dotnet clean should delete the generated files" output

    if not (File.Exists handWritten) then
        fail "dotnet clean should keep a file the compiler did not write" output

/// Mode (b): the project SDK opts in and defaults TargetFramework
let private testProjectSdk () =
    let projectDir = projectsDir </> "SdkApp"
    build "SdkApp" |> ignore
    expectNode projectDir (projectDir </> "dist" </> "Program.js") "Hello, project SDK"

/// An error only Fickl finds fails the build, once, at its absolute path; a warning of the .NET compile appears once
let private testDiagnostics () =
    let projectDir = projectsDir </> "Errors"
    let exitCode, output = dotnet projectDir [ "build" ]

    if exitCode = 0 then
        fail "the build of Errors should fail" output

    let source = projectDir </> "Library.fs"

    if not (output.Contains $"%s{source}(10,19,10,31): error FS0001: ") then
        fail $"expected FS0001 at %s{source}(10,19,10,31)" output

    let counts = Regex.Match(output, @"(\d+) Warning\(s\)\s+(\d+) Error\(s\)")

    if
        not counts.Success
        || counts.Groups[1].Value <> "1"
        || counts.Groups[2].Value <> "1"
    then
        fail "expected 1 warning (FS0025 from fsc only) and 1 error (FS0001, without MSB3073)" output

let handle () =
    Directory.clean workDir
    Directory.CreateDirectory packagesDir |> ignore
    copyFixtures ()
    writeNuGetConfig ()

    let exitCode, output =
        run
            workDir
            "dotnet"
            [
                "pack"
                Path.Resolve("src", "Fickl.Sdk")
                "-c"
                "Release"
                "-o"
                packagesDir
                $"-p:FicklSdkPackageVersion=%s{version}"
            ]

    if exitCode <> 0 then
        fail "dotnet pack of Fickl.Sdk failed" output

    forgetCachedPackage ()
    testPackageReference ()
    testProjectSdk ()
    testDiagnostics ()
    printfn "Fickl.Sdk integration tests passed"
