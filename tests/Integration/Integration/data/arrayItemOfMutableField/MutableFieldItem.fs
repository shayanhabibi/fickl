module MutableFieldItem

// fickl: nothing runs between two reads of a mutable field that are written one after the other, so such a read
// can be written twice. An array held in a mutable field is then read where it is written, like a local one, and
// a mutable field can index an array.

type Body =
    {
        mutable Pos: float[]
        mutable Count: int
    }

type IHolder =
    abstract Items: float[]

let read (body: Body) (i: int) = body.Pos[i]

let readAtField (xs: float[]) (body: Body) = xs[body.Count]

let writeAtField (xs: float[]) (body: Body) (v: float) = xs[body.Count] <- v

// A property may run code, so it is still read once
let readProperty (holder: IHolder) (i: int) = holder.Items[i]
