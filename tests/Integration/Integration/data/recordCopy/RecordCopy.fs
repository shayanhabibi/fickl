module RecordCopy

open Types

// A copy lists every field of the record, so a file that copies a record often enough declares a copier for it.
// These two records have the same fields: four copies cost less than a copier, five cost more.

type Below =
    {
        Margin: int
        Border: int
        Colour: string
        Height: int
        Length: int
        Weight: float
        Radius: int
        Shadow: bool
    }

type Above =
    {
        Margin: int
        Border: int
        Colour: string
        Height: int
        Length: int
        Weight: float
        Radius: int
        Shadow: bool
    }

let below1 (r: Below) = { r with Margin = 1 }
let below2 (r: Below) = { r with Colour = "red" }
let below3 (r: Below) = { r with Weight = 0.5 }
let below4 (r: Below) = { r with Shadow = true }

let above1 (r: Above) = { r with Margin = 1 }
let above2 (r: Above) = { r with Colour = "red" }
let above3 (r: Above) = { r with Weight = 0.5 }
let above4 (r: Above) = { r with Shadow = true }
let above5 (r: Above) = { r with Radius = r.Radius + 1 }

// A record declared in another file

let rename (title: string) (s: Settings) = { s with Title = title }
let resize (width: int) (height: int) (s: Settings) = { s with Width = width; Height = height }
let detach (s: Settings) = { s with Parent = None }
let raise (s: Settings) = { s with ``z index`` = s.``z index`` + 1 }
let onClose (f: unit -> unit) (s: Settings) = { s with OnClose = f }
let combine (s: Settings) = { s with Combine = fun x y -> x * y }
let hideAll (xs: Settings list) = xs |> List.map (fun s -> { s with Visible = false })
let fromCall (make: unit -> Settings) = { make () with Opacity = 1. }
let nested (s: Settings) = { { s with Title = "a" } with Tags = [] }

// A copy that changes more than two fields is a constructor call
let reset (s: Settings) = { s with Width = 0; Height = 0; Opacity = 0. }

type Slot<'T> =
    {
        Content: 'T
        Fallback: 'T option
        History: 'T list
        Caption: string
        Position: int
        Disabled: bool
    }

let fill (value: 'T) (slot: Slot<'T>) = { slot with Content = value }
let clear (slot: Slot<'T>) = { slot with Fallback = None }
let forget (slot: Slot<'T>) = { slot with History = [] }
let caption (slot: Slot<int>) = { slot with Caption = string slot.Content }
let move (slot: Slot<string>) = { slot with Position = slot.Position + 1 }
let disable (slot: Slot<'T>) = { slot with Disabled = true }

[<Struct>]
type Bounds =
    {
        Left: float
        Right: float
        Top: float
        Bottom: float
        Near: float
        Far: float
        Unit: string
    }

let left (b: Bounds) = { b with Left = 0. }
let right (b: Bounds) = { b with Right = 0. }
let top (b: Bounds) = { b with Top = 0. }
let bottom (b: Bounds) = { b with Bottom = 0. }
let near (b: Bounds) = { b with Near = 0. }
let far (b: Bounds) = { b with Far = 0. }
let unit' (b: Bounds) = { b with Unit = "px" }

// The copier reads the source after the new value is evaluated, so a new value that can change the source
// of a record with mutable fields keeps the constructor call

type Counter =
    {
        mutable Count: int
        Description: string
        Minimum: int
        Maximum: int
        Increment: int
        Wraps: bool
    }

let describe (c: Counter) = { c with Description = "counter" }
let minimum (c: Counter) = { c with Minimum = 0 }
let maximum (c: Counter) = { c with Maximum = 100 }
let increment (c: Counter) = { c with Increment = 1 }
let wraps (c: Counter) = { c with Wraps = true }
let limit (next: Counter -> int) (c: Counter) = { c with Maximum = next c }
let recount (next: Counter -> int) (c: Counter) = { c with Count = next c }

// Too few fields to gain from a copier, however often it is copied

type Point = { X: float; Y: float; Z: float }

let x (p: Point) = { p with X = 0. }
let y (p: Point) = { p with Y = 0. }
let z (p: Point) = { p with Z = 0. }
let origin (p: Point) = { { { p with X = 0. } with Y = 0. } with Z = 0. }
let mirror (p: Point) = { p with X = -p.X }
let flip (p: Point) = { p with Y = -p.Y }

// Anonymous records are objects, which a copy lists in full

let anonymous (r: {| Margin: int; Border: int; Colour: string; Height: int |}) = {| r with Margin = 1 |}
