module Fable.Tests.Cli

open System
open System.IO
open Fable.Cli.Entry
open Fable.Cli.CustomLogging
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Microsoft.Extensions.Options
open Expecto

type Result<'T1, 'T2> with
    member this.Value = match this with Ok v -> v | Error _ -> failwith "I'm Error!"

/// Shows each byte that is not printable ASCII as `\xNN`, so a failure names every escape and line break
let private showBytes (bytes: byte array) =
    bytes
    |> Array.map (fun b -> if b >= 32uy && b < 127uy && b <> byte '\\' then string (char b) else $"\\x%02X{b}")
    |> String.concat ""

/// Runs the CLI with `--version` in a child process and returns the exact bytes it writes to stdout
let private versionOutput () =
    let info =
        Diagnostics.ProcessStartInfo(
            "dotnet",
            [ Path.Combine(AppContext.BaseDirectory, "fable.dll"); "--version" ],
            RedirectStandardOutput = true,
            RedirectStandardError = true
        )
    // The console logger writes colour codes into a pipe on Linux and macOS. On Windows it does so only with this
    // variable, which makes the child behave here as it does on those platforms
    info.Environment["DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION"] <- "1"
    info.Environment.Remove "NO_COLOR" |> ignore
    use child = Diagnostics.Process.Start info
    let stderr = child.StandardError.ReadToEndAsync()
    use stdout = new MemoryStream()
    child.StandardOutput.BaseStream.CopyTo stdout
    child.WaitForExit()
    Expect.equal child.ExitCode 0 $"--version failed: {stderr.Result}"
    stdout.ToArray()

/// Formats one Information entry as the CLI's logger does and returns the text it writes
let private formatEntry (options: CustomOptions) (message: string) =
    let monitor =
        { new IOptionsMonitor<CustomOptions> with
            member _.CurrentValue = options
            member _.Get _ = options
            member _.OnChange _ = { new IDisposable with member _.Dispose() = () } }
    use formatter = new CustomFormatter(monitor)
    use writer = new StringWriter(NewLine = "\n")
    let entry =
        LogEntry<string>(LogLevel.Information, "", EventId 0, message, null, Func<string, exn, string>(fun s _ -> s))
    formatter.Write(&entry, null, writer)
    showBytes (Text.Encoding.UTF8.GetBytes(writer.ToString()))

let tests =
  testList "Cli" [

    testCase "Can use --outdir in lower case" <| fun () ->
        let res = parseCliArgs ["--outDir"; "foo"]
        Expect.isOk res "--outDir args"
        Expect.equal (res.Value.Value("--outDir")) (Some "foo") "--outDir value"

        let res = parseCliArgs ["--outdir"; "foo"]
        Expect.isOk res "--outdir args"
        Expect.equal (res.Value.Value("--outDir")) (Some "foo") "--outdir value"

    // A script reads the version with `$(dotnet fickl --version)` and writes it to a line-based file such as
    // $GITHUB_ENV, so a colour code before it or after its newline corrupts the value or adds a line
    testCase "--version prints only the version and a newline" <| fun () ->
        let expected = Text.Encoding.ASCII.GetBytes(Fable.Literals.VERSION + Environment.NewLine)
        Expect.equal (showBytes (versionOutput ())) (showBytes expected) "--version stdout bytes"

    // A reader that splits the output into lines otherwise gets a line holding only the reset
    testCase "A coloured log line resets its colour before the newline" <| fun () ->
        let options = CustomOptions(UseNoPrefixMsgStyle = true, UseColor = true)
        Expect.equal (formatEntry options "hello") @"\x1B[37mhello\x1B[39m\x1B[22m\x0A" "formatted line"

    testCase "A log line without colour is the message and a newline" <| fun () ->
        let options = CustomOptions(UseNoPrefixMsgStyle = true, UseColor = false)
        Expect.equal (formatEntry options "hello") @"hello\x0A" "formatted line"

    testCase "Log colour is off when output is redirected or NO_COLOR is set" <| fun () ->
        let enabled (env: (string * string) list) redirected =
            let env = Map.ofList env
            colorEnabledFrom (fun name -> Map.tryFind name env |> Option.toObj) redirected

        let allow = "DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION"
        Expect.isTrue (enabled [] false) "a terminal"
        Expect.isFalse (enabled [ "NO_COLOR", "1" ] false) "NO_COLOR in a terminal"
        Expect.isTrue (enabled [ "NO_COLOR", "" ] false) "an empty NO_COLOR"
        Expect.isFalse (enabled [] true) "redirected"
        Expect.isTrue (enabled [ allow, "1" ] true) $"redirected with {allow}=1"
        Expect.isTrue (enabled [ allow, "True" ] true) $"redirected with {allow}=True"
        Expect.isFalse (enabled [ allow, "0" ] true) $"redirected with {allow}=0"
        Expect.isFalse (enabled [ allow, "1"; "NO_COLOR", "1" ] true) "NO_COLOR wins"

    testCase "Cannot use --outir" <| fun () ->
        let res = parseCliArgs ["--outir"; "foo"]
        Expect.isError res "--outir args"

    testCase "--packageCache is accepted" <| fun () ->
        Expect.isOk (parseCliArgs ["--packageCache"]) "--packageCache args"

    testCase "The package cache is off by default in CI" <| fun () ->
        let resolve (env: (string * string) list) noFlag onFlag =
            let env = Map.ofList env
            Fable.Cli.PackageCache.resolveEnabledFrom (fun name -> Map.tryFind name env |> Option.toObj) noFlag onFlag

        Expect.isTrue (resolve [] false false) "outside CI"
        Expect.isFalse (resolve [ "CI", "true" ] false false) "CI=true"
        Expect.isFalse (resolve [ "TF_BUILD", "True" ] false false) "Azure Pipelines"
        Expect.isFalse (resolve [ "JENKINS_URL", "http://ci" ] false false) "Jenkins"
        Expect.isTrue (resolve [ "CI", "false" ] false false) "CI=false"
        Expect.isTrue (resolve [ "CI", "0" ] false false) "CI=0"
        Expect.isTrue (resolve [ "CI", "true" ] false true) "--packageCache in CI"
        Expect.isTrue (resolve [ "CI", "true"; "FICKL_USE_PACKAGE_CACHE", "1" ] false false) "FICKL_USE_PACKAGE_CACHE=1 in CI"
        Expect.isFalse (resolve [ "FICKL_USE_PACKAGE_CACHE", "false" ] false false) "FICKL_USE_PACKAGE_CACHE=false"
        Expect.isFalse (resolve [ "FICKL_USE_PACKAGE_CACHE", "1" ] true false) "--noPackageCache wins"
  ]