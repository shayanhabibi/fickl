module Weird2

open System
open Check
open Weird
open Weird.Operators2

do Log.add "Weird2.init"
let late = (Log.add "Weird2.value"; Early.initialised + 1)

let maybe = MaybeBuilder()
let collect = ListCollector()

let run () =
    check "w.init.order" (fun () -> String.Join(",", Log.lines) + "|" + string late)
    check "w.inline.internal" (fun () -> Early.usesSecret ())
    check "w.private.via.public" (fun () -> Early.exposed ())
    check "w.inline.mutable" (fun () -> let a = Early.bump () in let b = Early.bump () in string a + string b + string Early.counter)
    check "w.module.type.samename" (fun () -> string (Foo.value (Foo.create 3)))
    check "w.rqa.union" (fun () -> [ Color.Red; Color.``Light Blue``; Color.``with-dash`` ] |> List.map string |> String.concat ",")
    check "w.rqa.union.eq" (fun () -> string (Color.``Light Blue`` = Color.``Light Blue``))
    check "w.backtick.fields"
        (fun () ->
            let r = { ``my field`` = 1; ``a-b`` = "ab"; ``class`` = 2; ``1x`` = 3 }
            let r2 = { r with ``a-b`` = "cd" }
            string r.``my field`` + r.``a-b`` + r2.``a-b`` + string (r.``class`` + r.``1x``))
    check "w.backtick.fields.eq"
        (fun () ->
            let mk () = { ``my field`` = 1; ``a-b`` = "ab"; ``class`` = 2; ``1x`` = 3 }
            string (mk () = mk ()) + string (compare (mk ()) { mk () with ``1x`` = 4 }))
    check "w.explicit.iface.vs.class"
        (fun () ->
            let t = Thing()
            t.Name + "," + t.Describe() + "," + (t :> IThing).Name + "," + (t :> IThing).Describe())
    check "w.explicit.iface.generic.fn"
        (fun () ->
            let describe (x: #IThing) = x.Describe()
            describe (Thing()))
    check "w.two.ifaces.same.member"
        (fun () ->
            let b = Both()
            string ((b :> IOne).Value()) + string ((b :> ITwo).Value()))
    check "w.two.ifaces.list"
        (fun () ->
            let ones: IOne list = [ Both(); { new IOne with member _.Value() = 10 } ]
            ones |> List.sumBy (fun o -> o.Value()) |> string)
    check "w.iface.generic.method"
        (fun () ->
            let g = { new IGen with member _.Map x = [ x; x ] }
            string (List.length (g.Map 1)) + (g.Map "a" |> String.concat ""))
    check "w.iface.prop.setter"
        (fun () ->
            let mutable store = 0
            let p = { new IProp with member _.P with get () = store and set v = store <- v * 2 }
            p.P <- 5
            string p.P)
    check "w.point.props"
        (fun () ->
            let p = Point(1, 2, Label = "lbl")
            p.Label + string p.[0] + string p.[1] + string p.[2, 3])
    check "w.point.optional"
        (fun () ->
            let p = Point(1, 2)
            String.Join(",", p.Sum(), p.Sum(2), p.Sum(offset = 5), p.Sum(scale = 3, offset = 1), p.Opt(), p.Opt(1), p.Many(), p.Many(1, 2, 3)))
    check "w.point.named.order"
        (fun () ->
            let log = ResizeArray()
            let n (s: string) v = log.Add s; v
            let p = Point(1, 2)
            let r = p.Sum(offset = n "offset" 1, scale = n "scale" 2)
            string r + ":" + String.Join(",", log))
    check "w.point.ops" (fun () -> string (Point(1, 2) + Point(3, 4)) + string (-Point(1, 2)))
    check "w.point.implicit" (fun () -> let p: Point = (5, 6) in string p)
    check "w.srtp.operator" (fun () -> string (Ops.(>>=)(Some 1, fun x -> Some(x + 1))))
    check "w.custom.ops"
        (fun () ->
            let m = Map.ofList [ "k", 9 ]
            let r = ref 1
            String.Join(",", (1 |>> (fun x -> x)), (2 *** 3), (1 .>>. "a"), (1 <^> 2), (%%5), (!!r), ("abc" =~ "b"), ((fun x -> x + 1) $ 1), (m?k)))
    check "w.custom.ap" (fun () -> match 5 with Positive p -> string p | _ -> "np")
    check "w.custom.range" (fun () -> let a: int list = (..) 1 3 in let b: int list = (.. ..) 1 2 3 in String.Join(",", a) + "|" + String.Join(",", b))
    check "w.ext.string" (fun () -> "hi".Shout + "ab".Repeat(2) + String.Hello)
    check "w.ext.int" (fun () -> string (21).Double)
    check "w.ext.list" (fun () -> string [ 1; 2; 3 ].Second)
    check "w.ext.vs.intrinsic" (fun () -> let p = Point(1, 2) in p.Describe() + string (p.Swap()))
    check "w.ext.csharp" (fun () -> string ((4).Triple()) + string (CsExt.Triple 5))
    check "w.ce.maybe"
        (fun () ->
            let r = maybe {
                let! a = Some 1
                let! b = Some 2
                let mutable i = 0
                while i < 3 do
                    i <- i + 1
                for x in [ 1; 2 ] do
                    i <- i + x
                try
                    if a > 5 then failwith "x"
                finally
                    i <- i + 100
                return a + b + i
            }
            string r)
    check "w.ce.maybe.none" (fun () -> string ((maybe { let! a = None in return a + 1 }).IsNone))
    check "w.ce.applicative"
        (fun () ->
            let r = maybe {
                let! a = Some 1
                and! b = Some 2
                return a + b
            }
            string r)
    check "w.ce.trywith" (fun () -> string (maybe { try return (failwith "boom": int) with _ -> return -1 }))
    check "w.ce.custom.ops"
        (fun () ->
            let r = collect {
                for x in [ 1; 2; 3; 4; 5; 6 ] do
                keep (x % 2 = 0)
                double
            }
            String.Join(",", r))
    check "w.ce.yield"
        (fun () ->
            let r = collect {
                yield 1
                yield! [ 2; 3 ]
                for x in [ 4; 5 ] do
                    yield x * 10
            }
            String.Join(",", r))
    check "w.custom.equality"
        (fun () ->
            let a = { Key = "Ab"; Extra = 1 }
            let b = { Key = "aB"; Extra = 2 }
            string (a = b) + string (hash a = hash b) + string (Set.count (set [ a; b ])) + string (Map.ofList [ a, 1; b, 2 ] |> Map.count)
            + string (List.distinct [ a; b ] |> List.length) + string (compare a b))
    check "w.custom.equality.dict"
        (fun () ->
            let d = Collections.Generic.Dictionary<Loose, int>()
            d.[{ Key = "X"; Extra = 0 }] <- 1
            string d.[{ Key = "x"; Extra = 9 }])
    check "w.custom.equality.hashset"
        (fun () ->
            let h = Collections.Generic.HashSet<Loose>()
            h.Add { Key = "X"; Extra = 0 } |> ignore
            string (h.Add { Key = "x"; Extra = 9 }))
    check "w.union.member" (fun () -> (Circle 1.0).Kind + (Square 2.0).Kind)
    check "w.generic.tree" (fun () -> string ((Node(Leaf 1, Node(Leaf 2, Leaf 3))).Fold(id, (+))))
    check "w.delegate" (fun () -> let d = Delegates(fun a b -> a * b) in string (d.Invoke(6, 7)))
    check "w.func" (fun () -> let f = Func<int, int, int>(fun a b -> a - b) in string (f.Invoke(10, 3)))
    check "w.action" (fun () -> let mutable x = 0 in let a = Action<int>(fun v -> x <- v) in a.Invoke 5; string x)
    check "w.func.to.fsharp" (fun () -> let f = Func<int, int>(fun a -> a + 1) in [ 1; 2 ] |> List.map f.Invoke |> List.sum |> string)
    check "w.inheritance" (fun () -> Dog("rex").Greet() + "|" + Puppy("bit").Greet() + "|" + (Puppy("p") :> Animal).Sound())
    check "w.generic.override" (fun () -> GenDerived().Wrap 1 + (GenDerived() :> GenBase<int>).Wrap 2 + GenBase<string>().Wrap "s")
    check "w.module.rec" (fun () -> string (RecModule.isEvenR 10))
    check "w.module.rec.types"
        (fun () ->
            let n: RecModule.Node = { Children = []; Owner = None }
            RecModule.describe { Name = "o"; Root = { n with Children = [ n ] } })
    check "w.unicode.names" (fun () -> string π.τ + π.变量 + π.``a.b`` + π.``a/b`` + π.``#x`` + π.``x y z`` + π.``default``)
    check "w.typeof.generic" (fun () -> typedefof<int list>.Name + "," + typeof<Map<int, string>>.Name)
    check "w.reflection.union"
        (fun () ->
            let cases = Reflection.FSharpType.GetUnionCases typeof<Shape>
            cases |> Array.map (fun c -> c.Name) |> String.concat ",")
    check "w.reflection.record"
        (fun () ->
            let fields = Reflection.FSharpType.GetRecordFields typeof<Foo>
            let r = Reflection.FSharpValue.MakeRecord(typeof<Foo>, [| box 5 |]) :?> Foo
            (fields |> Array.map (fun f -> f.Name) |> String.concat ",") + string r.V)
    check "w.reflection.getunionfields"
        (fun () ->
            let case, args = Reflection.FSharpValue.GetUnionFields(Circle 2.5, typeof<Shape>)
            case.Name + string args.[0])
    check "w.quote" (fun () -> let q = <@ 1 + 2 @> in string (q.GetType().Name.Length > 0))
    check "w.match.string.switch"
        (fun () ->
            [ "a"; "bb"; "ccc"; "dddd"; "x" ]
            |> List.map (function "a" -> 1 | "bb" -> 2 | "ccc" -> 3 | "dddd" -> 4 | _ -> 0)
            |> List.sum |> string)
    check "w.match.or.bindings"
        (fun () ->
            let f = function
                | (x, 0) | (0, x) -> x
                | (a, b) -> a * b
            string (f (5, 0)) + string (f (0, 6)) + string (f (2, 3)))
    check "w.match.as.nested" (fun () -> match [ (1, "a") ] with [ (n, _) as pair ] as whole -> string n + snd pair + string whole.Length | _ -> "")
    check "w.match.typetest.tuple"
        (fun () ->
            let f (a: obj, b: obj) =
                match a, b with
                | (:? string as s), (:? int as i) -> s + string i
                | _ -> "no"
            f (box "x", box 1) + f (box 1, box "x"))
    check "w.match.float.const" (fun () -> match 1.0 with 1.0 -> "one" | _ -> "other")
    check "w.match.decimal.const" (fun () -> match 1.5M with 1.5M -> "dec" | _ -> "other")
    check "w.match.null.string" (fun () -> let f (s: string) = match s with null -> "null" | "" -> "empty" | s -> s in f null + f "" + f "z")
    check "w.match.char.many" (fun () -> "a1-Z" |> Seq.map (function 'a' | 'b' -> 'L' | '1' -> 'D' | 'Z' -> 'U' | _ -> '?') |> Seq.toArray |> String)
    check "w.match.big.int"
        (fun () ->
            let f n = match n with 0 -> "z" | 1 -> "o" | 2 -> "t" | 100 -> "h" | 1000 -> "k" | -1 -> "m" | _ -> "?"
            [ 0; 1; 2; 100; 1000; -1; 5 ] |> List.map f |> String.concat "")
    check "w.try.return.in.loop"
        (fun () ->
            let mutable result = 0
            for i in 1..5 do
                try
                    if i = 3 then failwith "skip"
                    result <- result + i
                with _ -> result <- result + 100
            string result)
    check "w.exn.in.finally"
        (fun () ->
            try
                try failwith "first" finally failwith "second"
            with Failure m -> m)
    check "w.nested.closures.shadow"
        (fun () ->
            let x = 1
            let f =
                let x = x + 1
                fun () ->
                    let x = x * 10
                    fun y -> x + y
            string (f () 5) + string x)
    check "w.generic.inline.cross"
        (fun () ->
            let inline twice (f: ^a -> ^a) x = f (f x)
            string (twice ((+) 1) 1) + twice (fun s -> s + "!") "a")
    check "w.units.int"
        (fun () ->
            let a = 3<Measure.kg>
            let b = 4<Measure.kg>
            string (a + b))
    check "w.units.generic" (fun () -> let inline sq (x: float<'u>) = x * x in string (sq 3.0<Measure.m>))
    check "w.literal.attr" (fun () -> string Lits.Combined + Lits.Text)
    check "w.enum.literal" (fun () -> string (int Lits.FlagsCombo))
    check "w.lazy.static" (fun () -> string (StaticLazy.Value) + string (StaticLazy.Value))
