/// Publishes the Fickl NuGet family together, with the tool pointer last.
module Fickl.Build.Release

open System
open System.Diagnostics
open System.IO
open System.IO.Compression
open System.Xml.Linq
open Partas.Build
open Fickl.Build.Forwarded
open Build.FableLibrary

let private runDotnet (arguments: string list) =
    let info =
        ProcessStartInfo("dotnet", UseShellExecute = false, WorkingDirectory = Paths.root)

    for argument in arguments do
        info.ArgumentList.Add argument
    // Do not echo arguments: nuget push includes the API key.
    use child = Process.Start info
    child.WaitForExit()

    if child.ExitCode <> 0 then
        failwith $"dotnet {arguments.Head} failed with exit code {child.ExitCode}"

/// fable-library-js ships declaration files, so the .d.ts files written by `--declaration` can import its types.
/// The TypeScript build of the library is its source; tsc writes declarations only, next to the JavaScript.
let private emitLibraryDeclarations () =
    let tsBuildDir = Path.Combine(Paths.root, "temp", "fable-library-ts")
    let jsOutDir = Path.Combine(Paths.root, "temp", "fable-library-js")

    SimpleExec.Command.Run(
        "npx",
        $"tsc --project \"{tsBuildDir}\" --declaration --emitDeclarationOnly --outDir \"{jsOutDir}\"",
        workingDirectory = Paths.root
    )

/// Whether the GitHub release of a tag exists: `gh release view` exits 0 for one that does.
let private releaseExists (tag: string) =
    let info =
        ProcessStartInfo(
            "gh",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Paths.root
        )

    for argument in [ "release"; "view"; tag ] do
        info.ArgumentList.Add argument

    use child = Process.Start info
    child.StandardOutput.ReadToEnd() |> ignore
    child.StandardError.ReadToEnd() |> ignore
    child.WaitForExit()
    child.ExitCode = 0

let private version () =
    XDocument.Load(Path.Combine(Paths.root, "src", "Fickl.Branding.targets")).Descendants(XName.Get "FicklVersion")
    |> Seq.exactlyOne
    |> _.Value

let private projects =
    [
        "Fable.AST"
        "Fickl.Core"
        "Fable.Compiler"
        "Fable.PublishUtils"
        "Fable.Cli"
        "Fickl.Sdk"
    ]

let private packageIds =
    [
        "Fickl.AST"
        "Fickl.Core"
        "Fickl.Compiler"
        "Fickl.PublishUtils"
        "Fickl.any"
        "Fickl.win-x64"
        "Fickl.win-arm64"
        "Fickl.linux-x64"
        "Fickl.linux-arm64"
        "Fickl.linux-musl-x64"
        "Fickl.osx-x64"
        "Fickl.osx-arm64"
        "Fickl.Sdk"
        "Fickl"
    ]

/// <summary>Builds the browser compiler and writes the two archives a release carries.</summary>
/// <remarks>The archives go to <c>temp/release-&lt;version&gt;</c>, beside the packages <c>publish</c> writes.</remarks>
let browserCompiler =
    command "browser-compiler" {
        description
            "Build the browser compiler and write fickl-standalone-<version>.tgz and fickl-metadata-<version>.tgz to temp/release-<version>"

        input {
            return
                stage "Browser compiler" {
                    run (fun _ ->
                        let releaseVersion = version ()
                        let output = Path.Combine(Paths.root, "temp", "release-" + releaseVersion)

                        for path in BrowserCompiler.pack releaseVersion output do
                            printfn $"{path}"

                        0
                    )
                }
        }
    }

let publish =
    let packOnly =
        Input.option<bool> "--pack-only"
        |> Input.description "Build and validate packages without publishing"

    let packed =
        Input.option<bool> "--packed"
        |> Input.description
            "Validate and publish the packages a previous --pack-only run left in temp/release-<version>"

    let githubRelease =
        Input.option<bool> "--github-release"
        |> Input.description "After publishing, create the GitHub release v<version> with generated notes (needs gh)"

    command "publish" {
        description "Pack and publish all Fickl NuGet packages; reads FICKL_NUGET_KEY from the environment"

        input {
            let! packOnly = packOnly
            and! packed = packed
            and! githubRelease = githubRelease

            return
                stage "Fickl release" {
                    run (fun _ ->
                        let key = Environment.GetEnvironmentVariable "FICKL_NUGET_KEY"

                        if not packOnly && String.IsNullOrWhiteSpace key then
                            failwith "Set FICKL_NUGET_KEY before publishing (or use --pack-only)"

                        let releaseVersion = version ()
                        let output = Path.Combine(Paths.root, "temp", "release-" + releaseVersion)

                        // A tag push publishes the version the tag names, so the two must agree
                        if Environment.GetEnvironmentVariable "GITHUB_REF_TYPE" = "tag" then
                            let tag = Environment.GetEnvironmentVariable "GITHUB_REF_NAME"

                            if tag <> "v" + releaseVersion then
                                failwith $"Tag {tag} does not match FicklVersion {releaseVersion}"

                        if not packed then
                            // CLI payloads are generated files under temp; never rely on leftover build outputs.
                            BuildFableLibraryJavaScript().Run(true)
                            BuildFableLibraryTypeScript().Run(true)
                            emitLibraryDeclarations ()
                            BuildFableLibraryRust().Run(true)
                            BuildFableLibraryDart().Run(true)
                            BuildFableLibraryBeam().Run(true)

                            for project in projects do
                                printfn $"Packing {project} {releaseVersion}"

                                runDotnet
                                    [
                                        "pack"
                                        Path.Combine(Paths.root, "src", project)
                                        "-c"
                                        "Release"
                                        "-o"
                                        output
                                    ]

                            BrowserCompiler.pack releaseVersion output |> ignore
                        // Validate the entire set before the first upload. Never publish upstream package IDs.
                        let packages =
                            packageIds
                            |> List.map (fun id ->
                                let path = Path.Combine(output, $"{id}.{releaseVersion}.nupkg")
                                use archive = ZipFile.OpenRead path

                                let spec =
                                    archive.Entries
                                    |> Seq.filter (fun e -> e.FullName.EndsWith ".nuspec")
                                    |> Seq.exactlyOne

                                use stream = spec.Open()
                                let document = XDocument.Load stream

                                let value name =
                                    document.Descendants() |> Seq.find (fun e -> e.Name.LocalName = name) |> _.Value

                                if value "id" <> id || value "version" <> releaseVersion then
                                    failwith $"Unexpected package identity: {path}"

                                // A PackagePath ending in `\` gains a second separator when packed on Linux
                                match
                                    archive.Entries
                                    |> Seq.tryFind (fun e -> e.FullName.Contains "//" || e.FullName.StartsWith "/")
                                with
                                | Some entry -> failwith $"Malformed entry {entry.FullName} in {path}"
                                | None -> ()

                                for dependency in
                                    document.Descendants() |> Seq.filter (fun e -> e.Name.LocalName = "dependency") do
                                    let dependencyId = dependency.Attribute(XName.Get "id").Value
                                    let dependencyVersion = dependency.Attribute(XName.Get "version").Value

                                    if
                                        dependencyId.StartsWith("Fickl.", StringComparison.Ordinal)
                                        && dependencyVersion <> releaseVersion
                                    then
                                        failwith $"Mismatched dependency {dependencyId} {dependencyVersion} in {path}"

                                let hasEntry name =
                                    archive.Entries |> Seq.exists (fun e -> e.FullName = name)

                                // Fickl.Sdk runs the compiler it carries, from its MSBuild files
                                if id = "Fickl.Sdk" then
                                    for name in
                                        [
                                            "build/Fickl.Sdk.targets"
                                            "Sdk/Sdk.props"
                                            "Sdk/Sdk.targets"
                                            "tools/net10.0/any/fable.dll"
                                        ] do
                                        if not (hasEntry name) then
                                            failwith $"Missing {name} in {path}"

                                if
                                    id = "Fickl.any"
                                    || id = "Fickl.Sdk"
                                    || id.StartsWith("Fickl.win-", StringComparison.Ordinal)
                                    || id.StartsWith("Fickl.linux-", StringComparison.Ordinal)
                                    || id.StartsWith("Fickl.osx-", StringComparison.Ordinal)
                                then
                                    if
                                        not (
                                            archive.Entries
                                            |> Seq.exists (fun e -> e.FullName = "fable-library-js/List.d.ts")
                                        )
                                    then
                                        failwith $"Missing fable-library-js declarations in {path}"

                                    for language in [ "js"; "ts"; "rust"; "dart"; "beam"; "php" ] do
                                        let prefix = $"fable-library-{language}/"

                                        if
                                            not (
                                                archive.Entries
                                                |> Seq.exists (fun e ->
                                                    e.FullName.StartsWith(prefix, StringComparison.Ordinal)
                                                    && e.Length > 0L
                                                )
                                            )
                                        then
                                            failwith $"Missing {language} runtime library in {path}"

                                path
                            )

                        let archives = BrowserCompiler.validate releaseVersion output

                        // The release carries the browser compiler before NuGet has the version, so a roll to it
                        // finds the archives.
                        if not packOnly && githubRelease then
                            let tag = $"v{releaseVersion}"
                            let assets = archives |> List.map (fun path -> $"\"{path}\"") |> String.concat " "

                            // A rerun after a failed NuGet push finds the release already there.
                            if releaseExists tag then
                                SimpleExec.Command.Run(
                                    "gh",
                                    $"release upload {tag} {assets} --clobber",
                                    workingDirectory = Paths.root
                                )
                            else
                                SimpleExec.Command.Run(
                                    "gh",
                                    $"release create {tag} --title \"Fickl {releaseVersion}\" --generate-notes {assets}",
                                    workingDirectory = Paths.root
                                )

                        if not packOnly then
                            for path in packages do
                                printfn $"Publishing {Path.GetFileName path}"

                                runDotnet
                                    [
                                        "nuget"
                                        "push"
                                        path
                                        "--source"
                                        "https://api.nuget.org/v3/index.json"
                                        "--api-key"
                                        key
                                        "--skip-duplicate"
                                        // Compiler uses embedded symbols, so its generated snupkg has no PDBs.
                                        if
                                            Path
                                                .GetFileName(path)
                                                .StartsWith("Fickl.Compiler.", StringComparison.Ordinal)
                                        then
                                            "--no-symbols"
                                    ]

                        printfn $"Validated {packages.Length} packages and {archives.Length} archives in {output}"
                        0
                    )
                }
        }
    }
