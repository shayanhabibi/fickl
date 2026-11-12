// Change: `TimeSpan` ticks
// Fickl: `TimeSpan.FromTicks` keeps sub-millisecond ticks and reads back `Int64.MaxValue`, and `ToString` prints all seven fraction digits.
// Issues: 264
module Main

open System

Check.equal 1L (TimeSpan.FromTicks(1L).Ticks)
Check.equal 1234567L (TimeSpan.FromTicks(1234567L).Ticks)
Check.equal "00:00:00.1234567" (TimeSpan.FromTicks(1234567L).ToString())
Check.equal Int64.MaxValue (TimeSpan.FromTicks(Int64.MaxValue).Ticks)
