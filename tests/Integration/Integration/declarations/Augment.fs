/// Augmentations: a file that only augments interfaces stays a module
module Augment

open Fickl.Core

[<AugmentGlobal("Window")>]
type AppWindow =
    abstract appName: string

[<AugmentModule("vue", "ComponentCustomProperties")>]
type Translations =
    abstract ``$t``: key: string -> string
