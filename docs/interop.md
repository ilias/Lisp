# .NET Interop Guide

Use this page for quick interop orientation.
For complete interop behavior and examples, see [reference.md#net-interop](reference.md#net-interop).

## Core Interop Forms

| Form | Purpose |
| --- | --- |
| `new+` | Construct .NET objects with automatic argument conversion |
| `call+` | Call instance methods with conversion |
| `call-static+` | Call static methods with conversion |
| `.` / `.!` / `..` | Property/field read, write, and chain access |
| `enum` | Resolve enum values |
| `list->array` / `array->list` | Convert collections between Scheme and .NET |
| `new` / `call` / `call-static` | Lower-level reflective API |

## Implementation Mapping

| Area | Primary implementation |
| --- | --- |
| Interop primitives | `source/Prim.cs` |
| Reflection helpers | `source/Util.cs` |
| Host orchestration | `source/InterpreterHost.cs` |

## Coverage Entry Points

Use these sections in `tests.ss`:

- `.NET interop`
- `Enhanced .NET interop`

Also see executable examples in `examples.ss`.

## Navigation

- Full interop section: [reference.md#net-interop](reference.md#net-interop)
- Practical interop examples: [examples.md](examples.md)
- Embedding from C#: [reference.md#embedding-from-c](reference.md#embedding-from-c)
