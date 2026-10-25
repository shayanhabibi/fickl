/// Compiles each case in tests/Differences with Fickl and with upstream Fable, runs it with node, and lists in the
/// README the cases only Fickl gets right. A case Fable also passes leaves the table.
module Fickl.Build.Differences

open System
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions
open Partas.Build
open Fickl.Build.Forwarded

type private Case =
    {
        Name: string
        Directory: string
        /// Compiled in this order: the other files by name, then Main.fs
        Files: string list
        Change: string
        Fickl: string
        Issues: int list
        Args: string list
    }

type private Outcome =
    | Passed
    | TypeCheck of string
    | Compile of string
    | Runtime of string

let private casesDir = Path.Combine(Paths.root, "tests", "Differences")
let private workDir = Path.Combine(Paths.root, "temp", "differences")

/// The README block between these markers is rewritten by `differences --update-readme`.
let private readmeStart = "<!-- fickl-differences:start -->"
let private readmeEnd = "<!-- fickl-differences:end -->"

/// Reads the `// Key: value` lines at the top of a case's Main.fs.
let private readCase (directory: string) =
    let main = Path.Combine(directory, "Main.fs")

    if not (File.Exists main) then
        failwith $"{directory} has no Main.fs"

    let header =
        File.ReadLines main
        |> Seq.takeWhile _.StartsWith("//")
        |> Seq.choose (fun line ->
            match line.TrimStart('/').Split(':', 2) with
            | [| key; value |] -> Some(key.Trim(), value.Trim())
            | _ -> None
        )
        |> Map.ofSeq

    let field key =
        header.TryFind key
        |> Option.defaultWith (fun () -> failwith $"{main} has no // {key}: line")

    let others =
        Directory.GetFiles(directory, "*.fs")
        |> Array.filter (fun path -> Path.GetFileName path <> "Main.fs")
        |> Array.sort
        |> List.ofArray

    {
        Name = Path.GetFileName directory
        Directory = directory
        Files = others @ [ main ]
        Change = field "Change"
        Fickl = field "Fickl"
        Issues =
            match field "Issues" with
            | "none" -> []
            | issues -> issues.Split(',') |> Array.map (_.Trim() >> int) |> List.ofArray
        Args =
            header.TryFind "Args"
            |> Option.map (fun args -> args.Split(' ', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray)
            |> Option.defaultValue []
    }

let private runProcess (workingDirectory: string) (fileName: string) (arguments: string list) =
    let info =
        ProcessStartInfo(fileName, UseShellExecute = false, WorkingDirectory = workingDirectory)

    for argument in arguments do
        info.ArgumentList.Add argument

    info.RedirectStandardOutput <- true
    info.RedirectStandardError <- true
    use child = Process.Start info
    let stdout = child.StandardOutput.ReadToEndAsync()
    let stderr = child.StandardError.ReadToEndAsync()
    child.WaitForExit()

    // Fable colours some of its output, and the codes would end up in the README table
    let log = Regex.Replace(stdout.Result + stderr.Result, @"\x1b\[[0-9;]*[A-Za-z]", "")

    child.ExitCode, log.ReplaceLineEndings("\n")

/// The first line of a log matching a pattern, without the file position before it.
let private firstMatch (pattern: string) (log: string) =
    // A line that matches but holds nothing, such as a bare `error` before the message, does not count
    Regex.Matches(log, pattern, RegexOptions.Multiline)
    |> Seq.map (fun found -> found.Groups[1].Value.Trim())
    |> Seq.tryFind (String.IsNullOrEmpty >> not)

let private lastLine (log: string) =
    log.Split('\n', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
    |> Array.tryLast
    |> Option.defaultValue "no output"

/// Compiles a case into its own project, so one case failing to type-check does not hide the others.
let private check
    (compiler: string)
    (command: string * string list)
    (references: string)
    (extra: string list)
    (case: Case)
    =
    let project = Path.Combine(workDir, compiler, case.Name)

    if Directory.Exists project then
        Directory.Delete(project, true)

    Directory.CreateDirectory project |> ignore

    let files = Path.Combine(casesDir, "Check.fs") :: case.Files

    for file in files do
        File.Copy(file, Path.Combine(project, Path.GetFileName file))

    let compiles =
        files
        |> List.map (fun file -> $"""    <Compile Include="{Path.GetFileName file}" />""")
        |> String.concat "\n"

    File.WriteAllText(
        Path.Combine(project, "Case.fsproj"),
        $"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <NoWarn>FS0988</NoWarn>
  </PropertyGroup>
  <ItemGroup>
{compiles}
  </ItemGroup>
  <ItemGroup>
{references}
  </ItemGroup>
</Project>
"""
    )

    let output = Path.Combine(project, "out")
    let fileName, prefix = command

    let exitCode, log =
        runProcess
            project
            fileName
            [
                yield! prefix
                "Case.fsproj"
                "--lang"
                "javascript"
                "--outDir"
                output
                "--noCache"
                yield! extra
                yield! case.Args
            ]

    File.WriteAllText(Path.Combine(project, "compile.log"), log)

    // Fable reports F# errors as "FSHARP: <message> (code <n>)"
    match firstMatch @"(?:error FS\d+: |FSHARP: )(.*?)(?: \(code \d+\))?$" log with
    | Some message -> TypeCheck message
    | None when exitCode <> 0 || not (File.Exists(Path.Combine(output, "Main.js"))) ->
        Compile(
            firstMatch @"^(?:.*error\s*:?\s*)(.*)$" log
            |> Option.orElse (firstMatch @"^(Unknown argument.*)$" log)
            |> Option.defaultWith (fun () -> lastLine log)
        )
    | None ->
        File.WriteAllText(Path.Combine(output, "package.json"), """{ "type": "module" }""")
        let exitCode, log = runProcess output "node" [ "Main.js" ]
        File.WriteAllText(Path.Combine(project, "run.log"), log)

        if exitCode = 0 then
            Passed
        else
            // node prints a thrown F# exception as an object with its message field
            Runtime(
                firstMatch @"message: '((?:[^'\\]|\\.)*)'" log
                |> Option.orElse (firstMatch @"^\w*Error: (.*)$" log)
                |> Option.defaultWith (fun () -> lastLine log)
            )

let private describe (outcome: Outcome) =
    match outcome with
    | Passed -> "passes"
    | TypeCheck message -> $"doesn't type-check: {message}"
    | Compile message -> $"doesn't compile: {message}"
    | Runtime message -> $"compiles, but fails at runtime: {message}"

/// Keeps a message to one table cell.
let private cell (text: string) =
    let text = text.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ")

    if text.Length > 160 then
        text[..156] + "..."
    else
        text

let differences =
    let fickl =
        Input.optionMaybe<string> "--fickl"
        |> Input.description "A Fickl executable to check instead of this repository's Fable.Cli in Release"

    let fableVersion =
        Input.option<string> "--fable-version"
        |> Input.description "The upstream Fable version to compare with; latest is the newest stable on nuget.org"
        |> Input.def "latest"

    let only =
        Input.optionMaybe<string> "--only"
        |> Input.description "Comma-separated cases to check, by folder name under tests/Differences"

    let updateReadme =
        Input.option<bool> "--update-readme"
        |> Input.description "Write the table of differences into the README"

    command "differences" {
        description
            "Check the cases in tests/Differences, which Fickl gets right, against upstream Fable, and tabulate the ones Fable gets wrong"

        input {
            let! fickl = fickl
            and! fableVersion = fableVersion
            and! only = only
            and! updateReadme = updateReadme

            return
                stage "differences" {
                    run (fun _ ->
                        let fableVersion =
                            if fableVersion = "latest" then
                                Benchmark.latestStable "fable"
                            else
                                fableVersion

                        let cases =
                            Directory.GetDirectories casesDir
                            |> Array.sort
                            |> Array.map readCase
                            |> Array.filter (fun case ->
                                match only with
                                | None -> true
                                | Some names -> names.Split(',') |> Array.exists (fun n -> n.Trim() = case.Name)
                            )
                            |> List.ofArray

                        if updateReadme && only.IsSome then
                            failwith "--update-readme needs every case; drop --only"

                        // Keep the repository's build settings out of the case projects
                        Directory.CreateDirectory workDir |> ignore

                        for props in
                            [
                                "Directory.Build.props"
                                "Directory.Build.targets"
                                "Directory.Packages.props"
                            ] do
                            File.WriteAllText(Path.Combine(workDir, props), "<Project />")

                        Directory.CreateDirectory Benchmark.benchDir |> ignore
                        let fable = Benchmark.installTool "fable" fableVersion "fable" None
                        let fableCore = Benchmark.latestStable "fable.core"

                        let ficklCommand =
                            match fickl with
                            | Some fickl -> fickl, []
                            | None ->
                                let cli = Path.Combine(Paths.root, "src", "Fable.Cli")

                                match runProcess Paths.root "dotnet" [ "build"; cli; "-c"; "Release" ] with
                                | 0, _ -> ()
                                | _, log -> failwith $"Building Fable.Cli failed:\n{log}"

                                "dotnet", [ "run"; "--no-build"; "-c"; "Release"; "--project"; cli; "--" ]

                        let ficklReferences =
                            $"""    <ProjectReference Include="{Path.Combine(Paths.root, "src", "Fickl.Core", "Fickl.Core.fsproj")}" />"""

                        let fableReferences =
                            $"""    <PackageReference Include="Fable.Core" Version="{fableCore}" />"""

                        let results =
                            [
                                for case in cases do
                                    let ficklOutcome =
                                        check "fickl" ficklCommand ficklReferences [ "--exclude"; "Fable.Core" ] case

                                    let fableOutcome = check "fable" (fable, []) fableReferences [] case

                                    printfn
                                        $"{case.Name}: Fickl {describe ficklOutcome}; Fable {describe fableOutcome}"

                                    case, ficklOutcome, fableOutcome
                            ]

                        let regressions = results |> List.filter (fun (_, fickl, _) -> fickl <> Passed)

                        for case, _, _ in results |> List.filter (fun (_, _, fable) -> fable = Passed) do
                            printfn
                                $"Fable {fableVersion} now passes {case.Name}, so it is left out of the table; delete tests/Differences/{case.Name} once Fickl merges that release"

                        let rows =
                            results
                            |> List.filter (fun (_, fickl, fable) -> fickl = Passed && fable <> Passed)

                        let issues (case: Case) =
                            match case.Issues with
                            | [] -> "—"
                            | issues ->
                                issues
                                |> List.map (fun n -> $"[#{n}](https://github.com/shayanhabibi/fickl/issues/{n})")
                                |> String.concat ", "

                        let block =
                            [
                                readmeStart
                                $"What Fickl gets right that Fable {fableVersion} does not, checked by compiling each case in [tests/Differences](tests/Differences) with both and running it with node. A case leaves this table once Fable passes it too."
                                ""
                                $"| Change | Fickl | Fable {fableVersion} | Issues |"
                                "| --- | --- | --- | --- |"
                                for case, _, fable in rows do
                                    $"| {case.Change} | {cell case.Fickl} | {cell (describe fable)} | {issues case} |"
                                readmeEnd
                            ]
                            |> String.concat "\n"

                        File.WriteAllText(Path.Combine(workDir, "differences.md"), block + "\n")

                        printfn
                            $"{rows.Length} of {results.Length} cases differ; table in temp/differences/differences.md"

                        if updateReadme && regressions.IsEmpty then
                            let readmePath = Path.Combine(Paths.root, "README.md")
                            let readme = File.ReadAllText(readmePath).ReplaceLineEndings "\n"
                            let first = readme.IndexOf readmeStart
                            let last = readme.IndexOf readmeEnd

                            if first < 0 || last < first then
                                failwith $"README.md has no {readmeStart} … {readmeEnd} block to replace"

                            File.WriteAllText(
                                readmePath,
                                readme[.. first - 1] + block + readme[last + readmeEnd.Length ..]
                            )

                            printfn "Updated README.md"

                        if regressions.IsEmpty then
                            0
                        else
                            for case, outcome, _ in regressions do
                                eprintfn
                                    $"Fickl fails {case.Name}: {describe outcome}; see temp/differences/fickl/{case.Name}"

                            1
                    )
                }
        }
    }
