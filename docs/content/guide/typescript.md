---
title: TypeScript
description: Declaration files for compiled JavaScript, TypeScript output, and what Fickl checks of each.
---

Fickl gives TypeScript code two ways in: declaration files that describe compiled JavaScript, and TypeScript output. Its priorities, in order, are correct JavaScript, correct declarations, TypeScript output that compiles, and TypeScript output that is fully typed.

## Declaration files

`--declaration` writes a `.d.ts` file next to each JavaScript file, describing what it exports. TypeScript code imports the JavaScript and gets the types from the declaration. This is the recommended way to call F# from TypeScript: the JavaScript is the same as without the flag, and the declarations are tested against a strict `tsconfig`.

```fsharp fickl flags="--declaration" show=js,dts
type Shape =
    | Circle of radius: float
    | Rect of width: float * height: float

let area shape =
    match shape with
    | Circle r -> System.Math.PI * r * r
    | Rect(w, h) -> w * h
```

```bash
dotnet fickl src/App.fsproj -o dist --declaration
```

## TypeScript output

`--lang ts` compiles each file to TypeScript instead, for projects that type-check or bundle their sources as TypeScript. Every example on this site shows its TypeScript output in the TypeScript tab.

```bash
dotnet fickl src/App.fsproj -o src/generated --lang ts
```

## Describing TypeScript types

F# has no words for some TypeScript types, such as `keyof T` or `Partial<T>`. Fickl.Core describes them, so a binding can say what the TypeScript side expects:

- [Types](../features/types.md): `keyof`, `Record`, `valueof`, `Partial`, branded primitives, `unknown`, `[<EmitType>]` and enums.
- [Interfaces](../features/interfaces.md): callable interfaces, global and module augmentations, `this` return types, type guards and labelled tuples.
