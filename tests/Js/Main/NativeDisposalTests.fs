module Fable.Tests.NativeDisposal

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Util.Testing

type Resource(dispose: unit -> unit) =
    interface IDisposable with member _.Dispose() = dispose()

type AsyncResource(dispose: unit -> Task) =
    interface IAsyncDisposable with member _.DisposeAsync() = ValueTask(dispose())

[<AttachMembers>]
type CustomResource(dispose: unit -> unit, nativeDispose: unit -> unit) =
    member _.``Symbol.dispose``() = nativeDispose()
    interface IDisposable with member _.Dispose() = dispose()

let private useAsyncResource<'T when 'T :> IAsyncDisposable> (resource: 'T) = task {
    use _resource = resource
    return 42
}

#if FABLE_COMPILER
let private getterResource (key: string) (throwsOnRepeat: bool) : obj =
    emitJsExpr (key, throwsOnRepeat) "(() => { const disposalKey = $0 === 'Symbol.dispose' ? Symbol.dispose : $0 === 'Symbol.asyncDispose' ? Symbol.asyncDispose : $0; const disposalResource = { reads: 0, called: 0 }; return Object.defineProperty(disposalResource, disposalKey, { get() { const read = ++disposalResource.reads; if ($1 && read > 1) throw new Error('Repeated disposal getter'); return { called: 0, dispose() { this.called = read; return Promise.resolve(); } }.dispose; } }); })()"

let private assertGetterResource (resource: obj) =
    emitJsExpr resource "Object($0).reads" |> equal 1
    emitJsExpr resource "Object($0).called" |> equal 1

let private overrideDisposalCall (resource: obj) (key: string) : unit =
    emitJsStatement (resource, key) "{ const disposalKey = $1 === 'Symbol.dispose' ? Symbol.dispose : $1 === 'Symbol.asyncDispose' ? Symbol.asyncDispose : $1; const method = { called: 0, dispose() { this.called = 1; } }.dispose; Object.defineProperty(method, 'call', { value: null }); Object.defineProperty($0, disposalKey, { value: method }); }"
#endif

let tests =
    testList "NativeDisposal" [
#if FABLE_COMPILER
        testCase "synchronous cleanup ignores the method's own call property" <| fun () ->
            for key in [ "Dispose"; "Symbol.dispose" ] do
                let resource: obj = emitJsExpr () "Object({ called: 0 })"
                overrideDisposalCall resource key
                (unbox<IDisposable> resource).Dispose()
                emitJsExpr resource "Object($0).called" |> equal 1

        testCaseAsync "asynchronous cleanup ignores the method's own call property" <| fun () -> async {
            for key in [ "DisposeAsync"; "Symbol.asyncDispose" ] do
                let resource: obj = emitJsExpr () "Object({ called: 0 })"
                overrideDisposalCall resource key
                do! Async.AwaitTask((unbox<IAsyncDisposable> resource).DisposeAsync().AsTask())
                emitJsExpr resource "Object($0).called" |> equal 1
        }

        testCase "Dispose getters are read once and invoke the first method with its receiver" <| fun () ->
            for throwsOnRepeat in [ false; true ] do
                let resource = getterResource "Dispose" throwsOnRepeat
                (unbox<IDisposable> resource).Dispose()
                assertGetterResource resource

        testCase "native disposal getters are read once and invoke the first method with its receiver" <| fun () ->
            for throwsOnRepeat in [ false; true ] do
                let resource = getterResource "Symbol.dispose" throwsOnRepeat
                let run () =
                    use _resource = unbox<IDisposable> resource
                    ()
                run()
                assertGetterResource resource

        testCaseAsync "DisposeAsync getters are read once and invoke the first method with its receiver" <| fun () -> async {
            for throwsOnRepeat in [ false; true ] do
                let resource = getterResource "DisposeAsync" throwsOnRepeat
                do! Async.AwaitTask((unbox<IAsyncDisposable> resource).DisposeAsync().AsTask())
                assertGetterResource resource
        }

        testCaseAsync "native asynchronous disposal getters are read once and invoke the first method with its receiver" <| fun () -> async {
            for throwsOnRepeat in [ false; true ] do
                let resource = getterResource "Symbol.asyncDispose" throwsOnRepeat
                let! _ = Async.AwaitTask(useAsyncResource (unbox<IAsyncDisposable> resource))
                assertGetterResource resource
        }

        testCase "FSharp use consumes a native disposable once with its receiver" <| fun () ->
            let resource: IDisposable = emitJsExpr () "Object({ count: 0, [Symbol.dispose]() { this.count++; } })"
            let run () =
                use _resource = resource
                42
            run() |> equal 42
            emitJsExpr resource "Object($0).count" |> equal 1

        testCase "FSharp Dispose takes precedence over native disposal" <| fun () ->
            let resource: IDisposable = emitJsExpr () "Object({ count: 0, Dispose() { this.count++; }, [Symbol.dispose]() { this.count += 10; } })"
            resource.Dispose()
            emitJsExpr resource "Object($0).count" |> equal 1

        testCase "native using consumes a compiled class" <| fun () ->
            let mutable count = 0
            let resource = Resource(fun () -> count <- count + 1)
            emitJsStatement resource "{ using resource = $0; }"
            count |> equal 1

        testCase "native symbols consume FSharp object expressions" <| fun () ->
            let mutable count = 0
            let resource = { new IDisposable with member _.Dispose() = count <- count + 1 }
            emitJsStatement resource "Object($0)[Symbol.dispose]()"
            count |> equal 1

        testCase "explicit native symbol implementations are preserved" <| fun () ->
            let mutable count = 0
            let resource = CustomResource((fun () -> count <- count + 1), (fun () -> count <- count + 10))
            emitJsStatement resource "{ using resource = $0; }"
            count |> equal 10

        testCase "native disposal runs when an FSharp body throws" <| fun () ->
            let resource: IDisposable = emitJsExpr () "Object({ count: 0, [Symbol.dispose]() { this.count++; } })"
            try
                use _resource = resource
                failwith "body"
            with _ -> ()
            emitJsExpr resource "Object($0).count" |> equal 1

        testCase "native using consumes runtime cancellation registrations" <| fun () ->
            use source = new System.Threading.CancellationTokenSource()
            let mutable count = 0
            let registration = source.Token.Register(fun () -> count <- count + 1)
            emitJsStatement registration "{ using resource = $0; }"
            source.Cancel()
            count |> equal 0

        testCase "native using consumes runtime cancellation sources" <| fun () ->
            let source = new System.Threading.CancellationTokenSource()
            emitJsStatement source "{ using resource = $0; }"
            source.IsCancellationRequested |> equal false

        testCaseAsync "async use consumes native synchronous disposables" <| fun () -> async {
            let resource: IDisposable = emitJsExpr () "Object({ count: 0, [Symbol.dispose]() { this.count++; } })"
            let! _ = async {
                use _resource = resource
                return 42
            }
            emitJsExpr resource "Object($0).count" |> equal 1
        }

        testCaseAsync "task use bang awaits native asynchronous disposal" <| fun () -> async {
            let resource: IAsyncDisposable = emitJsExpr () "Object({ count: 0, async [Symbol.asyncDispose]() { await Promise.resolve(); this.count++; } })"
            let! value = Async.AwaitTask(task {
                use! _resource = Task.FromResult resource
                return 42
            })
            value |> equal 42
            emitJsExpr resource "Object($0).count" |> equal 1
        }

        testCaseAsync "native await using consumes a compiled asynchronous class" <| fun () -> async {
            let mutable count = 0
            let resource = AsyncResource(fun () -> task {
                do! Task.Delay 1
                count <- count + 1
            })
            let promise: JS.Promise<unit> = emitJsExpr resource "(async () => { await using resource = $0; })()"
            do! Async.AwaitPromise promise
            count |> equal 1
        }

        testCaseAsync "generic task resources await asynchronous cleanup" <| fun () -> async {
            let resource: IAsyncDisposable = emitJsExpr () "Object({ count: 0, async [Symbol.asyncDispose]() { await Promise.resolve(); this.count++; } })"
            let! value = Async.AwaitTask(useAsyncResource resource)
            value |> equal 42
            emitJsExpr resource "Object($0).count" |> equal 1
        }

        testCaseAsync "native asynchronous symbols consume FSharp object expressions" <| fun () -> async {
            let mutable count = 0
            let resource =
                { new IAsyncDisposable with
                    member _.DisposeAsync() =
                        ValueTask(task {
                            do! Task.Delay 1
                            count <- count + 1
                        }) }
            let promise: JS.Promise<unit> = emitJsExpr resource "Object($0)[Symbol.asyncDispose]()"
            do! Async.AwaitPromise promise
            count |> equal 1
        }

        testCaseAsync "task cleanup is awaited after body failure" <| fun () -> async {
            let resource: IAsyncDisposable = emitJsExpr () "Object({ count: 0, async [Symbol.asyncDispose]() { await Promise.resolve(); this.count++; } })"
            let! failed = Async.AwaitTask(task {
                try
                    use _resource = resource
                    return failwith "body"
                with _ -> return true
            })
            failed |> equal true
            emitJsExpr resource "Object($0).count" |> equal 1
        }

        testCaseAsync "DisposeAsync takes precedence over the native asynchronous method" <| fun () -> async {
            let resource: IAsyncDisposable = emitJsExpr () "Object({ count: 0, async DisposeAsync() { this.count++; }, async [Symbol.asyncDispose]() { this.count += 10; } })"
            do! Async.AwaitTask(resource.DisposeAsync().AsTask())
            emitJsExpr resource "Object($0).count" |> equal 1
        }

        testCaseAsync "completed ValueTasks support native asynchronous object disposal" <| fun () -> async {
            let mutable count = 0
            let resource =
                { new IAsyncDisposable with
                    member _.DisposeAsync() =
                        count <- count + 1
                        ValueTask() }
            let promise: JS.Promise<unit> = emitJsExpr resource "Object($0)[Symbol.asyncDispose]()"
            do! Async.AwaitPromise promise
            count |> equal 1
        }
#endif
    ]
