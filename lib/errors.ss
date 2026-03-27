;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Error / Exception system (R7RS §6.11)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;; R7RS error: create an error object and raise it.
(define (error msg . irritants)
  (%raise (%make-error-object (call msg 'ToString) irritants)))

;; raise: raise any Scheme value as an exception.
(define (raise obj)             (%raise obj))
;; raise-continuable: same as raise in this implementation (continuable not supported).
(define (raise-continuable obj) (%raise obj))

;; with-exception-handler: install handler, call thunk;
;; on exception call handler with the raised value.
(define (with-exception-handler handler thunk)
  (%try-handler handler thunk))

;; guard macro: structured exception handling.
;; (guard (var clause ...) body...)
;; If body raises, bind var to the exception value and test each clause.
;; If no clause matches, re-raise the exception.
(macro guard ()
  ((_ (var clause...) body...)
   (call-with-current-continuation
     (lambda (?guard-k)
       (%try-handler
         (lambda (var)
           (?guard-k (cond clause... (else (raise var)))))
         (lambda () body...))))))



;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Records or structures -- defined as special vectors
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
