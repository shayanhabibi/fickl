module Referenced

let greet (name: string) = "hello from " + name

// An entry point in a referenced project: it is not run by the project that references it
[<EntryPoint>]
let main _ =
    printfn "referenced main"
    0
