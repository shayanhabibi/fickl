/// <summary>The browser compiler and its reference assemblies, as the two archives a release carries.</summary>
/// <remarks>
/// Both archives use the layout <c>npm pack</c> writes, one top folder <c>package/</c>: the standalone archive
/// holds <c>package.json</c> and <c>dist/</c> with <c>bundle.min.js</c>, <c>worker.min.js</c> and
/// <c>fable-library-js/</c>; the metadata archive holds <c>package.json</c> and <c>lib/*.dll</c>.
/// </remarks>
module Fickl.Build.BrowserCompiler

open System
open System.Formats.Tar
open System.IO
open System.IO.Compression
open System.Text.Json
open System.Text.RegularExpressions
open Fickl.Build.Forwarded

/// <summary>The standalone archive's file name.</summary>
let standaloneArchive (version: string) = $"fickl-standalone-{version}.tgz"

/// <summary>The metadata archive's file name.</summary>
let metadataArchive (version: string) = $"fickl-metadata-{version}.tgz"

let private standaloneDirectory =
    Path.Combine(Paths.root, "src", "fable-standalone")

let private metadataDirectory = Path.Combine(Paths.root, "src", "fable-metadata")

/// The Fable version the browser compiler reports: <c>fableVersion</c> in its package.json.
let private standaloneFableVersion () =
    let manifest = Path.Combine(standaloneDirectory, "package.json")
    use document = JsonDocument.Parse(File.ReadAllText manifest)
    document.RootElement.GetProperty("fableVersion").GetString()

/// The Fable version a precompile records: the <c>VERSION</c> literal of the compiler.
let private compilerVersion () =
    let source =
        Path.Combine(Paths.root, "src", "Fable.Transforms", "Global", "Compiler.fs")

    let found = Regex.Match(File.ReadAllText source, "let VERSION = \"([^\"]+)\"")

    if not found.Success then
        failwith $"No VERSION literal in {source}"

    found.Groups[1].Value

/// The files under a directory, each with its path relative to that directory, '/'-separated.
let private filesUnder (directory: string) =
    Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
    |> Seq.sort
    |> Seq.map (fun file -> file, Path.GetRelativePath(directory, file).Replace('\\', '/'))
    |> List.ofSeq

/// Writes a gzipped tarball holding each source file under its entry name.
let private writeArchive (path: string) (entries: (string * string) list) =
    use file = File.Create path
    use gzip = new GZipStream(file, CompressionLevel.Optimal)
    use tar = new TarWriter(gzip, TarEntryFormat.Pax, false)

    for source, name in entries do
        tar.WriteEntry(source, name)

/// The entry names of a gzipped tarball.
let private entriesOf (path: string) =
    use file = File.OpenRead path
    use gzip = new GZipStream(file, CompressionMode.Decompress)
    use tar = new TarReader(gzip)

    Seq.initInfinite (fun _ -> tar.GetNextEntry())
    |> Seq.takeWhile (fun entry -> not (isNull entry))
    |> Seq.map _.Name
    |> List.ofSeq

/// <summary>The two archives of a release, checked for every entry the live example plugin reads.</summary>
/// <param name="version">The fickl version, without a leading <c>v</c>.</param>
/// <param name="output">The directory holding the archives.</param>
/// <returns>The standalone archive's path, then the metadata archive's.</returns>
let validate (version: string) (output: string) =
    let check (archive: string) (required: (string * (string -> bool)) list) =
        let path = Path.Combine(output, archive)

        if not (File.Exists path) then
            failwith $"Missing {archive} in {output}"

        let entries = entriesOf path

        for entry, matches in required do
            if not (List.exists matches entries) then
                failwith $"Missing {entry} in {archive}"

        path

    let exactly (name: string) = name, (fun entry -> entry = name)

    let standalone =
        check
            (standaloneArchive version)
            [
                exactly "package/package.json"
                exactly "package/dist/bundle.min.js"
                exactly "package/dist/worker.min.js"
                "package/dist/fable-library-js/",
                (fun entry -> entry.StartsWith("package/dist/fable-library-js/", StringComparison.Ordinal))
            ]

    let metadata =
        check (metadataArchive version) [ exactly "package/package.json"; exactly "package/lib/FSharp.Core.dll" ]

    [ standalone; metadata ]

/// <summary>Builds the browser compiler and writes the two archives of a release.</summary>
/// <remarks>Fails when the browser compiler's <c>fableVersion</c> differs from the compiler's <c>VERSION</c>: the
/// live example plugin compares the first with the version a precompile records, which is the second.</remarks>
/// <param name="version">The fickl version, without a leading <c>v</c>.</param>
/// <param name="output">The directory the archives are written to.</param>
/// <returns>The standalone archive's path, then the metadata archive's.</returns>
let pack (version: string) (output: string) =
    let fableVersion = standaloneFableVersion ()
    let expected = compilerVersion ()

    if fableVersion <> expected then
        failwith
            $"fableVersion {fableVersion} in src/fable-standalone/package.json differs from the compiler's VERSION {expected}"

    let exitCode = Build.Main.main [| "standalone" |]

    if exitCode <> 0 then
        failwith $"Building the browser compiler failed with exit code {exitCode}"

    Directory.CreateDirectory output |> ignore
    let dist = Path.Combine(standaloneDirectory, "dist")

    writeArchive
        (Path.Combine(output, standaloneArchive version))
        [
            Path.Combine(standaloneDirectory, "package.json"), "package/package.json"
            Path.Combine(dist, "bundle.min.js"), "package/dist/bundle.min.js"
            Path.Combine(dist, "worker.min.js"), "package/dist/worker.min.js"
            for file, relative in filesUnder (Path.Combine(dist, "fable-library-js")) do
                file, $"package/dist/fable-library-js/{relative}"
        ]

    writeArchive
        (Path.Combine(output, metadataArchive version))
        [
            Path.Combine(metadataDirectory, "package.json"), "package/package.json"
            for file in
                Directory.EnumerateFiles(Path.Combine(metadataDirectory, "lib"), "*.dll")
                |> Seq.sort do
                file, $"package/lib/{Path.GetFileName file}"
        ]

    validate version output
