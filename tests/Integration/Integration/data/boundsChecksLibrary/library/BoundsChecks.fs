// The compiler reads [<BoundsChecks>] by its full name. The precompile in this harness cannot reference Fickl.Core,
// whose project reference brings in the sources of Fable.Core, so the library declares the attribute itself
namespace Fickl.Core

[<System.AttributeUsage(System.AttributeTargets.All)>]
type BoundsChecksAttribute(enabled: bool) =
    inherit System.Attribute()
