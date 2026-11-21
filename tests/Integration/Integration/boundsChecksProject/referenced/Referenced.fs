module Referenced

let read (a: float[]) i = a.[i + 1]
let write (a: float[]) i v = a.[i + 1] <- v
let stringRead (s: string) i = s.[i + 1]
let inline inlineRead (a: float[]) i = a.[i + 1]
