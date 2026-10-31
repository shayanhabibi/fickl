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

/// <summary>The TypeScript <c>Record&lt;K, V></c>: a plain JavaScript object used as a map from <c>'K</c> keys to <c>'V</c> values.</summary>
/// <remarks><c>r.[key]</c> compiles to <c>r[key]</c> and <c>r.[key] &lt;- value</c> to <c>r[key] = value</c>.
/// Build one with <c>Record.empty</c>, or bind a JavaScript API that takes or returns one.
/// TypeScript annotations write <c>globalThis.Record</c>, which Fable's own <c>Record</c> class cannot shadow.</remarks>
[<EmitType("globalThis.Record<$0, $1>")>]
type Record<'K, 'V> =
    /// <summary>The value at a key; <c>undefined</c> at runtime when the key is missing.</summary>
    [<EmitIndexer>]
    abstract Item: key: 'K -> 'V with get, set

/// <summary>The TypeScript <c>Readonly&lt;Record&lt;K, V>></c>: a <c>Record</c> that cannot be written.</summary>
[<EmitType("Readonly<globalThis.Record<$0, $1>>")>]
type ReadonlyRecord<'K, 'V> =
    /// <summary>The value at a key; <c>undefined</c> at runtime when the key is missing.</summary>
    [<EmitIndexer>]
    abstract Item: key: 'K -> 'V

/// <summary>The TypeScript <c>{ [P in keyof T]: V }</c>: a <c>'V</c> for each property name of <c>'T</c>.</summary>
/// <remarks>TypeScript annotations write it <c>Record&lt;keyof T &amp; string, V></c>, so every key of <c>T</c> must be present.</remarks>
[<EmitType("globalThis.Record<keyof $0 & string, $1>")>]
type PropertyRecord<'T, 'V> =
    /// <summary>The value for a property name.</summary>
    [<EmitIndexer>]
    abstract Item: key: keyof<'T> -> 'V with get, set

/// <summary>Builds and reads <c>Record</c> objects.</summary>
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Record =
    /// <summary>An empty object, <c>{}</c>.</summary>
    let empty<'K, 'V> () : Record<'K, 'V> = nativeOnly

    /// <summary>The value at a key the object has as its own property, or <c>None</c>: <c>Object.hasOwn(r, key)</c>.</summary>
    /// <remarks>Unlike <c>r.[key]</c>, an inherited property such as <c>toString</c> is not found.</remarks>
    let tryGet (key: 'K) (record: Record<'K, 'V>) : 'V option = nativeOnly

    /// <summary>The object's own property names: <c>Object.keys(r)</c>.</summary>
    /// <remarks>A number key is a string here, as JavaScript object keys always are.</remarks>
    let keys (record: Record<'K, 'V>) : string[] = nativeOnly

/// <summary>The TypeScript <c>T[keyof T]</c>: the value of some property of <c>'T</c>, not known which.</summary>
/// <remarks>Read it as a type with <c>ValueOf.unsafeAs</c>, which nothing checks. Changes only TypeScript annotations;
/// the JavaScript value is the property's value itself.</remarks>
[<Erase; EmitType("$0[keyof $0]")>]
type valueof<'T> = private | ValueOf of obj

/// <summary>Builds and reads <c>valueof</c> values.</summary>
[<RequireQualifiedAccess>]
module ValueOf =
    /// <summary>The value of the property the key names: <c>target[key]</c>.</summary>
    let ofKey (key: keyof<'T, 'R>) (target: 'T) : valueof<'T> = nativeOnly

    /// <summary>Treats the value as a <c>'R</c>; nothing checks that it is one. TypeScript sees the cast.</summary>
    let unsafeAs<'T, 'R> (value: valueof<'T>) : 'R = nativeOnly
