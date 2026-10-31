/// fickl: Fickl's compiler options that Fable's CompilerOptions has no field for. Each is passed as a define, so it
/// reaches the checker, the package cache key and the compile server like any other define.
module Fickl.Options

/// `--strictOptions`: an option, not a value option, of a type that can be null keeps `Some null` apart from `None`
[<Literal>]
let strictOptionsDefine = "FICKL_STRICT_OPTIONS"

/// `--nativeErrors`: every exception is a JS `Error`, with a stack trace and a `cause` (JavaScript and TypeScript)
[<Literal>]
let nativeErrorsDefine = "FICKL_NATIVE_ERRORS"

/// `--privateFields`: the `let` fields of every class with attached members are ES `#private` fields
/// (JavaScript and TypeScript)
[<Literal>]
let privateFieldsDefine = "FICKL_PRIVATE_FIELDS"
