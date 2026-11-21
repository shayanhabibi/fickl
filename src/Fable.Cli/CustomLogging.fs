// Source: https://github.com/ionide/FSharp.Analyzers.SDK/blob/main/src/FSharp.Analyzers.Cli/CustomLogging.fs
module Fable.Cli.CustomLogging

open System
open System.IO
open System.Runtime.CompilerServices
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Console
open Microsoft.Extensions.Logging.Abstractions
open Microsoft.Extensions.Options

/// fickl: whether log messages are coloured. `NO_COLOR` set to anything but an empty string turns colour off
/// (https://no-color.org). Redirected output, a pipe or a file, is coloured only when
/// `DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION` is `1` or `true`, the switch .NET's own console logging reads.
let colorEnabledFrom (getEnv: string -> string) (outputRedirected: bool) =
    if not (String.IsNullOrEmpty(getEnv "NO_COLOR")) then
        false
    elif outputRedirected then
        match getEnv "DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION" with
        | null -> false
        | v -> v = "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)
    else
        true

/// `colorEnabledFrom` over this process's environment and standard output.
let colorEnabled () =
    colorEnabledFrom Environment.GetEnvironmentVariable Console.IsOutputRedirected

type CustomOptions() =
    inherit ConsoleFormatterOptions()

    /// if true: no LogLevel as prefix, output coloured according to LogLevel when `UseColor` is true
    /// if false: LogLevel as prefix, no colored output
    member val UseNoPrefixMsgStyle = false with get, set

    /// fickl: whether the no-prefix style colours messages. The CLI sets it from `colorEnabled`.
    member val UseColor = true with get, set

type CustomFormatter(options: IOptionsMonitor<CustomOptions>) as this =
    inherit ConsoleFormatter("customName")

    let mutable optionsReloadToken: IDisposable = null
    let mutable formatterOptions = options.CurrentValue
    do optionsReloadToken <- options.OnChange(fun x -> this.ReloadLoggerOptions(x))

    member private _.ReloadLoggerOptions(opts: CustomOptions) = formatterOptions <- opts

    override this.Write<'TState>
        (logEntry: inref<LogEntry<'TState>>, _scopeProvider: IExternalScopeProvider, textWriter: TextWriter)
        =
        let message = logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception)

        if formatterOptions.UseNoPrefixMsgStyle then
            if formatterOptions.UseColor then
                // fickl: the reset goes before the closing newline, which would otherwise start a line of its own
                this.SetColor(textWriter, logEntry.LogLevel)
                textWriter.Write(message)
                this.ResetColor(textWriter)
                textWriter.WriteLine()
            else
                textWriter.WriteLine(message)
        else
            this.WritePrefix(textWriter, logEntry.LogLevel)
            textWriter.WriteLine(message)

    member private _.WritePrefix(textWriter: TextWriter, logLevel: LogLevel) =
        match logLevel with
        | LogLevel.Trace -> textWriter.Write("trace: ")
        | LogLevel.Debug -> textWriter.Write("debug: ")
        | LogLevel.Information -> textWriter.Write("info: ")
        | LogLevel.Warning -> textWriter.Write("warn: ")
        | LogLevel.Error -> textWriter.Write("error: ")
        | LogLevel.Critical -> textWriter.Write("critical: ")
        | _ -> ()

    // see https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter
    member private _.SetColor(textWriter: TextWriter, logLevel: LogLevel) =

        let color =
            match logLevel with
            | LogLevel.Error -> "\x1B[31m" // ConsoleColor.DarkRed
            | LogLevel.Warning -> "\x1B[33m" // ConsoleColor.DarkYellow
            | LogLevel.Information -> "\x1B[37m" // ConsoleColor.Gray
            | LogLevel.Trace -> "\x1B[1m\x1B[36m" // ConsoleColor.Cyan
            | _ -> "\x1B[39m\x1B[22m" // default foreground color

        textWriter.Write(color)

    member private _.ResetColor(textWriter: TextWriter) = textWriter.Write("\x1B[39m\x1B[22m") // reset to default foreground color

    interface IDisposable with
        member _.Dispose() = optionsReloadToken.Dispose()

[<Extension>]
type ConsoleLoggerExtensions =

    [<Extension>]
    static member AddCustomConsole(builder: ILoggingBuilder, configure: Action<CustomOptions>) : ILoggingBuilder =
        builder
            .AddConsole(fun options -> options.FormatterName <- "customName")
            .AddConsoleFormatter<CustomFormatter, CustomOptions>(configure)
