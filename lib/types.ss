(call-static 'System.Console 'Write ", boolean")

(define (boolean? n)   (= (call n 'GetType) (get-type "System.Boolean")))
(define (boolean=? . l) (COMPARE-ALL (lambda (a b) (eqv? a b)) ,@l))
(define (not x)        (if x #f #t))
(define (xor x y)      (CALLNATIVE 'XorObj x y))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Symbols -- c# type == Lisp.Symbol
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", symbol")

(define (symbol? x)        (= (call x 'GetType) (get-type "Lisp.Symbol")))
(define (symbol->string s) (call s 'ToString))
(define (symbol-generate)  (call-static 'Lisp.Symbol 'GenSym))
(define (string->symbol s) (call-static 'Lisp.Symbol 'Create s))
(define (symbol=? s1 . rest) (for-all (lambda (s) (eq? s s1)) rest))
(define (symbols->vector)
  (new 'System.Collections.ArrayList (get (get 'Lisp.Symbol 'syms) 'Keys)))
(define (symbols->list)    (vector->list (symbols->vector)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Numbers -- c# type == System.Int32, System.Double, or System.Numerics.BigInteger
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

