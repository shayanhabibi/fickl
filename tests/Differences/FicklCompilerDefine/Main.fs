// Change: the `FICKL_COMPILER` define
// Fickl: Defines `FICKL_COMPILER`, so code can choose Fickl.Core or Fable.Core when both have an attribute.
// Issues: 43
module Main

#if FICKL_COMPILER
let compiler = "fickl"
#else
let compiler = "fable"
#endif

Check.equal "fickl" compiler
