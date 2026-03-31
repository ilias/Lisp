(call-static 'System.Console 'Write ", parameters")

;; --- Parameter objects (SRFI-39) ---
;;
;; A parameter object is a procedure encapsulating a mutable dynamic cell.
;; The cell holds a "current value" that can be temporarily rebound via parameterize.
;;
;; (make-parameter val [converter])
;;   Create a parameter whose initial value is (converter val), or simply val
;;   if no converter is supplied.  The converter is also applied on every update.
;;   Example: (define p (make-parameter 10))
;;            (p)         ==> 10
;;            (p 20)      ==> sets p to 20
;;            (p)         ==> 20
;;
;; (make-parameter val converter) with a converter that enforces a type:
;;   (define nat (make-parameter 0 (lambda (n) (if (< n 0) 0 n))))
;;   (nat -5)  ;; silently clamps to 0
;;   (nat)     ==> 0
(define (make-parameter val . rest)
  (let* ((conv    (if (null? rest) (lambda (x) x) (car rest)))
         (current (conv val)))
    (lambda args
      (if (null? args)
          current
          (set! current (conv (car args)))))))

;; (parameterize ((p v) ...) body...)
;;   Temporarily bind each parameter p to (converted) value v while body executes.
;;   The original values are restored after body returns (or raises an exception).
;;   Uses dynamic-wind so the restoration happens even on continuations.
;;   Example:
;;     (define current-indent (make-parameter 0))
;;     (parameterize ((current-indent 4))
;;       (current-indent))  ==> 4
;;     (current-indent)     ==> 0  (restored)
(macro parameterize ()
  ((_ () body...)               (begin body...))
  ((_ ((p v) rest...) body...)  (let* ((?p   p)
                                       (?old (?p)))
                                   (dynamic-wind
                                     (lambda () (?p v))
                                     (lambda () (parameterize (rest...) body...))
                                     (lambda () (?p ?old))))))

; (define current-indent (make-parameter 0))
; (current-indent)                         ==> 0
; (parameterize ((current-indent 4))
;   (current-indent))                      ==> 4
; (current-indent)                         ==> 0  (restored)

