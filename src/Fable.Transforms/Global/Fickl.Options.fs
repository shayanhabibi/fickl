/// fickl: Fickl's compiler options that Fable's CompilerOptions has no field for. Each is passed as a define, so it
/// reaches the checker, the package cache key and the compile server like any other define.
module Fickl.Options

/// `--strictOptions`: an option, not a value option, of a type that can be null keeps `Some null` apart from `None`
[<Literal>]
let strictOptionsDefine = "FICKL_STRICT_OPTIONS"
