module Fable.Tests.Compiler.UnsupportedBclMember

open Util.Testing
open Fable.Tests.Compiler.Util
open Fable.Tests.Compiler.Util.Compiler

let private compile source = Compiler.Cached.compile Compiler.Settings.standard source

/// DateTime members that fable-library does not implement, each with an expression using it.
let private unsupportedDateTimeMembers =
    [
        "ToOADate", "d.ToOADate()"
        "FromOADate", "DateTime.FromOADate(43831.0)"
        "ParseExact", "DateTime.ParseExact(\"21.10.2020\", \"dd.MM.yyyy\", CultureInfo.InvariantCulture)"
        "TryParseExact",
        "DateTime.TryParseExact(\"21.10.2020\", \"dd.MM.yyyy\", CultureInfo.InvariantCulture, DateTimeStyles.None)"
        "ToBinary", "d.ToBinary()"
        "FromBinary", "DateTime.FromBinary(0L)"
        "ToFileTime", "d.ToFileTime()"
        "ToFileTimeUtc", "d.ToFileTimeUtc()"
        "FromFileTime", "DateTime.FromFileTime(0L)"
        "FromFileTimeUtc", "DateTime.FromFileTimeUtc(0L)"
        "GetDateTimeFormats", "d.GetDateTimeFormats()"
        "AddMicroseconds", "d.AddMicroseconds(1.0)"
        "get_Microsecond", "d.Microsecond"
        "get_Nanosecond", "d.Nanosecond"
    ]

let tests =
    testList
        "Unsupported BCL members"
        [
            for name, expr in unsupportedDateTimeMembers do
                testCase $"DateTime.%s{name} is a compile error"
                <| fun _ ->
                    let source =
                        $"""
open System
open System.Globalization

let d = DateTime(2020, 1, 1)
let x = %s{expr}
"""

                    compile source
                    |> Assert.Is.error
                    |> Assert.Exists.errorWith $"System.DateTime.%s{name}"
                    |> ignore

            testCase "Supported DateTime members still compile"
            <| fun _ ->
                let source =
                    """
open System
open System.Globalization

let d = DateTime(2020, 1, 1)
let a = d.AddDays(1.0).ToUniversalTime().DayOfWeek
let b = DateTime.Parse("2020-01-01", CultureInfo.InvariantCulture)
let c = DateTime.TryParse("2020-01-01")
let e = d.ToLongDateString(), d.Ticks, d.Kind, d.TimeOfDay, DateTime.IsLeapYear 2020
let f = DateTime.Compare(d, b), d.CompareTo(b), d.Equals(b), d.Subtract(b), DateTime.DaysInMonth(2020, 2)
let g = DateTime.Now, DateTime.UtcNow, DateTime.Today, DateTime.SpecifyKind(d, DateTimeKind.Utc)
"""

                compile source |> Assert.Is.strictSuccess |> ignore
        ]
