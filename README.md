# Lisp — A Scheme Interpreter for .NET

**Lisp** is a Scheme interpreter written in C#, targeting .NET 10. It implements a substantial
subset of R5RS/R7RS Scheme with a pattern-based macro system, a full standard library written
in Scheme itself (`init.ss` and the `lib/` directory), and deep two-way .NET interoperability via reflection.

**Author:** Ilias H. Mavreas  
**Runtime:** .NET 10  

## Building & Running

```sh
dotnet build
dotnet run
```

`init.ss` and the contents of the `lib/` directory are automatically copied to the build output directory and loaded at startup.
To run a script file, pass it as an argument:

```sh
dotnet run test2.ss
```

## Recent Updates

**Dotted pair / improper list support:**
- Pairs now support proper dotted-pair notation: `(a . b)`, `(a b . c)`. The reader creates real dotted pairs, `cdr` returns the non-list tail directly, and `list?` correctly returns `#f` for improper lists.
- `set-car!` / `set-cdr!` argument order corrected to `(set-car! pair val)` / `(set-cdr! pair val)`.
- `hash-table->alist` returns `((key . value) ...)` dotted-pair alists; `alist->hash-table` now expects the same format.
- `syntax-rules` and native `macro` patterns support dotted-rest tails: `(a b . rest)` captures all remaining forms as a bare cons tail rather than a flat list.

**Standard library additions:**
- `number->string` / `string->number` (R7RS §6.2.7) — radix-aware numeric ↔ string conversion; `number->string` accepts an optional radix (2, 8, 10, 16) and handles negative numbers and BigIntegers correctly; `string->number` parses signed strings in any radix and returns `#f` for invalid input.
- `nan?` / `infinite?` / `finite?` (R7RS §6.2.6) — IEEE-754 numeric predicates.
- `arithmetic-shift` (R7RS §6.2.6) — left shift for non-negative count, sign-extending right shift for negative count; correctly handles negative integers.
- `floor-quotient` / `floor-remainder` / `truncate-quotient` / `truncate-remainder` (R7RS §6.2.6) — R7RS integer division operators; floor variants round toward −∞, truncate variants round toward zero; both work exactly over BigIntegers.
- `define-record-type` — R7RS §6.6 / SRFI-9 record type definition; declares a named record type with a typed constructor, predicate, per-field accessors, and optional field mutators; the constructor may take a subset of the declared fields (unset fields default to `#f`); interoperates with the existing `record` infrastructure (`record?`, `record-name`, etc.).
- `call/cc-full` — reentrant/coroutine continuations captured via threads + semaphores; companion `make-generator` builds a lazy sequence generator from a coroutine body.
- `dynamic-wind` — executes before/thunk/after guards correctly around continuations, including in the presence of escape continuations.
- `letrec*` — sequential binding form (R7RS); bindings are evaluated left to right and each binding is in scope for subsequent ones.
- `case-lambda` — multi-clause lambda dispatching on argument count.
- `cut` / `cute` (SRFI-26) — partial application with `<>` slots; `cute` evaluates non-slot argument expressions once at call-to-cut time.
- `receive` (SRFI-8) — destructure multiple return values without `call-with-values`.
- `fluid-let` — dynamic binding that saves and restores top-level variable values around a body.
- `while` / `until` — imperative loops; `while` iterates while the test is true, `until` iterates until it is true; both return the last body value.
- Variadic `gcd` / `lcm` — accept zero or more arguments; `(gcd)` → 0, `(lcm)` → 1.
- `member` / `assoc` with optional comparator — a third argument overrides the default `equal?` test.
- `hash-table-update!/default` — update a hash table entry in place, inserting a default if the key is absent.
- Extended file system API — `rename-file`, `copy-file`, `create-directory`, `directory-list`, `directory-list-subdirs`, `set-current-directory!`.
- `set-cons` — alias for `adjoin`; prepends an element to a list only if it is not already a member.

**Standards and usability fixes (Phase 2):**
- Reader `#` dispatch is now strict: unknown dispatch forms (for example `#u`) raise a Scheme reader error instead of silently evaluating as `#f`.
- `eqv?` semantics now align with Scheme-style behavior: numeric/char/boolean values compare by value, while other compound/heap objects compare by identity.
- `load` now resolves relative paths against the currently evaluated source file first, then falls back to the runtime base directory and current working directory.
- Added regression coverage for strict reader dispatch, `eqv?` edge behavior, and source-relative `load` path handling.

**Module/import modernization (Phase 3):**
- Module tables are now interpreter-local (stored in `InterpreterContext`) rather than process-global.
- `import` now supports R7RS-style import-set combinators: `(only ...)`, `(except ...)`, `(rename ...)`, `(prefix ...)`.
- Existing import forms remain supported for compatibility: `(import 'module)` and `(import 'module 'sym1 'sym2 ...)`.
- Added runtime isolation checks and Scheme tests for module isolation and import-set behavior.

## Enhanced .NET Interop

The interpreter now provides enhanced interoperability with .NET, making it easier to work with .NET objects, types, and APIs from Scheme code.

### Type Conversion Primitives
- `(->string obj)` — Convert any object to its string representation
- `(->int obj)` — Convert to integer (handles strings, numbers, etc.)
- `(->double obj)` — Convert to double precision float
- `(->bool obj)` — Convert to boolean (truthy/falsy conversion)

### Reflection and Casting
- `(typeof obj)` — Get the .NET type name of an object
- `(cast "TypeName" obj)` — Cast an object to a specific .NET type

### Property and Field Access
- `(. obj 'PropertyName)` — Get a property or field value
- `(.! obj 'PropertyName value)` — Set a property or field value
- `(.. obj 'Prop1 'Prop2 ...)` — Chain property access

### Enhanced Object Creation and Method Calls
- `(new+ 'TypeName arg1 arg2 ...)` — Create .NET objects with automatic type conversion
- `(call+ obj 'MethodName arg1 arg2 ...)` — Call instance methods with automatic type conversion
- `(call-static+ 'TypeName 'MethodName arg1 arg2 ...)` — Call static methods with automatic type conversion

### Enums and Collections
- `(enum 'TypeName 'ValueName)` — Get enum values
- `(list->array 'ElementType list)` — Convert Scheme list to .NET array
- `(array->list array)` — Convert .NET array to Scheme list

### Examples
```scheme
;; Type conversion
(->string 42)                    ; "42"
(->int "123")                    ; 123
(->bool 0)                       ; #f

;; Property access
(define sb (new 'System.Text.StringBuilder "hello"))
(. sb 'Length)                   ; 5
(.! sb 'Capacity 100)            ; set capacity
(.. "hello world" 'Length)       ; 11 (chained access)

;; Enhanced object creation
(define dt (new+ 'System.DateTime 2024 1 1))  ; automatic int conversion
(. dt 'Year)                      ; 2024

;; Array conversion
(define arr (list->array 'System.String '("a" "b" "c")))
(define lst (array->list arr))    ; ("a" "b" "c")

;; Enum access
(define sunday (enum 'System.DayOfWeek 'Sunday))  ; 0
```

## Basic Module System

A practical module system allows organizing code into separate namespaces and importing functionality between them.

### Module Operations
- `(define-library 'name 'export1 'export2 ...)` — Create/register a module environment, optionally with explicit exports.
- `(import 'module-name)` — Import all visible exports from a module.
- `(import 'module-name 'sym1 'sym2 ...)` — Legacy selective import form.
- `(import '(only module-name sym1 ...))` — Import only selected bindings.
- `(import '(except module-name sym1 ...))` — Import all visible bindings except the listed ones.
- `(import '(rename module-name (old1 new1) ...))` — Import with renamed identifiers.
- `(import '(prefix module-name p:))` — Import with a symbol prefix.
- `(module-define 'module-name 'symbol value)` — Define a symbol in a module
- `(module-ref 'module-name 'symbol)` — Get a symbol's value from a module

Module registries are isolated per interpreter instance.

### Examples
```scheme
;; Create a module with explicit exports
(define-library 'math-utils 'square 'pi)

;; Define functions in the module
(module-define 'math-utils 'square (lambda (x) (* x x)))
(module-define 'math-utils 'pi 3.14159)
(module-define 'math-utils 'private-helper (lambda (x) x))

;; Import the module (imports 'square and 'pi, but not 'private-helper)
(import 'math-utils)

;; Use imported symbols
(square 5)                       ; 25
pi                               ; 3.14159

;; Selective import
(import 'math-utils 'square)     ; Imports only 'square

;; Import-set examples
(import '(only math-utils square))
(import '(prefix math-utils m:))
(import '(rename math-utils (square sqr)))

;; Direct access
((module-ref 'math-utils 'square) 3)  ; 9
```

**Reader / numeric literals:**
- Radix prefix literals: `#b` (binary), `#o` (octal), `#d` (decimal), `#x` (hexadecimal) for integer literals.
- Named character literals: `#\nul`, `#\null`, `#\return`, `#\escape`, `#\altmode`, `#\delete`, `#\rubout`, `#\backspace`, `#\alarm` in addition to `#\space` and `#\newline`.
- Special float literals: `+inf.0`, `-inf.0`, `+nan.0`.

**VM and infrastructure:**
- The bytecode VM now covers the full regression suite end to end: isolated validation builds report `interp-sites=0`, `interp-runs=0`, and `tree-walk=0` across `tests.ss`.
- `let-syntax` / `letrec-syntax` bodies now expand and compile through the VM path instead of forcing `INTERP` fallback.
- Ordinary `lambda` evaluation now produces `VmClosure` instances, and each lambda AST caches its compiled chunk to avoid recompiling the same procedure shape repeatedly.
- Runtime error handling has been tightened: `if` test expressions no longer swallow unexpected failures, `try` now catches Scheme-level errors instead of arbitrary engine exceptions, and host interop failures are wrapped as Scheme-visible errors for `try` / `with-exception-handler`.
- Macro state and the active interpreter instance now live under a thread-local `InterpreterContext`, so macro expansion state is no longer backed by a process-wide static dictionary.
- The init file cache is now explicitly process-wide via `InitCacheStore`, and the regression suite includes C#-backed isolation checks that prove two interpreter instances on the same thread keep macro tables and runtime state separated.
- Stats reporting has been expanded from a single timing line into a grouped report with status, runtime path, work/control counters, throughput, fallback summaries, memory usage, and optional fallback-kind attribution.
- The REPL now exposes `(stats-reset)` to clear accumulated totals and `(stats-total)` to print the accumulated report across multiple top-level evaluations.
- Exact integers and rationals can now be displayed in p-adic form with `(p-adic p)` or `(p-adic p digits)`; `(p-adic 10)` restores the default decimal printer.

**Code quality and REPL improvements:**
- `Ctrl+C` in the REPL now interrupts the current evaluation and returns to the prompt instead of terminating the process. A `volatile` flag on `InterpreterContext` is set by the signal handler and checked on every interpreter iteration (both the tree-walker and the VM). A new `UserInterruptException` type is used so the REPL can distinguish an interrupt from a real error.
- The REPL paren-depth counter (`...` continuation prompt) now correctly handles R7RS block comments (`#| … |#`, including nesting) and character literals containing parentheses such as `#\(` and `#\)`, which previously caused the prompt to wait for a closing paren that would never arrive.
- String escape sequences are now R7RS-compliant: `\t` (tab), `\r` (carriage return), `\a` (alarm/bell), `\b` (backspace), `\0` (null), `\\` (backslash) and the hex escape `\xHHHH;` are all handled. Previously only `\n` and `\"` were recognized; any other escape silently output its literal character.
- A shared `Util.ApplyDocComment(value, name)` helper eliminates the previously triplicated `DebugName`/`DocComment` assignment logic that appeared identically in the tree-walker (`Define.Eval`), the VM (`DEFINE_VAR` case), and `Program.Eval`.
- The three private list-counting helpers (`CountArgs` in `Expressions.cs`, `CountTopLevelArgs` in `Program.cs`, `CountListItems` in `Macro.cs`) have been removed; all call sites now use the existing `Pair.Count` property directly.
- `Bytecode.cs` (1 262 lines, three unrelated concerns) has been split into three focused files: `BytecodeISA.cs` (`OpCode`, `Instruction`, `Chunk`, `VmClosure`), `BytecodeCompiler.cs` (the compiler), and `Vm.cs` (the VM execution engine and disassembler).

---

## Source Layout

The source is primarily split between the C# engine under `source/` and the Scheme standard library under `lib/`.

**C# Runtime (`source/`)**

| File | Responsibility |
| ------ | ---------------- |
| `source/Interpreter.cs` | Console entry point and REPL |
| `source/InterpreterContext.cs` | Thread-local interpreter context for active program and macro state |
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
| `source/BytecodeCompiler.cs` | Compiler: tree of `Expression` nodes → `Chunk` bytecode |
| `source/Vm.cs` | VM execution engine (`Execute`) and disassembler |
| `source/Macro.cs` | Macro storage and `syntax-rules` expansion |
| `source/CoreTypes.cs` | Core runtime types such as `Symbol`, `Closure`, and `Pair` |
| `source/Environment.cs` | Lexical environments and tail-call trampoline token |
| `source/Continuations.cs` | Full continuation support |
| `source/Exceptions.cs` | Lisp-specific exception and error object types |
| `source/GlobalUsings.cs` | Shared framework imports |

This keeps parsing, macro expansion, numeric semantics, VM execution, and host integration isolated enough to evolve independently.

**Scheme Library (`lib/`)**

The standard library is loaded via `init.ss` and includes modular categories:
- `macros.ss`: Core `let`, `cond`, `case`, `and`, `or`, `do` loops.
- `numbers.ss`: Math primitives like `gcd`, `lcm`, rounding, shifting, logic.
- `pairs.ss` & `vectors.ss` & `strings.ss` & `chars.ss`: Data structure primitives.
- `filesystem.ss` & `ports.ss`: I/O, output, and file operations.
- `records.ss`: Dynamic typed structures mapping to `define-record-type`.
- `continuations.ss`: `call/cc-full`, `dynamic-wind`, loops, sequences.
- `hashtables.ss`: Mutable dictionary bindings for Scheme.

---

## REPL Usage

The REPL prints a `lisp>` prompt. Type an expression and press **Enter**. The expression
is submitted automatically once all open parentheses are closed — no blank line required.
Multi-line expressions show a `...` continuation prompt:

```scheme
lisp> (+ 1 2)
3
lisp> (+ 1 2) (+ 3 4)
3
7
lisp> (define (fact n)
...      (if (= n 0) 1 (* n (fact (- n 1)))))
lisp> (fact 10)
3628800
```

Entering multiple expressions on a single line prints each result in turn.

Type `(exit)` to quit.

---

## Language Reference

### Literals & Data Types

| Literal | C# type | Example |
| --------- | --------- | --------- |
| Integer | `System.Int32` | `42`, `-7` |
| Real | `System.Double` | `3.14`, `-0.5` |
| Rational | `Lisp.Rational` | `1/3`, `-3/4`, `4/2` → `2` |
| Complex | `System.Numerics.Complex` | `3+4i`, `+i`, `-2.5i`, `1.5-0.5i` |
| Boolean | `System.Boolean` | `#t`, `#f` |
| Character | `System.Char` | `#\a`, `#\space`, `#\newline` |
| String | `System.String` | `"hello\nworld"` |
| Symbol | `Lisp.Symbol` | `foo`, `+`, `my-var` |
| Pair / List | `Lisp.Pair` | `(1 2 3)`, `(a . b)` |
| Vector | `System.Collections.ArrayList` | `#(1 2 3)` |
| Empty list | `Lisp.Pair.Empty` | `'()`, `nil` |
| Quote | — | `'expr` ≡ `(quote expr)` |

Named characters: `#\space`, `#\newline`, `#\tab`.

---

### Core Special Forms

#### `define`

```scheme
(define name value)              ; bind a name to a value
(define (name args...) body...)  ; define a procedure
(define (name . rest) body...)   ; variadic procedure
```

#### `lambda`

```scheme
(lambda (x y) body...)           ; fixed arity
(lambda (x . rest) body...)      ; variadic
(lambda x body...)               ; all args as list
(lambda () body...)              ; thunk
```

#### `if` / `cond` / `case` / `when` / `unless`

```scheme
(if test then else)
(if test then)                   ; else evaluates to #f

(cond
  (test1 expr...)
  (test2 => proc)                ; calls (proc test2) if test2 is truthy
  (else  expr...))

(case key
  ((val1 val2) expr...)
  (else        expr...))

(when  pred body...)             ; evaluates body when pred is truthy
(unless pred body...)            ; evaluates body when pred is falsy
```

#### `let` / `let*` / `letrec`

```scheme
(let    ((x 1) (y 2)) body...)      ; parallel bindings
(let*   ((x 1) (y x)) body...)      ; sequential bindings
(letrec ((f (lambda ...))) body...)  ; recursive bindings
(let name ((x init) ...) body...)   ; named let (loop)
```

#### `begin`

```scheme
(begin expr1 expr2 ... exprN)    ; sequential, returns last
```

#### `set!`

```scheme
(set! name value)                ; mutate an existing binding
```

#### `quote`

```scheme
'expr
(quote expr)                     ; returns expr unevaluated
```

#### `do` loop

```scheme
(do ((var init step) ...)
    (test result...)
  body...)
```

**Example:**

```scheme
(do ((i 0 (+ i 1))
     (s 0 (+ s i)))
    ((= i 5) s))   ; => 10
```

#### `apply` / `map` / `for-each`

```scheme
(apply f arg... lst)             ; call f with args from lst
(map f lst ...)                  ; return list of (f elem...)
(for-each f lst ...)             ; side-effect each element
```

**Examples:**

```scheme
(map (lambda (x) (* x x)) '(1 2 3 4 5))        ; => (1 4 9 16 25)
(map + '(1 2 3) '(10 20 30))                    ; => (11 22 33)
(map car '((a b) (c d) (e f)))                  ; => (a c e)
(apply + '(1 2 3 4 5))                          ; => 15
(apply map (list + '(1 2 3) '(10 20 30)))       ; => (11 22 33)
(for-each display '(1 2 3))                     ; prints 123, returns '()
```

#### `eval`

```scheme
(eval '(+ 1 2))                  ; => 3
(eval "(+ 1 2)")                 ; also accepts a string
```

#### `rec`

```scheme
(rec name (lambda (x) ... (name ...) ...))
```

Shorthand for `(letrec ((name ...)) name)`. Useful for anonymous recursive functions:

```scheme
((rec fact (lambda (n) (if (= n 0) 1 (* n (fact (- n 1)))))) 5)  ; => 120
```

---

### Lambda Shorthand

The backslash syntax provides a compact notation for curried lambdas:

```scheme
\x.body          ≡  (lambda (x) body)
\x,y.body        ≡  (lambda (x y) body)
```

**Examples:**

```scheme
(\x.(* x x) 5)      ; => 25
(define add \x,y.(+ x y))
(add 3 4)            ; => 7
```

With `(carry #t)` enabled, curried application is supported:

```scheme
(carry #t)
(\x.\y.\z.(+ x y z) 1 2 3)  ; => 6
(carry #f)
```

---

### Quasiquotation

```scheme
(define x 42)
`(a ,x c)              ; => (a 42 c)      — unquote
`(a ,@(list 1 2) c)    ; => (a 1 2 c)     — unquote-splicing
`(a `(b ,x) c)         ; nested quasiquote
```

---

### Macros

Lisp has a pattern-based macro system with ellipsis support.

**Syntax:**

```scheme
(macro name (literal...)
  (pattern  template)
  ...)
```

**Pattern features:**

| Syntax | Meaning |
| -------- | --------- |
| `var` | bind any single form |
| `var...` | bind zero or more remaining forms |
| `(a b) ...` | destructure repeated pairs into `a...` and `b...` |
| `(a b . rest)` | bind `a`, `b` normally; bind `rest` to the tail cons cell (improper list tail) |
| `?name` | generate a unique symbol (hygienic gensym) |
| `literal` | match an exact symbol (listed in the literals list) |

**Example:**

```scheme
(macro swap! ()
  ((_ a b)
   (let ((?tmp a))
     (set! a b)
     (set! b ?tmp))))

(let ((x 1) (y 2))
  (swap! x y)
  (list x y))   ; => (2 1)
```

---

#### `define-syntax` / `syntax-rules`

The interpreter also understands the standard R7RS `define-syntax` / `syntax-rules` form. It is transparently translated to the native `macro` representation at load time, so both styles interoperate fully.

**Syntax:**

```scheme
(define-syntax name
  (syntax-rules (literal ...)
    (pattern template)
    ...))
```

- **`literal ...`** — symbols that must match literally (not treated as pattern variables).

- Each **`pattern`** begins with the macro name (or `_`) followed by sub-patterns; the name/`_` head is ignored during matching.

- Each **`template`** is the expansion.

**Pattern language:**

| Element | Example | Meaning |
| --------- | --------- | --------- |
| literal symbol | `else` | matches the exact symbol (must appear in literal list) |
| `_` | `_` | wildcard — matches any single form, binding is discarded |
| pattern variable | `x` | matches any single form, bound in template |
| `var ...` | `x ...` | matches zero or more remaining forms (ellipsis) |
| `(a b) ...` | `(var init) ...` | destructures repeated pairs, binding `var...` and `init...` |
| `(a b . rest)` | `(_ x . rest)` | binds `a`, `b` normally; binds `rest` to the improper-list tail (non-list cdr) |

**Examples:**

```scheme
; Boolean and — short-circuits
(define-syntax my-and
  (syntax-rules ()
    ((_)           #t)
    ((_ e)         e)
    ((_ e1 e2 ...) (if e1 (my-and e2 ...) #f))))

(my-and)                      ; => #t
(my-and #f (error "boom"))    ; => #f  (right side not evaluated)
(my-and 1 2 3)                ; => 3

; let in terms of lambda — uses pair-ellipsis destructuring
(define-syntax my-let
  (syntax-rules ()
    ((_ ((var init) ...) body ...)
     ((lambda (var ...) body ...) init ...))))

(my-let ((a 1) (b 2)) (+ a b))  ; => 3

; swap! without a gensym
(define-syntax swap!
  (syntax-rules ()
    ((_ a b)
     (let ((tmp a))
       (set! a b)
       (set! b tmp)))))

; cond with a literal else keyword
(define-syntax my-cond
  (syntax-rules (else)
    ((_ (else e ...))           (begin e ...))
    ((_ (test e ...) rest ...)  (if test (begin e ...) (my-cond rest ...)))))

(my-cond ((= 1 2) 'no) (else 'yes))  ; => yes
```

**Interoperability:**

`define-syntax` macros are stored in the same table as native `macro` definitions.  The standard introspection procedures work on both:

```scheme
(macro? 'my-and)           ; => #t
(macro-body 'my-and)       ; returns the internal pattern list
(macros->list)             ; includes my-and, my-let, swap!, …
```

**Native `macro` vs `define-syntax` cheat-sheet:**

| Feature | Native `macro` | `define-syntax` |
| --------- | --------------- | ----------------- |
| Wildcard | `?name` (gensym) | `_` (discard) |
| Ellipsis token | `var...` (no space) | `var ...` (space before `...`) |
| Literal matching | listed normally | listed in literals list |
| Head position | `_` always | macro name or `_` |
| Hygiene | manual `?gensym` | `_`-wildcards are discarded |

---

#### `let-syntax` / `letrec-syntax`

Locally-scoped syntax bindings — macros defined for the duration of the body only.
Neither form is visible outside its `body` expressions, preventing namespace pollution
and allowing safe reuse of macro names in different scopes.

```scheme
(let-syntax ((name (literal...)
               (pattern template)
               ...) ...)
  body ...)

(letrec-syntax ((name (literal...)
                  (pattern template)
                  ...) ...)
  body ...)
```

The pattern/template language is identical to the native `macro` system (see above).
`letrec-syntax` permits macro bodies to reference other macros defined in the same
`letrec-syntax` block; in this implementation both forms share the same evaluation
model.

**Examples:**

```scheme
; Local increment — not visible outside
(let-syntax ((inc () ((_ x) (+ x 1))))
  (inc 10))                        ; => 11

; inc is unbound after the block
(macro? 'inc)                      ; => #f (assuming inc was not already defined)

; Multiple local macros
(let-syntax ((double () ((_ x) (* x 2)))
             (square () ((_ x) (* x x))))
  (list (double 5) (square 4)))    ; => (10 16)

; Hygienic swap (gensym prevents variable capture)
(let ((a 1) (b 2))
  (let-syntax ((swap! () ((_ x y) (let ((?t x)) (set! x y) (set! y ?t)))))
    (swap! a b)
    (list a b)))                   ; => (2 1)

; letrec-syntax — self-recursive macro (my-or)
(letrec-syntax ((my-or ()
  ((_)            #f)
  ((_ e)          e)
  ((_ e1 e2...)   (let ((?v e1)) (if ?v ?v (my-or e2...))))))
  (my-or #f #f 42))                ; => 42
```

`let-syntax` macros shadow any same-named global macros within their body.  On exit,
the global macro table is fully restored (including the original definition if one
existed).

---

### Error Handling

#### Low-level forms

```scheme
(try expr)                      ; evaluate, return '() on error
(try expr catch-expr)           ; return catch-expr on error
(try expr (begin handler...))

(throw "message")               ; raise a C# exception (low-level)
```

`try` catches Scheme-visible errors, including values raised by `error` / `raise`
and host interop failures that are wrapped into Scheme errors. It is no longer
intended to catch arbitrary internal runtime faults.

**Example:**

```scheme
(try (/ 1 0) "division error")           ; => "division error"
(try (throw "oops") 'caught)             ; => caught
(try (call-static 'System.Convert 'ToInt32 "abc") 'caught) ; => caught
```

#### R7RS Exception System (§6.11)

```scheme
; Raising exceptions
(error msg)                     ; raise an error-object with no irritants
(error msg irritant ...)        ; raise an error-object with irritants
(raise obj)                     ; raise any Scheme value as an exception
(raise-continuable obj)         ; same as raise (continuable not supported)

; Catching exceptions
(with-exception-handler handler thunk)
                                ; call thunk; on exception call (handler value)
                                ; wrapped host interop failures arrive as error-objects

; guard macro (R7RS §4.2.7)
(guard (var
        (test expr) ...)
  body ...)
                                ; evaluate body; if an exception is raised,
                                ; bind it to var and test each clause in order;
                                ; if no clause matches, re-raise the exception

; Error object predicates
(error-object?          obj)    ; #t if obj was raised by (error ...)
(error-object-message   obj)    ; extract the message string
(error-object-irritants obj)    ; extract the irritants as a list
```

**Examples:**

```scheme
; Catch an error by message
(with-exception-handler
  (lambda (e)
    (if (error-object? e)
        (error-object-message e)
        (raise e)))
  (lambda () (error "bad value" 42)))   ; => "bad value"

; Inspect irritants
(with-exception-handler
  (lambda (e) (error-object-irritants e))
  (lambda () (error "oops" 1 2 3)))     ; => (1 2 3)

; guard: catch and test
(guard (e
        ((error-object? e)
         (string-append "caught: " (error-object-message e)))
        (else 'unknown))
  (error "file not found"))             ; => "caught: file not found"

; guard with else
(guard (e (else 'fallback))
  (error "anything"))                   ; => fallback

; raise any value
(with-exception-handler
  (lambda (e) e)
  (lambda () (raise 'my-error)))        ; => my-error

; try still catches all exceptions (including raise)
(try (error "bad value" 42) "caught!") ; => "caught!"
(try (raise 'anything)     'caught)    ; => caught
```

#### Uncaught Error Format

When an exception is not caught inside Scheme, the interpreter now prints a structured
diagnostic instead of only the raw exception message. The report may include:

```text
error in '<file>': <message>
  at <source>:<line>:<column>[-<endLine>:<endColumn>]
  Scheme stack:
    <procedure> at <source>:<line>:<column>  <expression>
    <caller> at <source>:<line>:<column>     <expression>
```

- **first line** — the uncaught error message, prefixed with the active file name, `init.ss`, or `error:` in the REPL.
- **`at ...`** — the best available source span for the failing form.
- **`Scheme stack:`** — a Scheme-level call trace built from VM call-site metadata rather than the raw .NET stack.
- **procedure names** — named procedures defined with `define` appear by name; anonymous procedures fall back to `lambda (...)` or a generic placeholder.

Source names depend on where the code came from:

- files passed on the command line use the file path
- startup library failures use the `init.ss` path
- interactive submissions use `<repl>`

**Example — runtime error in a file:**

```text
error in '.\demo.ss': division by zero
  at .\demo.ss:1:1-2:11
  Scheme stack:
    g at .\demo.ss:1:1-2:11  (/ 1 x)
    <top-level> at .\demo.ss:7:1-7:6  (f 1)
```

**Example — reader / parse error:**

```text
error in '.\bad.ss': division by zero in rational literal
  at .\bad.ss:1:4
```

Notes:

- Proper tail calls are preserved, so tail-recursive loops do not accumulate unbounded stack frames in the Scheme trace.
- Macro-expanded code inherits the originating call-site location when the generated form does not carry a more precise span of its own.
- Caught exceptions handled by `try`, `guard`, or `with-exception-handler` still behave as Scheme values; the new source/span formatting is for uncaught console diagnostics.

---

## Standard Library

The library is loaded from `init.ss` at startup. The banner lists each module:

```text
[generics, Utilities, carry, combinators, trace, macros, procedures,
 pair, char, string, boolean, symbol, numbers, vectors, input, output,
 records, multipleValues, delayEvaluation, continuation, Unification, sets] done.
```

---

### Pairs & Lists

```scheme
; Construction
(cons a b)               ; construct a pair / prepend to list
(list x ...)             ; build a proper list
nil                      ; the empty list '()

; Access
(car pair)               ; first element
(cdr pair)               ; rest
; Derived accessors — all c[ad]{1,4}r combinations up to 4 levels:
(caar x) (cadr x) (cdar x) (cddr x)
(caaar x) (caadr x) (cadar x) (caddr x) ...
(cadddr x) (cddddr x)
; Positional aliases
(second x)   (third x)   (fourth x)   (fifth x)

; Predicates
(null? x)                ; true if x is the empty list
(pair? x)                ; true if x is a Pair
(list? x)                ; R5RS-correct: #t for proper lists and '(); #f for cycles (tortoise-and-hare)
(atom? x)                ; true if x is not a pair

; Length & access
(length lst)             ; number of elements (uses .Count)
(list-ref lst n)         ; nth element, 0-based
(list-tail lst n)        ; tail after n elements (alias: drop)

; Mutation
(set-car! pair val)
(set-cdr! pair val)

; Building & transforming
(append lst ...)         ; concatenate lists
(reverse lst)
(list-copy lst)          ; shallow copy via (map identity lst)

; Search
(member x lst)           ; find using equal?, returns tail or #f
(memq   x lst)           ; find using eq?
(memv   x lst)           ; find using eqv?
(assoc  key alist)       ; find by equal?
(assq   key alist)       ; find by eq?
(assv   key alist)       ; find by eqv?

; Set operations
(adjoin   e lst)         ; add e if not present (using equal?)
(set-cons e lst)         ; alias for adjoin
(union       l1 l2)
(intersection l1 l2)
(difference  l1 l2)

; Sequence building
(iota n)                 ; (0 1 2 ... n-1)
(iota n start)           ; (start start+1 ... start+n-1)
(iota n start step)      ; with custom step
(range start end)        ; (start start+1 ... end) inclusive

; Zip / flatten
(zip lst ...)            ; ((l1[0] l2[0]...) (l1[1] l2[1]...) ...)
(flatten lst)            ; flatten nested lists to a flat list

; Positional / tail
(last      lst)          ; last element
(last-pair lst)          ; last cons cell

; Construction helpers
(make-list n)            ; list of n #f values
(make-list n fill)       ; list of n copies of fill

; Searching
(find pred lst)          ; first element satisfying pred, or #f
(remove pred lst)        ; complement of filter — elements not satisfying pred

; Duplicate removal
(delete-duplicates lst)  ; remove repeated elements (keeps first occurrence)
(list-set lst n val)     ; return new list with position n replaced by val

; Concatenation
(concatenate lsts)       ; (apply append lsts)

; SRFI-1 aliases
(append-map f lst)       ; alias for flat-map
(for-all pred lst)       ; alias for every
(exists  pred lst)       ; alias for any
```

**Examples:**

```scheme
(make-list 3)                          ; => (#f #f #f)
(make-list 3 0)                        ; => (0 0 0)
(find even? '(1 3 4 5))               ; => 4
(find even? '(1 3 5))                 ; => #f
(remove even? '(1 2 3 4 5))           ; => (1 3 5)
(filter-map (lambda (x) (if (> x 3) (* x 2) #f)) '(1 2 3 4 5))  ; => (8 10)
(delete-duplicates '(1 2 1 3 2 4))    ; => (1 2 3 4)
(list-set '(a b c d) 2 'X)            ; => (a b X d)
(concatenate '((1 2) (3 4) (5)))      ; => (1 2 3 4 5)
(range 1 5)                            ; => (1 2 3 4 5)
(range 3 7)                            ; => (3 4 5 6 7)
(range 5 5)                            ; => (5)
```

---

### Dotted Pairs

A **dotted pair** (improper pair) is a `cons` cell where the `cdr` is not a list.
The reader and printer both use `(a . b)` notation.

```scheme
; Construction
(cons 'a 'b)         ; => (a . b)   — dotted pair (cdr is a symbol, not a list)
(cons 1 2)           ; => (1 . 2)
(cons 'a '(b c))     ; => (a b c)   — proper list (cdr is a proper list)

; Literals
'(a . b)             ; => (a . b)
'(1 . 2)             ; => (1 . 2)
'(a b . c)           ; => (a b . c) — improper list: cdr of last cons is c

; Predicates
(pair? '(a . b))     ; => #t
(list? '(a . b))     ; => #f   — not a proper list
(list? '(a b . c))   ; => #f

; Access
(car '(a . b))       ; => a
(cdr '(a . b))       ; => b   — returns b directly, not a one-element list

; Association lists using dotted pairs (preferred format)
(define ages '((alice . 30) (bob . 25) (carol . 35)))
(assq 'bob ages)     ; => (bob . 25)
(cdr (assq 'bob ages)) ; => 25
```

**Examples:**

```scheme
; Building mixed lists
(cons 1 (cons 2 (cons 3 '())))     ; => (1 2 3)    proper list
(cons 1 (cons 2 3))                ; => (1 2 . 3)  improper list

; Pattern matching in macros — dotted rest (. rest) captures tail as-is
; See the Macros section for dotted-pattern examples
```

---

### Higher-Order List Functions

```scheme
(filter   pred lst)             ; keep elements where (pred x) is truthy
(remove   pred lst)             ; drop elements where (pred x) is truthy (complement of filter)
(filter-map f lst)              ; map then drop #f results
(foldl    f init lst)           ; left fold:  (f (f init l[0]) l[1]) ...
(foldr    f init lst)           ; right fold: (f l[0] (f l[1] ... init))
(reduce   f init lst)           ; right fold (alias for foldr)

(any      pred lst)             ; #t if at least one element satisfies pred
(every    pred lst)             ; #t if all elements satisfy pred
(for-all  pred lst)             ; alias for every (SRFI-1)
(exists   pred lst)             ; alias for any   (SRFI-1)
(count    pred lst)             ; number of elements satisfying pred
(flat-map f    lst)             ; (apply append (map f lst))
(append-map f  lst)             ; alias for flat-map (SRFI-1)

(take lst n)                    ; first n elements
(drop lst n)                    ; all elements after the first n (alias: list-tail)

; Sorting (insertion sort — stable for equal keys)
(sort    lst)                   ; sort numbers/strings in ascending order
(sort-by f lst)                 ; sort by key function f — compares (f elem) values

; Require multiple values (call-with-values must be available)
(partition pred lst)            ; → (values matching non-matching)
(span      pred lst)            ; → (values prefix rest) while pred holds
(break     pred lst)            ; → (values prefix rest) until pred holds
```

**Examples:**

```scheme
(filter odd? '(1 2 3 4 5))               ; => (1 3 5)
(remove even? '(1 2 3 4 5))              ; => (1 3 5)
(filter-map (lambda (x) (if (even? x) (* x x) #f)) '(1 2 3 4))  ; => (4 16)
(foldl + 0 '(1 2 3 4 5))                 ; => 15
(foldr cons '() '(1 2 3))                ; => (1 2 3)
(any  negative? '(1 -2 3))               ; => #t
(every positive? '(1 2 3))               ; => #t
(count even? '(1 2 3 4 5 6))             ; => 3
(flat-map (lambda (x) (list x (- x)))
          '(1 2 3))                      ; => (1 -1 2 -2 3 -3)
(take '(1 2 3 4 5) 3)                    ; => (1 2 3)
(drop '(1 2 3 4 5) 2)                    ; => (3 4 5)
(zip  '(1 2 3) '(a b c))                 ; => ((1 a) (2 b) (3 c))
(flatten '(1 (2 (3 4) 5)))               ; => (1 2 3 4 5)
(delete-duplicates '(1 2 1 3 2))         ; => (1 2 3)
(concatenate '((1 2) (3 4)))             ; => (1 2 3 4)
(sort '(3 1 4 1 5 9 2 6))               ; => (1 1 2 3 4 5 6 9)
(sort-by string-length '("bb" "a" "ccc")) ; => ("a" "bb" "ccc")
(sort-by car '((3 c) (1 a) (2 b)))      ; => ((1 a) (2 b) (3 c))
(call-with-values
  (lambda () (partition even? '(1 2 3 4 5)))
  list)                                  ; => ((2 4) (1 3 5))
(call-with-values
  (lambda () (span positive? '(1 2 -1 3)))
  list)                                  ; => ((1 2) (-1 3))
```

---

### Functional Combinators

```scheme
(identity x)                    ; returns x unchanged
(compose  f g ...)              ; right-to-left function composition
(negate   pred)                 ; returns a procedure that negates pred
```

**Examples:**

```scheme
(identity 42)                               ; => 42
((compose car reverse) '(1 2 3))            ; => 3
((compose (lambda (x) (* x x))
          (lambda (x) (+ x 1))) 4)          ; => 25   ((4+1)^2)
((negate even?) 3)                          ; => #t
((negate null?) '(1))                       ; => #t
```

---

### Numbers

The interpreter implements a full exact/inexact numeric tower:

| Type | Exactness | C# backing |
| ------ | ----------- | ------------ |
| Integer | exact | `System.Int32` / `System.Numerics.BigInteger` |
| Rational | exact | `Lisp.Rational` (p/q in lowest terms) |
| Real | inexact | `System.Double` (64-bit IEEE 754) |
| Complex | inexact | `System.Numerics.Complex` |

```scheme
; Arithmetic  (promotes through the tower as needed)
(+ a b ...)    (- a b ...)    (* a b ...)    (/ a b ...)
(neg a)                                       ; unary negation

; Comparison (variadic, chaining; < > <= >= not defined for complex)
(< a b ...)   (<= a b ...)   (= a b ...)
(<> a b ...)  (>= a b ...)   (> a b ...)

; Predicates
(zero?     x)    (positive? x)   (negative? x)
(even?     x)    (odd?      x)
(exact?    x)    (inexact?  x)
(number?   x)    (integer?  x)
(real?     x)    ; #t for int / rational / real; #f for non-real complex
(complex?  x)    ; #t for all numbers
(rational? x)    ; #t for int / rational / finite double; #f for complex
(exact-integer? x)  ; (and (integer? x) (exact? x))
(int?      x)    ; alias for integer?
(finite?   x)    ; #t if not infinite and not NaN
(infinite? x)    ; #t if +Inf or -Inf
(nan?      x)    ; #t if NaN (not-a-number)

; Rounding / conversion
(floor x)      (ceiling x)    (round x)      (truncate x)
(exact->inexact n)             ; to System.Double
(inexact->exact n)             ; to System.Int32, rounds half-even
(exact n)                      ; alias for inexact->exact
(inexact n)                    ; alias for exact->inexact
(tointeger x)                  ; System.Convert.ToInt32
(todouble x)                   ; System.Convert.ToDouble
(square x)                     ; (* x x)

; Math functions
(abs x)      (sqrt x)     (expt x y)    (pow x y)
(exp x)      (log x)      (log10 x)
(sin x)      (cos x)      (tan x)
(asin x)     (acos x)     (atan x)      (atan2 y x)   ; 2-arg arc-tangent
(min x ...)  (max x ...)
(gcd a b)    (lcm a b)    (reciprocal x)
(isPrime n)                 ; #t for exact prime integers, else #f

; Rational accessors
(numerator   r)    ; exact integer numerator of r
(denominator r)    ; exact positive integer denominator of r

; Integer arithmetic
(quotient  x y)    ; truncated division
(remainder x y)    ; truncated remainder
(modulo    x y)    ; R5RS floor remainder: result has same sign as divisor
(truncate-quotient  x y)  ; alias for quotient
(truncate-remainder x y)  ; alias for remainder
(floor-quotient  x y)     ; ⌊x/y⌋ — proper floor division
(floor-remainder x y)     ; x - y*⌊x/y⌋ — always same sign as y

; Bitwise
(bit-and a b)   (bit-or a b)   (bit-xor a b)   (xor a b)
(bit-not x)              ; bitwise complement: (- -1 x)
(arithmetic-shift x n)   ; left shift if n≥0, right shift if n<0

; Radix conversion
(number->string n)          ; decimal
(number->string n radix)    ; any radix (e.g. 2, 8, 16)
(p-adic p)                  ; show exact results in p-adic form for prime p
(p-adic p digits)           ; same, but only show the least-significant displayed digits
(p-adic 10)                 ; restore default decimal/rational display
(string->number s)          ; auto-detect int or float
(string->number s radix)    ; parse integer in given radix
(string->integer s)         ; always parses as Int32
(string->real s)            ; always parses as Double

; Constants
PI     ; System.Math.PI  (~3.14159…)
E      ; System.Math.E   (~2.71828…)
PHI    ; golden ratio (/ (+ 1 (sqrt 5)) 2)  (~1.61803…)

; Exponentiation alias
(**  x y)    ; alias for (expt x y)
```

**Examples:**

```scheme
(exact-integer? 5)                   ; => #t
(exact-integer? 5.0)                 ; => #f
(finite? 1.0)                        ; => #t
(infinite? (/ 1.0 0.0))              ; => #t
(nan? (sqrt -1.0))                   ; => #t  (implementation-dependent)
(atan2 1.0 1.0)                      ; => 0.7853981... (pi/4)
(floor-quotient -7 2)                ; => -4   (floor, not truncate)
(floor-remainder -7 2)               ; => 1
(truncate-quotient -7 2)             ; => -3   (alias for quotient)
(truncate-remainder -7 2)            ; => -1   (alias for remainder)
(bit-not 5)                          ; => -6
(arithmetic-shift 1 4)               ; => 16
(arithmetic-shift 16 -2)             ; => 4
(isPrime 97)                         ; => #t
(isPrime 91)                         ; => #f
(begin (p-adic 7) (number->string 100))    ; => "202_7"
(begin (p-adic 7 4) (number->string (+ (expt 7 5) 3))) ; => "...0003_7"
(begin (p-adic 7 32) (number->string 1/2))            ; => "...33333333333333333333333333333334_7"
(p-adic 10)                          ; restore normal decimal/rational output
```

---

#### Rational Numbers (Exact Fractions)

Rational literals are written `p/q`. The parser normalises them immediately: the
GCD is divided out, the sign is kept in the numerator, and if the denominator reduces
to 1 the result is a plain integer.

```scheme
1/3                  ; => 1/3        (Rational)
4/2                  ; => 2          (normalised to Int32)
-6/4                 ; => -3/2       (sign in numerator, GCD reduced)
0/99                 ; => 0          (zero)
```

**Arithmetic — results stay exact:**

```scheme
(+ 1/3 1/6)          ; => 1/2
(+ 1/3 2/3)          ; => 1          (collapses to integer)
(* 3 1/3)            ; => 1
(/ 1 3)              ; => 1/3        (integer division → rational)
(/ 2/3 1/2)          ; => 4/3
(- 1/3)              ; => -1/3
```

Mixing an exact rational with an inexact `double` promotes to `double`:

```scheme
(+ 1/3 1.0)          ; => 1.3333...  (inexact Double)
(exact? (+ 1/3 1.0)) ; => #f
```

**Rounding** — returned values are exact integers:

```scheme
(floor    7/2)       ; => 3
(ceiling  7/2)       ; => 4
(truncate -7/2)      ; => -3
(round    1/2)       ; => 0   (round-to-even / banker's rounding)
(round    3/2)       ; => 2   (2 is even)
```

**Conversions:**

```scheme
(exact->inexact 1/3)   ; => 0.3333...  (Double)
(inexact->exact 0.5)   ; => 1/2        (exact IEEE-754 rational)
(inexact->exact 0.75)  ; => 3/4
(inexact->exact 3.0)   ; => 3          (integer)
```

**`numerator` / `denominator`:**

```scheme
(numerator   3/4)    ; => 3
(denominator 3/4)    ; => 4
(numerator   5)      ; => 5   (integers are already in lowest terms)
(denominator 5)      ; => 1
```

**`expt` with negative exponent returns an exact rational:**

```scheme
(expt 2 -1)          ; => 1/2
(expt 3 -2)          ; => 1/9
(expt 1/2  3)        ; => 1/8
(expt 2/3 -2)        ; => 9/4    ; (2/3)⁻² = (3/2)²
```

**Representation:**

```scheme
(number->string 1/3)  ; => "1/3"
(number->string -3/4) ; => "-3/4"
```

**Optional p-adic display mode:**

This is a printer setting, not a separate numeric type. Arithmetic still uses the existing
exact integer/rational tower; only the way exact results are rendered changes.

`p` must be prime. The optional second argument controls how many least-significant p-adic digits
are shown. Large finite exact integers are truncated on the left when needed, and non-terminating
expansions show only the requested suffix. That precision stays in effect until changed again.

```scheme
(p-adic 7)
(number->string 100)   ; => "202_7"
(number->string -1)    ; => "...6666666666666666_7"

(p-adic 7 4)
(number->string (+ (expt 7 5) 3)) ; => "...0003_7"

(p-adic 7 32)
(number->string 1/2)   ; => "...33333333333333333333333333333334_7"

(p-adic 10)            ; disable p-adic formatting
(number->string 1/2)   ; => "1/2"
```

---

#### Complex Numbers

Complex numbers are always *inexact*. Literals follow the syntax `<real>±<real>i`.

```scheme
3+4i          ; real 3.0, imaginary 4.0
-2-0.5i       ; real -2.0, imaginary -0.5
+i            ; 0.0 + 1.0i  (unit imaginary)
-i            ; 0.0 - 1.0i
+3i           ; 0.0 + 3.0i  (pure imaginary)
1.5-2.5i
```

**Construction / decomposition:**

```scheme
(make-rectangular x y) ; build from real and imaginary parts
(make-polar r theta)   ; build from magnitude and angle
(real-part z)          ; real component
(imag-part z)          ; imaginary component
(magnitude z)          ; |z|  = sqrt(x²+y²)
(angle z)              ; arg(z) = atan2(y, x), in (-π, π]
```

If the imaginary argument to `make-rectangular` is an exact `0`, the
result is the (possibly exact) real part rather than a complex number:

```scheme
(make-rectangular 5   0)   ; => 5     (exact Int32)
(make-rectangular 1/2 0)   ; => 1/2   (exact Rational)
(make-rectangular 3.0 0.0) ; => 3.+0.i  (inexact 0.0 keeps complex)
```

**Arithmetic:**

```scheme
(+ 1+2i 3+4i)          ; => 4.+6.i
(* 1+2i 1+2i)          ; => -3.+4.i
(/ 1+2i 1+2i)          ; => 1.+0.i
(magnitude 3+4i)       ; => 5.0
(expt +i 2)            ; => -1.+0.i  (≈ -1)
(expt +i 4)            ; => 1.+0.i
```

**Type predicates:**

```scheme
(complex?  3+4i)       ; => #t  (all numbers are complex)
(real?     3+4i)       ; => #f  (non-zero imaginary part)
(real?     3.+0.i)     ; => #t  (zero imaginary part)
(rational? 3+4i)       ; => #f
(exact?    3+4i)       ; => #f  (always inexact)
(inexact?  3+4i)       ; => #t
```

**Comparison:** only `=` is defined for complex numbers; `<`, `>`, `<=`, `>=` raise an error.

```scheme
(= 3+4i 3+4i)          ; => #t
(= 3+4i 3+5i)          ; => #f
```

---

### Characters

Characters are `System.Char` written as `#\x`.

```scheme
(char? x)

; Comparison (variadic)
(char=?  a b ...)    (char<?  a b ...)    (char>?  a b ...)
(char<=? a b ...)    (char>=? a b ...)    (char<>? a b ...)
; Case-insensitive variants
(char-ci=?  a b)  (char-ci<?  a b)  (char-ci>?  a b)
(char-ci<=? a b)  (char-ci>=? a b)  (char-ci<>? a b)

; Classification
(char-alphabetic?   c)   (char-numeric? c)
(char-upper-case?   c)  (char-lower-case? c)
(char-whitespace?   c)
(char-punctuation?  c)   ; Char.IsPunctuation — e.g. #\, #\. #\!
(char-symbol?       c)   ; Char.IsSymbol — e.g. #\+ #\< #\>

; Conversion
(char-upcase   c)      (char-downcase c)
(char->integer c)      (integer->char n)
(char->digit   c)      ; decimal digit value, or #f if not a digit
(char->digit   c radix); digit value in given radix, or #f
(digit-value   c)      ; R7RS §6.6 — decimal digit value 0-9, or #f
```

**Examples:**

```scheme
(char-alphabetic? #\a)          ; => #t
(char-numeric?    #\5)          ; => #t
(char-whitespace? #\space)      ; => #t
(char-punctuation? #\,)         ; => True
(char-punctuation? #\a)         ; => False
(char-symbol?     #\+)          ; => True
(char-symbol?     #\<)          ; => True
(char-symbol?     #\a)          ; => False
(char->digit #\7)               ; => 7
(char->digit #\a)               ; => #f  (not a decimal digit)
(char->digit #\a 16)            ; => 10  (hex: a = 10)
(char->digit #\A 16)            ; => 10  (uppercase hex also supported)
(char->digit #\0 2)             ; => 0   (binary digit)
(char->digit #\2 2)             ; => #f  (not a binary digit)
```

---

### Strings

Strings are immutable `System.String` values. "Mutating" operations return new strings.

```scheme
(string? x)
(string char ...)              ; create a string from characters
(string-length s)
(string-ref s n)               ; character at index (0-based)
(string-set! s n c)            ; returns a new string with position n replaced
(string-copy s)
(make-string n)                ; n space characters
(make-string n char)           ; n copies of char

; Construction
(string-append s ...)
(substring s start end)        ; end is exclusive
(list->string lst)             ; list of chars → string
(string->list s)               ; string → list of chars
(string-fill! s char)          ; replace every char; returns new string

; Case
(string-upcase   s)
(string-downcase s)

; Trimming
(string-trim       s)          ; trim both ends
(string-trim-left  s)
(string-trim-right s)

; Search & test
(string-contains s sub)        ; #t / #f
(string-index s pred)          ; first index where (pred char) is true, or #f

; Splitting / joining
(string-split s delimiter)     ; split s on delimiter string, returns list
(string-join  lst)             ; join with "" separator
(string-join  lst sep)         ; join with sep string

; Repetition / traversal
(string-repeat   s n)          ; concatenate s with itself n times
(string-for-each f s)          ; call (f char) for each character (side-effect)
(string-map      f s)          ; apply f to each char, return new string

; Conversion to/from vectors
(string->vector  s)            ; string → vector of chars
(vector->string  v)            ; vector of chars → string

; Comparison (variadic)
(string=?  a b ...)   (string<?  a b ...)   (string>?  a b ...)
(string<=? a b ...)   (string>=? a b ...)   (string<>? a b ...)
; Case-insensitive variants
(string-ci=?  a b)  (string-ci<?  a b)  (string-ci>?  a b)
(string-ci<=? a b)  (string-ci>=? a b)  (string-ci<>? a b)

; Conversion
(string->symbol  s)
(string->integer s)
(string->real    s)            ; returns System.Double
(string->number  s)            ; #f on parse failure
(string->number  s radix)      ; integer in given radix, #f on failure
(number->string  n)            ; shortest round-trip decimal; +nan.0 / +inf.0 / -inf.0
(number->string  n radix)      ; integer in given radix
```

**Examples:**

```scheme
(string-split "a,b,c" ",")              ; => ("a" "b" "c")
(string-join '("a" "b" "c") "-")        ; => "a-b-c"
(string-repeat "ab" 3)                  ; => "ababab"
(string-index "abc3" char-numeric?)     ; => 3
(string-index "hello" char-numeric?)    ; => #f
(string-contains "hello world" "world") ; => #t
(string-map char-upcase "hello")        ; => "HELLO"
(string->vector "abc")                  ; => #(a b c)
(vector->string (vector #\x #\y #\z))   ; => "xyz"
(string #\h #\i)                        ; => "hi"
(string #\a #\b #\c)                    ; => "abc"
```

---

### Booleans & Equality

```scheme
(boolean? x)
(boolean=? a b ...)             ; all arguments are the same boolean

(not x)                         ; #f if x is truthy, #t if x is #f

(and expr ...)                  ; short-circuit, returns last truthy value or #f
(or  expr ...)                  ; short-circuit, returns first truthy value or #f
(xor a b)                       ; integer bitwise XOR (1 or 0)

(eq?    x y)                    ; reference equality
(eqv?   x y)                    ; scalar value equality + object identity semantics
(equal? x y)                    ; deep structural equality
(=      x y ...)                ; alias for equal? (with numeric coercion for numbers)
```

> **Note:** `eq?` uses `Object.ReferenceEquals`. Two `'()` literals evaluated at different
> times may not be `eq?`; prefer `null?` or `equal?` to compare empty lists.

---

### Symbols

Symbols are interned `Lisp.Symbol` values. The interpreter is **case-sensitive**: `'a` and
`'A` are distinct symbols.

```scheme
(symbol?         x)
(symbol->string  s)             ; returns the symbol name as a string
(string->symbol  s)             ; intern a string as a symbol
(symbol-generate)               ; create a unique gensym symbol
(symbols->list)                 ; list all currently interned symbols
(symbols->vector)               ; same, as a vector
(symbol=? s1 s2 ...)            ; #t if all arguments name the same symbol
```

---

### Vectors

Vectors are `System.Collections.ArrayList` (mutable, 0-indexed).

```scheme
(vector? x)
(vector x ...)                  ; create from elements
(make-vector n)                 ; n zeros
(make-vector n fill)            ; n copies of fill
(vector-length v)
(vector-ref    v i)
(vector-set!   v i obj)         ; mutates in-place
(vector-copy   v)
(vector-fill!  v x)             ; mutates in-place, returns v
(vector->list  v)
(list->vector  lst)
(vector-map    f v)             ; returns a new vector
(vector-for-each f v)          ; call (f elem) for each element (side-effect)

; Set operations on vectors
(vector-union        v1 v2)
(vector-intersection v1 v2)
(vector-difference   v1 v2)

; Concatenation
(vector-append v ...)          ; concatenate vectors → new vector
```

**Examples:**

```scheme
(vector-map    (lambda (x) (* x x)) #(1 2 3))   ; => #(1 4 9)
(vector-append #(1 2) #(3 4) #(5))              ; => #(1 2 3 4 5)
(let ((sum 0))
  (vector-for-each (lambda (x) (set! sum (+ sum x))) #(1 2 3 4))
  sum)                                           ; => 10
```

---

### Input / Output

```scheme
; Port predicates
(input-port?  x)                ; #t for StreamReader or StringReader
(output-port? x)                ; #t for any TextWriter (StreamWriter, StringWriter, Console.Out, etc.)
(port?        x)                ; #t for any port (input or output)

; Dynamic port variables
*INPUT*                         ; current input port ('() means stdin)
*OUTPUT*                        ; current output port ('() means stdout)

; Input
(current-input-port)
(open-input-file "path")        ; returns StreamReader, sets *INPUT*
(close-input-port port)
(call-with-input-file "path" proc)
(read . port)                   ; read one Scheme expression
(read-line . port)              ; read one line as a string
(read-toend . port)             ; read all remaining text
(read-char . port)              ; read one character
(peek-char . port)              ; peek without consuming
(eof-object? x)                 ; true if char is EOF (65535)
(eof-object)                    ; returns the EOF sentinel value
(char-ready? . port)            ; #t if a character is ready (always #t in this impl.)
(read-string k . port)          ; read up to k characters; returns string or eof-object
(load "file.ss")                ; load and evaluate a Scheme source file (path resolves relative to current source file first)
(load "file.ss" #t)             ; load and print each input line in gray
(with-input-from-file "path" thunk) ; temporarily redirect *INPUT*

; String ports (in-memory I/O)
(open-input-string s)           ; create a StringReader from s
(open-output-string)            ; create a StringWriter (accumulates text)
(get-output-string port)        ; extract accumulated string from StringWriter
(string-port? x)                ; #t for StringReader or StringWriter

; Output
(current-output-port)
(current-error-port)            ; standard error (Console.Error)
(open-output-file "path")       ; returns StreamWriter, sets *OUTPUT*
(close-output-port port)
(call-with-output-file "path" proc)
(display   obj . port)
(write     obj . port)          ; display with Scheme read syntax (quotes strings etc.)
(write-char char . port)
(write-string s . port)         ; write a string directly (no quotes)
(writeline obj . port)
(newline . port)
(flush-output-port . port)      ; flush port's internal buffer
(console     fmt ...)           ; Console.Write
(consoleLine fmt ...)           ; Console.WriteLine
(with-output-to-file "path" thunk) ; temporarily redirect *OUTPUT*
```

**Examples:**

```scheme
; String port (in-memory I/O)
(define p (open-output-string))
(display "hello" p)
(display " world" p)
(get-output-string p)                  ; => "hello world"

(string-port? p)                       ; => True
(string-port? (open-input-string "x")) ; => True

; Read from a string
(define in (open-input-string "(+ 1 2)"))
(read in)                              ; => (+ 1 2)
```

---

### Records

Records are vectors with a type tag and named fields.

```scheme
; Define a record type (fields with default values)
(record define <type> (field default) ...)
(record define <type> field ...)       ; all fields default to 0

; Create an instance
(record name new <type>)

; Read a single field
(record name field)

; Read multiple fields — returns a list of those field values
(record name field1 field2 ...)

; Set one field
(record name ! field value)

; Set multiple fields
(record name ! (field1 val1) (field2 val2) ...)

; Type test
(record name ? <type>)

; Read a lambda-field and invoke it
(record name call field arg ...)

; Get all field values as a vector
(record name)
```

**Introspection:**

```scheme
(record?          x)            ; true if x is a record instance
(record-name      r)            ; type symbol
(record-instance  r)            ; unique instance id (gensym)
(record-fields    r)            ; vector of field name symbols
(record-values    r)            ; vector of current field values
(record-field-get  r 'field)    ; get a field by symbol name
(record-field-set! r 'field v)  ; set a field by symbol name
```

**Example:**

```scheme
(record define <point> (x 0) (y 0))
(record p new <point>)
(record p ! x 3)
(record p ! y 4)
(record p ? <point>)             ; => #t
(record p x)                     ; => 3
(record p x y)                   ; => (3 4)   — multi-field read returns a list
(record-name p)                  ; => <point>
(record-fields p)                ; => #(x y)
```

---

### define-record-type (R7RS / SRFI-9)

`define-record-type` is the standard record declaration form.  It declares a
named type together with a constructor, a predicate, per-field accessors, and
optional field mutators in a single expression.

**Syntax:**

```scheme
(define-record-type <type-name>
  (<constructor-name> <constructor-field> ...)
  <predicate-name>
  (<field-name> <accessor-name>)                   ; read-only field
  (<field-name> <accessor-name> <modifier-name>)   ; mutable field
  ...)
```

- **`<type-name>`** — the type tag symbol (conventionally angle-bracketed, e.g. `<point>`).
- **Constructor clause** — lists the fields that are given as arguments; any
  declared field not listed here is initialised to `#f`.
- **Predicate** — returns `#t` iff its argument is an instance of this type.
- **Field spec without modifier** — read-only accessor only.
- **Field spec with modifier** — accessor plus a two-argument `(mutator obj val)` procedure.

**Example:**

```scheme
(define-record-type <point>
  (make-point x y)
  point?
  (x point-x)            ; read-only
  (y point-y set-point-y!)) ; mutable

(define p (make-point 3 4))
(point? p)               ; => #t
(point-x p)              ; => 3
(point-y p)              ; => 4
(set-point-y! p 99)
(point-y p)              ; => 99

; Instances of different types are distinct:
(define-record-type <color>
  (make-color r g b)
  color?
  (r color-r) (g color-g) (b color-b))

(color? p)               ; => #f
(point? (make-color 1 2 3)) ; => #f
```

**Constructor with fewer fields than declared:**

```scheme
(define-record-type <node>
  (make-node value)      ; only 'value' is supplied at construction time
  node?
  (value node-value set-node-value!)
  (next  node-next  set-node-next!)) ; defaults to #f

(define n (make-node 42))
(node-next n)            ; => #f
(set-node-next! n 'end)
(node-next n)            ; => 'end
```

**Interop with the `record` primitives:**

Instances created by `define-record-type` are ordinary record vectors and work
with all introspection procedures:

```scheme
(record? p)              ; => #t
(record-name p)          ; => <point>
(record-fields p)        ; => #(x y)
```

---

### Multiple Values

```scheme
(values v ...)
(call-with-values producer consumer)

; Partitioning / splitting lists into two groups
(partition pred lst)            ; → (values matching non-matching)
(span      pred lst)            ; → (values prefix rest) while pred holds
(break     pred lst)            ; → (values prefix rest) until pred holds

; Integer square root
(exact-integer-sqrt k)          ; → (values s r) such that s² + r = k, r ≥ 0

; Binding forms
(let-values  (((var ...) expr) ...) body...)   ; bind multiple-value expressions
(let*-values (((var ...) expr) ...) body...)   ; sequential version
```

**Examples:**

```scheme
(call-with-values (lambda () (values 1 2)) +)       ; => 3
(call-with-values (lambda () 4) (lambda (x) x))     ; => 4
(values 42)                                         ; => 42

(call-with-values (lambda () (partition even? '(1 2 3 4 5))) list)
; => ((2 4) (1 3 5))

(call-with-values (lambda () (span positive? '(1 2 -1 3))) list)
; => ((1 2) (-1 3))

(call-with-values (lambda () (exact-integer-sqrt 14)) list)
; => (3 5)     ; because 3²=9, 14-9=5

(let-values (((a b) (values 10 20))
             ((c)   (values 30)))
  (+ a b c))
; => 60
```

---

### Delay & Force

```scheme
(delay expr)             ; create a promise (not yet evaluated)
(force promise)          ; force evaluation; result is memoized
```

**Example:**

```scheme
(define p (delay (begin (display "computed!") 42)))
(force p)                ; prints "computed!" => 42
(force p)                ; => 42 (cached, body not re-run)
```

---

### Continuations

Two flavours of continuation are provided, with different trade-offs.

---

#### `call/cc` / `let/cc` — escape continuations

`call/cc` and `let/cc` implement **escape continuations** — fast, allocation-free
local exits implemented as tagged `throw`/`catch` pairs in C#.  Each invocation
allocates a unique tag, so independent nested `call/cc` forms never interfere.

```scheme
(call/cc (lambda (k) body...))
(call-with-current-continuation (lambda (k) body...))  ; alias

(let/cc k body...)      ; binds k to the current escape continuation
```

Invoking `k` unwinds the computation and returns that value from the enclosing
`call/cc` expression.

**Examples:**

```scheme
(call/cc (lambda (k) (* 5 4)))              ; => 20
(call/cc (lambda (k) (* 5 (k 4))))          ; => 4
(* 2 (call/cc (lambda (k) (* 5 (k 4)))))   ; => 8
(let/cc k (* 5 (k 4)))                      ; => 4

; Early exit from a loop
(call/cc (lambda (exit)
  (for-each (lambda (x)
    (if (negative? x) (exit x)))
    '(1 2 -3 4))
  'done))   ; => -3

; Nested independent continuations — inner escape does not affect outer
(+ 100 (call/cc (lambda (outer-k)
          (* 2 (call/cc (lambda (inner-k) (inner-k 5)))))))
; => 110   inner returns 5, outer computes (* 2 5) = 10, result 100+10

; Outer escape invoked from within an inner lambda still works
(+ 100 (call/cc (lambda (outer-k)
          (* 2 (call/cc (lambda (inner-k) (outer-k 100)))))))
; => 200   outer-k(100) short-circuits both inner and outer
```

---

#### `call/cc-full` — coroutine / reentrant continuations

`call/cc-full` is a C# primitive that runs the body in a dedicated thread and uses
semaphores to alternate control between the body and the caller.  This gives
**coroutine / generator semantics**: the body can call `k` any number of times to
yield values back to the caller, and the caller can resume the body by calling `k`
again.

```scheme
(call/cc-full (lambda (k) body...))
```

- **First call** — `call/cc-full` starts the body.  If the body calls `(k v)`, the

  caller immediately receives `v` and the body is suspended at that point.

- **Resuming** — calling `k` again from the caller's side wakes the body; the body

  continues past its suspended `(k ...)` expression and runs until the next `(k v)`
  or until it returns normally.

- **Normal return** — when the body returns without calling `k`, that return value is

  delivered to whoever last called `k` (or to the original `call/cc-full` form if `k`
  was never invoked at all).

> **Limitation:** calling `k` *after* the body has finished returns the body's final
> value without re-running it.  True upward continuations (re-entering a completed
> stack frame) are not supported in a tree-walk interpreter without a CPS transform.

**Examples:**

```scheme
; Simple escape — behaves identically to call/cc
(call/cc-full (lambda (k) (* 5 4)))              ; => 20
(call/cc-full (lambda (k) (* 5 (k 4))))          ; => 4
(* 2 (call/cc-full (lambda (k) (* 5 (k 4)))))   ; => 8

; Coroutine — body yields three values then returns 'done
(define resume #f)
(define v1 (call/cc-full (lambda (k)
              (set! resume k)
              (k 1)    ; yield 1 — caller receives 1, body suspends here
              (k 2)    ; yield 2
              (k 3)    ; yield 3
              'done))) ; body returns; caller receives 'done
v1            ; => 1
(resume #f)   ; => 2
(resume #f)   ; => 3
(resume #f)   ; => done
```

#### `make-generator` — convenient generator wrapper

`make-generator` (defined in `init.ss` via `call/cc-full`) packages this pattern into
a zero-argument thunk that returns the next value on each call:

```scheme
(define (make-generator proc) ...)

; Usage
(define gen
  (make-generator (lambda (yield)
    (yield 10)
    (yield 20)
    (yield 30))))

(gen)   ; => 10
(gen)   ; => 20
(gen)   ; => 30
```

---

### Combinators

The SKI combinator basis. Enable curried application with `(carry #t)` first:

```scheme
(carry #t)

I   ; identity:       \x.x
K   ; constant:       \x.\y.x
S   ; substitution:   \x.\y.\z.((x z)(y z))

(((S K) K) 'a)   ; => a

(carry #f)
```

---

### Unification

A complete most-general-unifier (MGU) based on Robinson's algorithm:

```scheme
(unify u v)
```

Returns the unified term (applying the MGU to `u`), or `"clash"` / `"cycle"` on failure.

**Examples:**

```scheme
(unify 'x 'y)                          ; => y
(unify '(f x y) '(g x y))             ; => "clash"
(unify '(f x (h)) '(f (h) y))         ; => (f (h) (h))
(unify '(f (g x) y) '(f y x))         ; => "cycle"
(unify '(f (g x) y) '(f y (g x)))     ; => (f (g x) (g x))
(unify '(f (g x) y) '(f y z))         ; => (f (g x) (g x))
```

---

### Set Comprehensions

The `set-of` macro provides Haskell-style list comprehensions:

```scheme
(set-of expr clause ...)
```

| Clause form | Meaning |
| ------------- | --------- |
| `(x in list)` | iterate `x` over `list` |
| `(x is expr)` | bind `x` to the value of `expr` |
| `predicate` | filter: include only when truthy |

**Examples:**

```scheme
(set-of x (x in '(a b c)))
; => (a b c)

(set-of x (x in '(1 2 3 4)) (even? x))
; => (2 4)

(set-of (list x y) (x in '(4 2 3)) (y is (* x x)))
; => ((4 16) (2 4) (3 9))

(set-of (list x y) (x in '(a b)) (y in '(1 2)))
; => ((a 1) (a 2) (b 1) (b 2))
```

---

### SRFI-1 Extended List Functions

These supplement the core `map`/`filter`/`fold` already described above.

```scheme
; Folds
(fold         f seed lst)       ; left fold  — f called as (f elem acc)
(fold-right   f seed lst)       ; right fold — f called as (f elem acc)

; Unfolding
(unfold       pred f g seed)    ; build a list: seed → while not pred, emit (f seed), advance (g seed)
(unfold-right pred f g seed)    ; same but builds in reverse

; Search
(list-index   pred lst)         ; index of first element satisfying pred, or #f

; Deletion
(delete       x lst)            ; remove all occurrences equal? to x
(delete       x lst pred)       ; same with custom equality predicate

; List sets (treat lists as sets)
(lset-adjoin  = lst x ...)      ; add elements not already in lst
(lset-union   = lst ...)        ; union of two or more lists
(lset-intersection = lst ...)   ; intersection of two or more lists
(lset-difference   = lst ...)   ; elements in first list not in any other
```

**Examples:**

```scheme
(fold + 0 '(1 2 3 4))                          ; => 10
(fold-right cons '() '(1 2 3))                 ; => (1 2 3)
(unfold (lambda (x) (= x 5)) identity
        (lambda (x) (+ x 1)) 0)               ; => (0 1 2 3 4)
(list-index even? '(3 1 4 1 5))               ; => 2
(delete 3 '(1 2 3 4 3 5))                     ; => (1 2 4 5)
(lset-union equal? '(a b c) '(b c d))         ; => (a b c d)
(lset-intersection equal? '(a b c) '(b c d)) ; => (b c)
(lset-difference equal? '(a b c) '(b c d))   ; => (a)
```

---

### String Extras (SRFI-13)

```scheme
(string-prefix?    prefix s)         ; #t if s starts with prefix
(string-suffix?    suffix s)         ; #t if s ends with suffix
(string-pad        s width)          ; left-pad with spaces to width
(string-pad        s width char)     ; left-pad with char
(string-pad-right  s width)          ; right-pad with spaces to width
(string-pad-right  s width char)     ; right-pad with char
(string-replace    s1 s2 start end)  ; replace s1[start..end) with s2
```

**Examples:**

```scheme
(string-prefix? "he" "hello")          ; => #t
(string-suffix? "lo" "hello")          ; => #t
(string-pad "hi" 5)                    ; => "   hi"
(string-pad-right "hi" 5)              ; => "hi   "
(string-pad "hi" 5 #\*)                ; => "***hi"
(string-replace "hello" "ello" 1 5)    ; => "hello"  (equivalent here)
(string-replace "abcde" "XY" 1 3)      ; => "aXYde"
```

---

### Hash Tables

```scheme
; Construction
(make-hash-table)              ; new empty table (equal? keys)
(make-eq-hash-table)           ; alias — same as make-hash-table
(make-eqv-hash-table)          ; alias — same as make-hash-table
(alist->hash-table alist)      ; build from association list

; Predicates
(hash-table? x)
(hash-table-size ht)           ; number of entries
(hash-table-exists? ht key)    ; #t if key is present
(hash-table-contains? ht key)  ; alias for hash-table-exists?

; Access
(hash-table-ref          ht key)           ; error if missing
(hash-table-ref/default  ht key default)   ; return default if missing
(hash-table-get          ht key default)   ; alias for hash-table-ref/default

; Mutation
(hash-table-set!    ht key val)   ; insert or update
(hash-table-put!    ht key val)   ; alias for hash-table-set!
(hash-table-delete! ht key)       ; remove key
(hash-table-clear!  ht)           ; remove all entries
(hash-table-update! ht key f)                    ; update value with (f old-val)
(hash-table-update!/default ht key f default)    ; same, using default when missing

; Whole-table operations
(hash-table-keys   ht)            ; list of all keys
(hash-table-values ht)            ; list of all values
(hash-table->alist ht)            ; list of (key . value) dotted pairs
(hash-table-walk   ht proc)       ; call (proc key value) for each entry
(hash-table-for-each ht proc)     ; alias for hash-table-walk
(hash-table-copy   ht)            ; shallow copy
(hash-table-merge! ht1 ht2)       ; merge ht2 into ht1 (ht2 wins on conflict); returns ht1
(hash-table-map    ht f)          ; new table with values replaced by (f key value)
```

**Notes:**
- `hash-table->alist` returns an association list of `(key . value)` dotted pairs (not two-element lists).
- `alist->hash-table` expects the same dotted-pair format: `'((key . value) ...)`.

**Examples:**

```scheme
(define ht (make-hash-table))
(hash-table-set! ht 'name "Alice")
(hash-table-set! ht 'age  30)
(hash-table-ref ht 'name)                  ; => "Alice"
(hash-table-ref/default ht 'missing 0)    ; => 0
(hash-table-size ht)                       ; => 2
(hash-table-exists? ht 'age)               ; => #t
(hash-table-keys ht)                       ; => (name age)  (order may vary)
(hash-table->alist ht)                     ; => ((name . "Alice") (age . 30))

; alist->hash-table expects dotted-pair alists
(define ht2 (alist->hash-table '((x . 10) (y . 20))))
(hash-table-ref ht2 'x)                    ; => 10

(define ht3 (make-hash-table))
(hash-table-set! ht3 'age 99)
(hash-table-merge! ht ht3)
(hash-table-ref ht 'age)                   ; => 99  (ht3 wins)
```

---

### File System

These functions wrap `System.IO` and operate relative to the current working directory.

```scheme
; Predicates
(file-exists?      path)     ; #t if file exists
(directory-exists? path)     ; #t if directory exists

; File operations
(delete-file  path)          ; delete a file
(rename-file  old new)       ; rename / move a file
(copy-file    src dst)       ; copy a file
(file-size    path)          ; file length in bytes (integer)

; Directory operations
(current-directory)          ; return current working directory as string
(set-current-directory! path); change current working directory
(create-directory   path)    ; create a new directory
(directory-list     path)    ; list of file names in directory
(directory-list-subdirs path); list of sub-directory names
```

**Examples:**

```scheme
(file-exists? "init.ss")             ; => #t  (always present at runtime)
(file-exists? "missing.txt")         ; => #f
(directory-exists? ".")              ; => #t
(string? (current-directory))        ; => #t

(call-with-output-file "out.txt"
  (lambda (p) (display "hello" p)))
(file-size "out.txt")                ; => 5
(delete-file "out.txt")
```

---

### Parameter Objects (SRFI-39)

Parameter objects provide dynamically-scoped mutable bindings — a safe alternative
to `fluid-let`.

```scheme
(make-parameter init)             ; create a parameter with initial value
(make-parameter init converter)   ; apply converter to every value stored
(parameterize ((param val) ...) body ...)
  ; evaluate body with each param rebound to val; restores on exit (even on throw)
```

A parameter object `p` is also callable:

- `(p)` — read current value

- `(p new-val)` — write new value (use `parameterize` for scoped changes)

**Examples:**

```scheme
(define indent (make-parameter 0))
(indent)                               ; => 0
(parameterize ((indent 4))
  (indent))                            ; => 4
(indent)                               ; => 0  (restored)

; Nested parameterize
(parameterize ((indent 2))
  (list (indent)
        (parameterize ((indent 8)) (indent))
        (indent)))                     ; => (2 8 2)

; Converter: always store string-length instead of the string
(define width (make-parameter "hello" string-length))
(width)                                ; => 5
(parameterize ((width "hi")) (width))  ; => 2
```

---

### Random Numbers

```scheme
(random-integer n)      ; exact integer in [0, n)
(random-real)           ; inexact double  in [0.0, 1.0)
(random n)              ; if n is exact: (random-integer n)
                        ; if n is inexact: (* (random-real) n)
(random-seed! n)        ; reseed the generator (for reproducibility)
(random-choice lst)     ; pick one random element from lst
(random-shuffle lst)    ; return a shuffled copy of lst (Fisher-Yates)
```

The underlying generator is `System.Random` stored in `*random-gen*`.

**Examples:**

```scheme
(random-integer 6)                    ; => 0..5  (like a die)
(random-real)                         ; => e.g. 0.7341...
(random 10)                           ; => exact integer 0..9
(random 1.0)                          ; => inexact double 0.0..1.0

(random-seed! 42)
(random 1000)                         ; deterministic after seeding

(random-choice '(rock paper scissors)); => one of the three
(length (random-shuffle '(1 2 3 4)))  ; => 4  (same elements, shuffled)
```

---

### `receive` (SRFI-8)

`receive` binds multiple return values from an expression, similar to
`call-with-values` but with more readable syntax.

```scheme
(receive (var ...)  expr  body ...)   ; bind each value to a var
(receive rest-var   expr  body ...)   ; bind all values as a list
(receive ()         expr  body ...)   ; ignore values, evaluate for side-effects
```

**Examples:**

```scheme
(receive (q r) (exact-integer-sqrt 17)
  (list q r))                          ; => (4 1)

(receive (a b c) (values 1 2 3)
  (+ a b c))                           ; => 6

(receive all (values 1 2 3)
  all)                                 ; => (1 2 3)
```

---

### `cut` / `cute` (SRFI-26)

Partial application using `<>` as a slot marker for arguments supplied later.
`cute` is identical to `cut` under strict (applicative-order) evaluation.

```scheme
(cut  proc arg-or-<> ...)   ; returns a new procedure; <> marks unfilled slots
(cute proc arg-or-<> ...)   ; alias for cut
```

**Examples:**

```scheme
(map (cut + <> 1) '(1 2 3))          ; => (2 3 4)
(map (cut * 2 <>) '(1 2 3))          ; => (2 4 6)
((cut list 1 <> 3) 2)                ; => (1 2 3)
(map (cut list 'x <> 'z) '(a b c))  ; => ((x a z) (x b z) (x c z))
(filter (cut < <> 5) '(3 7 1 8 2))  ; => (3 1 2)
```

---

### `fluid-let`

Temporarily rebind top-level variables for the dynamic extent of a body.

> **Note:** `fluid-let` does **not** restore bindings on exception.
> Use `parameterize` (parameter objects) for exception-safe dynamic binding.

```scheme
(fluid-let ((var expr) ...) body ...)
```

**Example:**

```scheme
(define x 10)
(fluid-let ((x 99))
  x)           ; => 99
x              ; => 10  (restored after body completes normally)
```

---

### `while` / `until`

Imperative loop macros.  Both return an unspecified value.

```scheme
(while pred body ...)   ; loop while pred is truthy
(until pred body ...)   ; loop until pred becomes truthy
```

**Examples:**

```scheme
(let ((i 0) (s 0))
  (while (< i 5)
    (set! s (+ s i))
    (set! i (+ i 1)))
  s)                    ; => 10   (0+1+2+3+4)

(let ((i 0))
  (until (= i 3)
    (set! i (+ i 1)))
  i)                    ; => 3
```

---

## .NET Interoperability

Lisp can call any .NET method or access any field/property via reflection.
Type names are passed as quoted symbols.

### `(new 'TypeName arg ...)`

Instantiate a .NET type:

```scheme
(new 'System.Text.StringBuilder)
(new 'System.IO.StreamReader "file.txt")
(new 'System.String #\x 5)              ; => "xxxxx"
```

### `(call obj 'MethodName arg ...)`

Call an instance method:

```scheme
(call "hello" 'ToUpper)                 ; => "HELLO"
(call "hello world" 'IndexOf "world")   ; => 6
(call sb 'Append "hi")
(call sb 'ToString)
```

### `(call-static 'TypeName 'MethodName arg ...)`

Call a static method:

```scheme
(call-static 'System.Math 'Sqrt 16.0)
(call-static 'System.String 'Format "{0}+{1}" 1 2)
(call-static 'System.Console 'WriteLine "hello")
(call-static 'System.Convert 'ToInt32 "42")
```

### `(get obj-or-type 'PropertyOrField index ...)`

Get a property, field, or indexed item:

```scheme
(get "hello" 'Length)                   ; => 5
(get 'System.Math 'PI)                  ; => 3.14159...
(get lst 'Item 0)                       ; indexed access
(get 'System.Environment 'Version)      ; static property
```

### `(set obj-or-type 'PropertyOrField value)`

Set a property or field:

```scheme
(set 'Lisp.Interpreter 'EndProgram #t)     ; quit
(set 'Lisp.Expressions.App 'CarryOn #t)    ; enable carry
(set v 'Item 0 99)                         ; indexed set
```

### Practical Examples

```scheme
; Read a file
(define content
  (call (new 'System.IO.StreamReader "data.txt") 'ReadToEnd))

; Build a string with StringBuilder
(let ((sb (new 'System.Text.StringBuilder)))
  (call sb 'Append "Hello")
  (call sb 'Append ", World!")
  (call sb 'ToString))

; Environment variable
(call-static 'System.Environment 'GetEnvironmentVariable "PATH")

; Date/time
(call (get 'System.DateTime 'Now) 'ToString "yyyy-MM-dd")
```

### Type Loading

Types in the running assembly and all loaded assemblies are found automatically.
To load from an external assembly:

```scheme
(get-type "MyType@path\\to\\MyAssembly.dll")
```

---

## Tracing & Debugging

```scheme
(trace #t)               ; enable tracing
(trace #f)               ; disable tracing

(trace-all)              ; trace all symbols
(trace-add 'foo 'bar)    ; trace specific functions
(trace-remove 'foo)      ; stop tracing a function
(trace-clear)            ; clear all trace targets
```

| Symbol | Effect |
| -------- | -------- |
| `_all_` | trace everything |
| `lambda` | trace every lambda creation |
| `macro` | trace macro expansion results |
| any name | trace calls to that procedure |

```scheme
(trace #t)
(trace-add 'fib)
(define (fib n) (if (< n 2) n (+ (fib (- n 1)) (fib (- n 2)))))
(fib 5)
```

---

## Performance Stats

```scheme
(stats #t)               ; enable per-expression stats reporting
(stats #f)               ; disable
(stats-reset)            ; clear accumulated totals
(stats-total)            ; print totals accumulated so far
```

When enabled, after each top-level expression is evaluated the interpreter prints a grouped report.
Totals are accumulated across evaluations until `(stats-reset)` is called.

```text
  stats:
    elapsed      <ms> ms
    status       <summary>
    runtime      <vm-only or fallback summary>
    work         closures=<n>, prims=<n>
    control      tail-calls=<n>, env-frames=<n>
    throughput   <derived rates or "sample too small">
    fallback     <none | sites=<n>, runs=<n>, tree-walk=<n>>
    memory       allocated=<bytes>, heap=<bytes>, gc=<g0>/<g1>/<g2>
    emit-kinds   <optional fallback-site kind summary>
    exec-kinds   <optional fallback-run kind summary>
```

- **elapsed** — wall-clock time of the evaluation in milliseconds.

- **status** — quick health signal for the run, for example `clean vm path` or `interp fallback observed`.

- **runtime** — whether execution stayed on the VM path or used fallback at runtime.

- **work** — counts of closure invocations and primitive calls.

- **control** — tail-call and environment-frame churn.

- **throughput** — derived rates (`closures/ms`, `prims/ms`) for non-trivial runs.

- **fallback** — counts of compiler-emitted fallback sites, executed `INTERP` runs, and tree-walk dispatches.

- **memory** — per-expression allocation plus live heap / GC counts when available.

- **emit-kinds / exec-kinds** — optional per-expression-kind fallback attribution, shown only when nonzero.

- **closures** — number of closure invocations (user-defined function calls, including

  every trampoline bounce for tail-recursive loops).

**Example:**

```scheme
(define (fib n) (if (< n 2) n (+ (fib (- n 1)) (fib (- n 2)))))

(stats #t)
(fib 30)
;   stats:
;     elapsed      2521.891 ms
;     status       clean vm path
;     runtime      vm-only
;     work         closures=4,381,783, prims=5,702,885
;     control      tail-calls=0, env-frames=4,381,783
;     throughput   closures/ms=1737.6, prims/ms=2261.6
;     fallback     none
;     memory       allocated=<implementation-dependent>

(stats-total)     ; summarize all runs since the last reset
(stats-reset)     ; start a fresh totals window
(stats #f)
```

If console colors are enabled with `(colors #t)`, the `status`, `runtime`, `fallback`, and fallback-kind lines are severity-colored in interactive output. Redirected output stays plain text.

---

## Show Lines

```scheme
(showlines #t)                 ; echo each top-level form before it is executed
(showlines #f)                 ; disable
(show-input-lines #t)          ; same command, original name
(show-input-lines #f)          ; disable
```

When enabled, before each top-level expression is evaluated the interpreter prints the
source text of that expression prefixed with `>>`. This applies everywhere forms are
evaluated: the interactive REPL, files passed on the command line, and files loaded with
`(load ...)` at runtime.

**Example — interactive:**

```scheme
lisp> (showlines #t)
lisp> (+ 1 2)
>> (+ 1 2)
3
```

**Example — file (`demo.ss`):**

```scheme
(showlines #t)
(define x 42)
(display x)
(newline)
```

Output:

```text
>> (showlines #t)
>> (define x 42)
>> (display x)
42
>> (newline)
```

**Example — loading a file at runtime:**

```scheme
; Use the global showlines flag:
(showlines #t)
(load "mylib.ss")         ; prints each form in mylib.ss as it executes
(showlines #f)

; Or use load's built-in echo argument (prints in gray, scoped to the file):
(load "mylib.ss" #t)      ; same effect, no need to toggle showlines
```

---

## Introspection

### Debugging / Introspection Quick Start

The fastest way to inspect what the interpreter is doing is to combine the line echo,
trace, stats, and disassembly commands in one short session:

```scheme
(showlines #t)              ; echo each top-level form before execution
(trace #t)                  ; enable trace output
(trace-add 'fib 'macro)     ; trace a procedure and macro expansion
(stats #t)                  ; print timing and counters after each top-level eval
(colors #t)                 ; enable colored console output

(define (fib n)
  (if (< n 2) n (+ (fib (- n 1)) (fib (- n 2)))))

(fib 5)                     ; inspect runtime behavior
(disasm fib)                ; compact bytecode + source sections
(disasm-verbose #t)
(disasm fib)                ; full bytecode + every source section

(trace-clear)
(trace #f)
(stats #f)
(showlines #f)
(disasm-verbose #f)
```

Typical workflow:

1. Turn on `(showlines #t)` if you need to see exactly which top-level forms are executing.
2. Add `(trace #t)` plus `(trace-add 'name)` when you need evaluation-time detail for a specific function or macro.
3. Enable `(stats #t)` when you want timing, closure-call counts, tail-call counts, allocation, and GC information.
4. Use `(stats-total)` when you want to summarize several top-level benchmark runs, and `(stats-reset)` before starting a new measurement window.
5. Use `(disasm proc)` to inspect the compiled VM bytecode and its source-section grouping.
6. Switch to `(disasm-verbose #t)` only when the compact disassembly hides details you need, such as single-variable or literal sections.
7. Use `(colors #f)` if you want plain console output while keeping the same debugging information.

Uncaught runtime failures now participate in the same tooling story: they print source
locations and a Scheme stack trace automatically, so in many cases you can start with
the error report itself before turning on `trace` or `disasm`.

For the exact disassembly output format, source-section grouping rules, compact vs.
verbose behavior, and examples, see [`disasm` — Bytecode Disassembler](#disasm--bytecode-disassembler).

```scheme
; Procedures
(procedure?      x)              ; #t if x is a closure or builtin
(closure?        x)              ; #t if x is specifically a Lisp.Closure
(closure-args    f)              ; argument name list
(closure-body    f)              ; body expression list
(procedures->list)               ; all defined procedure names

; Macros
(macro?          x)              ; #t if x is a defined macro
(macro-body      x)              ; list of macro clauses
(macro-const     x)              ; list of macro literals
(macros->list)                   ; all defined macro names
(macros->vector)                 ; same, as a vector

; Symbols
(symbols->list)                  ; all currently interned symbols

; Environment inspection
(env)                            ; print all user-defined functions as (define ...) forms
(env 'name)                      ; print a single named function definition
(disasm proc)                    ; print the VM bytecode of a compiled procedure

; Utilities
(help)                           ; print a short summary of useful REPL commands
(lastValue #f)                   ; suppress intermediate result printing
(int? x)                         ; alias for (integer? x)
(colors #t)                      ; enable colored console output
(colors #f)                      ; disable colored console output
(p-adic 7)                       ; display exact numbers in 7-adic form
(p-adic 7 32)                    ; same, but show 32 p-adic digits
(p-adic 10)                      ; restore default decimal/rational display
(disasm-verbose #t)              ; show trivial source labels in disassembly
(disasm-verbose #f)              ; compact disassembly labels (default)
(stats #t)                       ; enable grouped stats per top-level eval
(stats #f)                       ; disable stats
(stats-reset)                    ; clear accumulated stats totals
(stats-total)                    ; print accumulated stats totals
(exit)                           ; terminate the interpreter
(LispVersion)                    ; interpreter version string
(.NetVer)                        ; .NET runtime version string
(GACRoot)                        ; .NET Framework root path
(SysRoot)                        ; Windows %SystemRoot% path
(Environment "VAR")              ; get an environment variable

; Pre-defined math functions
(! n)                            ; factorial: n! (recursive, exact integers)
(fib n)                          ; Fibonacci: F(n) — 0-indexed (fib 0)=0, (fib 1)=1, (fib 7)=13
```

**Examples:**

```scheme
(! 5)                            ; => 120
(! 10)                           ; => 3628800
(map ! '(0 1 2 3 4 5))          ; => (1 1 2 6 24 120)

(fib 7)                          ; => 13
(map fib '(0 1 2 3 4 5 6 7))    ; => (0 1 1 2 3 5 8 13)
```

### `disasm` — Bytecode Disassembler

`(disasm proc)` compiles `proc` (if it is a `VmClosure`) and pretty-prints its bytecode.
The disassembler now annotates bytecode with the originating Scheme source sections,
uses color in the console by default, and indents nested subexpressions so bytecode
for calls such as `(+ a b)` and `(- k 1)` appears visually under their enclosing form.
Large generated forms are also reformatted more aggressively, so busy call sites expand
into one-argument-per-line blocks instead of staying as a single wide source label.

By default the disassembly is **compact**: trivial source labels for single variables
and literals are hidden. You can switch back to a fully verbose view with:

```scheme
(disasm-verbose #t)
```

To restore the compact default:

```scheme
(disasm-verbose #f)
```

Console coloring for disassembly, trace, evaluation results, and stats can be
enabled or disabled independently of redirection:

```scheme
(colors #t)
(colors #f)
```

The color hierarchy is intentional: section headers and `;; source ...` annotations stay
muted, while opcodes, operands, symbol names, and primitive targets remain the visual focus.

`(disasm proc)` can also accept any other value and describes it accordingly:

| Argument type | Output |
| --------------- | -------- |
| `VmClosure` (compiled lambda) | full bytecode listing, nested prototypes recursively indented |
| fallback-produced value without bytecode | `(no bytecode available)` |
| built-in `Primitive` | `(built-in primitive: <MethodName>)` |
| anything else | `(not a procedure: <value>)` |

**Example — disassembling a simple squaring function:**

```scheme
(define (square x) (* x x))
(disasm square)
```

```text
=== closure  lambda(x)  (3 instructions) ===
  ;; source (* x x)
     0: LOAD_VAR          #0  x
     1: LOAD_VAR          #1  x
     2: PRIM              Mul_Prim  argc=2
```

**Example — disassembling an inline lambda:**

```scheme
(disasm (lambda (x) (if (= x 0) 1 (* x x))))
```

```text
=== closure  lambda(x)  (10 instructions) ===
    ;; source (= x 0)
  0: LOAD_VAR          #0  x
  1: LOAD_CONST        #0  0
  2: PRIM              Eq_Prim  argc=2
  3: JUMP_IF_FALSE     -> 7
  4: LOAD_CONST        #1  1
  5: RETURN
  6: JUMP              -> 10
    ;; source (* x x)
  7: LOAD_VAR          #1  x
  8: LOAD_VAR          #2  x
  9: PRIM              Mul_Prim  argc=2
```

**Example — disassembling `map` from `init.ss`:**

```scheme
(disasm map)
```

```text
=== closure  lambda(f ls . more)  (8 instructions) ===
    ;; source more
     0: LOAD_VAR          #0  more
    ;; source (null? more)
     1: PRIM              NullQ_Prim  argc=1
     2: JUMP_IF_FALSE     -> 6
    ;; source (lambda () (define (map1 ls) ...))
     3: MAKE_CLOSURE      proto #0  params=()
     4: TAIL_CALL         argc=0  (tail)
     5: JUMP              -> 8
    ;; source (lambda () ...)
     6: MAKE_CLOSURE      proto #1  params=()
     7: TAIL_CALL         argc=0  (tail)
  === proto #0  lambda()  (6 instructions) ===
   ;; source (define map1 (lambda (ls) ...))
       0: MAKE_CLOSURE      proto #0  params=(ls)
       1: DEFINE_VAR        #0  map1
       2: POP
   ;; source (map1 ls)
       3: LOAD_VAR          #1  map1
       4: LOAD_VAR          #2  ls
       5: TAIL_CALL         argc=1  (tail)
    === proto #0  lambda(ls)  (16 instructions) ===
     ;; source (null? ls)
      0: LOAD_VAR          #0  ls
      1: PRIM              NullQ_Prim  argc=1
         2: JUMP_IF_FALSE     -> 6
         3: LOAD_CONST        #0  ()
         4: RETURN
         5: JUMP              -> 16
     ;; source (cons (f (car ls)) (map1 (cdr ls)))
         6: LOAD_VAR          #1  f
    ;; source (car ls)
         7: LOAD_VAR          #2  ls
         8: PRIM              Car_Prim  argc=1
         9: CALL              argc=1
    ;; source (map1 (cdr ls))
        10: LOAD_VAR          #3  map1
        11: LOAD_VAR          #4  ls
        12: PRIM              Cdr_Prim  argc=1
        13: CALL              argc=1
        14: PRIM              Cons_Prim  argc=2
        15: RETURN
  === proto #1  lambda()  ... ===
       ; (multi-list variant — omitted for brevity)
```

Nested `proto #N lambda(...)` blocks are the compiled bodies of closures created
by `MAKE_CLOSURE` instructions in the outer chunk.

In compact mode, some trivial labels such as a bare variable section may be omitted.
Enable `(disasm-verbose #t)` if you want every simple variable and literal section
to be shown explicitly.

---

### `env` — Environment Inspection

`(env)` prints every user-defined function that is currently bound in the global environment,
displayed as a readable `(define ...)` form, sorted alphabetically:

```scheme
(define (square x) (* x x))
(define (add a b) (+ a b))

(env)
; =>
; (define (add a b) (+ a b))
; (define (square x) (* x x))
```

`(env 'name)` restricts output to a single named binding:

```scheme
(env 'square)
; => (define (square x) (* x x))
```

Only closures (functions defined with `define` or `lambda`) appear in the output; plain value
bindings (e.g. `(define pi 3.14159)`) are silently skipped.

---

## Interpreter Behaviour Notes

These are places where this interpreter intentionally or incidentally diverges from standard
Scheme (R5RS/R7RS):

| Feature | Standard Scheme | This interpreter |
| --------- | ---------------- | ----------------- |
| `inexact->exact` / `exact` | truncates toward zero | uses `System.Convert.ToInt32` — **rounds** (e.g. `(exact 3.9)` = `4`) |
| Symbol case | R5RS: fold to lower-case | **case-sensitive**: `'a` ≠ `'A` |
| `call/cc` / `let/cc` | full re-entrant continuations | escape continuations only (local exit via tagged `try`/`throw`) |
| `call/cc-full` | N/A (extension) | coroutine-style reentrant continuations via dedicated thread + semaphores; supports multiple yields but not upward continuations after body finishes |
| `eq?` on `'()` | any two `'()` values are `eq?` | two separately-evaluated `'()` may not be `eq?`; use `null?` or `equal?` |

---

## Architecture

The interpreter is implemented under `source/` in the `Lisp` namespace, with execution split across parser, macro expander, AST, bytecode compiler, and VM layers:

| Class | Namespace | Role |
| ------- | ----------- | ------ |
| `Util` | `Lisp` | Parser, reflection helpers, `Dump` printer |
| `Symbol` | `Lisp` | Interned symbol table (`Dictionary<string,Symbol>`) |
| `Pair` | `Lisp` | Linked list / cons cell, implements `ICollection` |
| `Closure` | `Lisp` | First-class function value with lexical environment |
| `Arithmetic` | `Lisp` | Numeric dispatch (`int`/`float`/`double`) |
| `Macro` | `Lisp.Macros` | Pattern-based macro system with ellipsis and gensym |
| `Program` | `Lisp.Programs` | Top-level evaluator, global environment |
| `Env` / `Extended_Env` | `Lisp.Environment` | Lexical environment chain |
| `Expression` subclasses | `Lisp.Expressions` | AST: `Lit`, `Var`, `Lambda`, `Define`, `If`, `Try`, `App`, `Prim`, `Assignment`, `CommaAt`, `Evaluate` |
| `Prim` | `Lisp.Expressions` | Built-in primitives: `new`, `get`, `set`, `call`, `call-static`, `LESSTHAN` |
| `Interpreter` | `Lisp` | `Main` entry point, REPL loop |

### Evaluation Pipeline

All expressions are compiled to bytecode before execution:

```text
Input string
     │
     ▼
 Util.Parse()              →  object tree (Pair / Symbol / literal)
     │
     ▼
 Macro.Check()             →  macro-expanded object tree
     │
     ▼
 Expression.Parse()        →  typed AST (Expression subclass tree)
     │
     ▼
 BytecodeCompiler.CompileTop()  →  Chunk  (bytecode + constant pool)
     │
     ▼
 Vm.Execute(chunk, env)    →  result object
     │
     ▼
 Util.Dump()               →  printed representation
```

### Bytecode VM

The interpreter uses a **stack-based bytecode VM** (`Vm.Execute`) rather than direct
AST tree-walking. Every top-level expression — and every `lambda` — is compiled to
a `Chunk` of flat instructions before execution.

#### Instruction set

| Opcode | Operand | Description |
| -------- | --------- | ------------- |
| `LOAD_CONST` | constant index | push a constant from the chunk's constant pool |
| `LOAD_VAR` | symbol index | look up a variable in the current environment and push it |
| `STORE_VAR` | symbol index | pop stack top, mutate an existing binding (`set!`), push symbol |
| `DEFINE_VAR` | symbol index | pop stack top, create a new binding in the current env, push symbol |
| `POP` | — | discard the top of the operand stack |
| `JUMP` | target offset | unconditional branch to instruction index |
| `JUMP_IF_FALSE` | target offset | pop top; jump if it is `#f`, otherwise fall through |
| `RETURN` | — | pop return value, restore caller frame, push return value to caller |
| `MAKE_CLOSURE` | prototype index | capture current env + compiled prototype → push `VmClosure` |
| `CALL` | argc | call the procedure `argc` positions below the stack top |
| `TAIL_CALL` | argc | same as `CALL` but reuses the current call frame (TCO) |
| `PRIM` | packed `primIdx << 16` with `argc` | call a C# built-in `Primitive` delegate directly, no frame push |
| `INTERP` | AST node index | fall back to tree-walk evaluation for unsupported forms |

The `INTERP` opcode is a targeted escape hatch for forms that still require AST-driven
evaluation at runtime. Most ordinary language features, including quasiquote splices,
`try`/`try-cont`, local syntax bindings, and standard procedure calls, now stay on the VM path.
In current focused validation, the remaining routine runtime fallback is primarily dynamic
`evaluate` usage.

#### Key data structures

| C# class | Role |
| ---------- | ------ |
| `Chunk` | Compiled bytecode unit: instruction list, constant pool, symbol table, nested prototypes, primitive references, AST fallback nodes |
| `VmClosure` | A `Closure` subclass that holds a `Chunk` instead of an AST body; ordinary `lambda` evaluation now produces these directly |
| `CallFrame` | One entry on the VM call stack: `Chunk`, program counter, `Env`, stack-base index |
| `BytecodeCompiler` | Static class; `CompileTop(Expression) → Chunk`; compiles top-level expressions, `Lambda`, `If`, `Define`, `App`, `Prim`, `Assignment`, `Lit`, and local syntax-expanded bodies |
| `Vm` | Static class; `Execute(Chunk, Env) → object`; flat operand array + flat `CallFrame[]` call stack; proper TCO via frame reuse |

#### Tail-call optimisation (TCO)

`TAIL_CALL` reuses the current `CallFrame` in-place: it overwrites the `Chunk`, `Pc`,
`Env`, and `StackBase` fields rather than pushing a new frame. This means mutually
recursive loops and `named let` loops run in constant stack space regardless of depth.

For calls through a tree-walk `Closure` or a C# `Primitive`, TCO trampolining is still
used (the VM drives the trampoline itself, catching `TailCall` thunks).

#### `disasm` — disassemble a procedure

See the [disasm section](#disasm--bytecode-disassembler) in Introspection above for
full documentation and worked examples.

### Number Types

| C# type | Scheme type | Exactness | Notes |
| --------- | ------------- | ----------- | ------- |
| `System.Int32` | integer | exact | `42`, `-7` |
| `System.Numerics.BigInteger` | integer | exact | automatic overflow promotion |
| `Lisp.Rational` | rational | exact | `p/q` struct, normalised; see below |
| `System.Double` | real | inexact | 64-bit IEEE 754; shortest round-trip printing |
| `System.Numerics.Complex` | complex | inexact | `a+bi` literals; see below |

Real numbers print as the shortest decimal string that round-trips back to the same
`double` value. Whole-number doubles always include a decimal point as an inexactness
marker (`3.0` → `3.`, `100.0` → `100.`). Special values: `+inf.0`, `-inf.0`, `+nan.0`.

The arithmetic tower promotes as required: `int → BigInteger` on overflow;
`int/BigInteger/Rational → double` when mixed with an inexact operand;
any numeric type `→ Complex` when mixed with a complex operand.

#### BigInteger — Automatic Overflow Promotion

When an `Int32` operation would overflow, the result is promoted transparently to
`System.Numerics.BigInteger`.  Demotion back to `Int32` happens automatically when
the value fits.

```scheme
(+ 2147483647 1)            ; => 2147483648  (BigInteger, not overflow)
(- (+ 2147483647 1) 1)      ; => 2147483647  (demoted back to Int32)
(expt 2 100)                ; => 1267650600228229401496703205376
(** 2 100)                  ; same — ** is an alias for expt
(! 30)                      ; => 265252859812191058636308480000000  (30 factorial)
(fib 100)                   ; => 354224848179261915075
```

BigInteger literals are parsed automatically when a numeric literal exceeds the
`Int32` range:

```scheme
99999999999999999999          ; parsed as BigInteger
(exact? 99999999999999999999) ; => #t
```

All numeric predicates, arithmetic, comparison, bitwise, and radix functions work
transparently on BigInteger values.

#### Rational — Automatic Exact Fractions

`Lisp.Rational` is a C# `struct` holding two `BigInteger` fields (`Numer`, `Denom`)
always kept in **normalised form**: GCD is divided out, the denominator is positive,
and when the denominator equals 1 the value is demoted back to `Int32` or `BigInteger`.

Integer division `/` returns a `Rational` whenever the result is not whole:

```scheme
(/ 1 3)              ; => 1/3     (Rational, not 0.333…)
(/ 4 2)              ; => 2       (Int32, GCD normalised)
(exact? (/ 1 3))     ; => #t
```

`inexact->exact` converts a `double` to the exact `Rational` that the IEEE-754
bit-pattern represents:

```scheme
(inexact->exact 0.1)  ; => 3602879701896397/36028797018963968
(inexact->exact 0.5)  ; => 1/2
```

Rational arithmetic is performed purely in `BigInteger` and the result is normalised
before being returned, so no floating-point error accumulates.

#### Complex — Inexact Pairs

`System.Numerics.Complex` provides hardware-speed complex arithmetic.  All complex
values are inexact; operations that produce a result with a zero imaginary part still
return a `Complex` unless the *constructor* was given an exact `0`:

```scheme
(make-rectangular 3.0 0.0)  ; => 3.+0.i   (remains Complex)
(make-rectangular 3   0)    ; => 3        (exact 0 → strip imaginary)
```

Complex numbers are printed in `a+bi` form with trailing dots on whole-number
components to signal inexactness:

```text
3+4i        prints as  3.+4.i
1.5-2.5i    prints as  1.5-2.5i
+i          prints as  0.+1.i
```
