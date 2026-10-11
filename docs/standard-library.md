# Standard Library Guide

Use this page for quick standard-library orientation.
For complete API details, see [reference.md#standard-library-reference](reference.md#standard-library-reference).

## Scope

This guide groups the library surface loaded from `init.ss` and `lib/`.

## Sequence Construction

Use `range-step` to build an inclusive sequence with a custom step. Positive and
negative steps work in their respective directions; values past the end are not
included, and a zero step is an error.

```scheme
(range-step 1 10 3)   ; => (1 4 7 10)
(range-step 10 1 -3)  ; => (10 7 4 1)
```

For consecutive integer lists, the reader also accepts `[start..end]`, for
example `[1..10]` => `(1 2 3 4 5 6 7 8 9 10)`.

## Library Modules

| Module | Purpose |
| --- | --- |
| `lib/numbers.ss` | Arithmetic helpers, numeric predicates, division variants |
| `lib/strings.ss` | String utilities and transformations |
| `lib/pairs.ss` | List operations and pair helpers |
| `lib/vectors.ss` | Vector procedures |
| `lib/chars.ss` | Character predicates and transforms |
| `lib/records.ss` | Record helpers and `define-record-type` support |
| `lib/hashtables.ss` | Mutable hash table API |
| `lib/ports.ss` | Port-based I/O support |
| `lib/filesystem.ss` | File and directory utilities |
| `lib/continuations.ss` | Continuations and control constructs |
| `lib/macros.ss` | Macro helpers and convenience forms |

## Implementation Mapping

| Area | Primary implementation |
| --- | --- |
| Startup loading | `init.ss`, `source/Program.cs`, `source/InterpreterHost.cs` |
| Primitive backing | `source/Prim.cs` |
| Numeric tower internals | `source/Numeric.cs` |

## Coverage Entry Points

Start with these sections in `tests.ss`:

- `SRFI-1 list functions`
- `hash tables`
- `file system`
- `define-record-type`
- `number->string`
- `base`
- `string->number`
- `nan? infinite? finite?`
- `arithmetic-shift`

## Navigation

- Full standard library details: [reference.md#standard-library-reference](reference.md#standard-library-reference)
- Language forms: [language.md](language.md)
- Practical runnable examples: [examples.md](examples.md)
