module RuntimeHashingFixture

open System
open System.Collections.Generic

type Key =
    {
        Id: int
        Name: string
        Values: int[]
    }

let mutable checks = 0
let failures = ResizeArray<int>()

let check condition =
    checks <- checks + 1

    if not condition then
        failures.Add checks

let make i =
    {
        Id = i
        Name = sprintf "key-%d" i
        Values = [| i; i + 1; -i |]
    }

let dictionary = Dictionary<Key, int>(HashIdentity.Structural)
let set = HashSet<Key>(HashIdentity.Structural)

for i in 0..255 do
    dictionary.Add(make i, i)
    check (set.Add(make i))

for i in 0..255 do
    let key = make i
    check (dictionary[key] = i)
    check (set.Contains key)
    check (not (set.Add key))
    check (hash key = hash (make i))
    dictionary[key] <- i + 1000
    check (dictionary[make i] = i + 1000)

for i in 0..2..255 do
    check (dictionary.Remove(make i))
    check (set.Remove(make i))

check (dictionary.Count = 128 && set.Count = 128)

let arrays = Dictionary<int[], int>(HashIdentity.Structural)

for length in [ 0; 1; 2; 18; 19; 32; 1024 ] do
    let key = Array.init length id
    arrays[key] <- length
    check (arrays[Array.copy key] = length)
    check (hash key = hash (Array.copy key))

let anonymous =
    {|
        A = [| 1; 2; 3 |]
        B = Some(Some 42)
        C = "record"
    |}

let anonymousCopy =
    {|
        A = [| 1; 2; 3 |]
        B = Some(Some 42)
        C = "record"
    |}

check (anonymous = anonymousCopy)
check (hash anonymous = hash anonymousCopy)
let anonSet = HashSet<_>(HashIdentity.Structural)
check (anonSet.Add anonymous)
check (anonSet.Contains anonymousCopy)

let options: string option option list =
    [ None; Some None; Some(Some null); Some(Some ""); Some(Some "value") ]

let optionSet = HashSet<_>(HashIdentity.Structural)

for item in options do
    check (optionSet.Add item)

for item in options do
    check (optionSet.Contains item)

check (optionSet.Count = options.Length)
check (hash [| Some(); None |] = hash [| Some(); None |])

// Equal hashes are permitted for unequal values; lookup must still use equality.
[<CustomEquality; NoComparison>]
type Collision =
    {
        Value: int
    }

    override _.GetHashCode() = 7

    override x.Equals(other) =
        match other with
        | :? Collision as y -> x.Value = y.Value
        | _ -> false

let collisions = Dictionary<Collision[], int>(HashIdentity.Structural)

for i in 0..63 do
    collisions[[| { Value = i } |]] <- i

for i in 0..63 do
    check (collisions[[| { Value = i } |]] = i)

printfn "CHECKS: %d; FAILURES: %A" checks (failures.ToArray())

if failures.Count > 0 then
    failwith "F# semantic assertions failed"
