(call-static 'System.Console 'Write ", Utilities")

;; --- System / environment utilities ---

;; (Environment var) -- return the value of OS environment variable named var.
(define (Environment x) (call-static 'System.Environment 'GetEnvironmentVariable x))
;; (SysRoot) -- returns the Windows system root directory (e.g. "C:\Windows").
(define (SysRoot)       (Environment "systemroot"))
;; (LispVersion) -- returns the assembly version of the running Lisp interpreter.
(define (LispVersion)  (get (call (call-static 'System.Reflection.Assembly 'GetEntryAssembly) 'GetName) 'Version))
;; (.NetVer) -- returns the .NET runtime version as a string.
(define (.NetVer)       (call (get 'System.Environment 'Version ) 'ToString))
;; (GACRoot) -- returns the path to the .NET Framework GAC for the current runtime.
(define (GACRoot)       (string-append (SysRoot)
                                       "\\Microsoft.NET\\Framework\\v"
                                       (call (.NetVer) 'Substring 0 (call (.NetVer) 'LastIndexOf "."))
                                       "\\"))

;; --- Equality predicates ---

;; (eq? x y) -- #t if x and y are the exact same object (reference equality).
;; Use for symbols and booleans; not reliable for numbers or strings.
(define (eq? x y)       (call-static 'System.Object 'ReferenceEquals x y))

;; (eqv? x y) -- #t if x and y are equivalent values (calls .Equals).
;; Reliable for numbers, characters, booleans, and symbols.
(define (eqv? x y)      (call x 'Equals y))

;; (equal? x y) -- #t if x and y are structurally equal (deep comparison).
;; Recursively compares pairs, vectors, strings, characters, and numbers.
;; Example: (equal? '(1 2 3) '(1 2 3)) ==> #t
;; Example: (equal? "abc" "abc")        ==> #t
(define (equal? x y)
  (cond
    ((null? x)    (null? y))
    ((number? x)  (and (number? y) (eqv? (todouble x) (todouble y))))
    ((symbol? x)  (and (symbol? y) (eqv? x y)))
    ((pair? x)    (and (pair? y)
                       (equal? (car x) (car y))
                       (equal? (cdr x) (cdr y))))
    ((char? x)    (and (char? y) (char=? x y)))
    ((boolean? x) (and (boolean? y) (if x y (not y))))
    ((vector? x)  (and (vector? y) 
                       (equal? (vector->list x) (vector->list y)) ))
    ((string? x)  (and (string? y) (string=? x y)))
    (else         #f)))

;; = is an alias for equal? (supports structural, numeric, and string equality).
(define =               equal?)

;; (reduce fn base-value lst) -- right-fold lst with fn starting from base-value.
;; (reduce fn base '(a b c)) => (fn a (fn b (fn c base)))
;; Example: (reduce + 0 '(1 2 3 4)) ==> 10
;; Example: (reduce * 1 '(2 3 4 5)) ==> 120
(define (reduce fn base-value lis)
   (if (null? lis) base-value (fn (car lis) (reduce fn base-value (cdr lis)))))
;(reduce * 1 (2 3 4 5 6))
 
;; (COMPARE-ALL f lst...) -- applies f pairwise to adjacent elements in lst.
;; Returns #t only if f returns #t for every consecutive pair.
;; Used internally to implement variadic comparisons like (< 1 2 3).
(define (COMPARE-ALL f . lst)
    (let ((ans #t) )
       (do ((ans #t ans) 
            (x (map f (reverse (cdr (reverse lst))) (cdr lst) ) (cdr x)))
           ((null? x) ans)
           (set! ans (and ans (car x))) ) ))

;; (STRCMP? a b ignore-case?) -- compares strings a and b.
;; Returns negative/zero/positive like C's strcmp.
;; Used by string<?, string=?, char<?, etc.
(define (STRCMP? a b t) 
  (call-static 'System.String 'Compare (call a 'ToString) (call b 'ToString) t))

;; (CALLNATIVE f arg...) -- calls the C# Lisp.Arithmetic method named f.
;; Used internally for +, -, *, /, quotient, remainder, etc.
(define (CALLNATIVE f . a)
  (call-static 'Lisp.Arithmetic f ,@a))

;; (throw msg) -- raises a .NET exception with the given message string.
(define (throw msg)     (call-static 'Lisp.Util 'Throw msg))
;; (get-type name) -- returns the .NET System.Type object for a fully-qualified type name.
(define (get-type type) (call-static 'Lisp.Util 'GetType type))
 
;; (lastValue x) -- set the REPL's last returned value to x.
(define (lastValue x)   (set 'Lisp.Program 'lastValue x))

;; (colors x) -- enable (#t) or disable (#f) coloured console output.
(define (colors x)      (set 'Lisp.ConsoleOutput 'Enabled (if x #t #f)))
(define (color-output x) (colors x))
;; (pretty-print x) -- enable (#t) or disable (#f) pretty-printing of top-level results.
;; When enabled, long s-expressions are formatted with indentation across multiple lines.
(define (pretty-print x) (set 'Lisp.ConsoleOutput 'PrettyPrint (if x #t #f)))
;; (pp x) -- pretty-print x once without toggling the global flag.
(define (pp x) (consoleLine (call-static 'Lisp.Util 'PrettyPrint x)))
;; (disasm-verbose x) -- enable (#t) or disable (#f) verbose disassembly output.
(define (disasm-verbose x) (set 'Lisp.Vm 'DisassemblyVerbose (if x #t #f)))

;; (exit) -- terminate the interpreter / REPL session.
(define (exit)          (set 'Lisp.Program 'EndProgram #t))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Carry argument utilities - Lisp.App.AutoCurry
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", carry")

;; (carry x) -- enable (#t) or disable (#f) automatic currying of lambda expressions.
;; When enabled, a multi-argument lambda applied to fewer arguments than it expects
;; returns a partially-applied function rather than signalling an error.
;; Example with carry enabled:
;;   (\x.\y.\z.(+ x y z) 1 2 3) ==> ((LAMBDA (x) (LAMBDA (y) (LAMBDA (z) (+ x y z)))) 1 2 3)
(define (carry x)  (set 'Lisp.App 'AutoCurry (if x #t #f)))

; (carry #t)
; allow things like 
; (\x.\y.\z.(+ x y z) 1 2 3) ==> ((LAMBDA (x) (LAMBDA (y) (LAMBDA (z) (+ x y z)))) 1 2 3)
; there are some limitations

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Combinator calculus - must have (carry #t)
;; Classic SKI and BCKW combinators expressed in lambda notation.
;; Requires (carry #t) to be set so curried application works.
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", combinators")

;; I combinator (identity): (I x) ==> x
(define I \x.x)
;; K combinator (constant): (K x y) ==> x  -- returns the first of two arguments
(define K \x.\y.x)
;; S combinator (substitution): (S x y z) ==> ((x z)(y z))
;; Example: (((S K) K) a) ==> a
(define S \x.\y.\z.((x z)(y z)))
;; Y combinator (fixed point): (Y f) ==> (f (Y f))
;; Enables anonymous recursion.  Example: (Y (lambda (f) (lambda (n) (if (= n 0) 1 (* n (f (- n 1)))))))
(define Y \f.(\x.(f (x x))) \x.(f (x x))) 
;; B combinator (composition): (B x y z) ==> (x (y z))  -- equivalent to compose
(define B \x.\y.\z.(x (y z)))
;; C combinator (flip / swap): (C x y z) ==> ((x z) y)  -- swaps the last two args
(define C \x.\y.\z.((x z) y))
;; W combinator (duplication): (W x y) ==> ((x y) y)  -- supplies same arg twice
(define W \x.\y.((x y) y))

; (define a 'a)
; (((S K) K) a) ==> a
; (((S I) I) a) ==> a a  (not working correctly)
