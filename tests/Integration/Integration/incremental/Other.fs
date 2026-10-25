module Other

#nowarn "40" // the recursive values below

let unrelated () = 42

let rec otherRecursive = Lib.delay (fun () -> otherRecursive2 ())
and otherRecursive2 = fun () -> Lib.recursed <- Lib.recursed + 1
