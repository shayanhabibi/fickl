// Change: `DateTimeOffset.ToString` with a format
// Fickl: Formats the date in the value's own offset, not in the machine's timezone.
// Issues: none
module Main

open System
open System.Globalization

// At +14 and -12 the offset's date differs from the UTC date, so one of the two differs from the date in any timezone
for d in [ DateTimeOffset(2014, 9, 1, 0, 30, 5, TimeSpan.FromHours 14); DateTimeOffset(2014, 9, 1, 23, 30, 5, TimeSpan.FromHours -12) ] do
    Check.equal "Monday, 01 September 2014" (d.ToString("D", CultureInfo.InvariantCulture))
    Check.equal "01/09/2014" (d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))
