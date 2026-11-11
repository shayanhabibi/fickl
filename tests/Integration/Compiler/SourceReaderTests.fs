module Fable.Tests.Compiler.SourceReader

open System
open System.IO
open System.Threading.Tasks
open Util.Testing

/// Holds the file open with no sharing, the way an editor does while it saves, for the given time.
let private lockFor (path: string) (milliseconds: int) =
    let stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)

    Task.Run(fun () ->
        Task.Delay(milliseconds).Wait()
        stream.Dispose()
    )

let private withSourceFile (test: string -> unit) =
    let path = Path.Combine(Path.GetTempPath(), $"SourceReaderTests-{Guid.NewGuid()}.fs")
    File.WriteAllText(path, "module Locked")

    try
        test path
    finally
        File.Delete path

let tests =
  testList "Source reader" [
    testCase "A source file that is locked for a moment is still read" <| fun _ ->
      withSourceFile (fun path ->
        let unlocked = lockFor path 500
        let _, source = Fable.Compiler.File(path).ReadSource()

        source.Value |> equal "module Locked"
        unlocked.Wait()
      )

    testCase "A source file that stays locked fails with an IOException" <| fun _ ->
      withSourceFile (fun path ->
        let unlocked = lockFor path 4000

        let failed =
          try
            Fable.Compiler.File(path).ReadSource() |> ignore
            false
          with :? IOException ->
            true

        unlocked.Wait()
        failed |> equal true
      )
  ]
