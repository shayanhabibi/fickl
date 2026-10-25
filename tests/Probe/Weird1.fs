namespace Weird

open System

module Log =
    let lines = ResizeArray<string>()
    let add s = lines.Add s

module Early =
    let internal secret = "internal-secret"
    let private hidden = "private-hidden"
    let usesSecret () = secret + "!"
    let exposed () = hidden + "?"
    let mutable counter = 0
    let inline bump () = counter <- counter + 1; counter
    do Log.add "Early.init"
    let initialised = (Log.add "Early.value"; 42)

type Foo = { V: int }

module Foo =
    let create v = { V = v }
    let value (f: Foo) = f.V

[<RequireQualifiedAccess>]
type Color =
    | Red
    | Green
    | ``Light Blue``
    | ``with-dash``

type ``My Record`` = { ``my field``: int; ``a-b``: string; ``class``: int; ``1x``: int }

type IThing =
    abstract Name: string
    abstract Describe: unit -> string

type Thing() =
    member _.Name = "class-name"
    member _.Describe() = "class-describe"
    interface IThing with
        member _.Name = "iface-name"
        member _.Describe() = "iface-describe"

type IOne =
    abstract Value: unit -> int

type ITwo =
    abstract Value: unit -> int

type Both() =
    interface IOne with
        member _.Value() = 1
    interface ITwo with
        member _.Value() = 2

type IGen =
    abstract Map<'T> : 'T -> 'T list

type IProp =
    abstract P: int with get, set

type Point(x: int, y: int) =
    let mutable label = ""
    member _.X = x
    member _.Y = y
    member _.Label with get () = label and set v = label <- v
    member _.Item with get (i: int) = if i = 0 then x else y
    member _.Item with get (i: int, j: int) = x * i + y * j
    member _.Sum(?scale: int, ?offset: int) = (x + y) * defaultArg scale 1 + defaultArg offset 0
    member _.Opt([<Runtime.InteropServices.Optional; Runtime.InteropServices.DefaultParameterValue(7)>] n: int) = n
    member _.Many([<ParamArray>] xs: int[]) = Array.sum xs
    static member (+)(a: Point, b: Point) = Point(a.X + b.X, a.Y + b.Y)
    static member (~-)(a: Point) = Point(-a.X, -a.Y)
    static member op_Implicit(t: int * int) = Point(fst t, snd t)
    override _.ToString() = $"({x},{y})"

type Ops =
    static member inline (>>=)(a: int option, f) = Option.bind f a

module Operators2 =
    let (|>>) x f = f x + 1
    let ( *** ) a b = a * b * 10
    let (.>>.) a b = (a, b)
    let (<^>) a b = a + b
    let (~%%) x = x * 2
    let (!!) (x: int ref) = x.Value + 100
    let (=~) (s: string) (p: string) = s.Contains p
    let ($) f x = f x
    let (?) (o: Map<string, int>) (k: string) = o.[k]
    let (|Positive|_|) x = if x > 0 then Some x else None
    let (..) a b = [ b; a ]
    let (.. ..) a s b = [ b; s; a ]

[<AutoOpen>]
module Extensions =
    type String with
        member s.Shout = s.ToUpper() + "!"
        member s.Repeat(n) = String.replicate n s
        static member Hello = "hello-ext"

    type Int32 with
        member i.Double = i * 2

    type List<'T> with
        member xs.Second = List.item 1 xs

    type Point with
        member p.Swap() = Point(p.Y, p.X)
        member p.Describe() = "ext-describe"

[<Runtime.CompilerServices.Extension>]
type CsExt =
    [<Runtime.CompilerServices.Extension>]
    static member Triple(x: int) = x * 3

type MaybeBuilder() =
    member _.Bind(m, f) = Option.bind f m
    member _.Return x = Some x
    member _.ReturnFrom m = m
    member _.Zero() = Some()
    member _.Delay f = f
    member _.Run f = f ()
    member _.Combine(a, b) = Option.bind (fun () -> b ()) a
    member _.TryWith(body, handler) = try body () with e -> handler e
    member _.TryFinally(body, comp) = try body () finally comp ()
    member _.Using(res: #IDisposable, body) = try body res finally res.Dispose()
    member this.While(guard, body) = if guard () then this.Combine(body (), (fun () -> this.While(guard, body))) else Some()
    member this.For(xs: seq<_>, body) = this.Using(xs.GetEnumerator(), fun en -> this.While((fun () -> en.MoveNext()), (fun () -> body en.Current)))
    member _.MergeSources(a, b) = match a, b with Some x, Some y -> Some(x, y) | _ -> None
    member _.BindReturn(m, f) = Option.map f m

type ListCollector() =
    member _.Yield x = [ x ]
    member _.YieldFrom xs = xs
    member _.Zero() = []
    member _.Combine(a, b) = a @ b
    member _.Delay f = f ()
    member _.For(xs: seq<int>, f: int -> int list) = List.collect f (List.ofSeq xs)
    [<CustomOperation("double")>]
    member _.Double(xs: int list) = xs |> List.map (fun x -> x * 2)
    [<CustomOperation("keep", MaintainsVariableSpace = true)>]
    member _.Keep(xs: int list, [<ProjectionParameter>] f: int -> bool) = List.filter f xs

[<CustomEquality; CustomComparison>]
type Loose =
    { Key: string; Extra: int }
    override this.Equals o =
        match o with
        | :? Loose as l -> l.Key.ToLower() = this.Key.ToLower()
        | _ -> false
    override this.GetHashCode() = this.Key.ToLower().GetHashCode()
    interface IComparable with
        member this.CompareTo o =
            match o with
            | :? Loose as l -> String.Compare(this.Key.ToLower(), l.Key.ToLower())
            | _ -> -1

type Shape =
    | Circle of radius: float
    | Square of side: float
    member s.Kind =
        match s with
        | Circle _ -> "circle"
        | Square _ -> "square"

type Tree<'T> =
    | Leaf of 'T
    | Node of Tree<'T> * Tree<'T>
    member t.Fold(f: 'T -> 'S, g: 'S -> 'S -> 'S) : 'S =
        match t with
        | Leaf x -> f x
        | Node(l, r) -> g (l.Fold(f, g)) (r.Fold(f, g))

type Delegates =
    delegate of int * int -> int

[<AbstractClass>]
type Animal(name: string) =
    abstract Sound: unit -> string
    abstract Greet: unit -> string
    default this.Greet() = name + " says " + this.Sound()
    member _.Name = name

type Dog(name) =
    inherit Animal(name)
    override _.Sound() = "woof"

type Puppy(name) =
    inherit Dog(name)
    override _.Sound() = "yip"
    override this.Greet() = "little " + base.Greet()

type GenBase<'T>() =
    abstract Wrap: 'T -> string
    default _.Wrap x = "base:" + string x

type GenDerived() =
    inherit GenBase<int>()
    override _.Wrap x = "derived:" + string (x + 1)

module rec RecModule =
    let isEvenR n = if n = 0 then true else isOddR (n - 1)
    let isOddR n = if n = 0 then false else isEvenR (n - 1)
    type Node = { Children: Node list; Owner: Owner option }
    type Owner = { Name: string; Root: Node }
    let describe (o: Owner) = o.Name + string o.Root.Children.Length

module π =
    let τ = 6.28
    let 变量 = "unicode"
    let ``a.b`` = "dotted"
    let ``a/b`` = "slashed"
    let ``#x`` = "hash"
    let ``x y z`` = "spaced"
    let ``default`` = "default-kw"

module Measure =
    [<Measure>]
    type kg

    [<Measure>]
    type m

module Lits =
    [<Literal>]
    let A = 1

    [<Literal>]
    let B = 2

    [<Literal>]
    let Combined = A + B * 10

    [<Literal>]
    let Text = "a" + "b"

    [<Literal>]
    let FlagsCombo = IO.FileAttributes.ReadOnly ||| IO.FileAttributes.Hidden

type StaticLazy() =
    static let l = lazy (Log.add "lazy"; 5)
    static member Value = l.Value
