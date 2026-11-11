module App

// The entry point of a referenced project is still a function that can be called
let runReferenced () = Referenced.main [||]

[<EntryPoint>]
let main _ =
    printfn "%s" (Referenced.greet "app")
    0
