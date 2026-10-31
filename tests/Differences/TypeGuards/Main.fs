// Change: `[<TypePredicate>]` and `[<TypeAssertion>]`
// Fickl: Emits a function as a TypeScript type guard, `x is T` or `asserts x is T`, while F# calls it as returning an option or a Result of the narrowed argument.
// Issues: 147
module Main

open Fable.Core
open Fickl.Core

[<TypePredicate("value", typeof<string>)>]
let tryText (value: obj) : string option =
    match value with
    | :? string as text -> Some text
    | _ -> None

[<TypeAssertion("value", typeof<string>)>]
let checkText (value: obj) : Result<string, exn> =
    match value with
    | :? string as text -> Ok text
    | _ -> Error(exn "not text")

// JavaScript sees a boolean predicate and a function that throws
Check.equal "boolean" (JsInterop.emitJsExpr () "typeof tryText('a')")
Check.equal true (JsInterop.emitJsExpr () "(() => { try { checkText(1); return false } catch { return true } })()")

// F# sees the option and the Result
Check.equal (Some "a") (tryText (box "a"))
Check.equal None (tryText (box 1))
Check.equal (Ok "a") (checkText (box "a"))
Check.equal "not text" (match checkText (box 1) with Error e -> e.Message | Ok _ -> "")
