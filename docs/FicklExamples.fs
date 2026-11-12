/// <summary>
/// The <c>fsharp fickl</c> fence: an F# example compiled by the Fickl compiler of this repository when the site is
/// built, shown in tabs beside the JavaScript and TypeScript it compiles to. The output on every page is what the
/// compiler produces, so an example cannot drift from it.
/// </summary>
module Docs.FicklExamples

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions
open Nacara.Core

/// <summary>One fence of a page.</summary>
type private Cell =
    {
        /// <summary>The line of the opening fence, from the start of the page body.</summary>
        Line: int
        Source: string
        /// <summary>Compiler flags, from <c>flags="--strictOptions --nativeErrors"</c>.</summary>
        Flags: string list
        /// <summary>The tabs after F#, from <c>show=js,ts,dts</c>.</summary>
        Show: string list
    }

/// <summary>A cell found in the page body, and the lines it replaces.</summary>
type private Found =
    {
        Cell: Cell
        First: int
        Last: int
        Indent: string
    }

let private openFence = Regex(@"^(\s*)(`{3,}|~{3,})\s*fsharp\s+fickl\b(.*)$")
let private anyFence = Regex(@"^\s*(`{3,}|~{3,})")
let private option = Regex("""(\w+)=("([^"]*)"|\S+)""")

let private parseOptions (text: string) =
    [
        for m in option.Matches text ->
            m.Groups[1].Value,
            (if m.Groups[3].Success then
                 m.Groups[3].Value
             else
                 m.Groups[2].Value)
    ]
    |> Map.ofList

/// <summary>Finds the fickl fences, skipping fences nested in other fences.</summary>
let private scan (lines: string array) =
    let found = ResizeArray<Found>()
    let mutable i = 0

    while i < lines.Length do
        let m = openFence.Match lines[i]

        if m.Success then
            let indent = m.Groups[1].Value
            let marker = m.Groups[2].Value
            let options = parseOptions m.Groups[3].Value
            let mutable j = i + 1

            while j < lines.Length && lines[j].Trim() <> marker do
                j <- j + 1

            let source =
                lines[i + 1 .. j - 1]
                |> Array.map (fun l ->
                    if l.StartsWith indent then
                        l.Substring indent.Length
                    else
                        l
                )
                |> String.concat "\n"

            let split (key: string) fallback =
                match options.TryFind key with
                | Some v -> v.Split([| ' '; ',' |], StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
                | None -> fallback

            found.Add
                {
                    Cell =
                        {
                            Line = i
                            Source = source
                            Flags = split "flags" []
                            Show = split "show" [ "js"; "ts" ]
                        }
                    First = i
                    Last = min j (lines.Length - 1)
                    Indent = indent
                }

            i <- j + 1
        else
            let other = anyFence.Match lines[i]

            if other.Success then
                let marker = other.Groups[1].Value
                let mutable j = i + 1

                while j < lines.Length && lines[j].Trim() <> marker do
                    j <- j + 1

                i <- j + 1
            else
                i <- i + 1

    List.ofSeq found

/// <summary>The repository the site sits in.</summary>
let private repository = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))

/// <summary>
/// The compiler: <c>FICKL_DOCS_CLI</c>, or the Release build of src/Fable.Cli. Build it with
/// <c>dotnet build src/Fable.Cli -c Release</c>.
/// </summary>
let private compiler () =
    match Environment.GetEnvironmentVariable "FICKL_DOCS_CLI" with
    | null
    | "" -> Path.Combine(repository, "src", "Fable.Cli", "bin", "Release", "net10.0", "fable.dll")
    | path -> Path.GetFullPath path

let private hash (text: string) =
    SHA256.HashData(Encoding.UTF8.GetBytes text)
    |> Convert.ToHexString
    |> _.Substring(0, 16).ToLowerInvariant()

/// <summary>The module a cell is compiled in, added when the cell does not declare one.</summary>
let private withModule index (source: string) =
    if Regex.IsMatch(source, @"^\s*(module|namespace)\s", RegexOptions.Multiline) then
        source
    else
        $"module Example%d{index + 1}\n\n%s{source}"

let private project =
    """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <NoWarn>$(NoWarn);FS3370</NoWarn>
  </PropertyGroup>
  <ItemGroup>
FILES  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="CORE" />
  </ItemGroup>
</Project>
"""

type private Output =
    {
        Js: string list
        Ts: string list
        Dts: string list
    }

let private run (cli: string) (args: string list) =
    let info =
        ProcessStartInfo("dotnet", RedirectStandardOutput = true, RedirectStandardError = true)

    info.ArgumentList.Add cli

    for arg in args do
        info.ArgumentList.Add arg

    use proc = Process.Start info
    let out = proc.StandardOutput.ReadToEndAsync()
    let err = proc.StandardError.ReadToEndAsync()
    proc.WaitForExit()
    proc.ExitCode, out.Result + err.Result

/// <summary>
/// Compiles the cells of one page that share their flags, once to JavaScript and once to TypeScript, into a
/// folder named by their sources, the flags and the compiler, so an unchanged page is not compiled again.
/// </summary>
let private compile (cells: (int * Cell) list) (flags: string list) : Result<Output, string> =
    let cli = compiler ()

    if not (File.Exists cli) then
        Error
            $"The Fickl compiler was not found at %s{cli}. Build it with `dotnet build src/Fable.Cli -c Release`, or set FICKL_DOCS_CLI."
    else
        let stamp = FileInfo cli |> fun f -> $"%d{f.Length}:%d{f.LastWriteTimeUtc.Ticks}"
        let sources = cells |> List.map (fun (i, c) -> withModule i c.Source)

        let key =
            hash (String.concat "\u0000" (stamp :: String.concat " " flags :: sources))

        let dir = Path.Combine(__SOURCE_DIRECTORY__, ".nacara", "fickl-examples", key)
        let done' = Path.Combine(dir, "done")
        let fileName n = $"Example%d{n + 1}"

        let read lang ext =
            cells
            |> List.map (fun (i, _) -> File.ReadAllText(Path.Combine(dir, lang, fileName i + ext)).Trim())

        let outputs () =
            {
                Js = read "js" ".js"
                Ts = read "ts" ".ts"
                Dts =
                    if List.contains "--declaration" flags then
                        read "js" ".d.ts"
                    else
                        []
            }

        if File.Exists done' then
            Ok(outputs ())
        else
            Directory.CreateDirectory dir |> ignore

            for (i, _), source in List.zip cells sources do
                File.WriteAllText(Path.Combine(dir, fileName i + ".fs"), source + "\n")

            let files =
                cells
                |> List.map (fun (i, _) -> $"    <Compile Include=\"%s{fileName i}.fs\" />\n")
                |> String.concat ""

            let core = Path.Combine(repository, "src", "Fickl.Core", "Fickl.Core.fsproj")

            File.WriteAllText(
                Path.Combine(dir, "Examples.fsproj"),
                project.Replace("FILES", files).Replace("CORE", core)
            )

            let compileTo lang extra =
                run
                    cli
                    ([
                        Path.Combine(dir, "Examples.fsproj")
                        "-o"
                        Path.Combine(dir, lang)
                        "--noCache"
                     ]
                     @ [ "--exclude"; "Fickl.Core"; "--exclude"; "Fable.Core" ]
                     @ [ "--fableLib"; "@fable-org/fable-library-" + lang ]
                     @ extra
                     @ flags)

            match compileTo "js" [] with
            | code, log when code <> 0 -> Error log
            | _ ->
                match compileTo "ts" [ "--lang"; "ts" ] with
                | code, log when code <> 0 -> Error log
                | _ ->
                    File.WriteAllText(done', "")
                    Ok(outputs ())

let private label =
    function
    | "js" -> "JavaScript", "js"
    | "ts" -> "TypeScript", "ts"
    | "dts" -> "Declarations", "ts"
    | other -> other, other

/// <summary>The tabs that replace a cell.</summary>
let private render (indent: string) (cell: Cell) (output: Output) index =
    let fence lang (code: string) =
        [ $"```%s{lang}"; code.Replace("\r\n", "\n"); "```" ]

    let tab title lines =
        [ $":::tab %s{title}" ] @ lines @ [ ":::" ]

    let pick =
        function
        | "js" -> output.Js |> List.tryItem index
        | "ts" -> output.Ts |> List.tryItem index
        | "dts" -> output.Dts |> List.tryItem index
        | _ -> None

    [
        yield "::::tabs"
        yield! tab "F#" (fence "fsharp" cell.Source)

        for show in cell.Show do
            match pick show with
            | Some code ->
                let title, lang = label show
                yield! tab title (fence lang code)
            | None -> ()

        yield "::::"
    ]
    |> List.collect (fun l -> l.Split '\n' |> List.ofArray)
    |> List.map (fun l ->
        if l = "" then
            l
        else
            indent + l
    )

let private transformPage (context: TransformContext) (page: Page) =
    let lines = page.Body.Replace("\r\n", "\n").Split '\n'

    match scan lines with
    | [] -> page
    | found ->
        let indexed = found |> List.mapi (fun i f -> i, f)

        let report (line: int) (message: string) =
            let diagnostic = Diagnostic.error "fickl-example" message

            context.Diagnostics.Add(
                match page.SourceFile with
                | Some file -> diagnostic |> Diagnostic.at file (page.BodyLine + line) 1
                | None -> diagnostic
            )

        let replacements =
            indexed
            |> List.groupBy (fun (_, f) -> f.Cell.Flags)
            |> List.collect (fun (flags, group) ->
                let cells = group |> List.map (fun (i, f) -> i, f.Cell)

                match compile cells flags with
                | Ok output ->
                    group
                    |> List.mapi (fun n (_, f) -> f.First, f.Last, render f.Indent f.Cell output n)
                | Error log ->
                    let tail =
                        log.Split('\n') |> Array.filter (fun l -> l.Trim() <> "") |> Array.truncate 40

                    report (snd group.Head).First ("The example did not compile:\n" + String.concat "\n" tail)
                    []
            )
            |> List.sortByDescending (fun (first, _, _) -> first)

        let body =
            (List.ofArray lines, replacements)
            ||> List.fold (fun lines (first, last, replacement) ->
                List.take first lines @ replacement @ List.skip (last + 1) lines
            )
            |> String.concat "\n"

        { page with Body = body }

/// <summary>Registers the fence. Its transform runs before Markdown turns the fences into highlighted code.</summary>
let register site =
    site
    |> Site.plugin
        { new IPlugin with
            member _.Name = "fickl-examples"

            member _.Configure registry =
                let registry =
                    registry
                    |> Registry.transform
                        {
                            Name = "fickl-examples"
                            Extensions = [ ".md"; ".markdown" ]
                            Transform = transformPage
                        }

                { registry with
                    Transforms =
                        List.last registry.Transforms
                        :: List.take (registry.Transforms.Length - 1) registry.Transforms
                }
        }
