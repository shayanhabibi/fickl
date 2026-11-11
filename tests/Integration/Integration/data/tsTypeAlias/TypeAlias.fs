module TypeAlias

open System
open Fable.Core
open Fickl.Core

[<Measure>]
type m

type User = { Name: string; Age: int }

type Shape =
    | Circle of radius: float
    | Square of side: float

type IGreeter =
    abstract Greet: name: string -> string

/// The id of a user.
type UserId = int

type UserMap = Map<string, User>
type Handler = string -> unit
type Reducer = int -> string -> int
type Callback = Func<string, int, bool>
type Point = float * float
type StructPoint = (struct (int * int))
type Pair<'T> = 'T * 'T
type Lookup<'K, 'V when 'K: comparison> = Map<'K, 'V list>
type Greeters<'T when 'T :> IGreeter> = 'T[]

// Abbreviations of abbreviations
type AccountId = UserId
type Pairs = Pair<UserId> list

// Units of measure are erased
type Meters = float<m>
type Distance<[<Measure>] 'u> = float<'u>

type MaybeUser = User option
type Shapes = ResizeArray<Shape>
type Greeter = IGreeter
type Names = string seq
type Anything = obj
type Json = unknown
type Nothing = unit
type Settings = {| Verbose: bool; Depth: int |}

// Named like the type fable-library exports for a list
type FSharpList = int list

type private Secret = string
type internal Token = Guid

module Nested =
    type Id = string

    module Deeper =
        type Ids = Id[]

// Modules can each have an abbreviation of the same name
module Other =
    type Id = int

// A value can have the name of an abbreviation
let Handler (handler: Handler) = handler "a"

let userName (users: UserMap) (id: UserId) = users |> Map.tryFind (string id)
let apply (reducer: Reducer) (state: UserId) = reducer state "x"
let swap ((a, b): Pair<int>) : Pair<int> = b, a
let length (distance: Meters) : float = float distance
let first (ids: Nested.Deeper.Ids) : Nested.Id = ids[0]
let numbers: FSharpList = [ 1; 2 ]
let private secret: Secret = "s"
let reveal () = secret
