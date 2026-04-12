# Getting Started

This guide is the fastest path to running and validating the interpreter.
For full language details, see [reference.md](reference.md).
For internals and contributor notes, see [architecture.md](architecture.md).

Focused docs:

- [language.md](language.md)
- [standard-library.md](standard-library.md)
- [interop.md](interop.md)
- [repl.md](repl.md)
- [examples.md](examples.md)

## Prerequisites

- .NET SDK 10
- PowerShell 7 or later (optional, for helper scripts)

## Build

```sh
dotnet build
```

## Run Interactive REPL

```sh
dotnet run
```

You should see the `lisp>` prompt.

## Run a Script

```sh
dotnet run tests.ss
```

## Common Scripted Workflows

Use `--` to pass options through `dotnet run`.

```sh
dotnet run -- --eval "(+ 1 2)"
dotnet run -- --load examples.ss --eval "(main)"
dotnet run -- --lib-path ./lib --eval "(load \"numbers.ss\")"
dotnet run -- --primitive-profile core --eval "(+ 1 2)"
```

Actions run in command-line order and then the process exits.

## CLI Options

| Option | Meaning |
| --- | --- |
| `--help` | Show help and exit |
| `--version` | Show version and exit |
| `--no-init` | Skip loading `init.ss` |
| `--stats` | Print execution statistics after each expression |
| `--no-color` | Disable ANSI color output |
| `--primitive-profile NAME` | Primitive profile (`core` or `full`) |
| `--load FILE` | Load and evaluate a file (repeatable) |
| `--eval EXPR` | Evaluate an expression (repeatable) |
| `--lib-path DIR` | Add a load search directory (repeatable) |

If any load/eval action fails, exit code is non-zero.

## REPL Command Shortcuts

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

`Ctrl+C` interrupts active evaluation. At an idle prompt, `Ctrl+C` exits.

## Quick Validation

Run the Scheme regression suite:

```sh
dotnet run tests.ss
```

Run CLI integration checks:

```powershell
pwsh ./scripts/cli-integration.ps1
```

## Troubleshooting

- Relative `load` resolves in this order:
  1. Current source file directory
  2. Runtime base directory
  3. Current working directory
- Unknown reader `#` dispatch forms are treated as errors.
- In script mode, failures return a non-zero exit status.
