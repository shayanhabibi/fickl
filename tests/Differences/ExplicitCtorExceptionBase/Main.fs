// Change: explicit constructors of a class inheriting `System.Exception`
// Fickl: A class with only explicit constructors that inherits System.Exception extends it and keeps the message passed to the base.
// Issues: 334
module Main

type ExplicitException =
    inherit System.Exception
    val Code: int
    new(m: string) = { inherit System.Exception(m); Code = 2 }
    new(m: string, code: int) = { inherit System.Exception(m); Code = code }

let e = ExplicitException("boom", 7)
Check.equal "boom" e.Message
Check.equal 7 e.Code
Check.equal true (box e :? System.Exception)
