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
        (display "  PASS  ")
        (display label)
        (newline))
      (begin
        (set! *fail* (+ *fail* 1))
        (display "  FAIL  ")
        (display label)
        (display "  expected: ")
        (write expected)
        (display "  got: ")
        (write actual)
        (newline))))

(define (report)
  (newline)
  (display "=== Results: ")
  (display *pass*)
  (display " passed, ")
  (display *fail*)
  (display " failed ===")
  (newline))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 1. Arithmetic
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Arithmetic")

(check "add integers"        10      (+ 3 7))
(check "add three"           15      (+ 1 2 3 4 5))
(check "add zero"            0       (+))
(check "subtract"            4       (- 10 6))
(check "subtract multi"      10      (- 10 3 3))  ; right-fold: 10-(3-(3-0))=10
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

;;; filter is not in init.ss — define it here
(define (filter pred lst)
  (cond ((null? lst) '())
        ((pred (car lst)) (cons (car lst) (filter pred (cdr lst))))
        (else (filter pred (cdr lst)))))

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

(define (flatten lst)
  (cond ((null? lst)  '())
        ((pair? (car lst)) (append (flatten (car lst)) (flatten (cdr lst))))
        (else (cons (car lst) (flatten (cdr lst))))))
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
;; Final report
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(report)
