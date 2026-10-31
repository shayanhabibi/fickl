namespace Fickl.Core

open Fable.Core

/// <summary>Controls JavaScript callback representation.</summary>
[<Fable.Core.Erase>]
module JsInterop =
    /// <summary>Compiles a literal callback as an ordinary JavaScript function.</summary>
    /// <param name="callback">A lambda expression; references to existing functions are rejected.</param>
    /// <remarks>The function has its own <c>this</c>, <c>arguments</c>, and prototype. Targets JavaScript and TypeScript.</remarks>
    /// <example><code lang="fsharp">
    /// let handler = jsFunction (fun (event: obj) -> emitJsExpr event "this.handle($0)")
    /// // function (event) { return this.handle(event); }
    /// </code></example>
    let jsFunction (callback: 'Arg -> 'Result) : 'Arg -> 'Result = nativeOnly

    /// <summary>Compiles a literal callback as a JavaScript function whose receiver supplies its first argument.</summary>
    /// <param name="callback">A lambda with a receiver argument followed by its ordinary arguments.</param>
    /// <remarks>The receiver becomes a typed TypeScript <c>this</c> parameter and a captured local in the body.
    /// Call through a JavaScript API that supplies a receiver; ordinary F# application supplies no receiver.</remarks>
    /// <example><code lang="fsharp">
    /// let handler = withThis (fun (self: Element) (event: Event) -> self.Handle(event))
    /// // function (this: Element, event: Event) { let self = this; return self.Handle(event); }
    /// </code></example>
    let withThis (callback: 'This -> 'Arg -> 'Result) : 'Arg -> 'Result = nativeOnly

    /// <summary>Builds an object with some of the properties of <c>'T</c>, typed <c>Partial&lt;T></c> in TypeScript.</summary>
    /// <param name="setters">A lambda that sets properties of its argument, as for <c>jsOptions</c>.</param>
    /// <remarks>Unlike <c>jsOptions</c>, the result does not claim to be a complete <c>'T</c>. TypeScript checks the object
    /// literal with <c>satisfies Partial&lt;T></c>, so a misspelled key or a mistyped value is an error.</remarks>
    /// <example><code lang="fsharp">
    /// comp.setState (partial (fun s -> s.count &lt;- 1))
    /// // comp.setState({ count: 1 } satisfies Partial&lt;State>)
    /// </code></example>
    let partial<'T> (setters: 'T -> unit) : Partial<'T> = nativeOnly

    /// <summary>Treats a <c>Partial&lt;T></c> as a complete <c>'T</c>; TypeScript sees the cast.</summary>
    /// <param name="value">An object that the caller knows has every property of <c>'T</c>.</param>
    let full (value: Partial<'T>) : 'T = nativeOnly
