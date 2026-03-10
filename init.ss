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

(call-static 'System.Console 'Write " [generics")

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

(call-static 'System.Console 'Write ", Utilities")

(define (Environment x) (call-static 'System.Environment 'GetEnvironmentVariable x))
(define (SysRoot)       (Environment "systemroot"))
(define (LispVersion)  (get (call (call-static 'System.Reflection.Assembly 'GetEntryAssembly) 'GetName) 'Version))
(define (.NetVer)       (call (get 'System.Environment 'Version ) 'ToString))
(define (GACRoot)       (string-append (SysRoot)
                                       "\\Microsoft.NET\\Framework\\v"
                                       (call (.NetVer) 'Substring 0 (call (.NetVer) 'LastIndexOf "."))
                                       "\\"))

(define (eq? x y)       (call-static 'System.Object 'ReferenceEquals x y))
(define (eqv? x y)      (call x 'Equals y))
(define (equal? x y)
  (cond
    ((null? x)    (null? y))
    ((number? x)  (and (number? y) (eqv? (todouble x) (todouble y))))
    ((symbol? x)  (and (symbol? y) (eqv? x y)))
    ((pair? x)    (and (pair? y)
                       (equal? (car x) (car y))
                       (equal? (cdr x) (cdr y))))
    ((char? x)    (and (char? y) (char=? x y)))
    ((boolean? x) (and (boolean? y) (if x y (not y))))
    ((vector? x)  (and (vector? y) 
                       (equal? (vector->list x) (vector->list y)) ))
    ((string? x)  (and (string? y) (string=? x y)))
    (else         #f)))
(define =               equal?)
(define (reduce fn base-value lis)
   (if (null? lis) base-value (fn (car lis) (reduce fn base-value (cdr lis)))))
;(reduce * 1 (2 3 4 5 6))
 
(define (COMPARE-ALL f . lst)
    (let ((ans #t) )
       (do ((ans #t ans) 
            (x (map f (reverse (cdr (reverse lst))) (cdr lst) ) (cdr x)))
           ((null? x) ans)
           (set! ans (and ans (car x))) ) ))
           
(define (STRCMP? a b t) 
  (call-static 'System.String 'Compare (call a 'ToString) (call b 'ToString) t))

(define (CALLNATIVE f . a)
  (call-static 'Lisp.Arithmetic f ,@a))
                 
(define (throw msg)     (call-static 'Lisp.Util 'Throw msg))
(define (get-type type) (call-static 'Lisp.Util 'GetType type))
 
(define (lastValue x)   (set 'Lisp.Programs.Program 'lastValue x))

(define (exit)          (set 'Lisp.Interpreter 'EndProgram #t))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Carry argument utilities - Lisp.Expressions.App.CarryOn
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", carry")

(define (carry x)  (set 'Lisp.Expressions.App 'CarryOn (if x #t #f)))

; (carry #t)
; allow things like 
; (\x.\y.\z.(+ x y z) 1 2 3) ==> ((LAMBDA (x) (LAMBDA (y) (LAMBDA (z) (+ x y z)))) 1 2 3)
; there are some limitations

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Combinator calculus - must hace (carry #t)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", combinators")

(define I \x.x)
(define K \x.\y.x)
(define S \x.\y.\z.((x z)(y z)))

; (define a 'a)
; (((S K) K) a) ==> a
; (((S I) I) a) ==> a a  (not working correctly)

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Trace utility -- Lisp.Expressions.Expression.traceHash with Lisp.Symbol keys
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", trace")

(define (*traceHash*)      (get 'Lisp.Expressions.Expression 'traceHash))
(define (trace x)          (set 'Lisp.Expressions.Expression 'Trace (if x #t #f)))
(define (trace-add . x)    (map (lambda (a) (call (*traceHash*) 'Add a)) x))
(define (trace-all)        (trace-add '_all_))
(define (trace-clear)      (call (*traceHash*) 'Clear))
(define (trace-remove . x) (map (lambda (a) (call (*traceHash*) 'Remove a)) x))
                              
; (trace #t)
; (trace-all)
; or
; (trace #t)
; (trace-clear)
; (trace-add 'member 'macro 'cdr 'null?)

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Stats -- Lisp.Programs.Program.Stats / Iterations
;; (stats #t)  -- enable timing + iteration count per top-level eval
;; (stats #f)  -- disable
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(define (stats x) (set 'Lisp.Programs.Program 'Stats (if x #t #f)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Macros -- c# type Lisp.Macros.Macro
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", macros")

(define (macro? x)      (call (get 'Lisp.Macros.Macro 'macros) 'ContainsKey x))
(define (macros->vector) 
  (new 'System.Collections.ArrayList (get (get 'Lisp.Macros.Macro 'macros) 'Keys)))
(define (macros->list)  (vector->list (macros->vector)))
(define (macro-body x)  (cdr (get (get 'Lisp.Macros.Macro 'macros) 'Item x)))
(define (macro-const x) (car (get (get 'Lisp.Macros.Macro 'macros) 'Item x)))
(define *displayMacros* 
  (lambda ()
    (let ((x (macros->vector)))
         (do ((y (- (vector-length x) 1) (- y 1)))
             ((< y 0) "")
             (display "\nMACRO {0} " (vector-ref x y))
             (letrec ((const (macro-const (vector-ref x y)))
                      (body  (macro-body  (vector-ref x y))))
                     (display (if (null? const) "()" const))
                     (map (lambda (x1) 
                                  (if (null? x1)
                                      (display "---")
                                      (display "\n==>   {0}\n      {1}" (car x1) (car (cdr x1)))))
                          body ))))))
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Procedures -- c# type Lisp.Closure
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", procedures")

(define (PROCEDURE? x)   (call (get (get 'Lisp.Programs.Program 
                                         'current) 
                                    'initEnv) 
                               'Apply x))
(define (procedure? x)   (closure? x))
(define (closure? x)     (= (call x 'GetType) (get-type "Lisp.Closure")))
(define (closure-args x) (get (PROCEDURE? x) 'ids))
(define (closure-body x) (get (PROCEDURE? x) 'body))
(define (procedures->vector)
  (new 'System.Collections.ArrayList (get (get (get (get 'Lisp.Programs.Program 'current) 'initEnv) 'table) 'Keys)))
(define (procedures->list) (vector->list (procedures->vector)))
(define *displayProcedures* 
  (lambda ()
    (let ((x (procedures->vector)))
         (do ((y (- (vector-length x) 1) (- y 1)))
             ((< y 0) "Done")
             (display "\nCLOSURE {0} " (vector-ref x y))
             (letrec ((args (closure-args (vector-ref x y)))
                      (body (closure-body (vector-ref x y))))
                     (display (if (null? args) "()" args))
                     (if (null? body)
                         (display " ()\n")
                         (display "\n{0}\n" body)))))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Lists -- c# type Lisp.Pair
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", pair")

(define (append . args) 
    (let f ((ls '()) (args args))
      (if (null? args)
          ls
          (let g ((ls ls))
            (if (null? ls)
                (f (car args) (cdr args))
                (cons (car ls) (g (cdr ls))) )))))
(define (assoc thing alist)
   (cond ((null? alist)               #f)
         ((= (car (car alist)) thing) (car alist))
         (else                        (assoc thing (cdr alist)))))
(define (car ls)        (get ls 'car))
(define (cdr ls)        (get ls 'cdr))
(define (caar   x)      (car (car x)))
(define (cadr   x)      (car (cdr x)))
(define (cdar   x)      (cdr (car x)))
(define (cddr   x)      (cdr (cdr x)))
(define (caaar  x)      (caar (car x)))
(define (caadr  x)      (caar (cdr x)))
(define (cadar  x)      (cadr (car x)))
(define (caddr  x)      (cadr (cdr x)))
(define (cdaar  x)      (cdar (car x)))
(define (cdadr  x)      (cdar (cdr x)))
(define (cddar  x)      (cddr (car x)))
(define (cdddr  x)      (cddr (cdr x)))
(define (caaaar x)      (caaar (car x)))
(define (caaadr x)      (caaar (cdr x)))
(define (caadar x)      (caadr (car x)))
(define (caaddr x)      (caadr (cdr x)))
(define (cadaar x)      (cadar (car x)))
(define (cadadr x)      (cadar (cdr x)))
(define (caddar x)      (caddr (car x)))
(define (cadddr x)      (caddr (cdr x)))
(define (cdaaar x)      (cdaar (car x)))
(define (cdaadr x)      (cdaar (cdr x)))
(define (cdadar x)      (cdadr (car x)))
(define (cdaddr x)      (cdadr (cdr x)))
(define (cddaar x)      (cddar (car x)))
(define (cddadr x)      (cddar (cdr x)))
(define (cdddar x)      (cdddr (car x)))
(define (cddddr x)      (cdddr (cdr x)))
(define (cons a ls)     (call-static 'Lisp.Pair 'Cons a ls))
(define (length x)      (get x 'Count))
(define (list . x)      x)
(define (list? x)
  ; Tortoise-and-hare: detects cycles so circular lists return #f instead of hanging
  (define (loop slow fast)
    (cond ((null? fast) #t)
          ((not (pair? fast)) #f)
          ((eq? slow fast) #f)
          ((null? (cdr fast)) #t)
          ((not (pair? (cdr fast))) #f)
          (else (loop (cdr slow) (cddr fast)))))
  (cond ((null? x) #t)
        ((pair? x) (loop x (cdr x)))
        (else #f)))
(define (list-ref l n)  (if (= n 0) (car l) (list-ref (cdr l) (- n 1))))
(define (list-tail l n) (if (= n 0) l (list-tail (cdr l) (- n 1))))
(define (member x y)
  (cond ((null? y)     #f)
	    ((= x (car y)) y)
		(else           (member x (cdr y)) )))
(define nil             '())
(define (null? x)       (call-static 'Lisp.Pair 'IsNull x))
(define (pair? obj) 
  (cond ((null? obj) #f) 
	    ((eqv? (call obj 'GetType) (get-type "Lisp.Pair")) #t)
	    (else #f)))
(define (reverse ls)
    (let rev ((ls ls) (new '()))
      (if (null? ls)
          new
          (rev (cdr ls) (cons (car ls) new)))))
(define (set-car! a ls) (set ls 'car a))
(define (set-cdr! a ls) (set ls 'cdr a))
(define (assq thing alist)
   (cond ((null? alist)                #f)
         ((eq?  (car (car alist)) thing) (car alist))
         (else                           (assq thing (cdr alist)))))
(define (assv thing alist)
   (cond ((null? alist)                #f)
         ((eqv? (car (car alist)) thing) (car alist))
         (else                           (assv thing (cdr alist)))))
(define (memq x y)
  (cond ((null? y)      #f)
        ((eq? x (car y)) y)
        (else             (memq x (cdr y)))))
(define (memv x y)
  (cond ((null? y)       #f)
        ((eqv? x (car y)) y)
        (else              (memv x (cdr y)))))
(define (list-copy ls)  (map (lambda (x) x) ls))
(define (iota n . rest)
  (let ((start (if (null? rest) 0 (car rest)))
        (step  (if (or (null? rest) (null? (cdr rest))) 1 (cadr rest))))
    (let loop ((i 0) (acc '()))
      (if (= i n)
          (reverse acc)
          (loop (+ i 1) (cons (+ start (* i step)) acc))))))
(define (adjoin e l)    (if (member e l) l (cons e l)))
(define (union l1 l2)
  (cond ((null? l1) l2)
	    ((null? l2) l1)
	    (else       (union (cdr l1) (adjoin (car l1) l2)))))
(define (intersection l1 l2)
  (cond ((null? l1)           l1)
	    ((null? l2)           l2)
	    ((member (car l1) l2) (cons (car l1) (intersection (cdr l1) l2)))
	    (else                 (intersection (cdr l1) l2))))
(define (difference l1 l2)
  (cond ((null? l1)           l1)
	    ((member (car l1) l2) (difference (cdr l1) l2))
	    (else                 (cons (car l1) (difference (cdr l1) l2)))))

;; --- Higher-order list utilities ---
(define (filter pred lst)
  (cond ((null? lst) '())
        ((pred (car lst)) (cons (car lst) (filter pred (cdr lst))))
        (else (filter pred (cdr lst)))))
(define (foldl f init lst)
  (if (null? lst) init (foldl f (f init (car lst)) (cdr lst))))
(define (foldr f init lst)
  (if (null? lst) init (f (car lst) (foldr f init (cdr lst)))))
(define (any pred lst)
  (cond ((null? lst) #f) ((pred (car lst)) #t) (else (any pred (cdr lst)))))
(define (every pred lst)
  (cond ((null? lst) #t) ((not (pred (car lst))) #f) (else (every pred (cdr lst)))))
(define (take lst n)
  (if (or (= n 0) (null? lst)) '() (cons (car lst) (take (cdr lst) (- n 1)))))
(define drop list-tail)
(define (last lst)
  (if (null? (cdr lst)) (car lst) (last (cdr lst))))
(define (last-pair lst)
  (if (null? (cdr lst)) lst (last-pair (cdr lst))))
(define (zip . lists)     (apply map list lists))
(define (flatten lst)
  (cond ((null? lst) '())
        ((pair? (car lst)) (append (flatten (car lst)) (flatten (cdr lst))))
        (else (cons (car lst) (flatten (cdr lst))))))
(define (count pred lst)  (foldl (lambda (acc x) (if (pred x) (+ acc 1) acc)) 0 lst))
(define (flat-map f lst)  (apply append (map f lst)))
(define second  cadr)
(define third   caddr)
(define fourth  cadddr)
(define (fifth x)         (car (cddddr x)))
(define (atom? x)         (not (pair? x)))

;; --- Functional combinators ---
(define (identity x) x)
(define (compose . fns)
  (reduce (lambda (f g) (lambda (x) (f (g x)))) identity fns))
(define (negate pred)     (lambda args (not (apply pred args))))

;; --- Additional list operations ---
(define (make-list n . rest)
  (let ((fill (if (null? rest) #f (car rest))))
    (let loop ((i n) (acc '()))
      (if (= i 0) acc (loop (- i 1) (cons fill acc))))))
(define (find pred lst)
  (cond ((null? lst) #f) ((pred (car lst)) (car lst)) (else (find pred (cdr lst)))))
(define (remove pred lst)          (filter (negate pred) lst))
(define (filter-map f lst)
  (let loop ((lst lst) (acc '()))
    (if (null? lst)
        (reverse acc)
        (let ((v (f (car lst))))
          (loop (cdr lst) (if v (cons v acc) acc))))))
(define (delete-duplicates lst)
  (let loop ((lst lst) (seen '()))
    (cond ((null? lst)              (reverse seen))
          ((member (car lst) seen)  (loop (cdr lst) seen))
          (else                     (loop (cdr lst) (cons (car lst) seen))))))
(define (list-set lst n val)
  (let loop ((lst lst) (i 0) (acc '()))
    (if (null? lst)
        (reverse acc)
        (loop (cdr lst) (+ i 1) (cons (if (= i n) val (car lst)) acc)))))
(define (concatenate lsts)         (apply append lsts))
(define append-map                 flat-map)
(define for-all                    every)
(define exists                     any)

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Characters -- c# type == String.Char
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", char")

(define (char? c)            (= (call c 'GetType) (get-type "System.Char")))
(define (char<? . l)         (COMPARE-ALL (lambda (a b) (<  (STRCMP? a b #f) 0)) ,@l))
(define (char<=? . l)        (COMPARE-ALL (lambda (a b) (<= (STRCMP? a b #f) 0)) ,@l))
(define (char=? . l)         (COMPARE-ALL (lambda (a b) (=  (STRCMP? a b #f) 0)) ,@l))
(define (char<>? . l)        (COMPARE-ALL (lambda (a b) (<> (STRCMP? a b #f) 0)) ,@l))
(define (char>=? . l)        (COMPARE-ALL (lambda (a b) (>= (STRCMP? a b #f) 0)) ,@l))
(define (char>?  . l)        (COMPARE-ALL (lambda (a b) (>  (STRCMP? a b #f) 0)) ,@l))
(define (char-ci<? . l)      (COMPARE-ALL (lambda (a b) (<  (STRCMP? a b #t) 0)) ,@l))
(define (char-ci<=? . l)     (COMPARE-ALL (lambda (a b) (<= (STRCMP? a b #t) 0)) ,@l))
(define (char-ci=? . l)      (COMPARE-ALL (lambda (a b) (=  (STRCMP? a b #t) 0)) ,@l))
(define (char-ci<>? . l)     (COMPARE-ALL (lambda (a b) (<> (STRCMP? a b #t) 0)) ,@l))
(define (char-ci>=? . l)     (COMPARE-ALL (lambda (a b) (>= (STRCMP? a b #t) 0)) ,@l))
(define (char-ci>?  . l)     (COMPARE-ALL (lambda (a b) (>  (STRCMP? a b #t) 0)) ,@l))
(define (char-alphabetic? c) (call-static 'System.Char    'IsLetter c))
(define (char-numeric? c)    (call-static 'System.Char    'IsDigit c))
(define (char-lower-case? c) (call-static 'System.Char    'IsLower c))
(define (char-upper-case? c) (call-static 'System.Char    'IsUpper c))
(define (char-whitespace? c) (call-static 'System.Char    'IsWhiteSpace c))
(define (char-upcase c)      (call-static 'System.Char    'ToUpper c))
(define (char-downcase c)    (call-static 'System.Char    'ToLower c))
(define (char->integer c)    (call-static 'System.Convert 'ToInt32 c))
(define (integer->char i)    (call-static 'System.Convert 'ToChar i))
(define (char->digit c . rest)
  (let* ((radix (if (null? rest) 10 (car rest)))
         (n     (char->integer c))
         (i     (cond ((and (>= n (char->integer #\0)) (<= n (char->integer #\9)))
                       (- n (char->integer #\0)))
                      ((and (>= n (char->integer #\a)) (<= n (char->integer #\z)))
                       (+ 10 (- n (char->integer #\a))))
                      ((and (>= n (char->integer #\A)) (<= n (char->integer #\Z)))
                       (+ 10 (- n (char->integer #\A))))
                      (else -1))))
    (if (and (>= i 0) (< i radix)) i #f)))
(define (char-punctuation? c)  (call-static 'System.Char 'IsPunctuation c))
(define (char-symbol? c)       (call-static 'System.Char 'IsSymbol c))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Strings -- c# type == System.String
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", string")

(define (string . lst)      (list->string lst))
(define (string? x)         (= (call x 'GetType) (get-type "System.String")))
(define (string<? . l)      (COMPARE-ALL (lambda (a b) (<  (STRCMP? a b #f) 0)) ,@l))
(define (string<=? . l)     (COMPARE-ALL (lambda (a b) (<= (STRCMP? a b #f) 0)) ,@l))
(define (string=? . l)      (COMPARE-ALL (lambda (a b) (=  (STRCMP? a b #f) 0)) ,@l))
(define (string<>? . l)     (COMPARE-ALL (lambda (a b) (<> (STRCMP? a b #f) 0)) ,@l))
(define (string>=? . l)     (COMPARE-ALL (lambda (a b) (>= (STRCMP? a b #f) 0)) ,@l))
(define (string>?  . l)     (COMPARE-ALL (lambda (a b) (>  (STRCMP? a b #f) 0)) ,@l))
(define (string-ci<? . l)   (COMPARE-ALL (lambda (a b) (<  (STRCMP? a b #t) 0)) ,@l))
(define (string-ci<=? . l)  (COMPARE-ALL (lambda (a b) (<= (STRCMP? a b #t) 0)) ,@l))
(define (string-ci=? . l)   (COMPARE-ALL (lambda (a b) (=  (STRCMP? a b #t) 0)) ,@l))
(define (string-ci<>? . l)  (COMPARE-ALL (lambda (a b) (<> (STRCMP? a b #t) 0)) ,@l))
(define (string-ci>=? . l)  (COMPARE-ALL (lambda (a b) (>= (STRCMP? a b #t) 0)) ,@l))
(define (string-ci>?  . l)  (COMPARE-ALL (lambda (a b) (>  (STRCMP? a b #t) 0)) ,@l))
(define (string-length s)   (get s 'Length))
(define (string-ref s n)    (get s 'Chars (inexact->exact n)))
(define (string-set! s n c) 
  (string-append (substring s 0 n) 
                 (call c 'ToString) 
                 (substring s (+ n 1) (string-length s)))) 
(define (STRAPPEND s a)     (call s 'Insert (string-length s) (call a 'ToString)))
(define (string-copy a)     (string-append a))
(define (string-append . l) (reduce STRAPPEND "" l))
(define (string-upcase s)   (call s 'ToUpper))
(define (string-downcase s) (call s 'ToLower))
(define (string-trim s)     (call s 'Trim))
(define (string-trim-right s) (call s 'TrimEnd))
(define (string-trim-left s)  (call s 'TrimStart))
(define (string-contains s sub) (not (= -1 (call s 'IndexOf sub))))
(define (substring s f l)   (call s 'Substring (inexact->exact f) (inexact->exact (- l f))))
(define (string-fill! s c)
  (let ((n (string-length s)))
    (do ((i 0 (+ i 1)))
        ((= i n) s)
        (set! s (string-set! s i c))))) 
(define (string->list s)
  (do ((i (- (string-length s) 1) (- i 1))
       (ls '() (cons (string-ref s i) ls)))
      ((< i 0) ls)
      "nothing")) 
(define (list->string ls)   (string-append ,@(map (lambda (x) (call x 'ToString)) ls)))
(define (make-string n . rest)
  (if (null? rest)
      (new 'System.String #\  n)
      (new 'System.String (car rest) n)))
(define (string->integer s) (call-static 'System.Convert 'ToInt32 s))
(define (string->real s)    (call-static 'System.Convert 'ToDouble s))
(define (string-split s delim)
  (let loop ((s s) (acc '()))
    (let ((i (call s 'IndexOf delim)))
      (if (< i 0)
          (reverse (cons s acc))
          (loop (substring s (+ i (string-length delim)) (string-length s))
                (cons (substring s 0 i) acc))))))
(define (string-join lst . rest)
  (let ((sep (if (null? rest) "" (car rest))))
    (if (null? lst)
        ""
        (foldl (lambda (acc x) (string-append acc sep x)) (car lst) (cdr lst)))))
(define (string-for-each f s)
  (let ((n (string-length s)))
    (do ((i 0 (+ i 1))) ((= i n)) (f (string-ref s i)))))
(define (string-repeat s n)
  (let loop ((i 0) (acc ""))
    (if (= i n) acc (loop (+ i 1) (string-append acc s)))))
(define (string-index s pred)
  (let ((n (string-length s)))
    (let loop ((i 0))
      (cond ((= i n) #f)
            ((pred (string-ref s i)) i)
            (else (loop (+ i 1)))))))
(define (string-map f s)       (list->string (map f (string->list s))))
(define (string->vector s)     (list->vector (string->list s)))
(define (vector->string v)     (list->string (vector->list v)))
        
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Boolean -- c# type == System.Boolean
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", boolean")

(define (boolean? n)   (= (call n 'GetType) (get-type "System.Boolean")))
(define (boolean=? . l) (COMPARE-ALL (lambda (a b) (eqv? a b)) ,@l))
(define (not x)        (if x #f #t))
(define (xor x y)      (CALLNATIVE 'XorObj x y))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Symbols -- c# type == Lisp.Symbol
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", symbol")

(define (symbol? x)        (= (call x 'GetType) (get-type "Lisp.Symbol")))
(define (symbol->string s) (call s 'ToString))
(define (symbol-generate)  (call-static 'Lisp.Symbol 'GenSym))
(define (string->symbol s) (call-static 'Lisp.Symbol 'Create s))
(define (symbols->vector)
  (new 'System.Collections.ArrayList (get (get 'Lisp.Symbol 'syms) 'Keys)))
(define (symbols->list)    (vector->list (symbols->vector)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Numbers -- c# type == System.Int32 or System.Single
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", numbers")

(define (+ . lst)       (reduce (lambda (a b)(CALLNATIVE 'AddObj a b)) 0 lst))
(define (- . lst)       (if (null? (cdr lst)) (CALLNATIVE 'NegObj (car lst)) (reduce (lambda (a b)(CALLNATIVE 'SubObj a b)) 0 lst)))
(define (/ . lst)       (reduce (lambda (a b)(CALLNATIVE 'DivObj a b)) 1 lst))
(define (* . lst)       (reduce (lambda (a b)(CALLNATIVE 'MulObj a b)) 1 lst))
(define (< . lst)       (COMPARE-ALL (lambda (a b) (LESSTHAN a b))       ,@lst))
(define (<= . lst)      (COMPARE-ALL (lambda (a b) (or (< a b) (= a b))) ,@lst))
(define (= . lst)       (COMPARE-ALL (lambda (a b) (try (eqv? (todouble a) (todouble b)) (eqv? a b))) ,@lst))
(define (<> . lst)      (COMPARE-ALL (lambda (a b) (not (= a b)))        ,@lst))
(define (>= . lst)      (COMPARE-ALL (lambda (a b) (not (< a b)))        ,@lst))
(define (>  . lst)      (COMPARE-ALL (lambda (a b) (not (<= a b)))       ,@lst))
(define (abs a)         (call-static 'System.Math 'Abs a))
(define (acos a)        (call-static 'System.Math 'Acos a))
(define (asin a)        (call-static 'System.Math 'Asin a))
(define (atan a)        (call-static 'System.Math 'Atan a))
(define (bit-and a b)   (CALLNATIVE 'BitAndObj a b))
(define (bit-or a b)    (CALLNATIVE 'BitOrObj a b))
(define (bit-xor a b)   (CALLNATIVE 'BitXorObj a b))
(define (ceiling x)     (if (exact? x) x (call-static 'System.Math 'Ceiling x)))
(define (cos a)         (call-static 'System.Math 'Cos a))
(define E               (get 'System.Math 'E))
(define (even? x)       (= (remainder x 2) 0))
(define (exact? x)      (eqv? (call x 'GetType) (get-type "System.Int32")))
(define (exp a)         (call-static 'System.Math 'Exp a))
(define (expt a b)      (CALLNATIVE 'PowObj a b))
(define (! a)           (if (<= a 1) 1 (* a (! (- a 1)))))
;; (define (fib n)         (if (<= n 2) 1 (+ (fib (- n 1)) (fib (- n 2)))))
(define (fib n)         (define (loop a b k)
                          (if (= k 0)
                              a
                              (loop b (+ a b) (- k 1))))
                        (loop 0 1 n))

(define (floor a)       (if (exact? a) a (call-static 'System.Math 'Floor a)))
(define (gcd a b)       (if (= b 0)
                          (abs a)
                          (gcd b (remainder a b))))
(define (lcm a b)       (quotient (* (abs a) (abs b)) (gcd a b)))
(define (inexact? x)    (not (exact? x)))
(define (integer? n)
  (and (number? n)
       (try (= (todouble n) (todouble (tointeger n))) #f)))
(define (int? n)        (integer? n))
(define (log a)         (call-static 'System.Math 'Log a))
(define (log10 a)       (call-static 'System.Math 'Log10 a))
(define (max x . lst)
  (if (null? lst) 
      x
      (let ((n (max ,@lst)))
        (if (< x n) n x))))
(define (min x . lst)
  (if (null? lst) 
      x
      (let ((n (min ,@lst)))
        (if (< x n) x n))))
(define (modulo x y)
  (let ((r (remainder x y)))
    (if (or (zero? r)
            (and (positive? r) (positive? y))
            (and (negative? r) (negative? y)))
        r
        (+ r y))))
(define (neg a)         (CALLNATIVE 'NegObj a))
(define (negative? a)   (< a 0))
(define (number? n)     (or (eqv? (call n 'GetType) (get-type "System.Int32"))
                            (eqv? (call n 'GetType) (get-type "System.Double"))))
(define (odd? x)        (if (even? x) #f #t))
(define PI              (get 'System.Math 'PI))
(define (positive? x)   (> x 0))
(define (pow x y)       (call-static 'System.Math 'Pow x y))
(define (quotient x y)  (CALLNATIVE 'IDivObj x y))
(define (real? n)       (number? n))
(define (reciprocal n)  (if (= n 0) "oops!" (/ 1 n)))
(define (remainder x y) (CALLNATIVE 'ModObj x y))
(define (round a)       (if (exact? a) a (call-static 'System.Math 'Round a)))
(define (sin a)         (call-static 'System.Math 'Sin a))
(define (sort l)
  (define (insert x l)
    (if (null? l)
        (list x)
        (if (<= x (car l))
            (cons x l)
            (cons (car l) (insert x (cdr l))))))
  (if (null? l)
      '()
      (insert (car l) (sort (cdr l)))))
(define (sort-by f l)
  (define (insert x l)
    (if (null? l)
        (list x)
        (if (<= (f x) (f (car l)))
            (cons x l)
            (cons (car l) (insert x (cdr l))))))
  (if (null? l)
      '()
      (insert (car l) (sort-by f (cdr l)))))
(define (sqrt a)        (call-static 'System.Math 'Sqrt a))
(define PHI             (/ (+ 1 (sqrt 5)) 2))
(define (tan a)         (call-static 'System.Math 'Tan a))
(define (todouble a)      (call-static 'System.Convert 'ToDouble a))
(define (tointeger a)     (call-static 'System.Convert 'ToInt32 a))
(define (truncate x)      (if (exact? x) x (exact->inexact (tointeger (call-static 'System.Math 'Truncate x)))))
(define (zero? x)         (= x 0))
(define (square x)        (* x x))
(define (complex? n)      (number? n))
(define (rational? n)     (number? n))
(define (exact->inexact n) (call-static 'System.Convert 'ToDouble n))
(define (inexact->exact n) (call-static 'System.Convert 'ToInt32 n))
(define exact   inexact->exact)
(define inexact exact->inexact)
(define (number->string n . rest)
  (if (null? rest)
      (if (number? n)
          (if (exact? n)
              (call n 'ToString)
              (if (nan? n)      "+nan.0"
                  (if (infinite? n)
                      (if (> n 0) "+inf.0" "-inf.0")
                      (call-static 'Lisp.Util 'Dump n))))
          (call n 'ToString))
      (call-static 'System.Convert 'ToString n (car rest))))
(define (string->number s . rest)
  (try (if (null? rest)
           (cond ((equal? s "+nan.0")  (/ 0.0 0.0))
                 ((equal? s "+inf.0")  (/ 1.0 0.0))
                 ((equal? s "-inf.0")  (/ -1.0 0.0))
                 ((or (string-contains s ".")
                      (string-contains s "E")
                      (string-contains s "e"))
                  (string->real s))
                 (else (string->integer s)))
           (call-static 'System.Convert 'ToInt32 s (car rest)))
       #f))

;; --- Numeric additions ---
(define (exact-integer? x)    (and (integer? x) (exact? x)))
(define (infinite? x)         (call-static 'System.Double 'IsInfinity (todouble x)))
(define (nan? x)              (call-static 'System.Double 'IsNaN (todouble x)))
(define (finite? x)           (not (or (infinite? x) (nan? x))))
(define (atan2 y x)           (call-static 'System.Math 'Atan2 y x))
(define (floor-quotient x y)  (tointeger (floor (/ (inexact x) y))))
(define (floor-remainder x y) (- x (* y (floor-quotient x y))))
(define (truncate-quotient x y)  (quotient  x y))
(define (truncate-remainder x y) (remainder x y))
(define (bit-not x)           (- -1 x))
(define (arithmetic-shift x n)
  (if (>= n 0)
      (tointeger (* x (expt 2 n)))
      (quotient x (tointeger (expt 2 (neg n))))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Vectors -- c# type == System.Collections.ArrayList
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", vectors")

(define (list->vector x)      (vector ,@x))
(macro make-vector ()
 ((_ n)      (MAKE-VECTOR n 0))
 ((_ n a...) (MAKE-VECTOR n a...)))
(define (MAKE-VECTOR n . obj) 
   (do ((v '() v) (i 1 (+ i 1)) (o (if (null? obj) '() (car obj)) o))
       ((or (> i n) (null? o)) (list->vector v))
       (set! v (cons o v)))) 
(define (vector? x)           (= (call x 'GetType) (get-type "System.Collections.ArrayList")))
(define (vector . x)          (new 'System.Collections.ArrayList (call x 'ToArray)))
(define (vector-copy v)       (list->vector (vector->list v)))
(define (vector-fill! v x)
    (let ((n (vector-length v)))
      (do ((i 0 (+ i 1)))
          ((= i n) v)
          (vector-set! v i x)))) 
(define (vector-length x)     (get x 'Count))
(define (vector-map f v)      (list->vector (map f (vector->list v))))
(define (vector-ref v x)      (get v 'Item x))
(define (vector-set! v k obj) (set v 'Item k obj))
(define (vector->list x)      
  (define (other itm y)
    (if (= itm (vector-length y))
        '()
        (cons (vector-ref y itm) (other (inexact->exact (+ itm 1)) y))))
  (other 0 x))
(define (vector-union v1 v2) 
  (list->vector (union (vector->list v1) (vector->list v2))))
(define (vector-intersection v1 v2) 
  (list->vector (intersection (vector->list v1) (vector->list v2))))
(define (vector-difference v1 v2) 
  (list->vector (difference (vector->list v1) (vector->list v2))))
(define (vector-for-each f v)
  (let ((n (vector-length v)))
    (do ((i 0 (+ i 1))) ((= i n)) (f (vector-ref v i)))))
(define (vector-append . vecs)
  (list->vector (apply append (map vector->list vecs))))
  
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Input Ports -- c# type == System.IO.StreamReader
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", input")

(define (input-port? x)
  (or (= (call x 'GetType) (get-type "System.IO.StreamReader"))
      (= (call x 'GetType) (get-type "System.IO.StringReader"))))
(define *INPUT*                           '())
(define *INPUT-BUFFER*                    "")
(define (current-input-port)              (if (null? *INPUT*) (get 'System.Console 'In) *INPUT*))
(define (open-input-file fname)
  (let ((sr (new 'System.IO.StreamReader fname)))
    (set! *INPUT* sr)
    (set! *INPUT-BUFFER* (call sr 'ReadToEnd))
    sr))
(define (close-input-port iport)          (call iport 'Close))
(define (call-with-input-file fname proc)
    (let ((p (open-input-file fname)))
      (let ((v (proc p)))
        (close-input-port p)
        v)))
(define (INPUT-PORT x)                    (if (null? x) (current-input-port) (car x)))
(define (read . iport)
  (let ((result (call-static 'Lisp.Util 'ParseOne *INPUT-BUFFER*)))
    (set! *INPUT-BUFFER* (get 'Lisp.Util 'ParseRemainder))
    result))
(define (read-line . iport)               (call (INPUT-PORT iport) 'ReadLine))
(define (read-toend . iport)              (call (INPUT-PORT iport) 'ReadToEnd))
(define (read-char . iport)
  (if (> (string-length *INPUT-BUFFER*) 0)
      (let ((c (string-ref *INPUT-BUFFER* 0)))
        (set! *INPUT-BUFFER* (substring *INPUT-BUFFER* 1 (string-length *INPUT-BUFFER*)))
        c)
      (integer->char (call (INPUT-PORT iport) 'Read))))
(define (peek-char . iport)
  (if (> (string-length *INPUT-BUFFER*) 0)
      (string-ref *INPUT-BUFFER* 0)
      (integer->char (call (INPUT-PORT iport) 'Peek))))
(define (eof-object? x)                   (and (char? x) (= (char->integer x) 65535)))
(define (load x)
  (begin (display " [")
         (display x)
         (display "...")
         (let ((_inFile (new 'System.IO.StreamReader x)))
           (call (get 'Lisp.Programs.Program 'current)
                 'Eval
                 (call _inFile 'ReadToEnd))
           (call _inFile 'Close))
         (display "]")))
(define (open-input-string s)     (new 'System.IO.StringReader s))
(define (string-port? x)
  (or (= (call x 'GetType) (get-type "System.IO.StringReader"))
      (= (call x 'GetType) (get-type "System.IO.StringWriter"))))
(define (with-input-from-file fname thunk)
  (let ((old *INPUT*) (old-buf *INPUT-BUFFER*))
    (open-input-file fname)
    (let ((result (thunk)))
      (close-input-port *INPUT*)
      (set! *INPUT* old)
      (set! *INPUT-BUFFER* old-buf)
      result)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Output Ports -- c# type == System.IO.StreamWriter
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", output")

(define (output-port? obj)
  (or (= (call obj 'GetType) (get-type "System.IO.StreamWriter"))
      (= (call obj 'GetType) (get-type "System.IO.StringWriter"))))
(define *OUTPUT*                           '())
(define (current-output-port)              (if (null? *OUTPUT*) (get 'System.Console 'Out) *OUTPUT*))
(define (open-output-file fname)
  (let ((sw (new 'System.IO.StreamWriter fname)))
    (set! *OUTPUT* sw)
    sw))
(define (close-output-port oport)          (call oport 'Close))
(define (call-with-output-file fname proc)
    (let ((p (open-output-file fname)))
      (let ((v (proc p)))
        (close-output-port p)
        v)))
(define (console . x)                      (call-static 'System.Console 'Write ,@x))
(define (consoleLine . x)                  (call-static 'System.Console 'WriteLine ,@x))
(define (display obj . rest)
  (cond
    ((null? rest)
     (call (current-output-port) 'Write obj))
    ((output-port? (car rest))
     (call (car rest) 'Write obj))
    (else
     (call-static 'System.Console 'Write obj ,@rest))))
(define (write obj . rest)
  (let ((port (if (null? rest) (current-output-port) (car rest))))
    (call port 'Write (call-static 'Lisp.Util 'Dump obj))))
(define (write-char char . rest)
  (let ((port (if (null? rest) (current-output-port) (car rest))))
    (call port 'Write char)))
(define (writeline . x)                    (call (current-output-port) 'WriteLine ,@x))
(define (OUTPUT-PORT x)                    (if (null? x) (current-output-port) (car x)))
(define (newline . rest)
  (let ((port (if (null? rest) (current-output-port) (car rest))))
    (call port 'WriteLine "")))
(define (write-string s . rest)
  (let ((port (if (null? rest) (current-output-port) (car rest))))
    (call port 'Write s)))
(define (flush-output-port . rest)
  (let ((port (if (null? rest) (current-output-port) (car rest))))
    (call port 'Flush)))
(define (open-output-string)      (new 'System.IO.StringWriter))
(define (get-output-string port)  (call port 'ToString))
(define (with-output-to-file fname thunk)
  (let ((old *OUTPUT*))
    (open-output-file fname)
    (let ((result (thunk)))
      (close-output-port *OUTPUT*)
      (set! *OUTPUT* old)
      result)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Error
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(define (error msg . irritants)
  (if (null? irritants)
      (throw msg)
      (throw (string-append msg ": "
               (string-join (map (lambda (x) (call x 'ToString)) irritants) " ")))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Records or structures -- defined as special vectors
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", records")

(macro record (define new ! ? call)
  ((_ define type (f1 v1) ...) (define (type)
                                       (vector 'record
                                               'type
                                               (symbol-generate)
                                               (list->vector '(f1...))
                                               (list->vector '(v1...)))))
  ((_ define type flds...)     (record define type (flds... 0) ...))
  ((_ name new type)           (define name (type)))
  ((_ name call method a...)   ((record name method) a...))
  ((_ name ? type)             (= 'type (record-name name)))
  ((_ name ! (field val) ...)  (begin (record-field-set! name 'field... val...) ...))
  ((_ name ! field val)        (record-field-set! name 'field val))
  ((_ name)                    (record-values name))
  ((_ name field)              (record-field-get name 'field))
  ((_ name fields...)          (list (record-field-get name 'fields...) ...)))
  
(define (record? r)            (and (vector? r) (= (vector-ref r 0) 'record)))
(define (record-name r)        (vector-ref r 1))
(define (record-instance r)    (vector-ref r 2))
(define (record-fields r)      (vector-ref r 3))
(define (record-values r)      (vector-ref r 4))
(define (record-field-get r f) 
   (let ((fields (member f (vector->list (record-fields r))))
         (len    (vector-length (record-fields r))))
        (if (pair? fields) 
            (vector-ref (record-values r) (- len (length fields)))
            '() )))
(define (record-field-set! r f obj) 
   (let ((fields (member f (vector->list (record-fields r))))
         (len    (vector-length (record-fields r))))
        (if (pair? fields) 
            (vector-set! (record-values r) (- len (length fields)) obj)
            '() )))

; (record define <point> (x 1) (y 2) (act 3))
; (record p1 new <point>)
; (record p1 x)
; (record p1 ? <point>)
; (record p1 ! y 33)
; (record p1 ! act (lambda (x) (+ x x)))
; (record p1 call act 3)
; (record p1)

; what about the following - when try to print
; (record p1 ! y p1)

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Multiple values -- more work
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

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

(call-static 'System.Console 'Write ", delayEvaluation")

(macro delay ()
  ((_ exp)    (make-promise (lambda () exp)))) 
  
(define make-promise
   (lambda (thunk)
      (let ((value #f) (set? #f))
         (lambda ()
            (unless set?
               (let ((v (thunk)))
                  (unless set?
                     (set! value v)
                     (set! set? #t))))
            value))))

(define force (lambda (promise) (promise)))

; (define (stream-car s) (car (force s)))
; (define (stream-cdr s) (cdr (force s)))
; (define counters
;   (let next ((n 1))
;     (delay (cons n (next (+ n 1)))))) 
; (stream-car counters)              ==> 1
; (stream-car (stream-cdr counters)) ==> 2

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Continuations -- (local exit only) -- change!!!
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", continuation")

(macro call/cc ()
  ((_ exp)  (let ((?value '()))
                 (try (exp (lambda (?x) 
                                   (set! ?value ?x) 
                                   (throw "out")))
                      ?value))))

(macro let/cc ()
  ((_ var exp...) (call/cc (lambda (k) exp... ))))

(macro call-with-current-continuation ()
  ((_ exp) (call/cc exp)))

; (call/cc (lambda (k) (* 5 4)))
; (call/cc (lambda (k) (* 5 (k 4))))
; (* 2 (call/cc (lambda (k) (* 5 (k 4)))))

; (let ((x (call/cc (lambda (k) k)))) (x (lambda (ignore) "hi")))
; (((call/cc (lambda (k) k)) (lambda (x) x)) "hey")
; (define retry #f)
; (define (factorial x)
;   (if (= x 0)
;       (call/cc (lambda (k) (set! retry k) 1))
;       (* x (factorial (- x 1)))))
; (factorial 4)
; (retry 1)     ==>  24
; (retry 2)     ==>  48
; (retry 5)     ==>  120

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; A Unification Algorithm
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;    A unification algorithm attempts to make two symbolic expressions equal by computing
;; a unifying substitution for the expressions. A substitution is a function that replaces
;; variables with other expressions. A substitution must treat all occurrences of a variable
;; the same way, e.g., if it replaces one occurrence of the variable x by a, it must replace
;; all occurrences of x by a. A unifying substitution, or unifier, for two expressions e1 
;; and e2 is a substitution, , such that . 
;;    For example, the two expressions f(x) and f(y) can be unified by substituting x for
;; y (or y for x). In this case, the unifier  could be described as the function that replaces
;; y with x and leaves other variables unchanged. On the other hand, the two expressions
;; x + 1 and y + 2 cannot be unified. It might appear that substituting 3 for x and 2 for
;; y would make both expressions equal to 4 and hence equal to each other. The symbolic 
;; expressions, 3 + 1 and 2 + 2, however, still differ. 
;;    Two expressions may have more than one unifier. For example, the expressions f(x,y)
;; and f(1,y) can be unified to f(1,y) with the substitution of 1 for x. They may also be
;; unified to f(1,5) with the substitution of 1 for x and 5 for y. The first substitution
;; is preferable, since it does not commit to the unnecessary replacement of y. Unification
;; algorithms typically produce the most general unifier, or mgu, for two expressions. The
;; mgu for two expressions makes no unnecessary substitutions; all other unifiers for the
;; expressions are special cases of the mgu. In the example above, the first substitution
;; is the mgu and the second is a special case. 
;;    For the purposes of this program, a symbolic expression can be a variable, a constant,
;; or a function application. Variables are represented by Scheme symbols, e.g., x; a function
;; application is represented by a list with the function name in the first position and its
;; arguments in the remaining positions, e.g., (f x); and constants are represented by
;; zero-argument functions, e.g., (a). 
;;    The algorithm presented here finds the mgu for two terms, if it exists, using a
;; continuation passing style, or CPS (see Section 3.4), approach to recursion on subterms.
;; The procedure unify takes two terms and passes them to a help procedure, uni, along with
;; an initial (identity) substitution, a success continuation, and a failure continuation.
;; The success continuation returns the result of applying its argument, a substitution,
;; to one of the terms, i.e., the unified result. The failure continuation simply returns
;; its argument, a message. Because control passes by explicit continuation within unify
;; (always with tail calls), a return from the success or failure continuation is a return
;; from unify itself. 
;;    Substitutions are procedures. Whenever a variable is to be replaced by another term,
;; a new substitution is formed from the variable, the term, and the existing substitution.
;; Given a term as an argument, the new substitution replaces occurrences of its saved
;; variable with its saved term in the result of invoking the saved substitution on the
;; argument expression. Intuitively, a substitution is a chain of procedures, one for each
;; variable in the substitution. The chain is terminated by the initial, identity
;; substitution. 

(call-static 'System.Console 'Write ", Unification")

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
;; A Set Contructor
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

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

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Load - external functions and macros
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'WriteLine "] done.\n")

;;(load "unification.ss")
;;(load "sets.ss")
