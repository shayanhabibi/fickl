module Augmentations

open Fable.Core
open Fickl.Core

[<Global>]
type HTMLElement() =
    class
    end

type Counter() =
    inherit HTMLElement()
    member val count = 0 with get, set

/// Custom elements this module defines
[<AugmentGlobal("HTMLElementTagNameMap")>]
type TagNames =
    [<EmitProperty("my-counter")>]
    abstract counter: Counter

[<AugmentGlobal("Window")>]
type AppWindow =
    abstract counters: Counter[]

[<AugmentModule("./plugin.js", "PluginProperties")>]
type Translations =
    abstract translate: key: string -> string

let countAll (window: AppWindow) =
    window.counters |> Array.sumBy (fun counter -> counter.count)

let translateAll (plugin: Translations) (keys: string list) = keys |> List.map plugin.translate
