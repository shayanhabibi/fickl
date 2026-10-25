module Program

open FsToolkit.ErrorHandling
open Hedgehog
open Hedgehog.FSharp

type Person =
    {
        Name: string
        Age: int
    }

let validate (p: Person) =
    result {
        let! name =
            if p.Name = "" then
                Error "empty name"
            else
                Ok p.Name

        let! age =
            if p.Age < 0 then
                Error "negative age"
            else
                Ok p.Age

        return
            {
                Name = name
                Age = age
            }
    }

let genPerson =
    gen {
        let! name = Gen.string (Range.linear 1 10) Gen.alpha
        let! age = Gen.int32 (Range.linear 0 100)

        return
            {
                Name = name
                Age = age
            }
    }


[<EntryPoint>]
let main _ =
    let p =
        {
            Name = "Ada"
            Age = 36
        }

    printfn "%A" (validate p)
    printfn "%A" (Gen.sample 0 3 genPerson |> Seq.length)
    0
