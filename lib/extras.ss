;; Missing string utilities (SRFI-13)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(define (string-prefix? prefix s)
  (let ((pl (string-length prefix)) (sl (string-length s)))
    (and (<= pl sl)
         (string=? prefix (substring s 0 pl)))))

(define (string-suffix? suffix s)
  (let ((pl (string-length suffix)) (sl (string-length s)))
    (and (<= pl sl)
         (string=? suffix (substring s (- sl pl) sl)))))

(define (string-pad s n . rest)
  (let* ((c (if (null? rest) #\  (car rest)))
         (l (string-length s)))
    (if (>= l n)
        (substring s (- l n) l)
        (string-append (make-string (- n l) c) s))))

(define (string-pad-right s n . rest)
  (let* ((c (if (null? rest) #\  (car rest)))
         (l (string-length s)))
    (if (>= l n)
        (substring s 0 n)
        (string-append s (make-string (- n l) c)))))

;; (string-replace s1 s2 start end) -- replace s1[start..end) with s2
(define (string-replace s1 s2 start end)
  (string-append (substring s1 0 start)
                 s2
                 (substring s1 end (string-length s1))))

; (string-prefix? "he" "hello")       ==> #t
; (string-suffix? "lo" "hello")       ==> #t
; (string-pad     "hi" 5)             ==> "   hi"
; (string-pad-right "hi" 5)           ==> "hi   "
; (string-replace "hello" "XY" 1 3)   ==> "hXYlo"

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; SRFI-1 list functions
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;; fold (SRFI-1): (fold f init lst) -- f is called (f elem acc), left-to-right
;; Note: differs from foldl where f is called (f acc elem)
(define (fold f init lst)
  (if (null? lst) init (fold f (f (car lst) init) (cdr lst))))

;; fold-right (SRFI-1): same signature as existing foldr
(define fold-right foldr)

;; unfold: (unfold pred f g seed [tail-gen])
;; pred  -- stop when true; f -- element from seed; g -- next seed
(define (unfold pred f g seed . rest)
  (let ((tail-gen (if (null? rest) (lambda (x) '()) (car rest))))
    (let loop ((seed seed))
      (if (pred seed)
          (tail-gen seed)
          (cons (f seed) (loop (g seed)))))))

;; unfold-right: builds list in reverse order
(define (unfold-right pred f g seed . rest)
  (let ((tail (if (null? rest) '() (car rest))))
    (let loop ((seed seed) (acc tail))
      (if (pred seed)
          acc
          (loop (g seed) (cons (f seed) acc))))))

;; list-index: returns index of first element satisfying pred, or #f
(define (list-index pred lst)
  (let loop ((lst lst) (i 0))
    (cond ((null? lst)      #f)
          ((pred (car lst)) i)
          (else             (loop (cdr lst) (+ i 1))))))

;; delete: remove all elements equal to x (equal? by default)
(define (delete x lst . rest)
  (let ((cmp (if (null? rest) equal? (car rest))))
    (filter (lambda (e) (not (cmp e x))) lst)))

;; lset operations (SRFI-1) -- eq is a two-argument equality predicate
(define (lset-adjoin eq lst . elts)
  (fold (lambda (elt acc)
          (if (any (lambda (x) (eq elt x)) acc) acc (cons elt acc)))
        lst elts))

(define (lset-union eq . lists)
  (if (null? lists)
      '()
      (fold (lambda (lst acc)
              (fold (lambda (elt acc)
                      (if (any (lambda (x) (eq elt x)) acc) acc (cons elt acc)))
                    acc lst))
            (car lists) (cdr lists))))

(define (lset-intersection eq lst1 . rest)
  (fold (lambda (lst result)
          (filter (lambda (x) (any (lambda (e) (eq x e)) lst)) result))
        lst1 rest))

(define (lset-difference eq lst . rest)
  (fold (lambda (to-remove acc)
          (filter (lambda (x) (not (any (lambda (e) (eq x e)) to-remove))) acc))
        lst rest))

; (fold + 0 '(1 2 3 4))                                  ==> 10
; (unfold (lambda (x) (= x 5)) identity (lambda (x) (+ x 1)) 0)  ==> (0 1 2 3 4)
; (list-index even? '(3 1 4 1 5))                         ==> 2
; (delete 3 '(1 2 3 4 3 5))                               ==> (1 2 4 5)
; (lset-union equal? '(a b c) '(b c d))                   ==> (a b c d)

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; receive (SRFI-8) -- bind multiple return values
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(macro receive ()
  ((_ () expr body...)           (begin expr body...))
  ((_ (v1) expr body...)         (call-with-values (lambda () expr) (lambda (v1)      body...)))
  ((_ (v1 v...) expr body...)    (call-with-values (lambda () expr) (lambda (v1 v...) body...)))
  ((_ rest-var expr body...)     (call-with-values (lambda () expr) (lambda rest-var  body...))))

; (receive (q r) (exact-integer-sqrt 17) (list q r))  ==> (4 1)
; (receive (a b c) (values 1 2 3) (+ a b c))          ==> 6
; (receive all (values 1 2 3) all)                     ==> (1 2 3)

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; cut / cute (SRFI-26) -- partial application with slots
;; <> marks a slot filled by the final caller's argument.
;; Implemented as a runtime function.  The slot marker is the
;; numeric <> procedure itself (checked by identity via eq?),
;; so the numeric <> operator is completely unaffected: users
;; simply write <> in the cut arg list and the procedure identity
;; is used to distinguish slots from fixed arguments.
;; cute is identical to cut under strict (applicative-order) evaluation.
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(define (cut proc . args)
  (let ((slot <>))   ; capture <> (the not-equal procedure) as the sentinel
    (lambda rest-args
      (let loop ((args args) (provided rest-args) (result '()))
        (cond ((null? args)
               (apply proc (reverse result)))
              ((eq? (car args) slot)
               (if (null? provided)
                   (error "cut: not enough arguments provided")
                   (loop (cdr args) (cdr provided) (cons (car provided) result))))
              (else
               (loop (cdr args) provided (cons (car args) result))))))))

(define cute cut)   ; identical under strict evaluation

; (map (cut + <> 1)       '(1 2 3))         ==> (2 3 4)
; (map (cut list 'x <> 'z) '(a b c))        ==> ((x a z) (x b z) (x c z))
; ((cut list 1 <> 3) 2)                     ==> (1 2 3)
; (map (cut * 2 <>) '(1 2 3))               ==> (2 4 6)

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; fluid-let -- temporarily rebind top-level variables
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(macro fluid-let ()
  ((_ () body...)              (begin body...))
  ((_ ((v e) rest...) body...) (let ((?old v))
                                  (dynamic-wind
                                    (lambda () (set! v e))
                                    (lambda () (fluid-let (rest...) body...))
                                    (lambda () (set! v ?old))))))

;; Note: with dynamic-wind, fluid-let now correctly restores on exceptions and
;; call/cc escape continuations, unlike the previous let/set!/restore approach.

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; while / until -- imperative loop constructs
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(macro while ()
  ((_ pred body...)   ((lambda ()
                         (define (?loop)
                           (when pred
                             body...
                             (?loop)))
                         (?loop)))))

(macro until ()
  ((_ pred body...)   ((lambda ()
                         (define (?loop)
                           (unless pred
                             body...
                             (?loop)))
                         (?loop)))))

; (let ((i 0) (s 0))
;   (while (< i 5)
;     (set! s (+ s i))
;     (set! i (+ i 1)))
;   s)                    ==> 10

; (let ((i 0))
;   (until (= i 3)
;     (display i)
;     (set! i (+ i 1))))  ==> displays 012

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Load - external functions and macros
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

