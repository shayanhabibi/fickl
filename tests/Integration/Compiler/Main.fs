module Fable.Tests.Compiler.Main

open Fable.Tests.Compiler
open Expecto

let allTests =
    [
        CompilerMessages.tests
        CodeServicesApi.tests
        IwsamWarnings.tests

        WarningSuppression.tests
        IgnoredArgument.tests
        BehaviorWarning.tests
        AnonRecordInInterface.tests
        CompilerHelpers.tests
        Inflate.tests
        WatchDependency.tests
        RegexOptions.tests
        UnsupportedBclMember.tests
        SourceReader.tests
    ]


[<EntryPoint>]
let main args =
    let config = [ Sequenced ]

    allTests
    |> testList "All"
    |> runTestsWithCLIArgs config args
