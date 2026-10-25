module Check

let classify (e: exn) =
    match e with
    | :? System.ArgumentOutOfRangeException -> "ArgumentOutOfRangeException"
    | :? System.ArgumentNullException -> "ArgumentNullException"
    | :? System.ArgumentException -> "ArgumentException"
    | :? System.Collections.Generic.KeyNotFoundException -> "KeyNotFoundException"
    | :? System.IndexOutOfRangeException -> "IndexOutOfRangeException"
    | :? System.OverflowException -> "OverflowException"
    | :? System.DivideByZeroException -> "DivideByZeroException"
    | :? System.FormatException -> "FormatException"
    | :? System.InvalidOperationException -> "InvalidOperationException"
    | :? System.NullReferenceException -> "NullReferenceException"
    | :? System.NotSupportedException -> "NotSupportedException"
    | :? System.InvalidCastException -> "InvalidCastException"
    | :? MatchFailureException -> "MatchFailureException"
    | _ -> "Exception"

let check (name: string) (f: unit -> string) =
    let r =
        try
            f ()
        with e ->
            "throws " + classify e
    System.Console.WriteLine(name + "=" + r)
