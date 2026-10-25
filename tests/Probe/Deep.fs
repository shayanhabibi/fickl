module Deep

open System
open Check

type Tree =
    | Leaf
    | Node of Tree * int

type Rose = { Value: int; Kids: Rose list }

let rec isEven n = if n = 0 then true else isOdd (n - 1)
and isOdd n = if n = 0 then false else isEven (n - 1)

let rec sumCps n k = if n = 0 then k 0 else sumCps (n - 1) (fun acc -> k (acc + n))

let rec countDown n acc =
    match n with
    | 0 -> acc
    | n when n % 2 = 0 -> countDown (n - 1) (acc + 1)
    | n -> countDown (n - 1) acc

let rec deepTree n acc = if n = 0 then acc else deepTree (n - 1) (Node(acc, n))

let rec chain n = if n = 0 then { Value = 0; Kids = [] } else { Value = n; Kids = [ chain (n - 1) ] }

let rec loopTry n acc =
    if n = 0 then acc
    else
        let next = try n - 1 with _ -> 0
        loopTry next (acc + 1)

type Walker() =
    member this.Go(n: int, acc: int) : int = if n = 0 then acc else this.Go(n - 1, acc + 1)

let big = 1_000_000

let run () =
    check "deep.mutual.recursion" (fun () -> string (isEven big))
    check "deep.cps" (fun () -> string (sumCps 100_000 id))
    check "deep.guarded.tail" (fun () -> string (countDown big 0))
    check "deep.method.tail" (fun () -> string (Walker().Go(big, 0)))
    check "deep.try.tail" (fun () -> string (loopTry 100_000 0))
    check "deep.list.eq" (fun () -> string (List.init big id = List.init big id))
    check "deep.list.compare" (fun () -> string (compare (List.init big id) (List.init big id @ [ 1 ])))
    check "deep.list.hash" (fun () -> string (hash (List.init big id) = hash (List.init big id)))
    check "deep.list.string" (fun () -> (string (List.init big id)).Length |> string)
    check "deep.list.foldBack.sum" (fun () -> string (List.foldBack (fun x acc -> int64 x + acc) (List.init big id) 0L))
    check "deep.list.append" (fun () -> string (List.length (List.init big id @ List.init big id)))
    check "deep.list.mapi2" (fun () -> string (List.length (List.mapi2 (fun i a b -> i + a + b) (List.init big id) (List.init big id))))
    check "deep.list.zip" (fun () -> string (List.length (List.zip (List.init big id) (List.init big id))))
    check "deep.list.scanBack" (fun () -> string (List.length (List.scanBack (+) (List.init big id) 0)))
    check "deep.list.collect" (fun () -> string (List.length (List.collect (fun x -> [ x; x ]) (List.init big id))))
    check "deep.list.sortWith" (fun () -> string (List.head (List.sortWith (fun a b -> compare b a) (List.init big id))))
    check "deep.list.groupBy" (fun () -> string (List.length (List.groupBy (fun x -> x % 10) (List.init big id))))
    check "deep.list.splitAt" (fun () -> string (List.length (fst (List.splitAt (big / 2) (List.init big id)))))
    check "deep.list.except" (fun () -> string (List.length (List.except [ 1 ] (List.init big id))))
    check "deep.list.cons.build"
        (fun () ->
            let mutable xs = []
            for i in 1..big do
                xs <- i :: xs
            string (List.sum xs))
    check "deep.seq.toList" (fun () -> string (Seq.toList (seq { 1..big }) |> List.length))
    check "deep.seq.nested.yieldbang"
        (fun () ->
            let rec gen n = seq { if n > 0 then yield n; yield! gen (n - 1) }
            string (Seq.length (gen 10_000)))
    check "deep.seq.append.chain"
        (fun () ->
            let s = Seq.fold (fun acc i -> Seq.append acc [ i ]) Seq.empty [ 1..10_000 ]
            string (Seq.length s))
    check "deep.list.comprehension.rec"
        (fun () ->
            let rec build n = if n = 0 then [] else [ yield n; yield! build (n - 1) ]
            string (List.length (build 10_000)))
    check "deep.tree.eq" (fun () -> string (deepTree 10_000 Leaf = deepTree 10_000 Leaf))
    check "deep.tree.compare" (fun () -> string (compare (deepTree 10_000 Leaf) (deepTree 10_000 Leaf)))
    check "deep.tree.hash" (fun () -> string (hash (deepTree 10_000 Leaf) = hash (deepTree 10_000 Leaf)))
    check "deep.rose.eq" (fun () -> string (chain 1_000 = chain 1_000))
    check "deep.map.build"
        (fun () ->
            let m = Seq.fold (fun (m: Map<int, int>) i -> Map.add i i m) Map.empty (seq { 1..200_000 })
            string m.Count + "," + string (Map.find 777 m))
    check "deep.set.ops"
        (fun () ->
            let a = Set.ofSeq (seq { 1..200_000 })
            let b = Set.ofSeq (seq { 100_000..300_000 })
            string (Set.count (Set.intersect a b)) + "," + string (Set.count (Set.union a b)))
    check "deep.string.concat"
        (fun () ->
            let mutable s = ""
            for i in 1..100_000 do
                s <- s + "x"
            string s.Length)
    check "deep.array.init" (fun () -> string (Array.sum (Array.init big int64)))
    check "deep.array.sort.stable"
        (fun () ->
            let xs = Array.init 100_000 (fun i -> (i % 7, i))
            let sorted = Array.sortBy fst xs
            string (sorted |> Array.pairwise |> Array.forall (fun ((k1, i1), (k2, i2)) -> k1 < k2 || (k1 = k2 && i1 < i2))))
    check "deep.list.sort.stable"
        (fun () ->
            let xs = List.init 100_000 (fun i -> (i % 7, i))
            let sorted = List.sortBy fst xs
            string (sorted |> List.pairwise |> List.forall (fun ((k1, i1), (k2, i2)) -> k1 < k2 || (k1 = k2 && i1 < i2))))
    check "deep.async.recursion"
        (fun () ->
            let rec loop n = async { if n = 0 then return 0 else let! r = loop (n - 1) in return r + 1 }
            let mutable r = -1 in Async.StartImmediate(async { let! v = loop 100_000 in r <- v }); string r)
    check "deep.async.returnbang"
        (fun () ->
            let rec loop n = async { if n = 0 then return 7 else return! loop (n - 1) }
            let mutable r = -1 in Async.StartImmediate(async { let! v = loop big in r <- v }); string r)
    check "deep.lazy.chain"
        (fun () ->
            let rec mk n = if n = 0 then lazy 0 else let prev = mk (n - 1) in lazy (prev.Value + 1)
            string ((mk 10_000).Value))
