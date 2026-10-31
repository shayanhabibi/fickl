module StaticMemberVal

// A union declares the backing fields of its `static member val` properties, as a class does
type Color =
    | Red
    | Green

    static member internal tryFetchDefaultColor() : Color option = None

    static member val defaultColor =
        match Color.tryFetchDefaultColor () with
        | Some c -> c
        | None -> Red

// A static field cannot name the type parameters of its class, so they become `any`
type Box<'T> =
    | Full of 'T
    | Empty

    static member val Created = 0 with get, set
    static member val Default: Box<'T> = Empty

type Registry<'T>() =
    static member val Last: 'T option = None with get, set
