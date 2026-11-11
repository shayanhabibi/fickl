module FSharp.Core.OperatorIntrinsics

let makeRangeStepFunction<'T when 'T: comparison> (step: 'T) (stop: 'T) (zero: 'T) (add: 'T -> 'T -> 'T) =
    let stepComparedWithZero = compare step zero

    if stepComparedWithZero = 0 then
        invalidArg "step" "The step of a range cannot be zero."

    let stepGreaterThanZero = stepComparedWithZero > 0

    fun x ->
        let comparedWithLast = compare x stop

        if
            (stepGreaterThanZero && comparedWithLast <= 0)
            || (not stepGreaterThanZero && comparedWithLast >= 0)
        then
            Some(x, add x step)
        else
            None

let integralRangeStep<'T when 'T: comparison> (start: 'T) (step: 'T) (stop: 'T) (zero: 'T) (add: 'T -> 'T -> 'T) =
    let stepFn = makeRangeStepFunction step stop zero add
    Seq.delay (fun () -> Seq.unfold stepFn start)

let rangeBigInt start step stop =
    integralRangeStep start step stop 0I (+)

let rangeDecimal start step stop =
    integralRangeStep start step stop 0m (+)

// As FSharp.Core: when the step divides the range exactly, each item is start + i * step, so the rounding error of
// adding the step does not accumulate and the last item is not lost. Otherwise the step is added to the previous item
let rangeDouble (start: float) (step: float) (stop: float) =
    if step = 0.0 then
        invalidArg "step" "The step of a range cannot be zero."

    if System.Double.IsNaN start then
        invalidArg "n" "The start of a range cannot be NaN."

    if System.Double.IsNaN step then
        invalidArg "step" "The step of a range cannot be NaN."

    if System.Double.IsNaN stop then
        invalidArg "m" "The end of a range cannot be NaN."

    let count = (stop - start) / step

    if count = floor count then
        let stepFn i =
            if i <= count then
                Some(start + i * step, i + 1.0)
            else
                None

        Seq.delay (fun () -> Seq.unfold stepFn 0.0)
    else
        let stepFn x =
            if (step > 0.0 && x <= stop) || (step < 0.0 && x >= stop) then
                Some(x, x + step)
            else
                None

        Seq.delay (fun () -> Seq.unfold stepFn start)

let rangeInt64 start step stop =
    integralRangeStep start step stop 0L (+)

let rangeUInt64 start step stop =
    integralRangeStep start step stop 0UL (+)

let rangeChar (start: char) (stop: char) =
    let intStop = int stop

    let stepFn i =
        if i <= intStop then
            Some(char i, i + 1)
        else
            None

    Seq.delay (fun () -> Seq.unfold stepFn (int start))
