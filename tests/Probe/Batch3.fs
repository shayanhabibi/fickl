module Batch3

open System
open System.Collections.Generic
open Check

let defaultOfGeneric<'T> () = Unchecked.defaultof<'T>
let zeroArray<'T> n : 'T[] = Array.zeroCreate n
let createDefault<'T> n : 'T[] = Array.create n Unchecked.defaultof<'T>

let rec depthOf<'T> (n: int) (x: 'T) : int = if n = 0 then 0 else 1 + depthOf (n - 1) [ x ]

[<Struct>]
type Vec =
    { X: float; Y: float }
    static member (+)(a: Vec, b: Vec) = { X = a.X + b.X; Y = a.Y + b.Y }
    static member Zero = { X = 0.0; Y = 0.0 }
    static member DivideByInt(a: Vec, n: int) = { X = a.X / float n; Y = a.Y / float n }

type Money =
    | Money of decimal
    static member (+)(Money a, Money b) = Money(a + b)
    static member Zero = Money 0M


let inline addGeneric (a: ^T) (b: ^T) = a + b

[<AbstractClass>]
type BaseInit() as this =
    let log = ResizeArray<string>()
    do log.Add("base:" + this.Describe())
    member _.Log = String.concat "," log
    abstract Describe: unit -> string

type DerivedInit() =
    inherit BaseInit()
    let name = "derived"
    override _.Describe() = "describe(" + (if isNull (box name) then "null" else name) + ")"

type SelfRef() as self =
    let mutable count = 0
    let bump () = count <- count + 1
    do bump ()
    member _.Count = count
    member _.Me = self

type Secondary =
    val mutable Value: int
    new() as x = { Value = 1 } then x.Value <- x.Value + 10
    new(v) = { Value = v }

type IShape =
    abstract Area: float

type Square =
    { Side: float }
    interface IShape with
        member s.Area = s.Side * s.Side

type Circ =
    | Circ of float
    override this.ToString() = "circ!"
    interface IShape with
        member this.Area = (match this with Circ r -> 3.0 * r * r)

[<CustomEquality; NoComparison>]
type CaseInsensitive =
    | CI of string
    override this.Equals o =
        match o with
        | :? CaseInsensitive as (CI other) -> (match this with CI s -> s.ToLower() = other.ToLower())
        | _ -> false
    override this.GetHashCode() = match this with CI s -> s.ToLower().GetHashCode()

[<return: Struct>]
let (|Even|_|) n = if n % 2 = 0 then ValueSome(n / 2) else ValueNone

type Calc() =
    member _.Add (a: int) (b: int) = a + b
    member _.Scale(factor: int, x: int) = factor * x
    static member Twice (f: int -> int) x = f (f x)

let run () =
    check "b3.defaultof.int" (fun () -> string (defaultOfGeneric<int> () + 1))
    check "b3.defaultof.float" (fun () -> string (defaultOfGeneric<float> () + 0.5))
    check "b3.defaultof.bool" (fun () -> string (defaultOfGeneric<bool> ()))
    check "b3.defaultof.string" (fun () -> string (isNull (defaultOfGeneric<string> ())))
    check "b3.defaultof.struct" (fun () -> string (defaultOfGeneric<Vec> ()).X)
    check "b3.defaultof.char" (fun () -> string (int (defaultOfGeneric<char> ())))
    check "b3.zeroArray.int" (fun () -> string (Array.sum (zeroArray<int> 3)))
    check "b3.zeroArray.bool" (fun () -> string (zeroArray<bool> 2).[0])
    check "b3.zeroArray.float" (fun () -> string ((zeroArray<float> 2).[1] + 1.0))
    check "b3.createDefault.int" (fun () -> string ((createDefault<int> 2).[0] + 5))
    check "b3.inline.defaultof" (fun () -> let inline d () = Unchecked.defaultof<_> in string ((d (): int) + 1))
    check "b3.polymorphic.recursion" (fun () -> string (depthOf 5 1))
    check "b3.generic.sum.struct" (fun () -> string (List.sum [ { X = 1.0; Y = 2.0 }; { X = 3.0; Y = 4.0 } ]).X)
    check "b3.generic.average.struct" (fun () -> string (List.average [ { X = 1.0; Y = 2.0 }; { X = 3.0; Y = 4.0 } ]).Y)
    check "b3.generic.sum.union" (fun () -> match List.sum [ Money 1.5M; Money 2.5M ] with Money m -> string m)
    check "b3.inline.add" (fun () -> string (addGeneric 1 2) + addGeneric "a" "b" + string (addGeneric { X = 1.0; Y = 1.0 } { X = 1.0; Y = 1.0 }).X)
    check "b3.average.decimal" (fun () -> string (List.average [ 1M; 2M; 4M ]))
    check "b3.sumBy.decimal" (fun () -> string (List.sumBy (fun (x: decimal) -> x * 2M) [ 0.1M; 0.2M ]))
    check "b3.dict.array.key" (fun () -> let d = Dictionary<int[], int>() in d.[[| 1 |]] <- 1; string (d.ContainsKey [| 1 |]))
    check "b3.hashset.array" (fun () -> let h = HashSet<int[]>() in h.Add [| 1 |] |> ignore; string (h.Contains [| 1 |]))
    check "b3.dict.struct.key" (fun () -> let d = Dictionary<Vec, int>() in d.[{ X = 1.0; Y = 2.0 }] <- 1; string (d.ContainsKey { X = 1.0; Y = 2.0 }))
    check "b3.dict.reference" (fun () -> let d = Dictionary<string list, int>(HashIdentity.Reference) in d.[[ "a" ]] <- 1; string (d.ContainsKey [ "a" ]))
    check "b3.set.array" (fun () -> string (Set.contains [| 1 |] (set [ [| 1 |] ])))
    check "b3.dict.tuple.list" (fun () -> let d = Dictionary<int * string list, int>() in d.[(1, [ "a" ])] <- 1; string (d.ContainsKey((1, [ "a" ]))))
    check "b3.dict.customeq" (fun () -> let d = Dictionary<CaseInsensitive, int>() in d.[CI "A"] <- 1; string (d.ContainsKey(CI "a")))
    check "b3.customeq.union" (fun () -> string (CI "A" = CI "a") + string (List.contains (CI "x") [ CI "X" ]))
    check "b3.base.virtual.ctor" (fun () -> DerivedInit().Log)
    check "b3.self.ref" (fun () -> let s = SelfRef() in string s.Count + string (obj.ReferenceEquals(s, s.Me)))
    check "b3.secondary.ctor" (fun () -> string (Secondary().Value) + "," + string (Secondary(5).Value))
    check "b3.record.interface" (fun () -> string ((({ Side = 3.0 }: Square) :> IShape).Area))
    check "b3.union.interface" (fun () -> string ((Circ 1.0 :> IShape).Area) + string (Circ 1.0))
    check "b3.interface.list" (fun () -> [ ({ Side = 2.0 } :> IShape); (Circ 1.0 :> IShape) ] |> List.sumBy (fun s -> s.Area) |> string)
    check "b3.struct.ap" (fun () -> [ 4; 5 ] |> List.map (function Even h -> "e" + string h | _ -> "o") |> String.concat ",")
    check "b3.curried.member" (fun () -> let c = Calc() in let add1 = c.Add 1 in string (add1 2) + string (List.map (c.Add 10) [ 1; 2 ] |> List.sum))
    check "b3.method.as.function" (fun () -> let c = Calc() in [ 1; 2 ] |> List.map (fun x -> c.Scale(3, x)) |> List.sum |> string)
    check "b3.static.higher" (fun () -> string (Calc.Twice ((+) 3) 1))
    check "b3.method.group"
        (fun () ->
            let sb = Text.StringBuilder()
            [ "a"; "b" ] |> List.iter (sb.Append >> ignore)
            sb.ToString())
    check "b3.overloaded.method.group" (fun () -> [ 1.5; -2.5 ] |> List.map Math.Abs |> List.sum |> string)
    check "b3.string.method.group" (fun () -> [ " a "; "b " ] |> List.map (fun s -> s.Trim()) |> String.concat "|")
    check "b3.tuple8"
        (fun () ->
            let t = (1, 2, 3, 4, 5, 6, 7, 8, 9)
            let (a, _, _, _, _, _, _, h, i) = t
            string (a + h + i) + string (t = (1, 2, 3, 4, 5, 6, 7, 8, 9)))
    check "b3.struct.tuple8" (fun () -> let struct (a, _, _, _, _, _, _, h) = struct (1, 2, 3, 4, 5, 6, 7, 8) in string (a + h))
    check "b3.tuple8.tostring" (fun () -> string (1, 2, 3, 4, 5, 6, 7, 8))
    check "b3.use.null"
        (fun () ->
            let f () =
                use _d = (null: IDisposable)
                "ok"
            f ())
    check "b3.lock" (fun () -> let o = obj () in lock o (fun () -> "locked"))
    check "b3.literal.int" (fun () -> String.Join(",", 0x7FFFFFFF, 0b1010, 1_000_000, 0o17, -2147483648))
    check "b3.literal.int64" (fun () -> String.Join(",", 9223372036854775807L, -9223372036854775808L, 0xFFFFFFFFFFFFFFFFUL, 0x8000000000000000UL))
    check "b3.literal.small" (fun () -> String.Join(",", 255uy, -128y, 32767s, 65535us, 0xFFFFFFFFu))
    check "b3.literal.big" (fun () -> String.Join(",", 123456789012345678901234567890I, 79228162514264337593543950335M, 1e3f, 1n, 1un))
    check "b3.literal.decimal.small" (fun () -> string 0.0000000000000000000000000001M)
    check "b3.literal.char" (fun () -> String.Join(",", int '\u0000', int '\'', int '\065', int '\t', int '\\'))
    check "b3.literal.string.escapes" (fun () -> let s = "é\U0001F600\t\"\\" in string s.Length + string (int s.[0]))
    check "b3.literal.verbatim.braces" (fun () -> @"{0}" + """{"a":1}""")
    check "b3.extended.interp" (fun () -> let x = 5 in $$"""{"value": {{x}}}""")
    check "b3.extended.interp.triple" (fun () -> let x = 1 in $$$"""{{{{x}}}}""")
    check "b3.int64.minvalue.div" (fun () -> string (Int64.MinValue / -1L))
    check "b3.uint64.div" (fun () -> string (UInt64.MaxValue / 3UL) + "," + string (UInt64.MaxValue % 10UL))
    check "b3.int64.overflow.mul" (fun () -> string (Int64.MaxValue * 2L))
    check "b3.lazy.seq.capture"
        (fun () ->
            let fs = seq { for i in 1..3 -> fun () -> i } |> Seq.toList
            fs |> List.map (fun f -> string (f ())) |> String.concat ",")
    check "b3.finally.value"
        (fun () ->
            let mutable log = ""
            let r =
                try
                    "body"
                finally
                    log <- "finally"
            r + "," + log)
    check "b3.nested.try.rethrow.order"
        (fun () ->
            let log = ResizeArray()
            try
                try
                    try failwith "x" finally log.Add "f1"
                with e ->
                    log.Add "h1"
                    raise (InvalidOperationException "y")
            with :? InvalidOperationException as e -> log.Add("h2:" + e.Message)
            String.concat "," log)
    check "b3.event.remove"
        (fun () ->
            let e = Event<int>()
            let log = ResizeArray()
            let h = Handler<int>(fun _ x -> log.Add(string x))
            e.Publish.AddHandler h
            e.Trigger 1
            e.Publish.RemoveHandler h
            e.Trigger 2
            String.concat "," log)
    check "b3.string.join.nullsep" (fun () -> String.Join(null, [| "a"; "b" |]))
    check "b3.string.concat.null" (fun () -> String.Concat("a", null, "b"))
    check "b3.string.isnullorwhitespace" (fun () -> string (String.IsNullOrWhiteSpace " \t") + string (String.IsNullOrWhiteSpace null))
    check "b3.compare.ordinalignorecase" (fun () -> string (String.Compare("abc", "ABD", StringComparison.OrdinalIgnoreCase) < 0))
    check "b3.string.startswith.culture" (fun () -> string ("abc".StartsWith("AB", StringComparison.OrdinalIgnoreCase)))
    check "b3.list.pairwise.empty" (fun () -> string (List.pairwise [ 1 ] |> List.length))
    check "b3.seq.groupBy.lazy"
        (fun () ->
            let mutable n = 0
            let g = seq { 1..4 } |> Seq.map (fun x -> n <- n + 1; x) |> Seq.groupBy (fun x -> x % 2)
            let before = n
            let count = Seq.length g
            string before + "," + string count + "," + string n)
    check "b3.seq.sortBy.lazy"
        (fun () ->
            let mutable n = 0
            let s = seq { 3; 1; 2 } |> Seq.map (fun x -> n <- n + 1; x) |> Seq.sortBy id
            let before = n
            string before + "," + (s |> Seq.map string |> String.concat "") + "," + string n)
    check "b3.seq.distinct.order" (fun () -> [ 3; 1; 3; 2; 1 ] |> Seq.distinct |> Seq.map string |> String.concat "")
    check "b3.list.except.order" (fun () -> List.except [ 2 ] [ 3; 2; 1; 3 ] |> List.map string |> String.concat "")
    check "b3.map.tryFind.default" (fun () -> string (Map.empty<string, int> |> Map.tryFind "x" |> Option.defaultValue -1))
    check "b3.map.toSeq.order" (fun () -> Map.ofList [ "b", 2; "a", 1; "C", 3 ] |> Map.toSeq |> Seq.map fst |> String.concat "")
    check "b3.set.order.strings" (fun () -> set [ "b"; "a"; "B"; "_" ] |> Seq.toList |> String.concat "")
    check "b3.array.sortWith.stable"
        (fun () ->
            let xs = [| (1, "a"); (0, "b"); (1, "c"); (0, "d") |]
            Array.sortWith (fun (a, _) (b, _) -> compare a b) xs |> Array.map snd |> String.concat "")
    check "b3.list.sortWith.stable"
        (fun () ->
            let xs = [ (1, "a"); (0, "b"); (1, "c"); (0, "d") ]
            List.sortWith (fun (a, _) (b, _) -> compare a b) xs |> List.map snd |> String.concat "")
