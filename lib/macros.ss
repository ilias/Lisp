;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; General macros and conditional evaluation
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; macro  ::= (macro <name> (<literals> ...) clause ...)
;; clause ::= (pattern template)
;; ?var   ::= new unique symbol
;; var... ::= get rest of values 
;;            example: (a b c...)  with (1 2 3 4 5)
;;                     a    = 1
;;                     b    = 2
;;                     c... =  (3 4 5)
;; (pair) ... ::= rest of values
;;                example: (a b) ...  with ((1 2) (3 4) (5 6))
;;                         a... = (1 3 5)
;;                         b... = (2 4 6)

(call-static 'System.Console 'Write " generics")

;; (lambda formals body...) -- create an anonymous function.
;; Formals may be: ()  (no args), (a1 a2 ...) (fixed args), or a single symbol (variadic).
;; Example: ((lambda (x y) (+ x y)) 3 4) ==> 7
;; Example: ((lambda args args) 1 2 3)   ==> (1 2 3)
(macro lambda ()
  ((_ () b...)                 (LAMBDA () b...))
  ((_ (a1) b...)               (LAMBDA (a1) b...))
  ((_ (a1 a...) b...)          (LAMBDA (a1 a...) b...))
  ((_ a b...)                  (LAMBDA (. a) b...)))

;; (define name val) / (define (name args...) body...) -- bind name in the current scope.
;; Example: (define (square x) (* x x))
;; Example: (define pi 3.14159)
(macro define ()
  ((_ (n) b...)                (DEFINE n (lambda () b...)))
  ((_ (n v...) b...)           (DEFINE n (lambda (v...) b...)))
  ((_ n b...)                  (DEFINE n b...)))

;; (if test then [else...]) -- conditional evaluation.
;; (if test)              -- returns test's value or #f
;; (if test then)         -- returns then or #f
;; (if test then else...) -- standard two-branch conditional
(macro if ()
  ((_)                         #f)
  ((_ t)                       (let ((?x t)) (IF ?x ?x #f)))
  ((_ t then)                  (IF t then #f))
  ((_ t then else...)          (IF t then (begin else...))))

;; (cond clause...) -- multi-branch conditional.
;; Clause forms: (test) (test body...) (test => proc) (else body...)
;; Example: (cond ((< x 0) 'negative) ((= x 0) 'zero) (else 'positive))
(macro cond (else =>)
  ((_)                         '())
  ((_ (t) m...)                (let ((?x t)) (if ?x ?x (cond m...))))
  ((_ (else v...) m...)        (begin v...))
  ((_ (t => e) m...)           (let ((?x t)) (if ?x (e ?x) (cond m...))))
  ((_ (t v...) m...)           (if t (begin v...) (cond m...))))

;; (or expr...) -- return the first truthy value, or #f if all are false.
;; Short-circuits: stops evaluating after the first truthy result.
;; Example: (or #f #f 42) ==> 42
(macro or ()
  ((_)                         #f)
  ((_ e)                       e)
  ((_ e e1...)                 (let ((?t e)) (if ?t ?t (or e1...)))))

;; (and expr...) -- return the last value if all are truthy, or #f on first false.
;; Short-circuits: stops evaluating after the first false result.
;; Example: (and 1 2 3) ==> 3  ;  (and 1 #f 3) ==> #f
(macro and ()
  ((_)                         #t)
  ((_ e)                       e)
  ((_ e e1...)                 (if e (and e1...) #f)))

;; (case key clause...) -- select a branch by matching key against literal lists.
;; Example: (case (* 2 3) ((2 3 5 7) 'prime) ((1 4 6 8 9) 'composite))  ==> composite
(macro case (else)
  ((_ key )                    key)
  ((_ key (else b...) m...)    (begin b...))
  ((_ key (v b...) m...)       (if (pair? (member key 'v)) (begin b...) (case key m...) )))

;; (let ((var val)...) body...) -- bind variables for the duration of body.
;; Named let: (let name ((var val)...) body...) creates a local loop.
;; Example: (let ((x 1) (y 2)) (+ x y)) ==> 3
;; Example: (let loop ((n 5) (acc 1)) (if (= n 0) acc (loop (- n 1) (* acc n)))) ==> 120
(macro let ()
  ((_ () b...)                 (begin b...))
  ((_ ((n1 v1) ...) b...)      ((lambda (n1...) b...) v1...))
  ((_ name ((n1 v1) ...) b...) ((lambda () (define (name n1...) b...) (name v1...)))))

;; (let* ((var val)...) body...) -- like let but bindings are sequential (left-to-right).
;; Each binding is visible to subsequent init expressions.
;; Example: (let* ((x 1) (y (+ x 1))) y) ==> 2
(macro let* ()
  ((_ () b...)                 (begin b...))
  ((_ ((n1 v1)) b...)          ((lambda (n1) b...) v1))
  ((_ ((n1 v1) v...) b...)     ((lambda (n1) (let* (v...) b...)) v1)))

;; (letrec ((var val)...) body...) -- mutually recursive bindings.
;; All variables are visible in all init expressions (use for mutual recursion).
;; Example:
;;   (letrec ((even? (lambda (n) (if (= n 0) #t (odd? (- n 1)))))
;;            (odd?  (lambda (n) (if (= n 0) #f (even? (- n 1))))))
;;     (even? 10))  ==> #t
(macro letrec ()
  ((_ () b...)                 (begin b...))
  ((_ ((n1 v1) ...) b...)      ((lambda () (define n1... v1...) ... b...))))

;; letrec* -- like letrec but binds left-to-right (R7RS 4.2.2)
;; Each binding is visible to subsequent init expressions.
(macro letrec* ()
  ((_ () b...)                 (begin b...))
  ((_ ((n1 v1)) b...)          (let ((n1 v1)) b...))
  ((_ ((n1 v1) rest...) b...)  (let ((n1 v1)) (letrec* (rest...) b...))))

;; (case-lambda clause...) -- lambda with multiple arities (R7RS 4.2.9).
;; Each clause is (formals body...) matched by argument count.
;; Example:
;;   (define f
;;     (case-lambda
;;       ((x)   (* x x))
;;       ((x y) (+ x y))))
;;   (f 3)   ==> 9
;;   (f 3 4) ==> 7
;; Recursive macro: each expansion handles one clause and recurses on the rest.
;; Formals can be: ()   → zero-arg (matched when (null? args))
;;                 (x)  → fixed arity  (matched when (= (length args) arity))
;;                 args → variadic symbol (always matches)
(macro case-lambda ()
  ((_)
   (lambda ?args (error "case-lambda: no matching clause" (length ?args))))
  ((_ (f b...) rest...)
   (let ((?proc (lambda f b...)))
     (let ((?next (case-lambda rest...)))
       (lambda ?args
         (if (if (null? 'f)
                 (null? ?args)
                 (if (pair? 'f)
                     (= (length ?args) (length 'f))
                     #t))
             (apply ?proc ?args)
             (apply ?next ?args)))))))

;; (begin expr...) -- evaluate expressions in sequence; return value of last.
(macro begin ()
  ((_)                         '())
  ((_ b)                       b)
  ((_ b...)                    ((lambda () b...))))

;; (when pred body...) -- evaluate body only when pred is true; return value of last.
;; Example: (when (> x 0) (display "positive") x)
(macro when ()
  ((_ pred body...)            (and pred (begin body...))))

;; (unless pred body...) -- evaluate body only when pred is false.
;; Example: (unless (null? lst) (car lst))
(macro unless ()
  ((_ pred body...)            (or pred (begin body...))))

;; (try expr [catch...]) -- evaluate expr; if an exception is raised, evaluate catch.
;; Example: (try (/ 1 0) "division error")
(macro try ()
  ((_ exp)                     (TRY exp '()))
  ((_ exp catch...)            (TRY exp (begin catch...))))

;; try-cont: catches ContinuationException only (used internally by call/cc).
(macro try-cont ()
  ((_ exp)                     (TRY-CONT exp '()))
  ((_ exp catch...)            (TRY-CONT exp (begin catch...))))

;; (eval expr) -- evaluate expr as a Lisp form at runtime.
;; Example: (eval '(+ 1 2)) ==> 3
(macro eval ()
  ((_ exp)                     (EVAL exp))
  ((_ exp...)                  (begin (EVAL exp...) ...)))

;; (rec name expr) -- bind name to expr, making it available inside expr (for recursion).
;; Shorthand for (letrec ((name expr)) name).
;; Example:
;;   (map (rec sum
;;          (lambda (x)
;;            (if (= x 0) 0 (+ x (sum (- x 1))))))
;;        '(0 1 2 3 4 5))  ==> (0 1 3 6 10 15)
(macro rec ()
  ((_ x e)                     (letrec ((x e)) x)))

; (map (rec sum
;        (lambda (x)
;          (if (= x 0)
;              0
;              (+ x (sum (- x 1))))))
;      '(0 1 2 3 4 5)) ==> ( 0 1 3 6 10 15 )

;; (for-each f list...) -- apply f to each element of list(s) for side effects.
;; Supports one list, two lists, or an arbitrary number of lists.
;; Example: (for-each display '(1 2 3))  -- prints 123
(macro for-each ()
  ((_ f lst)                   (do ((l lst (cdr l))) ((null? l) '()) (f (car l))))
  ((_ f lst1 lst2)             (do ((l1 lst1 (cdr l1)) (l2 lst2 (cdr l2))) ((null? l1) '()) (f (car l1) (car l2))))
  ((_ f lst...)                (%for-each-n f (list lst...))))

;; (do ((var init step) ...) (test result...) body...) -- general iteration.
;; Initialises each var to init, then repeatedly: executes body, advances vars by step.
;; Stops when test is true; returns the result expressions (or test value if none).
;; Example:
;;   (do ((i 0 (+ i 1)) (sum 0 (+ sum i)))
;;       ((= i 5) sum))  ==> 10
(macro do ()  
  ((_ ((var init step) ...) (test) de...) 
   (begin (define (?do-loop var...)
             (let ((?x test))
                  (if ?x
                      ?x
                      (begin de... (?do-loop step...)))))
          (?do-loop init...)))
  ((_ ((var init step) ...) (test te...) de...) 
   (begin (define (?do-loop var...)
             (if test
                 (begin te...)
                 (begin de... (?do-loop step...))))
          (?do-loop init...))))

;; (apply f arg... lst) -- call f with positional args followed by elements of lst.
;; Example: (apply + '(1 2 3))       ==> 6
;; Example: (apply list 1 2 '(3 4))  ==> (1 2 3 4)
(define (apply f . args)
   (define (apply-list . ls)
      (cond ((null? ls)           '())
            ((or (null? (cdr ls)) 
             (null? (cadr ls)))   '(,@(car ls)))
            ((pair? (car ls))     '(,@(car ls) ,@(apply-list ,@(cdr ls))))
            (else                 '(,(car ls) ,@(apply-list ,@(cdr ls))))))
   (if (or (null? args) (null? (car args))) 
       (f)
       (f ,@(apply-list ,@args)) ))

;; (map f list...) -- apply f to each element (or set of elements) and collect results.
;; With one list: (map f '(1 2 3)) ==> (list (f 1) (f 2) (f 3))
;; With multiple lists: zips the lists and applies f element-wise.
;; Example: (map (lambda (x) (* x x)) '(1 2 3 4)) ==> (1 4 9 16)
;; Example: (map + '(1 2 3) '(4 5 6))             ==> (5 7 9)
(define map
  (lambda (f ls . more)
    (if (null? more)
        (let map1 ((ls ls))
          (if (null? ls)
              '()
              (cons (f (car ls))
                    (map1 (cdr ls)))))
        (let map-more ((ls ls) (more more))
          (if (null? ls)
              '()
              (cons (apply f (car ls) (map car more))
                    (map-more (cdr ls)
                              (map cdr more)))))))) 

(define (%for-each-n f lsts)
  (let loop ((lsts lsts))
    (if (or (null? lsts) (null? (car lsts)))
        '()
        (begin (apply f (map car lsts))
               (loop (map cdr lsts))))))
