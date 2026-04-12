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
:disasm NAME
:history [N]
:quit / :exit
```

`Ctrl+C` interrupts active evaluation. At an idle prompt, `Ctrl+C` exits the REPL.

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
