/// Does Observable.throttle (Fable.Compiler.Util) or FsWatcher (Fable.Cli.Main) ever lose a change?
///   unit <instances> <delayMs> <eventsPerInstance> [producersPerInstance]
///   fsw  <delayMs> <rounds> <files> <dir>
// Prints JSON; this program fails on loss/spurious events. check_results.py also rejects duplicates
// and overlapping deliveries, which the original executable exit status did not include.
open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Threading
open Fable.Compiler.Util

let spin (ticks: int64) =
    let sw = Stopwatch.StartNew()

    while sw.ElapsedTicks < ticks do
        Thread.SpinWait 20

type Instance(delayMs: int, producers: int, events: int, seed: int) =
    let subject = Observable.SingleObservable<int>(ignore)
    let delivered = ConcurrentQueue<int>()
    let mutable inside = 0
    let mutable overlaps = 0
    let mutable batches = 0
    let triggered = ConcurrentQueue<int>()

    let subscription =
        (Observable.throttle delayMs subject)
            .Subscribe(fun (batch: int[]) ->
                if Interlocked.Increment(&inside) > 1 then
                    Interlocked.Increment(&overlaps) |> ignore

                Interlocked.Increment(&batches) |> ignore

                for v in batch do
                    delivered.Enqueue v

                Interlocked.Decrement(&inside) |> ignore
            )

    member _.Run() =
        let delayTicks = int64 delayMs * Stopwatch.Frequency / 1000L

        let work (producer: int) =
            let random = Random(seed * 31 + producer)

            for i in 0 .. events - 1 do
                let id = producer * events + i
                triggered.Enqueue id
                subject.Trigger id
                // Gaps around the debounce interval maximise timer firings between triggers.
                match random.Next 4 with
                | 0 -> ()
                | 1 -> spin (int64 (random.NextDouble() * float delayTicks))
                | _ -> spin (int64 ((0.5 + random.NextDouble() * 1.5) * float delayTicks))

        [| for p in 0 .. producers - 1 -> Thread(fun () -> work p) |]
        |> Array.map (fun t ->
            t.Start()
            t
        )
        |> Array.iter (fun t -> t.Join())

    member _.Result() =
        let sent = HashSet(triggered)
        let got = List(delivered)
        let gotSet = HashSet(got)
        let lost = sent |> Seq.filter (gotSet.Contains >> not) |> Seq.length
        let spurious = gotSet |> Seq.filter (sent.Contains >> not) |> Seq.length
        sent.Count, got.Count, lost, got.Count - gotSet.Count, spurious, overlaps, batches

    interface IDisposable with
        member _.Dispose() = subscription.Dispose()

let unit instances delayMs events producers =
    let all =
        [| for i in 0 .. instances - 1 -> new Instance(delayMs, producers, events, i) |]

    let sw = Stopwatch.StartNew()

    all
    |> Array.map (fun i ->
        let t = Thread(fun () -> i.Run())
        t.Start()
        t
    )
    |> Array.iter (fun t -> t.Join())

    Thread.Sleep(max 500 (delayMs * 50))
    let results = all |> Array.map (fun i -> i.Result())
    let sum f = results |> Array.sumBy f
    let sent = sum (fun (s, _, _, _, _, _, _) -> s)
    let got = sum (fun (_, g, _, _, _, _, _) -> g)
    let lost = sum (fun (_, _, l, _, _, _, _) -> l)
    let dup = sum (fun (_, _, _, d, _, _, _) -> d)
    let spurious = sum (fun (_, _, _, _, s, _, _) -> s)
    let overlaps = sum (fun (_, _, _, _, _, o, _) -> o)
    let batches = sum (fun (_, _, _, _, _, _, b) -> b)

    let lossyInstances =
        results |> Array.filter (fun (_, _, l, _, _, _, _) -> l > 0) |> Array.length

    printfn
        """{"mode":"unit","instances":%d,"producers":%d,"delayMs":%d,"sent":%d,"delivered":%d,"lost":%d,"duplicates":%d,"spurious":%d,"overlappingOnNext":%d,"batches":%d,"lossyInstances":%d,"seconds":%.1f}"""
        instances
        producers
        delayMs
        sent
        got
        lost
        dup
        spurious
        overlaps
        batches
        lossyInstances
        sw.Elapsed.TotalSeconds

    for i in all do
        (i :> IDisposable).Dispose()

    if lost > 0 || spurious > 0 then
        1
    else
        0

let fsw delayMs rounds files (dir: string) =
    let dir = Path.GetFullPath dir
    Directory.CreateDirectory dir |> ignore

    let paths =
        [ for i in 0 .. files - 1 -> Path.Combine(dir, $"F{i}.fs").Replace('\\', '/') ] // Fable's Path.normalizePath

    for p in paths do
        File.WriteAllText(p, "module X\n")

    let watcher = Fable.Cli.Main.FsWatcher(delayMs)
    let delivered = ConcurrentQueue<string * int>()
    let mutable round = 0
    let random = Random(7)

    use _subscription =
        watcher
            .Observe(paths)
            .Subscribe(fun (changes: ISet<string>) ->
                for c in changes do
                    delivered.Enqueue((c, Volatile.Read(&round)))
            )

    Thread.Sleep 500
    let delayTicks = int64 delayMs * Stopwatch.Frequency / 1000L
    let mutable roundsLosing = 0
    let mutable lostTotal = 0
    let sw = Stopwatch.StartNew()

    for r in 1..rounds do
        Volatile.Write(&round, r)
        // Drain earlier deliveries, then write a random subset with gaps around the debounce interval.
        while delivered.TryDequeue() |> fst do
            ()

        let written = HashSet<string>(StringComparer.OrdinalIgnoreCase)

        for p in paths do
            if random.Next 3 > 0 then
                File.WriteAllText(p, $"module X\nlet v = {r}\n")
                written.Add p |> ignore

                match random.Next 3 with
                | 0 -> ()
                | _ -> spin (int64 ((0.3 + random.NextDouble() * 1.4) * float delayTicks))

        Thread.Sleep(max 300 (delayMs * 20))

        let got =
            HashSet<string>(delivered |> Seq.map fst, StringComparer.OrdinalIgnoreCase)

        let lost = written |> Seq.filter (got.Contains >> not) |> Seq.length

        if lost > 0 then
            roundsLosing <- roundsLosing + 1
            lostTotal <- lostTotal + lost

    printfn
        """{"mode":"fsw","delayMs":%d,"rounds":%d,"files":%d,"roundsLosing":%d,"lostWrites":%d,"seconds":%.1f}"""
        delayMs
        rounds
        files
        roundsLosing
        lostTotal
        sw.Elapsed.TotalSeconds

    if lostTotal > 0 then
        1
    else
        0

[<EntryPoint>]
let main argv =
    match argv with
    | [| "unit"; instances; delay; events |] -> unit (int instances) (int delay) (int events) 1
    | [| "unit"; instances; delay; events; producers |] -> unit (int instances) (int delay) (int events) (int producers)
    | [| "fsw"; delay; rounds; files; dir |] -> fsw (int delay) (int rounds) (int files) dir
    | _ ->
        eprintfn "usage: unit <instances> <delayMs> <events> [producers] | fsw <delayMs> <rounds> <files> <dir>"
        2
