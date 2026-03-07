# Lisp — A Scheme Interpreter for .NET

**Lisp** is a Scheme interpreter written in C#, targeting .NET 10. It implements a substantial
subset of R5RS/R7RS Scheme with a pattern-based macro system, a full standard library written
in Scheme itself (`init.ss`), and deep two-way .NET interoperability via reflection.

**Author:** Ilias H. Mavreas  
**Runtime:** .NET 10  

---

## Table of Contents

- [Lisp — A Scheme Interpreter for .NET](#lisp--a-scheme-interpreter-for-net)
  - [Table of Contents](#table-of-contents)
  - [Building \& Running](#building--running)
  - [REPL Usage](#repl-usage)
  - [Language Reference](#language-reference)
    - [Literals \& Data Types](#literals--data-types)
    - [Core Special Forms](#core-special-forms)
      - [`define`](#define)
      - [`lambda`](#lambda)
      - [`if` / `cond` / `case` / `when` / `unless`](#if--cond--case--when--unless)
      - [`let` / `let*` / `letrec`](#let--let--letrec)
      - [`begin`](#begin)
      - [`set!`](#set)
      - [`quote`](#quote)
      - [`do` loop](#do-loop)
      - [`apply` / `map` / `for-each`](#apply--map--for-each)
      - [`eval`](#eval)
      - [`rec`](#rec)
    - [Lambda Shorthand](#lambda-shorthand)
    - [Quasiquotation](#quasiquotation)
    - [Macros](#macros)
    - [Error Handling](#error-handling)
    - [Higher-Order List Functions](#higher-order-list-functions)
    - [Functional Combinators](#functional-combinators)
    - [Numbers](#numbers)
    - [Characters](#characters)
    - [Strings](#strings)
    - [Booleans \& Equality](#booleans--equality)
    - [Symbols](#symbols)
    - [Vectors](#vectors)
    - [Input / Output](#input--output)
    - [Records](#records)
    - [Multiple Values](#multiple-values)
    - [Delay \& Force](#delay--force)
    - [Continuations](#continuations)
    - [Combinators](#combinators)
    - [Unification](#unification)
    - [Set Comprehensions](#set-comprehensions)
  - [.NET Interoperability](#net-interoperability)
    - [`(new 'TypeName arg ...)`](#new-typename-arg-)
    - [`(call obj 'MethodName arg ...)`](#call-obj-methodname-arg-)
    - [`(call-static 'TypeName 'MethodName arg ...)`](#call-static-typename-methodname-arg-)
    - [`(get obj-or-type 'PropertyOrField index ...)`](#get-obj-or-type-propertyorfield-index-)
    - [`(set obj-or-type 'PropertyOrField value)`](#set-obj-or-type-propertyorfield-value)
    - [Practical Examples](#practical-examples)
    - [Type Loading](#type-loading)
  - [Tracing \& Debugging](#tracing--debugging)
  - [Performance Stats](#performance-stats)
  - [Introspection](#introspection)
    - [`env` — Environment Inspection](#env--environment-inspection)
  - [Interpreter Behaviour Notes](#interpreter-behaviour-notes)
  - [Architecture](#architecture)
    - [Evaluation Pipeline](#evaluation-pipeline)
    - [Number Types](#number-types)

---

## Building & Running

```
dotnet build
dotnet run
```

`init.ss` is automatically copied to the build output directory and loaded at startup.
To run a script file, pass it as an argument:

```
dotnet run test2.ss
```

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

| Literal | C# type | Example |
|---------|---------|---------|
| Integer | `System.Int32` | `42`, `-7` |
| Real | `System.Single` | `3.14`, `-0.5` |
| Boolean | `System.Boolean` | `#t`, `#f` |
| Character | `System.Char` | `#\a`, `#\space`, `#\newline` |
| String | `System.String` | `"hello\nworld"` |
| Symbol | `Lisp.Symbol` | `foo`, `+`, `my-var` |
| Pair / List | `Lisp.Pair` | `(1 2 3)`, `(a . b)` |
| Vector | `System.Collections.ArrayList` | `#(1 2 3)` |
| Empty list | `Lisp.Pair(null)` | `'()`, `nil` |
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
|--------|---------|
| `var` | bind any single form |
| `var...` | bind zero or more remaining forms |
| `(a b) ...` | destructure repeated pairs into `a...` and `b...` |
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

### Error Handling

```scheme
(try expr)                      ; evaluate, return '() on error
(try expr catch-expr)           ; return catch-expr on error
(try expr (begin handler...))

(throw "message")               ; raise an exception

; R7RS-compatible error procedure
(error msg)                     ; throw msg as a string
(error msg irritant ...)        ; throw "msg: irritant1 irritant2 ..."
```

**Example:**

```scheme
(try (/ 1 0) "division error")           ; => "division error"
(try (throw "oops") 'caught)             ; => caught
(try (error "bad value" 42) "caught!")   ; => "caught!"


---

## Standard Library

The library is loaded from `init.ss` at startup. The banner lists each module:

```
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
(set-car! val pair)
(set-cdr! val pair)

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
(adjoin e lst)           ; add e if not present
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

Integers are `System.Int32`; reals are `System.Single`.

```scheme
; Arithmetic
(+ a b ...)    (- a b ...)    (* a b ...)    (/ a b ...)
(neg a)                                       ; unary negation

; Comparison (variadic, chaining)
(< a b ...)   (<= a b ...)   (= a b ...)
(<> a b ...)  (>= a b ...)   (> a b ...)

; Predicates
(zero?     x)    (positive? x)   (negative? x)
(even?     x)    (odd?      x)
(exact?    x)    (inexact?  x)
(number?   x)    (integer?  x)
(real?     x)    ; #t for all numbers (integers are real per R5RS)
(complex?  x)    ; alias for number?
(rational? x)    ; alias for number?
(exact-integer? x)  ; (and (integer? x) (exact? x))
(finite?   x)    ; #t if not infinite and not NaN
(infinite? x)    ; #t if +Inf or -Inf
(nan?      x)    ; #t if NaN (not-a-number)

; Rounding / conversion
(floor x)      (ceiling x)    (round x)      (truncate x)
(exact->inexact n)             ; to System.Single
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
(string->number s)          ; auto-detect int or float
(string->number s radix)    ; parse integer in given radix
(string->integer s)         ; always parses as Int32
(string->real s)            ; always parses as Single

; Constants
PI     ; System.Math.PI
E      ; System.Math.E
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
(string->real    s)
(string->number  s)
(string->number  s radix)
(number->string  n)
(number->string  n radix)
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
(eqv?   x y)                    ; value equality (.Equals)
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
(load "file.ss")                ; load and evaluate a Scheme source file
(with-input-from-file "path" thunk) ; temporarily redirect *INPUT*

; String ports (in-memory I/O)
(open-input-string s)           ; create a StringReader from s
(open-output-string)            ; create a StringWriter (accumulates text)
(get-output-string port)        ; extract accumulated string from StringWriter
(string-port? x)                ; #t for StringReader or StringWriter

; Output
(current-output-port)
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

; Read a field
(record name field)

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
(record-name p)                  ; => <point>
(record-fields p)                ; => #(x y)
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

`call/cc` and `let/cc` are implemented via `try`/`throw` and support **escape
continuations** (local exit only — not re-entrant):

```scheme
(call/cc (lambda (k) body...))
(call-with-current-continuation (lambda (k) body...))  ; alias

(let/cc k body...)      ; binds k to the current escape continuation
```

Invoking `k` unwinds the computation and returns that value from the `call/cc` expression.

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
|-------------|---------|
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
|--------|--------|
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
(stats #t)               ; enable execution-time and iteration reporting
(stats #f)               ; disable
```

When enabled, after each top-level expression is evaluated the interpreter prints:

```
  time: <ms> ms  iterations: <n>
```

- **time** — wall-clock time of the evaluation in milliseconds.
- **iterations** — number of closure invocations (user-defined function calls, including
  every trampoline bounce for tail-recursive loops).

**Example:**

```scheme
(define (fib n) (if (< n 2) n (+ (fib (- n 1)) (fib (- n 2)))))

(stats #t)
(fib 30)
;   time: 2521.891 ms  iterations: 4,381,783
(stats #f)
```

---

## Introspection

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

; Symbols
(symbols->list)                  ; all currently interned symbols

; Environment inspection
(env)                            ; print all user-defined functions as (define ...) forms
(env 'name)                      ; print a single named function definition

; Utilities
(lastValue #f)                   ; suppress intermediate result printing
(stats #t)                       ; enable timing + iteration count per top-level eval
(stats #f)                       ; disable stats
(exit)                           ; terminate the interpreter
(LispVersion)                    ; interpreter version string
(.NetVer)                        ; .NET runtime version string
(GACRoot)                        ; .NET Framework root path
(Environment "VAR")              ; get an environment variable
```

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
|---------|----------------|-----------------|
| `inexact->exact` / `exact` | truncates toward zero | uses `System.Convert.ToInt32` — **rounds** (e.g. `(exact 3.9)` = `4`) |
| Symbol case | R5RS: fold to lower-case | **case-sensitive**: `'a` ≠ `'A` |
| `call/cc` | full re-entrant continuations | escape continuations only (local exit via `try`/`throw`) |
| `eq?` on `'()` | any two `'()` values are `eq?` | two separately-evaluated `'()` may not be `eq?`; use `null?` or `equal?` |
| `string` constructor | variadic char constructor `(string #\a #\b)` | broken for >0 args (right-fold bug); use `list->string` instead |
| `(gcd a b)` | `(gcd 0 4)` = `4` | argument order matters; `(gcd 4 0)` works, `(gcd 0 4)` may crash |
| `member` | uses `equal?` | uses `=`, which falls back to reference equality for non-numeric types |

---

## Architecture

The interpreter is implemented in `Program.cs` under the `Lisp` namespace:

| Class | Namespace | Role |
|-------|-----------|------|
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

| C# type | Scheme type | Notes |
|---------|-------------|-------|
| `System.Int32` | integer (exact) | `42`, `-7` |
| `System.Single` | real (inexact) | `3.14`, result of `exact->inexact` |
| `System.Double` | intermediate | used internally by `todouble` |

Arithmetic in `Lisp.Arithmetic` promotes `int → float → double` as needed. Integer division
returns an integer when exactly divisible, otherwise a float.
