module DynamicCast

open Fable.Core.JsInterop

type Item = { name: string }

type ITagged =
    abstract tag: int with get, set

type INamed =
    abstract name: string

// The receiver of a dynamic access is seen as `any`

let tag (item: Item) =
    item?tag <- 1
    let t: int = item?tag
    t

let describe (item: Item) : string = item?describe ()

let nested (named: INamed) : int = named?inner?value

// A value that is already `any` needs no cast

let tagOfObj (o: obj) : int =
    o?tag <- 1
    o?tag

// An unchecked cast between two different types goes through `unknown`

let tagOfUnbox (item: Item) =
    let t = unbox<ITagged> item
    t.tag

let asTagged (item: Item) : ITagged = !!item

let namedAsTagged (named: INamed) : ITagged = unbox named

let toGeneric<'T> (item: Item) : 'T = !!item

let ofGeneric<'T> (value: 'T) : Item = unbox value

let texts (numbers: int[]) : string[] = unbox numbers

// No cast is written from `obj`, to `obj`, or to the same type

let ofObj (o: obj) : ITagged = unbox o

let ofObjErased (o: obj) : ITagged = !!o

let toObj (item: Item) : obj = !!item

let same (item: Item) : Item = unbox item

let sameErased (item: Item) : Item = !!item
