(call-static 'System.Console 'Write ", Unification")

;; --- Unification (Robinson's algorithm) ---
;;
;; (unify u v) -- attempt to unify two term trees u and v.
;; Terms are either symbols (logic variables) or lists whose car is a functor name.
;; On success returns the substituted form of u; on failure returns an error string.
;;
;; Examples:
;;   (unify 'x 'y)                      ==>  y
;;   (unify '(f x y) '(g x y))          ==>  "clash"    (different functors)
;;   (unify '(f x (h)) '(f (h) y))      ==>  (f (h) (h))
;;   (unify '(f (g x) y) '(f y x))      ==>  "cycle"   (x occurs in y)
;;   (unify '(f (g x) y) '(f y (g x)))  ==>  (f (g x) (g x))
;;   (unify '(f (g x) y) '(f y z))      ==>  (f (g x) (g x))
(define unify #f)
(let ()
  ;; occurs? returns true if and only if u occurs in v
  (define occurs?
    (lambda (u v)
      (and (pair? v)
           (let f ((l (cdr v)))
             (and (pair? l)
                  (or (eq? u (car l))
                      (occurs? u (car l))
                      (f (cdr l)))))))) 

  ;; sigma returns a new substitution procedure extending s by
  ;; the substitution of u with v
  (define sigma
    (lambda (u v s)
      (lambda (x)
        (let f ((x (s x)))
          (if (symbol? x)
              (if (eq? x u) v x)
              (cons (car x) (map f (cdr x)))))))) 

  ;; try-subst tries to substitute u for v but may require a
  ;; full unification if (s u) is not a variable, and it may
  ;; fail if it sees that u occurs in v.
  (define try-subst
    (lambda (u v s ks kf)
      (let ((u (s u)))
        (if (not (symbol? u))
            (uni u v s ks kf)
            (let ((v (s v)))
              (cond
                ((eq? u v) (ks s))
                ((occurs? u v) (kf "cycle"))
                (else (ks (sigma u v s))))))))) 

  ;; uni attempts to unify u and v with a continuation-passing
  ;; style that returns a substitution to the success argument
  ;; ks or an error message to the failure argument kf.  The
  ;; substitution itself is represented by a procedure from
  ;; variables to terms.
  (define uni
    (lambda (u v s ks kf)
      (cond
        ((symbol? u) (try-subst u v s ks kf))
        ((symbol? v) (try-subst v u s ks kf))
        ((and (eq? (car u) (car v))
              (= (length u) (length v)))
         (let f ((u (cdr u)) (v (cdr v)) (s s))
           (if (null? u)
               (ks s)
               (uni (car u)
                    (car v)
                    s
                    (lambda (s) (f (cdr u) (cdr v) s))
                    kf))))
        (else (kf "clash"))))) 

  ;; unify shows one possible interface to uni, where the initial
  ;; substitution is the identity procedure, the initial success
  ;; continuation returns the unified term, and the initial failure
  ;; continuation returns the error message.
  (set! unify
    (lambda (u v)
      (uni u
           v
           (lambda (x) x)
           (lambda (s) (s u))
           (lambda (msg) msg))))) 

;(unify 'x 'y)						==>  y
;(unify '(f x y) '(g x y))			==>  "clash"
;(unify '(f x (h)) '(f (h) y))		==>  (f (h) (h))
;(unify '(f (g x) y) '(f y x))		==>  "cycle"
;(unify '(f (g x) y) '(f y (g x)))	==>  (f (g x) (g x))
;(unify '(f (g x) y) '(f y z))		==>  (f (g x) (g x)) 

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; A Set Constructor
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; (set-of expr clause ...) -- comprehension macro that builds a set (duplicate-free list).
;; Clauses:
;;   (x in s)   -- range: iterate x over the list s
;;   (x is e)   -- binding: let x = e
;;   pred       -- guard: include element only when pred is true
;;
;; (set-cons x y) -- prepend x to y only if x is not already in y.
;;
;; Examples:
;;   (set-of x (x in '(a b c)))              ==> (a b c)
;;   (set-of x (x in '(1 2 3 4)) (even? x))  ==> (2 4)
;;   (set-of (cons x y)
;;           (x in '(4 2 3))
;;           (y is (* x x)))                  ==> ((4 16) (2 4) (3 9))

(call-static 'System.Console 'Write ", sets")

(macro set-of ()
  ((_ e m...) (set-of-help e '() m...)))
  
(macro set-of-help (in is)
  ((_ e base)               (set-cons e base))
  ((_ e base (x in s) m...) (let ?loop ((?set s))
                              (if (null? ?set)
                                  base
                                  (let ((x (car ?set)))
                                    (set-of-help e (?loop (cdr ?set)) m...)))))
  ((_ e base (x is y) m...) (let ((x y)) (set-of-help e base m...)))
  ((_ e base p m...)        (if p (set-of-help e base m...) base)))

(define set-cons (lambda (x y) (if (member x y) y (cons x y))))
      
; (set-of x (x in '(a b c)))     ==> (a b c)
; (set-of x (x in '(1 2 3 4))
;           (even? x))           ==> (2 4)
; (set-of (cons x y)
;         (x in '(4 2 3))
;         (y is (* x x)))        ==> ((4 16) (2 4) (3 9))
; (set-of (cons x y)
;         (x in '(a b))
;         (y in '(1 2)))         ==> ((a 1) (a 2) (b 1) (b 2))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Range
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(define (range start end)
  (do ((i end (- i 1))
       (acc '() (cons i acc)))
      ((< i start) acc)))

; (range 1 10)  ==>  (1 2 3 4 5 6 7 8 9 10)
; (range 3 6)   ==>  (3 4 5 6)

