// Change: `Equals` and `CompareTo` on `DateOnly`, `TimeOnly` and `TimeSpan`
// Fickl: `Equals` and `CompareTo` on `DateOnly`, `TimeOnly` and `TimeSpan` are implemented.
// Issues: 318
module Main

open System

Check.equal true (DateOnly(2000, 1, 2).Equals(DateOnly(2000, 1, 2)))
Check.equal -1 (DateOnly(2000, 1, 2).CompareTo(DateOnly(2000, 1, 3)))
Check.equal true (TimeOnly(10, 30, 0).Equals(TimeOnly(10, 30, 0)))
Check.equal 1 (TimeOnly(10, 30, 0).CompareTo(TimeOnly(10, 29, 59)))
Check.equal false (TimeSpan(1, 2, 3).Equals(TimeSpan(1, 2, 4)))
Check.equal -1 (TimeSpan(1, 2, 3).CompareTo(TimeSpan(1, 2, 4)))
