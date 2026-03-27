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

(macro lambda ()
  ((_ () b...)                 (LAMBDA () b...))
  ((_ (a1) b...)               (LAMBDA (a1) b...))
  ((_ (a1 a...) b...)          (LAMBDA (a1 a...) b...))
  ((_ a b...)                  (LAMBDA (. a) b...)))
  
(macro define ()
  ((_ (n) b...)                (DEFINE n (lambda () b...)))
  ((_ (n v...) b...)           (DEFINE n (lambda (v...) b...)))
  ((_ n b...)                  (DEFINE n b...)))
    
(macro if ()
  ((_)                         #f)
  ((_ t)                       (let ((?x t)) (IF ?x ?x #f)))
  ((_ t then)                  (IF t then #f))
  ((_ t then else...)          (IF t then (begin else...))))
  
(macro cond (else =>)
  ((_)                         '())
  ((_ (t) m...)                (let ((?x t)) (if ?x ?x (cond m...))))
  ((_ (else v...) m...)        (begin v...))
  ((_ (t => e) m...)           (let ((?x t)) (if ?x (e ?x) (cond m...))))
  ((_ (t v...) m...)           (if t (begin v...) (cond m...))))
  
(macro or ()
  ((_)                         #f)
  ((_ e)                       e)
  ((_ e e1...)                 (let ((?t e)) (if ?t ?t (or e1...)))))
  
(macro and ()
  ((_)                         #t)
  ((_ e)                       e)
  ((_ e e1...)                 (if e (and e1...) #f)))

(macro case (else)
  ((_ key )                    key)
  ((_ key (else b...) m...)    (begin b...))
  ((_ key (v b...) m...)       (if (pair? (member key 'v)) (begin b...) (case key m...) )))
  
(macro let ()
  ((_ () b...)                 (begin b...))
  ((_ ((n1 v1) ...) b...)      ((lambda (n1...) b...) v1...))
  ((_ name ((n1 v1) ...) b...) ((lambda () (define (name n1...) b...) (name v1...)))))
  
(macro let* ()
  ((_ () b...)                 (begin b...))
  ((_ ((n1 v1)) b...)          ((lambda (n1) b...) v1))
  ((_ ((n1 v1) v...) b...)     ((lambda (n1) (let* (v...) b...)) v1)))
  
(macro letrec ()
  ((_ () b...)                 (begin b...))
  ((_ ((n1 v1) ...) b...)      ((lambda () (define n1... v1...) ... b...))))

;; letrec* -- like letrec but binds left-to-right (R7RS 4.2.2)
;; Each binding is visible to subsequent init expressions.
(macro letrec* ()
  ((_ () b...)                 (begin b...))
  ((_ ((n1 v1)) b...)          (let ((n1 v1)) b...))
  ((_ ((n1 v1) rest...) b...)  (let ((n1 v1)) (letrec* (rest...) b...))))

;; case-lambda -- lambda with multiple arities (R7RS 4.2.9)
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

(macro begin ()
  ((_)                         '())
  ((_ b)                       b)
  ((_ b...)                    ((lambda () b...))))
  
(macro when ()
  ((_ pred body...)            (and pred (begin body...))))

(macro unless ()
  ((_ pred body...)            (or pred (begin body...))))
  
(macro try ()
  ((_ exp)                     (TRY exp '()))
  ((_ exp catch...)            (TRY exp (begin catch...))))

;; try-cont: catches ContinuationException only (used internally by call/cc).
(macro try-cont ()
  ((_ exp)                     (TRY-CONT exp '()))
  ((_ exp catch...)            (TRY-CONT exp (begin catch...))))
  
(macro eval ()
  ((_ exp)                     (EVAL exp))
  ((_ exp...)                  (begin (EVAL exp...) ...)))
     
(macro rec ()
  ((_ x e)                     (letrec ((x e)) x)))
  
; (map (rec sum
;        (lambda (x)
;          (if (= x 0)
;              0
;              (+ x (sum (- x 1))))))
;      '(0 1 2 3 4 5)) ==> ( 0 1 3 6 10 15 )

(macro for-each ()
  ((_ f lst)                   (do ((l lst (cdr l))) ((null? l) '()) (f (car l))))
  ((_ f lst1 lst2)             (do ((l1 lst1 (cdr l1)) (l2 lst2 (cdr l2))) ((null? l1) '()) (f (car l1) (car l2))))
  ((_ f lst...)                (%for-each-n f (list lst...))))
          
;; expand the definition of 'do'
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

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Utilities
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

