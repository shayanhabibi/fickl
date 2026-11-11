module StaticFields

let seed = 10

// Initializers that only run what the class body can see become native static fields
type Counter() =
    static let mutable count = 42
    static let doubled = count * 2 + seed
    static let instance = Counter()

    static member Count
        with get () = count
        and set v = count <- v

    static member Doubled = doubled
    static member Instance = instance
    static member val Auto = 5 with get, set

[<Fable.Core.AttachMembers>]
type Attached() =
    static let mutable n = 1
    static let label = "x" + string n

    static member N
        with get () = n
        and set v = n <- v

    static member Label = label

// The static fields of a union go after the case singletons they may read
type Shape =
    | Empty
    | Circle of float

    static let fallback = Empty
    static member Fallback = fallback

type Point =
    { X: int }

    static let origin = { X = 0 }
    static member Origin = origin

type Registry<'T>() =
    static let mutable hits = 0
    static member Hit(_: 'T) = hits <- hits + 1
    static member Hits = hits

// A function declared outside the class reads it through the module binding, which is not set while
// the class is being evaluated. Initializers that reach one keep the static constructor.
type CallsOwnMember() =
    static let a = 3
    static let b = CallsOwnMember.Twice a
    static member Twice x = x * 2
    static member B = b

type ReflectsSelf() =
    static let t = typeof<ReflectsSelf>
    static member T = t

type BuildsCaseWithFields =
    | Leaf
    | Node of int

    static let node = Node 3
    static member Built = node

// A static field cannot name the type parameters of its class
type Box<'T>() =
    static member val Last: 'T option = None with get, set

// Only a static constructor made of field initializers alone is folded
type WithStaticDo() =
    static let mutable x = 1
    static do x <- x + 1
    static member X = x
