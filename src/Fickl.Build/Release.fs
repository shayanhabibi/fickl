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
    // The bundled big.js has a hand-written declaration file, which tsc reads but does not emit
    File.Copy(Path.Combine(tsBuildDir, "lib", "big.d.ts"), Path.Combine(jsOutDir, "lib", "big.d.ts"), true)

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
        "Fickl"
    ]

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

                                for dependency in
                                    document.Descendants() |> Seq.filter (fun e -> e.Name.LocalName = "dependency") do
                                    let dependencyId = dependency.Attribute(XName.Get "id").Value
                                    let dependencyVersion = dependency.Attribute(XName.Get "version").Value

                                    if
                                        dependencyId.StartsWith("Fickl.", StringComparison.Ordinal)
                                        && dependencyVersion <> releaseVersion
                                    then
                                        failwith $"Mismatched dependency {dependencyId} {dependencyVersion} in {path}"

                                if
                                    id = "Fickl.any"
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

                        if not packOnly && githubRelease then
                            SimpleExec.Command.Run(
                                "gh",
                                $"release create v{releaseVersion} --title \"Fickl {releaseVersion}\" --generate-notes",
                                workingDirectory = Paths.root
                            )

                        printfn $"Validated {packages.Length} packages in {output}"
                        0
                    )
                }
        }
    }
