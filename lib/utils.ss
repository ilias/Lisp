(call-static 'System.Console 'Write ", Utilities")

(define (Environment x) (call-static 'System.Environment 'GetEnvironmentVariable x))
(define (SysRoot)       (Environment "systemroot"))
(define (LispVersion)  (get (call (call-static 'System.Reflection.Assembly 'GetEntryAssembly) 'GetName) 'Version))
(define (.NetVer)       (call (get 'System.Environment 'Version ) 'ToString))
(define (GACRoot)       (string-append (SysRoot)
                                       "\\Microsoft.NET\\Framework\\v"
                                       (call (.NetVer) 'Substring 0 (call (.NetVer) 'LastIndexOf "."))
                                       "\\"))

(define (eq? x y)       (call-static 'System.Object 'ReferenceEquals x y))
(define (eqv? x y)      (call x 'Equals y))
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
(define =               equal?)
(define (reduce fn base-value lis)
   (if (null? lis) base-value (fn (car lis) (reduce fn base-value (cdr lis)))))
;(reduce * 1 (2 3 4 5 6))
 
(define (COMPARE-ALL f . lst)
    (let ((ans #t) )
       (do ((ans #t ans) 
            (x (map f (reverse (cdr (reverse lst))) (cdr lst) ) (cdr x)))
           ((null? x) ans)
           (set! ans (and ans (car x))) ) ))
           
(define (STRCMP? a b t) 
  (call-static 'System.String 'Compare (call a 'ToString) (call b 'ToString) t))

(define (CALLNATIVE f . a)
  (call-static 'Lisp.Arithmetic f ,@a))
                 
(define (throw msg)     (call-static 'Lisp.Util 'Throw msg))
(define (get-type type) (call-static 'Lisp.Util 'GetType type))
 
(define (lastValue x)   (set 'Lisp.Program 'lastValue x))

(define (colors x)      (set 'Lisp.ConsoleOutput 'Enabled (if x #t #f)))
(define (color-output x) (colors x))
(define (disasm-verbose x) (set 'Lisp.Vm 'DisassemblyVerbose (if x #t #f)))

(define (exit)          (set 'Lisp.Interpreter 'EndProgram #t))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Carry argument utilities - Lisp.App.CarryOn
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", carry")

(define (carry x)  (set 'Lisp.App 'CarryOn (if x #t #f)))

; (carry #t)
; allow things like 
; (\x.\y.\z.(+ x y z) 1 2 3) ==> ((LAMBDA (x) (LAMBDA (y) (LAMBDA (z) (+ x y z)))) 1 2 3)
; there are some limitations

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Combinator calculus - must hace (carry #t)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", combinators")

(define I \x.x)
(define K \x.\y.x)
(define S \x.\y.\z.((x z)(y z)))
(define Y \f.(\x.(f (x x))) \x.(f (x x))) 
(define B \x.\y.\z.(x (y z)))
(define C \x.\y.\z.((x z) y))
(define W \x.\y.((x y) y))

; (define a 'a)
; (((S K) K) a) ==> a
; (((S I) I) a) ==> a a  (not working correctly)

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Trace utility -- Lisp.Expression.traceHash with Lisp.Symbol keys
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

