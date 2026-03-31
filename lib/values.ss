(call-static 'System.Console 'Write ", multipleValues")

;; --- Multiple return values (R7RS §6.10) ---
;;
;; (values v...) -- return zero or more values from a procedure.
;; A single value is returned as-is (not wrapped).
;; Zero or multiple values are packaged in a tagged pair so that
;; call-with-values can unpack them.
;; Example: (values 1 2 3) -- returns three values
;; Example: (values)       -- returns zero values
(define *multiple-values* (cons '*multiple-values* '()))
(define *values0* (cons *multiple-values* '()))

(define (values . vals)
  (cond ((null? vals)       *values0*)
	    ((null? (cdr vals)) (car vals))
	    (else               (cons *multiple-values* vals))))

;; (call-with-values producer consumer)
;;   Call (producer) to get zero or more values, then pass them all to consumer.
;;   Example: (call-with-values (lambda () (values 1 2)) +)      ==> 3
;;   Example: (call-with-values (lambda () (values 4 5)) list)   ==> (4 5)
;;   Example: (call-with-values (lambda () 42) (lambda (x) x))  ==> 42
(define (call-with-values producer consumer)
  (let ((vals (producer)))
    (if (and (pair? vals)
	     (eq? (car vals) *multiple-values*))
	(apply consumer (cdr vals))
	(consumer vals))))

;; --- Multiple value utilities ---

;; (partition pred lst) -- split lst into two lists using pred.
;; Returns (values matching not-matching) as multiple values.
;; Example: (call-with-values (lambda () (partition even? '(1 2 3 4 5)))
;;            list)  ==>  ((2 4) (1 3 5))
(define (partition pred lst)
  (let loop ((lst lst) (yes '()) (no '()))
    (cond ((null? lst)         (values (reverse yes) (reverse no)))
          ((pred (car lst))    (loop (cdr lst) (cons (car lst) yes) no))
          (else                (loop (cdr lst) yes (cons (car lst) no))))))

;; (span pred lst) -- split lst at the first element for which pred is false.
;; Returns (values prefix rest) where prefix is the longest leading segment satisfying pred.
;; Example: (call-with-values (lambda () (span even? '(2 4 1 6))) list) ==> ((2 4) (1 6))
(define (span pred lst)
  (define (span-loop lst acc)
    (if (or (null? lst) (not (pred (car lst))))
        (values (reverse acc) lst)
        (span-loop (cdr lst) (cons (car lst) acc))))
  (span-loop lst '()))

;; (break pred lst) -- like span but splits at the first element satisfying pred.
;; Example: (call-with-values (lambda () (break even? '(1 3 2 4))) list) ==> ((1 3) (2 4))
(define (break pred lst) (span (negate pred) lst))

;; (exact-integer-sqrt k) -- return (values s r) such that s^2 + r = k and s = floor(sqrt(k)).
;; Example: (call-with-values (lambda () (exact-integer-sqrt 14)) list) ==> (3 5)
;;          because 3^2 + 5 = 14
(define (exact-integer-sqrt k)
  (let* ((s (tointeger (floor (sqrt (inexact k)))))
         (r (- k (* s s))))
    (values s r)))

;; --- let-values / let*-values (R7RS §4.3.1) ---
;;
;; (let-values (((v...) expr) ...) body...)
;;   Evaluate each expr, which returns multiple values, and bind them to the
;;   corresponding variables v... in parallel before executing body.
;;   Example:
;;     (let-values (((q r) (exact-integer-sqrt 17)))
;;       (list q r))  ==> (4 1)
;;
;; (let*-values (((v...) expr) ...) body...)
;;   Like let-values but binds sequentially; each binding is visible to the next.
;;   Example:
;;     (let*-values (((a b) (values 1 2))
;;                   ((c)   (+ a b)))
;;       c)  ==> 3
(macro let-values ()
  ((_ () body...)                       (begin body...))
  ((_ (((v) expr) rest...) body...)     (let ((v expr)) (let-values (rest...) body...)))
  ((_ (((v...) expr) rest...) body...)  (call-with-values
                                          (lambda () expr)
                                          (lambda (v...) (let-values (rest...) body...)))))

(macro let*-values ()
  ((_ () body...)                       (begin body...))
  ((_ (((v) expr) rest...) body...)     (let ((v expr)) (let*-values (rest...) body...)))
  ((_ (((v...) expr) rest...) body...)  (call-with-values
                                          (lambda () expr)
                                          (lambda (v...) (let*-values (rest...) body...)))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Delay evaluation --- more work
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
