// Change: offsets in `DateTimeOffset.Parse`
// Fickl: `DateTimeOffset.Parse` of an ISO string keeps the offset written in it, not the local one.
// Issues: 268
module Main

open System

let utc = DateTimeOffset.Parse "2020-06-01T00:00:00+00:00"
Check.equal TimeSpan.Zero utc.Offset
Check.equal 0 utc.Hour

let east = DateTimeOffset.Parse "2020-06-01T10:30:00+02:00"
Check.equal (TimeSpan.FromHours 2.) east.Offset
Check.equal 10 east.Hour
