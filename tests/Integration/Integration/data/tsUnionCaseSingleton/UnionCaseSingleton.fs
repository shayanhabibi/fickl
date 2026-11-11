module UnionCaseSingleton

// The static field holding a case with no fields has the type of the union, not `any`
type IOType =
    | Source
    | Sample
    | FreeText of string

// A static field cannot name the type parameters of its class, so they become `any`
type Box<'T> =
    | Full of 'T
    | Empty

type Only = | Only

type Phantom<'T> = | Phantom

let source = Source
let empty: Box<int> = Empty
let only = Only
let phantom: Phantom<int> = Phantom

// Two cases have the same type, so comparing them is not an error for tsc
type Gender =
    | Male
    | Female

let sameReference = obj.ReferenceEquals(Male, Female)
let samePhysical = LanguagePrimitives.PhysicalEquality Male Female
let sameStructural = Male = Female

let describe () =
    match Sample with
    | Source -> "source"
    | Sample -> "sample"
    | FreeText text -> text
