// Change: float ranges
// Fickl: Each item of a float range is computed from the start, so rounding errors do not add up and drop the last item.
// Issues: none
module Main

let xs = [ 0.5..0.05..1.5 ]
Check.equal 21 xs.Length
Check.equal 1.5 (List.last xs)
Check.equal [ 0.0; 0.3; 0.6; 0.8999999999999999 ] [ 0.0..0.3..1.0 ]
