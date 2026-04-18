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
dotnet run -- -l examples.ss -e "(main)"
dotnet run -- --primitive-profile=core --eval "(+ 1 2)"
dotnet run -- -p=full --eval "(+ 1 2)"
dotnet run -- -- --file-that-starts-with-dash.ss
```

Actions run in command-line order and then the process exits.
Values can be passed either as a separate token (`--eval "..."`) or inline (`--eval="..."`).

## CLI Options

| Long Option | Short Alias | Meaning |
| --- | --- | --- |
| `--help` | `-h` | Show help and exit |
| `--version` | `-v` | Show version and exit |
| `--no-init` | `-n` | Skip loading `init.ss` |
| `--stats` | `-s` | Print execution statistics after each expression |
| `--no-color` | `-C` | Disable ANSI color output |
| `--primitive-profile NAME` | `-p NAME` | Primitive profile (`core` or `full`) |
| `--load FILE` | `-l FILE` | Load and evaluate a file (repeatable) |
| `--eval EXPR` | `-e EXPR` | Evaluate an expression (repeatable) |
| `--lib-path DIR` | `-L DIR` | Add a load search directory (repeatable) |

Both long and short options also support inline `=` value forms:

- `--primitive-profile=core`
- `-p=core`
- `--eval="(+ 1 2)"`

CLI parsing behavior:

- `--` ends option parsing. Every token after it is treated as a positional script file.
- Unknown options return a parse error and include a suggestion when there is a close match.
- `--primitive-profile` is validated strictly. Unsupported names fail with an error listing valid profiles.
- Options that require a value fail fast when the value is missing.
- Options that do not accept values fail when passed with `=`.

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
