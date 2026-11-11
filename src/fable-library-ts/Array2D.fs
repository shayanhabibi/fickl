module Array2DModule

// A `'T[,]` is an array of rows, and each row is a plain JS array whatever the element type.
// The second length of an array with no rows is not kept, so it reads as 0.

let private checkNonNegative (argName: string) (value: int) =
    if value < 0 then
        invalidArg argName SR.inputMustBeNonNegative

let length1 (array: ResizeArray<'T>[]) : int = array.Length

let length2 (array: ResizeArray<'T>[]) : int =
    if array.Length = 0 then
        0
    else
        array.[0].Count

let length (array: ResizeArray<'T>[]) : int = length1 array * length2 array

let getLength (array: ResizeArray<'T>[]) (dimension: int) : int =
    match dimension with
    | 0 -> length1 array
    | 1 -> length2 array
    | _ -> raise (System.IndexOutOfRangeException())

let private checkIndices (array: ResizeArray<'T>[]) (index1: int) (index2: int) =
    if
        index1 < 0
        || index1 >= array.Length
        || index2 < 0
        || index2 >= array.[index1].Count
    then
        raise (System.IndexOutOfRangeException())

let item (array: ResizeArray<'T>[]) (index1: int) (index2: int) : 'T =
    checkIndices array index1 index2
    let row = array.[index1]
    row.[index2]

let setItem (array: ResizeArray<'T>[]) (index1: int) (index2: int) (value: 'T) : unit =
    checkIndices array index1 index2
    let row = array.[index1]
    row.[index2] <- value

let initialize (length1: int) (length2: int) (initializer: int -> int -> 'T) : ResizeArray<'T>[] =
    checkNonNegative "length1" length1
    checkNonNegative "length2" length2
    let rows = Array.zeroCreate<ResizeArray<'T>> length1

    for i = 0 to length1 - 1 do
        let row = ResizeArray<'T>()

        for j = 0 to length2 - 1 do
            row.Add(initializer i j)

        rows.[i] <- row

    rows

let create (length1: int) (length2: int) (value: 'T) : ResizeArray<'T>[] =
    initialize length1 length2 (fun _ _ -> value)

let ofRows (source: seq<seq<'T>>) : ResizeArray<'T>[] =
    let rows = source |> Seq.map ResizeArray |> Seq.toArray

    for i = 1 to rows.Length - 1 do
        if rows.[i].Count <> rows.[0].Count then
            invalidArg "vals" "The arrays have different lengths."

    rows

let map (mapping: 'T -> 'U) (array: ResizeArray<'T>[]) : ResizeArray<'U>[] =
    initialize
        (length1 array)
        (length2 array)
        (fun i j ->
            let row = array.[i]
            mapping row.[j]
        )

let mapIndexed (mapping: int -> int -> 'T -> 'U) (array: ResizeArray<'T>[]) : ResizeArray<'U>[] =
    initialize
        (length1 array)
        (length2 array)
        (fun i j ->
            let row = array.[i]
            mapping i j row.[j]
        )

let copy (array: ResizeArray<'T>[]) : ResizeArray<'T>[] = map id array

let iterate (action: 'T -> unit) (array: ResizeArray<'T>[]) : unit =
    for i = 0 to array.Length - 1 do
        let row = array.[i]

        for j = 0 to row.Count - 1 do
            action row.[j]

let iterateIndexed (action: int -> int -> 'T -> unit) (array: ResizeArray<'T>[]) : unit =
    for i = 0 to array.Length - 1 do
        let row = array.[i]

        for j = 0 to row.Count - 1 do
            action i j row.[j]

let blit
    (source: ResizeArray<'T>[])
    (sourceIndex1: int)
    (sourceIndex2: int)
    (target: ResizeArray<'T>[])
    (targetIndex1: int)
    (targetIndex2: int)
    (count1: int)
    (count2: int)
    : unit
    =
    let checkRange (argName: string) (index: int) (count: int) (length: int) =
        if index < 0 || count < 0 || index + count > length then
            invalidArg argName "The index is outside the legal range."

    checkRange "sourceIndex1" sourceIndex1 count1 (length1 source)
    checkRange "sourceIndex2" sourceIndex2 count2 (length2 source)
    checkRange "targetIndex1" targetIndex1 count1 (length1 target)
    checkRange "targetIndex2" targetIndex2 count2 (length2 target)

    for i = 0 to count1 - 1 do
        let sourceRow = source.[sourceIndex1 + i]
        let targetRow = target.[targetIndex1 + i]

        for j = 0 to count2 - 1 do
            targetRow.[targetIndex2 + j] <- sourceRow.[sourceIndex2 + j]
