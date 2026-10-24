open System

type Plain =
    {
        N: int
        S: string
    }

type FloatRecord = { F: float }
type Shallow = { Items: int[] }
let mutable calls = 0

[<CustomEquality; NoComparison>]
type Odd =
    {
        N: int
    }

    override _.GetHashCode() = 0

    override _.Equals(_) =
        calls <- calls + 1
        false

type Nested = { Item: Odd }

let a: Plain =
    {
        N = 1
        S = "a"
    }

let b: Plain =
    {
        N = 1
        S = "a"
    }

printfn "distinctEqual=%b reference=%b" (a = b) (Object.ReferenceEquals(a, b))
let n = { F = nan }

printfn
    "nanScalar=%b nanRecordSelf=%b nanRecordCopy=%b objectEquals=%b"
    (nan = nan)
    (n = n)
    (n = { F = nan })
    (n.Equals n)

let o: Odd = { N = 1 }
printfn "customSelf=%b calls=%d" (o = o) calls
let wrapped = { Item = o }
let before = calls
printfn "nestedCustomSelf=%b callsDelta=%d" (wrapped = wrapped) (calls - before)
let x = { Items = [| 1 |] }
let y = { Items = [| 1 |] }
printfn "shallowBefore=%b" (x = y)
x.Items[0] <- 2
printfn "shallowAfter=%b" (x = y)

if not (a = b) || Object.ReferenceEquals(a, b) || x = y then
    failwith "semantic regression"

let codes = ResizeArray<int>()

let filtered =
    String.filter
        (fun c ->
            codes.Add(int c)
            true
        )
        "😀"

printfn "stringFilterUtf16=%A length=%d" (codes.ToArray()) filtered.Length

try
    let result = String.filter (fun _ -> true) null
    printfn "stringFilterNull=result:%A" result
with e ->
    printfn "stringFilterNull=throws:%s" (e.GetType().FullName)
