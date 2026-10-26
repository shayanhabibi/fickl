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
