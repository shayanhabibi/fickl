module Program

type R1 = { A: int; B: string }
type R2 = { A: int; B: string; C: int }

// Record spreads are an F# 11 feature
let spreadRecord (r1: R1) : R2 = { ...r1; C = 3 }
