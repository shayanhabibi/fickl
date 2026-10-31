namespace Fickl.Core

#nowarn "1182"

open Fable.Core

/// <summary>JavaScript bindings: the errors JavaScript throws, to catch them by type with <c>:?</c>, <c>AbortSignal</c>, and <c>ReadonlyArray</c>.</summary>
/// <remarks>Each type is the JavaScript global of the same name, so <c>:? JS.TypeError</c> compiles to
/// <c>instanceof TypeError</c>. They inherit <c>exn</c> only so that a handler can test for them; an F# exception is
/// not a <c>JS.Error</c> unless it is compiled with <c>--nativeErrors</c> or <c>[&lt;JsError>]</c>.
/// Use <c>Thrown</c> for a thrown value that is not an error at all. Targets JavaScript and TypeScript.</remarks>
/// <example><code lang="fsharp">
/// try f () with
/// | :? JS.TypeError as e -> e.message
/// | :? JS.DOMException as e when e.name = "AbortError" -> "aborted"
/// | JS.Thrown value -> string value
/// | e -> e.Message
/// </code></example>
[<RequireQualifiedAccess>]
module JS =
    /// <summary>The JavaScript <c>Error</c>, the base of the errors JavaScript throws.</summary>
    [<Global; AllowNullLiteral>]
    type Error(message: string) =
        inherit System.Exception(message)
        new() = Error("")
        /// <summary>The error's message.</summary>
        member _.message: string = nativeOnly
        /// <summary>The error's name, such as <c>"TypeError"</c> or, for a <c>DOMException</c>, <c>"AbortError"</c>.</summary>
        member _.name: string = nativeOnly
        /// <summary>The stack trace, where the engine provides one.</summary>
        member _.stack: string = nativeOnly
        /// <summary>The value the error was created with as its cause, if any.</summary>
        member _.cause: obj = nativeOnly

    /// <summary>The JavaScript <c>TypeError</c>: a value is not of the expected type.</summary>
    [<Global; AllowNullLiteral>]
    type TypeError(message: string) =
        inherit Error(message)
        new() = TypeError("")

    /// <summary>The JavaScript <c>RangeError</c>: a value is outside its allowed range.</summary>
    [<Global; AllowNullLiteral>]
    type RangeError(message: string) =
        inherit Error(message)
        new() = RangeError("")

    /// <summary>The JavaScript <c>SyntaxError</c>, thrown for example by <c>JSON.parse</c>.</summary>
    [<Global; AllowNullLiteral>]
    type SyntaxError(message: string) =
        inherit Error(message)
        new() = SyntaxError("")

    /// <summary>The JavaScript <c>ReferenceError</c>: an undeclared variable was read.</summary>
    [<Global; AllowNullLiteral>]
    type ReferenceError(message: string) =
        inherit Error(message)
        new() = ReferenceError("")

    /// <summary>The JavaScript <c>AggregateError</c>, which holds several errors, thrown for example by <c>Promise.any</c>.</summary>
    [<Global; AllowNullLiteral>]
    type AggregateError(errors: obj[], message: string) =
        inherit Error(message)
        new(errors: obj[]) = AggregateError(errors, "")
        /// <summary>The errors this error holds.</summary>
        member _.errors: obj[] = nativeOnly

    /// <summary>The web <c>DOMException</c>, told apart by its <c>name</c>, such as <c>"AbortError"</c> or <c>"NotFoundError"</c>.</summary>
    /// <remarks>A global in browsers, Node.js 17 and later, Deno and Bun.</remarks>
    [<Global; AllowNullLiteral>]
    type DOMException(message: string, name: string) =
        inherit Error(message)
        new(message: string) = DOMException(message, "Error")
        new() = DOMException("", "Error")
        /// <summary>The legacy numeric code of the error, <c>0</c> for names that have none.</summary>
        member _.code: int = nativeOnly

    /// <summary>The web <c>AbortSignal</c>, which cancellable JavaScript APIs such as <c>fetch</c> and <c>addEventListener</c> take.</summary>
    /// <remarks>Convert a <c>CancellationToken</c> to a signal with <c>token.ToAbortSignal()</c>, and back with
    /// <c>CancellationToken.FromAbortSignal(signal)</c>. A global in browsers, Node.js 15 and later, Deno and Bun.</remarks>
    [<Global; AllowNullLiteral>]
    type AbortSignal private () =
        /// <summary>Whether the signal has aborted.</summary>
        member _.aborted: bool = nativeOnly
        /// <summary>Why the signal aborted: by default a <c>DOMException</c> named <c>"AbortError"</c>, or <c>"TimeoutError"</c> for <c>AbortSignal.timeout</c>.</summary>
        member _.reason: obj = nativeOnly
        /// <summary>Throws the abort reason if the signal has aborted.</summary>
        member _.throwIfAborted() : unit = nativeOnly

        /// <summary>Calls a function once, when the signal aborts.</summary>
        [<Emit("$0.addEventListener('abort', $1, { once: true })")>]
        member _.onAbort(listener: unit -> unit) : unit = nativeOnly

        /// <summary>A signal that has already aborted, with the given reason or an <c>"AbortError"</c> <c>DOMException</c>.</summary>
        static member abort(?reason: obj) : AbortSignal = nativeOnly
        /// <summary>A signal that aborts with a <c>"TimeoutError"</c> <c>DOMException</c> after the given time.</summary>
        static member timeout(milliseconds: int) : AbortSignal = nativeOnly
        /// <summary>A signal that aborts when any of the given signals does, with its reason.</summary>
        static member any(signals: AbortSignal[]) : AbortSignal = nativeOnly

    /// <summary>The web <c>AbortController</c>, which owns an <c>AbortSignal</c> and aborts it.</summary>
    [<Global; AllowNullLiteral>]
    type AbortController() =
        /// <summary>The signal this controller aborts.</summary>
        member _.signal: AbortSignal = nativeOnly
        /// <summary>Aborts the signal, with the given reason or an <c>"AbortError"</c> <c>DOMException</c>.</summary>
        member _.abort(?reason: obj) : unit = nativeOnly

    /// <summary>Matches a thrown value that is not an error: neither a JS <c>Error</c> nor an F# exception, such as a
    /// string, a plain object or <c>undefined</c>. The value is passed as it was thrown.</summary>
    /// <remarks>On .NET every thrown value is an exception, so this never matches. An F# <c>async</c> turns a thrown
    /// value that is not an error into an exception, so it does not match there either.</remarks>
    let (|Thrown|_|) (e: exn) : obj option =
        let value = box e

        if value :? exn then
            None
        else
            Some value

    /// <summary>A JavaScript array that is only read: the TypeScript <c>ReadonlyArray&lt;T></c>, also written <c>readonly T[]</c>.</summary>
    /// <remarks>A parameter of this type accepts a TypeScript <c>readonly T[]</c> or <c>as const</c> array, which a
    /// <c>'T[]</c> parameter rejects. Build one from an F# array or <c>ResizeArray</c> with <c>ReadonlyArray.ofArray</c> or
    /// <c>ReadonlyArray.ofResizeArray</c>, which compile to nothing in JavaScript.
    /// The members are those an F# array has whatever its representation: a numeric F# array is a typed array in
    /// JavaScript, which has no <c>map</c> returning another element type, or <c>concat</c>. Use the <c>Seq</c> functions for those.</remarks>
    [<EmitType("ReadonlyArray<$0>")>]
    type ReadonlyArray<'T> =
        inherit seq<'T>
        abstract length: int

        /// <summary>The element at an index.</summary>
        [<EmitIndexer>]
        abstract Item: index: int -> 'T with get

        abstract at: index: int -> 'T option
        abstract includes: searchElement: 'T -> bool
        abstract indexOf: searchElement: 'T -> int
        abstract join: separator: string -> string
        abstract slice: ?start: int * ?``end``: int -> ReadonlyArray<'T>
        abstract find: predicate: ('T -> bool) -> 'T option
        abstract findIndex: predicate: ('T -> bool) -> int
        abstract some: predicate: ('T -> bool) -> bool
        abstract every: predicate: ('T -> bool) -> bool
        abstract forEach: callbackfn: ('T -> unit) -> unit

    /// <summary>Conversions to and from <c>ReadonlyArray</c>.</summary>
    [<Erase>]
    module ReadonlyArray =
        /// <summary>Views an array as read-only, without a copy.</summary>
        /// <param name="array">The array; later changes to it show through the view.</param>
        let ofArray (array: 'T[]) : ReadonlyArray<'T> = nativeOnly

        /// <summary>Views a <c>ResizeArray</c> as read-only, without a copy.</summary>
        /// <param name="list">The list; later changes to it show through the view.</param>
        let ofResizeArray (list: ResizeArray<'T>) : ReadonlyArray<'T> = nativeOnly

        /// <summary>Copies the elements into a new F# array.</summary>
        /// <param name="array">The read-only array to copy.</param>
        let toArray (array: ReadonlyArray<'T>) : 'T[] = nativeOnly

    /// <summary>The JavaScript <c>WeakRef</c>: a reference that does not keep its target from being collected.</summary>
    /// <remarks><c>System.WeakReference&lt;'T></c> compiles to this too, so use either. A global from ES2021.</remarks>
    [<Global; AllowNullLiteral>]
    type WeakRef<'T when 'T: not struct>(target: 'T) =
        /// <summary>The target, or <c>None</c> once it has been collected.</summary>
        member _.deref() : 'T option = nativeOnly

    /// <summary>The JavaScript <c>FinalizationRegistry</c>: calls back, some time after a registered object is
    /// collected, with the value it was registered with.</summary>
    /// <remarks>The callback may run late or never, for example when the program ends first: use it to release
    /// resources that are also safe to leak, not for logic the program depends on. A global from ES2021.</remarks>
    /// <example><code lang="fsharp">
    /// let registry = JS.FinalizationRegistry(fun (held: string) -> printfn "collected %s" held)
    /// registry.register(node, "node-1")
    /// </code></example>
    [<Global; AllowNullLiteral>]
    type FinalizationRegistry<'T>(cleanupCallback: 'T -> unit) =
        /// <summary>Calls the cleanup callback with a held value once the target is collected.</summary>
        /// <param name="target">The object to watch. It must not be the held value, which would keep it alive.</param>
        /// <param name="heldValue">The value the callback receives.</param>
        /// <param name="unregisterToken">An object to pass to <c>unregister</c>, often the target itself.</param>
        member _.register(target: obj, heldValue: 'T, ?unregisterToken: obj) : unit = nativeOnly

        /// <summary>Stops watching the targets registered with a token, and returns whether there were any.</summary>
        /// <param name="unregisterToken">The token the targets were registered with.</param>
        member _.unregister(unregisterToken: obj) : bool = nativeOnly

    /// <summary>A deep copy of a value by the structured clone algorithm: <c>structuredClone(value)</c>.</summary>
    /// <remarks>The copy has the data properties but not the prototypes, so an F# record, union, list, map or set is not
    /// a working F# value afterwards; an F# map or set, which holds its comparer function, throws a
    /// <c>DataCloneError</c>. Use <c>Transfer.clone</c> for F# values, and <c>Transfer.toPlain</c> before
    /// <c>postMessage</c>. A global in browsers, Node.js 17 and later, Deno and Bun.</remarks>
    /// <param name="value">The value to copy.</param>
    [<Emit("structuredClone($0)")>]
    let structuredClone (value: 'T) : 'T = nativeOnly

    /// <summary>A deep copy of a value that moves the given objects, such as an <c>ArrayBuffer</c> or a
    /// <c>MessagePort</c>, into the copy instead of copying them: <c>structuredClone(value, { transfer })</c>.</summary>
    /// <param name="transfer">The objects to move; the originals become unusable.</param>
    /// <param name="value">The value to copy.</param>
    [<Emit("structuredClone($1, { transfer: $0 })")>]
    let structuredCloneTransfer (transfer: obj[]) (value: 'T) : 'T = nativeOnly
