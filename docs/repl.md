# REPL Guide

Use this page for quick REPL workflows.
For complete REPL behavior and examples, see [reference.md#repl-guide](reference.md#repl-guide).

## Start the REPL

```sh
dotnet run
```

The prompt is `lisp>`. Multi-line entries show `...` until parentheses balance.

## REPL Commands

```text
:help
:env [pattern]
:doc NAME
:load FILE
:time EXPR
:disasm NAME [MODE]
:history [N]
:quit / :exit
```

`Ctrl+C` interrupts active evaluation. At an idle prompt, `Ctrl+C` exits the REPL.

## Disassembler Output Notes

`:disasm NAME [MODE]` now prints a VM-oriented layout with:

- `PC` instruction index
- opcode column in readable long form or compact aliases (`ldc`, `ldv`, `jmp`, `ret`, ...)
- stack effect marker (`+1`, `-1`, `0`, or blank when context-dependent)
- labeled operands (`const[N]`/`c[N]`, `sym[N]`/`s[N]`, `target=LNNNN`)

Disassembly mode can be selected per call:

```scheme
(disasm f)          ; auto
(disasm f 'full)    ; long opcodes + fuller detail text
(disasm f 'compact) ; short opcodes + denser details
(disasm-threshold 80) ; auto mode uses compact at 80+ instructions
(disasm-threshold)    ; read current threshold
```

Set source-label verbosity from Scheme when needed:

```scheme
(disasm-verbose #t)
(disasm-verbose #f)
```

`disasm-verbose` controls source-label verbosity only; it is independent from `disasm` mode (`auto`/`full`/`compact`).

## Implementation Mapping

| Area | Primary implementation |
| --- | --- |
| REPL loop and CLI | `source/Interpreter.cs` |
| REPL command handlers | `source/InterpreterHost.cs` |
| Command implementations (`doc`, `disasm`, etc.) | `source/Prim.cs` |
| Runtime history/cancellation state | `source/InterpreterRuntime.cs` |

## Validation

- Run regression suite: `dotnet run tests.ss`
- Run CLI integration checks: `pwsh ./scripts/cli-integration.ps1`

## Navigation

- Full REPL section: [reference.md#repl-guide](reference.md#repl-guide)
- Getting started workflow: [getting-started.md](getting-started.md)
- Debugging and introspection details: [reference.md#introspection](reference.md#introspection)
