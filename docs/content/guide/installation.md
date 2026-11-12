---
title: Installation
description: Install the Fickl compiler as a .NET tool, compile a project and add Fickl.Core.
---

Fickl is a .NET tool. It needs the .NET SDK 10 or later, and Node.js or a browser to run what it produces.

## Install the compiler

Install it in the project's tool manifest, so everyone on the project uses the same version:

```bash
dotnet new tool-manifest
dotnet tool install fickl
```

The command is `dotnet fickl`. It takes the same arguments as `dotnet fable`.

## Compile a project

Point it at a project file or its folder. `-o` sets the output folder:

```bash
dotnet fickl src/App.fsproj -o dist
```

Each `.fs` file compiles to a `.js` file in `dist`, and the runtime library is copied to `dist/fable_modules`. Run the compiler in watch mode while you work:

```bash
dotnet fickl watch src/App.fsproj -o dist
```

TypeScript is a language choice, and declaration files are a flag on JavaScript output:

```bash
dotnet fickl src/App.fsproj -o dist --lang ts
dotnet fickl src/App.fsproj -o dist --declaration
```

[Compiler flags](../flags/index.md) lists the rest.

## Add Fickl.Core

`Fable.Core` keeps working. Fickl's own attributes, computation expressions and bindings are in the `Fickl.Core` package:

```bash
dotnet add src/App.fsproj package Fickl.Core
```

Open `Fickl.Core` in a file to use them. Its version matches the compiler's, so update both together.

## Coming from Fable

Replace `dotnet fable` with `dotnet fickl` in your scripts. Nothing else has to change: your project file, your packages and your attributes stay the same. Fickl's own behaviour changes are opt-in, through flags and through attributes in the `Fickl` namespaces; [Compatibility](compatibility.md) explains how to write code that compiles with both.

:::note
Fickl defines `FICKL_COMPILER` as well as `FABLE_COMPILER`, so `#if FABLE_COMPILER` blocks keep compiling.
:::
