// Change: `[<CaseTest>]` on erased union cases
// Fickl: A JavaScript predicate tells apart erased cases with the same payload type, here two strings.
// Issues: 7
module Main

open Fable.Core
open Fickl.Core

[<RequireQualifiedAccess; Erase>]
type Reference =
    | [<CaseTest("$0.startsWith('n:')")>] Name of string
    | Slug of string

let describe =
    function
    | Reference.Name name -> "name " + name
    | Reference.Slug slug -> "slug " + slug

Check.equal "name n:x" (describe (Reference.Name "n:x"))
Check.equal "slug x" (describe (Reference.Slug "x"))
