// Change: `importValueDynamic` of a function
// Fickl: `importValueDynamic` works with tupled and curried functions, called with their compiled signature.
// Issues: 314
module Main

open Fable.Core.JsInterop

(importValueDynamic Client.curried).``then``(fun curried -> Check.equal 3 (curried 1 "ab"))
|> ignore

(importValueDynamic Client.tupled).``then``(fun tupled -> Check.equal 3 (tupled (1, "ab")))
|> ignore
