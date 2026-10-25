/// Process-wide settings of a compile server (see CompileServer.fs), read by code compiled before it.
module Fable.Cli.ServerMode

/// Whether this process is a compile server, whose console nobody watches: output goes to the client as lines.
let mutable isServer = false

/// Whether compilations record the files each file depends on, as watch mode does, so a later
/// compilation in the same process can recompile the dependents of the files that changed.
let mutable collectsDependencies = false
