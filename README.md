# Lisp for .NET

Lisp is a Scheme interpreter written in C# for .NET 10.
It focuses on practical Scheme development with a fast VM path, a rich Scheme standard library, macro support, and direct .NET interoperability.

For full language and runtime reference details, see [docs/reference.md](docs/reference.md).
For implementation details and contributor-oriented notes, see [docs/architecture.md](docs/architecture.md).

## Documentation Map

- New to the project: start here in `README.md`
- Fast onboarding path: `docs/getting-started.md`
- Focused language guide: `docs/language.md`
- Focused standard library guide: `docs/standard-library.md`
- Focused interop guide: `docs/interop.md`
- Focused REPL guide: `docs/repl.md`
- Compact examples with source/test mapping: `docs/examples.md`
- Full language and standard library details: `docs/reference.md`
- VM/compiler/runtime internals: `docs/architecture.md`
- Practical recipes: `docs/reference.md` -> `Cookbook`
- Feature coverage overview: `docs/reference.md` -> `Compatibility Snapshot`

If you are onboarding a new contributor, the fastest path is:

1. Read Quick Start in this file.
2. Run `tests.ss` once.
3. Skim `docs/architecture.md` source layout before editing runtime code.

## Why This Project

- Run Scheme code on .NET with minimal setup.
- Use a substantial Scheme library loaded from `init.ss` and `lib/`.
- Mix Scheme and .NET through reflection-based interop.
- Choose between interactive REPL workflows and script-driven CLI workflows.

## Quick Start

### 1. Build

```sh
dotnet build
```

### 2. Start the REPL

```sh
dotnet run
```

### 3. Run a script

```sh
dotnet run tests.ss
```

At startup, `init.ss` and `lib/` are copied and loaded automatically.

## Common CLI Workflows

Use `--` to pass arguments through `dotnet run` to the interpreter:

```sh
dotnet run -- --eval "(+ 1 2)"
dotnet run -- --load script.ss --eval "(main)"
dotnet run -- --lib-path ./lib --load app/main.ss
dotnet run -- --primitive-profile core --eval "(+ 1 2)"
```

CLI options can be repeated where it makes sense (for example multiple `--load` or `--lib-path`).
If load/eval actions fail, the process exits with a non-zero status.

## CLI Option Reference

The interpreter accepts positional script files and explicit command actions.
Actions are executed in the exact order provided on the command line.

| Option | Meaning |
| --- | --- |
| `--help` | Print help and exit |
| `--version` | Print version and exit |
| `--no-init` | Skip loading `init.ss` |
| `--stats` | Print execution stats after each expression |
| `--no-color` | Disable ANSI color output |
| `--primitive-profile NAME` | Primitive profile: `core` or `full` (default `full`) |
| `--load FILE` | Load and evaluate a Scheme file (repeatable) |
| `--eval EXPR` | Evaluate a Scheme expression (repeatable) |
| `--lib-path DIR` | Add a directory to `load` search paths (repeatable) |

Positional script files are also supported and run in sequence with `--load` and `--eval` actions.

### Exit Behavior

- No script actions: starts interactive REPL.
- Any script action (`FILE`, `--load`, `--eval`): executes actions in order and exits.
- Any failed file load or eval action: returns non-zero exit code.

## REPL Basics

The prompt is `lisp>`.
The REPL auto-submits once parentheses are balanced.

```scheme
lisp> (+ 1 2)
3

lisp> (define (fact n)
...   (if (= n 0) 1 (* n (fact (- n 1)))))
lisp> (fact 5)
120
```

### REPL Commands

```text
:help                 Show command help
:env [pattern]        Show environment bindings
:doc NAME             Show docs for a symbol
:load FILE            Load and run a Scheme file
:time EXPR            Run expression and print elapsed time
:disasm NAME          Disassemble a procedure binding
:history [N]          Show recent submissions
:quit / :exit         Exit the REPL
```

`Ctrl+C` interrupts active evaluation. At an idle prompt, `Ctrl+C` exits the REPL.

## Language Coverage (High-Level)

This interpreter supports a broad practical subset of R5RS/R7RS, including:

- Core forms: `define`, `lambda`, `if`, `cond`, `case`, `begin`, `set!`, `let`, `let*`, `letrec`, `letrec*`, `do`
- Macros: native `macro`, plus `define-syntax` and `syntax-rules`
- Local macro scope: `let-syntax`, `letrec-syntax`
- Continuations and control: `call/cc-full`, `dynamic-wind`, generators
- Multiple values and helpers: `receive`, `case-lambda`, `cut`, `cute`
- Numbers: exact/inexact numeric tower, radix conversion, integer division variants, bit operations
- Data structures: pairs/lists (including dotted pairs), vectors, strings, chars, records, hash tables
- Error handling: `try`, `throw`, R7RS-style `error`, `raise`, `with-exception-handler`, `guard`

## Notable Runtime Capabilities

- Dotted pair/improper list semantics are implemented end-to-end.
- Module/import behavior supports modern import-set combinators (`only`, `except`, `rename`, `prefix`).
- Module tables are interpreter-local for better isolation.
- VM coverage is strong, with tree-walk fallback instrumentation and reporting.
- Stats tooling supports reset, total reporting, and fallback attribution.
- REPL history persists across sessions.

## .NET Interoperability

You can create and manipulate .NET objects directly from Scheme.

### Common Interop Primitives

- Conversion: `->string`, `->int`, `->double`, `->bool`
- Type/reflection: `typeof`, `cast`
- Member access: `.`, `.!`, `..`
- Calls/constructors: `new+`, `call+`, `call-static+`
- Enums and arrays: `enum`, `list->array`, `array->list`

### Example

```scheme
(define sb (new 'System.Text.StringBuilder "hello"))
(. sb 'Length)                  ; 5
(.! sb 'Capacity 100)

(define dt (new+ 'System.DateTime 2024 1 1))
(. dt 'Year)                    ; 2024

(define arr (list->array 'System.String '("a" "b" "c")))
(array->list arr)               ; ("a" "b" "c")
```

## Module System

You can organize code with `define-library` and `import`.
Both library-style and compatibility import forms are supported.

### Example

```scheme
(define-library 'math-utils 'square)
(module-define 'math-utils 'square (lambda (x) (* x x)))

(import 'math-utils)
(square 5)                      ; 25

(import '(rename math-utils (square sqr)))
(sqr 6)                         ; 36
```

Import name collisions are explicit errors, not silent overwrites.

## Embedding From C#

Use `InterpreterHost` for host-driven evaluation.

```csharp
using Lisp;

var host = new InterpreterHost(primitiveProfile: "full", statsEnabled: false);
host.AddLibraryPath("./lib");
host.LoadInitFromBaseDirectory();

var value = host.Eval("(+ 1 2 3)");
Console.WriteLine(value); // 6

host.EvalFile("examples.ss");
```

Each `InterpreterHost` instance owns its interpreter context, improving isolation for multi-host scenarios.

## Project Layout

### Engine (C#)

- `source/Interpreter.cs`: CLI entry point and REPL
- `source/InterpreterHost.cs`: embeddable host API
- `source/InterpreterContext.cs`: interpreter-local context
- `source/Program.cs`: top-level evaluation and startup flow
- `source/Prim.cs`: built-in primitives and .NET interop
- `source/Util.Parser.cs`: reader/parser
- `source/Util.Printer.cs`: printer and formatting
- `source/BytecodeISA.cs`: VM instruction set
- `source/BytecodeCompiler.cs`: compiler to bytecode
- `source/Vm.cs`: VM execution engine

### Scheme Standard Library

- `init.ss`: startup loader
- `lib/`: Scheme modules (numbers, strings, vectors, ports, records, macros, continuations, hash tables, and more)

## Testing and Validation

The repository includes:

- `tests.ss`: core Scheme regression suite
- `examples.ss`: runnable examples
- `scripts/cli-integration.ps1`: CLI integration checks

A quick smoke test is:

```sh
dotnet run tests.ss
```

A quick CLI integration check is:

```powershell
pwsh ./scripts/cli-integration.ps1
```

A quick fallback smoke check is:

```powershell
pwsh ./scripts/fallback-smoke.ps1
```

To run the same smoke workload against the Release build explicitly:

```powershell
pwsh ./scripts/fallback-smoke.ps1 -Configuration Release
```

## Troubleshooting

- If `load` cannot find a relative file, it resolves relative to the currently evaluated source first, then runtime base and working directory.
- Unknown `#` reader dispatch forms are errors (strict reader behavior).
- For script mode failures, inspect exit code and error output; failures return non-zero.

## License and Author

Author: Ilias H. Mavreas

