/// A long-lived Fickl process that compiles on behalf of short-lived CLI invocations, keeping what watch mode
/// keeps between rebuilds (the cracked project, FCS's per-file check results and Fable's compiled project), so a
/// later compilation of the same project only rechecks and recompiles what changed.
///
/// Opt in with `--server` or FICKL_SERVER=1. The client starts the server when none is running, and runs the
/// compilation in-process whenever the server cannot be reached. The server exits after FICKL_SERVER_IDLE_MINUTES
/// (default 20) without requests. It serves one request at a time and keeps the state of one project and set of
/// options: switching to another starts afresh, as the package cache keeps per-project state in the process.
module Fable.Cli.CompileServer

open System
open System.Collections.Generic
open System.IO
open System.IO.Pipes
open System.Security.Cryptography
open System.Text
open System.Text.Json
open Microsoft.Extensions.Logging
open Fable
open Fable.Compiler.Util
open Fable.Cli.Main

/// An error that has already been reported, so the caller exits with 1 without logging it again.
let alreadyReported = "\u0000fickl-already-reported"

type Request =
    {
        Cwd: string
        Args: string[]
        Env: Dictionary<string, string>
    }

let private utf8 = UTF8Encoding(false)

let private entryAssemblyPath () =
    Reflection.Assembly.GetEntryAssembly().Location

/// One server per user and Fickl build: a rebuilt compiler never talks to a server running the old one.
let pipeName () =
    let dll = entryAssemblyPath ()

    let identity =
        $"{Environment.UserName}|{dll}|{File.GetLastWriteTimeUtc(dll).Ticks}|{Fickl.Version.VERSION}"

    let hash = SHA256.HashData(utf8.GetBytes identity) |> Convert.ToHexString
    "fickl-" + hash.Substring(0, 16).ToLowerInvariant()

let private markerDir = Path.Combine(Path.GetTempPath(), "fickl-server")

let private markerPath name = Path.Combine(markerDir, name + ".pid")

/// The marker holds the server's process id and start time: a killed server leaves its marker behind, and its
/// process id can be reused by another process.
let private markerContents (p: Diagnostics.Process) =
    $"{p.Id} {p.StartTime.ToUniversalTime().Ticks}"

let private isServerRunning name =
    try
        let path = markerPath name

        File.Exists path
        && (let marker = File.ReadAllText(path).Trim()
            let pid = marker.Split(' ')[0] |> int

            try
                let p = Diagnostics.Process.GetProcessById(pid)
                not p.HasExited && markerContents p = marker
            with _ ->
                false)
    with _ ->
        false

// Wire format, one UTF-8 line per message: the client sends the request as JSON; the server answers with log
// lines, a level letter followed by the message as a JSON string, and finally "x" followed by the exit code.

let private levelLetter (level: LogLevel) =
    match level with
    | LogLevel.Error
    | LogLevel.Critical -> 'e'
    | LogLevel.Warning -> 'w'
    | _ -> 'i'

type private PipeLogger(send: char -> string -> unit) =
    interface ILogger with
        member _.BeginScope _ =
            { new IDisposable with
                member _.Dispose() = ()
            }

        member _.IsEnabled _ = true

        member _.Log(level, _eventId, state, ex, formatter) =
            send (levelLetter level) (formatter.Invoke(state, ex))

/// Forwards what the compiler writes straight to the console (rare: most output goes through Log) line by line.
type private PipeTextWriter(send: char -> string -> unit) =
    inherit TextWriter()
    let line = StringBuilder()
    override _.Encoding = utf8

    override _.Write(c: char) =
        if c = '\n' then
            send 'i' (line.ToString().TrimEnd('\r'))
            line.Clear() |> ignore
        else
            line.Append(c) |> ignore

    override this.Flush() =
        if line.Length > 0 then
            this.Write('\n')

// ----- Server -----

type private Session =
    {
        Key: string
        State: State
        /// Last write time (ticks) and length of every file the compilation depends on, when the compilation started.
        Snapshot: Map<string, int64 * int64>
    }

let mutable private session: Session option = None

/// The files a compilation of the project depends on: the files watch mode watches, plus the restore output,
/// which changes when package references do.
let private watchedFiles (projCracked: ProjectCracked) =
    [
        projCracked.ProjectFile
        IO.Path.Combine(IO.Path.GetDirectoryName(projCracked.ProjectFile), "obj", "project.assets.json")
        yield! projCracked.References
        yield!
            projCracked.SourceFiles
            |> Array.choose (fun f ->
                let path = f.NormalizedFullPath

                if Naming.isInFableModules path then
                    None
                else
                    Some path
            )
    ]
    |> List.distinct

/// A file written while a compilation runs may not be in its output, so it gets a time that never matches.
let private takeSnapshot (startedAt: DateTime) (files: string list) =
    files
    |> List.map (fun path ->
        let info = FileInfo(path)

        let stamp =
            if not info.Exists then
                0L, -1L
            elif info.LastWriteTimeUtc >= startedAt then
                -1L, info.Length
            else
                info.LastWriteTimeUtc.Ticks, info.Length

        path, stamp
    )
    |> Map.ofList

let private changedFiles (snapshot: Map<string, int64 * int64>) (current: Map<string, int64 * int64>) =
    let changes = HashSet<string>()

    for KeyValue(path, stamp) in current do
        match Map.tryFind path snapshot with
        | Some old when old = stamp && fst stamp <> -1L -> ()
        | _ -> changes.Add(path) |> ignore

    changes

/// The compilation of a server request: reuses the session when it is for the same project and options and
/// nothing forces a fresh start, otherwise compiles as a new process would.
let compile (cliArgs: CliArgs) : Result<unit, string> =
    let startedAt = DateTime.UtcNow
    ServerMode.collectsDependencies <- true
    // Verbosity only changes what is printed, so it does not need a fresh start; Fickl's and Fable's
    // environment variables can change the compilation (the server's own settings cannot)
    let key =
        let env =
            Environment.GetEnvironmentVariables()
            |> Seq.cast<Collections.DictionaryEntry>
            |> Seq.map (fun e -> string e.Key, string e.Value)
            |> Seq.filter (fun (k, _) ->
                (k.StartsWith("FICKL_", StringComparison.OrdinalIgnoreCase)
                 || k.StartsWith("FABLE_", StringComparison.OrdinalIgnoreCase))
                && not (k.StartsWith("FICKL_SERVER", StringComparison.OrdinalIgnoreCase))
            )
            |> Seq.sort
            |> Seq.toList

        sprintf "%A|%A" { cliArgs with Verbosity = Verbosity.Normal } env

    let reusable =
        match session with
        | Some s when s.Key = key && not cliArgs.NoCache ->
            match s.State.ProjectCrackedAndFableCompiler with
            // Deleting fable_modules (or the output directory holding it) also deletes the package outputs the
            // cached package sources compile to, which only cracking the project again restores
            | Some(projCracked, _) when Directory.Exists projCracked.FableModulesDir -> Some(s, projCracked)
            | _ -> None
        | _ -> None

    let reusable =
        reusable
        |> Option.bind (fun (s, projCracked) ->
            let changes =
                takeSnapshot startedAt (watchedFiles projCracked) |> changedFiles s.Snapshot

            let sources = HashSet(projCracked.SourceFilePaths)

            // Project file, restore output or reference changes: crack the project again, as a new process would
            // (watch mode's re-evaluation skips the package cache)
            if changes |> Seq.forall sources.Contains then
                Some(s, changes)
            else
                None
        )

    let state, changes =
        match reusable with
        | Some(s, changes) ->
            Log.verbose (lazy $"Compile server: reusing the session, {changes.Count} changed files")
            s.State, changes :> ISet<_>
        | None ->
            if session.IsSome then
                Log.verbose (lazy "Compile server: starting a new session")
                session <- None
                GC.Collect()

            State.Create(cliArgs), HashSet<string>() :> ISet<_>

    if changes.Count = 0 && reusable.IsSome then
        Log.always "No source changes since the last compilation (compile server)"

    // Generated Python names count from a process-wide index, which a new process starts at 0
    Fable.Py.Naming.resetUniqueIndex ()

    let exitCode, newState =
        try
            compileInServer state changes |> Async.RunSynchronously
        with _ ->
            // Whatever failed may have left the state half updated
            session <- None
            reraise ()

    session <-
        newState
        |> Option.bind (fun state ->
            state.ProjectCrackedAndFableCompiler
            |> Option.map (fun (projCracked, _) ->
                {
                    Key = key
                    State = state
                    Snapshot = takeSnapshot startedAt (watchedFiles projCracked)
                }
            )
        )

    if exitCode = 0 then
        Ok()
    else
        Error "Compilation failed"

let private idleTimeout () =
    match
        Environment.GetEnvironmentVariable "FICKL_SERVER_IDLE_MINUTES"
        |> Double.TryParse
    with
    | true, minutes when minutes > 0. -> TimeSpan.FromMinutes minutes
    | _ -> TimeSpan.FromMinutes 20.

let private applyEnvironment (env: Dictionary<string, string>) =
    let current = Environment.GetEnvironmentVariables()

    for key in current.Keys |> Seq.cast<string> |> Seq.toArray do
        if not (env.ContainsKey key) then
            Environment.SetEnvironmentVariable(key, null)

    for KeyValue(key, value) in env do
        if Environment.GetEnvironmentVariable key <> value then
            Environment.SetEnvironmentVariable(key, value)

let mutable private requestLogger: ILogger = Abstractions.NullLogger.Instance

/// Sets the verbosity of a request's log, which goes to the client that sent it.
let setRequestVerbosity verbosity = Log.setLogger verbosity requestLogger

/// Sends the log of requests handled in this process to `logger` (tests run requests without a client).
let setRequestLogger logger = requestLogger <- logger

/// Runs the server until it has been idle for the idle timeout. `handle` compiles a request, logging through
/// Log (see setRequestVerbosity), and returns the exit code.
let serve (name: string) (handle: Request -> int) =
    let createPipe () =
        new NamedPipeServerStream(
            name,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous ||| PipeOptions.CurrentUserOnly
        )

    // Two clients can start a server at the same time: the one that does not get the pipe leaves
    let firstPipe =
        try
            Some(createPipe ())
        with :? IOException ->
            None

    if firstPipe.IsNone then
        ()
    else

        ServerMode.isServer <- true
        let marker = markerContents (Diagnostics.Process.GetCurrentProcess())
        Directory.CreateDirectory(markerDir) |> ignore
        File.WriteAllText(markerPath name, marker)
        // Ctrl+C in the terminal that started the first client must not stop the server
        Console.CancelKeyPress.Add(fun e -> e.Cancel <- true)
        // Nobody reads the server's own console; requests get their output through the pipe
        Console.SetOut TextWriter.Null
        Console.SetError TextWriter.Null
        let timeout = idleTimeout ()
        let mutable running = true
        let mutable nextPipe = firstPipe

        try
            while running do
                use pipe =
                    match nextPipe with
                    | Some pipe ->
                        nextPipe <- None
                        pipe
                    | None -> createPipe ()

                let connected =
                    try
                        pipe.WaitForConnectionAsync().Wait(timeout)
                    with _ ->
                        false

                if not connected then
                    running <- false
                else
                    try
                        use reader = new StreamReader(pipe, utf8, false, 4096, true)
                        use writer = new StreamWriter(pipe, utf8, 4096, true)
                        writer.AutoFlush <- true
                        let request = reader.ReadLine() |> JsonSerializer.Deserialize<Request>
                        let gate = obj ()

                        let send (letter: char) (msg: string) =
                            lock gate (fun () -> writer.WriteLine(string letter + JsonSerializer.Serialize msg))

                        let stdout, stderr = Console.Out, Console.Error
                        let consoleWriter = new PipeTextWriter(send)

                        let exitCode =
                            try
                                try
                                    applyEnvironment request.Env
                                    Directory.SetCurrentDirectory request.Cwd
                                    Console.SetOut consoleWriter
                                    Console.SetError consoleWriter
                                    requestLogger <- PipeLogger(send)
                                    setRequestVerbosity Verbosity.Normal
                                    handle request
                                with ex ->
                                    send 'e' (ex.Message + Log.newLine + ex.StackTrace)
                                    1
                            finally
                                consoleWriter.Flush()
                                Console.SetOut stdout
                                Console.SetError stderr

                        send 'x' (string exitCode)
                    with _ ->
                        // The client went away; the next request still finds the session
                        ()
        finally
            try
                if File.ReadAllText(markerPath name).Trim() = marker then
                    File.Delete(markerPath name)
            with _ ->
                ()

// ----- Client -----

/// Whether to compile through a server: asked for with `--server` or FICKL_SERVER=1, never with FICKL_SERVER=0.
let isRequested (serverFlag: bool) =
    match Environment.GetEnvironmentVariable "FICKL_SERVER" with
    | "0" -> false
    | "1" -> true
    | _ -> serverFlag

let private startServer (name: string) =
    let processPath = Environment.ProcessPath
    let dll = entryAssemblyPath ()

    // `dotnet fable.dll` runs the dll through the host; an apphost or tool shim runs itself
    let fileName, args =
        match Path.GetFileNameWithoutExtension(processPath).ToLowerInvariant() with
        | "dotnet" -> processPath, [ dll; "--compile-server"; name ]
        | _ -> processPath, [ "--compile-server"; name ]

    // The server must not hold the client's console handles: whatever waits for the client's output (a shell
    // capturing it, a build tool) would otherwise wait for the server to exit too
    if OperatingSystem.IsWindows() then
        // Shell execution starts the process without inheriting handles
        let quote (arg: string) = "\"" + arg + "\""

        let psi =
            Diagnostics.ProcessStartInfo(fileName, args |> List.map quote |> String.concat " ")

        psi.UseShellExecute <- true
        psi.WindowStyle <- Diagnostics.ProcessWindowStyle.Hidden
        psi.WorkingDirectory <- Path.GetTempPath()
        Diagnostics.Process.Start(psi) |> ignore
    else
        // Elsewhere a child process only inherits the standard streams, which are replaced here
        let psi = Diagnostics.ProcessStartInfo(fileName, args)
        psi.UseShellExecute <- false
        psi.RedirectStandardInput <- true
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.WorkingDirectory <- Path.GetTempPath()
        let p = Diagnostics.Process.Start(psi)
        p.StandardInput.Close()
        p.StandardOutput.Close()
        p.StandardError.Close()

let private connect (name: string) =
    let tryConnect (timeoutMs: int) =
        let pipe =
            new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.CurrentUserOnly)

        try
            pipe.Connect(timeoutMs)
            Some pipe
        with _ ->
            pipe.Dispose()
            None

    match tryConnect 200 with
    | Some pipe -> Some pipe
    | None when isServerRunning name ->
        // Busy with another request: requests are served in turn
        tryConnect (int (TimeSpan.FromMinutes 10.).TotalMilliseconds)
    | None ->
        startServer name
        tryConnect 20_000

/// Compiles through the server. None when the server could not be reached or stopped before answering, in
/// which case the caller compiles in-process.
let tryCompile (argv: string[]) : int option =
    try
        let name = pipeName ()

        match connect name with
        | None ->
            Log.verbose (lazy "Compile server not reachable, compiling in this process")
            None
        | Some pipe ->
            use pipe = pipe
            use reader = new StreamReader(pipe, utf8, false, 4096, true)
            use writer = new StreamWriter(pipe, utf8, 4096, true)

            let env = Dictionary<string, string>()

            for entry in Environment.GetEnvironmentVariables() |> Seq.cast<Collections.DictionaryEntry> do
                env[string entry.Key] <- string entry.Value

            let request =
                {
                    Cwd = Directory.GetCurrentDirectory()
                    Args = argv
                    Env = env
                }

            writer.WriteLine(JsonSerializer.Serialize request)
            writer.Flush()

            let rec read () =
                match reader.ReadLine() with
                | null -> None
                | line when line.Length > 0 && line[0] = 'x' -> Some(int (JsonSerializer.Deserialize<string> line[1..]))
                | line when line.Length > 0 ->
                    let msg = JsonSerializer.Deserialize<string> line[1..]

                    match line[0] with
                    | 'e' -> Log.error msg
                    | 'w' -> Log.warning msg
                    | _ -> Log.always msg

                    read ()
                | _ -> read ()

            read ()
    with ex ->
        Log.verbose (lazy $"Compile server failed ({ex.Message}), compiling in this process")
        None
