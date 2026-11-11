module Aliases

open Shapes

/// The id of a point
type PointId = int

type Points = Map<PointId, Point>
type Transform = Point -> Point
type Pair<'T> = 'T * 'T
type Areas<'T when 'T :> IArea> = 'T list
type private Cache = ResizeArray<Point>

module Units =
    [<Measure>]
    type cm

    type Length = float<cm>

let find (points: Points) (id: PointId) = Map.tryFind id points
let origin: Pair<float> = 0., 0.
