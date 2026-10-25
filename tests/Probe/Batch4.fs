module Batch4

open System
open Check

type Point = { X: int; Y: int }
type Person = { Name: string; Age: int; Tags: string list; Home: Point option }

type Shape =
    | Dot
    | Circle of radius: float
    | Rect of width: int * height: int
    | Labeled of string * Shape

type Wrapper = Wrapper of int
type Big = { A: int; B: string; C: float; D: bool; E: char; F: int list; G: Point }

let flat (s: string) = s.Replace("\r", "").Replace("\n", "\\n")

let run () =
    check "pa.int" (fun () -> sprintf "%A" 42)
    check "pa.negint" (fun () -> sprintf "%A" -7)
    check "pa.float" (fun () -> sprintf "%A" 1.5)
    check "pa.float.int" (fun () -> sprintf "%A" 2.0)
    check "pa.float.nan" (fun () -> sprintf "%A" nan)
    check "pa.float32" (fun () -> sprintf "%A" 1.5f)
    check "pa.decimal" (fun () -> sprintf "%A" 1.50M)
    check "pa.int64" (fun () -> sprintf "%A" 5L)
    check "pa.byte" (fun () -> sprintf "%A" 7uy)
    check "pa.bigint" (fun () -> sprintf "%A" 12I)
    check "pa.bool" (fun () -> sprintf "%A" true)
    check "pa.char" (fun () -> sprintf "%A" 'c')
    check "pa.string" (fun () -> sprintf "%A" "hi")
    check "pa.string.escape" (fun () -> sprintf "%A" "a\"b\n")
    check "pa.unit" (fun () -> sprintf "%A" ())
    check "pa.null" (fun () -> sprintf "%A" (null: string))
    check "pa.tuple" (fun () -> sprintf "%A" (1, "a", 2.5))
    check "pa.struct.tuple" (fun () -> sprintf "%A" (struct (1, "a")))
    check "pa.list" (fun () -> sprintf "%A" [ 1; 2; 3 ])
    check "pa.list.empty" (fun () -> sprintf "%A" ([]: int list))
    check "pa.list.strings" (fun () -> sprintf "%A" [ "a"; "b" ])
    check "pa.array" (fun () -> sprintf "%A" [| 1; 2 |])
    check "pa.array.empty" (fun () -> sprintf "%A" ([||]: int[]))
    check "pa.seq" (fun () -> sprintf "%A" (seq { 1..3 }))
    check "pa.seq.long" (fun () -> flat (sprintf "%A" (seq { 1..10 })))
    check "pa.list.long" (fun () -> flat (sprintf "%A" [ 1..120 ]))
    check "pa.option.some" (fun () -> sprintf "%A" (Some 1))
    check "pa.option.none" (fun () -> sprintf "%A" (None: int option))
    check "pa.option.nested" (fun () -> sprintf "%A" (Some(Some "x")))
    check "pa.voption" (fun () -> sprintf "%A" (ValueSome 3))
    check "pa.result" (fun () -> sprintf "%A" (Ok 1: Result<int, string>) + "|" + sprintf "%A" (Error "e": Result<int, string>))
    check "pa.union.nofields" (fun () -> sprintf "%A" Dot)
    check "pa.union.field" (fun () -> sprintf "%A" (Circle 1.5))
    check "pa.union.fields" (fun () -> sprintf "%A" (Rect(2, 3)))
    check "pa.union.nested" (fun () -> sprintf "%A" (Labeled("x", Circle 1.0)))
    check "pa.union.single" (fun () -> sprintf "%A" (Wrapper 5))
    check "pa.union.list" (fun () -> sprintf "%A" [ Dot; Circle 2.0 ])
    check "pa.record" (fun () -> flat (sprintf "%A" { X = 1; Y = 2 }))
    check "pa.record.nested" (fun () -> flat (sprintf "%A" { Name = "Ann"; Age = 30; Tags = [ "a" ]; Home = Some { X = 1; Y = 2 } }))
    check "pa.record.list" (fun () -> flat (sprintf "%A" [ { X = 1; Y = 2 }; { X = 3; Y = 4 } ]))
    check "pa.record.big" (fun () -> flat (sprintf "%A" { A = 1; B = "b"; C = 1.5; D = true; E = 'e'; F = [ 1 ]; G = { X = 0; Y = 0 } }))
    check "pa.anon" (fun () -> flat (sprintf "%A" {| A = 1; B = "x" |}))
    check "pa.map" (fun () -> flat (sprintf "%A" (Map.ofList [ 1, "a"; 2, "b" ])))
    check "pa.set" (fun () -> sprintf "%A" (set [ 3; 1; 2 ]))
    check "pa.dict" (fun () -> let d = Collections.Generic.Dictionary<int, string>() in d.[1] <- "a"; flat (sprintf "%A" d))
    check "pa.resizearray" (fun () -> sprintf "%A" (ResizeArray [ 1; 2 ]))
    check "pa.list.options" (fun () -> sprintf "%A" [ Some 1; None ])
    check "pa.list.tuples" (fun () -> sprintf "%A" [ (1, "a"); (2, "b") ])
    check "pa.array2" (fun () -> sprintf "%A" [| [| 1 |]; [| 2; 3 |] |])
    check "pa.func" (fun () -> string ((sprintf "%A" (fun (x: int) -> x)).StartsWith "<fun"))
    check "pa.exn" (fun () -> string ((sprintf "%A" (Failure "boom")).Contains "boom"))
    check "pa.datetime" (fun () -> sprintf "%A" (DateTime(2024, 1, 2, 3, 4, 5)))
    check "pa.timespan" (fun () -> sprintf "%A" (TimeSpan.FromHours 1.5))
    check "pa.guid" (fun () -> sprintf "%A" (Guid.Parse "00000000-0000-0000-0000-000000000001"))
    check "pa.enum" (fun () -> sprintf "%A" DayOfWeek.Monday)
    check "pa.padded" (fun () -> sprintf "[%10A]" 42)
    check "pa.O.record" (fun () -> flat (sprintf "%O" { X = 1; Y = 2 }))
    check "pa.O.union" (fun () -> sprintf "%O" (Rect(2, 3)))
    check "pa.string.record" (fun () -> flat (string { X = 1; Y = 2 }))
    check "pa.string.union" (fun () -> string (Labeled("x", Dot)))
    check "pa.string.list.records" (fun () -> flat (string [ { X = 1; Y = 2 } ]))
    check "pa.string.map" (fun () -> flat (string (Map.ofList [ 1, "a" ])))
    check "pa.string.set" (fun () -> string (set [ 2; 1 ]))
    check "pa.interp.A" (fun () -> let r = { X = 1; Y = 2 } in flat $"%A{r}")
    check "pa.printfn.list" (fun () -> sprintf "%A" [ [ 1; 2 ]; [ 3 ] ])
