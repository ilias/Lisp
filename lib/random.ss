(call-static 'System.Console 'Write ", random")

(define *random-gen*    (new 'System.Random))
(define (random-integer n)    (call *random-gen* 'Next 0 n))
(define (random-real)         (call *random-gen* 'NextDouble))
(define (random n)            (if (exact? n) (random-integer n) (* (random-real) n)))
(define (random-seed! n)      (set! *random-gen* (new 'System.Random (inexact->exact n))))
(define (random-choice lst)   (list-ref lst (random-integer (length lst))))
(define (random-shuffle lst)
  (let ((v (list->vector lst)))
    (do ((i (- (vector-length v) 1) (- i 1)))
        ((= i 0) (vector->list v))
        (let* ((j  (call *random-gen* 'Next 0 (+ i 1)))
               (tmp (vector-ref v i)))
          (vector-set! v i (vector-ref v j))
          (vector-set! v j tmp)))))

; (random-integer 10)    ==> integer in [0, 10)
; (random-real)          ==> double  in [0, 1)
; (random 5)             ==> integer in [0, 5)
; (random-choice '(a b c d))   ==> one of a b c d
; (random-shuffle '(1 2 3 4 5)) ==> shuffled list

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
