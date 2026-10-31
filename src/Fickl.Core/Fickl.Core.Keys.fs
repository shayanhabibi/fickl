namespace Fickl.Core

#nowarn "1182"

open Fable.Core

/// <summary>The TypeScript <c>keyof T</c>: the name of a property of <c>'T</c>, a string at runtime.</summary>
/// <remarks>TypeScript annotations write it <c>keyof T &amp; string</c>, as F# property names are strings.
/// Build one with <c>keyof</c> from a property access, or with <c>KeyOf.unsafeOfString</c>. Targets JavaScript and TypeScript.</remarks>
[<Erase; EmitType("keyof $0 & string")>]
type keyof<'T> =
    private
    | KeyOf of string

    /// <summary>The property name.</summary>
    [<Emit("$0")>]
    member _.Value: string = nativeOnly

/// <summary>The name of a property of <c>'T</c> whose value is a <c>'R</c>, a string at runtime.</summary>
/// <remarks>TypeScript annotations write it <c>KeysOfType&lt;T, R></c>, the keys of <c>T</c> whose property type is
/// assignable to <c>R</c>, so <c>target[key]</c> is typed <c>R</c>. Build one with <c>keyof</c>, and read or write the
/// property with <c>KeyOf.get</c> and <c>KeyOf.set</c>. It converts to a <c>keyof&lt;'T></c> where one is expected.</remarks>
[<Erase; EmitType("{ [K in keyof $0]-?: $0[K] extends $1 ? K : never }[keyof $0] & string")>]
type keyof<'T, 'R> =
    private
    | TypedKeyOf of string

    /// <summary>The property name.</summary>
    [<Emit("$0")>]
    member _.Value: string = nativeOnly

    /// <summary>Forgets the property type.</summary>
    static member inline op_Implicit(key: keyof<'T, 'R>) : keyof<'T> = unbox key

/// <summary>Builds property keys from property accesses.</summary>
[<AutoOpen>]
module KeyOfBuilder =
    /// <summary>The name JavaScript reads the property by, typed by the object and property types.</summary>
    /// <param name="property">A lambda that reads one property of its argument, such as <c>fun c -> c.Port</c> or <c>_.Port</c>.
    /// A path, such as <c>_.a.b</c>, a method call or a function value is a compile error.</param>
    /// <remarks>The name is the one the compiled access uses, so a renamed or mangled member gives its JavaScript name.</remarks>
    /// <example><code lang="fsharp">
    /// let port = keyof (fun (c: Config) -> c.Port)   // const port = "Port"
    /// KeyOf.get port config                            // config[port]
    /// </code></example>
    let keyof (property: 'T -> 'R) : keyof<'T, 'R> = nativeOnly

    /// <summary>Matches a key with the given property name.</summary>
    let (|Key|_|) (name: string) (key: keyof<'T>) : bool = nativeOnly

/// <summary>Reads and writes properties through a <c>keyof</c>.</summary>
[<RequireQualifiedAccess>]
module KeyOf =
    /// <summary>Reads the property the key names: <c>target[key]</c>.</summary>
    let get (key: keyof<'T, 'R>) (target: 'T) : 'R = nativeOnly

    /// <summary>Writes the property the key names: <c>target[key] = value</c>.</summary>
    /// <remarks>TypeScript rejects the write when the property is <c>readonly</c>, as an F# record field is.</remarks>
    let set (key: keyof<'T, 'R>) (target: 'T) (value: 'R) : unit = nativeOnly

    /// <summary>Reads the property the key names, or <c>None</c> when it is missing, <c>null</c> or <c>undefined</c>.</summary>
    let tryGet (key: keyof<'T>) (target: 'T) : obj option = nativeOnly

    /// <summary>Forgets the property type of a key.</summary>
    let inline widen (key: keyof<'T, 'R>) : keyof<'T> = unbox key

    /// <summary>Treats a string as a key; nothing checks that <c>'T</c> has the property, or that it is a <c>'R</c>.</summary>
    /// <remarks>TypeScript sees the cast, <c>name as KeysOfType&lt;T, R></c>.</remarks>
    let inline unsafeOfString<'T, 'R> (name: string) : keyof<'T, 'R> = unbox name
