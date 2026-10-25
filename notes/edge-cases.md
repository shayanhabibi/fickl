# Edge cases: valid F# that compiles wrong or not at all (2026-10-26)

`tests/Probe` runs about 500 small checks on .NET and as JavaScript and lists the results that differ
(`python tests/Probe/run.py`). Every check is valid F# that .NET compiles and runs. The cases below are what it found
with Fickl 0.6.7, compared with upstream Fable 5.13 on the same sources: all of them also happen in Fable except
the `%o` format, which Fickl already fixes.

Status: **bug** (Fickl's output is wrong and can be fixed), **limit** (JavaScript has no cheap equivalent; documented),
**gap** (a BCL member the runtime library lacks).

## Compiles to invalid or unloadable JavaScript

| Case | Result | Status |
| --- | --- | --- |
| `string (~~~0uy)` | `~0.toString()`: a JS syntax error, so the whole module fails to load. Also through a `let` bound constant | bug (#47) |
| `StringBuilder.Insert` (any member the library type lacks) | Imports `StringBuilder__Insert_…`, which `fable-library` does not export: the module fails at load, with no compile error | bug (#48) |

## Rejected by Fickl

| Case | Result | Status |
| --- | --- | --- |
| `inc &arr.[i]` (byref to an array element, also of a struct array) | "Cannot compile expression ILAsm" | bug (#49) |
| `typeof<'T>` in a static member of a generic type | "Cannot get type info of generic parameter T" | limit (generics are erased) |
| `System.Linq.Enumerable` | Type check error: not defined | gap |
| `Double.ToString("R")`, `Convert.ToBoolean`, `Convert.ChangeType`, `BitOperations.PopCount`, `ArgumentException.ParamName`, `StringComparer.OrdinalIgnoreCase`, `Array2D`, `List.BinarySearch`, `Array.BinarySearch`, `SortedDictionary`, `Environment.GetCommandLineArgs`, `Async.RunSynchronously` | "… is not supported by Fable" | gap |

## Compiles, wrong result

### Members, names and dispatch

| Case | .NET | JS | Status |
| --- | --- | --- | --- |
| A class or object expression implementing two interfaces that both declare `Value()` | each interface its own implementation | both call the last one: the class gets two methods with the same name | bug (#50) |
| Record field named `__proto__` | a field | `this.__proto__ = …` sets the prototype; the field reads `{ }` and copy-and-update loses it | bug (#51) |
| Top-level code of a later file | runs on first access to the file's values | runs when the module is imported | limit |
| `(box x).GetType().Name` for a record or `int` | `Rec`, `Int32` | `Object` | gap |
| `typeof<Rec>.FullName` for a type in a module | `Odd+Rec` | `Odd.Rec` | bug (#67) |
| `string DayOfWeek.Monday`, flags enums | `Monday`, `ReadOnly, Hidden` | `1`, `3` | limit (enum names are not emitted) |

### Byrefs and value types

| Case | .NET | JS | Status |
| --- | --- | --- | --- |
| `f &x` where `f` takes `inref<int>` and `x` is an immutable `let` | `x + 1` | `0`: the bare value is passed, the callee reads `.contents` | bug (#52) |
| `bump &p` where `bump (p: byref<MP>) = p.X <- …` and `MP` is a struct record | `p.X` changed | unchanged: the write goes to the reference cell, not to `p` | bug (#53) |
| Struct records and `[<Struct>] val mutable` copied by `let b = a`, passed as arguments, stored in lists, captured, boxed, read from properties or dictionaries | independent copies | one shared object: mutating one changes all | limit |
| `static let mutable` or `static member val` in a generic type | one per instantiation | one shared by all instantiations | limit |

### Numbers

| Case | .NET | JS | Status |
| --- | --- | --- | --- |
| `~~~z` for `byte` / `uint16` | `255` / `65535` | `-1` | bug (#47) |
| `1L <<< 65` | `2` (count masked to 6 bits) | `0` | bug (#54) |
| `Math.Round(2.5, MidpointRounding.AwayFromZero)` | `3` | `2.5` (not rounded) | bug (#55) |
| `nan.Equals(nan)` | `true` | `false` | bug (#56) |
| `float32 0.1 = 0.1f`; `0.1f + 0.2f`; `string 0.1f` | `true`; `0.3`; `0.1` | `false`; `0.30000000447034836`; `0.10000000149011612` | bug (#57) |
| `Double.TryParse "0x10"` | `false` | `true, 16` | bug (#58) |
| `sprintf "%e" 1234.5` | `1.234500e+003` | `1.2345e+3` | bug (#59) |
| `(0.1234).ToString("P1")` | `12.3%` (en-US) | `12.3 %` (the invariant culture's pattern) | limit |
| `byte`, `sbyte`, `int16`, `uint16` arithmetic (`255uy + 1uy`) | wraps | `256` | limit (`--clampByteArrays` covers arrays) |
| `int32` overflow in `-` and `*` (`Int32.MinValue - 1`, `123456789 * 987654321`) | wraps | not wrapped | limit |
| `1 / 0` for integers | `DivideByZeroException` | `0` | limit |
| `Checked.(+)`, `Checked.int`, `Checked.byte`, `abs Int32.MinValue`, `Int32.MinValue / -1` | `OverflowException` | wraps | limit |
| `int 3e10`, `int64 1e19` | saturate | wrap | limit |
| `string 1e21`, `string 1e-5`, `string -0.0` | `1E+21`, `1E-05`, `-0` | `1e+21`, `0.00001`, `0` | bug (#59) |
| `sign nan` | throws | `0` | bug (#59) |

### Strings, chars and formatting

| Case | .NET | JS | Status |
| --- | --- | --- | --- |
| `"a" + (null: string) + "b"` | `ab` | `anullb` | bug (#60) |
| `string (null: string)`, `sprintf "%s" null` | `""` | `null` | bug (#60) |
| `String.Format("{{{0}}}", 5)` | `{5}` | `{{5}}` | bug (#61) |
| `Char.ToUpper 'ß'` | `ß` (a char) | `SS` (two characters in a char) | bug (#62) |
| `"straße".ToUpperInvariant()` | `STRAßE` | `STRASSE` | limit |
| `string (Some 1)`, `string None`, `string ()`, `string (1, "a")` | `Some(1)`, `""`, `""`, `(1, a)` | `1`, `undefined`, `undefined`, `1,a` | bug (#63) |
| `compare "a" "B"` | `31` | `1` | limit (only the sign is specified) |

### Collections

| Case | .NET | JS | Status |
| --- | --- | --- | --- |
| `Array.blit a 0 a 1 4` (overlapping, same array) | `11234` | `11111` | bug (#64) |
| `arr.[-1..1]` | first two elements | empty | bug (#65) |

### Type tests and boxing

| Case | .NET | JS | Status |
| --- | --- | --- | --- |
| `:? I` on a class implementing interface `I` | matches | does not match | limit |
| `box "a"` tested against `:? char` first | `string` | `char` | limit |
| `box 1.0` / `box 1uy` / `box TimeSpan.Zero` against `:? int` | no match | matches | limit |
| `box [ "a" ]` against `:? int list`; `box (1, 2)` against `:? int[]` | no match | matches | limit (erased generics; tuples are arrays) |
| `box (Some 1)` against `:? int`; `box None` against `:? unit` | no match | matches | limit (erased options) |
| `box 1 = box 1.0`, `hash (box 1) = hash (box 1.0)` | `false` | `true` | limit |
| `unbox<float> (box 1)`, `(box 1) :?> string` | `InvalidCastException` | returns the value | limit |

### Exceptions

| Case | .NET | JS | Status |
| --- | --- | --- | --- |
| `invalidArg` caught by `:? ArgumentException` | matches | does not match | bug (#66) |
| Library exceptions (`KeyNotFoundException`, `IndexOutOfRangeException`, `FormatException`, `OverflowException`, `MatchFailureException`, …) | specific types | a plain `Error`, so `:? KeyNotFoundException` handlers do not run | limit |

### Depth

| Case | .NET | JS | Status |
| --- | --- | --- | --- |
| Mutually recursive functions, 10⁶ calls (`isEven`/`isOdd`) | completes (tail calls) | stack overflow | limit |
| CPS recursion, 10⁵ continuations | completes | stack overflow | limit |
| `seq { yield n; yield! gen (n - 1) }`, 10⁴ deep; `Seq.append` folded 10⁴ times | completes | stack overflow | bug (#68) |
| `[ yield n; yield! build (n - 1) ]`, 10⁴ deep | completes | stack overflow | bug (#68) |
| `=`, `compare`, `hash` on a union tree 10⁴ deep | completes | stack overflow | limit |
| `Async.StartImmediate` of 10⁵ nested binds | completes synchronously | still pending | limit (the trampoline yields) |

## What worked

Explicit interface implementations next to class members of the same name, interface inheritance, generic interface
methods, abstract and overridden members through `base`, custom operators (including a redefined `(..)`), type
extensions (F# and C# style, and extension next to intrinsic), computation expressions with `While`, `For`,
`TryFinally`, `and!` and custom operations, custom equality and comparison in `Set`/`Map`/`Dictionary`, `module rec`,
unicode and backtick identifiers, JavaScript reserved words and globals as names, reflection on unions and records,
optional, named and `ParamArray` arguments, units of measure, literals, delegates, events, active patterns, and
evaluation order of arguments, records, tuples and guards.
