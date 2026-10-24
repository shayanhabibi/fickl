#load "StructFixture.fs"
printfn "OBSERVATIONS=%A" (StructFixture.observe ())

for n in [ 0; 1; 2; 100; 10000 ] do
    printfn "KERNEL=%d,%d" n (StructFixture.localKernel n 123)
