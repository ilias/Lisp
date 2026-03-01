# Lisp — A Scheme Interpreter for .NET

**Lisp** is a Scheme interpreter written in C#, targeting .NET 10. It implements a substantial
subset of R5RS Scheme with a pattern-based macro system, a full standard library written in
Scheme itself (`init.ss`), and deep two-way .NET interoperability via reflection.

**Author:** Ilias H. Mavreas  
**Version:** 2.x  
**Runtime:** .NET 10  

---

## Table of Contents

1. [Building & Running](#building--running)
2. [REPL Usage](#repl-usage)
3. [Language Reference](#language-reference)
   - [Literals & Data Types](#literals--data-types)
   - [Core Special Forms](#core-special-forms)
   - [Lambda Shorthand](#lambda-shorthand)
   - [Quasiquotation](#quasiquotation)
   - [Macros](#macros)
   - [Error Handling](#error-handling)
4. [Standard Library](#standard-library)
   - [Pairs & Lists](#pairs--lists)
   - [Numbers](#numbers)
   - [Characters](#characters)
   - [Strings](#strings)
   - [Booleans & Equality](#booleans--equality)
   - [Symbols](#symbols)
   - [Vectors](#vectors)
   - [Input / Output](#input--output)
   - [Records](#records)
   - [Multiple Values](#multiple-values)
   - [Delay & Force](#delay--force)
   - [Continuations](#continuations)
   - [Combinators](#combinators)
   - [Unification](#unification)
   - [Set Comprehensions](#set-comprehensions)
5. [.NET Interoperability](#net-interoperability)
6. [Tracing & Debugging](#tracing--debugging)
7. [Introspection](#introspection)
8. [Architecture](#architecture)

---

## Building & Running

```
dotnet build
dotnet run
```

`init.ss` is automatically copied to the build output directory and loaded at startup.

---

## REPL Usage

The REPL prints a `lisp> ` prompt. Enter an expression and press **Enter twice** (an empty
line terminates input and triggers evaluation):

```
lisp> (+ 1 2)
...    3
lisp> (define (fact n)
...      (if (= n 0) 1 (* n (fact (- n 1)))))
...    fact
lisp> (fact 10)
...    3628800
```

Type `(exit)` to quit.

---

## Language Reference

### Literals & Data Types

| Literal | Type | Example |
|---------|------|---------|
| Integer | `System.Int32` | `42`, `-7` |
| Real | `System.Single` | `3.14`, `-0.5` |
| Boolean | `System.Boolean` | `#t`, `#f` |
| Character | `System.Char` | `#\a`, `#\space` |
| String | `System.String` | `"hello\nworld"` |
| Symbol | `Lisp.Symbol` | `foo`, `+`, `my-var` |
| Pair / List | `Lisp.Pair` | `(1 2 3)`, `(a . b)` |
| Vector | `System.Collections.ArrayList` | `#(1 2 3)` |
| Empty list | `Lisp.Pair(null)` | `'()` |
| Quote | — | `'expr` &nbsp;≡&nbsp; `(quote expr)` |

### Core Special Forms

#### `define`

```scheme
(define name value)              ; bind name to value
(define (name args...) body...)  ; define a procedure
(define (name . rest) body...)   ; variadic procedure
```

#### `lambda`

```scheme
(lambda (x y) body...)           ; fixed arity
(lambda (x . rest) body...)      ; variadic
(lambda () body...)              ; thunk
```

#### `if` / `cond` / `case` / `when` / `unless`

```scheme
(if test then else)
(if test then)                   ; else returns #f

(cond
  (test1 expr...)
  (test2 => proc)                ; calls (proc test2) if test2 is true
  (else  expr...))

(case key
  ((val1 val2) expr...)
  (else        expr...))

(when  pred body...)             ; evaluates body when pred is truthy
(unless pred body...)            ; evaluates body when pred is falsy
```

#### `let` / `let*` / `letrec`

```scheme
(let    ((x 1) (y 2)) body...)   ; parallel bindings
(let*   ((x 1) (y x)) body...)   ; sequential bindings
(letrec ((f (lambda ...))) body...) ; recursive bindings

(let name ((x init) ...) body...) ; named let (loop)
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

#### `apply` and `map`

```scheme
(apply + '(1 2 3))               ; => 6
(map (lambda (x) (* x x)) '(1 2 3 4)) ; => (1 4 9 16)
(for-each display '(1 2 3))      ; side-effect each element
```

#### `eval`

```scheme
(eval '(+ 1 2))                  ; => 3
(eval "(+ 1 2)")                 ; also accepts a string
```

#### `rec`

```scheme
(rec sum (lambda (x) (if (= x 0) 0 (+ x (sum (- x 1))))))
```

Shorthand for `(letrec ((sum ...)) sum)`.

---

### Lambda Shorthand

The backslash syntax provides a compact notation for curried lambdas:

```scheme
\x.body          ≡  (lambda (x) body)
\x,y.body        ≡  (lambda (x y) body)
```

**Examples:**

```scheme
(\x.(* x x) 5)   ; => 25

(define add \x,y.(+ x y))
(add 3 4)         ; => 7
```

With `(carry #t)` enabled, curried application is supported:

```scheme
(carry #t)
(\x.\y.\z.(+ x y z) 1 2 3)  ; => 6
```

---

### Quasiquotation

Lisp supports `,` (unquote) and `,@` (unquote-splicing) inside quoted lists:

```scheme
(define x 42)
`(a ,x c)              ; => (a 42 c)
`(a ,@(list 1 2) c)    ; => (a 1 2 c)
```

> **Note:** The backtick `` ` `` is not parsed directly — use `(quote ...)` with `,`/`,@`
> inside list constructors or macro templates, as Lisp internally transforms these via the
> `Lit.Comma` evaluator.

---

### Macros

Lisp has a pattern-based hygienic macro system with ellipsis support.

**Syntax:**

```scheme
(macro name (literal...)
  (pattern  template)
  ...)
```

**Pattern features:**

| Syntax | Meaning |
|--------|---------|
| `var` | bind any single value |
| `var...` | bind zero or more remaining values |
| `(a b) ...` | destructure repeated pairs: `a...` and `b...` |
| `?name` | generate a unique symbol (gensym) |
| `literal` | match an exact symbol (listed in the literals list) |

**Examples:**

```scheme
(macro my-and ()
  ((_)        #t)
  ((_ e)      e)
  ((_ e e1...) (if e (my-and e1...) #f)))

(macro swap! ()
  ((_ a b)
   (let ((?tmp a))
     (set! a b)
     (set! b ?tmp))))
```

---

### Error Handling

```scheme
(try expr catch-expr)           ; catch any exception
(try expr (begin handler...))

(throw "message")               ; raise an exception
```

**Example:**

```scheme
(try (/ 1 0) "division error")  ; => "division error"
```

`throw` delegates to `Lisp.Util.Throw` and can be re-thrown by `if` expressions that
detect it in the exception message.

---

## Standard Library

The standard library is loaded from `init.ss` at startup. The initialization banner lists every
module as it loads:

```
[generics, Utilities, carry, combinators, trace, macros, procedures,
 pair, char, string, boolean, symbol, numbers, vectors, input, output,
 records, multipleValues, delayEvaluation, continuation, Unification, sets] done.
```

### Pairs & Lists

```scheme
(cons a b)               ; construct a pair
(car pair)               ; first element
(cdr pair)               ; rest
(caar x) (cadr x) ...   ; all c[ad]+r up to 4 levels
(null? x)               ; true if x is the empty list
(pair? x)               ; true if x is a Pair
(list x ...)            ; build a list
(list? x)               ; (and (pair? x) (pair? (cdr x)))
(length lst)            ; number of elements
(append lst ...)        ; concatenate lists
(reverse lst)           ; reverse a list
(list-ref lst n)        ; nth element (0-based)
(list-tail lst n)       ; drop n elements
(member x lst)          ; find x in lst, returns tail or #f
(assoc key alist)       ; find key in association list
(set-car! val pair)     ; destructive set
(set-cdr! val pair)     ; destructive set
(union l1 l2)           ; set union
(intersection l1 l2)    ; set intersection
(difference l1 l2)      ; set difference
(adjoin e l)            ; add e to l if not present
```

### Numbers

Integers are `System.Int32`; reals are `System.Single`. Division between two integers returns
an integer when evenly divisible, otherwise a float.

```scheme
(+ a b ...)    (- a b ...)    (* a b ...)    (/ a b ...)
(< a b ...)    (<= a b ...)   (= a b ...)    (<> a b ...)
(>= a b ...)   (> a b ...)

(abs x)        (neg x)        (zero? x)      (positive? x)   (negative? x)
(even? x)      (odd? x)       (exact? x)     (inexact? x)
(number? x)    (integer? x)   (real? x)

(min x ...)    (max x ...)
(floor x)      (ceiling x)    (round x)      (truncate x)
(sqrt x)       (expt x y)     (pow x y)
(exp x)        (log x)        (log10 x)
(sin x)        (cos x)        (tan x)
(asin x)       (acos x)       (atan x)

(quotient x y)   (remainder x y)   (modulo x y)   (gcd a b)
(reciprocal x)

(bit-and a b)    (bit-or a b)    (bit-xor a b)    (xor a b)

(tointeger x)    (todouble x)

PI               ; System.Math.PI
E                ; System.Math.E
```

### Characters

Characters are `System.Char` literals written as `#\x`.

```scheme
(char? x)
(char=? a b ...)    (char<? a b ...)    (char>? a b ...)
(char<=? a b ...)   (char>=? a b ...)   (char<>? a b ...)
; case-insensitive variants: char-ci=?  char-ci<?  etc.

(char-alphabetic? c)   (char-numeric? c)
(char-upper-case? c)   (char-lower-case? c)
(char-whitespace? c)

(char-upcase c)        (char-downcase c)
(char->integer c)      (integer->char n)
```

### Strings

Strings are immutable `System.String` values.

```scheme
(string? x)
(string char ...)               ; build from characters
(string-length s)
(string-ref s n)                ; character at index n
(string-set! s n c)            ; returns new string (not mutating)
(string-copy s)
(string-append s ...)
(substring s start end)         ; end is exclusive index
(string->list s)
(list->string lst)
(string->integer s)
(string->real s)

(string=? a b ...)   (string<? a b ...)   (string>? a b ...)
(string<=? a b ...)  (string>=? a b ...)  (string<>? a b ...)
; case-insensitive: string-ci=?  string-ci<?  etc.
```

### Booleans & Equality

```scheme
(boolean? x)
(not x)                         ; #f if x is #t, #f for anything non-boolean

(eq?    x y)                    ; reference equality (Object.ReferenceEquals)
(eqv?   x y)                    ; value equality (.Equals)
(equal? x y)                    ; deep structural equality
(= x y ...)                     ; alias for equal?
```

### Symbols

Symbols are interned (`Lisp.Symbol`). Two symbols with the same name are always `eq?`.

```scheme
(symbol? x)
(symbol->string s)
(string->symbol s)
(symbol-generate)               ; create a unique gensym
(symbols->list)                 ; list all interned symbols
```

### Vectors

Vectors are `System.Collections.ArrayList`.

```scheme
(vector? x)
(vector x ...)                  ; create a vector
(make-vector n)                 ; n-element vector filled with 0
(make-vector n fill)            ; filled with fill
(vector-length v)
(vector-ref v i)
(vector-set! v i obj)
(vector-copy v)
(vector-fill! v x)
(vector->list v)
(list->vector lst)
(vector-map f v)
(vector-union v1 v2)
(vector-intersection v1 v2)
(vector-difference v1 v2)
```

### Input / Output

```scheme
; Input
(open-input-file "path")        ; sets *INPUT*, returns StreamReader
(close-input-port port)
(current-input-port)
(read . port)                   ; read one char code
(read-line . port)              ; read one line
(read-toend . port)             ; read all remaining text
(read-char . port)
(peek-char . port)
(load "file.ss")                ; load and evaluate a Scheme file

; Output
(open-output-file "path")       ; sets *OUTPUT*, returns StreamWriter
(close-output-port port)
(current-output-port)
(display x . port)
(write x . port)
(writeline x . port)
(newline . port)
(console fmt ...)               ; Console.Write
(consoleLine fmt ...)           ; Console.WriteLine
```

### Records

Records are stored as special vectors with named fields.

```scheme
; Define a record type
(record define <point> (x 0) (y 0))  ; fields with default values
(record define <point> x y)          ; fields with default 0

; Create an instance
(record p1 new <point>)

; Access field
(record p1 x)                        ; => 0

; Test type
(record p1 ? <point>)                ; => #t

; Set fields
(record p1 ! x 10)
(record p1 ! (x 10) (y 20) ...)     ; multiple fields at once

; Call a method field (lambda stored in a field)
(record p1 ! act (lambda (v) (* v v)))
(record p1 call act 3)               ; => 9

; Get all values
(record p1)
```

### Multiple Values

```scheme
(values v ...)
(call-with-values producer consumer)
```

**Examples:**

```scheme
(call-with-values (lambda () (values 1 2)) +)    ; => 3
(call-with-values (lambda () 4) (lambda (x) x))  ; => 4
```

### Delay & Force

```scheme
(delay expr)             ; creates a promise  
(force promise)          ; evaluate and cache the promise
```

**Example:**

```scheme
(define p (delay (begin (display "computed!") 42)))
(force p)                ; prints "computed!" => 42
(force p)                ; => 42 (cached, no re-computation)
```

### Continuations

`call/cc` is implemented via `try`/`throw` and provides **local exit** (escape continuations):

```scheme
(call/cc (lambda (k) expr))
(let/cc k expr...)
```

**Examples:**

```scheme
(call/cc (lambda (k) (* 5 4)))          ; => 20
(call/cc (lambda (k) (* 5 (k 4))))      ; => 4
(* 2 (call/cc (lambda (k) (* 5 (k 4))))) ; => 8
```

> Full re-entrant continuations are not supported; these are escape-only continuations.

### Combinators

The SKI combinator basis is built in. Enable curried application with `(carry #t)` first:

```scheme
(carry #t)

(define I \x.x)
(define K \x.\y.x)
(define S \x.\y.\z.((x z)(y z)))

(define a 'a)
(((S K) K) a)   ; => a
```

### Unification

A full most-general-unifier (MGU) algorithm is available:

```scheme
(unify 'x 'y)                          ; => y
(unify '(f x y) '(g x y))             ; => "clash"
(unify '(f x (h)) '(f (h) y))         ; => (f (h) (h))
(unify '(f (g x) y) '(f y x))         ; => "cycle"
(unify '(f (g x) y) '(f y (g x)))     ; => (f (g x) (g x))
(unify '(f (g x) y) '(f y z))         ; => (f (g x) (g x))
```

### Set Comprehensions

The `set-of` macro provides list/set comprehension:

```scheme
(set-of expr clause ...)
```

Clause forms:

| Form | Meaning |
|------|---------|
| `(x in list)` | iterate `x` over `list` |
| `(x is expr)` | bind `x` to `expr` |
| `predicate` | filter: include only when true |

**Examples:**

```scheme
(set-of x (x in '(a b c)))                        ; => (a b c)
(set-of x (x in '(1 2 3 4)) (even? x))            ; => (2 4)
(set-of (cons x y)
        (x in '(4 2 3))
        (y is (* x x)))                            ; => ((4 16) (2 4) (3 9))
(set-of (cons x y)
        (x in '(a b))
        (y in '(1 2)))                             ; => ((a 1) (a 2) (b 1) (b 2))
```

---

## .NET Interoperability

Lisp can call any .NET method or access any field/property via four built-in primitives.
Type names are passed as quoted symbols.

### `(new 'TypeName arg ...)`

Instantiate a .NET type:

```scheme
(new 'System.Text.StringBuilder)
(new 'System.IO.StreamReader "file.txt")
(new 'System.Collections.ArrayList)
```

### `(call obj 'MethodName arg ...)`

Call an instance method:

```scheme
(call "hello" 'ToUpper)                           ; => "HELLO"
(call "hello world" 'IndexOf "world")             ; => 6
(call some-list 'Add 42)
```

### `(call-static 'TypeName 'MethodName arg ...)`

Call a static method:

```scheme
(call-static 'System.Math 'Sqrt 16.0)             ; => 4.0
(call-static 'System.String 'Format "{0}+{1}" 1 2) ; => "1+2"
(call-static 'System.Console 'WriteLine "hello")
(call-static 'System.IO.File 'Exists "test.txt")
```

### `(get obj-or-type 'PropertyOrField index ...)`

Get a property, field, or indexed item:

```scheme
(get "hello" 'Length)                             ; => 5
(get 'System.Math 'PI)                            ; => 3.14159...
(get some-list 'Count)
(get some-list 'Item 0)                           ; indexed access
```

### `(set obj-or-type 'PropertyOrField value)`

Set a property or field:

```scheme
(set 'Lisp.Interpreter 'EndProgram #t)           ; quit
(set 'Lisp.Expressions.App 'CarryOn #t)          ; enable carry mode
(set some-list 'Item 0 99)                        ; indexed set
```

### Practical Examples

```scheme
; Read a file
(define content
  (call (new 'System.IO.StreamReader "data.txt") 'ReadToEnd))

; Write a file
(define w (new 'System.IO.StreamWriter "out.txt"))
(call w 'WriteLine "Hello from Lisp")
(call w 'Close)

; Environment variable
(call-static 'System.Environment 'GetEnvironmentVariable "PATH")

; Date/time
(call (call-static 'System.DateTime 'get_Now) 'ToString)
```

### Type Loading

Custom assemblies can be loaded using the `@` syntax in type names:

```scheme
(get-type "MyType@path\\to\\MyAssembly.dll")
```

Types in the running assembly and all loaded assemblies are found automatically.

---

## Tracing & Debugging

Tracing prints evaluation steps to the console.

```scheme
(trace #t)               ; enable tracing globally
(trace #f)               ; disable tracing

(trace-all)              ; trace all symbols
(trace-add 'foo 'bar)    ; trace specific functions
(trace-remove 'foo)      ; stop tracing a function
(trace-clear)            ; clear all trace targets
```

Key built-in trace symbols:

| Symbol | What is traced |
|--------|--------------|
| `_all_` | everything |
| `lambda` | every lambda creation |
| `macro` | macro expansion result |
| `match` | macro pattern matches |
| any procedure name | calls to that procedure |

```scheme
; Example: trace fibonacci
(trace #t)
(trace-add 'fib)
(define (fib n) (if (< n 2) n (+ (fib (- n 1)) (fib (- n 2)))))
(fib 5)
```

---

## Introspection

```scheme
; Procedures
(procedure? x)           ; true if x is a defined closure
(closure? x)             ; true if x is a Lisp.Closure
(closure-args f)         ; argument list of closure f
(closure-body f)         ; body of closure f
(procedures->list)       ; list all defined procedures

; Macros
(macro? x)               ; true if x is a defined macro
(macro-body x)           ; macro clause list
(macro-const x)          ; macro literal list
(macros->list)           ; list all defined macros

; Symbols
(symbols->list)          ; list all interned symbols

; Utilities
(lastValue #f)           ; suppress printing intermediate results
(exit)                   ; terminate the interpreter
(LispVersion)           ; version string
(.NetVer)                ; .NET runtime version
(GACRoot)                ; .NET Framework root path
(Environment "VAR")      ; get environment variable
```

---

## Architecture

The interpreter is implemented in a single file, `Class1.cs`, structured as a set of nested
namespaces under `Lisp`:

| Class | Namespace | Role |
|-------|-----------|------|
| `Util` | `Lisp` | Parser, .NET reflection helpers, `Dump` printer |
| `Symbol` | `Lisp` | Interned symbol table (`Dictionary<string,Symbol>`) |
| `Pair` | `Lisp` | Linked list / cons cell (implements `ICollection`) |
| `Closure` | `Lisp` | First-class function value |
| `Arithmetic` | `Lisp` | Numeric dispatch (`int`/`float`/`double`) |
| `Macro` | `Lisp.Macros` | Pattern-based macro system |
| `Program` | `Lisp.Programs` | Top-level evaluator, global environment |
| `Env` / `Extended_Env` | `Lisp.Environment` | Lexical environment chain |
| `Expression` and subclasses | `Lisp.Expressions` | AST nodes: `Lit`, `Var`, `Lambda`, `Define`, `If`, `Try`, `App`, `Prim`, `Assignment`, `CommaAt`, `Evaluate` |
| `Prim` | `Lisp.Expressions` | Built-in primitives: `new`, `get`, `set`, `call`, `call-static`, `LESSTHAN` |
| `Interpreter` | `Lisp` | `Main` entry point and REPL loop |

### Evaluation Pipeline

```
Input string
     │
     ▼
 Util.Parse()          →  object tree (Pair / Symbol / literal)
     │
     ▼
 Macro.Check()         →  macro-expanded object tree
     │
     ▼
 Expression.Parse()    →  typed AST (Expression subclass tree)
     │
     ▼
 expr.Eval(env)        →  result object
     │
     ▼
 Util.Dump()           →  printed representation
```

### Number Types

| C# type | Scheme type | Literals |
|---------|-------------|---------|
| `System.Int32` | integer | `42`, `-7` |
| `System.Single` | real | `3.14`, `1.0` |
| `System.Double` | (intermediate) | — |

Arithmetic in `Lisp.Arithmetic` promotes through `int → float → double` as needed. Integer
division returns an integer when exact, otherwise a float.
