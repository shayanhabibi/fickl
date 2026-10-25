// Change: `[<InlineInterface>]`
// Fickl: A `TypeScriptTaggedUnion` case holding one interface compiles as that interface with the tag added, `{ type: "take", fromIndex, toIndex }`.
// Issues: none
module Main

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core

type Take =
    abstract fromIndex: int
    abstract toIndex: int

[<RequireQualifiedAccess; TypeScriptTaggedUnion("type")>]
type Command =
    | [<InlineInterface>] Take of Take
    | Save

let command =
    Command.Take
        { new Take with
            member _.fromIndex = 1
            member _.toIndex = 3
        }

Check.equal "take" command?``type``
Check.equal 1 command?fromIndex

match !!{| ``type`` = "take"; fromIndex = 2; toIndex = 5 |} with
| Command.Take take -> Check.equal 5 take.toIndex
| Command.Save -> failwith "matched Save"
