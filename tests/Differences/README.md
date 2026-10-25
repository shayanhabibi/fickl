# Differences from Fable

Each folder is one change Fickl makes that upstream Fable does not have yet. `dotnet run --project src/Fickl.Build -- differences` compiles every case with Fickl and with the latest Fable, runs the output with node, and writes the cases Fable gets wrong into the README table.

- A case fails Fickl's run: the command exits with an error, as that is a regression.
- A case passes with Fable too: it leaves the table, and the command says the folder can be deleted.

## Writing a case

A case is a folder with a `Main.fs`, plus any other `.fs` files, which compile first in name order. `Check.fs` compiles before all of them; `Check.equal expected actual` throws when they differ, which fails the run.

`Main.fs` starts with these comment lines:

```fsharp
// Change: `[<CallableInterface>]`
// Fickl: What Fickl does, in one sentence for the table.
// Issues: 8, 10
// Args: --declaration
```

`Issues` is `none` or Fickl issue numbers. `Args` is optional: extra options for both compilers.

Fickl compiles a case with `src/Fickl.Core` referenced, and Fable with the latest `Fable.Core` package, so a case using a Fickl attribute fails to type-check with Fable.

Fickl defines `FICKL_COMPILER` and Fable does not. When Fable gains an attribute that Fickl has, Fable's version lives in `Fable.Core` rather than `Fickl.Core`, so a case picks the one each compiler has:

```fsharp
#if FICKL_COMPILER
open Fickl.Core
#else
open Fable.Core
#endif
```

Once Fable passes the case, it leaves the table as usual.
