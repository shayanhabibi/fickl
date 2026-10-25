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
    // fickl: `--declaration` and the compile server session are process-wide, so their tests must not run
    // alongside other compilations
    | 0 ->
        testList "Sequential" [ CompilationTests.declarationTests; IncrementalTests.tests ]
        |> testSequenced
        |> runTestsWithCLIArgs [] args
    | exitCode -> exitCode
