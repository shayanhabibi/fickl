/// Compares a Fickl release with upstream Fable on six real workloads, and draws the README chart.
module Fickl.Build.Benchmark

open System
open System.Diagnostics
open System.Globalization
open System.IO
open System.Runtime.InteropServices
open System.Text
open System.Text.RegularExpressions
open System.Xml.Linq
open Partas.Build
open Fickl.Build.Forwarded

type private Workload =
    {
        Name: string
        Label: string
        /// Repository and commit to clone, or None for a workload in this repository
        Source: (string * string) option
        /// The project's folder, relative to the clone or to the repository root
        Directory: string
        Project: string
        Language: string
        Extra: string list
        /// Run in the clone after checkout, before the first compile
        Setup: (string * string) list
    }

let private workloads =
    [
        {
            Name = "sutil"
            Label = "Sutil demo app"
            Source = Some("https://github.com/davedawkins/Sutil.git", "089f1a00d42da7eb33e50e94c92c3ad294cc4960")
            Directory = "src/App"
            Project = "App.fsproj"
            Language = "javascript"
            Extra = []
            Setup = []
        }
        {
            Name = "fantomas"
            Label = "Fantomas online tools"
            Source =
                Some("https://github.com/fsprojects/fantomas-tools.git", "38a91eaf038e7f5f3ba794dc4ae80e8d0d3f9102")
            Directory = "src/client/fsharp"
            Project = "FantomasTools.fsproj"
            Language = "javascript"
            Extra = []
            Setup = []
        }
        {
            Name = "ionide"
            Label = "Ionide for VS Code"
            Source =
                Some("https://github.com/ionide/ionide-vscode-fsharp.git", "3bd3416975e92c2e5acba80570b40dfe0e44921e")
            Directory = "src"
            Project = "Ionide.FSharp.fsproj"
            Language = "javascript"
            Extra = []
            Setup = [ "dotnet", "tool restore"; "dotnet", "paket restore" ]
        }
        {
            Name = "jssuite"
            Label = "Fable JS test suite"
            // Upstream's suite at 5.24.0: Fickl's own suite uses features upstream Fable rejects
            Source = Some("https://github.com/fable-compiler/Fable.git", "be20361ffaf44ccc91a430501caec6e65db31db2")
            Directory = "tests/Js/Main"
            Project = "Fable.Tests.fsproj"
            Language = "javascript"
            Extra = [ "--exclude"; "Fable.Core" ]
            Setup = []
        }
        {
            Name = "py"
            Label = "Python package workload"
            Source = None
            Directory = "notes/benchmarks/workloads/py"
            Project = "App.fsproj"
            Language = "python"
            Extra = []
            Setup = []
        }
        {
            Name = "beam"
            Label = "BEAM package workload"
            Source = None
            Directory = "notes/benchmarks/workloads/beam"
            Project = "App.fsproj"
            Language = "beam"
            Extra = []
            Setup = []
        }
    ]

/// fantomas-tools pins a .NET 11 release candidate; accept any 11.0.1xx one, as the pinned one may be gone.
let private fantomasGlobalJson =
    """{ "sdk": { "version": "11.0.100-rc.1.26425.128", "rollForward": "latestFeature", "allowPrerelease": true } }"""

type private Config =
    | Fable
    | FicklNoPackageCache
    | FicklCacheHit

    member this.Key =
        match this with
        | Fable -> "fable"
        | FicklNoPackageCache -> "nopkg"
        | FicklCacheHit -> "hit"

let private configs = [ Fable; FicklNoPackageCache; FicklCacheHit ]

let internal benchDir = Path.Combine(Paths.root, "temp", "benchmark")

let private exec (workingDirectory: string) (fileName: string) (arguments: string) =
    let info =
        ProcessStartInfo(fileName, arguments, UseShellExecute = false, WorkingDirectory = workingDirectory)

    use child = Process.Start info
    child.WaitForExit()

    if child.ExitCode <> 0 then
        failwith $"{fileName} {arguments} failed with exit code {child.ExitCode} in {workingDirectory}"

/// On Windows a tool's shim is an .exe, or a .cmd for tool packages with per-runtime payloads such as Fickl's.
let internal executable (toolPath: string) (command: string) =
    if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
        [ ".exe"; ".cmd" ]
        |> List.map (fun extension -> Path.Combine(toolPath, command + extension))
        |> List.tryFind File.Exists
        |> Option.defaultValue (Path.Combine(toolPath, command + ".exe"))
    else
        Path.Combine(toolPath, command)

/// Installs a .NET tool into its own folder, leaving any global install alone.
let internal installTool (package: string) (version: string) (command: string) (source: string option) =
    let toolPath = Path.Combine(benchDir, "tools", $"{package}-{version}")

    if not (File.Exists(executable toolPath command)) then
        Directory.CreateDirectory toolPath |> ignore

        let addSource =
            match source with
            | Some source -> $" --add-source \"{source}\""
            | None -> ""

        exec benchDir "dotnet" $"tool install {package} --version {version} --tool-path \"{toolPath}\"{addSource}"

    executable toolPath command

let private checkout (workload: Workload) =
    match workload.Source with
    | None -> Path.Combine(Paths.root, workload.Directory)
    | Some(url, commit) ->
        let clone = Path.Combine(benchDir, "workloads", workload.Name)

        if not (Directory.Exists(Path.Combine(clone, ".git"))) then
            Directory.CreateDirectory clone |> ignore
            exec clone "git" "init --quiet"
            exec clone "git" $"remote add origin {url}"
            exec clone "git" $"fetch --quiet --depth 1 origin {commit}"
            exec clone "git" "checkout --quiet FETCH_HEAD"

            if workload.Name = "fantomas" then
                File.WriteAllText(Path.Combine(clone, "global.json"), fantomasGlobalJson)

            for fileName, arguments in workload.Setup do
                exec clone fileName arguments

        Path.Combine(clone, workload.Directory)

type private Row =
    {
        Workload: string
        Config: string
        Run: int
        WallMs: int64
        ParsedMs: string
        CompileMs: string
        ExitCode: int
    }

let private compile (fable: string) (fickl: string) (workload: Workload) (directory: string) (config: Config) run =
    let output = Path.Combine(benchDir, "output", $"{workload.Name}-{config.Key}")

    if Directory.Exists output then
        Directory.Delete(output, true)

    let tool =
        match config with
        | Fable -> fable
        | FicklNoPackageCache
        | FicklCacheHit -> fickl

    let info =
        ProcessStartInfo(tool, UseShellExecute = false, WorkingDirectory = directory)

    for argument in
        [
            workload.Project
            "--lang"
            workload.Language
            yield! workload.Extra
            "--outDir"
            output
            "--noCache"
            if config = FicklNoPackageCache then
                "--noPackageCache"
        ] do
        info.ArgumentList.Add argument

    info.RedirectStandardOutput <- true
    info.RedirectStandardError <- true
    // CI runners opt out of telemetry; measure Fable as a user runs it, with the default
    info.Environment.Remove "DOTNET_CLI_TELEMETRY_OPTOUT" |> ignore
    info.Environment.Remove "FICKL_NUGET_KEY" |> ignore

    info.Environment["FICKL_PACKAGE_CACHE"] <- Path.Combine(benchDir, "package-cache")

    let watch = Stopwatch.StartNew()
    use child = Process.Start info
    let stdout = child.StandardOutput.ReadToEndAsync()
    let stderr = child.StandardError.ReadToEndAsync()
    child.WaitForExit()
    watch.Stop()
    let log = stdout.Result + stderr.Result
    Directory.CreateDirectory(Path.Combine(benchDir, "logs")) |> ignore
    File.WriteAllText(Path.Combine(benchDir, "logs", $"{workload.Name}-{config.Key}-{run}.log"), log)

    let timing pattern =
        let matches = Regex.Matches(log, pattern)

        if matches.Count = 0 then
            ""
        else
            matches[matches.Count - 1].Groups[1].Value

    let row =
        {
            Workload = workload.Name
            Config = config.Key
            Run = run
            WallMs = watch.ElapsedMilliseconds
            ParsedMs = timing @"parsed in (\d+)ms"
            CompileMs = timing @"compilation finished in (\d+)ms"
            ExitCode = child.ExitCode
        }

    printfn $"{row.Workload} {row.Config} run {row.Run}: {row.WallMs} ms"

    if row.ExitCode <> 0 || row.CompileMs = "" then
        failwith $"{workload.Name} with {config.Key} failed; see temp/benchmark/logs"

    row

let private median (values: float list) =
    let sorted = List.sort values
    let n = sorted.Length

    if n % 2 = 1 then
        sorted[n / 2]
    else
        (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0

let private invariant (format: string) (value: float) =
    value.ToString(format, CultureInfo.InvariantCulture)

let private escape (text: string) =
    text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")

/// A horizontal bar anchored at the axis, rounded only at its data end.
let private bar (x: float) (y: float) (width: float) (height: float) (fill: string) =
    let r = min 4.0 (min width (height / 2.0))
    let f = invariant "0.##"

    $"""<path class="{fill}" d="M{f x} {f y}h{f (width - r)}a{f r} {f r} 0 0 1 {f r} {f r}v{f (height - 2.0 * r)}a{f r} {f r} 0 0 1 -{f r} {f r}h-{f (width - r)}z"/>"""

type private Panel =
    {
        Label: string
        /// Series class and value, drawn in legend order
        Bars: (string * float) list
        /// The speedup printed to the right of the panel
        Ratio: float
    }

/// One panel per row, sharing one value axis, with the speedup over Fable on the right of each.
let private chart
    (title: string)
    (subtitle: string)
    (legend: (string * string) list)
    (panels: Panel list)
    (format: float -> string)
    (footnotes: string list)
    =
    let width = 960.0
    let plotLeft = 250.0
    let plotRight = 820.0
    let barHeight = 14.0
    let barGap = 4.0
    let series = float legend.Length
    let panelHeight = series * (barHeight + barGap) + 26.0
    let top = 104.0 + ceil (series / 2.0) * 22.0 + 4.0
    let largest = panels |> List.collect (fun p -> List.map snd p.Bars) |> List.max
    let limit = largest * 1.1

    let step =
        [
            for magnitude in [ 0.001; 0.01; 0.1; 1.0; 10.0; 100.0; 1000.0; 10000.0 ] do
                for m in [ 1.0; 2.0; 5.0 ] -> m * magnitude
        ]
        |> List.find (fun step -> limit / step <= 8.0)

    let scale (value: float) = value / limit * (plotRight - plotLeft)

    let plotBottom = top + float panels.Length * panelHeight
    let height = plotBottom + 40.0 + float footnotes.Length * 18.0 + 16.0
    let f = invariant "0.##"
    let svg = StringBuilder()
    let line (text: string) = svg.AppendLine text |> ignore

    line
        $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {f width} {f height}" font-family="Segoe UI, Helvetica, Arial, sans-serif">"""

    line
        """<style>
  svg { --surface: #fcfcfb; --ink: #0b0b0b; --muted: #52514e; --grid: #e4e3df;
        --fable: #2a78d6; --nopkg: #1baf7a; --hit: #eda100; }
  @media (prefers-color-scheme: dark) {
    svg { --surface: #1a1a19; --ink: #f0efec; --muted: #c3c2b7; --grid: #383835;
          --fable: #3987e5; --nopkg: #199e70; --hit: #c98500; }
  }
  .surface { fill: var(--surface); } .ink { fill: var(--ink); } .muted { fill: var(--muted); }
  .grid { stroke: var(--grid); stroke-width: 1; }
  .fable { fill: var(--fable); } .nopkg { fill: var(--nopkg); } .hit { fill: var(--hit); }
</style>"""

    line $"""<rect class="surface" width="{f width}" height="{f height}"/>"""
    line $"""<text class="ink" x="32" y="52" font-size="28" font-weight="700">{escape title}</text>"""
    line $"""<text class="muted" x="32" y="80" font-size="14">{escape subtitle}</text>"""

    legend
    |> List.iteri (fun i (key, label) ->
        let x = 32.0 + float (i % 2) * 380.0
        let y = 104.0 + float (i / 2) * 22.0
        line (bar x (y - 11.0) 14.0 14.0 key)
        line $"""<text class="ink" x="{f (x + 22.0)}" y="{f y}" font-size="13">{escape label}</text>"""
    )

    let mutable tick = 0.0

    while tick <= limit do
        let x = plotLeft + scale tick
        line $"""<line class="grid" x1="{f x}" y1="{f top}" x2="{f x}" y2="{f plotBottom}"/>"""

        line
            $"""<text class="muted" x="{f x}" y="{f (plotBottom + 18.0)}" font-size="11" text-anchor="middle">{escape (format tick)}</text>"""

        tick <- tick + step

    panels
    |> List.iteri (fun i panel ->
        let panelTop = top + float i * panelHeight + 8.0
        let middle = panelTop + series * (barHeight + barGap) / 2.0

        line (
            match panel.Label.Split '\n' with
            | [| label; detail |] ->
                $"""<text x="{f (plotLeft - 16.0)}" y="{f (middle - 5.0)}" font-size="14" text-anchor="end"><tspan class="ink" font-weight="600">{escape label}</tspan><tspan class="muted" x="{f (plotLeft - 16.0)}" dy="18" font-size="12">{escape detail}</tspan></text>"""
            | _ ->
                $"""<text class="ink" x="{f (plotLeft - 16.0)}" y="{f (middle + 3.0)}" font-size="14" font-weight="600" text-anchor="end">{escape panel.Label}</text>"""
        )

        panel.Bars
        |> List.iteri (fun j (key, value) ->
            let y = panelTop + float j * (barHeight + barGap)
            line (bar plotLeft y (scale value) barHeight key)

            line
                $"""<text class="ink" x="{f (plotLeft + scale value + 6.0)}" y="{f (y + barHeight - 3.0)}" font-size="11">{escape (format value)}</text>"""
        )

        line
            $"""<text class="ink" x="{f (plotRight + 24.0)}" y="{f (middle + 2.0)}" font-size="22" font-weight="700">{invariant "0.0" panel.Ratio}×</text>"""

        line
            $"""<text class="muted" x="{f (plotRight + 24.0)}" y="{f (middle + 20.0)}" font-size="11">faster*</text>"""
    )

    footnotes
    |> List.iteri (fun i note ->
        line
            $"""<text class="muted" x="32" y="{f (plotBottom + 46.0 + float i * 18.0)}" font-size="11">{escape note}</text>"""
    )

    line "</svg>"
    svg.ToString()

/// Each watch session saves a comment to a leaf file, then a comment to an early file most of the project depends on,
/// then a new public value to that early file. Only the last changes what the dependents compile to.
type private WatchTarget =
    {
        Workload: string
        Leaf: string
        Root: string
    }

let private watchTargets =
    [
        {
            Workload = "sutil"
            Leaf = "App.fs"
            Root = "Types.fs"
        }
        {
            Workload = "jssuite"
            Leaf = "Main.fs"
            Root = "Util/Util.Testing.fs"
        }
    ]

type private WatchRow =
    {
        Workload: string
        Compiler: string
        Edit: string
        Run: int
        Ms: int64
    }

/// Starts a watch process and times each saved edit until the compiler next reports `Watching`.
let private watch (tool: string) (key: string) (workload: Workload) (directory: string) (target: WatchTarget) runs =
    let output = Path.Combine(benchDir, "output", $"{workload.Name}-watch-{key}")

    if Directory.Exists output then
        Directory.Delete(output, true)

    let info =
        ProcessStartInfo(tool, UseShellExecute = false, WorkingDirectory = directory)

    for argument in
        [
            workload.Project
            "--lang"
            workload.Language
            yield! workload.Extra
            "--outDir"
            output
            "--noCache"
            "--watch"
        ] do
        info.ArgumentList.Add argument

    info.RedirectStandardOutput <- true
    info.RedirectStandardError <- true
    info.Environment.Remove "DOTNET_CLI_TELEMETRY_OPTOUT" |> ignore
    info.Environment["FICKL_PACKAGE_CACHE"] <- Path.Combine(benchDir, "package-cache")
    use lines = new Collections.Concurrent.BlockingCollection<string>()
    let log = StringBuilder()

    let files =
        [
            "leaf", Path.Combine(directory, target.Leaf), (fun run -> $"\n// fickl watch probe {run}\n")
            "root", Path.Combine(directory, target.Root), (fun run -> $"\n// fickl watch probe {run}\n")
            "api", Path.Combine(directory, target.Root), (fun run -> $"\nlet ficklWatchProbe{run} = {run}\n")
        ]

    let originals =
        files
        |> List.map (fun (_, path, _) -> path)
        |> List.distinct
        |> List.map (fun path -> path, File.ReadAllText path)

    use child = new Process(StartInfo = info)

    let receive (args: DataReceivedEventArgs) =
        if not (isNull args.Data) then
            lock log (fun () -> log.AppendLine args.Data |> ignore)
            lines.Add args.Data

    child.OutputDataReceived.Add receive
    child.ErrorDataReceived.Add receive

    let waitForWatching (timeout: TimeSpan) =
        let elapsed = Stopwatch.StartNew()
        let mutable seen = false

        while not seen do
            let remaining = timeout - elapsed.Elapsed
            let mutable text = null

            if remaining <= TimeSpan.Zero || not (lines.TryTake(&text, remaining)) then
                failwith $"{workload.Name} --watch with {key} did not report Watching in time; see temp/benchmark/logs"

            seen <- text.Contains "Watching"

    try
        child.Start() |> ignore
        child.BeginOutputReadLine()
        child.BeginErrorReadLine()
        waitForWatching (TimeSpan.FromMinutes 15.0)

        [
            // Run 0 warms the JIT on the edit path, and is kept out of the medians
            for run in 0..runs do
                for edit, path, text in files do
                    // Past any debounce and settling left by the previous edit
                    Threading.Thread.Sleep 1500
                    let mutable stale = null

                    while lines.TryTake(&stale) do
                        ()

                    let elapsed = Stopwatch.StartNew()
                    File.AppendAllText(path, text run)
                    waitForWatching (TimeSpan.FromMinutes 5.0)
                    elapsed.Stop()
                    printfn $"{workload.Name} watch {key} {edit} run {run}: {elapsed.ElapsedMilliseconds} ms"

                    {
                        Workload = workload.Name
                        Compiler = key
                        Edit = edit
                        Run = run
                        Ms = elapsed.ElapsedMilliseconds
                    }
        ]
    finally
        if not child.HasExited then
            child.Kill true

        child.WaitForExit()

        for path, text in originals do
            File.WriteAllText(path, text)

        Directory.CreateDirectory(Path.Combine(benchDir, "logs")) |> ignore
        File.WriteAllText(Path.Combine(benchDir, "logs", $"{workload.Name}-watch-{key}.log"), log.ToString())

/// The newest stable version of a package on nuget.org.
let internal latestStable (package: string) =
    use client = new Net.Http.HttpClient()

    let index =
        client.GetStringAsync($"https://api.nuget.org/v3-flatcontainer/{package.ToLowerInvariant()}/index.json").Result

    Text.Json.JsonDocument.Parse(index).RootElement.GetProperty("versions").EnumerateArray()
    |> Seq.map _.GetString()
    |> Seq.filter (fun v -> not (v.Contains '-'))
    |> Seq.maxBy Version.Parse

let private ficklVersion () =
    XDocument.Load(Path.Combine(Paths.root, "src", "Fickl.Branding.targets")).Descendants(XName.Get "FicklVersion")
    |> Seq.exactlyOne
    |> _.Value

let private oneDecimal = invariant "0.0"
let private whole = invariant "0"

/// The README block between these markers is rewritten by `benchmark --update-readme`.
let private readmeStart = "<!-- fickl-benchmarks:start -->"
let private readmeEnd = "<!-- fickl-benchmarks:end -->"

/// Reads the results of `benchmark --only` runs, such as one CI job per workload, back into rows.
let private readShards (folder: string) =
    let lines name =
        Directory.GetFiles(folder, name, SearchOption.AllDirectories)
        |> Seq.collect (File.ReadAllLines >> Seq.skip 1)
        |> Seq.filter (fun line -> line <> "")
        |> Seq.map _.Split('\t')
        |> List.ofSeq

    let rows =
        lines "results.tsv"
        |> List.map (fun f ->
            {
                Workload = f[0]
                Config = f[1]
                Run = int f[2]
                WallMs = int64 f[3]
                ParsedMs = f[4]
                CompileMs = f[5]
                ExitCode = int f[6]
            }
        )

    let watchRows =
        lines "watch.tsv"
        |> List.map (fun f ->
            {
                WatchRow.Workload = f[0]
                Compiler = f[1]
                Edit = f[2]
                Run = int f[3]
                Ms = int64 f[4]
            }
        )

    let measured = rows |> List.map _.Workload |> Set.ofList
    workloads |> List.filter (fun w -> measured.Contains w.Name), rows, watchRows

let benchmark =
    let fickl =
        Input.optionMaybe<string> "--fickl"
        |> Input.description "A Fickl executable to measure instead of installing the release"

    let version =
        Input.optionMaybe<string> "--version"
        |> Input.description "The Fickl version to install and name the results after; defaults to FicklVersion"

    let fableVersion =
        Input.option<string> "--fable-version"
        |> Input.description "The upstream Fable version to compare with; latest is the newest stable on nuget.org"
        |> Input.def "latest"

    let runs =
        Input.option<int> "--runs"
        |> Input.description "Measured runs per configuration, after one warm-up run"
        |> Input.def 5
        |> Input.validate (fun runs ->
            if runs >= 1 then
                Ok()
            else
                Error "--runs must be at least 1"
        )

    let only =
        Input.optionMaybe<string> "--only"
        |> Input.description "Comma-separated workloads to measure (sutil, fantomas, ionide, jssuite, py, beam)"

    let skipWatch =
        Input.option<bool> "--skip-watch"
        |> Input.description "Measure only fresh compilations, not --watch response times"

    let report =
        Input.optionMaybe<string> "--report"
        |> Input.description
            "Draw the charts from the results.tsv and watch.tsv files under this folder, such as CI shards, instead of measuring"

    let updateReadme =
        Input.option<bool> "--update-readme"
        |> Input.description "Write the charts and timings to notes/benchmarks and point the README at them"

    command "benchmark" {
        description "Compare Fickl with upstream Fable on real workloads and draw the README charts"

        input {
            let! fickl = fickl
            and! version = version
            and! fableVersion = fableVersion
            and! runs = runs
            and! only = only
            and! skipWatch = skipWatch
            and! report = report
            and! updateReadme = updateReadme

            return
                stage "benchmark" {
                    run (fun _ ->
                        let version = version |> Option.defaultWith ficklVersion

                        let fableVersion =
                            if fableVersion = "latest" then
                                latestStable "fable"
                            else
                                fableVersion

                        let measureAll () =
                            let selected =
                                match only with
                                | None -> workloads
                                | Some names ->
                                    let names = names.Split ',' |> Array.map _.Trim() |> Set.ofArray
                                    workloads |> List.filter (fun w -> names.Contains w.Name)

                            Directory.CreateDirectory benchDir |> ignore
                            printfn $"Fickl {version} against Fable {fableVersion}"
                            let fable = installTool "fable" fableVersion "fable" None

                            let fickl =
                                fickl
                                |> Option.defaultWith (fun () ->
                                    // A release job benchmarks the packages it just built, before nuget.org indexes them
                                    let packed = Path.Combine(Paths.root, "temp", "release-" + version)

                                    installTool
                                        "Fickl"
                                        version
                                        "fickl"
                                        (Some packed |> Option.filter Directory.Exists)
                                )

                            let rows = ResizeArray()
                            let watchRows = ResizeArray()

                            for workload in selected do
                                let directory = checkout workload

                                let measure config run =
                                    rows.Add(compile fable fickl workload directory config run)
                                // The warm-up run fills the package cache the cache-hit rows read
                                for config in configs do
                                    measure config 0

                                for run in 1..runs do
                                    // Alternate the order so drift on the machine does not favour one compiler
                                    let order =
                                        if run % 2 = 1 then
                                            configs
                                        else
                                            List.rev configs

                                    for config in order do
                                        measure config run

                                if not skipWatch then
                                    for target in watchTargets |> List.filter (fun t -> t.Workload = workload.Name) do
                                        watchRows.AddRange(watch fable "fable" workload directory target runs)
                                        watchRows.AddRange(watch fickl "fickl" workload directory target runs)

                            selected, List.ofSeq rows, List.ofSeq watchRows

                        let selected, rows, watchRows =
                            match report with
                            | None -> measureAll ()
                            | Some folder -> readShards folder

                        if updateReadme && selected.Length <> workloads.Length then
                            failwith "--update-readme needs every workload; drop --only"

                        let tsv =
                            [
                                "name\tconfig\trun\twallMs\tparsedMs\tcompileMs\texitCode"
                                for row in rows do
                                    $"{row.Workload}\t{row.Config}\t{row.Run}\t{row.WallMs}\t{row.ParsedMs}\t{row.CompileMs}\t{row.ExitCode}"
                            ]
                            |> String.concat "\n"

                        let watchTsv =
                            [
                                "name\tcompiler\tedit\trun\tms"
                                for row in watchRows do
                                    $"{row.Workload}\t{row.Compiler}\t{row.Edit}\t{row.Run}\t{row.Ms}"
                            ]
                            |> String.concat "\n"

                        let seconds =
                            rows
                            |> Seq.filter (fun row -> row.Run > 0)
                            |> Seq.groupBy (fun row -> row.Workload, row.Config)
                            |> Seq.map (fun (key, rows) ->
                                key, rows |> Seq.map (fun row -> float row.WallMs / 1000.0) |> List.ofSeq |> median
                            )
                            |> Map.ofSeq

                        let watchMs =
                            watchRows
                            |> Seq.filter (fun row -> row.Run > 0)
                            |> Seq.groupBy (fun row -> row.Workload, row.Edit, row.Compiler)
                            |> Seq.map (fun (key, rows) ->
                                key, rows |> Seq.map (fun row -> float row.Ms) |> List.ofSeq |> median
                            )
                            |> Map.ofSeq

                        let fableName = $"Fable {fableVersion}"
                        let ficklName = $"Fickl {version}"

                        let machine =
                            $"{RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}"

                        let today = DateTime.UtcNow.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
                        let footer = $"{machine} · .NET {Environment.Version} · {today}"

                        let compilePanels =
                            selected
                            |> List.map (fun w ->
                                {
                                    Label = w.Label
                                    Bars = configs |> List.map (fun c -> c.Key, seconds[w.Name, c.Key])
                                    Ratio = seconds[w.Name, "fable"] / seconds[w.Name, "hit"]
                                }
                            )

                        let fastest = compilePanels |> List.map _.Ratio |> List.max

                        let svg =
                            chart
                                $"{ficklName} against {fableName}"
                                "End-to-end compilation time · fresh process · median of interleaved runs · lower is better"
                                [
                                    "fable", fableName
                                    "nopkg", $"{ficklName} · package cache off"
                                    "hit", $"{ficklName} · package cache hit"
                                ]
                                compilePanels
                                (fun s -> $"{oneDecimal s} s")
                                [
                                    $"*Cache-hit speedup versus {fableName} with default settings. All runs use --noCache; restores are included."
                                    $"Median of {runs} runs after a warm-up that fills the package cache. JS suite uses --exclude Fable.Core."
                                    footer
                                ]

                        let watchPanels =
                            [
                                for target in watchTargets do
                                    for edit, description in
                                        [
                                            "leaf", "comment in a leaf file"
                                            "root", "comment in an early file"
                                            "api", "new value in an early file"
                                        ] do
                                        match
                                            watchMs.TryFind(target.Workload, edit, "fable"),
                                            watchMs.TryFind(target.Workload, edit, "fickl")
                                        with
                                        | Some fable, Some fickl ->
                                            let workload = workloads |> List.find (fun w -> w.Name = target.Workload)

                                            {
                                                Label = workload.Label + "\n" + description
                                                Bars = [ "fable", fable; "hit", fickl ]
                                                Ratio = fable / fickl
                                            }
                                        | _ -> ()
                            ]

                        let watchSvg =
                            if watchPanels.IsEmpty then
                                None
                            else
                                chart
                                    $"{ficklName} against {fableName} in --watch"
                                    "From saving a file to the compiler reporting Watching again · median · lower is better"
                                    [ "fable", fableName; "hit", ficklName ]
                                    watchPanels
                                    (fun ms ->
                                        if ms >= 1000.0 then
                                            $"{oneDecimal (ms / 1000.0)} s"
                                        else
                                            $"{whole ms} ms"
                                    )
                                    [
                                        "*Speedup versus Fable. Each edit appends a comment; both compilers keep their default debounce."
                                        $"Median of {runs} edits per file after a warm-up edit, in one watch session per compiler."
                                        footer
                                    ]
                                |> Some

                        // --report measures nothing, so nothing else has created it on the CI job that draws the charts
                        Directory.CreateDirectory benchDir |> ignore
                        File.WriteAllText(Path.Combine(benchDir, "results.tsv"), tsv)
                        File.WriteAllText(Path.Combine(benchDir, "chart.svg"), svg)
                        File.WriteAllText(Path.Combine(benchDir, "watch.tsv"), watchTsv)

                        watchSvg
                        |> Option.iter (fun svg -> File.WriteAllText(Path.Combine(benchDir, "watch.svg"), svg))

                        printfn $"Charts and timings in {benchDir}; up to {oneDecimal fastest}× faster"

                        if updateReadme then
                            let notes = Path.Combine(Paths.root, "notes", "benchmarks")
                            File.WriteAllText(Path.Combine(notes, $"fickl-{version}.svg"), svg)
                            File.WriteAllText(Path.Combine(notes, $"fickl-{version}.tsv"), tsv)

                            let watchLines =
                                match watchSvg with
                                | None -> []
                                | Some watchSvg ->
                                    File.WriteAllText(Path.Combine(notes, $"fickl-{version}-watch.svg"), watchSvg)
                                    File.WriteAllText(Path.Combine(notes, $"fickl-{version}-watch.tsv"), watchTsv)

                                    let fastestWatch = watchPanels |> List.map _.Ratio |> List.max

                                    [
                                        ""
                                        $"![{ficklName} against {fableName} in watch mode: up to {oneDecimal fastestWatch}× faster to respond](notes/benchmarks/fickl-{version}-watch.svg)"
                                        ""
                                        $"Time from saving a file to `Watching` in one `--watch` session per compiler. Raw timings are in [notes/benchmarks/fickl-{version}-watch.tsv](notes/benchmarks/fickl-{version}-watch.tsv)."
                                    ]

                            let block =
                                [
                                    readmeStart
                                    $"![{ficklName} against {fableName}: up to {oneDecimal fastest}× faster end to end](notes/benchmarks/fickl-{version}.svg)"
                                    ""
                                    $"Wall clock of a fresh process with `--noCache`, median of {runs} interleaved runs on {RuntimeInformation.OSDescription}, measured by the release workflow. Raw timings are in [notes/benchmarks/fickl-{version}.tsv](notes/benchmarks/fickl-{version}.tsv)."
                                    yield! watchLines
                                    readmeEnd
                                ]
                                |> String.concat "\n"

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

                        0
                    )
                }
        }
    }
