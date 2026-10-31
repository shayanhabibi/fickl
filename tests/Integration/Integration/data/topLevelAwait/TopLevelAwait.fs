module TopLevelAwait

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core

type Settings =
    abstract name: string
    abstract retries: int

type SettingsModule =
    abstract settings: Settings

let settingsModule: SettingsModule =
    JS.awaitTopLevel (importDynamic<SettingsModule> "./settings.js")

let retries: int =
    JS.awaitTopLevel (if settingsModule.settings.retries > 0 then JS.Constructors.Promise.resolve 1 else JS.Constructors.Promise.resolve 2)

let private ready = JS.awaitTopLevel (JS.Constructors.Promise.resolve "ready")

// Functions read the awaited values; they are not inlined
let describe () = $"{settingsModule.settings.name} {retries} {ready}"
