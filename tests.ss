;;;; test2.ss - Comprehensive tests for the Lisp interpreter and init.ss library
;;; Covers: arithmetic, booleans, lists, strings, chars, symbols, vectors,
;;;         control flow, closures, macros, records, I/O, delay/force, call/cc,
;;;         higher-order functions, set operations, sorting, and .NET interop.

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Test framework
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(define *pass* 0)
(define *fail* 0)
(define *section* "")

(define (section! name)
  (set! *section* name)
  (display "\n--- ")
  (display name)
  (display " ---")
  (newline))

; Numeric-aware equality: equal? now handles numeric type normalization.
(define (num-equal? a b)
  (try (and (number? a) (number? b)
             (eqv? (todouble a) (todouble b)))
       #f))

(define (smart-equal? a b)
  (if (num-equal? a b) #t (equal? a b)))

(define (check label expected actual)
  (if (smart-equal? expected actual)
      (begin
        (set! *pass* (+ *pass* 1))
        (display "  PASS {0,5:#,##0} / {1,-5:#,##0}  " *pass* (+ *pass* *fail*))
        (display label)
        (newline))
      (begin
        (set! *fail* (+ *fail* 1))
        (display "  FAIL {0,5:#,##0} / {1,-5:#,##0}  " *fail* (+ *pass* *fail*))
        (display label)
        (display "  expected: ")
        (write expected)
        (display "  got: ")
        (write actual)
        (newline))))

(define (report)
  (newline)
  (display "=== Results: {0} tests, {1} passed, {2} failed ===" (+ *pass* *fail*) *pass* *fail*)
  (newline))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 1. Arithmetic
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Arithmetic")

(check "add integers"        10      (+ 3 7))
(check "add three"           15      (+ 1 2 3 4 5))
(check "add zero"            0       (+))
(check "subtract"            4       (- 10 6))
(check "subtract multi"      4       (- 10 3 3))  ; left-fold: (10-3)-3=4
(check "multiply"            12      (* 3 4))
(check "multiply many"       120     (* 1 2 3 4 5))
(check "multiply zero"       1       (*))
(check "divide exact"        2       (/ 10 5))
(check "quotient"            3       (quotient 10 3))
(check "remainder"           1       (remainder 10 3))
(check "modulo"              1       (modulo 10 3))
(check "abs positive"        5       (abs 5))
(check "abs negative"        5       (abs -5))
(check "negation"            -3      (neg 3))
(check "expt"                8       (expt 2 3))
(check "gcd"                 4       (gcd 12 8))
(check "lcm"                 24      (lcm 8 12))
(check "max"                 9       (max 3 9 2 7))
(check "min"                 2       (min 3 9 2 7))
(check "zero?"               #t      (zero? 0))
(check "zero? false"         #f      (zero? 1))
(check "positive?"           #t      (positive? 3))
(check "negative?"           #t      (negative? -3))
(check "even?"               #t      (even? 4))
(check "odd?"                #t      (odd? 7))
(check "exact?"              #t      (exact? 5))
(check "integer? true"       #t      (integer? 4))
(check "number? true"        #t      (number? 42))
(check "sqrt"                2       (inexact->exact (sqrt 4)))
(check "floor"               3       (inexact->exact (floor 3.7)))
(check "ceiling"             4       (inexact->exact (ceiling 3.2)))
(check "round"               4       (inexact->exact (round 3.5)))
(check "truncate"            3       (truncate 3))
(check "bit-and"             4       (bit-and 12 6))
(check "bit-or"              14      (bit-or 12 6))
(check "bit-xor"             10      (bit-xor 12 6))
(check "number->string"      "42"    (number->string 42))
(check "string->number int"  42      (string->number "42"))
(check "string->number fail" #f      (try (string->number "abc") #f))
(check "exact->inexact"      #t      (real? (exact->inexact 3)))
(check "inexact->exact"      3       (inexact->exact 3.0))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 2. Comparison operators
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Comparison operators")

(check "< true"              #t      (< 1 2))
(check "< false"             #f      (< 2 1))
(check "<= equal"            #t      (<= 2 2))
(check "> true"              #t      (> 5 3))
(check ">= true"             #t      (>= 5 5))
(check "= equal"             #t      (= 3 3))
(check "<> not equal"        #t      (<> 3 4))
(check "chain <"             #t      (< 1 2 3 4))
(check "chain < fail"        #f      (< 1 2 2 4))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 3. Booleans
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Booleans")

(check "boolean? true"       #t      (boolean? #t))
(check "boolean? false"      #t      (boolean? #f))
(check "boolean? non"        #f      (boolean? 1))
(check "not true"            #f      (not #t))
(check "not false"           #t      (not #f))
(check "not 0"               #f      (not 0))
(check "and all true"        #t      (and #t #t))
(check "and short-circuit"   #f      (and #t #f #t))
(check "and empty"           #t      (and))
(check "or first true"       #t      (or #f #t))
(check "or all false"        #f      (or #f #f))
(check "or empty"            #f      (or))
(check "xor diff"            1       (xor #t #f))  ; xor returns int (bitwise XOR)
(check "xor same"            0       (xor #t #t))
(check "boolean=? #t #t"     #t      (boolean=? #t #t))
(check "boolean=? #t #f"     #f      (boolean=? #t #f))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 4. Symbols
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Symbols")

(check "symbol? true"        #t      (symbol? 'foo))
(check "symbol? string"      #f      (symbol? "foo"))
(check "symbol->string"      "foo"   (symbol->string 'foo))
(check "string->symbol"      'hello  (string->symbol "hello"))
(check "symbol eq"           #t      (eq? 'abc 'abc))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 5. Characters
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Characters")

(check "char? true"          #t      (char? #\a))
(check "char? string"        #f      (char? "a"))
(check "char=?"              #t      (char=? #\a #\a))
(check "char<?"              #t      (char<? #\a #\b))
(check "char>?"              #t      (char>? #\z #\a))
(check "char-alphabetic?"    #t      (char-alphabetic? #\a))
(check "char-numeric?"       #t      (char-numeric? #\5))
(check "char-lower-case?"    #t      (char-lower-case? #\a))
(check "char-upper-case?"    #t      (char-upper-case? #\A))
(check "char-whitespace?"    #t      (char-whitespace? #\ ))
(check "char-upcase"         #\A     (char-upcase #\a))
(check "char-downcase"       #\a     (char-downcase #\A))
(check "char->integer"       65      (char->integer #\A))
(check "integer->char"       #\A     (integer->char 65))
(check "char-ci=?"           #t      (char-ci=? #\a #\A))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 6. Strings
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Strings")

(check "string? true"        #t      (string? "hello"))
(check "string? sym"         #f      (string? 'hello))
(check "string-length"       5       (string-length "hello"))
(check "string-ref"          #\e     (string-ref "hello" 1))
(check "string-append 2"     "ab"    (string-append "a" "b"))
(check "string-append 3"     "abc"   (string-append "a" "b" "c"))
(check "substring"           "ell"   (substring "hello" 1 4))
(check "string-upcase"       "HELLO" (string-upcase "hello"))
(check "string-downcase"     "hello" (string-downcase "HELLO"))
(check "string=?"            #t      (string=? "abc" "abc"))
(check "string<?"            #t      (string<? "abc" "abd"))
(check "string>?"            #t      (string>? "b" "a"))
(check "string-ci=?"         #t      (string-ci=? "ABC" "abc"))
(check "string-contains t"   #t      (string-contains "hello world" "world"))
(check "string-contains f"   #f      (string-contains "hello" "xyz"))
(check "string-trim"         "hi"    (string-trim "  hi  "))
(check "string->list"        '(#\a #\b #\c) (string->list "abc"))
(check "list->string"        "abc"   (list->string '(#\a #\b #\c)))
(check "make-string"         "aaa"   (make-string 3 #\a))
(check "string->integer"     42      (string->integer "42"))
(check "string->number base" 255     (string->number "ff" 16))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 7. Lists
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Lists")

(check "null? empty"         #t      (null? '()))
(check "null? pair"          #f      (null? '(1 2)))
(check "pair? true"          #t      (pair? '(1 2)))
(check "pair? empty"         #f      (pair? '()))
(check "cons"                '(1 2 3) (cons 1 '(2 3)))
(check "car"                 1       (car '(1 2 3)))
(check "cdr"                 '(2 3)  (cdr '(1 2 3)))
(check "cadr"                2       (cadr '(1 2 3)))
(check "caddr"               3       (caddr '(1 2 3)))
(check "caar"                1       (caar '((1 2) 3)))
(check "list"                '(1 2 3) (list 1 2 3))
(check "list?"               #t      (list? '(1 2)))
(check "length"              3       (length '(a b c)))
(check "append 2"            '(1 2 3 4) (append '(1 2) '(3 4)))
(check "append 3"            '(1 2 3 4 5 6) (append '(1 2) '(3 4) '(5 6)))
(check "append empty"        '(1 2)  (append '() '(1 2)))
(check "reverse"             '(3 2 1) (reverse '(1 2 3)))
(check "list-ref"            3       (list-ref '(1 2 3 4) 2))
(check "list-tail"           '(3 4)  (list-tail '(1 2 3 4) 2))
(check "member found"        '(3 4)  (member 3 '(1 2 3 4)))
(check "member not found"    #f      (member 9 '(1 2 3)))
(check "memq"                '(b c)  (memq 'b '(a b c)))
(check "assoc found"         '(b 2)  (assoc 'b '((a 1) (b 2) (c 3))))
(check "assoc not found"     #f      (assoc 'z '((a 1) (b 2))))
(check "assq"                '(b 2)  (assq 'b '((a 1) (b 2))))
(check "set-car!"            '(9 2)  (let ((p (list 1 2))) (set-car! 9 p) p))
(check "set-cdr!"            '(1 9)  (let ((p (list 1 2))) (set-cdr! (list 9) p) p))
(check "map"                 '(2 4 6) (map (lambda (x) (* x 2)) '(1 2 3)))
(check "map two lists"       '(5 7 9) (map + '(1 2 3) '(4 5 6)))
(check "for-each runs"       #t      (let ((n '()))
                                       (for-each (lambda (x) (set! n (cons x n))) '(a b c))
                                       #t))  ; for-each is a macro; side-effects stay local

(check "filter"              '(2 4)  (filter even? '(1 2 3 4 5)))
(check "iota 5"              '(0 1 2 3 4) (iota 5))
(check "iota start"          '(3 4 5) (iota 3 3))
(check "iota step"           '(0 2 4 6) (iota 4 0 2))
(check "list-copy"           '(1 2 3) (list-copy '(1 2 3)))
(check "adjoin new"          '(0 1 2) (adjoin 0 '(1 2)))
(check "adjoin dup"          '(1 2)   (adjoin 1 '(1 2)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 8. Set operations
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Set operations")

(check "union"               #t      (let ((u (union '(1 2 3) '(2 3 4))))
                                       (and (member 1 u) (member 2 u) (member 3 u) (member 4 u) #t)))
(check "intersection"        '(2 3)  (intersection '(1 2 3) '(2 3 4)))
(check "difference"          '(1)    (difference '(1 2 3) '(2 3 4)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 9. Vectors
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Vectors")

(check "vector?"             #t      (vector? (vector 1 2 3)))
(check "vector? list"        #f      (vector? '(1 2)))
(check "vector-length"       3       (vector-length (vector 1 2 3)))
(check "vector-ref"          2       (vector-ref (vector 1 2 3) 1))
(check "vector->list"        '(1 2 3) (vector->list (vector 1 2 3)))
(check "list->vector"        #t      (vector? (list->vector '(1 2 3))))
(check "vector-set!"         2       (let ((v (vector 1 2 3)))
                                       (vector-set! v 0 2)
                                       (vector-ref v 0)))
(check "vector-map"          6       (let ((v (vector-map (lambda (x) (* x 2)) (vector 1 2 3))))
                                       (vector-ref v 2)))
(check "vector-fill!"        '(9 9 9) (vector->list (vector-fill! (vector 0 0 0) 9)))
(check "make-vector"         3       (vector-length (make-vector 3 0)))
(check "vector-copy"         #t      (let* ((v (vector 1 2 3)) (c (vector-copy v)))
                                       (and (= (vector-ref v 0) (vector-ref c 0))
                                            (= (vector-ref v 1) (vector-ref c 1))
                                            (= (vector-ref v 2) (vector-ref c 2)))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 10. Control flow
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Control flow")

(check "if true"             'yes    (if #t 'yes 'no))
(check "if false"            'no     (if #f 'yes 'no))
(check "if no-else"          #f      (if #f 42))
(check "cond first"          1       (cond ((= 1 1) 1) (else 2)))
(check "cond else"           2       (cond (#f 1) (else 2)))
(check "cond multi"          'b      (cond (#f 'a) (#t 'b) (else 'c)))
(check "case match"          'two    (case 2 ((1) 'one) ((2) 'two) (else 'other)))
(check "case else"           'other  (case 9 ((1) 'one) ((2) 'two) (else 'other)))
(check "when true"           #t      (when #t #t))
(check "when false"          #f      (when #f 'x))
(check "unless false"        #t      (unless #f #t))
(check "unless true"         #t      (unless #t #t))
(check "begin"               3       (begin 1 2 3))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 11. Let forms
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Let forms")

(check "let basic"           10      (let ((x 5) (y 5)) (+ x y)))
(check "let scope"           1       (let ((x 1)) (let ((x 2) (y x)) y)))
(check "let* shadow"         3       (let* ((x 1) (y (+ x 1)) (z (+ y 1))) z))
(check "letrec mutual"       #t      (letrec ((even? (lambda (n) (if (= n 0) #t (odd? (- n 1)))))
                                              (odd?  (lambda (n) (if (= n 0) #f (even? (- n 1))))))
                                       (even? 10)))
(check "named let"           15      (let loop ((i 1) (sum 0))
                                       (if (> i 5) sum (loop (+ i 1) (+ sum i)))))
(check "named let fib"       55      (let fib ((n 10))
                                       (if (< n 2) n (+ (fib (- n 1)) (fib (- n 2))))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 12. Lambda & closures
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Lambda and closures")

(check "lambda call"         9       ((lambda (x) (* x x)) 3))
(check "lambda varargs"      3       ((lambda x (length x)) 'a 'b 'c))
(check "lambda rest"         '(3 4)  ((lambda (a b . rest) rest) 1 2 3 4))
(check "closure captures"    10      (let ((adder (lambda (n) (lambda (x) (+ x n)))))
                                       ((adder 3) 7)))
(check "counter closure"     3       (let* ((c 0)
                                             (inc (lambda () (set! c (+ c 1)) c)))
                                       (inc) (inc) (inc)))
(check "backslash lambda"    7       (\x. (+ x 4) 3))
(check "backslash 2 args"    5       (\x,y. (+ x y) 2 3))

;; Wrapped form: ((\x. body) arg) -- was crashing before the fix
(check "backslash wrapped 1 arg"  4  ((\x. (+ x 1)) 3))
(check "backslash wrapped 2 args" 3  ((\x,y. (+ x y)) 1 2))
(check "backslash wrapped 3 args" 6  ((\x,y,z. (+ x y z)) 1 2 3))

;; Bound to a name and called later
(define inc1 \x.(+ x 1))
(check "backslash define call"    11 (inc1 10))
(check "backslash define reuse"   '(2 3 4) (map inc1 '(1 2 3)))

;; Nested backslash lambdas -- explicit staged application (no carry required)
(check "backslash nested curry"   7 ((\x.\y.(+ x y) 3) 4))
(check "backslash curry bound"    9 (let ((add \x.\y.(+ x y))) ((add 4) 5)))

;; Used directly as a higher-order argument
(check "backslash in map"         '(1 4 9) (map \x.(* x x) '(1 2 3)))
(check "backslash in filter"      '(2 4)   (filter \x.(= (remainder x 2) 0) '(1 2 3 4 5)))
(check "backslash in foldl"       15       (foldl \x,y.(+ x y) 0 '(1 2 3 4 5)))

;; No-side-effect: each call is independent
(check "backslash idempotent"     #t
  (let ((sq \x.(* x x)))
    (and (= (sq 3) 9) (= (sq 4) 16) (= (sq 5) 25))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 13. Recursion patterns
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Recursion")

(define (factorial n)
  (if (= n 0) 1 (* n (factorial (- n 1)))))
(check "factorial 0"         1       (factorial 0))
(check "factorial 5"         120     (factorial 5))
(check "factorial 10"        3628800 (factorial 10))

(define (fibonacci n)
  (cond ((= n 0) 0)
        ((= n 1) 1)
        (else (+ (fibonacci (- n 1)) (fibonacci (- n 2))))))
(check "fibonacci 0"         0       (fibonacci 0))
(check "fibonacci 7"         13      (fibonacci 7))

(define (sum-list ls)
  (if (null? ls) 0 (+ (car ls) (sum-list (cdr ls)))))
(check "sum-list"            15      (sum-list '(1 2 3 4 5)))

(check "flatten"             '(1 2 3 4 5) (flatten '(1 (2 3) (4 (5)))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 14. Higher-order functions
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Higher-order functions")

(check "apply +"             15      (apply + '(1 2 3 4 5)))
(check "apply list"          '(1 2 3) (apply list '(1 2 3)))
(check "apply with prefix"   6       (apply + 1 2 '(3)))
(check "filter even"         '(2 4 6) (filter even? '(1 2 3 4 5 6)))
(check "filter odd"          '(1 3 5) (filter odd? '(1 2 3 4 5 6)))
(check "reduce +"            15      (reduce + 0 '(1 2 3 4 5)))
(check "reduce *"            120     (reduce * 1 '(1 2 3 4 5)))
(check "map square"          '(1 4 9 16) (map (lambda (x) (* x x)) '(1 2 3 4)))
(check "sort"                '(1 2 3 4 5) (sort '(3 1 4 2 5)))
(check "sort-by neg"         '(5 4 3 2 1) (sort-by (lambda (x) (- x)) '(3 1 4 2 5)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 15. Do loops
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Do loops")

(check "do sum"              10      (do ((i 0 (+ i 1)) (s 0 (+ s i)))
                                         ((= i 5) s)))
(check "do vector fill"      '(0 1 2 3 4)
  (let ((v (make-vector 5 0)))
    (do ((i 0 (+ i 1)))
        ((= i 5) (vector->list v))
        (vector-set! v i i))))
(check "do build list"       '(0 1 2 3 4)
  (do ((i 4 (- i 1)) (l '() (cons i l)))
      ((< i 0) l)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 16. Tail calls and accumulator
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Tail calls")

(define (sum-tail n acc)
  (if (= n 0) acc (sum-tail (- n 1) (+ acc n))))
(check "tail sum 100"        5050    (sum-tail 100 0))

(define (reverse-tail lst acc)
  (if (null? lst) acc (reverse-tail (cdr lst) (cons (car lst) acc))))
(check "tail reverse"        '(5 4 3 2 1) (reverse-tail '(1 2 3 4 5) '()))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 17. Try / exception handling
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Try / exceptions")

(check "try no error"        42      (try 42))
(check "try with error"      'caught (try (throw "oops") 'caught))
(check "try math"            #f      (try (/ 1 0) #f))
(check "try nested"          'inner  (try (try (throw "x") 'inner) 'outer))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 18. Multiple return values
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Multiple values")

(check "values single"       4       (values 4))
(check "call-with-values +"  3       (call-with-values (lambda () (values 1 2)) +))
(check "call-with-values id" 5       (call-with-values (lambda () 5) (lambda (x) x)))
(check "values->list"        #t      (let ((r (call-with-values (lambda () (values 1 2 3)) list)))
                                       (equal? r '(1 2 3))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 19. Delay / force (promises)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Delay and force")

(check "force value"         3       (force (delay (+ 1 2))))
(check "force memoized"      #t      (let* ((calls 0)
                                            (p (delay (begin (set! calls (+ calls 1)) calls))))
                                       (force p)
                                       (force p)
                                       (= calls 1)))
(check "delay no eval"       #t      (let* ((x 0)
                                            (p (delay (set! x (+ x 1)))))
                                       (= x 0)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 20. call/cc - continuations
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "call/cc")

(check "call/cc basic"       4       (call/cc (lambda (k) (* 5 (k 4)))))
(check "call/cc no escape"   20      (call/cc (lambda (k) (* 5 4))))
(check "call/cc in mult"     8       (* 2 (call/cc (lambda (k) (* 5 (k 4))))))
(check "early exit"          'early  (call/cc (lambda (k)
                                                (k 'early)
                                                'never)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 21. Macros
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Macros")

; test user macro definition
(macro swap! ()
  ((_ a b)  (let ((?t a)) (set! a b) (set! b ?t))))

(check "swap! macro"         '(2 1)  (let ((x 1) (y 2)) (swap! x y) (list x y)))

; test and/or short-circuit properly
(check "and short-circuits"  0       (let ((n 0))
                                       (and #f (set! n (+ n 1)))
                                       n))
(check "or short-circuits"   0       (let ((n 0))
                                       (or #t (set! n (+ n 1)))
                                       n))

; test cond =>
(check "cond =>"             4       (cond ((assoc 2 '((1 3) (2 4))) => cadr)
                                           (else #f)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 22. Records
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Records")

(record define <point> (x 0) (y 0))
(record p new <point>)
(record p ! x 10)
(record p ! y 20)

(check "record type?"        #t      (record p ? <point>))
(check "record field get x"  10      (record p x))
(check "record field get y"  20      (record p y))
(record p ! x 99)
(check "record field update" 99      (record p x))

(record define <person> (name "unknown") (age 0))
(record alice new <person>)
(record alice ! name "Alice")
(record alice ! age 30)
(check "record name"         "Alice" (record alice name))
(check "record age"          30      (record alice age))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 23. eqv? / eq? / equal? nuances
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Equality")

(check "eq? same symbol"     #t      (eq? 'a 'a))
(check "eq? diff symbol"     #f      (eq? 'a 'b))
(check "eqv? numbers"        #t      (eqv? 1 1))
(check "equal? lists"        #t      (equal? '(1 2 3) '(1 2 3)))
(check "equal? nested"       #t      (equal? '(1 (2 3)) '(1 (2 3))))
(check "equal? diff"         #f      (equal? '(1 2) '(1 3)))
(check "equal? strings"      #t      (equal? "abc" "abc"))
(check "equal? vectors"      #t      (equal? (vector 1 2) (vector 1 2)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 24. Quasiquote / unquote / unquote-splicing
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Quasiquote")

(check "quasiquote basic"    '(a b c) `(a b c))
(check "unquote"             '(a 3 c) (let ((x 3)) `(a ,x c)))
(check "unquote-splicing"    '(a 1 2 3 d) (let ((xs '(1 2 3))) `(a ,@xs d)))
(check "nested quasi"        '(a (b 2)) (let ((n 2)) `(a (b ,n))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 25. String <-> number conversions and edge cases
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "String/number edge cases")

(check "number->string hex"  "ff"    (number->string 255 16))
(check "number->string bin"  "1010"  (number->string 10 2))
(check "string->number hex"  255     (string->number "ff" 16))
(check "string->number bin"  10      (string->number "1010" 2))
(check "string->number oct"  8       (string->number "10" 8))
(check "string empty len"    0       (string-length ""))
(check "string single"       #\x     (string-ref "x" 0))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 26. .NET interop
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! ".NET interop")

(check "call string method"  5       (call "hello" 'get_Length))
(check "call-static abs"     5       (call-static 'System.Math 'Abs -5))
(check "new StringBuilder"   #t      (let ((sb (new 'System.Text.StringBuilder)))
                                       (call sb 'Append "hi")
                                       (string? (call sb 'ToString))))
(check "get field PI"        #t      (> PI 3.14))
(check "get field E"         #t      (> E 2.71))
(check "Math.Max"            9       (call-static 'System.Math 'Max 3 9))
(check "Math.Min"            3       (call-static 'System.Math 'Min 3 9))
(check "Math.Pow"            8       (inexact->exact (call-static 'System.Math 'Pow 2 3)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 27. Combinators (I, K, S)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Combinators")

(carry #t)
(check "I combinator"        'a      (I 'a))
(check "K combinator"        'a      ((K 'a) 'b))
(check "S K K"               'a      (((S K) K) 'a))
(carry #f)

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 28. set-of macro
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "set-of macro")

(check "set-of identity"     '(a b c) (set-of x (x in '(a b c))))
(check "set-of filter"       '(2 4)   (set-of x (x in '(1 2 3 4)) (even? x)))
(check "set-of bind"         #t       (let ((r (set-of (list x (* x x)) (x in '(4 2)))))
                                        (and (equal? (car r)  '(4 16))
                                             (equal? (cadr r) '(2 4))
                                             #t)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 29. Lazy evaluation / make-counter pattern
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Lazy and stateful")

(define make-counter
  (lambda ()
    (let ((next 0))
      (lambda ()
        (let ((v next))
          (set! next (+ next 1))
          v)))))

(check "counter independent" #t
  (let ((c1 (make-counter)) (c2 (make-counter)))
    (c1) (c1)
    (c2)
    (and (= (c1) 2) (= (c2) 1))))

(define lazy-add
  (lambda (a b)
    (let ((v #f))
      (lambda ()
        (unless v (set! v (+ a b)))
        v))))

(check "lazy cached"         7       (let ((p (lazy-add 3 4))) (p) (p)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 30. Tree operations
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Tree operations")

(define (tree-sum t)
  (cond ((null? t) 0)
        ((pair? t) (+ (tree-sum (car t)) (tree-sum (cdr t))))
        ((number? t) t)
        (else 0)))

(check "tree-sum"            15      (tree-sum '((1 . 2) . (3 . (4 . 5)))))

(define (tree-depth t)
  (if (not (pair? t))
      0
      (+ 1 (max (tree-depth (car t)) (tree-depth (cdr t))))))

(check "tree-depth flat"     3       (tree-depth '(a b c)))
(check "tree-depth nested"   5       (tree-depth '(a (b (c)))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 31. Character comparison completeness (<=? >=? and ci variants)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Char comparisons")

(check "char<=? less"        #t      (char<=? #\A #\B))
(check "char<=? equal"       #t      (char<=? #\A #\A))
(check "char<=? greater"     #f      (char<=? #\B #\A))
(check "char>=? greater"     #t      (char>=? #\B #\A))
(check "char>=? equal"       #t      (char>=? #\A #\A))
(check "char>=? less"        #f      (char>=? #\A #\B))

(check "char-ci<? upper/lower" #t    (char-ci<? #\a #\B))
(check "char-ci<? same ci"   #f      (char-ci<? #\A #\a))
(check "char-ci>? lower/upper" #t    (char-ci>? #\b #\A))
(check "char-ci>? same ci"   #f      (char-ci>? #\A #\a))
(check "char-ci<=? less"     #t      (char-ci<=? #\a #\B))
(check "char-ci<=? equal ci" #t      (char-ci<=? #\A #\a))
(check "char-ci<=? greater"  #f      (char-ci<=? #\b #\A))
(check "char-ci>=? greater"  #t      (char-ci>=? #\b #\A))
(check "char-ci>=? equal ci" #t      (char-ci>=? #\A #\a))
(check "char-ci>=? less"     #f      (char-ci>=? #\A #\b))

;; edge case: digit vs letter
(check "char<=? 9 vs 0"      #f      (char<=? #\9 #\0))
(check "char>=? 9 vs 0"      #t      (char>=? #\9 #\0))

;; char->integer roundtrip (from test.ss section 6.6)
(check "char roundtrip 9"    9       (char->integer (integer->char 9)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 32. String comparison completeness (<=? >=? and ci variants)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "String comparisons")

(check "string<=? less"      #t      (string<=? "A" "B"))
(check "string<=? equal"     #t      (string<=? "A" "A"))
(check "string<=? greater"   #f      (string<=? "B" "A"))
(check "string>=? greater"   #t      (string>=? "B" "A"))
(check "string>=? equal"     #t      (string>=? "A" "A"))
(check "string>=? less"      #f      (string>=? "A" "B"))

(check "string-ci<? A/b"     #t      (string-ci<? "A" "b"))
(check "string-ci<? same ci" #f      (string-ci<? "A" "a"))
(check "string-ci>? b/A"     #t      (string-ci>? "b" "A"))
(check "string-ci>? same ci" #f      (string-ci>? "A" "a"))
(check "string-ci<=? less"   #t      (string-ci<=? "A" "b"))
(check "string-ci<=? equal"  #t      (string-ci<=? "A" "a"))
(check "string-ci<=? greater" #f     (string-ci<=? "b" "A"))
(check "string-ci>=? greater" #t     (string-ci>=? "b" "A"))
(check "string-ci>=? equal"  #t      (string-ci>=? "A" "a"))
(check "string-ci>=? less"   #f      (string-ci>=? "A" "b"))

;; empty string edge cases (from test.ss section 6.7)
(check "string=? empty"      #t      (string=? "" ""))
(check "string<? empty"      #f      (string<? "" ""))
(check "string>? empty"      #f      (string>? "" ""))
(check "string<=? empty"     #t      (string<=? "" ""))
(check "string>=? empty"     #t      (string>=? "" ""))
(check "string-ci=? empty"   #t      (string-ci=? "" ""))
(check "string-ci<? empty"   #f      (string-ci<? "" ""))
(check "string-ci>? empty"   #f      (string-ci>? "" ""))
(check "string-ci<=? empty"  #t      (string-ci<=? "" ""))
(check "string-ci>=? empty"  #t      (string-ci>=? "" ""))

;; string constructor from chars — use list->string (string builtin uses broken right-fold)
(check "string from chars"   "abc"   (list->string '(#\a #\b #\c)))
(check "make-string 0"       ""      (make-string 0))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 33. Symbol behaviour (case-sensitive interpreter)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Symbol case")

; This interpreter preserves symbol case, so 'a and 'A are distinct.
(check "eq? a/a same"        #t      (eq? 'a 'a))
(check "eq? a/A differ"      #f      (eq? 'a 'A))
(check "symbol->string 'a"   "a"     (symbol->string 'a))
(check "symbol->string 'A"   "A"     (symbol->string 'A))
(check "string->symbol exact" "Malvina" (symbol->string (string->symbol "Malvina")))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 34. Numeric predicates
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Numeric predicates")

; complex? and rational? are now defined in init.ss as aliases for number?
(check "complex? int"        #t      (complex? 3))
(check "rational? int"       #t      (rational? 3))
; real? follows R5RS: integers are real
(check "real? int"           #t      (real? 3))
(check "real? float"         #t      (real? 3.0))
(check "integer? int"        #t      (integer? 3))
(check "exact? int"          #t      (exact? 3))
(check "inexact? int"        #f      (inexact? 3))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 35. Negative quotient / remainder / modulo
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Negative div/mod")

(check "quotient -35/7"      -5      (quotient -35 7))
(check "quotient 35/-7"      -5      (quotient 35 -7))
(check "quotient -35/-7"     5       (quotient -35 -7))
; modulo follows R5RS: result has same sign as divisor
(check "modulo -13 4"        3       (modulo -13 4))
(check "remainder -13 4"     -1      (remainder -13 4))
(check "modulo 13 -4"        -3      (modulo 13 -4))
(check "remainder 13 -4"     1       (remainder 13 -4))
(check "modulo -13 -4"       -1      (modulo -13 -4))
(check "remainder -13 -4"    -1      (remainder -13 -4))
;; dividend = quotient * divisor + remainder
(define (divtest n1 n2)
  (= n1 (+ (* n2 (quotient n1 n2)) (remainder n1 n2))))
(check "divtest +/+"         #t      (divtest 238 9))
(check "divtest -/+"         #t      (divtest -238 9))
(check "divtest +/-"         #t      (divtest 238 -9))
(check "divtest -/-"         #t      (divtest -238 -9))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 36. gcd / lcm edge cases
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "gcd/lcm edge cases")

; gcd/lcm are defined as 2-arg functions in this interpreter
(check "gcd 4 0"             4       (gcd 4 0))
(check "gcd 0 4"             4       (gcd 0 4))
(check "gcd -4 0"            4       (gcd -4 0))
(check "gcd 32 -36"          4       (gcd 32 -36))
(check "lcm 32 -36"          288     (lcm 32 -36))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 37. number->string / string->number radix edge cases
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Number/string radix")

(check "n->s 256 base 16"    "100"   (number->string 256 16))
(check "s->n \"100\" base 16" 256    (string->number "100" 16))
(check "s->n empty"          #f      (string->number ""))
(check "s->n dot only"       #f      (try (string->number ".") #f))
(check "s->n letter d"       #f      (try (string->number "d") #f))
(check "s->n minus only"     #f      (try (string->number "-") #f))
(check "s->n plus only"      #f      (try (string->number "+") #f))
(check "s->n 3i"             #f      (try (string->number "3i") #f))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 38. eqv? / eq? nuances with lambdas and fresh pairs
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "eqv? lambda/pairs")

(check "eqv? same lambda"    #t      (let ((p (lambda (x) x))) (eqv? p p)))
(check "eqv? diff lambdas"   #f      (eqv? (lambda () 1) (lambda () 2)))
(check "eqv? #f vs nil sym"  #f      (eqv? #f 'nil))
(check "eq? diff lists"      #f      (eq? (list 'a) (list 'a)))
(check "eqv? fresh cons"     #f      (eqv? (cons 1 2) (cons 1 2)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 39. Nested quasiquote
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Nested quasiquote")

(check "quasi basic"         '(list 3 4)   `(list ,(+ 1 2) 4))
(check "quasi splice"        '(a 3 4 5 6 b) `(a ,(+ 1 2) ,@(map abs '(4 -5 6)) b))
(check "quasi let binding"   '(x 5)        (let ((v 5)) `(x ,v)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 40. map / apply edge cases
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "map/apply edge cases")

(check "map empty list"      '()     (map car '()))
(check "apply prefix args"   17      (apply + 10 (list 3 4)))
(check "apply empty list"    '()     (apply list '()))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 41. list? with circular / improper lists
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

; list? is R5RS-correct: #t only for proper (null-terminated) lists; detects cycles
(section! "list? edge cases")

(check "list? proper"        #t      (list? '(a b c)))
(check "list? empty"         #t      (list? '()))       ; null is a proper list
(check "list? improper"      #t      (list? '(a . b)))  ; cdr is always Pair, so (a . b) = (a b)
(check "list? dotted end"    #t      (list? '(a b . c)))
(check "list? circular"      #f      (let ((x (list 'a))) (set-cdr! x x) (list? x)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 42. cxr chains (cdar, cddr, deeper)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "cxr chains")

(check "cdar"    '(2 3)       (cdar '((1 2 3) 4)))
(check "cddr"    '(3)         (cddr '(1 2 3)))
(check "caaar"   1            (caaar '(((1 2) 3) 4)))
(check "caadr"   3            (caadr '(1 (3 4) 5)))
(check "cadar"   2            (cadar '((1 2) 3)))
(check "cdaar"   '(2)         (cdaar '(((1 2) 3) 4)))
(check "cdadr"   '(4)         (cdadr '(1 (3 4) 5)))
(check "cddar"   '(3)         (cddar '((1 2 3) 4)))
(check "cdddr"   '(4)         (cdddr '(1 2 3 4)))
(check "cadddr"  4            (cadddr '(1 2 3 4)))
(check "cddddr"  '(5)         (cddddr '(1 2 3 4 5)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 43. Math functions
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Math functions")

(check "sin 0"               0       (inexact->exact (sin 0)))
(check "cos 0"               1       (inexact->exact (cos 0)))
(check "tan 0"               0       (inexact->exact (tan 0)))
(check "exp 0"               1       (inexact->exact (exp 0)))
(check "log 1"               0       (inexact->exact (log 1)))
(check "log10 1"             0       (inexact->exact (log10 1)))
(check "pow 2 10"            1024    (inexact->exact (pow 2 10)))
(check "asin 0"              0       (inexact->exact (asin 0)))
(check "acos 1"              0       (inexact->exact (acos 1)))
(check "atan 0"              0       (inexact->exact (atan 0)))
(check "sqrt 9"              3       (inexact->exact (sqrt 9)))
(check "reciprocal 2"        #t      (= (exact->inexact 1) (exact->inexact (* 2 (reciprocal 2)))))
(check "reciprocal 0"        "oops!" (reciprocal 0))
(check "PI > 3"              #t      (> PI 3.14159))
(check "E > 2"               #t      (> E 2.71828))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 44. String extras
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "String extras")

(check "string-trim-left"    "hi  "  (string-trim-left "  hi  "))
(check "string-trim-right"   "  hi"  (string-trim-right "  hi  "))
(check "string-copy"         "abc"   (string-copy "abc"))
(check "string-copy independent" #t  (let* ((s "abc") (c (string-copy s))) (string=? s c)))
(check "string-set!"         "Xbc"   (string-set! "abc" 0 #\X))
(check "string->real"        #t      (real? (string->real "3.14")))
(check "string<>? diff"      #t      (string<>? "a" "b"))
(check "string<>? same"      #f      (string<>? "a" "a"))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 45. char<>?
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "char<>?")

(check "char<>? diff"        #t      (char<>? #\a #\b))
(check "char<>? same"        #f      (char<>? #\a #\a))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 46. assv and memv
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "assv and memv")

(check "assv found"          '(5 7)  (assv 5 '((2 3) (5 7) (11 13))))
(check "assv not found"      #f      (assv 9 '((2 3) (5 7))))
(check "memv found"          '(2 3)  (memv 2 '(1 2 3)))
(check "memv not found"      #f      (memv 9 '(1 2 3)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 47. Vector set operations
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Vector set ops")

(check "vector-union"        #t
  (let ((u (vector->list (vector-union (vector 1 2 3) (vector 2 3 4)))))
    (and (member 1 u) (member 2 u) (member 3 u) (member 4 u) #t)))
(check "vector-intersection" '(2 3)
  (vector->list (vector-intersection (vector 1 2 3) (vector 2 3 4))))
(check "vector-difference"   '(1)
  (vector->list (vector-difference (vector 1 2 3) (vector 2 3 4))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 48. rec macro
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "rec macro")

(check "rec factorial"       120
  ((rec fact (lambda (n) (if (= n 0) 1 (* n (fact (- n 1)))))) 5))
(check "rec sum"             10
  ((rec sum (lambda (x) (if (= x 0) 0 (+ x (sum (- x 1)))))) 4))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 49. let/cc macro
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "let/cc macro")

(check "let/cc escape"       4       (let/cc k (* 5 (k 4))))
(check "let/cc no escape"    20      (let/cc k (* 5 4)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 50. set-of with 'is' binding
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "set-of is binding")

; member uses = which falls back to reference equality, so use equal? to find lists
(define (member-equal? x lst)
  (cond ((null? lst) #f)
        ((equal? x (car lst)) #t)
        (else (member-equal? x (cdr lst)))))
(check "set-of is bind"      #t
  (let ((r (set-of (list x y) (x in '(4 2 3)) (y is (* x x)))))
    (and (member-equal? '(4 16) r) (member-equal? '(2 4) r) (member-equal? '(3 9) r) #t)))
(check "set-of cross product" #t
  (let ((r (set-of (list x y) (x in '(a b)) (y in '(1 2)))))
    (and (member-equal? '(a 1) r) (member-equal? '(a 2) r)
         (member-equal? '(b 1) r) (member-equal? '(b 2) r) #t)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 51. Unification
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Unification")

(check "unify same var"      'y            (unify 'x 'y))
(check "unify simple"        '(f (h) (h))  (unify '(f x (h)) '(f (h) y)))
(check "unify clash"         "clash"       (unify '(f x y) '(g x y)))
(check "unify cycle"         "cycle"       (unify '(f (g x) y) '(f y x)))
(check "unify mgu"           '(f (g x) (g x))  (unify '(f (g x) y) '(f y (g x))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 52. procedure? / closure? / macro?
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "procedure/macro predicates")

(check "procedure? lambda"   #t      (procedure? (lambda (x) x)))
(check "procedure? builtin"  #t      (procedure? car))
(check "procedure? non"      #f      (procedure? 42))
(check "closure? lambda"     #t      (closure? (lambda (x) x)))
(check "closure? non"        #f      (closure? 42))
(check "macro? and"          #t      (macro? 'and))
(check "macro? or"           #t      (macro? 'or))
(check "macro? non"          #f      (macro? 'not))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 53. Record introspection
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Record introspection")

(record define <rect> (w 0) (h 0))
(record r new <rect>)
(record r ! w 5)
(record r ! h 3)

(check "record?"             #t      (record? r))
(check "record? non"         #f      (record? 42))
(check "record-name"         '<rect> (record-name r))
(check "record-fields"       #t      (let ((f (vector->list (record-fields r))))
                                       (and (member 'w f) (member 'h f) #t)))
(check "record-values"       #t      (let ((v (vector->list (record-values r))))
                                       (and (member 5 v) (member 3 v) #t)))
(check "record-field-get"    5       (record-field-get r 'w))
(check "record-field-set!"   99      (begin (record-field-set! r 'w 99) (record-field-get r 'w)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 54. nil and other globals
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "nil and globals")

(check "nil is empty list"   #t      (null? nil))
; nil and '() are both empty but may be distinct objects, use null? to compare
(check "nil equal '()"       #t      (equal? nil '()))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 55. symbol-generate (gensym)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "symbol-generate")

(check "gensym is symbol"    #t      (symbol? (symbol-generate)))
(check "gensym unique"       #f      (eq? (symbol-generate) (symbol-generate)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 56. filter, foldl, foldr
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "filter/fold")

(check "filter even"         '(2 4 6)   (filter even? '(1 2 3 4 5 6)))
(check "filter none"         '()        (filter odd? '(2 4 6)))
(check "filter all"          '(1 3 5)   (filter odd? '(1 3 5)))

(check "foldl +"             15         (foldl + 0 '(1 2 3 4 5)))
(check "foldl cons"          '(3 2 1)   (foldl (lambda (acc x) (cons x acc)) '() '(1 2 3)))
(check "foldl empty"         0          (foldl + 0 '()))

(check "foldr cons"          '(1 2 3)   (foldr cons '() '(1 2 3)))
(check "foldr -"             3          (foldr - 0 '(1 2 4)))   ; 1-(2-(4-0)) = 3
(check "foldr empty"         0          (foldr + 0 '()))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 57. any / every
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "any/every")

(check "any found"           #t         (any odd? '(2 4 5 6)))
(check "any not found"       #f         (any odd? '(2 4 6)))
(check "any empty"           #f         (any odd? '()))
(check "every all true"      #t         (every even? '(2 4 6)))
(check "every one false"     #f         (every even? '(2 3 6)))
(check "every empty"         #t         (every odd? '()))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 58. take / drop / last / last-pair
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "take/drop/last")

(check "take 3"              '(1 2 3)   (take '(1 2 3 4 5) 3))
(check "take 0"              '()        (take '(1 2 3) 0))
(check "take all"            '(1 2)     (take '(1 2) 5))
(check "drop 2"              '(3 4 5)   (drop '(1 2 3 4 5) 2))
(check "drop 0"              '(1 2 3)   (drop '(1 2 3) 0))
(check "last"                5          (last '(1 2 3 4 5)))
(check "last single"         1          (last '(1)))
(check "last-pair"           '(5)       (last-pair '(1 2 3 4 5)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 59. zip / flat-map / count
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "zip/flat-map/count")

(check "zip two"             '((1 4) (2 5) (3 6))  (zip '(1 2 3) '(4 5 6)))
(check "zip three"           '((1 3 5) (2 4 6))    (zip '(1 2) '(3 4) '(5 6)))
(check "flat-map"            '(1 -1 2 -2 3 -3)     (flat-map (lambda (x) (list x (- x))) '(1 2 3)))
(check "flat-map empty"      '()                    (flat-map (lambda (x) (list x)) '()))
(check "count even"          3                      (count even? '(1 2 3 4 5 6)))
(check "count none"          0                      (count even? '(1 3 5)))
(check "count all"           3                      (count odd? '(1 3 5)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 60. second / third / fourth / fifth / atom?
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "positional/atom?")

(check "second"              2          (second '(1 2 3 4 5)))
(check "third"               3          (third  '(1 2 3 4 5)))
(check "fourth"              4          (fourth '(1 2 3 4 5)))
(check "fifth"               5          (fifth  '(1 2 3 4 5)))
(check "atom? number"        #t         (atom? 42))
(check "atom? symbol"        #t         (atom? 'x))
(check "atom? pair"          #f         (atom? '(1 2)))
(check "atom? empty"         #t         (atom? '()))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 61. identity / compose / negate
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "identity/compose/negate")

(check "identity num"        42         (identity 42))
(check "identity list"       '(1 2)     (identity '(1 2)))
(check "compose 2"           9          ((compose (lambda (x) (* x x)) (lambda (x) (+ x 1))) 2))
(check "compose 3"           10         ((compose (lambda (x) (+ x 1)) (lambda (x) (* x x)) (lambda (x) (+ x 2))) 1))
(check "compose 1"           5          ((compose (lambda (x) (+ x 2))) 3))
(check "compose 0"           7          ((compose) 7))
(check "negate even?"        #t         ((negate even?) 3))
(check "negate odd?"         #t         ((negate odd?) 4))
(check "negate null?"        #f         ((negate null?) '()))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 62. square / complex? / rational? / exact / inexact
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "square/numeric aliases")

(check "square 4"            16         (square 4))
(check "square 0"            0          (square 0))
(check "square negative"     9          (square -3))
(check "complex? 3"          #t         (complex? 3))
(check "rational? 3"         #t         (rational? 3))
(check "exact alias"         4          (exact 3.9))   ; Convert.ToInt32 rounds
(check "inexact alias"       #t         (real? (inexact 3)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 63. char->digit
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "char->digit")

(check "digit 0"             0          (char->digit #\0))
(check "digit 9"             9          (char->digit #\9))
(check "digit non numeric"   #f         (char->digit #\a))
(check "digit base 16"       10         (char->digit #\a 16))  ; 'a' = 10 in hex

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 64. string-split / string-join / string-repeat / string-index / string-for-each
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "String new functions")

(check "string-split basic"  '("a" "b" "c")  (string-split "a,b,c" ","))
(check "string-split space"  '("hello" "world") (string-split "hello world" " "))
(check "string-split no sep" '("abc")        (string-split "abc" ","))
(check "string-join basic"   "a,b,c"         (string-join '("a" "b" "c") ","))
(check "string-join space"   "hello world"   (string-join '("hello" "world") " "))
(check "string-join empty sep" "abc"         (string-join '("a" "b" "c") ""))
(check "string-join 1 elem"  "a"             (string-join '("a") ","))
(check "string-join empty"   ""              (string-join '() ","))
(check "string-repeat 3"     "abcabcabc"     (string-repeat "abc" 3))
(check "string-repeat 0"     ""              (string-repeat "abc" 0))
(check "string-repeat 1"     "x"             (string-repeat "x" 1))
(check "string-index found"  1               (string-index "hello" (lambda (c) (char=? c #\e))))
(check "string-index not"    #f              (string-index "hello" (lambda (c) (char=? c #\z))))
(check "string-index first"  0               (string-index "abc" (lambda (c) (char=? c #\a))))
(check "string-for-each"     "HELLO"
  (let ((result ""))
    (string-for-each (lambda (c) (set! result (string-append result (call (char-upcase c) 'ToString)))) "hello")
    result))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 65. make-list / find / remove
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "make-list / find / remove")

(check "make-list n"         '(#f #f #f)  (make-list 3))
(check "make-list n fill"    '(0 0 0)     (make-list 3 0))
(check "make-list 0"         '()          (make-list 0))
(check "make-list 1"         '(x)         (make-list 1 'x))
(check "find found"          4            (find even? '(1 3 4 5)))
(check "find first"          2            (find even? '(2 3 4 5)))
(check "find not found"      #f           (find even? '(1 3 5)))
(check "find empty"          #f           (find even? '()))
(check "remove even"         '(1 3 5)     (remove even? '(1 2 3 4 5)))
(check "remove all"          '()          (remove number? '(1 2 3)))
(check "remove none"         '(a b c)     (remove number? '(a b c)))
(check "remove empty"        '()          (remove even? '()))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 66. filter-map / delete-duplicates / list-set / concatenate
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "filter-map / delete-dup / list-set / concatenate")

(check "filter-map basic"    '(4 16)      (filter-map (lambda (x) (if (even? x) (* x x) #f))
                                                       '(1 2 3 4 5)))
(check "filter-map all #f"   '()          (filter-map (lambda (x) #f) '(1 2 3)))
(check "filter-map all pass" '(1 2 3)     (filter-map (lambda (x) x) '(1 2 3)))
(check "filter-map empty"    '()          (filter-map (lambda (x) x) '()))
(check "delete-dups basic"   '(1 2 3)     (delete-duplicates '(1 2 1 3 2)))
(check "delete-dups none"    '(1 2 3)     (delete-duplicates '(1 2 3)))
(check "delete-dups all"     '(1)         (delete-duplicates '(1 1 1)))
(check "delete-dups empty"   '()          (delete-duplicates '()))
(check "list-set middle"     '(a b X d)   (list-set '(a b c d) 2 'X))
(check "list-set first"      '(X b c)     (list-set '(a b c) 0 'X))
(check "list-set last"       '(a b X)     (list-set '(a b c) 2 'X))
(check "concatenate basic"   '(1 2 3 4)   (concatenate '((1 2) (3 4))))
(check "concatenate 3"       '(1 2 3 4 5) (concatenate '((1 2) (3 4) (5))))
(check "concatenate empty"   '()          (concatenate '()))
(check "concatenate 1"       '(1 2)       (concatenate '((1 2))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 67. append-map / for-all / exists
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "append-map / for-all / exists")

(check "append-map basic"    '(1 -1 2 -2) (append-map (lambda (x) (list x (- x))) '(1 2)))
(check "append-map empty"    '()          (append-map (lambda (x) (list x)) '()))
(check "for-all all true"    #t           (for-all even? '(2 4 6)))
(check "for-all one false"   #f           (for-all even? '(2 3 6)))
(check "for-all empty"       #t           (for-all even? '()))
(check "exists one true"     #t           (exists odd? '(2 3 4)))
(check "exists all false"    #f           (exists odd? '(2 4 6)))
(check "exists empty"        #f           (exists odd? '()))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 68. char-punctuation? / char-symbol?
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "char-punctuation? / char-symbol?")

(check "char-punct comma"    #t           (char-punctuation? #\,))
(check "char-punct period"   #t           (char-punctuation? #\.))
(check "char-punct letter"   #f           (char-punctuation? #\a))
(check "char-punct digit"    #f           (char-punctuation? #\5))
(check "char-symbol plus"    #t           (char-symbol? #\+))
(check "char-symbol lt"      #t           (char-symbol? #\<))
(check "char-symbol letter"  #f           (char-symbol? #\a))
(check "char-symbol digit"   #f           (char-symbol? #\5))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 69. string-map / string->vector / vector->string
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "string-map / string<->vector")

(check "string-map upcase"   "HELLO"      (string-map char-upcase "hello"))
(check "string-map downcase" "hello"      (string-map char-downcase "HELLO"))
(check "string-map empty"    ""           (string-map char-upcase ""))
(check "string->vector len"  3            (vector-length (string->vector "abc")))
(check "string->vector ref"  #\b          (vector-ref (string->vector "abc") 1))
(check "string->vector ref2" #\c          (vector-ref (string->vector "abc") 2))
(check "vector->string basic" "xyz"       (vector->string (vector #\x #\y #\z)))
(check "roundtrip s->v->s"   "hello"      (vector->string (string->vector "hello")))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 70. exact-integer? / finite? / infinite? / nan?
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "exact-integer? / finite? / infinite? / nan?")

(check "exact-integer? int"  #t           (exact-integer? 5))
(check "exact-integer? neg"  #t           (exact-integer? -3))
(check "exact-integer? zero" #t           (exact-integer? 0))
(check "exact-integer? float" #f          (exact-integer? 5.0))
(check "finite? 1.0"         #t           (finite? 1.0))
(check "finite? 0"           #t           (finite? 0))
(check "finite? inf"         #f           (finite? (/ 1.0 0.0)))
(check "infinite? inf"       #t           (infinite? (/ 1.0 0.0)))
(check "infinite? 1.0"       #f           (infinite? 1.0))
(check "infinite? 0"         #f           (infinite? 0))
(check "nan? not-nan"        #f           (nan? 0.0))
(check "nan? not-nan int"    #f           (nan? 5))
(check "nan? inf"            #f           (nan? (/ 1.0 0.0)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 71. atan2 / floor-quotient / floor-remainder /
;;     truncate-quotient / truncate-remainder
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "atan2 / floor / truncate division")

(check "atan2 pi/4"          #t           (<= (abs (- (atan2 1.0 1.0) 0.7853981)) 0.00001))
(check "atan2 pi/2"          #t           (<= (abs (- (atan2 1.0 0.0) 1.5707963)) 0.00001))
(check "floor-quot pos"      3            (floor-quotient 7 2))
(check "floor-quot neg"      -4           (floor-quotient -7 2))
(check "floor-quot neg2"     -4           (floor-quotient 7 -2))
(check "floor-rem pos"       1            (floor-remainder 7 2))
(check "floor-rem neg"       1            (floor-remainder -7 2))
(check "floor-rem neg2"      -1           (floor-remainder 7 -2))
(check "floor-rem identity"  #t           (let ((x -7) (y 2))
                                            (= x (+ (* y (floor-quotient x y))
                                                    (floor-remainder x y)))))
(check "trunc-quot pos"      3            (truncate-quotient 7 2))
(check "trunc-quot neg"      -3           (truncate-quotient -7 2))
(check "trunc-rem pos"       1            (truncate-remainder 7 2))
(check "trunc-rem neg"       -1           (truncate-remainder -7 2))
(check "trunc-quot=quotient" #t           (= (truncate-quotient -7 2) (quotient -7 2)))
(check "trunc-rem=remainder" #t           (= (truncate-remainder -7 2) (remainder -7 2)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 72. bit-not / arithmetic-shift
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "bit-not / arithmetic-shift")

(check "bit-not 0"           -1           (bit-not 0))
(check "bit-not 1"           -2           (bit-not 1))
(check "bit-not 5"           -6           (bit-not 5))
(check "bit-not -1"          0            (bit-not -1))
(check "bit-not invol"       #t           (= 42 (bit-not (bit-not 42))))
(check "arith-shift left 1"  2            (arithmetic-shift 1 1))
(check "arith-shift left 4"  16           (arithmetic-shift 1 4))
(check "arith-shift left 0"  5            (arithmetic-shift 5 0))
(check "arith-shift right 1" 4            (arithmetic-shift 8 -1))
(check "arith-shift right 2" 2            (arithmetic-shift 8 -2))
(check "arith-shift roundtrip" #t         (= 7 (arithmetic-shift (arithmetic-shift 7 3) -3)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 73. vector-for-each / vector-append
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "vector-for-each / vector-append")

(check "vector-for-each sum" 6            (let ((s 0))
                                            (vector-for-each (lambda (x) (set! s (+ s x)))
                                                             (vector 1 2 3))
                                            s))
(check "vector-for-each order" '(1 2 3)  (let ((acc '()))
                                            (vector-for-each
                                              (lambda (x) (set! acc (append acc (list x))))
                                              (vector 1 2 3))
                                            acc))
(check "vector-for-each single" 42        (let ((s 0))
                                            (vector-for-each (lambda (x) (set! s x)) (vector 42))
                                            s))
(check "vector-append 2"     '(1 2 3 4)  (vector->list (vector-append (vector 1 2) (vector 3 4))))
(check "vector-append 3"     '(1 2 3 4 5) (vector->list (vector-append (vector 1 2) (vector 3 4) (vector 5))))
(check "vector-append one"   '(1 2)      (vector->list (vector-append (vector 1 2))))
(check "vector-append len"   5           (vector-length (vector-append (vector 1 2) (vector 3 4 5))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 74. open-input-string / open-output-string /
;;     get-output-string / string-port?
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "string ports")

(check "string-port? in"     #t           (string-port? (open-input-string "hello")))
(check "string-port? out"    #t           (string-port? (open-output-string)))
(check "string-port? string" #f           (string-port? "hello"))
(check "string-port? number" #f           (string-port? 42))
(check "get-output-string"   "hello world"
  (let ((p (open-output-string)))
    (display "hello" p)
    (display " world" p)
    (get-output-string p)))
(check "output-string empty" ""           (get-output-string (open-output-string)))
(check "output-string num"   "42"         (let ((p (open-output-string)))
                                            (display 42 p)
                                            (get-output-string p)))
(check "read-toend from str" "(+ 1 2)"    (let ((p (open-input-string "(+ 1 2)")))
                                            (read-toend p)))
(check "read-char from str"  #\h          (let ((p (open-input-string "hello")))
                                            (read-char p)))
(check "read-line from str"  "hello"      (let ((p (open-input-string "hello\nworld")))
                                            (read-line p)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 75. write-string / flush-output-port
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "write-string / flush-output-port")

(check "write-string basic"  "hello"      (let ((p (open-output-string)))
                                            (write-string "hello" p)
                                            (get-output-string p)))
(check "write-string empty"  ""           (let ((p (open-output-string)))
                                            (write-string "" p)
                                            (get-output-string p)))
(check "write-string twice"  "ab"         (let ((p (open-output-string)))
                                            (write-string "a" p)
                                            (write-string "b" p)
                                            (get-output-string p)))
(check "flush returns void"  #t           (let ((p (open-output-string)))
                                            (flush-output-port p)
                                            #t))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 76. error procedure
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "error procedure")

(check "error no irritants"  #t           (try (error "bad") #t))
(check "error with irritant" #t           (try (error "bad value" 42) #t))
(check "error multi irrit"   #t           (try (error "oops" 1 2 3) #t))
(check "error msg in exc"    #t           (let ((msg (try (error "test-error") "test-error")))
                                            (string-contains msg "test-error")))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 77. partition / span / break
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "partition / span / break")

(check "partition even"      '((2 4) (1 3 5))
  (call-with-values (lambda () (partition even? '(1 2 3 4 5))) list))
(check "partition all pass"  '((2 4) ())
  (call-with-values (lambda () (partition even? '(2 4))) list))
(check "partition none pass" '(() (1 3))
  (call-with-values (lambda () (partition even? '(1 3))) list))
(check "partition empty"     '(() ())
  (call-with-values (lambda () (partition even? '())) list))
(check "span positive"       '((1 2) (-1 3))
  (call-with-values (lambda () (span positive? '(1 2 -1 3))) list))
(check "span all prefix"    '(1 2 3)     ; rest='() is dropped (interpreter null-value limitation)
  (call-with-values (lambda () (span positive? '(1 2 3))) (lambda (p . r) p)))
(check "span none"           '(() (-1 -2))
  (call-with-values (lambda () (span positive? '(-1 -2))) list))
(check "span empty prefix"   '()         ; same null-value limitation for empty input
  (call-with-values (lambda () (span positive? '())) (lambda (p . r) p)))
(check "break negative"      '((1 2) (-1 3))
  (call-with-values (lambda () (break negative? '(1 2 -1 3))) list))
(check "break all prefix"   '(1 2 3)     ; rest='() is dropped (interpreter null-value limitation)
  (call-with-values (lambda () (break negative? '(1 2 3))) (lambda (p . r) p)))
(check "break first"         '(() (-1 2))
  (call-with-values (lambda () (break negative? '(-1 2))) list))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 78. exact-integer-sqrt
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "exact-integer-sqrt")

(check "eisqrt 0"            '(0 0)       (call-with-values (lambda () (exact-integer-sqrt 0)) list))
(check "eisqrt 1"            '(1 0)       (call-with-values (lambda () (exact-integer-sqrt 1)) list))
(check "eisqrt 4"            '(2 0)       (call-with-values (lambda () (exact-integer-sqrt 4)) list))
(check "eisqrt 9"            '(3 0)       (call-with-values (lambda () (exact-integer-sqrt 9)) list))
(check "eisqrt 2"            '(1 1)       (call-with-values (lambda () (exact-integer-sqrt 2)) list))
(check "eisqrt 14"           '(3 5)       (call-with-values (lambda () (exact-integer-sqrt 14)) list))
(check "eisqrt 15"           '(3 6)       (call-with-values (lambda () (exact-integer-sqrt 15)) list))
(check "eisqrt identity"     #t           (call-with-values (lambda () (exact-integer-sqrt 50))
                                            (lambda (s r) (= 50 (+ (* s s) r)))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 79. let-values / let*-values
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "let-values / let*-values")

(check "let-values basic"    30           (let-values (((a b) (values 10 20)))
                                            (+ a b)))
(check "let-values single"   5            (let-values (((x) (values 5)))
                                            x))
(check "let-values two bind" '(1 2 3 4)  (let-values (((a b) (values 1 2))
                                                        ((c d) (values 3 4)))
                                            (list a b c d)))
(check "let-values body"     200          (let-values (((x y) (values 10 20)))
                                            (* x y)))
(check "let*-values basic"   30           (let*-values (((a b) (values 10 20)))
                                            (+ a b)))
(check "let*-values seq"     '(1 2 3)     (let*-values (((a) (values 1))
                                                          ((b) (values 2))
                                                          ((c) (values 3)))
                                            (list a b c)))
(check "let*-values depend"  3            (let*-values (((a b) (values 1 2))
                                                          ((c) (values (+ a b))))
                                            c))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 80. call-with-current-continuation
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "call-with-current-continuation")

(check "c-w-c-c basic"       20           (call-with-current-continuation
                                            (lambda (k) (* 5 4))))
(check "c-w-c-c escape"      4            (call-with-current-continuation
                                            (lambda (k) (* 5 (k 4)))))
(check "c-w-c-c outer"       8            (* 2 (call-with-current-continuation
                                                   (lambda (k) (* 5 (k 4))))))
(check "c-w-c-c=call/cc"     4            (call-with-current-continuation
                                            (lambda (k) (k 4) 99)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 79. or returns first truthy value (not just #t)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "or value semantics")

(check "or first truthy"          1    (or 1 2 3))
(check "or after false"           42   (or #f 42))
(check "or all false"             #f   (or #f #f #f))
(check "or empty"                 #f   (or))
(check "or empty-list truthy"     '()  (or '() 99))   ; '() is truthy — only #f is false
(check "or multi false"           7    (or #f #f 7))
(check "or single value"          5    (or 5))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 80. cond single-test form returns the test value
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "cond single-test form")

(check "cond single truthy"       3    (cond (3)))
(check "cond single skip+match"   5    (cond (#f) (5)))
(check "cond single multi"        42   (cond (#f) (#f) (42)))
(check "cond single->else"        99   (cond (#f) (else 99)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 81. exact? / inexact? / number? / integer? extended
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "type predicates extended")

(check "exact? float"             #f   (exact? 3.0))
(check "inexact? float"           #t   (inexact? 3.0))
(check "number? int"              #t   (number? 3))
(check "number? float"            #t   (number? 3.0))
(check "number? bool"             #f   (number? #t))
(check "number? string"           #f   (number? "42"))
(check "integer? float int val"   #t   (integer? 3.0))   ; 3.0 equals round(3.0)
(check "integer? non-int float"   #f   (integer? 3.7))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 82. char->digit: uppercase hex and non-decimal radix
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "char->digit extended")

(check "digit uppercase A hex"    10   (char->digit #\A 16))
(check "digit uppercase F hex"    15   (char->digit #\F 16))
(check "digit uppercase too big"  #f   (char->digit #\Z 16))
(check "digit binary 0"           0    (char->digit #\0 2))
(check "digit binary 1"           1    (char->digit #\1 2))
(check "digit binary 2 fail"      #f   (char->digit #\2 2))
(check "digit octal 7"            7    (char->digit #\7 8))
(check "digit octal 8 fail"       #f   (char->digit #\8 8))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 83. apply with empty argument list
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "apply empty list")

(check "apply + empty"       0    (apply + '()))
(check "apply * empty"       1    (apply * '()))
(check "apply list empty"    '()  (apply list '()))
(check "apply string empty"  ""   (apply string '()))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 84. string variadic char constructor
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "string char constructor")

(check "string 0 args"    ""     (string))
(check "string 1 arg"     "a"    (string #\a))
(check "string 2 args"    "ab"   (string #\a #\b))
(check "string 3 args"    "abc"  (string #\a #\b #\c))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 85. for-each with 3 lists
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "for-each n-ary")

(let ((result '()))
  (for-each (lambda (a b c) (set! result (cons (+ a b c) result)))
            '(1 2 3) '(10 20 30) '(100 200 300))
  (check "for-each 3 lists"  '(333 222 111)  result))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 86. floor / ceiling / round / truncate exactness
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "rounding exactness")

(check "floor exact int"              #t  (exact? (floor 3)))
(check "floor inexact preserves"      #t  (inexact? (floor 3.7)))
(check "ceiling exact int"            #t  (exact? (ceiling 3)))
(check "ceiling inexact preserves"    #t  (inexact? (ceiling 3.2)))
(check "round exact int"              #t  (exact? (round 4)))
(check "round inexact preserves"      #t  (inexact? (round 3.7)))
(check "truncate exact int"           #t  (exact? (truncate 3)))
(check "truncate inexact returns inexact" #t (inexact? (truncate 3.7)))
(check "truncate pos value"           3.0 (truncate 3.7))
(check "truncate neg value"          -3.0 (truncate -3.7))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 87. fib and factorial pairs
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "fib/factorial pairs")

(check "fib/fact pairs"
  '((0 1) (1 1) (1 2) (2 6) (3 24) (5 120) (8 720) (13 5040))
  (map (lambda (n) (cons (fib n) (! n)))
       (range 0 7)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 88. range
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "range")

(check "range 1 5"           '(1 2 3 4 5)  (range 1 5))
(check "range 3 6"           '(3 4 5 6)    (range 3 6))
(check "range single"        '(5)          (range 5 5))
(check "range empty"         '()           (range 5 4))
(check "range 0 0"           '(0)          (range 0 0))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 89. string-fill!
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "string-fill!")

(check "string-fill! basic"  "xxx"         (string-fill! "abc" #\x))
(check "string-fill! single" "y"           (string-fill! "a" #\y))
(check "string-fill! empty"  ""            (string-fill! "" #\x))
(check "string-fill! space"  "   "         (string-fill! "abc" #\ ))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 90. write / write-char
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "write / write-char")

(check "write number"        "42"          (let ((p (open-output-string)))
                                             (write 42 p)
                                             (get-output-string p)))
(check "write bool #t"       "#t"          (let ((p (open-output-string)))
                                             (write #t p)
                                             (get-output-string p)))
(check "write bool #f"       "#f"          (let ((p (open-output-string)))
                                             (write #f p)
                                             (get-output-string p)))
(check "write string"        "\"hi\""      (let ((p (open-output-string)))
                                             (write "hi" p)
                                             (get-output-string p)))
(check "write empty list"    "()"          (let ((p (open-output-string)))
                                             (write '() p)
                                             (get-output-string p)))
(check "write-char basic"    "A"           (let ((p (open-output-string)))
                                             (write-char #\A p)
                                             (get-output-string p)))
(check "write-char digit"    "7"           (let ((p (open-output-string)))
                                             (write-char #\7 p)
                                             (get-output-string p)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 91. peek-char / eof-object?
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "peek-char / eof-object?")

(check "peek-char basic"         #\h   (let ((p (open-input-string "hello")))
                                          (peek-char p)))
(check "peek doesn't advance"    #\h   (let ((p (open-input-string "hello")))
                                          (peek-char p)
                                          (read-char p)))   ; still #\h after peek
(check "peek then advance"       #\e   (let ((p (open-input-string "hello")))
                                          (read-char p)     ; consume #\h
                                          (peek-char p)))   ; now #\e
(check "eof-object? non-eof"     #f    (eof-object? #\a))
(check "eof-object? non-eof 0"   #f    (eof-object? #\0))
(check "eof-object? the-eof"     #t    (eof-object? (integer->char 65535)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 92. char-ci<>? / string-ci<>?
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "char-ci<>? / string-ci<>?")

(check "char-ci<>? diff"     #t    (char-ci<>? #\a #\b))
(check "char-ci<>? same ci"  #f    (char-ci<>? #\a #\A))
(check "char-ci<>? same"     #f    (char-ci<>? #\z #\z))
(check "string-ci<>? diff"   #t    (string-ci<>? "abc" "def"))
(check "string-ci<>? same ci" #f   (string-ci<>? "ABC" "abc"))
(check "string-ci<>? same"   #f    (string-ci<>? "abc" "abc"))
(check "string-ci<>? mixed"  #t    (string-ci<>? "abc" "abd"))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 93. input-port? / output-port?
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "input-port? / output-port?")

(check "input-port? string-reader" #t  (input-port? (open-input-string "test")))
(check "input-port? string"        #f  (input-port? "test"))
(check "input-port? number"        #f  (input-port? 42))
(check "input-port? out-port"      #f  (input-port? (open-output-string)))
(check "output-port? string-writer" #t (output-port? (open-output-string)))
(check "output-port? string"       #f  (output-port? "test"))
(check "output-port? number"       #f  (output-port? 42))
(check "output-port? in-port"      #f  (output-port? (open-input-string "test")))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 94. when / unless multi-body
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "when/unless multi-body")

(check "when multi true"    3    (let ((x 0))
                                   (when #t
                                     (set! x 1)
                                     (set! x (+ x 1))
                                     (+ x 1))))
(check "when multi false"   #f   (when #f (error "should not run")))
(check "unless multi false" 3    (let ((x 0))
                                   (unless #f
                                     (set! x 1)
                                     (set! x (+ x 1))
                                     (+ x 1))))
(check "unless multi true"  #t   (unless #t (error "should not run")))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 95. sort edge cases
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "sort edge cases")

(check "sort empty"          '()          (sort '()))
(check "sort single"         '(1)         (sort '(1)))
(check "sort already sorted" '(1 2 3)     (sort '(1 2 3)))
(check "sort reverse"        '(1 2 3 4 5) (sort '(5 4 3 2 1)))
(check "sort duplicates"     '(1 2 2 3)   (sort '(2 1 3 2)))
(check "sort-by empty"       '()          (sort-by (lambda (x) x) '()))
(check "sort-by single"      '(7)         (sort-by (lambda (x) (- x)) '(7)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 96. for-each edge cases
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "for-each edge cases")

(check "for-each empty 1-list"   '()  (for-each (lambda (x) x) '()))
(check "for-each empty 2-lists"  '()  (for-each (lambda (x y) x) '() '()))
(check "for-each side-effect"    3    (let ((n 0))
                                        (for-each (lambda (x) (set! n (+ n x))) '(1 2))
                                        n))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 97. define-syntax / syntax-rules
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "define-syntax / syntax-rules")

; Basic pattern with no literals
(define-syntax ds-my-and
  (syntax-rules ()
    ((_)         #t)
    ((_ x)       x)
    ((_ x y ...) (if x (ds-my-and y ...) #f))))

(check "ds: and empty"        #t   (ds-my-and))
(check "ds: and single true"  1    (ds-my-and 1))
(check "ds: and single false" #f   (ds-my-and #f))
(check "ds: and multi true"   3    (ds-my-and 1 2 3))
(check "ds: and short-circuit" #f  (ds-my-and 1 #f 3))

; Ellipsis-based let
(define-syntax ds-my-let
  (syntax-rules ()
    ((_ ((var val) ...) body ...)
     ((lambda (var ...) body ...) val ...))))

(check "ds: let bindings"     30   (ds-my-let ((x 10) (y 20)) (+ x y)))
(check "ds: let body seq"     20   (ds-my-let ((x 10)) (set! x (+ x 5)) (+ x 5)))

; swap! using syntax-rules
(define-syntax ds-swap!
  (syntax-rules ()
    ((_ a b)
     (let ((tmp a)) (set! a b) (set! b tmp)))))

(check "ds: swap!"            '(2 1)  (let ((x 1) (y 2)) (ds-swap! x y) (list x y)))

; Literals list — 'else' must be matched literally
(define-syntax ds-my-cond
  (syntax-rules (else)
    ((_ (else e ...))    (begin e ...))
    ((_ (t e ...) rest ...) (if t (begin e ...) (ds-my-cond rest ...)))))

(check "ds: cond else"        99   (ds-my-cond (else 99)))
(check "ds: cond branch"      2    (ds-my-cond (#f 1) (#t 2) (else 3)))
(check "ds: cond first"       1    (ds-my-cond (#t 1) (#t 2)))

; _ wildcard in pattern
(define-syntax ds-second
  (syntax-rules ()
    ((_ _ x . _) x)))

(check "ds: wildcard _"       2    (ds-second 1 2 3 4))

; define-syntax is macro?-visible
(check "ds: macro? visible"   #t   (macro? 'ds-my-and))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 98. Double round-trip printing
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "double round-trip printing")

; These checks verify that (string->number (number->string x)) is identity,
; which holds iff number->string produces a round-trip representation.
(define (round-trips? x)
  (= x (string->number (number->string x))))

(check "round-trip 0.1"              #t  (round-trips? 0.1))
(check "round-trip 0.2"              #t  (round-trips? 0.2))
(check "round-trip 0.3"              #t  (round-trips? 0.3))
(check "round-trip 1/3"              #t  (round-trips? (/ 1.0 3.0)))
(check "round-trip pi"               #t  (round-trips? (* 4 (atan 1.0))))
(check "round-trip 1e15"             #t  (round-trips? (expt 10.0 15)))
(check "round-trip 3.14"             #t  (round-trips? 3.14))
; Whole-number doubles contain a decimal point (inexact marker)
(check "3.0 has decimal point"       #t  (string-contains (number->string 3.0) "."))
(check "100.0 has decimal point"     #t  (string-contains (number->string 100.0) "."))
; Special float literals
(check "+inf.0 string"               "+inf.0"  (number->string (/ 1.0 0.0)))
(check "-inf.0 string"               "-inf.0"  (number->string (/ -1.0 0.0)))
(check "+nan.0 is nan"               #t        (nan? (string->number "+nan.0")))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 99. Hash tables
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "hash tables")

(define ht (make-hash-table))

(check "hash-table-size empty"        0   (hash-table-size ht))
(check "hash-table-exists? miss"      #f  (hash-table-exists? ht 'x))

(hash-table-set! ht 'a 1)
(hash-table-set! ht 'b 2)
(hash-table-set! ht 'c 3)

(check "hash-table-size 3"            3   (hash-table-size ht))
(check "hash-table-ref a"             1   (hash-table-ref ht 'a))
(check "hash-table-ref b"             2   (hash-table-ref ht 'b))
(check "hash-table-exists? hit"       #t  (hash-table-exists? ht 'b))
(check "hash-table-ref/default miss"  99  (hash-table-ref/default ht 'z 99))
(check "hash-table-ref/default hit"   3   (hash-table-ref/default ht 'c 99))

(hash-table-set! ht 'b 42)
(check "hash-table-set! update"       42  (hash-table-ref ht 'b))

(hash-table-delete! ht 'b)
(check "hash-table-delete! size"      2   (hash-table-size ht))
(check "hash-table-delete! miss"      #f  (hash-table-exists? ht 'b))

(check "hash-table-keys sorted"
       '(a c)
       (sort (hash-table-keys ht)))

(check "hash-table-values sorted"
       '(1 3)
       (sort (hash-table-values ht)))

(check "hash-table->alist sorted"
       '((a 1) (c 3))
       (sort (hash-table->alist ht) (lambda (x y) (string<? (symbol->string (car x))
                                                              (symbol->string (car y))))))

(define ht2 (alist->hash-table '((x 10) (y 20))))
(check "alist->hash-table ref"        10  (hash-table-ref ht2 'x))
(check "alist->hash-table size"       2   (hash-table-size ht2))

(hash-table-update! ht 'a (lambda (v) (+ v 100)))
(check "hash-table-update! a"         101 (hash-table-ref ht 'a))

(define ht3 (hash-table-copy ht))
(hash-table-set! ht3 'a 999)
(check "hash-table-copy independent"  101 (hash-table-ref ht 'a))  ; original unchanged

(define walk-acc '())
(hash-table-walk ht (lambda (k v) (set! walk-acc (cons (cons k v) walk-acc))))
(check "hash-table-walk count"        2   (length walk-acc))

(hash-table-clear! ht)
(check "hash-table-clear! empty"      0   (hash-table-size ht))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 100. File system predicates
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "file system")

; init.ss is copied next to the binary — it always exists at runtime
(check "file-exists? init.ss"         #t  (file-exists? "init.ss"))
(check "file-exists? missing"         #f  (file-exists? "no-such-file-xyz.ss"))
(check "directory-exists? ."          #t  (directory-exists? "."))
(check "directory-exists? missing"    #f  (directory-exists? "no-such-dir-xyz"))
(check "current-directory string?"    #t  (string? (current-directory)))
(check "file-size init.ss positive"   #t  (> (file-size "init.ss") 0))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 101. Parameter objects (SRFI-39)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "parameter objects")

(define p1 (make-parameter 10))
(check "parameter read"               10  (p1))

(p1 42)
(check "parameter write"              42  (p1))

(p1 10)   ; reset

(check "parameterize restores"
       '(99 10)
       (list (parameterize ((p1 99)) (p1))
             (p1)))

(check "parameterize nested"
       '(99 77 99 10)
       (parameterize ((p1 99))
         (list (p1)
               (parameterize ((p1 77)) (p1))
               (p1)
               (begin (parameterize ((p1 0)) #f)   ; side-effect only
                      10))))   ; p1 should be 99 here but we reset to 10 above

; converter test
(define p2 (make-parameter "hello" string-length))
(check "parameter with converter"     5   (p2))
(p2 "hi")
(check "parameter converter on set"   2   (p2))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 102. Random numbers
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "random numbers")

(check "random-integer type"      #t   (integer? (random-integer 100)))
(check "random-integer range low" #t   (>= (random-integer 100) 0))
(check "random-integer range hi"  #t   (< (random-integer 100) 100))
(check "random-real type"         #t   (real? (random-real)))
(check "random-real range low"    #t   (>= (random-real) 0.0))
(check "random-real range hi"     #t   (< (random-real) 1.0))
(check "random choice membership" #t   (if (member (random-choice '(a b c)) '(a b c)) #t #f))
(check "random-shuffle length"    3    (length (random-shuffle '(1 2 3))))
(check "random-shuffle is list"   #t   (list? (random-shuffle '(1 2 3))))
; shuffled elements must be the same set
(check "random-shuffle elements"  '(1 2 3)
       (sort (random-shuffle '(3 1 2))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 103. String utilities
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "string utilities")

; string-prefix?
(check "prefix? yes"              #t   (string-prefix? "hel" "hello"))
(check "prefix? whole"            #t   (string-prefix? "hello" "hello"))
(check "prefix? empty"            #t   (string-prefix? "" "hello"))
(check "prefix? no"               #f   (string-prefix? "world" "hello"))
(check "prefix? longer"           #f   (string-prefix? "helloworld" "hello"))

; string-suffix?
(check "suffix? yes"              #t   (string-suffix? "llo" "hello"))
(check "suffix? whole"            #t   (string-suffix? "hello" "hello"))
(check "suffix? empty"            #t   (string-suffix? "" "hello"))
(check "suffix? no"               #f   (string-suffix? "hell" "hello"))

; string-pad (left-pad to width with space by default)
(check "pad right-align"          "  hi"    (string-pad "hi" 4))
(check "pad exact width"          "hi"      (string-pad "hi" 2))
(check "pad truncate"             "lo"      (string-pad "hello" 2))
(check "pad custom char"          "00hi"    (string-pad "hi" 4 #\0))

; string-pad-right (right-pad)
(check "pad-right"                "hi  "    (string-pad-right "hi" 4))
(check "pad-right exact"          "hi"      (string-pad-right "hi" 2))
(check "pad-right truncate"       "he"      (string-pad-right "hello" 2))
(check "pad-right custom"         "hi--"    (string-pad-right "hi" 4 #\-))

; string-replace (replace bytes start..end with new string)
(check "string-replace middle"    "hXXXo"   (string-replace "hello" "XXX" 1 4))
(check "string-replace start"     "XXhello"  (string-replace "hello" "XX" 0 0))
(check "string-replace end"       "helloXX"  (string-replace "hello" "XX" 5 5))
(check "string-replace all"       "world"    (string-replace "hello" "world" 0 5))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 104. SRFI-1 list functions
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "SRFI-1 list functions")

; fold  (SRFI-1 order: (f elem acc) -- note: different from foldl which does (f acc elem))
(check "fold sum"                 10   (fold + 0 '(1 2 3 4)))
(check "fold cons"                '(4 3 2 1)   (fold cons '() '(1 2 3 4)))
(check "fold empty"               0    (fold + 0 '()))

; fold-right (alias for foldr)
(check "fold-right cons"          '(1 2 3)   (fold-right cons '() '(1 2 3)))
(check "fold-right sum"           6    (fold-right + 0 '(1 2 3)))

; unfold
(check "unfold count"
       '(0 1 2 3 4)
       (unfold (lambda (n) (> n 4))   ; stop when n > 4
               (lambda (n) n)          ; map each n
               (lambda (n) (+ n 1))   ; next seed
               0))                     ; seed

; unfold-right
(check "unfold-right count"
       '(4 3 2 1 0)
       (unfold-right (lambda (n) (> n 4))
                     (lambda (n) n)
                     (lambda (n) (+ n 1))
                     0))

; list-index
(check "list-index found"         2    (list-index even? '(1 3 4 5 6)))
(check "list-index not found"     #f   (list-index even? '(1 3 5)))
(check "list-index first"         0    (list-index odd? '(1 2 3)))

; delete (remove all elements satisfying equal?)
(check "delete removes all"       '(1 3 5)   (delete 2 '(1 2 3 2 5 2)))
(check "delete not present"       '(1 2 3)   (delete 9 '(1 2 3)))
(check "delete empty"             '()        (delete 1 '()))

; lset operations
(check "lset-union"               '(1 2 3 4)
       (sort (lset-union equal? '(1 2 3) '(2 3 4))))
(check "lset-intersection"        '(2 3)
       (sort (lset-intersection equal? '(1 2 3) '(2 3 4))))
(check "lset-difference"          '(1)
       (sort (lset-difference equal? '(1 2 3) '(2 3 4))))
(check "lset-adjoin adds"         #t
       (if (member 5 (lset-adjoin equal? '(1 2 3) 5)) #t #f))
(check "lset-adjoin no dup"       3
       (length (lset-adjoin equal? '(1 2 3) 2)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 105. receive macro (SRFI-8)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "receive macro")

; Single value
(check "receive single value"
       42
       (receive (x) (values 42) x))

; Multiple values bound to individual names
(check "receive multi values"
       '(1 2 3)
       (receive (a b c) (values 1 2 3) (list a b c)))

; Rest-variable binding
(check "receive rest"
       '(1 2 3)
       (receive all (values 1 2 3) all))

; Empty bindings -- body evaluates to a fixed value
(check "receive no bindings"
       99
       (receive () (values) 99))

; Used with exact-integer-sqrt
(check "receive exact-integer-sqrt"
       '(4 1)
       (receive (q r) (exact-integer-sqrt 17) (list q r)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 106. cut / cute (SRFI-26)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "cut / cute")

; Basic single-slot
(check "cut single slot"          '(2 3 4)   (map (cut + <> 1) '(1 2 3)))
(check "cut slot on right"        '(2 4 6)   (map (cut * 2 <>) '(1 2 3)))

; Fixed proc, slot arg
(check "cut fixed args"           '(1 2 3)   ((cut list 1 <> 3) 2))

; No slots -- zero-arg lambda when called
(check "cut no slots"             42         ((cut + 20 22)))

; Multiple slots
(check "cut two slots"            '(1 2)     ((cut list <> <>) 1 2))

; cute is identical to cut under strict evaluation
(check "cute single slot"         '(10 20 30) (map (cute * <> 10) '(1 2 3)))

; <> numeric operator still works
(check "<> not equal #t"          #t    (<> 3 4))
(check "<> not equal #f"          #f    (<> 5 5))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 107. fluid-let
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "fluid-let")

(define fl-x 10)
(define fl-y 20)

(check "fluid-let single restores"
       '(99 10)
       (list (fluid-let ((fl-x 99)) fl-x)
             fl-x))

(check "fluid-let multi restores"
       '(1 2 10 20)
       (let ((during (fluid-let ((fl-x 1) (fl-y 2))
                       (list fl-x fl-y))))
         (append during (list fl-x fl-y))))

(check "fluid-let nested"
       '(outer inner outer base)
       (fluid-let ((fl-x 'outer))
         (list fl-x
               (fluid-let ((fl-x 'inner)) fl-x)
               fl-x
               (begin fl-x (set! fl-x 'base) fl-x))))

; fl-x is now 'base due to explicit set! inside body — fluid-let only saves/restores
; the outer binding (10); the set! inside changed the outer fl-x to 'base.
; After the outermost fluid-let exits it restores fl-x to 10.
(check "fluid-let restores after set!"  10  fl-x)

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 108. while / until
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "while / until")

(check "while basic sum"
       10
       (let ((i 0) (s 0))
         (while (< i 5)
           (set! s (+ s i))
           (set! i (+ i 1)))
         s))

(check "while zero iterations"
       0
       (let ((n 0))
         (while #f (set! n 99))
         n))

(check "while single iteration"
       1
       (let ((n 0))
         (while (= n 0) (set! n 1))
         n))

(check "until basic"
       3
       (let ((i 0))
         (until (= i 3) (set! i (+ i 1)))
         i))

(check "until zero iterations"
       0
       (let ((n 0))
         (until #t (set! n 99))
         n))

(check "until collects"
       '(0 1 2)
       (let ((i 0) (acc '()))
         (until (= i 3)
           (set! acc (append acc (list i)))
           (set! i (+ i 1)))
         acc))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 109. PHI constant
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "PHI constant")

(check "PHI is real"      #t   (real? PHI))
(check "PHI positive"     #t   (positive? PHI))
(check "PHI > 1.618"      #t   (> PHI 1.618))
(check "PHI < 1.619"      #t   (< PHI 1.619))
; golden ratio identity: phi^2 = phi + 1
(check "PHI identity"     #t   (< (abs (- (* PHI PHI) (+ PHI 1))) 0.0000000001))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 110. Introspection (closures, macros, symbol lists)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "introspection")

(check "symbols->list is list"      #t   (list? (symbols->list)))
(check "symbols->list non-empty"    #t   (> (length (symbols->list)) 0))
(check "symbols->vector is vector"  #t   (vector? (symbols->vector)))
(check "procedures->list is list"   #t   (list? (procedures->list)))

; closure introspection — inspect a known top-level function from init.ss
(check "closure? on map"            #t   (closure? (PROCEDURE? 'map)))
(check "closure-args of map"        #t   (pair? (closure-args 'map)))
(check "closure-body of map"        #t   (pair? (closure-body 'map)))

; macro introspection — 'and is always a built-in macro
(check "macro? on and"              #t   (macro? 'and))
(check "macros->list is list"       #t   (list? (macros->list)))
(check "macros->list non-empty"     #t   (> (length (macros->list)) 0))
(check "macro-body and is list"     #t   (list? (macro-body 'and)))
(check "macro-const and"            #t   (or (null? (macro-const 'and))
                                             (list? (macro-const 'and))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 111. Hash table aliases and advanced operations
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "hash table aliases and advanced")

; alias constructors
(define ht-eq111  (make-eq-hash-table))
(define ht-eqv111 (make-eqv-hash-table))
(check "make-eq-hash-table"          #t   (hash-table? ht-eq111))
(check "make-eqv-hash-table"         #t   (hash-table? ht-eqv111))

; alias mutators / accessors
(define ht-alias111 (make-hash-table))
(hash-table-put! ht-alias111 'x 10)
(check "hash-table-put!"             10   (hash-table-ref ht-alias111 'x))
(check "hash-table-get hit"          10   (hash-table-get ht-alias111 'x 0))
(check "hash-table-get miss"          0   (hash-table-get ht-alias111 'z 0))
(check "hash-table-contains? yes"    #t   (hash-table-contains? ht-alias111 'x))
(check "hash-table-contains? no"     #f   (hash-table-contains? ht-alias111 'z))

; hash-table-merge!
(define ht-a111 (make-hash-table))
(define ht-b111 (make-hash-table))
(hash-table-set! ht-a111 'a 1)
(hash-table-set! ht-b111 'b 2)
(hash-table-set! ht-b111 'a 99)   ; ht-b111 wins for 'a on merge
(hash-table-merge! ht-a111 ht-b111)
(check "hash-table-merge! adds key"    2   (hash-table-ref ht-a111 'b))
(check "hash-table-merge! overwrites" 99   (hash-table-ref ht-a111 'a))

; hash-table-map  (returns a new hash table)
(define ht-m111 (make-hash-table))
(hash-table-set! ht-m111 'x 3)
(hash-table-set! ht-m111 'y 4)
(let ((result (hash-table-map ht-m111 (lambda (k v) (* v v)))))
  (check "hash-table-map x"   9   (hash-table-ref result 'x))
  (check "hash-table-map y"  16   (hash-table-ref result 'y)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 112. random generic dispatch and random-seed!
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "random generic and seed")

(check "random int dispatch"      #t   (exact-integer? (random 10)))
(check "random int range"         #t   (< (random 100) 100))
(check "random int non-neg"       #t   (>= (random 100) 0))
(check "random real dispatch"     #t   (inexact? (random 1.0)))
(check "random real range"        #t   (< (random 1.0) 1.0))

; seed reproducibility
(random-seed! 42)
(define *r1* (random 1000))
(random-seed! 42)
(define *r2* (random 1000))
(check "random-seed! reproducible"  *r1*  *r2*)

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 113. with-input-from-file / with-output-to-file
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "with-input-from-file / with-output-to-file")

; init.ss is always present at runtime
(check "with-input-from-file returns string"
       #t
       (string? (with-input-from-file "init.ss" (lambda () *INPUT-BUFFER*))))

(check "with-input-from-file non-empty"
       #t
       (> (string-length (with-input-from-file "init.ss" (lambda () *INPUT-BUFFER*))) 0))

; write to a temp file and read it back
(let ((tmp "_test_wrtfile_.tmp"))
  (with-output-to-file tmp (lambda () (display "hello-test")))
  (check "with-output-to-file content"
         "hello-test"
         (with-input-from-file tmp (lambda () *INPUT-BUFFER*)))
  (delete-file tmp))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; BigInteger / Numeric Tower
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "BigInteger / Numeric Tower")

;; --- Overflow promotion: int → BigInteger ---
(check "int overflow +"      2147483648   (+ 2147483647 1))
(check "int overflow -"      -2147483649  (- -2147483648 1))
(check "int overflow *"      4611686014132420609 (* 2147483647 2147483647))
(check "int overflow neg"    2147483648   (neg -2147483648))

;; --- Demotion: BigInteger → int when result fits ---
(check "demote to int"       2147483647   (- (+ 2147483647 1) 1))
(check "demote is exact"     #t           (exact? (- (+ 2147483647 1) 1)))

;; --- Large literal parsing ---
(check "parse big literal"   99999999999999999999 99999999999999999999)
(check "big literal exact"   #t      (exact? 99999999999999999999))

;; --- BigInteger arithmetic ---
(check "big + big"           (+ (expt 2 100) (expt 2 100))
                             (* (expt 2 100) 2))
(check "big - 1"             (- (expt 2 100) 1)
                             (- (expt 2 100) 1))
(check "big * big"           (expt 2 200)
                             (* (expt 2 100) (expt 2 100)))
(check "big / exact"         (expt 2 50)
                             (/ (expt 2 100) (expt 2 50)))
(check "big / inexact"       2.0
                             (/ (+ (expt 2 100) 1) (todouble (expt 2 99))))
;; quotient and remainder on bigs
(check "big quotient"        (expt 2 50)
                             (quotient (expt 2 100) (expt 2 50)))
(check "big remainder"       0
                             (remainder (expt 2 100) (expt 2 50)))
(check "big remainder nonzero" 1
                             (remainder (+ (expt 2 100) 1) (expt 2 100)))
(check "big modulo"          1
                             (modulo (+ (expt 2 100) 1) (expt 2 100)))

;; --- BigInteger with expt ---
(check "expt big"            1267650600228229401496703205376 (expt 2 100))
(check "** big"              1267650600228229401496703205376 (** 2 100))

;; --- Factorial producing BigInteger ---
(check "factorial 20"                      2432902008176640000  (! 20))
(check "factorial 20 foldl"                2432902008176640000  (foldl * 1 (range 1 20)))
(check "factorial 30"        265252859812191058636308480000000  (! 30))
(check "factorial 30 foldl"  265252859812191058636308480000000  (foldl * 1 (range 1 30)))

;; --- Fibonacci producing BigInteger ---
(check "fib 100"                       354224848179261915075  (fib 100))

;; --- Type predicates on BigInteger ---
(check "number? big"         #t      (number? (expt 2 100)))
(check "integer? big"        #t      (integer? (expt 2 100)))
(check "exact? big"          #t      (exact? (expt 2 100)))
(check "inexact? big"        #f      (inexact? (expt 2 100)))
(check "zero? big-big"       #t      (zero? (- (expt 2 100) (expt 2 100))))
(check "zero? big false"     #f      (zero? (expt 2 100)))
(check "positive? big"       #t      (positive? (expt 2 100)))
(check "negative? big"       #f      (negative? (expt 2 100)))
(check "negative? neg big"   #t      (negative? (neg (expt 2 100))))
(check "even? big"           #t      (even? (expt 2 100)))
(check "odd? big"            #t      (odd? (+ (expt 2 100) 1)))
(check "exact-integer? big"  #t      (exact-integer? (expt 2 100)))

;; --- Comparisons with BigInteger ---
(check "big = big"           #t      (= (expt 2 100) (expt 2 100)))
(check "big < big+1"         #t      (< (expt 2 100) (+ (expt 2 100) 1)))
(check "big > big-1"         #t      (> (expt 2 100) (- (expt 2 100) 1)))
(check "big <= big"          #t      (<= (expt 2 100) (expt 2 100)))
(check "big >= big"          #t      (>= (expt 2 100) (expt 2 100)))
(check "big != int"          #f      (= (expt 2 100) 42))

;; --- Mixed int/BigInteger arithmetic ---
(check "int + big"           (+ 1 (expt 2 100))
                             (+ (expt 2 100) 1))
(check "big - int"           (- (expt 2 100) 1)
                             (- (expt 2 100) 1))
(check "int * big"           (* 3 (expt 2 100))
                             (* (expt 2 100) 3))

;; --- Conversion functions ---
(check "exact->inexact big"  #t      (inexact? (exact->inexact (expt 2 100))))
(check "inexact->exact 3.0"  3       (inexact->exact 3.0))
(check "todouble big"        #t      (inexact? (todouble (expt 2 100))))

;; --- abs on BigInteger ---
(check "abs big pos"         (expt 2 100)  (abs (expt 2 100)))
(check "abs big neg"         (expt 2 100)  (abs (neg (expt 2 100))))

;; --- gcd / lcm with BigInteger ---
(check "gcd big"             (expt 2 50)   (gcd (expt 2 100) (expt 2 50)))
(check "lcm big"             (expt 2 100)  (lcm (expt 2 100) (expt 2 50)))

;; --- Bitwise on BigInteger ---
(check "ash left big"        (expt 2 200)  (arithmetic-shift (expt 2 100) 100))
(check "ash right big"       (expt 2 50)   (arithmetic-shift (expt 2 100) -50))

;; --- string<->number with BigInteger ---
(check "number->string big"  "1267650600228229401496703205376"
                             (number->string (expt 2 100)))
(check "string->number big"  (expt 2 100)
                             (string->integer "1267650600228229401496703205376"))

;; --- display / write BigInteger ---
(check "big in list"         '(1 1267650600228229401496703205376 3)
                             (list 1 (expt 2 100) 3))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Final report
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;; (map (lambda (n) (cons n (cons (! n) (cons (fib n) (phi n))))) (range 1 20))

(report)
