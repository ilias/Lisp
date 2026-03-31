(call-static 'System.Console 'Write ", boolean")

;; --- Boolean type ---

;; (boolean? n) -- #t if n is #t or #f (System.Boolean).
(define (boolean? n)   (= (call n 'GetType) (get-type "System.Boolean")))
;; (boolean=? b ...) -- #t if all boolean arguments are equal (all #t or all #f).
(define (boolean=? . l) (COMPARE-ALL (lambda (a b) (eqv? a b)) ,@l))
;; (not x) -- logical negation; returns #f if x is truthy, #t if x is #f.
(define (not x)        (if x #f #t))
;; (xor x y) -- exclusive-or of two boolean (or integer) values.
(define (xor x y)      (CALLNATIVE 'XorObj x y))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Symbols -- c# type == Lisp.Symbol
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", symbol")

;; (symbol? x) -- #t if x is a Lisp symbol.
(define (symbol? x)        (= (call x 'GetType) (get-type "Lisp.Symbol")))
;; (symbol->string s) -- return the name of symbol s as a string.
;; Example: (symbol->string 'hello) ==> "hello"
(define (symbol->string s) (call s 'ToString))
;; (symbol-generate) -- create a fresh unique symbol (gensym).
(define (symbol-generate)  (call-static 'Lisp.Symbol 'GenSym))
;; (string->symbol s) -- intern string s as a symbol.
;; Example: (string->symbol "foo") ==> foo
(define (string->symbol s) (call-static 'Lisp.Symbol 'Create s))
;; (symbol=? s1 s2 ...) -- #t if all given symbols are the same (by identity).
(define (symbol=? s1 . rest) (for-all (lambda (s) (eq? s s1)) rest))
;; (symbols->vector) / (symbols->list) -- return all interned symbols.
(define (symbols->vector)
  (new 'System.Collections.ArrayList (get (get 'Lisp.Symbol 'syms) 'Keys)))
(define (symbols->list)    (vector->list (symbols->vector)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Numbers -- c# type == System.Int32, System.Double, or System.Numerics.BigInteger
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

