/// TsEnum: the JavaScript exports the enum object, and the declarations type it with an ambient enum
module Enums

open Fickl.Core

[<TsEnum>]
type Status =
    | Active = 1
    | Retired = 2

let retire (status: Status) =
    match status with
    | Status.Active -> Status.Retired
    | other -> other
