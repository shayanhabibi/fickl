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

/// `--checkedDivision`: dividing an integer or a decimal by zero raises DivideByZeroException (JavaScript and
/// TypeScript)
[<Literal>]
let checkedDivisionDefine = "FICKL_CHECKED_DIVISION"

/// `--debugPipelines`: each stage of a `|>`, `||>` or `|||>` pipeline is a statement of its own that binds the
/// stage's result to a named constant, so a debugger can step through the stages (JavaScript and TypeScript)
[<Literal>]
let debugPipelinesDefine = "FICKL_DEBUG_PIPELINES"

/// The name `--debugPipelines` gives the constant that holds the result of a pipeline stage, before it is made unique
[<Literal>]
let pipelineStageName = "pipe"

/// Whether a binding holds the result of a pipeline stage under `--debugPipelines`, and so must not be inlined
let isPipelineStage (defines: string list) (isCompilerGenerated: bool) (name: string) =
    isCompilerGenerated
    && (name = pipelineStageName
        || name.StartsWith(pipelineStageName + "_", System.StringComparison.Ordinal))
    && List.contains debugPipelinesDefine defines
