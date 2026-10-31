module Transfer

open Fickl.Core

type Shape =
    | Circle of radius: float
    | Empty

type Model = { Name: string; Shapes: Shape list; Scores: Map<string, int> }

let send (model: Model) : obj = Transfer.toPlain model

let receive (data: obj) : Model = Transfer.ofPlain data

let copy (model: Model) = Transfer.clone model

let inline sendAny (value: 'T) = Transfer.toPlain value

let sendShapes (shapes: Shape list) = sendAny shapes

let rawCopy (values: string[]) = JS.structuredClone values

let moveBuffer (buffer: obj) = JS.structuredCloneTransfer [| buffer |] buffer
