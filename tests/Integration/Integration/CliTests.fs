module Fable.Tests.Cli

open Fable.Cli.Entry
open Expecto

type Result<'T1, 'T2> with
    member this.Value = match this with Ok v -> v | Error _ -> failwith "I'm Error!"

let tests =
  testList "Cli" [

    testCase "Can use --outdir in lower case" <| fun () ->
        let res = parseCliArgs ["--outDir"; "foo"]
        Expect.isOk res "--outDir args"
        Expect.equal (res.Value.Value("--outDir")) (Some "foo") "--outDir value"

        let res = parseCliArgs ["--outdir"; "foo"]
        Expect.isOk res "--outdir args"
        Expect.equal (res.Value.Value("--outDir")) (Some "foo") "--outdir value"

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