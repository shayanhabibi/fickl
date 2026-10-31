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

    /// <summary>The web <c>EventTarget</c>: an object that dispatches events to the listeners added to it.</summary>
    /// <remarks>Inherit it to make an F# class an event target that JavaScript code subscribes to with
    /// <c>addEventListener</c>, and expose its events to F# with <c>JsEvent.ofTarget this "name"</c>. A global in browsers,
    /// Node.js 15 and later, Deno and Bun.</remarks>
    /// <example><code lang="fsharp">
    /// type Store() =
    ///     inherit JS.EventTarget()
    ///     member this.Changed: IEvent&lt;JS.CustomEvent&lt;int>> = JsEvent.ofTarget this "change"
    ///     member this.Set(value: int) = JsEvent.dispatch this "change" value |> ignore
    /// </code></example>
    [<Global; AllowNullLiteral>]
    type EventTarget() =
        /// <summary>Calls the listener for each event of the type.</summary>
        member _.addEventListener(``type``: string, listener: Event -> unit, ?options: AddEventListenerOptions) : unit =
            nativeOnly

        /// <summary>Stops calling a listener; <c>capture</c> must match the one it was added with.</summary>
        member _.removeEventListener(``type``: string, listener: Event -> unit, ?options: EventListenerOptions) : unit =
            nativeOnly

        /// <summary>Calls the listeners of the event's type, and returns <c>false</c> if one of them called
        /// <c>preventDefault</c> on a cancelable event.</summary>
        member _.dispatchEvent(event: Event) : bool = nativeOnly

    /// <summary>The web <c>Event</c>, which an <c>EventTarget</c> dispatches.</summary>
    and [<Global; AllowNullLiteral>] Event(``type``: string, ?eventInitDict: EventInit) =
        /// <summary>The event's type, such as <c>"click"</c>.</summary>
        member _.``type``: string = nativeOnly
        /// <summary>The target the event was dispatched to.</summary>
        member _.target: EventTarget = nativeOnly
        /// <summary>The target whose listener is running.</summary>
        member _.currentTarget: EventTarget = nativeOnly
        /// <summary>Whether the event can be cancelled with <c>preventDefault</c>.</summary>
        member _.cancelable: bool = nativeOnly
        /// <summary>Whether <c>preventDefault</c> was called on a cancelable event.</summary>
        member _.defaultPrevented: bool = nativeOnly
        /// <summary>The time the event was created, in milliseconds.</summary>
        member _.timeStamp: float = nativeOnly
        /// <summary>Cancels the event, if it is cancelable.</summary>
        member _.preventDefault() : unit = nativeOnly
        /// <summary>Stops the event from reaching the other listeners.</summary>
        member _.stopImmediatePropagation() : unit = nativeOnly
        /// <summary>Stops the event from propagating to other targets.</summary>
        member _.stopPropagation() : unit = nativeOnly

    /// <summary>The web <c>CustomEvent</c>, an event that carries a value in <c>detail</c>.</summary>
    /// <remarks>Create and dispatch one with <c>JsEvent.dispatch</c>. A global in browsers, Node.js 19 and later, Deno and Bun.</remarks>
    and [<Global; AllowNullLiteral>] CustomEvent<'T>(``type``: string, ?eventInitDict: CustomEventInit<'T>) =
        inherit Event(``type``)
        /// <summary>The value the event carries.</summary>
        member _.detail: 'T = nativeOnly

    /// <summary>Options for an <c>Event</c>; build one with <c>jsOptions</c>.</summary>
    and [<AllowNullLiteral; EmitType("EventInit")>] EventInit =
        /// <summary>Whether the event bubbles up a DOM tree.</summary>
        abstract bubbles: bool with get, set
        /// <summary>Whether <c>preventDefault</c> cancels the event.</summary>
        abstract cancelable: bool with get, set
        /// <summary>Whether the event crosses a shadow DOM boundary.</summary>
        abstract composed: bool with get, set

    /// <summary>Options for a <c>CustomEvent</c>; build one with <c>jsOptions</c>.</summary>
    and [<AllowNullLiteral; EmitType("CustomEventInit<$0>")>] CustomEventInit<'T> =
        inherit EventInit
        /// <summary>The value the event carries.</summary>
        abstract detail: 'T with get, set

    /// <summary>Options for <c>removeEventListener</c>; build one with <c>jsOptions</c>.</summary>
    and [<AllowNullLiteral; EmitType("EventListenerOptions")>] EventListenerOptions =
        /// <summary>Whether the listener runs in the capture phase.</summary>
        abstract capture: bool with get, set

    /// <summary>Options for <c>addEventListener</c>; build one with <c>jsOptions</c>.</summary>
    and [<AllowNullLiteral; EmitType("AddEventListenerOptions")>] AddEventListenerOptions =
        inherit EventListenerOptions
        /// <summary>Whether the listener is removed after its first call.</summary>
        abstract once: bool with get, set
        /// <summary>Whether the listener promises not to call <c>preventDefault</c>.</summary>
        abstract passive: bool with get, set
        /// <summary>A signal that removes the listener when it aborts.</summary>
        abstract signal: AbortSignal with get, set

    /// <summary>The web <c>AbortSignal</c>, which cancellable JavaScript APIs such as <c>fetch</c> and <c>addEventListener</c> take.</summary>
    /// <remarks>Convert a <c>CancellationToken</c> to a signal with <c>token.ToAbortSignal()</c>, and back with
    /// <c>CancellationToken.FromAbortSignal(signal)</c>. A global in browsers, Node.js 15 and later, Deno and Bun.
    /// It is an <c>EventTarget</c>, so <c>JsEvent.ofTarget signal "abort"</c> is its abort event.</remarks>
    and [<Global; AllowNullLiteral>] AbortSignal private () =
        inherit EventTarget()
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

    /// <summary>Awaits a promise at the top level of the module, <c>export const config = await loadConfig();</c>,
    /// so the value has the resolved type.</summary>
    /// <remarks>
    /// Allowed only as the whole initializer of a module-level value, and only on JavaScript and TypeScript, whose
    /// output is an ES module; anywhere else it is a compile error:
    /// <code>
    /// let config: Config = JS.awaitTopLevel (importDynamic&lt;Config> "./config.js")
    /// </code>
    /// This changes the order modules run in. A module that imports this one, directly or through others, does not
    /// start until the promise settles, and fails to load if it rejects, so a slow promise delays the whole program
    /// and a watch-mode reload waits for it too. In an import cycle, a module of the cycle can run before the value
    /// is set and read it uninitialized. TypeScript needs <c>module</c> set to <c>es2022</c>, <c>esnext</c>,
    /// <c>preserve</c> or <c>nodenext</c>, and a target of <c>es2017</c> or later. On .NET it throws.
    /// </remarks>
    let awaitTopLevel (promise: Fable.Core.JS.Promise<'T>) : 'T = nativeOnly

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

    /// <summary>A JavaScript <c>symbol</c>: a unique value used as a property key.</summary>
    /// <remarks>Create one with <c>JS.Symbol.create</c>, or a shared one with <c>JS.Symbol.``for``</c>.</remarks>
    [<Global; EmitType("symbol"); AllowNullLiteral>]
    type Symbol private () =
        /// <summary>The description the symbol was created with, if any.</summary>
        member _.description: string option = nativeOnly

        /// <summary>A new symbol, unequal to every other: <c>Symbol(description)</c>.</summary>
        [<Emit("Symbol($0)")>]
        static member create(?description: string) : Symbol = nativeOnly

        /// <summary>The symbol registered under a key, shared across the program: <c>Symbol.for(key)</c>.</summary>
        static member ``for``(key: string) : Symbol = nativeOnly

        /// <summary>The key a symbol from <c>Symbol.for</c> is registered under, or <c>None</c>.</summary>
        static member keyFor(symbol: Symbol) : string option = nativeOnly

        /// <summary>The well-known <c>Symbol.iterator</c>.</summary>
        static member iterator: Symbol = nativeOnly

        /// <summary>The well-known <c>Symbol.asyncIterator</c>.</summary>
        static member asyncIterator: Symbol = nativeOnly

        /// <summary>The well-known <c>Symbol.toStringTag</c>.</summary>
        static member toStringTag: Symbol = nativeOnly

    /// <summary>The TypeScript <c>PropertyKey</c>: a property name, which is a string, a number or a symbol.</summary>
    /// <remarks>A key a proxy trap receives is a string or a symbol; a number key arrives as its string.</remarks>
    type PropertyKey = U3<string, float, Symbol>

    /// <summary>The traps of a <c>Proxy</c>, the TypeScript <c>ProxyHandler&lt;T></c>. Every trap is optional: one that
    /// is not set forwards the operation to the target.</summary>
    /// <remarks>Build a handler with <c>JS.ProxyHandler.create</c>, which sets only the traps assigned. In each trap,
    /// the <c>JS.Reflect</c> member of the same name performs the default operation.</remarks>
    [<AllowNullLiteral; EmitType("ProxyHandler<$0>")>]
    type ProxyHandler<'T> =
        /// <summary>Reads a property: <c>(target, key, receiver) => value</c>.</summary>
        abstract get: ('T -> PropertyKey -> obj -> obj) with get, set
        /// <summary>Writes a property: <c>(target, key, value, receiver) => succeeded</c>. Returning <c>false</c> makes the
        /// write throw a <c>TypeError</c> in strict-mode code, which ES modules are.</summary>
        abstract set: ('T -> PropertyKey -> obj -> obj -> bool) with get, set
        /// <summary>The <c>in</c> operator: <c>(target, key) => present</c>.</summary>
        abstract has: ('T -> PropertyKey -> bool) with get, set
        /// <summary>The <c>delete</c> operator: <c>(target, key) => succeeded</c>.</summary>
        abstract deleteProperty: ('T -> PropertyKey -> bool) with get, set
        /// <summary>Lists own keys, for <c>Object.keys</c> and <c>Reflect.ownKeys</c>: <c>(target) => keys</c>.</summary>
        abstract ownKeys: ('T -> PropertyKey[]) with get, set
        /// <summary>Describes an own property: <c>(target, key) => descriptor</c>, <c>None</c> when it is missing.</summary>
        abstract getOwnPropertyDescriptor: ('T -> PropertyKey -> Fable.Core.JS.PropertyDescriptor option) with get, set
        /// <summary>Defines a property, for <c>Object.defineProperty</c>: <c>(target, key, descriptor) => succeeded</c>.</summary>
        abstract defineProperty: ('T -> PropertyKey -> Fable.Core.JS.PropertyDescriptor -> bool) with get, set
        /// <summary>Calls a function target: <c>(target, thisArg, args) => result</c>.</summary>
        abstract apply: ('T -> obj -> obj[] -> obj) with get, set
        /// <summary>Calls a function target with <c>new</c>: <c>(target, args, newTarget) => instance</c>.</summary>
        abstract construct: ('T -> obj[] -> obj -> obj) with get, set
        /// <summary>Reads the prototype, for <c>Object.getPrototypeOf</c>: <c>(target) => prototype</c>.</summary>
        abstract getPrototypeOf: ('T -> obj) with get, set
        /// <summary>Sets the prototype: <c>(target, prototype) => succeeded</c>.</summary>
        abstract setPrototypeOf: ('T -> obj -> bool) with get, set
        /// <summary>Whether properties can be added: <c>(target) => extensible</c>.</summary>
        abstract isExtensible: ('T -> bool) with get, set
        /// <summary>Stops properties from being added: <c>(target) => succeeded</c>.</summary>
        abstract preventExtensions: ('T -> bool) with get, set

    /// <summary>Builds <c>ProxyHandler</c> objects.</summary>
    [<RequireQualifiedAccess>]
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module ProxyHandler =
        /// <summary>A handler with only the traps the lambda sets, compiled to an object literal as <c>jsOptions</c> is.</summary>
        /// <param name="setters">A lambda that assigns traps on its argument.</param>
        /// <example><code lang="fsharp">
        /// JS.ProxyHandler.create (fun h -> h.get &lt;- fun target key receiver -> JS.Reflect.get (target, key, receiver))
        /// // { get: (target, key, receiver) => Reflect.get(target, key, receiver) }
        /// </code></example>
        let create<'T> (setters: ProxyHandler<'T> -> unit) : ProxyHandler<'T> = nativeOnly

    /// <summary>A <c>Proxy</c> from <c>JS.Proxy.revocable</c>, with the function that revokes it.</summary>
    [<AllowNullLiteral; EmitType("{ proxy: $0; revoke: () => void }")>]
    type RevocableProxy<'T> =
        /// <summary>The proxy.</summary>
        abstract proxy: 'T
        /// <summary>Revokes the proxy: every later operation on it throws a <c>TypeError</c>.</summary>
        abstract revoke: unit -> unit

    /// <summary>The JavaScript <c>Proxy</c>, which wraps an object and intercepts operations on it with traps.</summary>
    /// <remarks>The proxy has the type of its target, and nothing checks that the traps keep to it.</remarks>
    [<RequireQualifiedAccess>]
    module Proxy =
        /// <summary>A proxy for the target that runs the handler's traps: <c>new Proxy(target, handler)</c>.</summary>
        /// <param name="target">The object, array or function the proxy forwards to.</param>
        /// <param name="handler">The traps, from <c>JS.ProxyHandler.create</c>.</param>
        [<Emit("new Proxy($0, $1)")>]
        let create (target: 'T) (handler: ProxyHandler<'T>) : 'T = nativeOnly

        /// <summary>A proxy that can be revoked: <c>Proxy.revocable(target, handler)</c>.</summary>
        /// <param name="target">The object, array or function the proxy forwards to.</param>
        /// <param name="handler">The traps, from <c>JS.ProxyHandler.create</c>.</param>
        [<Emit("Proxy.revocable($0, $1)")>]
        let revocable (target: 'T) (handler: ProxyHandler<'T>) : RevocableProxy<'T> = nativeOnly

    /// <summary>The JavaScript <c>Reflect</c>: the default behaviour of each <c>Proxy</c> trap, as a function.</summary>
    [<Global>]
    type Reflect private () =
        /// <summary>Reads a property, calling a getter with the receiver as <c>this</c>.</summary>
        static member get(target: obj, key: PropertyKey, ?receiver: obj) : obj = nativeOnly
        /// <summary>Writes a property, and returns whether it was written.</summary>
        static member set(target: obj, key: PropertyKey, value: obj, ?receiver: obj) : bool = nativeOnly
        /// <summary>Whether the object or its prototype chain has the property, as the <c>in</c> operator.</summary>
        static member has(target: obj, key: PropertyKey) : bool = nativeOnly
        /// <summary>Deletes a property, and returns whether it is gone.</summary>
        static member deleteProperty(target: obj, key: PropertyKey) : bool = nativeOnly
        /// <summary>The object's own keys, strings and symbols, enumerable or not.</summary>
        static member ownKeys(target: obj) : PropertyKey[] = nativeOnly

        /// <summary>The descriptor of an own property, or <c>None</c>.</summary>
        static member getOwnPropertyDescriptor
            (target: obj, key: PropertyKey)
            : Fable.Core.JS.PropertyDescriptor option
            =
            nativeOnly

        /// <summary>Defines a property, and returns whether it was defined.</summary>
        static member defineProperty
            (target: obj, key: PropertyKey, attributes: Fable.Core.JS.PropertyDescriptor)
            : bool
            =
            nativeOnly

        /// <summary>Calls a function with a <c>this</c> and arguments.</summary>
        static member apply(target: obj, thisArgument: obj, arguments: obj[]) : obj = nativeOnly
        /// <summary>Calls a constructor with <c>new</c>, and optionally another constructor as <c>new.target</c>.</summary>
        static member construct(target: obj, arguments: obj[], ?newTarget: obj) : obj = nativeOnly
        /// <summary>The object's prototype.</summary>
        static member getPrototypeOf(target: obj) : obj = nativeOnly
        /// <summary>Sets the object's prototype, and returns whether it was set.</summary>
        static member setPrototypeOf(target: obj, prototype: obj) : bool = nativeOnly
        /// <summary>Whether properties can be added to the object.</summary>
        static member isExtensible(target: obj) : bool = nativeOnly
        /// <summary>Stops properties from being added, and returns whether that succeeded.</summary>
        static member preventExtensions(target: obj) : bool = nativeOnly
