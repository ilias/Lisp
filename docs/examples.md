# Examples and Coverage Map

This page provides compact, runnable examples and maps each one to implementation files and regression coverage.

## Quick Run Commands

```sh
dotnet run -- --eval "(+ 1 2 3)"
dotnet run -- --load examples.ss
dotnet run tests.ss
```

## Example Matrix

| Example | Snippet | Source mapping | Test coverage |
| --- | --- | --- | --- |
| Arithmetic and numeric tower | `(+ 2147483647 1)` | `source/Numeric.cs`, `lib/numbers.ss` | `tests.ss` sections: `Arithmetic`, `BigInteger / Numeric Tower`, `rational numbers`, `complex numbers` |
| Lists and dotted pairs | `(cdr '(a b . c))` | `source/CoreTypes.cs`, `lib/pairs.ss` | `tests.ss` sections: `Lists`, `dotted pairs`, `list? edge cases` |
| Macros and syntax-rules | `(define-syntax when1 ...)` | `source/Macro.cs`, `source/Expressions.cs`, `lib/macros.ss` | `tests.ss` sections: `Macros`, `define-syntax / syntax-rules`, `let-syntax / letrec-syntax` |
| Module import sets | `(import '(rename math-utils (square sqr)))` | `source/Expressions.cs`, `source/Prim.cs` | `tests.ss` section: `Module system` |
| Continuations | `(call/cc-full (lambda (k) ...))` | `source/Continuations.cs`, `source/Prim.cs`, `lib/continuations.ss` | `tests.ss` sections: `call/cc`, `call/cc-full`, `Nested call/cc`, `dynamic-wind` |
| Hash tables | `(hash-table-update!/default ht 'k f 0)` | `lib/hashtables.ss`, `source/Prim.cs` | `tests.ss` sections: `hash tables`, `hash-table-update!/default`, `hash-table-for-each` |
| File system and ports | `(directory-list ".")` | `lib/filesystem.ss`, `lib/ports.ss`, `source/Prim.cs` | `tests.ss` sections: `file system`, `file system extras`, `read from file`, `call-with-input-file / call-with-output-file` |
| .NET object interop | `(call-static 'System.Math 'Sqrt 16.0)` | `source/Prim.cs`, `source/Util.cs` | `tests.ss` sections: `.NET interop`, `Enhanced .NET interop` |
| REPL introspection | `:doc map`, `:disasm fib` | `source/InterpreterHost.cs`, `source/Prim.cs`, `source/Vm.cs` | `tests.ss` section: `introspection` |

## Source and Test Files

- Runtime implementation: `source/`
- Scheme standard library: `lib/`
- Example script: `examples.ss`
- Regression suite: `tests.ss`

## Navigation

- Language guide: [language.md](language.md)
- Standard library guide: [standard-library.md](standard-library.md)
- Interop guide: [interop.md](interop.md)
- REPL guide: [repl.md](repl.md)
- Full reference: [reference.md](reference.md)
