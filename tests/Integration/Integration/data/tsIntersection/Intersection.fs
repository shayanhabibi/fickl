module Intersection

open Fable.Core
open Fickl.Core

type Named =
    abstract name: string

type Aged =
    abstract age: int

type Tagged =
    abstract tag: string

type Person = { name: string; age: int; tag: string }

let nameOf (x: Named) : string = x.name
let ageOf (x: Aged) : int = x.age

// The value is the object itself, viewed as each of its types
let describe (x: I2<Named, Aged>) : string = x.As1.name + string x.As2.age

// It converts implicitly where one of its types is expected
let describeImplicit (x: I2<Named, Aged>) : string = nameOf x + string (ageOf x)
let asNamed (x: I2<Named, Aged>) : Named = x

let ofPerson (p: Person) : I2<Named, Aged> = I2.UnsafeOf p
let ofObj (o: obj) : I3<Named, Aged, Tagged> = I3.UnsafeOf o
let three (x: I3<Named, Aged, Tagged>) : string = x.As1.name + x.As3.tag
let four (x: I4<Named, Aged, Tagged, Person>) : Person = x.As4

// Parentheses where the intersection is an operand
let inResizeArray (xs: ResizeArray<I2<Named, Aged>>) : int = xs.Count
let inArray (xs: I2<Named, Aged>[]) : int = xs.Length
let inUnion (x: U2<I2<Named, Aged>, string>) : obj = box x
let inOption (x: I2<Named, Aged> option) : bool = x.IsSome
let asParameter (f: I2<Named, Aged> -> string) (x: I2<Named, Aged>) : string = f x
let ofUnion (x: I2<U2<Named, Tagged>, Aged>) : int = x.As2.age
let ofFunction (x: I2<int -> string, Named>) : string = x.As2.name
let nested (x: I2<I2<Named, Aged>, Tagged>) : string = x.As1.As1.name + x.As2.tag

// Generic code keeps the type parameters
let first (x: I2<'A, 'B>) : 'A = x.As1
let unsafeBoth<'A, 'B> (value: obj) : I2<'A, 'B> = I2.UnsafeOf value
