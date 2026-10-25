module Odd

open System
open System.Collections.Generic
open Check

type IA =
    abstract M: unit -> string
    abstract P: int

type IB =
    abstract M: unit -> string

type IC =
    inherit IA
    abstract N: unit -> string

[<AbstractClass>]
type Base() =
    abstract V: unit -> string
    default _.V() = "base"
    member this.CallV() = this.V()

type Derived() =
    inherit Base()
    override _.V() = "derived:" + base.V()

type Shape =
    | Circle of r: float
    | Rect of w: float * h: float
    member this.Area =
        match this with
        | Circle r -> 3.0 * r * r
        | Rect(w, h) -> w * h
    static member (+)(a: Shape, b: Shape) = a.Area + b.Area
    override this.ToString() = sprintf "Shape(%g)" this.Area

exception MyErr of code: int * msg: string
exception Plain

type Rec = { A: int; B: string }

type Opt =
    { mutable Count: int }
    member this.Next() = this.Count <- this.Count + 1; this.Count

type Explicit =
    val X: int
    val mutable Y: string
    new() = { X = 1; Y = "y" }
    new(x) = { X = x; Y = "y2" }

type WithStaticInit() =
    static let log = ResizeArray<string>()
    static do log.Add "static-init"
    do log.Add "instance"
    static member Log = String.concat "," log

type Ev() =
    let ev = Event<int>()
    member _.Changed = ev.Publish
    member _.Fire x = ev.Trigger x

type Rec2 = { Self: Lazy<Rec2>; N: int }

let (|Even|Odd|) n = if n % 2 = 0 then Even else Odd
let (|DivBy|_|) d n = if n % d = 0 then Some(n / d) else None
let (|Pair|) (a, b) = struct (b, a)

[<Literal>]
let Lit = "lit"

let inline addAll (xs: ^T list) = List.reduce (fun a b -> a + b) xs
let inline getLength (x: ^T) = (^T: (member Length: int) x)
let inline zeroOf< ^T when ^T: (static member Zero: ^T)> () = LanguagePrimitives.GenericZero< ^T>

[<Measure>]
type m

[<Measure>]
type s

let speed (d: float<m>) (t: float<s>) = d / t

let rec fib = function 0 | 1 as n -> n | n -> fib (n - 1) + fib (n - 2)

let rec recObj = { Self = lazy recObj; N = 3 }

let describeNothing = ()
let order = ResizeArray<string>()
let note s = order.Add s; s

let run () =
    check "odd.objexpr.multi"
        (fun () ->
            let o =
                { new IA with
                    member _.M() = "a"
                    member _.P = 1
                  interface IB with
                    member _.M() = "b" }
            (o :> IA).M() + (o :?> IB).M() + string (o :> IA).P)
    check "odd.objexpr.inherit"
        (fun () ->
            let o =
                { new IC with
                    member _.M() = "m"
                    member _.P = 2
                    member _.N() = "n" }
            o.M() + o.N() + string ((o :> IA).P))
    check "odd.objexpr.base"
        (fun () ->
            let o = { new Base() with member _.V() = "obj" }
            o.CallV() + Derived().CallV())
    check "odd.objexpr.tostring" (fun () -> string { new Object() with member _.ToString() = "custom" })
    check "odd.union.operator" (fun () -> string (Circle 1.0 + Rect(2.0, 3.0)))
    check "odd.union.tostring" (fun () -> string (Rect(2.0, 3.0)) + sprintf "|%O" (Circle 1.0))
    check "odd.exn.fields"
        (fun () ->
            try raise (MyErr(3, "boom")) with
            | MyErr(c, m) -> string c + m)
    check "odd.exn.message" (fun () -> try raise (MyErr(3, "boom")) with e -> string (e :? MyErr))
    check "odd.exn.plain" (fun () -> try raise Plain with Plain -> "plain" | _ -> "other")
    check "odd.exn.eq" (fun () -> string (MyErr(1, "a") = MyErr(1, "a")))
    check "odd.exn.reraise"
        (fun () ->
            try
                try failwith "inner" with _ -> reraise ()
            with Failure m -> m)
    check "odd.exn.filter.order"
        (fun () ->
            try invalidArg "p" "bad" with
            | :? ArgumentNullException -> "null"
            | :? ArgumentException as e -> "arg"
            | _ -> "other")
    check "odd.finally.order"
        (fun () ->
            let log = ResizeArray()
            let f () =
                try
                    try
                        log.Add "body"
                        failwith "x"
                    finally
                        log.Add "finally1"
                with _ ->
                    log.Add "handler"
                    0
            f () |> ignore
            String.concat "," log)
    check "odd.use.dispose.order"
        (fun () ->
            let log = ResizeArray()
            let mk n = { new IDisposable with member _.Dispose() = log.Add("d" + n) }
            (use _a = mk "a"
             use _b = mk "b"
             log.Add "body")
            String.concat "," log)
    check "odd.seq.dispose.early"
        (fun () ->
            let log = ResizeArray()
            let s = seq {
                try
                    yield 1
                    yield 2
                    log.Add "after2"
                finally
                    log.Add "fin" }
            let h = Seq.head s
            string h + ":" + String.concat "," log)
    check "odd.seq.use.inside"
        (fun () ->
            let log = ResizeArray()
            let s = seq {
                use _d = { new IDisposable with member _.Dispose() = log.Add "disposed" }
                yield 1
                yield 2 }
            let t = Seq.take 1 s |> Seq.toList
            string t.Head + ":" + String.concat "," log)
    check "odd.seq.lazy.effects"
        (fun () ->
            let log = ResizeArray()
            let s = seq { for i in 1..3 do log.Add(string i); yield i }
            let first = Seq.tryFind (fun x -> x = 2) s
            string first + ":" + String.concat "," log)
    check "odd.seq.cache"
        (fun () ->
            let mutable n = 0
            let s = Seq.cache (seq { n <- n + 1; yield n })
            string (Seq.head s) + string (Seq.head s) + string n)
    check "odd.activepatterns"
        (fun () ->
            [ 4; 9; 7 ]
            |> List.map (function Even & DivBy 4 q -> "e4:" + string q | Even -> "e" | DivBy 3 q -> "d3:" + string q | Odd -> "o")
            |> String.concat ",")
    check "odd.activepattern.struct" (fun () -> let (Pair(struct (a, b))) = (1, 2) in string a + string b)
    check "odd.literal.pattern" (fun () -> match "lit" with Lit -> "matched" | _ -> "no")
    check "odd.guard.order"
        (fun () ->
            let log = ResizeArray()
            let r =
                match (1, 2) with
                | (a, _) when (log.Add "g1"; a > 5) -> "first"
                | (_, b) when (log.Add "g2"; b = 2) -> "second"
                | _ -> "third"
            r + ":" + String.concat "," log)
    check "odd.eval.order.args"
        (fun () ->
            order.Clear()
            let f a b c = a + b + c
            f (note "a") (note "b") (note "c") |> ignore
            String.concat "," order)
    check "odd.eval.order.record"
        (fun () ->
            order.Clear()
            let r = { B = note "b"; A = (note "a" |> ignore; 1) }
            String.concat "," order + string r.A)
    check "odd.eval.order.tuple"
        (fun () ->
            order.Clear()
            let t = (note "1", note "2", note "3")
            String.concat "," order)
    check "odd.eval.order.method"
        (fun () ->
            order.Clear()
            let s = String.Concat(note "x", note "y")
            String.concat "," order + s)
    check "odd.eval.order.pipe"
        (fun () ->
            order.Clear()
            let r = note "a" |> (fun a -> note "f" |> ignore; a + "!")
            String.concat "," order + r)
    check "odd.eval.order.binop"
        (fun () ->
            order.Clear()
            let r = (note "l").Length + (note "r").Length
            String.concat "," order)
    check "odd.eval.order.array.set"
        (fun () ->
            order.Clear()
            let arr = [| "" |]
            arr.[(note "i" |> ignore; 0)] <- note "v"
            String.concat "," order)
    check "odd.mutable.record" (fun () -> let o = { Count = 0 } in string (o.Next() + o.Next()))
    check "odd.explicit.ctor" (fun () -> let e = Explicit(5) in e.Y <- "z"; string e.X + e.Y + Explicit().Y)
    check "odd.static.init" (fun () -> WithStaticInit() |> ignore; WithStaticInit() |> ignore; WithStaticInit.Log)
    check "odd.event"
        (fun () ->
            let e = Ev()
            let log = ResizeArray()
            e.Changed.Add(fun x -> log.Add(string x))
            e.Changed |> Event.filter (fun x -> x > 1) |> Event.add (fun x -> log.Add("f" + string x))
            e.Fire 1
            e.Fire 2
            String.concat "," log)
    check "odd.srtp.add" (fun () -> string (addAll [ 1; 2; 3 ]) + addAll [ "a"; "b" ] + string (addAll [ 1.5; 2.0 ]))
    check "odd.srtp.member" (fun () -> string (getLength "abc") + string (getLength [| 1; 2 |]))
    check "odd.srtp.zero" (fun () -> string (zeroOf<int> ()) + string (zeroOf<decimal> ()) + string (zeroOf<float> ()))
    check "odd.measure" (fun () -> string (speed 10.0<m> 2.0<s>))
    check "odd.rec.function" (fun () -> string (fib 20))
    check "odd.rec.value" (fun () -> string recObj.Self.Value.Self.Value.N)
    check "odd.lazy.reentrant"
        (fun () ->
            let rec l: Lazy<int> = lazy (l.Value + 1)
            string l.Value)
    check "odd.dict.key.record"
        (fun () ->
            let d = Dictionary<Rec, int>()
            d.[{ A = 1; B = "x" }] <- 5
            string d.[{ A = 1; B = "x" }])
    check "odd.hashset.tuple" (fun () -> let h = HashSet<int * string>() in h.Add((1, "a")) |> ignore; string (h.Contains((1, "a"))))
    check "odd.dict.missing" (fun () -> let d = Dictionary<string, int>() in string d.["nope"])
    check "odd.dict.dup" (fun () -> let d = Dictionary<string, int>() in d.Add("a", 1); d.Add("a", 2); "added")
    check "odd.dict.order"
        (fun () ->
            let d = Dictionary<int, int>()
            for i in [ 5; 1; 3 ] do d.[i] <- i
            d.Remove 1 |> ignore
            d.[9] <- 9
            d.Keys |> Seq.map string |> String.concat ",")
    check "odd.dict.enumerate.mutate"
        (fun () ->
            let d = Dictionary<int, int>()
            d.[1] <- 1
            d.[2] <- 2
            for kv in d do d.[kv.Key + 10] <- 0
            "mutated")
    check "odd.list.enumerate.mutate"
        (fun () ->
            let l = ResizeArray [ 1; 2 ]
            for x in l do l.Add x
            "mutated")
    check "odd.map.custom.compare"
        (fun () ->
            let m = Map.ofList [ (Rect(1.0, 1.0)), "r"; (Circle 1.0), "c" ]
            m |> Map.toList |> List.map snd |> String.concat ",")
    check "odd.set.records" (fun () -> set [ { A = 2; B = "b" }; { A = 1; B = "z" }; { A = 1; B = "a" } ] |> Seq.map (fun r -> r.B) |> String.concat ",")
    check "odd.array.equality.ref" (fun () -> string ([| 1 |] = [| 1 |]) + string (obj.ReferenceEquals([| 1 |], [| 1 |])))
    check "odd.array.equals.method" (fun () -> string ([| 1 |].Equals([| 1 |])))
    check "odd.array.hash" (fun () -> string (hash [| 1; 2 |] = hash [| 1; 2 |]))
    check "odd.list.equals.method" (fun () -> string ([ 1 ].Equals([ 1 ])))
    check "odd.string.null" (fun () -> let s: string = null in string (isNull s) + string (String.IsNullOrEmpty s) + string s)
    check "odd.string.null.concat" (fun () -> let s: string = null in "a" + s + "b")
    check "odd.string.null.length" (fun () -> let s: string = null in string s.Length)
    check "odd.option.none.tostring" (fun () -> string (None: int option))
    check "odd.option.some.tostring" (fun () -> string (Some 1) + string (Some "a"))
    check "odd.option.nested" (fun () -> string (Some(None: int option)) + string (Some(Some 1)).IsSome)
    check "odd.unit.tostring" (fun () -> string ())
    check "odd.tuple.tostring" (fun () -> string (1, "a", 2.5))
    check "odd.list.tostring" (fun () -> string [ 1; 2; 3 ])
    check "odd.array.tostring" (fun () -> string [| 1; 2 |])
    check "odd.record.tostring" (fun () -> string { A = 1; B = "x" })
    check "odd.char.arith" (fun () -> string ('a' + char 1) + string (int 'z' - int 'a') + string (char (int 'A' + 2)))
    check "odd.char.range" (fun () -> String([| 'a'..'e' |]))
    check "odd.range.float" (fun () -> [ 0.0..0.1..0.3 ] |> List.map string |> String.concat ",")
    check "odd.range.neg.step" (fun () -> [ 5..-2..0 ] |> List.map string |> String.concat ",")
    check "odd.range.empty" (fun () -> string (List.length [ 5..1 ]))
    check "odd.range.int.max" (fun () -> string (Seq.length (seq { Int32.MaxValue - 2 .. Int32.MaxValue })))
    check "odd.range.byte" (fun () -> string (Seq.length [ 250uy..255uy ]))
    check "odd.slice.string" (fun () -> "hello".[1..3] + "|" + "hello".[3..] + "|" + "hello".[..1] + "|" + "hello".[4..2])
    check "odd.slice.oob" (fun () -> "hello".[2..10])
    check "odd.slice.array.oob" (fun () -> string ([| 1; 2; 3 |].[1..10].Length))
    check "odd.slice.list" (fun () -> [ 1..5 ].[1..2] |> List.map string |> String.concat ",")
    check "odd.slice.neg" (fun () -> string ([| 1; 2; 3 |].[-1..1].Length))
    check "odd.jagged" (fun () -> let a = Array.init 2 (fun i -> Array.create (i + 1) i) in string a.[1].[1])
    check "odd.string.format.align" (fun () -> String.Format("[{0,5}][{1,-5}][{2:000}]", 1, "ab", 7))
    check "odd.string.format.braces" (fun () -> String.Format("{{{0}}}", 5))
    check "odd.interp" (fun () -> let x = 5 in $"x={x,4}|{x:D3}|{{lit}}|%d{x}")
    check "odd.interp.nested" (fun () -> let xs = [ 1; 2 ] in $"""{String.concat "," (xs |> List.map (fun i -> $"<{i}>"))}""")
    check "odd.verbatim" (fun () -> @"a\nb""c" + """triple "q" """)
    check "odd.sprintf.formats" (fun () -> sprintf "%5d|%-5d|%05d|%+d|%x|%X|%o|%e|%.3f|%g|%c|%b" 42 42 42 42 255 255 8 1234.5 3.14159 0.0001 'z' true)
    check "odd.sprintf.s.null" (fun () -> sprintf "[%s]" (null: string))
    check "odd.sprintf.A.record" (fun () -> sprintf "%A" { A = 1; B = "x" })
    check "odd.sprintf.partial" (fun () -> let f = sprintf "%d-%d" in let g = f 1 in g 2 + g 3)
    check "odd.sprintf.star" (fun () -> sprintf "%*d|%-*s|" 5 1 4 "a")
    check "odd.sprintf.percent" (fun () -> sprintf "100%% %d%%" 5)
    check "odd.failwithf" (fun () -> try failwithf "e%d" 1 with Failure m -> m)
    check "odd.string.compare.default" (fun () -> string (String.Compare("a", "B")) + string (compare "a" "B") + string ("a" < "B"))
    check "odd.string.compare.ordinal" (fun () -> string (sign (String.CompareOrdinal("a", "B"))))
    check "odd.list.sort.strings" (fun () -> List.sort [ "b"; "B"; "a"; "A"; "_"; "1" ] |> String.concat ",")
    check "odd.string.split" (fun () -> String.Join("|", "a,,b,".Split(',')) + ";" + String.Join("|", "a,,b,".Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)))
    check "odd.string.split.string" (fun () -> String.Join("|", "a--b---c".Split("--")))
    check "odd.string.trim.chars" (fun () -> "xxaxx".Trim('x') + "|" + "  a  ".TrimStart() + "|")
    check "odd.string.indexof.empty" (fun () -> string ("abc".IndexOf "") + string ("abc".LastIndexOf "") + string ("".IndexOf ""))
    check "odd.string.replace.empty" (fun () -> "abc".Replace("b", ""))
    check "odd.string.substring.oob" (fun () -> "abc".Substring(2, 5))
    check "odd.string.padleft" (fun () -> "7".PadLeft(3, '0') + "7".PadRight(3, '.'))
    check "odd.string.toupper.special" (fun () -> "straße ǆ".ToUpperInvariant())
    check "odd.string.surrogate" (fun () -> string "😀".Length + string ("a😀b".ToCharArray().Length))
    check "odd.string.reverse" (fun () -> String(Array.rev ("abc".ToCharArray())))
    check "odd.string.contains.char" (fun () -> string ("abc".Contains 'b') + string ("abc".StartsWith 'a'))
    check "odd.string.equals.ignorecase" (fun () -> string (String.Equals("a", "A", StringComparison.OrdinalIgnoreCase)))
    check "odd.string.join.objs" (fun () -> String.Join(",", [| box 1; box "a"; null; box 2.5 |]))
    check "odd.string.concat.seq" (fun () -> String.Concat([ "a"; "b" ]))
    check "odd.char.isletter" (fun () -> string (Char.IsLetter 'é') + string (Char.IsDigit '٣') + string (Char.IsWhiteSpace ' ') + string (Char.IsUpper 'Σ'))
    check "odd.char.tostring" (fun () -> string 'a' + "a".[0].ToString() + string (Char.ToUpper 'ß'))
    check "odd.timespan" (fun () -> string (TimeSpan.FromMinutes 90.5) + "|" + string (TimeSpan(1, 2, 3, 4, 5)))
    check "odd.timespan.neg" (fun () -> string (TimeSpan.FromSeconds -1.5))
    check "odd.datetime.add" (fun () -> let d = DateTime(2024, 1, 31) in (d.AddMonths 1).ToString("yyyy-MM-dd"))
    check "odd.datetime.leap" (fun () -> (DateTime(2024, 2, 29).AddYears 1).ToString("yyyy-MM-dd") + string (DateTime.IsLeapYear 1900))
    check "odd.datetime.diff" (fun () -> string ((DateTime(2024, 3, 1) - DateTime(2024, 2, 1)).TotalDays))
    check "odd.datetime.dow" (fun () -> string (DateTime(2000, 1, 1).DayOfWeek) + string (DateTime(2000, 1, 1).DayOfYear))
    check "odd.datetime.format" (fun () -> DateTime(2024, 5, 6, 7, 8, 9, 10).ToString("yyyy-MM-ddTHH:mm:ss.fff"))
    check "odd.datetime.min" (fun () -> DateTime.MinValue.ToString("yyyy-MM-dd") + "|" + DateTime.MaxValue.ToString("yyyy-MM-dd"))
    check "odd.datetime.ticks" (fun () -> string (DateTime(2000, 1, 1).Ticks))
    check "odd.guid" (fun () -> string (Guid.Parse("00000000-0000-0000-0000-000000000001")))
    check "odd.regex" (fun () -> let m = Text.RegularExpressions.Regex.Match("a12b345", @"(\d+)") in m.Value + "," + m.Groups.[1].Value + "," + string m.Index)
    check "odd.regex.replace" (fun () -> Text.RegularExpressions.Regex.Replace("a1b22", @"\d+", "#"))
    check "odd.regex.named" (fun () -> let m = Text.RegularExpressions.Regex.Match("k=v", @"(?<key>\w)=(?<v>\w)") in m.Groups.["key"].Value + m.Groups.["v"].Value)
    check "odd.regex.split" (fun () -> String.Join("|", Text.RegularExpressions.Regex.Split("a1b2c", @"\d")))
    check "odd.stringbuilder" (fun () -> let sb = Text.StringBuilder() in sb.Append(1).Append('c').Append("s").AppendLine("x") |> ignore; sb.ToString().Replace("\r", ""))
    check "odd.list.pattern.cons" (fun () -> match [ 1; 2; 3 ] with x :: y :: rest -> string (x + y + List.length rest) | _ -> "no")
    check "odd.list.pattern.array" (fun () -> match [| 1; 2 |] with [| a; b |] -> string (a + b) | _ -> "no")
    check "odd.match.incomplete" (fun () -> let f (x: int option) = match x with Some v -> v in string (f None))
    check "odd.list.head.empty" (fun () -> string (List.head ([]: int list)))
    check "odd.list.item.oob" (fun () -> string ([ 1 ].[5]))
    check "odd.array.item.oob" (fun () -> string ([| 1 |].[5]))
    check "odd.array.neg.index" (fun () -> string ([| 1 |].[-1]))
    check "odd.array.create.neg" (fun () -> string (Array.zeroCreate<int> -1).Length)
    check "odd.list.reduce.empty" (fun () -> string (List.reduce (+) ([]: int list)))
    check "odd.seq.item.oob" (fun () -> string (Seq.item 5 [ 1 ]))
    check "odd.map.find.missing" (fun () -> string (Map.find 3 (Map.ofList [ 1, 1 ])))
    check "odd.list.take.oob" (fun () -> string (List.take 5 [ 1 ]))
    check "odd.seq.take.oob" (fun () -> string (Seq.toList (Seq.take 5 [ 1 ])))
    check "odd.list.truncate" (fun () -> string (List.truncate 5 [ 1 ]))
    check "odd.list.chunk" (fun () -> sprintf "%A" (List.chunkBySize 2 [ 1..5 ]))
    check "odd.list.windowed" (fun () -> string (List.windowed 2 [ 1..4 ] |> List.length))
    check "odd.list.splitInto" (fun () -> List.splitInto 3 [ 1..7 ] |> List.map (List.length >> string) |> String.concat ",")
    check "odd.list.transpose" (fun () -> sprintf "%A" (List.transpose [ [ 1; 2 ]; [ 3; 4 ] ]))
    check "odd.list.groupBy.order" (fun () -> List.groupBy (fun x -> x % 3) [ 5; 1; 3; 4; 2 ] |> List.map (fun (k, v) -> string k + ":" + (v |> List.map string |> String.concat "")) |> String.concat ",")
    check "odd.list.countBy" (fun () -> List.countBy id [ "b"; "a"; "b" ] |> List.map (fun (k, c) -> k + string c) |> String.concat ",")
    check "odd.list.distinctBy" (fun () -> List.distinctBy (fun (x: string) -> x.ToLower()) [ "A"; "a"; "B" ] |> String.concat ",")
    check "odd.seq.sortDescending.stable" (fun () -> Seq.sortByDescending fst [ (1, "a"); (2, "b"); (1, "c") ] |> Seq.map snd |> String.concat "")
    check "odd.array.sortInPlace" (fun () -> let a = [| 3; 1; 2 |] in Array.sortInPlace a; a |> Array.map string |> String.concat "")
    check "odd.array.blit.overlap" (fun () -> let a = [| 1..5 |] in Array.blit a 0 a 1 4; a |> Array.map string |> String.concat "")
    check "odd.array.fill" (fun () -> let a = Array.zeroCreate 4 in Array.fill a 1 2 7; a |> Array.map string |> String.concat "")
    check "odd.resizearray.ops" (fun () -> let r = ResizeArray [ 3; 1; 2 ] in r.Sort(); r.Insert(0, 9); r.RemoveAt 1; r.Reverse(); String.Join(",", r))
    check "odd.queue.stack" (fun () -> let q = Queue [ 1; 2 ] in let s = Stack [ 1; 2 ] in string (q.Dequeue()) + string (s.Pop()) + string (s.Peek()))
    check "odd.typeof.name" (fun () -> typeof<int list>.Name + "," + typeof<Rec>.FullName + "," + typeof<string option>.Name)
    check "odd.gettype" (fun () -> (box { A = 1; B = "" }).GetType().Name + "," + (box 1).GetType().Name)
    check "odd.nameof" (fun () -> nameof Rec + nameof describeNothing + nameof (Unchecked.defaultof<Rec>.A))
