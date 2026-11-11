module Pipelines

// fickl: compiled with --debugPipelines, each stage of a forward pipeline is a statement that binds its result (#305)

let increment x = x + 1
let double x = x * 2

/// One binding for each stage but the last, which is returned
let chain (xs: int list) =
    xs
    |> List.filter (fun x -> x % 2 = 0)
    |> List.map (fun x -> x * x)
    |> List.sum

/// A first value that is a call is bound like a stage
let fromCall x = increment x |> double |> increment

/// A name or a constant is piped as it is
let fromName x = x |> increment

/// The values of `||>` and `|||>` are bound from left to right
let two a b = (increment a, double b) ||> max |> double
let three a = (increment a, double a, a) |||> fun x y z -> x + y + z

/// Where a statement cannot stand the stages are assignments in a sequence
let inCondition (xs: int list) =
    if (xs |> List.map increment |> List.sum) > 10 then
        "big"
    else
        "small"

let inArgument (xs: int list) =
    double (xs |> List.map increment |> List.sum)

/// A lambda as the last stage is applied to the binding
let lambdaStage (xs: int list) =
    xs |> List.rev |> fun ys -> ys.Length + 1

let inAsync (xs: int list) =
    async {
        let! total = async { return xs |> List.map increment |> List.sum }
        return total |> double |> increment
    }

/// The name of a binding does not clash with one of the user's
let userNamed x =
    let pipe = increment x
    let pipe_1 = double pipe
    (pipe + pipe_1) |> increment |> double

/// `<|` and `>>` are left as they are
let backward x = double <| (increment <| double x)
let composed = increment >> double >> increment
