# Probe

Small checks of valid F# whose results should be the same on .NET and in JavaScript. Each check prints
`name=value`; `run.py` builds and runs the project on .NET, compiles it with Fickl, runs it with node and lists the
checks whose results differ:

```bash
python tests/Probe/run.py
python tests/Probe/run.py --compiler fable   # another compiler, e.g. upstream Fable
```

`notes/edge-cases.md` explains what the differences are. A check that Fickl cannot compile at all is not in the
project; it is listed in that file instead.
