module Client

let tupled (a: int, b: string) = a + b.Length
let curried (a: int) (b: string) = a + b.Length
