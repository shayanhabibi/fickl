// Change: overloads taking `'T -> 'U` and `Func<'T, 'U>`
// Fickl: Overloads that differ only by a function or a delegate parameter get different names.
// Issues: 287
module Main

open System

type DelegateOverload =
    static member Map(arg: 'T -> 'U, x: 'T) = arg x
    static member Map(arg: Func<'T, 'U>, x: 'T) = arg.Invoke x

Check.equal 3 (DelegateOverload.Map(Func<int, int>(fun x -> x + 2), 1))
