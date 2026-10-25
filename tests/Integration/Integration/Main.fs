module Fable.Tests.Main
open System

let allTests =
  [
    Cli.tests
    FileWatcher.tests
    CompilationTests.tests
  ]

open Expecto

[<EntryPoint>]
let main args =
    match testList "All" allTests |> runTestsWithCLIArgs [] args with
    // fickl: `--declaration` is a process-wide switch, so its test must not run alongside other compilations
    | 0 -> CompilationTests.declarationTests |> runTestsWithCLIArgs [] args
    | exitCode -> exitCode
