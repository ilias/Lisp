(call-static 'System.Console 'Write ", random")

;; --- Pseudo-random number generation (backed by System.Random) ---

(define *random-gen*    (new 'System.Random))
;; (random-integer n) -- return a uniformly random integer in [0, n).
;; Example: (random-integer 6) ==> 0..5  (one side of a die)
(define (random-integer n)    (call *random-gen* 'Next 0 n))
;; (random-real) -- return a uniformly random double in [0.0, 1.0).
(define (random-real)         (call *random-gen* 'NextDouble))
;; (random n) -- return a random number in [0, n).
;; If n is exact, returns an integer; if inexact, returns a double.
;; Example: (random 10)   ==> integer in [0, 10)
;; Example: (random 1.0)  ==> double  in [0.0, 1.0)
(define (random n)            (if (exact? n) (random-integer n) (* (random-real) n)))
;; (random-seed! n) -- reset the random generator with seed n for reproducibility.
;; Example: (random-seed! 42) -- always produces the same sequence after this call
(define (random-seed! n)      (set! *random-gen* (new 'System.Random (inexact->exact n))))
;; (random-choice lst) -- return a uniformly random element from list lst.
;; Example: (random-choice '(rock paper scissors)) ==> one of the three
(define (random-choice lst)   (list-ref lst (random-integer (length lst))))
;; (random-shuffle lst) -- return a new list with the elements in a random order.
;; Uses a Fisher-Yates (Knuth) in-place shuffle on a temporary vector.
;; Example: (random-shuffle '(1 2 3 4 5)) ==> e.g. (3 1 5 2 4)
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
