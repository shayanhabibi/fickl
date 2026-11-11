module Fable.Tests.Array2DTests

open Util.Testing
open Fable.Tests.Util

type Cell =
    {
        Row: int
        Col: int
    }

type Grid = { Cells: int[,] }

let sumAll (grid: int[,]) =
    let mutable sum = 0

    for i = 0 to Array2D.length1 grid - 1 do
        for j = 0 to Array2D.length2 grid - 1 do
            sum <- sum + grid.[i, j]

    sum

let inline dimensions (grid: 'T[,]) = grid.GetLength(0), grid.GetLength(1)

let tests =
    testList
        "Array2D"
        [
            testCase "Array2D.zeroCreate, indexed set and get work" // See #319
            <| fun () ->
                let a = Array2D.zeroCreate<int> 2 3
                a.[1, 2] <- 5
                let b = Array2D.init 2 2 (fun i j -> i * 10 + j)
                equal 5 a.[1, 2]
                equal 0 a.[0, 0]
                equal 2 (Array2D.length1 a)
                equal 3 (Array2D.length2 a)
                equal 11 b.[1, 1]

            testCase "Array2D.zeroCreate fills with the zero value of the element type"
            <| fun () ->
                let floats = Array2D.zeroCreate<float> 2 2
                let bools = Array2D.zeroCreate<bool> 2 2
                let longs = Array2D.zeroCreate<int64> 2 2
                let strings = Array2D.zeroCreate<string> 2 2
                equal 0. floats.[1, 1]
                equal false bools.[1, 0]
                equal 0L longs.[0, 1]
                equal true (isNull strings.[0, 0])

            testCase "Array2D.create works"
            <| fun () ->
                let a = Array2D.create 3 2 "x"
                equal 3 (Array2D.length1 a)
                equal 2 (Array2D.length2 a)
                equal "x" a.[2, 1]
                a.[0, 0] <- "y"
                equal "y" a.[0, 0]
                equal "x" a.[0, 1]
                equal "x" a.[1, 0]

            testCase "Array2D.init works"
            <| fun () ->
                let a = Array2D.init 3 4 (fun i j -> { Row = i; Col = j })
                equal { Row = 2; Col = 3 } a.[2, 3]
                equal { Row = 0; Col = 1 } a.[0, 1]
                equal { Row = 1; Col = 0 } a.[1, 0]

            testCase "Array2D.init calls the initializer in row order"
            <| fun () ->
                let calls = ResizeArray()

                Array2D.init
                    2
                    2
                    (fun i j ->
                        calls.Add((i, j))
                        0
                    )
                |> ignore

                equal [ (0, 0); (0, 1); (1, 0); (1, 1) ] (List.ofSeq calls)

            testCase "array2D works with nested lists"
            <| fun () ->
                let a =
                    array2D
                        [
                            [ 1; 2; 3 ]
                            [ 4; 5; 6 ]
                        ]

                equal 2 (Array2D.length1 a)
                equal 3 (Array2D.length2 a)
                equal 6 a.[1, 2]
                equal 4 a.[1, 0]

            testCase "array2D works with nested arrays and sequences"
            <| fun () ->
                let a =
                    array2D
                        [|
                            [| "a"; "b" |]
                            [| "c"; "d" |]
                            [| "e"; "f" |]
                        |]

                equal 3 (Array2D.length1 a)
                equal 2 (Array2D.length2 a)
                equal "d" a.[1, 1]

                let b =
                    array2D (
                        seq {
                            for i in 0..1 ->
                                seq {
                                    for j in 0..2 -> float (i + j)
                                }
                        }
                    )

                equal 3. b.[1, 2]

            testCase "array2D copies its input"
            <| fun () ->
                let rows =
                    [|
                        [| 1; 2 |]
                        [| 3; 4 |]
                    |]

                let a = array2D rows
                a.[0, 0] <- 10
                rows.[1].[1] <- 40
                equal 1 rows.[0].[0]
                equal 4 a.[1, 1]

            testCase "array2D fails with rows of different lengths"
            <| fun () ->
                throwsAnyError (fun () ->
                    array2D
                        [
                            [ 1; 2 ]
                            [ 3 ]
                        ]
                )

            testCase "Array2D.get and Array2D.set work"
            <| fun () ->
                let a = Array2D.zeroCreate<int> 2 2
                Array2D.set a 1 0 7
                equal 7 (Array2D.get a 1 0)
                equal 7 a.[1, 0]
                equal 0 (Array2D.get a 0 1)

            testCase "2D array indices can be any expression"
            <| fun () ->
                let a = Array2D.init 3 3 (fun i j -> i * 3 + j)
                let mutable calls = 0

                let next () =
                    calls <- calls + 1
                    calls

                equal 5 a.[next (), next ()]
                equal 2 calls
                a.[next () - 3, next () - 2] <- 100
                equal 100 a.[0, 2]
                equal 4 calls

            testCase "2D array elements can be options and null"
            <| fun () ->
                let a = Array2D.create 2 2 (None: int option)
                equal None a.[1, 1]
                a.[1, 1] <- Some 3
                equal (Some 3) a.[1, 1]
                equal None a.[0, 1]
                let b = Array2D.zeroCreate<Cell option> 1 1
                equal None b.[0, 0]

            testCase "Reading a 2D array out of bounds fails"
            <| fun () ->
                let a = Array2D.zeroCreate<int> 2 3
                throwsAnyError (fun () -> a.[2, 0])
                throwsAnyError (fun () -> a.[0, 3])
                throwsAnyError (fun () -> a.[-1, 0])
                throwsAnyError (fun () -> a.[0, -1])
                throwsAnyError (fun () -> Array2D.get a 5 5)

            testCase "Length, GetLength and Rank work on 2D arrays"
            <| fun () ->
                let a = Array2D.zeroCreate<int> 2 3
                equal 6 a.Length
                equal 2 (a.GetLength(0))
                equal 3 (a.GetLength(1))
                equal 2 a.Rank
                let dimension = 1
                equal 3 (a.GetLength(dimension))
                equal (2, 3) (dimensions a)
                equal (1, 4) (dimensions (Array2D.create 1 4 "a"))

            testCase "Length of a jagged array is still the number of rows"
            <| fun () ->
                let jagged =
                    [|
                        [| 1; 2; 3 |]
                        [| 4; 5; 6 |]
                    |]

                equal 2 jagged.Length

            testCase "Array2D.base1 and Array2D.base2 are zero"
            <| fun () ->
                let a = Array2D.zeroCreate<int> 2 3
                equal 0 (Array2D.base1 a)
                equal 0 (Array2D.base2 a)

            testCase "Empty 2D arrays work"
            <| fun () ->
                let a = Array2D.zeroCreate<int> 0 0
                equal 0 (Array2D.length1 a)
                equal 0 (Array2D.length2 a)
                equal 0 a.Length
                let b = Array2D.zeroCreate<int> 3 0
                equal 3 (Array2D.length1 b)
                equal 0 (Array2D.length2 b)
                equal 0 b.Length
                let c = Array2D.map string b
                equal 3 (Array2D.length1 c)

            testCase "Creating a 2D array with a negative length fails"
            <| fun () ->
                throwsAnyError (fun () -> Array2D.zeroCreate<int> -1 2)
                throwsAnyError (fun () -> Array2D.create 2 -1 0)
                throwsAnyError (fun () -> Array2D.init -1 -1 (fun _ _ -> 0))

            testCase "Array2D.map works"
            <| fun () ->
                let a = Array2D.init 2 3 (fun i j -> i * 3 + j)
                let b = a |> Array2D.map (fun x -> string (x * 2))
                equal 2 (Array2D.length1 b)
                equal 3 (Array2D.length2 b)
                equal "10" b.[1, 2]
                equal "0" b.[0, 0]
                equal 5 a.[1, 2]

            testCase "Array2D.mapi works"
            <| fun () ->
                let a = Array2D.create 2 2 1
                let b = a |> Array2D.mapi (fun i j x -> i * 100 + j * 10 + x)
                equal 1 b.[0, 0]
                equal 11 b.[0, 1]
                equal 101 b.[1, 0]
                equal 111 b.[1, 1]

            testCase "Array2D.iter works"
            <| fun () ->
                let a = Array2D.init 2 3 (fun i j -> i * 3 + j)
                let seen = ResizeArray()
                a |> Array2D.iter seen.Add
                equal [ 0; 1; 2; 3; 4; 5 ] (List.ofSeq seen)

            testCase "Array2D.iteri works"
            <| fun () ->
                let a = Array2D.init 2 2 (fun i j -> i + j)
                let seen = ResizeArray()
                a |> Array2D.iteri (fun i j x -> seen.Add((i, j, x)))

                equal [ (0, 0, 0); (0, 1, 1); (1, 0, 1); (1, 1, 2) ] (List.ofSeq seen)

            testCase "Array2D.copy works"
            <| fun () ->
                let a = Array2D.init 2 2 (fun i j -> i + j)
                let b = Array2D.copy a
                b.[0, 0] <- 50
                equal 0 a.[0, 0]
                equal 50 b.[0, 0]
                equal 2 b.[1, 1]
                equal 2 (Array2D.length2 b)

            testCase "Array2D.blit works"
            <| fun () ->
                let source = Array2D.init 3 3 (fun i j -> i * 3 + j + 1)
                let target = Array2D.zeroCreate<int> 4 4
                Array2D.blit source 1 1 target 2 1 2 2
                equal 5 target.[2, 1]
                equal 6 target.[2, 2]
                equal 8 target.[3, 1]
                equal 9 target.[3, 2]
                equal 0 target.[0, 0]
                equal 0 target.[2, 0]
                equal 0 target.[2, 3]

            testCase "Array2D.blit fails when the range does not fit"
            <| fun () ->
                let source = Array2D.zeroCreate<int> 2 2
                let target = Array2D.zeroCreate<int> 2 2
                throwsAnyError (fun () -> Array2D.blit source 1 0 target 0 0 2 1)
                throwsAnyError (fun () -> Array2D.blit source 0 0 target 0 1 1 2)
                throwsAnyError (fun () -> Array2D.blit source -1 0 target 0 0 1 1)

            testCase "2D arrays can be passed to functions and kept in records"
            <| fun () ->
                let grid = { Cells = Array2D.init 3 3 (fun i j -> i * j) }
                equal 9 (sumAll grid.Cells)
                grid.Cells.[0, 0] <- 1
                equal 10 (sumAll grid.Cells)

            testCase "2D arrays of floats keep fractional values"
            <| fun () ->
                let a = Array2D.zeroCreate<float> 2 2
                a.[0, 1] <- 1.5
                a.[1, 0] <- a.[0, 1] * 2.
                equal 3. a.[1, 0]
                equal 4.5 (a.[0, 1] + a.[1, 0])
        ]
