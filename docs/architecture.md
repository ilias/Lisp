# Architecture and Contributor Notes

This document collects implementation details that are useful for contributors, embedders, and anyone working on the interpreter internals.
For a quick setup and runtime workflow guide, see [getting-started.md](getting-started.md).
For user-facing language and library behavior, see [reference.md](reference.md).

## Index

- [Source Layout](#source-layout)
- [Evaluation Pipeline](#evaluation-pipeline)
- [Bytecode VM](#bytecode-vm)
- [Number Types](#number-types)

## Source Layout

The source is primarily split between the C# engine under `source/` and the Scheme standard library under `lib/`.

### C# Runtime (`source/`)

| File | Responsibility |
| --- | --- |
| `source/Interpreter.cs` | Console entry point and REPL |
| `source/InterpreterHost.cs` | Instance-oriented host orchestration for CLI/REPL execution |
| `source/InterpreterContext.cs` | Thread-local interpreter context for active program and macro state |
| `source/InterpreterRuntime.cs` | Runtime-scoped REPL state, history, and cancellation handling |
| `source/InitCacheStore.cs` | Process-wide init-file cache snapshots |
| `source/Program.cs` | Init loading, top-level evaluation, stats |
| `source/RuntimeIsolationChecks.cs` | C# helper checks for interpreter state isolation |
| `source/Util.cs` | Reflection helpers and shared utilities |
| `source/Util.Parser.cs` | S-expression reader / parser |
| `source/Util.Printer.cs` | Printer, pretty-printer, numeric display |
| `source/Expressions.cs` | AST nodes and tree-walk evaluation |
| `source/Prim.cs` | Built-in procedures and runtime interop |
| `source/Numeric.cs` | Exact numeric tower and arithmetic helpers |
| `source/BytecodeISA.cs` | Bytecode instruction set: `OpCode`, `Instruction`, `Chunk`, `VmClosure` |
| `source/BytecodeCompiler.cs` | Compiler: tree of `Expression` nodes to `Chunk` bytecode |
| `source/Vm.cs` | VM execution engine (`Execute`) and disassembler |
| `source/Macro.cs` | Macro storage and `syntax-rules` expansion |
| `source/CoreTypes.cs` | Core runtime types such as `Symbol`, `Closure`, and `Pair` |
| `source/Environment.cs` | Lexical environments and tail-call trampoline token |
| `source/Continuations.cs` | Full continuation support |
| `source/Exceptions.cs` | Lisp-specific exception and error object types |
| `source/GlobalUsings.cs` | Shared framework imports |

This keeps parsing, macro expansion, numeric semantics, VM execution, and host integration isolated enough to evolve independently.

### Scheme Library (`lib/`)

The standard library is loaded via `init.ss` and includes modular categories:

- `macros.ss`: Core `let`, `cond`, `case`, `and`, `or`, `do` loops.
- `numbers.ss`: Math primitives like `gcd`, `lcm`, rounding, shifting, logic.
- `pairs.ss`, `vectors.ss`, `strings.ss`, `chars.ss`: Data structure primitives.
- `filesystem.ss`, `ports.ss`: I/O, output, and file operations.
- `records.ss`: Dynamic typed structures mapping to `define-record-type`.
- `continuations.ss`: `call/cc-full`, `dynamic-wind`, loops, sequences.
- `hashtables.ss`: Mutable dictionary bindings for Scheme.

## Evaluation Pipeline

All expressions are compiled to bytecode before execution:

```text
Input string
     │
     ▼
 Util.Parse()                     →  object tree (Pair / Symbol / literal)
     │
     ▼
 Macro.Check()                    →  macro-expanded object tree
     │
     ▼
 Expression.Parse()               →  typed AST (Expression subclass tree)
     │
     ▼
 BytecodeCompiler.CompileTop()    →  Chunk (bytecode + constant pool)
     │
     ▼
 Vm.Execute(chunk, env)           →  result object
     │
     ▼
 Util.Dump()                      →  printed representation
```

## Bytecode VM

The interpreter uses a stack-based bytecode VM (`Vm.Execute`) rather than direct AST tree-walking. Every top-level expression, and every `lambda`, is compiled to a `Chunk` of flat instructions before execution.

### Instruction Set

| Opcode | Operand | Description |
| --- | --- | --- |
| `LOAD_CONST` | constant index | Push a constant from the chunk's constant pool |
| `LOAD_VAR` | symbol index | Look up a variable in the current environment and push it |
| `STORE_VAR` | symbol index | Pop stack top, mutate an existing binding (`set!`), push symbol |
| `DEFINE_VAR` | symbol index | Pop stack top, create a new binding in the current environment, push symbol |
| `POP` | — | Discard the top of the operand stack |
| `JUMP` | target offset | Unconditional branch to an instruction index |
| `JUMP_IF_FALSE` | target offset | Pop top; jump if it is `#f`, otherwise fall through |
| `RETURN` | — | Pop return value, restore caller frame, push return value to caller |
| `MAKE_CLOSURE` | prototype index | Capture current env and compiled prototype, then push `VmClosure` |
| `CALL` | argc | Call the procedure `argc` positions below the stack top |
| `TAIL_CALL` | argc | Same as `CALL` but reuse the current call frame |
| `PRIM` | packed `primIdx << 16` with `argc` | Call a built-in C# primitive directly |
| `INTERP` | AST node index | Fall back to tree-walk evaluation for unsupported forms |

The `INTERP` opcode is a targeted escape hatch for forms that still require AST-driven evaluation at runtime. Most ordinary language features, including quasiquote splices, `try`/`try-cont`, local syntax bindings, dynamic `eval`, and standard procedure calls, stay on the VM path.

### Key Data Structures

| C# class | Role |
| --- | --- |
| `Chunk` | Compiled bytecode unit with instruction list, constant pool, symbol table, nested prototypes, primitive references, and AST fallback nodes |
| `VmClosure` | A `Closure` subclass that holds a `Chunk` instead of an AST body |
| `CallFrame` | One entry on the VM call stack: `Chunk`, program counter, `Env`, stack-base index |
| `BytecodeCompiler` | Static compiler from `Expression` trees to `Chunk` bytecode |
| `Vm` | Static execution engine with a flat operand array, flat `CallFrame[]` stack, and proper TCO via frame reuse |

### Tail-Call Optimization (TCO)

`TAIL_CALL` reuses the current `CallFrame` in place: it overwrites the `Chunk`, `Pc`, `Env`, and `StackBase` fields rather than pushing a new frame. This means mutually recursive loops and `named let` loops run in constant stack space regardless of depth.

For calls through a tree-walk `Closure` or a C# `Primitive`, TCO trampolining is still used. The VM drives the trampoline itself by catching `TailCall` thunks.

### Disassembly

For the user-facing disassembler workflow and examples, see the [`disasm` section in reference.md](reference.md#disasm--bytecode-disassembler).

## Number Types

| C# type | Scheme type | Exactness | Notes |
| --- | --- | --- | --- |
| `System.Int32` | integer | exact | `42`, `-7` |
| `System.Numerics.BigInteger` | integer | exact | automatic overflow promotion |
| `Lisp.Rational` | rational | exact | `p/q` struct, normalized |
| `System.Double` | real | inexact | 64-bit IEEE 754; shortest round-trip printing |
| `System.Numerics.Complex` | complex | inexact | `a+bi` literals |

Real numbers print as the shortest decimal string that round-trips back to the same `double` value. Whole-number doubles always include a decimal point as an inexactness marker (`3.0` to `3.`, `100.0` to `100.`). Special values are `+inf.0`, `-inf.0`, and `+nan.0`.

The arithmetic tower promotes as required: `int` to `BigInteger` on overflow, `int`/`BigInteger`/`Rational` to `double` when mixed with an inexact operand, and any numeric type to `Complex` when mixed with a complex operand.

### BigInteger: Automatic Overflow Promotion

When an `Int32` operation would overflow, the result is promoted transparently to `System.Numerics.BigInteger`. Demotion back to `Int32` happens automatically when the value fits.

```scheme
(+ 2147483647 1)            ; => 2147483648
(- (+ 2147483647 1) 1)      ; => 2147483647
(expt 2 100)                ; => 1267650600228229401496703205376
(** 2 100)                  ; alias for expt
(! 30)                      ; exact factorial result
(fib 100)                   ; exact Fibonacci result
```

BigInteger literals are parsed automatically when a numeric literal exceeds the `Int32` range:

```scheme
99999999999999999999
(exact? 99999999999999999999) ; => #t
```

### Rational: Automatic Exact Fractions

`Lisp.Rational` is a C# struct holding two `BigInteger` fields (`Numer`, `Denom`) kept in normalized form: the GCD is divided out, the denominator is positive, and values with denominator `1` are demoted back to `Int32` or `BigInteger`.

Integer division `/` returns a `Rational` whenever the result is not whole:

```scheme
(/ 1 3)              ; => 1/3
(/ 4 2)              ; => 2
(exact? (/ 1 3))     ; => #t
```

`inexact->exact` converts a `double` to the exact `Rational` represented by the IEEE-754 bit pattern:

```scheme
(inexact->exact 0.1)  ; => 3602879701896397/36028797018963968
(inexact->exact 0.5)  ; => 1/2
```

### Complex: Inexact Pairs

`System.Numerics.Complex` provides hardware-speed complex arithmetic. All complex values are inexact. Operations that produce a result with a zero imaginary part still return a `Complex` unless the constructor was given an exact `0`:

```scheme
(make-rectangular 3.0 0.0)  ; => 3.+0.i
(make-rectangular 3   0)    ; => 3
```

Complex numbers are printed in `a+bi` form with trailing dots on whole-number components to signal inexactness:

```text
3+4i        prints as  3.+4.i
1.5-2.5i    prints as  1.5-2.5i
+i          prints as  0.+1.i
```