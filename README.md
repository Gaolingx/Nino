# Nino

Definite useful and high performance serialization library for any C# projects, including but not limited to .NET Core
apps or Unity/Godot games.

![build](https://img.shields.io/github/actions/workflow/status/JasonXuDeveloper/Nino/.github/workflows/ci.yml?branch=main)![license](https://img.shields.io/github/license/JasonXuDeveloper/Nino)

[Official Website](https://nino.xgamedev.net/en/)

Plausibly the fastest and most flexible binary serialization library for C# projects.

## Features

[![nino.nuget](https://img.shields.io/nuget/v/Nino?label=Nino)](https://www.nuget.org/packages/Nino)

- Support all **unmanaged types** (`int`/`float`/`DateTime`/`Vector`/`Matrix`, etc)

- Support all custom `interfaces`/`classes`/`structs`/`records` annotated with **[NinoType]** (including `generics`,
  support custom constructor for deserialization)

- Support all **`ICollection<SupportedType>`** types (`List`, `Dictonary`, `ConcurrentDictonary`, `Hashset`, etc)

- Support all **`Span<SupportedType>`** types

- Support all **`Nullable<SupportedType>`** types

- Support all **Embed** serializable types (i.e. `Dictionary<Int, List<SupportedType[]>>`)

- Support **type check** (guarantees data integrity)

- High **performance** with low GC allocation

- Support **polymorphism**

- Support **cross-project** (C# Project) type serialization (i.e. serialize a class with member of types in A.dll from B.dll)

## Quick Start

[Documentation](https://nino.xgamedev.net/en/doc/start)

## Performance

[Microbenchmark](https://nino.xgamedev.net/en/perf/micro)
