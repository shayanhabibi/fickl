module Mid

let inline incTwice x = Lib.inc (Lib.inc x)

let describe (p: Lib.Point) = sprintf "%d,%d" p.X p.Y
