---
title: Compiling with dotnet build
description: Run Fickl as part of dotnet build with the Fickl.Sdk package, as a package reference or as a project SDK.
---

The `Fickl.Sdk` package runs the Fickl compiler after `dotnet build`, and deletes what it wrote on `dotnet clean`. The compiler is inside the package, so a project needs no tool manifest and no separate `dotnet fickl` step. Its version is the compiler's version.

There are two ways to use it. Both do the same thing; they differ in how the project opts in.

## As a package reference

Add the package to the entry project, the one you would pass to `dotnet fickl`, and opt in with `FicklCompileOnBuild`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <FicklCompileOnBuild>true</FicklCompileOnBuild>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.fs" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Fickl.Sdk" Version="0.9.0" />
  </ItemGroup>
</Project>
```

Without `FicklCompileOnBuild` the package does nothing. The package is a development dependency, so it does not flow to projects that reference this one.

## As a project SDK

Name the package in the `Sdk` attribute instead of `Microsoft.NET.Sdk`:

```xml
<Project Sdk="Fickl.Sdk/0.9.0">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.fs" />
  </ItemGroup>
</Project>
```

The SDK is `Microsoft.NET.Sdk` with the Fickl targets added. A project that uses it is opted in already (`FicklCompileOnBuild` defaults to `true`), and `TargetFramework` defaults to `net10.0`. To keep the version out of the project file, put it in `global.json`:

```json
{
  "msbuild-sdks": {
    "Fickl.Sdk": "0.9.0"
  }
}
```

and write `<Project Sdk="Fickl.Sdk">`.

## Building

`dotnet build` compiles the project for .NET first, then runs Fickl on it. Fickl's output goes to `fickl-out` next to the project file:

```bash
dotnet build
node fickl-out/Program.js
```

A build with nothing changed skips the Fickl step. It runs again when a source file, the project file, its package references, a referenced project or one of the options below changes.

Errors and warnings appear in the build output like the .NET compiler's: an absolute path, a line and column range, and the F# or Fickl code (`FS0001`, `FABLE0108`) as the code. An error fails the build. Fickl leaves F# warnings to the .NET compile, which reports them already, so each appears once; a warning in code that only Fickl compiles, under `#if FABLE_COMPILER`, is not shown.

## Options

Set these as properties in the project file, or on the command line with `-p:Name=Value`.

| Property | Default | Meaning |
| --- | --- | --- |
| `FicklCompileOnBuild` | `false`, or `true` with the SDK | Run Fickl after `Build` |
| `FicklOutDir` | `fickl-out` | Output folder, relative to the project folder |
| `FicklLanguage` | `javascript` | The `--lang` argument: `typescript`, `python`, and so on |
| `FicklConfiguration` | `$(Configuration)` | The `-c` argument |
| `FicklExtraArgs` | | Other arguments, such as `--declaration --sourceMaps` |
| `FicklTargetFramework` | the first of `TargetFrameworks` | For a multi-targeting project, the framework whose build runs Fickl; `*` runs it for each |
| `FicklOutputImportance` | `normal` | MSBuild importance of the compiler's own output; `high` shows it at the default verbosity |
| `FicklCompilerPath` | the compiler in the package | Another build of `fable.dll` to run |
| `FicklSkipDotNetCompile` | `false` | With the SDK only: skip the .NET compile (see below) |

The output folder is excluded from the project's default items, so the generated files and the package sources Fickl copies into `fable_modules` do not show up in the project.

## Cleaning

`dotnet clean` deletes the files Fickl recorded writing to `FicklOutDir`, the folders that held nothing else, and the `fable_modules` folder in it. A file you put in the output folder yourself, such as a hand-written `index.js` or an `index.html`, is kept.

Each compilation adds the files it writes to `fable_modules/recorded_outputs`, so a file that a later compilation no longer writes is still deleted. Deleting `fable_modules` by hand also deletes that record, and the next `dotnet clean` deletes only what was written after it.

The same clean is available without MSBuild:

```bash
dotnet fickl clean --outDir fickl-out --recorded --yes
```

Without `--recorded`, `dotnet fickl clean` keeps its Fable behaviour and deletes every file with the output extension under the folder.

## Referenced projects

Fickl compiles the sources of referenced projects as part of the entry project, as `dotnet fickl` does. Opt in in the entry project only. A referenced library that opts in too is compiled twice: once on its own and once as part of the entry project.

## Skipping the .NET compile

With the SDK, `FicklSkipDotNetCompile` stops the .NET compiler from running, so `dotnet build` writes JavaScript and no assembly. It is faster, but it only works for a project that nothing references, since there is no assembly to reference. F# warnings then come from Fickl, which reports them itself.

## Limits

- The Fickl step does not run in design-time builds, the builds an IDE runs to load a project. It was checked from the command line only: loading a `Fickl.Sdk` project, and seeing its errors, in Rider, Visual Studio and Ionide is not verified.
- Tested on Windows with JavaScript output. Linux and macOS, and the other languages, are not verified. For languages other than JavaScript and TypeScript, the clean may leave files the compiler writes besides the one for each source, such as Python's `__init__.py`.
- Watch mode is not part of the build. Run `dotnet fickl watch` for that.
- A `TargetFramework` of its own for Fickl projects (such as `net10.0-js`) is not part of the package.
