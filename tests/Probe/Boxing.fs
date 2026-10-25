module Boxing

open System
open Check

type R = { A: int }

type I =
    abstract X: int

type C() =
    interface I with
        member _.X = 1

let describe (o: obj) =
    match o with
    | :? int -> "int"
    | :? float -> "float"
    | :? int64 -> "int64"
    | :? byte -> "byte"
    | :? decimal -> "decimal"
    | :? char -> "char"
    | :? string -> "string"
    | :? bool -> "bool"
    | :? (int list) -> "int list"
    | :? (string list) -> "string list"
    | :? (int[]) -> "int[]"
    | :? (float[]) -> "float[]"
    | :? R -> "R"
    | :? I -> "I"
    | :? (int option) -> "int option"
    | :? (int * int) -> "int*int"
    | :? (int -> int) -> "fun"
    | :? Exception -> "exn"
    | :? DateTime -> "DateTime"
    | :? TimeSpan -> "TimeSpan"
    | :? Guid -> "Guid"
    | :? unit -> "unit"
    | null -> "null"
    | _ -> "other"

let isGeneric<'T> (o: obj) =
    match o with
    | :? 'T -> "yes"
    | _ -> "no"

let run () =
    check "box.int" (fun () -> describe (box 1))
    check "box.float.integral" (fun () -> describe (box 1.0))
    check "box.float" (fun () -> describe (box 1.5))
    check "box.int64" (fun () -> describe (box 1L))
    check "box.byte" (fun () -> describe (box 1uy))
    check "box.decimal" (fun () -> describe (box 1M))
    check "box.char" (fun () -> describe (box 'a'))
    check "box.string.single" (fun () -> describe (box "a"))
    check "box.bool" (fun () -> describe (box true))
    check "box.string.list" (fun () -> describe (box [ "a" ]))
    check "box.int.list" (fun () -> describe (box [ 1 ]))
    check "box.empty.list" (fun () -> describe (box ([]: string list)))
    check "box.float.array" (fun () -> describe (box [| 1.5 |]))
    check "box.int.array" (fun () -> describe (box [| 1 |]))
    check "box.record" (fun () -> describe (box { A = 1 }))
    check "box.interface" (fun () -> describe (box (C())))
    check "box.some" (fun () -> describe (box (Some 1)))
    check "box.none" (fun () -> describe (box (None: int option)))
    check "box.tuple" (fun () -> describe (box (1, 2)))
    check "box.fun" (fun () -> describe (box (fun (x: int) -> x)))
    check "box.exn" (fun () -> describe (box (Exception "e")))
    check "box.datetime" (fun () -> describe (box (DateTime(2000, 1, 1))))
    check "box.timespan" (fun () -> describe (box TimeSpan.Zero))
    check "box.guid" (fun () -> describe (box Guid.Empty))
    check "box.unit" (fun () -> describe (box ()))
    check "box.null" (fun () -> describe null)
    check "box.generic.int.as.float" (fun () -> isGeneric<float> (box 1))
    check "box.generic.string" (fun () -> isGeneric<string> (box "a"))
    check "box.generic.list" (fun () -> isGeneric<int list> (box [ "a" ]))
    check "box.eq" (fun () -> string (box 1 = box 1) + string (box 1 = box 1L) + string (box 1 = box 1.0))
    check "box.equals" (fun () -> string ((box 1).Equals(box 1.0)) + string ((box 1).Equals(box 1)))
    check "box.hash.int.float" (fun () -> string (hash (box 1) = hash (box 1.0)))
    check "box.unbox.wrong" (fun () -> string (unbox<float> (box 1)))
    check "box.unbox.int.as.int64" (fun () -> string (unbox<int64> (box 1)))
    check "box.unbox.null.int" (fun () -> string (unbox<int> null))
    check "box.unbox.null.option" (fun () -> string (unbox<int option> null))
    check "box.downcast.fail" (fun () -> string ((box "a") :?> R).A)
    check "box.downcast.int.string" (fun () -> (box 1) :?> string)
    check "box.tostring" (fun () -> (box 1.0).ToString() + "," + (box 'c').ToString() + "," + (box true).ToString())
    check "obj.list.mixed" (fun () -> [ box 1; box "a"; box 2.5; box 'c'; box 3L ] |> List.map describe |> String.concat ",")
    check "dyn.typeof.equality" (fun () -> string ((box 1).GetType() = typeof<int>) + string ((box 1.0).GetType() = typeof<float>))
    check "dyn.typeof.float.int" (fun () -> string ((box 2.0).GetType() = typeof<int>))
    check "option.of.obj" (fun () -> string (Option.ofObj (null: string)) + string (Option.ofObj "a"))
    check "nullable" (fun () -> let n = Nullable<int>() in string n.HasValue + string (Nullable 3).Value)
    check "nullable.eq" (fun () -> string (Nullable 3 = Nullable 3) + string (Nullable<int>() = Nullable<int>()))
    check "voption" (fun () -> string (ValueSome 1) + string (ValueNone: int voption) + string (ValueOption.defaultValue 0 ValueNone))
    check "option.some.null" (fun () -> let x: string option = Some null in string x.IsSome)
    check "option.some.unit" (fun () -> let x = Some() in string x.IsSome)
    check "option.generic.some.none"
        (fun () ->
            let wrap (x: 'T) : 'T option = Some x
            let v = wrap (None: int option)
            string v.IsSome)
    check "option.list.of.none" (fun () -> string (List.choose id [ Some(None: int option); None ] |> List.length))
    check "result" (fun () -> string (Ok 1: Result<int, string>) + string (Error "e": Result<int, string>))
    check "choice" (fun () -> string (Choice1Of2 1: Choice<int, string>))
