(call-static 'System.Console 'Write ", multipleValues")

(define *multiple-values* (cons '*multiple-values* '()))
(define *values0* (cons *multiple-values* '()))

(define (values . vals)
  (cond ((null? vals)       *values0*)
	    ((null? (cdr vals)) (car vals))
	    (else               (cons *multiple-values* vals))))

(define (call-with-values producer consumer)
  (let ((vals (producer)))
    (if (and (pair? vals)
	     (eq? (car vals) *multiple-values*))
	(apply consumer (cdr vals))
	(consumer vals))))
	
; (call-with-values (lambda () (values 1 2)) +)   ==> 3
; (call-with-values values (lambda args args))    ==> ()
; (+ (values 2) 4)                                ==> 6
; (if (values #f) 1 2)                            ==> 2
; (call-with-values (lambda () 4)(lambda (x) x))  ==> 4

;; --- Multiple value utilities (require values to be defined) ---
(define (partition pred lst)
  (let loop ((lst lst) (yes '()) (no '()))
    (cond ((null? lst)         (values (reverse yes) (reverse no)))
          ((pred (car lst))    (loop (cdr lst) (cons (car lst) yes) no))
          (else                (loop (cdr lst) yes (cons (car lst) no))))))
(define (span pred lst)
  (define (span-loop lst acc)
    (if (or (null? lst) (not (pred (car lst))))
        (values (reverse acc) lst)
        (span-loop (cdr lst) (cons (car lst) acc))))
  (span-loop lst '()))
(define (break pred lst) (span (negate pred) lst))
(define (exact-integer-sqrt k)
  (let* ((s (tointeger (floor (sqrt (inexact k)))))
         (r (- k (* s s))))
    (values s r)))

;; --- let-values / let*-values ---
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
