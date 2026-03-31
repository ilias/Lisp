(call-static 'System.Console 'Write ", vectors")

;; Vectors are backed by System.Collections.ArrayList.

;; (list->vector lst) -- convert a list to a vector.
;; Example: (list->vector '(1 2 3)) ==> #(1 2 3)
(define (list->vector x)      (vector ,@x))

;; (make-vector n [fill]) -- create a vector of n elements initialized to fill (default 0).
;; Example: (make-vector 3 'x) ==> #(x x x)
(macro make-vector ()
 ((_ n)      (MAKE-VECTOR n 0))
 ((_ n a...) (MAKE-VECTOR n a...)))
(define (MAKE-VECTOR n . obj) 
   (do ((v '() v) (i 1 (+ i 1)) (o (if (null? obj) '() (car obj)) o))
       ((or (> i n) (null? o)) (list->vector v))
       (set! v (cons o v)))) 

;; (vector? x) -- #t if x is a vector (ArrayList).
(define (vector? x)           (= (call x 'GetType) (get-type "System.Collections.ArrayList")))
;; (vector x...) -- create a vector from the given elements.
;; Example: (vector 1 2 3) ==> #(1 2 3)
(define (vector . x)          (new 'System.Collections.ArrayList (call x 'ToArray)))
;; (vector-copy v) -- return a shallow copy of vector v.
(define (vector-copy v)       (list->vector (vector->list v)))
;; (vector-fill! v x) -- mutate every element of v to x; returns v.
(define (vector-fill! v x)
    (let ((n (vector-length v)))
      (do ((i 0 (+ i 1)))
          ((= i n) v)
          (vector-set! v i x)))) 
;; (vector-length v) -- return the number of elements in vector v.
(define (vector-length x)     (get x 'Count))
;; (vector-map f v) -- apply f to each element and return a new vector of results.
;; Example: (vector-map (lambda (x) (* x x)) (vector 1 2 3)) ==> #(1 4 9)
(define (vector-map f v)      (list->vector (map f (vector->list v))))
;; (vector-ref v i) -- return the element at zero-based index i.
(define (vector-ref v x)      (get v 'Item x))
;; (vector-set! v k obj) -- mutate index k in vector v to obj.
(define (vector-set! v k obj) (set v 'Item k obj))
;; (vector->list v) -- convert vector v to a list.
(define (vector->list x)      
  (define (other itm y)
    (if (= itm (vector-length y))
        '()
        (cons (vector-ref y itm) (other (inexact->exact (+ itm 1)) y))))
  (other 0 x))

;; (vector-union v1 v2) -- vector equivalent of union (no duplicates).
;; Example: (vector-union (vector 1 2 3) (vector 2 3 4)) ==> #(1 2 3 4)
(define (vector-union v1 v2) 
  (list->vector (union (vector->list v1) (vector->list v2))))
;; (vector-intersection v1 v2) -- elements present in both vectors.
(define (vector-intersection v1 v2) 
  (list->vector (intersection (vector->list v1) (vector->list v2))))
;; (vector-difference v1 v2) -- elements in v1 not in v2.
(define (vector-difference v1 v2) 
  (list->vector (difference (vector->list v1) (vector->list v2))))
;; (vector-for-each f v) -- call f on each element of v for side effects.
(define (vector-for-each f v)
  (let ((n (vector-length v)))
    (do ((i 0 (+ i 1))) ((= i n)) (f (vector-ref v i)))))
;; (vector-append v...) -- concatenate vectors into a new vector.
;; Example: (vector-append (vector 1 2) (vector 3 4)) ==> #(1 2 3 4)
(define (vector-append . vecs)
  (list->vector (apply append (map vector->list vecs))))
