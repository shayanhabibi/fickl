/// fickl: a compile server session, like watch mode, rechecks only the files an edit can affect: FCS reuses the
/// results of files whose dependencies kept their signature, and Fable recompiles only the dependents it records.
/// Each step here edits the project in `incremental` and compiles it again in one session; the output must be what
/// a fresh compilation of the same sources gives.
module Fable.Tests.IncrementalTests

open System.IO
open System.Text
open Expecto
open Microsoft.Extensions.Logging

/// Keeps what a compilation logs, to show it when the compilation does not do what the test expects
type private CapturingLogger() =
    let log = StringBuilder()

    member _.Take() =
        let text = log.ToString()
        log.Clear() |> ignore
        text

    interface ILogger with
        member _.BeginScope _ =
            { new System.IDisposable with
                member _.Dispose() = ()
            }

        member _.IsEnabled _ = true

        member _.Log(_level, _eventId, state, ex, formatter) =
            lock log (fun () -> log.AppendLine(formatter.Invoke(state, ex)) |> ignore)

type private Step =
    {
        Name: string
        /// File, text to replace (exactly once), replacement
        Edits: (string * string * string) list
        Compiles: bool
    }

let private step name edits =
    {
        Name = name
        Edits = edits
        Compiles = true
    }

let private steps =
    [
        step "inline body, signature unchanged" [ "Lib.fs", "let inline double x = x * 2", "let inline double x = x * 3" ]
        step "inline body used through another inline function" [ "Lib.fs", "let inline inc x = x + 1", "let inline inc x = x + 10" ]
        step "helper of an inline function" [ "Lib.fs", "s + \"!\"", "s + \"?\"" ]
        step
            "helper of an inline function renamed"
            [
                "Lib.fs", "let helper (s", "let helper2 (s"
                "Lib.fs", "= helper (s", "= helper2 (s"
            ]
        step "literal value" [ "Lib.fs", "let Greeting = \"hi\"", "let Greeting = \"hello\"" ]
        step "function body" [ "Lib.fs", "let plain x = x - 1", "let plain x = x - 2" ]
        step "mutable value initialiser" [ "Lib.fs", "let mutable counter = 0", "let mutable counter = 5" ]
        step
            "union case order"
            [ "Lib.fs", "| Circle of float\n    | Square of float", "| Square of float\n    | Circle of float" ]
        step "string enum compiled name" [ "Lib.fs", "CompiledName(\"dark-blue\")", "CompiledName(\"navy\")" ]
        step "default parameter value" [ "Lib.fs", "DefaultParameterValue(1)", "DefaultParameterValue(2)" ]
        step "Emit template" [ "Lib.fs", "Emit(\"($0 + 100)\")", "Emit(\"($0 + 200)\")" ]
        step "SRTP inline body" [ "Lib.fs", ": ^T = a + b", ": ^T = a + b + b" ]
        step "generalization" [ "Lib.fs", "let ident x = x", "let ident (x: int) = x" ]
        step "inline body behind a signature file" [ "Sig.fs", "(x: int) = x * 3", "(x: int) = x * 4" ]
        step "function body behind a signature file" [ "Sig.fs", "\"hey \" + s", "\"yo \" + s" ]
        step "inline member body" [ "Lib.fs", "this.Count <- this.Count + 1", "this.Count <- this.Count + 2" ]
        step
            "interface member order"
            [
                "Lib.fs",
                "abstract Area: float\n    abstract Name: string",
                "abstract Name: string\n    abstract Area: float"
            ]
        step
            "record field renamed in three files"
            [
                "Lib.fs", "{ X: int; Y: int }", "{ PX: int; Y: int }"
                "Mid.fs", "p.X p.Y", "p.PX p.Y"
                "Use.fs", "{ X = 1; Y = 2 }", "{ PX = 1; Y = 2 }"
            ]
        { step "type error" [ "Lib.fs", "let plain x = x - 2", "let plain x = x - \"2\"" ] with Compiles = false }
        step "type error fixed" [ "Lib.fs", "let plain x = x - \"2\"", "let plain x = x - 2" ]
        step "inline function made non-inline" [ "Lib.fs", "let inline double x = x * 3", "let double x = x * 3" ]
        step "function made inline again" [ "Lib.fs", "let double x = x * 3", "let inline double x = x * 4" ]
        step "AttachMembers added" [ "Lib.fs", "type Counter() =", "[<AttachMembers>]\ntype Counter() =" ]
        step "only the last file" [ "Use.fs", "let colors =", "let more = 1\n\nlet colors =" ]
        step "independent file" [ "Other.fs", "let unrelated () = 42", "let unrelated () = 43" ]
        step "comment only" [ "Lib.fs", "let plain x", "// a comment\nlet plain x" ]
        step "inline body using a new helper" [
            "Lib.fs", "let inline inc x = x + 10", "let bump x = x + 100\nlet inline inc x = bump x"
        ]
        // The compiler names the values of a let rec that is not all functions, and a top-level pattern
        step "let rec value body" [ "Lib.fs", "recursed <- 5", "recursed <- 6" ]
        step "let rec value body again" [ "Lib.fs", "recursed <- 6", "recursed <- 7" ]
        step "let rec value in another file" [ "Other.fs", "Lib.recursed + 1", "Lib.recursed + 2" ]
        step "top-level pattern" [ "Lib.fs", "let first, second = 1, 2", "let first, second = 3, 4" ]
    ]

let private outputs (outDir: string) =
    Directory.GetFiles(outDir, "*.js")
    |> Array.map (fun path -> Path.GetFileName path, File.ReadAllText(path).ReplaceLineEndings())
    |> Map.ofArray

let private copySources (fromDir: string) (toDir: string) =
    Directory.CreateDirectory toDir |> ignore

    for path in Directory.GetFiles fromDir do
        match Path.GetExtension path with
        | ".fs"
        | ".fsi"
        | ".fsproj" -> File.Copy(path, Path.Combine(toDir, Path.GetFileName path), true)
        | _ -> ()

let private applyEdit (dir: string) (file, oldText: string, newText: string) =
    let path = Path.Combine(dir, file)
    let content = File.ReadAllText(path).ReplaceLineEndings("\n")
    let index = content.IndexOf(oldText)
    Expect.isGreaterThanOrEqual index 0 $"%s{file} has no %A{oldText}"
    Expect.equal (content.IndexOf(oldText, index + 1)) -1 $"%s{file} has %A{oldText} more than once"
    File.WriteAllText(path, content.Remove(index, oldText.Length).Insert(index, newText))

let private args (dir: string) (outDir: string) (extra: string list) =
    [
        Path.Combine(dir, "incremental.fsproj")
        "--cwd"
        dir
        "--outDir"
        outDir
        "--exclude"
        "Fable.Core"
        yield! extra
    ]
    |> Array.ofList

/// The session is process-wide, so Main runs this after the parallel compilations rather than among them.
let tests =
    testCase "Incremental compilation matches a fresh one after each edit"
    <| fun () ->
        let root = Path.Combine(Path.GetTempPath(), "fickl-incremental-test")

        if Directory.Exists root then
            Directory.Delete(root, true)

        let sessionDir = Path.Combine(root, "session")
        let sessionOut = Path.Combine(sessionDir, "out")
        let freshDir = Path.Combine(root, "fresh")
        let freshOut = Path.Combine(freshDir, "out")
        copySources (Path.Combine(__SOURCE_DIRECTORY__, "incremental")) sessionDir

        let logger = CapturingLogger()

        let compileInSession () =
            Fable.Cli.Entry.serverRequest logger (args sessionDir sessionOut [ "--verbose" ])

        let compileFresh () =
            copySources sessionDir freshDir

            if Directory.Exists freshOut then
                Directory.Delete(freshOut, true)

            Fable.Cli.Entry.main (args freshDir freshOut [ "--noCache" ])

        Expect.equal (compileInSession ()) 0 $"The first compilation failed:{System.Environment.NewLine}{logger.Take()}"

        // Nothing changed, so a session that is reused writes nothing
        let written = Directory.GetFiles(sessionOut, "*.js") |> Array.map File.GetLastWriteTimeUtc
        Expect.equal (compileInSession ()) 0 "The compilation without changes failed"

        Expect.equal
            (Directory.GetFiles(sessionOut, "*.js") |> Array.map File.GetLastWriteTimeUtc)
            written
            "A compilation without changes rewrote output: the session was not reused"

        let failures = ResizeArray()

        for step in steps do
            List.iter (applyEdit sessionDir) step.Edits
            let sessionExit = compileInSession ()
            let sessionLog = logger.Take()
            let freshExit = compileFresh ()
            let expectedExit = if step.Compiles then 0 else 1

            if sessionExit <> expectedExit || freshExit <> expectedExit then
                failures.Add
                    $"%s{step.Name}: exit code %d{sessionExit} in the session and %d{freshExit} fresh{System.Environment.NewLine}%s{sessionLog}"
            elif step.Compiles then
                let session = outputs sessionOut
                let fresh = outputs freshOut

                for KeyValue(file, expected) in fresh do
                    match Map.tryFind file session with
                    | Some actual when actual = expected -> ()
                    | Some actual ->
                        failures.Add
                            $"%s{step.Name}: %s{file} differs{System.Environment.NewLine}%s{sessionLog}{System.Environment.NewLine}--- session ---{System.Environment.NewLine}%s{actual}{System.Environment.NewLine}--- fresh ---{System.Environment.NewLine}%s{expected}"
                    | None -> failures.Add $"%s{step.Name}: the session did not write %s{file}"

        if failures.Count > 0 then
            failtest (String.concat (System.Environment.NewLine + System.Environment.NewLine) failures)
