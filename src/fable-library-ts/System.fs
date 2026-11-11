namespace System

type SystemException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = SystemException(SR.Arg_SystemException, Unchecked.defaultof<exn>)
    new(message: string) = SystemException(message, Unchecked.defaultof<exn>)

type ApplicationException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = ApplicationException(SR.Arg_ApplicationException, Unchecked.defaultof<exn>)
    new(message: string) = ApplicationException(message, Unchecked.defaultof<exn>)

type ArithmeticException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = ArithmeticException(SR.Arg_ArithmeticException, Unchecked.defaultof<exn>)
    new(message: string) = ArithmeticException(message, Unchecked.defaultof<exn>)

type DivideByZeroException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = DivideByZeroException(SR.Arg_DivideByZero, Unchecked.defaultof<exn>)
    new(message: string) = DivideByZeroException(message, Unchecked.defaultof<exn>)

type FormatException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = FormatException(SR.Arg_FormatException, Unchecked.defaultof<exn>)
    new(message: string) = FormatException(message, Unchecked.defaultof<exn>)

type IndexOutOfRangeException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = IndexOutOfRangeException(SR.Arg_IndexOutOfRangeException, Unchecked.defaultof<exn>)
    new(message: string) = IndexOutOfRangeException(message, Unchecked.defaultof<exn>)

type InvalidOperationException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = InvalidOperationException(SR.Arg_InvalidOperationException, Unchecked.defaultof<exn>)
    new(message: string) = InvalidOperationException(message, Unchecked.defaultof<exn>)

type NotFiniteNumberException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = NotFiniteNumberException(SR.Arg_NotFiniteNumberException, Unchecked.defaultof<exn>)
    new(message: string) = NotFiniteNumberException(message, Unchecked.defaultof<exn>)

type NotImplementedException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = NotImplementedException(SR.Arg_NotImplementedException, Unchecked.defaultof<exn>)
    new(message: string) = NotImplementedException(message, Unchecked.defaultof<exn>)

type NotSupportedException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = NotSupportedException(SR.Arg_NotSupportedException, Unchecked.defaultof<exn>)
    new(message: string) = NotSupportedException(message, Unchecked.defaultof<exn>)

type NullReferenceException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = NullReferenceException(SR.Arg_NullReferenceException, Unchecked.defaultof<exn>)
    new(message: string) = NullReferenceException(message, Unchecked.defaultof<exn>)

type OutOfMemoryException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = OutOfMemoryException(SR.Arg_OutOfMemoryException, Unchecked.defaultof<exn>)
    new(message: string) = OutOfMemoryException(message, Unchecked.defaultof<exn>)

type OverflowException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = OverflowException(SR.Arg_OverflowException, Unchecked.defaultof<exn>)
    new(message: string) = OverflowException(message, Unchecked.defaultof<exn>)

type RankException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = RankException(SR.Arg_RankException, Unchecked.defaultof<exn>)
    new(message: string) = RankException(message, Unchecked.defaultof<exn>)

type StackOverflowException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = StackOverflowException(SR.Arg_StackOverflowException, Unchecked.defaultof<exn>)
    new(message: string) = StackOverflowException(message, Unchecked.defaultof<exn>)

type TimeoutException(message: string, innerException: exn | null) =
    inherit Exception(message, innerException)
    new() = TimeoutException(SR.Arg_TimeoutException, Unchecked.defaultof<exn>)
    new(message: string) = TimeoutException(message, Unchecked.defaultof<exn>)

type ArgumentException(message: string, paramName: string, innerException: exn | null) =
    inherit
        Exception(
            (if System.String.IsNullOrEmpty(paramName) then
                 message
             else
                 message + SR.Arg_ParamName_Name + paramName + "')"),
            innerException
        )

    // Use Unchecked.defaultof rather than a `null` literal for the absent parameter name
    // and inner exception: Fable erases nullable reference annotations, so a `null` literal
    // would be emitted against a non-nullable parameter type and fail type checking.
    new() = ArgumentException(SR.Arg_ArgumentException, Unchecked.defaultof<string>, Unchecked.defaultof<exn>)
    new(message) = ArgumentException(message, Unchecked.defaultof<string>, Unchecked.defaultof<exn>)
    new(message: string, paramName: string) = ArgumentException(message, paramName, Unchecked.defaultof<exn>)

    new(message: string, innerException: exn | null)
        =
        ArgumentException(message, Unchecked.defaultof<string>, innerException)

    member _.ParamName = paramName

type ArgumentNullException(paramName: string, message: string, innerException: exn | null) =
    inherit ArgumentException(message, paramName, innerException)
    new(paramName: string, message: string) = ArgumentNullException(paramName, message, Unchecked.defaultof<exn>)

    new(message: string, innerException: exn | null)
        =
        ArgumentNullException(Unchecked.defaultof<string>, message, innerException)

    new(paramName) = ArgumentNullException(paramName, SR.ArgumentNull_Generic)
    new() = ArgumentNullException(Unchecked.defaultof<string>)

type ArgumentOutOfRangeException(paramName: string, message: string, innerException: exn | null) =
    inherit ArgumentException(message, paramName, innerException)

    new(paramName: string, message: string) = ArgumentOutOfRangeException(paramName, message, Unchecked.defaultof<exn>)

    new(message: string, innerException: exn | null)
        =
        ArgumentOutOfRangeException(Unchecked.defaultof<string>, message, innerException)

    new(paramName) = ArgumentOutOfRangeException(paramName, SR.Arg_ArgumentOutOfRangeException)
    new() = ArgumentOutOfRangeException(Unchecked.defaultof<string>)
