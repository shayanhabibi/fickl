module Fable.Tests.Compiler.UnsupportedBclMember

open Util.Testing
open Fable.Tests.Compiler.Util
open Fable.Tests.Compiler.Util.Compiler

let private compile source = Compiler.Cached.compile Compiler.Settings.standard source

let private compileWithTemporal source = Compiler.Cached.compileWithTemporal Compiler.Settings.standard source

let private preamble =
    """
open System
open System.Globalization

let d = DateTime(2020, 1, 1)
let dto = DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)
let dOnly = DateOnly(2020, 1, 1)
let tOnly = TimeOnly(10, 30)
let ts = TimeSpan(1, 2, 3)
"""

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

let private unsupportedDateTimeOffsetMembers =
    [
        "ParseExact", "DateTimeOffset.ParseExact(\"21.10.2020\", \"dd.MM.yyyy\", CultureInfo.InvariantCulture)"
        "ToFileTime", "dto.ToFileTime()"
        "FromFileTime", "DateTimeOffset.FromFileTime(0L)"
        "AddMicroseconds", "dto.AddMicroseconds(1.0)"
        "get_Microsecond", "dto.Microsecond"
        "get_Nanosecond", "dto.Nanosecond"
        "get_TotalOffsetMinutes", "dto.TotalOffsetMinutes"
    ]

let private unsupportedDateOnlyMembers =
    [
        "ParseExact", "DateOnly.ParseExact(\"21.10.2020\", \"dd.MM.yyyy\", CultureInfo.InvariantCulture)"
        "ToLongDateString", "dOnly.ToLongDateString()"
        "ToShortDateString", "dOnly.ToShortDateString()"
    ]

let private unsupportedTimeOnlyMembers =
    [
        "ParseExact", "TimeOnly.ParseExact(\"10:30\", \"HH:mm\", CultureInfo.InvariantCulture)"
        "ToLongTimeString", "tOnly.ToLongTimeString()"
        "ToShortTimeString", "tOnly.ToShortTimeString()"
    ]

let private unsupportedTimeSpanMembers =
    [
        "ParseExact", "TimeSpan.ParseExact(\"01:02:03\", \"c\", CultureInfo.InvariantCulture)"
        "FromMicroseconds", "TimeSpan.FromMicroseconds(1.0)"
        "get_Microseconds", "ts.Microseconds"
        "get_Nanoseconds", "ts.Nanoseconds"
        "get_TotalMicroseconds", "ts.TotalMicroseconds"
    ]

/// Members only the Temporal runtime modules implement: an error by default, supported with --temporal.
let private temporalOnlyMembers =
    [
        "TimeOnly", "get_Microsecond", "tOnly.Microsecond"
        "TimeOnly", "get_Nanosecond", "tOnly.Nanosecond"
        "TimeSpan", "get_TotalNanoseconds", "ts.TotalNanoseconds"
    ]

let private unsupportedMembers =
    [
        "DateTime", unsupportedDateTimeMembers
        "DateTimeOffset", unsupportedDateTimeOffsetMembers
        "DateOnly", unsupportedDateOnlyMembers
        "TimeOnly", unsupportedTimeOnlyMembers
        "TimeSpan", unsupportedTimeSpanMembers
    ]

/// Members that are supported, taken from tests/Js/Main: none of them may become an error.
let private supportedMembers =
    [
        "DateTime",
        """
let a = d.AddDays(1.0).ToUniversalTime().DayOfWeek, d.ToLocalTime(), d.IsDaylightSavingTime()
let b = DateTime.Parse("2020-01-01", CultureInfo.InvariantCulture)
let c = DateTime.TryParse("2020-01-01"), DateTime.TryParse("2020-01-01", CultureInfo.InvariantCulture, DateTimeStyles.None)
let e = d.ToLongDateString(), d.ToShortDateString(), d.ToLongTimeString(), d.ToShortTimeString()
let f = DateTime.Compare(d, b), d.CompareTo(b), d.Equals(b), d.Subtract(b), d.Subtract(ts), DateTime.DaysInMonth(2020, 2)
let g = DateTime.Now, DateTime.UtcNow, DateTime.Today, DateTime.SpecifyKind(d, DateTimeKind.Utc)
let h = d.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second, d.Millisecond, d.DayOfYear, d.Ticks, d.Kind
let j = d.Date, d.TimeOfDay, DateTime.IsLeapYear 2020, DateTime.MinValue, DateTime.MaxValue, DateTime(0L), DateTime()
let k = d.AddYears(1), d.AddMonths(1), d.AddHours(1.0), d.AddMinutes(1.0), d.AddSeconds(1.0), d.AddMilliseconds(1.0)
let l = d.AddTicks(1L), d.Add(ts), d + ts, d - ts, d - b, d < b, d = b, d.GetHashCode(), compare d b
let m = d.ToString(), d.ToString("O"), d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
let n = DateTime(2020, 1, 1, 1, 2, 3), DateTime(2020, 1, 1, 1, 2, 3, DateTimeKind.Utc), DateTime(2020, 1, 1, 1, 2, 3, 4)
let o = DateTime(dOnly, tOnly), dto.DateTime, dto.UtcDateTime, dto.LocalDateTime
"""
        "DateTimeOffset",
        """
let a = dto.AddDays(1.0).ToUniversalTime().DayOfWeek, dto.ToLocalTime(), dto.ToOffset(TimeSpan.FromHours 1.0)
let b = DateTimeOffset.Parse("2020-01-01", CultureInfo.InvariantCulture), DateTimeOffset.Parse("2020-01-01")
let c = DateTimeOffset.TryParse("2020-01-01"), DateTimeOffset(d), DateTimeOffset(d, ts), DateTimeOffset(0L, TimeSpan.Zero)
let e = dto.Year, dto.Month, dto.Day, dto.Hour, dto.Minute, dto.Second, dto.Millisecond, dto.DayOfYear
let f = dto.Ticks, dto.UtcTicks, dto.Offset, dto.Date, dto.TimeOfDay, dto.DateTime, dto.UtcDateTime, dto.LocalDateTime
let g = DateTimeOffset.Now, DateTimeOffset.UtcNow, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, DateTimeOffset()
let h = dto.AddYears(1), dto.AddMonths(1), dto.AddHours(1.0), dto.AddMinutes(1.0), dto.AddSeconds(1.0)
let j = dto.AddMilliseconds(1.0), dto.AddTicks(1L), dto.Add(ts), dto.Subtract(ts), dto.Subtract(dto), dto + ts, dto - ts
let k = dto - dto, dto < dto, dto = dto, dto.Equals(dto), dto.EqualsExact(dto), dto.CompareTo(dto), dto.GetHashCode()
let l = DateTimeOffset.Compare(dto, dto), compare dto dto, dto.ToString(), dto.ToString("O")
let m = dto.ToUnixTimeSeconds(), dto.ToUnixTimeMilliseconds()
let n = DateTimeOffset.FromUnixTimeSeconds(0L), DateTimeOffset.FromUnixTimeMilliseconds(0L)
let o: DateTimeOffset = DateTimeOffset.op_Implicit d
"""
        "DateOnly",
        """
let a = dOnly.Year, dOnly.Month, dOnly.Day, dOnly.DayOfWeek, dOnly.DayOfYear, dOnly.DayNumber
let b = DateOnly.MinValue, DateOnly.MaxValue, DateOnly.FromDayNumber 1, DateOnly.FromDateTime d
let c = dOnly.AddDays 1, dOnly.AddMonths 1, dOnly.AddYears 1, dOnly.ToDateTime tOnly
let e = dOnly.ToDateTime(tOnly, DateTimeKind.Utc), dOnly.ToString(CultureInfo.InvariantCulture)
let f = dOnly.ToString("d", CultureInfo.InvariantCulture), dOnly.ToString("O", CultureInfo.InvariantCulture)
let g = DateOnly.Parse("2020-01-01"), DateOnly.Parse("2020-01-01", CultureInfo.InvariantCulture)
let h = DateOnly.TryParse("2020-01-01"), dOnly < dOnly, dOnly = dOnly, dOnly.GetHashCode(), compare dOnly dOnly
let j = dOnly.Equals(dOnly), dOnly.Equals(box dOnly), dOnly.CompareTo(dOnly), dOnly.CompareTo(box dOnly)
"""
        "TimeOnly",
        """
let a = tOnly.Hour, tOnly.Minute, tOnly.Second, tOnly.Millisecond, tOnly.Ticks
let b = TimeOnly.MinValue, TimeOnly.MaxValue, TimeOnly(0L), TimeOnly(1, 2, 3), TimeOnly(1, 2, 3, 4)
let c = TimeOnly.FromTimeSpan ts, TimeOnly.FromDateTime d, tOnly.ToTimeSpan()
let e = tOnly.Add ts, tOnly.AddHours 1.0, tOnly.AddMinutes 1.0, tOnly.IsBetween(tOnly, tOnly), tOnly - tOnly
let f = tOnly.ToString(CultureInfo.InvariantCulture), tOnly.ToString("T", CultureInfo.InvariantCulture)
let g = TimeOnly.Parse("10:30"), TimeOnly.Parse("10:30", CultureInfo.InvariantCulture), TimeOnly.TryParse("10:30")
let h = tOnly < tOnly, tOnly = tOnly, tOnly.GetHashCode(), compare tOnly tOnly
let j = tOnly.Equals(tOnly), tOnly.Equals(box tOnly), tOnly.CompareTo(tOnly), tOnly.CompareTo(box tOnly)
"""
        "TimeSpan",
        """
let a = ts.Days, ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds, ts.Ticks
let b = ts.TotalDays, ts.TotalHours, ts.TotalMinutes, ts.TotalSeconds, ts.TotalMilliseconds
let c = TimeSpan.FromDays 1.0, TimeSpan.FromHours 1.0, TimeSpan.FromMinutes 1.0, TimeSpan.FromSeconds 1.0
let e = TimeSpan.FromMilliseconds 1.0, TimeSpan.FromTicks 1L, TimeSpan(1L), TimeSpan(1, 2, 3, 4), TimeSpan(1, 2, 3, 4, 5)
let f = TimeSpan.Zero, ts.Negate(), ts.Add ts, ts.Subtract ts, ts.Multiply 2.0, ts.Divide 2.0, ts.Divide ts, ts.Duration()
let g = ts + ts, ts - ts, ts * 2.0, 2.0 * ts, ts / 2.0, ts / ts, -ts, ts < ts, ts = ts
let h = TimeSpan.Compare(ts, ts), ts.CompareTo ts, ts.GetHashCode(), compare ts ts
let j = ts.ToString(), ts.ToString("c", CultureInfo.InvariantCulture), ts.ToString("g", CultureInfo.InvariantCulture)
let l = ts.Equals(ts), ts.Equals(box ts), TimeSpan.Equals(ts, ts), ts.CompareTo(box ts)
let k = TimeSpan.Parse("01:02:03"), TimeSpan.Parse("01:02:03", CultureInfo.InvariantCulture), TimeSpan.TryParse("01:02:03")
"""
    ]

/// The values a fable-library-ts module exports by name (`export default` and types are not importable by name).
let private exportsOf (moduleName: string) =
    let path =
        System.IO.Path.Combine(__SOURCE_DIRECTORY__, "../../../src/fable-library-ts", moduleName + ".ts")

    System.Text.RegularExpressions.Regex.Matches(
        System.IO.File.ReadAllText path,
        @"^export\s+(?:declare\s+)?(?:abstract\s+)?(?:async\s+)?(?:function\*?|class|const|let|var)\s+([A-Za-z0-9_$]+)",
        System.Text.RegularExpressions.RegexOptions.Multiline
    )
    |> Seq.map (fun m -> m.Groups[1].Value)
    |> Set.ofSeq

let tests =
    testList
        "Unsupported BCL members"
        [
            // The compiler decides what is supported from this table, so it must follow the runtime library.
            for KeyValue(moduleName, names) in Fable.Transforms.JS.Replacements.dateLibraryExports do
                testCase $"dateLibraryExports matches the exports of %s{moduleName}.ts"
                <| fun _ ->
                    let actual = exportsOf moduleName
                    let missing = actual - names |> String.concat ", "
                    let extra = names - actual |> String.concat ", "

                    if missing <> "" || extra <> "" then
                        failwith
                            $"dateLibraryExports in Replacements.fs does not match %s{moduleName}.ts. Add to \"%s{moduleName}\": [%s{missing}]. Remove from it: [%s{extra}]."

            testCase "dateLibraryExports covers every date and time module"
            <| fun _ ->
                let modules =
                    Fable.Transforms.JS.Replacements.dateLibraryExports |> Map.toList |> List.map fst

                equal
                    [
                        "Date"
                        "DateOffset"
                        "DateOnly"
                        "DateOnlyTemporal"
                        "DateTimeOffsetTemporal"
                        "DateTimeTemporal"
                        "TimeOnly"
                        "TimeOnlyTemporal"
                        "TimeSpan"
                        "TimeSpanTemporal"
                    ]
                    modules

            for mode, compile in [ "", compile; " with --temporal", compileWithTemporal ] do
                for typeName, members in unsupportedMembers do
                    for name, expr in members do
                        testCase $"%s{typeName}.%s{name} is a compile error%s{mode}"
                        <| fun _ ->
                            compile $"%s{preamble}\nlet x = %s{expr}\n"
                            |> Assert.Is.error
                            |> Assert.Exists.errorWith $"System.%s{typeName}.%s{name}"
                            |> ignore

                for typeName, source in supportedMembers do
                    testCase $"Supported %s{typeName} members still compile%s{mode}"
                    <| fun _ -> compile (preamble + source) |> Assert.Is.strictSuccess |> ignore

            for typeName, name, expr in temporalOnlyMembers do
                testCase $"%s{typeName}.%s{name} is a compile error"
                <| fun _ ->
                    compile $"%s{preamble}
let x = %s{expr}
"
                    |> Assert.Is.error
                    |> Assert.Exists.errorWith $"System.%s{typeName}.%s{name}"
                    |> ignore

                testCase $"%s{typeName}.%s{name} compiles with --temporal"
                <| fun _ ->
                    compileWithTemporal $"%s{preamble}
let x = %s{expr}
"
                    |> Assert.Is.strictSuccess
                    |> ignore
        ]
