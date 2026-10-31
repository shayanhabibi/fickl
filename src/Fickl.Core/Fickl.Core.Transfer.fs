namespace Fickl.Core

#nowarn "1182"

open Fable.Core

/// <summary>F# values as plain data that <c>structuredClone</c>, <c>postMessage</c>, <c>BroadcastChannel</c> and
/// IndexedDB copy without loss, and back. Targets JavaScript and TypeScript.</summary>
/// <remarks>The conversion follows the static type <c>'T</c>, which the compiler passes as reflection info:
/// <list type="bullet">
/// <item>a record is an object with its field names, and an anonymous record the same;</item>
/// <item>a union case is <c>{ case: "Name", fields: [...] }</c>;</item>
/// <item>a list, set, array or tuple is an array, and a map an array of <c>[key, value]</c> pairs;</item>
/// <item>None is <c>null</c>, and Some the value, or <c>{ value }</c> where the value is itself an option, unit or <c>obj</c>;</item>
/// <item>a decimal is its string.</item>
/// </list>
/// Other values, such as numbers, strings, dates and class instances, are kept as they are; a class instance still
/// loses its prototype in a clone. Where <c>'T</c> is a type parameter, call from an <c>inline</c> function so the
/// compiler knows the type.</remarks>
/// <example><code lang="fsharp">
/// worker.postMessage (Transfer.toPlain model)
/// let model: Model = Transfer.ofPlain event.data
/// let copy = Transfer.clone model
/// </code></example>
[<RequireQualifiedAccess>]
module Transfer =
    /// <summary>Copies an F# value into plain objects and arrays.</summary>
    /// <param name="value">The value to convert.</param>
    let toPlain<'T> (value: 'T) : obj = nativeOnly

    /// <summary>Rebuilds an F# value from the plain data <c>toPlain</c> gives, restoring prototypes.</summary>
    /// <param name="data">The plain data, for example a message's <c>data</c>.</param>
    let ofPlain<'T> (data: obj) : 'T = nativeOnly

    /// <summary>A deep copy of an F# value through <c>structuredClone</c>, with its prototypes restored.</summary>
    /// <param name="value">The value to copy.</param>
    let clone<'T> (value: 'T) : 'T = nativeOnly
