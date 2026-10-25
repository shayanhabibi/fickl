module Values

open System
open Check

[<Struct>]
type MP = { mutable X: int; mutable Y: int }

[<Struct>]
type SP =
    val mutable A: int
    new(a) = { A = a }
    member this.Bump() = this.A <- this.A + 1

[<Struct>]
type SU =
    | SA of a: int
    | SB of b: string

type Holder() =
    member val P = { X = 1; Y = 2 } with get, set

type Counter<'T>() =
    static let mutable count = 0
    static member Incr() = count <- count + 1; count

type Cache<'T>() =
    static member val Items: string list = [] with get, set


let inc (x: byref<int>) = x <- x + 1
let setOut (x: outref<int>) = x <- 42
let readIn (x: inref<int>) = x + 1
let bumpStruct (p: byref<MP>) = p.X <- p.X + 100

let tryOut (s: string) =
    match Int32.TryParse s with
    | true, v -> v
    | _ -> -1

let run () =
    check "struct.copy.let"
        (fun () ->
            let mutable a = { X = 1; Y = 2 }
            let b = a
            a.X <- 10
            string b.X + "," + string a.X)
    check "struct.copy.arg"
        (fun () ->
            let mutate (p: MP) = let mutable q = p in q.X <- 99; q.X
            let mutable a = { X = 1; Y = 2 }
            let r = mutate a
            string r + "," + string a.X)
    check "struct.array.elem"
        (fun () ->
            let arr = [| { X = 1; Y = 1 }; { X = 2; Y = 2 } |]
            arr.[0].X <- 50
            string arr.[0].X)
    check "struct.array.copyout"
        (fun () ->
            let arr = [| { X = 1; Y = 1 } |]
            let mutable e = arr.[0]
            e.X <- 77
            string arr.[0].X + "," + string e.X)
    check "struct.list.copy"
        (fun () ->
            let mutable a = { X = 1; Y = 1 }
            let xs = [ a; a ]
            a.X <- 5
            xs |> List.map (fun p -> string p.X) |> String.concat ",")
    check "struct.closure.capture"
        (fun () ->
            let mutable a = { X = 1; Y = 1 }
            let snapshot = a
            let f () = snapshot.X
            a.X <- 9
            string (f ()))
    check "struct.val.method"
        (fun () ->
            let mutable s = SP(1)
            let t = s
            s.Bump()
            string s.A + "," + string t.A)
    check "struct.val.array.method"
        (fun () ->
            let arr = [| SP(1) |]
            arr.[0].Bump()
            string arr.[0].A)
    check "struct.property.copy"
        (fun () ->
            let h = Holder()
            let mutable p = h.P
            p.X <- 1000
            string h.P.X)
    check "struct.dict.copy"
        (fun () ->
            let d = Collections.Generic.Dictionary<string, MP>()
            d.["a"] <- { X = 1; Y = 1 }
            let mutable v = d.["a"]
            v.X <- 3
            string d.["a"].X)
    check "struct.eq" (fun () -> string ({ X = 1; Y = 2 } = { X = 1; Y = 2 }))
    check "struct.union.eq" (fun () -> string (SA 1 = SA 1) + string (SB "x" <> SA 1))
    check "struct.tuple.eq" (fun () -> string (struct (1, "a") = struct (1, "a")))
    check "struct.anon.eq" (fun () -> string ({| A = 1; B = 2 |} = {| B = 2; A = 1 |}))
    check "struct.default" (fun () -> let d = Unchecked.defaultof<MP> in string d.X + string d.Y)
    check "struct.default.su" (fun () -> let d = Unchecked.defaultof<SP> in string d.A)
    check "struct.zerocreate" (fun () -> let a: MP[] = Array.zeroCreate 2 in string a.[1].X)
    check "byref.local" (fun () -> let mutable x = 1 in inc &x; inc &x; string x)
    check "byref.field"
        (fun () ->
            let mutable p = { X = 1; Y = 2 }
            bumpStruct &p
            string p.X)
    check "byref.outref" (fun () -> let mutable x = 0 in setOut &x; string x)
    check "byref.inref" (fun () -> let x = 5 in string (readIn &x))
    check "byref.tryparse" (fun () -> string (tryOut "12") + string (tryOut "x"))
    check "byref.tryparse.mutable"
        (fun () ->
            let mutable v = 0
            let ok = Int32.TryParse("77", &v)
            string ok + string v)
    check "byref.dict.trygetvalue"
        (fun () ->
            let d = Collections.Generic.Dictionary<int, string>()
            d.[1] <- "one"
            let mutable s = null
            let ok = d.TryGetValue(1, &s)
            string ok + s)
    check "byref.ref.cell"
        (fun () ->
            let r = ref 1
            inc &r.contents
            string r.Value)
    check "static.generic.counter"
        (fun () ->
            Counter<int>.Incr() |> ignore
            Counter<int>.Incr() |> ignore
            let s = Counter<string>.Incr()
            string s + "," + string (Counter<int>.Incr()))
    check "static.generic.val"
        (fun () ->
            Cache<int>.Items <- [ "i" ]
            Cache<string>.Items <- [ "s" ]
            String.concat "," (Cache<int>.Items @ Cache<string>.Items))
    check "boxed.struct.copy"
        (fun () ->
            let mutable a = { X = 1; Y = 1 }
            let o = box a
            a.X <- 2
            string (unbox<MP> o).X)
    check "mutable.capture.ref"
        (fun () ->
            let mutable i = 0
            let fs = [ for _ in 1..3 -> (i <- i + 1; let j = i in fun () -> j) ]
            fs |> List.map (fun f -> string (f ())) |> String.concat ",")
    check "loop.closure.capture"
        (fun () ->
            let fs = ResizeArray()
            for i in 0..2 do
                fs.Add(fun () -> i)
            let gs = ResizeArray()
            for i = 0 to 2 do
                gs.Add(fun () -> i * 10)
            let mutable k = 0
            let hs = ResizeArray()
            while k < 3 do
                let kk = k
                hs.Add(fun () -> kk * 100)
                k <- k + 1
            (Seq.append fs (Seq.append gs hs)) |> Seq.map (fun f -> string (f ())) |> String.concat ",")
