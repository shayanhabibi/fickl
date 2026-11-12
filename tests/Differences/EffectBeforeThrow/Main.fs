// Change: a binding with effects before a throw
// Fickl: A binding whose value has effects runs where it is declared, not after or inside a later throw.
// Issues: 408
module Main

exception EffectFailed of int * int

let effect (log: ResizeArray<string>) (name: string) (value: 'T) : 'T =
    log.Add name
    value

let effectsOf (f: ResizeArray<string> -> int) =
    let log = ResizeArray<string>()

    try
        f log |> ignore
    with _ ->
        log.Add "threw"

    List.ofSeq log

let effectAfterThrow (log: ResizeArray<string>) : int =
    let v = effect log "v" 1
    failwith "boom" |> ignore
    v

let effectInThrownValue (log: ResizeArray<string>) : int =
    let v = effect log "v" 1
    raise (EffectFailed(effect log "w" 2, v))

Check.equal [ "v"; "threw" ] (effectsOf effectAfterThrow)
Check.equal [ "v"; "w"; "threw" ] (effectsOf effectInThrownValue)
