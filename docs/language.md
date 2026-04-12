# Language Guide

Use this page for quick language orientation.
For complete semantics and examples, see [reference.md#language-reference](reference.md#language-reference).

## Scope

This guide covers core language syntax and semantics:

- Literals and runtime value types
- Core forms (`define`, `lambda`, `if`, `cond`, `case`, `let*`, `letrec`, `do`)
- Quasiquote and macro forms
- Error and exception forms

## Core Forms Quick List

| Form | Purpose |
| --- | --- |
| `define` | Create bindings and procedures |
| `lambda` | Create procedures (fixed or variadic) |
| `if` / `cond` / `case` | Conditional evaluation |
| `begin` | Sequence expressions |
| `set!` | Mutate an existing binding |
| `let` / `let*` / `letrec` / `letrec*` | Local bindings and recursion |
| `do` | Iteration loop form |
| `quote` / `quasiquote` | Code/data quoting and templating |
| `define-syntax` / `syntax-rules` | Hygienic macro definitions |
| `macro` | Native macro form |
| `try` / `throw` / `error` / `raise` | Error handling and signaling |

## Implementation Mapping

| Area | Primary implementation |
| --- | --- |
| Parsing and reader behavior | `source/Util.Parser.cs` |
| AST and special forms | `source/Expressions.cs` |
| Macro expansion | `source/Macro.cs` |
| Top-level orchestration | `source/Program.cs` |
| Primitive forms | `source/Prim.cs` |

## Coverage Entry Points

Use these sections in `tests.ss` to validate language behavior quickly:

- `Arithmetic`
- `Let forms`
- `Macros`
- `define-syntax / syntax-rules`
- `quasiquote edge cases`
- `edge case regressions`

## Navigation

- Full language details: [reference.md#language-reference](reference.md#language-reference)
- Standard library functions: [standard-library.md](standard-library.md)
- REPL commands and workflows: [repl.md](repl.md)
- Interop details: [interop.md](interop.md)
