module Fable.Metadata

open System

let coreAssemblies =
    [|
        "Fable.Core"
        "FSharp.Core"
        "mscorlib"
        "netstandard"
        "System.Collections"
        "System.Collections.Concurrent"
        "System.ComponentModel"
        "System.ComponentModel.Primitives"
        "System.ComponentModel.TypeConverter"
        "System.Console"
        "System.Core"
        "System.Diagnostics.Debug"
        "System.Diagnostics.Tools"
        "System.Diagnostics.Tracing"
        "System.Globalization"
        "System"
        "System.IO"
        "System.Net.Requests"
        "System.Net.WebClient"
        "System.Numerics"
        "System.Reflection"
        "System.Reflection.Extensions"
        "System.Reflection.Metadata"
        "System.Reflection.Primitives"
        "System.Reflection.TypeExtensions"
        "System.Runtime"
        "System.Runtime.Extensions"
        "System.Runtime.InteropServices"
        "System.Runtime.Numerics"
        "System.Text.Encoding"
        "System.Text.Encoding.Extensions"
        "System.Text.RegularExpressions"
        "System.Threading"
        "System.Threading.Tasks"
        "System.Threading.Thread"
        "System.ValueTuple"
    |]

let isSystemPackage (pkgName: string) =
    pkgName.StartsWith("System.", StringComparison.Ordinal)
    || pkgName.StartsWith("Microsoft.", StringComparison.Ordinal)
    || pkgName.StartsWith("runtime.", StringComparison.Ordinal)
    || pkgName = "NETStandard.Library"
    || pkgName = "FSharp.Core"
    || pkgName = "Fable.Core"
    // fickl: Fickl.Core holds only attributes the compiler reads, so like Fable.Core it stays a reference instead of
    // being compiled from its sources; a compiled assembly that uses its attributes must still be able to find them
    || pkgName = "Fickl.Core"
    || pkgName = "testhost"
