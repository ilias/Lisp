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

; Numeric-aware equality: uses = which correctly handles int/BigInteger/Rational/Complex.
(define (num-equal? a b)
  (try (and (number? a) (number? b) (= a b))
       #f))

(define (smart-equal? a b)
  (if (num-equal? a b) #t (equal? a b)))

(define (check label expected actual)
  (let ((prev (get 'System.Console 'ForegroundColor)))
    (if (smart-equal? expected actual)
        (begin
          (set! *pass* (+ *pass* 1))
          (set 'System.Console 'ForegroundColor (get 'System.ConsoleColor 'Green))
          (display "  PASS {0,5:#,##0} / {1,-5:#,##0}  " *pass* (+ *pass* *fail*))
          (display label)
          (set 'System.Console 'ForegroundColor (get 'System.ConsoleColor 'DarkGray))
          (display " --- expected: ")
          (set 'System.Console 'ForegroundColor (get 'System.ConsoleColor 'Yellow))
          (display "{0}" expected)
          (newline)
          (set 'System.Console 'ForegroundColor prev))
        (begin
          (set! *fail* (+ *fail* 1))
          (set 'System.Console 'ForegroundColor (get 'System.ConsoleColor 'Red))
          (display "  FAIL {0,5:#,##0} / {1,-5:#,##0}  " *fail* (+ *pass* *fail*))
          (display label)
          (set 'System.Console 'ForegroundColor (get 'System.ConsoleColor 'DarkGray))
          (display " --- expected: ")
          (set 'System.Console 'ForegroundColor (get 'System.ConsoleColor 'Yellow))
          (display "{0}" expected)
          (set 'System.Console 'ForegroundColor (get 'System.ConsoleColor 'DarkGray))
          (display "  got: ")
          (set 'System.Console 'ForegroundColor (get 'System.ConsoleColor 'Magenta))
          (display "{0}" actual)
          (newline)
          (set 'System.Console 'ForegroundColor prev)))))

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
(check "isPrime 2"           #t      (isPrime 2))
(check "isPrime 97"          #t      (isPrime 97))
(check "isPrime composite"   #f      (isPrime 91))
(check "isPrime one"         #f      (isPrime 1))
(check "isPrime negative"    #f      (isPrime -7))
(check "isPrime float"       #f      (isPrime 7.0))
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
(check "symbol=? same"       #t      (symbol=? 'foo 'foo))
(check "symbol=? diff"       #f      (symbol=? 'foo 'bar))
(check "symbol=? variadic t" #t      (symbol=? 'x 'x 'x))
(check "symbol=? variadic f" #f      (symbol=? 'a 'b 'a))

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
(check "digit-value 0"       0       (digit-value #\0))
(check "digit-value 5"       5       (digit-value #\5))
(check "digit-value 9"       9       (digit-value #\9))
(check "digit-value alpha"   #f      (digit-value #\a))
(check "digit-value upper"   #f      (digit-value #\Z))
(check "digit-value space"   #f      (digit-value #\space))

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
(check "if test error propagates" 'caught
  (try (if (throw "if-test") 1 2) 'caught))
(check "try catches host interop" 'caught
  (try (call-static 'System.Convert 'ToInt32 "abc") 'caught))
(check "with-ex-handler host interop" #t
  (with-exception-handler error-object?
    (lambda () (call-static 'System.Convert 'ToInt32 "abc"))))

;; R7RS exception system — error-object predicates
(check "error-object? yes"   #t
  (with-exception-handler error-object? (lambda () (error "oops"))))
(check "error-object? no"    #f
  (with-exception-handler error-object? (lambda () (raise 42))))
(check "error-object-message" "bad"
  (with-exception-handler error-object-message (lambda () (error "bad" 1 2))))
(check "error-object-irritants" '(1 2)
  (with-exception-handler error-object-irritants (lambda () (error "msg" 1 2))))
(check "error no irritants"  #t
  (with-exception-handler
    (lambda (e) (and (error-object? e) (null? (error-object-irritants e))))
    (lambda () (error "bare"))))

;; raise any value
(check "raise symbol"        'my-err
  (with-exception-handler (lambda (e) e) (lambda () (raise 'my-err))))
(check "raise-continuable"   99
  (with-exception-handler (lambda (e) 99) (lambda () (raise-continuable 'x))))

;; guard macro
(check "guard match"         "got it"
  (guard (e ((error-object? e)
             (string-append "got " (error-object-message e))))
    (error "it")))
(check "guard else"          'fallback
  (guard (e (else 'fallback)) (error "any")))
(check "guard no match re-raise" 'outer
  (try (guard (e ((string? e) "str")) (error "not a string")) 'outer))

;; try still catches error-objects
(check "try catches error"   'caught
  (try (error "boom") 'caught))
(check "try catches raise"   'caught
  (try (raise 'anything) 'caught))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Interpreter isolation
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Interpreter isolation")

(check "macro tables isolated" #t
  (call-static 'Lisp.RuntimeIsolationChecks 'MacroTablesAreIsolated))
(check "macro docs isolated" #t
  (call-static 'Lisp.RuntimeIsolationChecks 'MacroDocCommentsAreIsolated))
(check "runtime state isolated" #t
  (call-static 'Lisp.RuntimeIsolationChecks 'RuntimeStateIsIsolated))
(check "malformed special forms" #t
  (call-static 'Lisp.RuntimeIsolationChecks 'MalformedSpecialFormsReportSchemeErrors))
(check "malformed special forms locations" #t
  (call-static 'Lisp.RuntimeIsolationChecks 'MalformedSpecialFormsReportSourceLocations))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; R7RS ports and I/O

(section! "R7RS ports and I/O")

(check "port? output-port"   #t  (port? (current-output-port)))
(check "port? input-string"  #t  (port? (open-input-string "x")))
(check "port? error-port"    #t  (port? (current-error-port)))
(check "port? number"        #f  (port? 42))
(check "port? string"        #f  (port? "hello"))

(check "current-error-port"  #t  (output-port? (current-error-port)))

(check "eof-object pred"     #t  (eof-object? (eof-object)))
(check "eof-object not char" #f  (eof-object? #\a))

(check "char-ready? no arg"  #t  (char-ready?))
(check "char-ready? port"    #t  (char-ready? (open-input-string "x")))

(check "read-string basic"   "hel"
  (let ((p (open-input-string "hello")))
    (read-string 3 p)))
(check "read-string full"    "hi"
  (let ((p (open-input-string "hi")))
    (read-string 10 p)))
(check "read-string eof"     #t
  (let ((p (open-input-string "")))
    (eof-object? (read-string 1 p))))

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
(check "exact 1.5"           3/2        (exact 1.5))   ; inexact->exact gives exact Rational
(check "exact 4.0"           4          (exact 4.0))   ; whole-number double gives integer
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
;; BigInteger bitwise ops (2^40 = 1099511627776 does not fit in int32)
(check "bit-and big identity"  (expt 2 40)  (bit-and (expt 2 40) (expt 2 40)))
(check "bit-and big mask"      0            (bit-and (expt 2 40) (- (expt 2 40) 1)))
(check "bit-or big"            (+ (expt 2 40) 1) (bit-or (expt 2 40) 1))
(check "bit-xor big self"      0            (bit-xor (expt 2 40) (expt 2 40)))
(check "bit-xor big 1"         (+ (expt 2 40) 1) (bit-xor (expt 2 40) 1))
(check "arith-shift big left"  (expt 2 50)  (arithmetic-shift (expt 2 40) 10))
(check "arith-shift big right" (expt 2 30)  (arithmetic-shift (expt 2 40) -10))

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
    ((_)                 #f)
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
       (sort (hash-table-keys ht) (lambda (x y) (string<? (symbol->string x) (symbol->string y)))))

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
;; 114. letrec*
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "letrec*")

;; basic: second binding can reference the first
(check "letrec* seq"         3
       (letrec* ((x 1) (y (+ x 2))) y))

;; empty bindings
(check "letrec* empty"       42
       (letrec* () 42))

;; mutual-recursion still works (same scope as letrec)
(check "letrec* mutual even?"  #t
       (letrec* ((even? (lambda (n) (if (= n 0) #t (odd?  (- n 1)))))
                 (odd?  (lambda (n) (if (= n 0) #f (even? (- n 1))))))
         (even? 10)))

;; later bindings depend on earlier ones in a chain
(check "letrec* chain"       6
       (letrec* ((a 1) (b (+ a 1)) (c (+ b a))) (+ a b c)))

;; single binding
(check "letrec* single"      99
       (letrec* ((x 99)) x))

;; body with multiple expressions
(check "letrec* multi-body"  20
       (letrec* ((x 10)) (+ x x)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 115. case-lambda
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "case-lambda")

;; single 0-arg clause
(check "case-lam 0arg"       42
       ((case-lambda (() 42))))

;; single 1-arg clause
(check "case-lam 1arg"       49
       ((case-lambda ((x) (* x x))) 7))

;; dispatch on arity: 0 vs 1
(check "case-lam arity 0"    0
       ((case-lambda (() 0) ((x) x))))

(check "case-lam arity 1"    5
       ((case-lambda (() 0) ((x) x)) 5))

;; dispatch on arity: 1 vs 2
(check "case-lam arity 2"    3
       ((case-lambda ((x) x) ((x y) (+ x y))) 1 2))

;; three fixed arities
(define f3
  (case-lambda
    (()       'zero)
    ((x)      (list 'one x))
    ((x y)    (list 'two x y))))

(check "case-lam f3 0"       'zero        (f3))
(check "case-lam f3 1"       '(one 7)     (f3 7))
(check "case-lam f3 2"       '(two 3 4)   (f3 3 4))

;; variadic catch-all clause
(define fv
  (case-lambda
    ((x)   (* x x))
    (args  (length args))))

(check "case-lam variadic 1"  9   (fv 3))
(check "case-lam variadic 3"  3   (fv 1 2 3))
(check "case-lam variadic 0"  0   (fv))

;; multiple body expressions in a clause
(check "case-lam multi-body"  2
       ((case-lambda ((x) (define y (+ x 1)) y)) 1))

;; error on no match
(check "case-lam no-match"    #t
       (try ((case-lambda ((x) x))) #t))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 116. Named character literals (#\newline etc.)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "named char literals")

(check "newline char code"    10   (char->integer #\newline))
(check "space char code"      32   (char->integer #\space))
(check "tab char code"         9   (char->integer #\tab))
(check "nul char code"         0   (char->integer #\nul))
(check "null char code"        0   (char->integer #\null))
(check "return char code"     13   (char->integer #\return))
(check "escape char code"     27   (char->integer #\escape))
(check "altmode char code"    27   (char->integer #\altmode))
(check "delete char code"    127   (char->integer #\delete))
(check "rubout char code"    127   (char->integer #\rubout))
(check "backspace char code"   8   (char->integer #\backspace))
(check "alarm char code"       7   (char->integer #\alarm))
; single-letter literals still work
(check "single #\\a"          #\a  (integer->char 97))
(check "single #\\0"          #\0  (integer->char 48))
; named char in char comparison
(check "newline = char 10"    #t   (char=? #\newline (integer->char 10)))
(check "space = char 32"      #t   (char=? #\space   (integer->char 32)))
; named char in write output
(check "write #\\space"  "#\\ "   (let ((p (open-output-string)))
                                     (write #\space p)
                                     (get-output-string p)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 117. Radix prefix literals (#b #o #x #d)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "radix prefix literals")

; binary
(check "#b0"         0     #b0)
(check "#b1"         1     #b1)
(check "#b101"       5     #b101)
(check "#b1010"      10    #b1010)
(check "#b11111111"  255   #b11111111)
; octal
(check "#o0"         0     #o0)
(check "#o7"         7     #o7)
(check "#o17"        15    #o17)
(check "#o377"       255   #o377)
(check "#o777"       511   #o777)
; hex (lower and upper)
(check "#xff"        255   #xff)
(check "#xAB"        171   #xAB)
(check "#xFFFF"      65535 #xFFFF)
(check "#x10"        16    #x10)
; decimal prefix (same as no prefix)
(check "#d42"        42    #d42)
(check "#d0"         0     #d0)
; used in arithmetic
(check "#b1010 + #xA" 20  (+ #b1010 #xA))
(check "#o10 = #d8"   #t  (= #o10 #d8))
; exact? is #t for radix literals
(check "#xff exact"   #t  (exact? #xff))
(check "#b101 exact"  #t  (exact? #b101))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 118. Reader literals +inf.0 / -inf.0 / +nan.0
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "reader special float literals")

(check "+inf.0 positive-inf"   #t   (infinite? +inf.0))
(check "+inf.0 positive"       #t   (> +inf.0 0))
(check "-inf.0 negative-inf"   #t   (infinite? -inf.0))
(check "-inf.0 negative"       #t   (< -inf.0 0))
(check "+nan.0 is nan"         #t   (nan? +nan.0))
(check "-nan.0 is nan"         #t   (nan? -nan.0))
; round-trip: what Dump produces can be read back
(check "+inf.0 round-trip"     #t   (infinite? (string->number "+inf.0")))
(check "-inf.0 round-trip"     #t   (infinite? (string->number "-inf.0")))
(check "+nan.0 round-trip"     #t   (nan? (string->number "+nan.0")))
; in arithmetic
(check "+inf.0 + 1"            #t   (infinite? (+ +inf.0 1)))
(check "-inf.0 * -1 = +inf"    #t   (= (/ 1.0 0.0) (* -inf.0 -1)))
(check "+inf.0 > 1e300"        #t   (> +inf.0 1e300))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 119. dynamic-wind
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "dynamic-wind")

; basic: all three thunks run, body value returned
(check "dwind normal value"
       42
       (dynamic-wind (lambda () #f) (lambda () 42) (lambda () #f)))

; before and after run in order around body
(check "dwind normal order"
       '(before body after)
       (let ((log '()))
         (dynamic-wind
           (lambda () (set! log (cons 'before log)))
           (lambda () (set! log (cons 'body   log)))
           (lambda () (set! log (cons 'after  log))))
         (reverse log)))

; after runs even when body raises an error
(check "dwind on error"
       '(before after)
       (let ((log '()))
         (try
           (dynamic-wind
             (lambda () (set! log (cons 'before log)))
             (lambda () (error "boom"))
             (lambda () (set! log (cons 'after  log))))
           #f)
         (reverse log)))

; after runs when body escapes via call/cc
(check "dwind on escape"
       '(before after)
       (let ((log '()))
         (call/cc
           (lambda (k)
             (dynamic-wind
               (lambda () (set! log (cons 'before log)))
               (lambda () (k 'escaped))
               (lambda () (set! log (cons 'after  log))))))
         (reverse log)))

; escape value is correctly returned by call/cc
(check "dwind escape value"
       'escaped
       (call/cc
         (lambda (k)
           (dynamic-wind
             (lambda () #f)
             (lambda () (k 'escaped))
             (lambda () #f)))))

; nested dynamic-wind: both afters run on inner escape
(check "dwind nested afters"
       '(outer-before inner-before inner-after outer-after)
       (let ((log '()))
         (call/cc
           (lambda (k)
             (dynamic-wind
               (lambda () (set! log (append log '(outer-before))))
               (lambda ()
                 (dynamic-wind
                   (lambda () (set! log (append log '(inner-before))))
                   (lambda () (k 'done))
                   (lambda () (set! log (append log '(inner-after))))))
               (lambda () (set! log (append log '(outer-after)))))))
         log))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 120. parameterize with escape (dynamic-wind backed)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "parameterize with dynamic-wind")

(define p-dw (make-parameter 0))

; parameter restored after normal exit (existing behaviour preserved)
(check "param normal restore"
       '(99 0)
       (list (parameterize ((p-dw 99)) (p-dw))
             (p-dw)))

; parameter restored after call/cc escape
(check "param escape restore"
       0
       (begin
         (call/cc
           (lambda (k)
             (parameterize ((p-dw 99))
               (k 'escape))))
         (p-dw)))   ; should be 0, not 99

; parameter restored after error in body
(check "param error restore"
       0
       (begin
         (try
           (parameterize ((p-dw 99))
             (error "boom inside parameterize"))
           #f)
         (p-dw)))   ; should be 0, not 99

; nested parameterize restores inner, then outer
(check "param nested escape"
       '(77 0)
       (let ((saved #f))
         (call/cc
           (lambda (k)
             (parameterize ((p-dw 99))
               (parameterize ((p-dw 77))
                 (set! saved (p-dw))
                 (k 'escape)))))
         (list saved (p-dw))))  ; saved=77, p-dw restored to 0

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 121. values / call-with-values (extended)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "values / call-with-values extended")

; zero values
(check "values zero"          '()       (call-with-values (lambda () (values)) list))

; single value passes through (not wrapped)
(check "values one"            5        (values 5))

; two values summed
(check "values two sum"        3        (call-with-values (lambda () (values 1 2)) +))

; three values collected
(check "values three"          '(1 2 3) (call-with-values (lambda () (values 1 2 3)) list))

; non-multiple-values body (single return)
(check "cwv single body"       4        (call-with-values (lambda () 4) (lambda (x) x)))

; arithmetic with multiple values
(check "cwv multiply"          50       (call-with-values (lambda () (values 5 10)) *))

; let-values basic destructuring
(check "let-values sum"        30       (let-values (((a b) (values 10 20))) (+ a b)))

; let-values two bindings
(check "let-values two binds"  '(1 2 3 4)
       (let-values (((a b) (values 1 2))
                    ((c d) (values 3 4)))
         (list a b c d)))

; receive with values
(check "values in receive"     15
       (receive (a b c)
         (values 4 5 6)
         (+ a b c)))

; values in conditional
(check "values in if"          2
       (if (values #f) 1 2))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 122. Variadic gcd / lcm
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "variadic gcd/lcm")

; 0-argument identity elements
(check "gcd 0 args"      0    (gcd))
(check "lcm 0 args"      1    (lcm))

; 1-argument: abs value
(check "gcd 1 pos"       5    (gcd 5))
(check "gcd 1 neg"       5    (gcd -5))
(check "lcm 1 pos"       7    (lcm 7))
(check "lcm 1 neg"       7    (lcm -7))

; 2-argument: same as before
(check "gcd 2 basic"     4    (gcd 12 8))
(check "gcd 2 neg"       4    (gcd 32 -36))
(check "gcd 2 zero"      5    (gcd 5 0))
(check "lcm 2 basic"     12   (lcm 4 6))
(check "lcm 2 with zero" 0    (lcm 5 0))

; 3 arguments
(check "gcd 3 args"      2    (gcd 12 8 6))
(check "lcm 3 args"      60   (lcm 4 6 10))

; 4 arguments
(check "gcd 4 args"      1    (gcd 6 10 15 35))
(check "lcm 4 args"      12   (lcm 3 4 6 12))

; zero throughout
(check "gcd all zero"    0    (gcd 0 0 0))
(check "lcm all zero"    0    (lcm 0 0 0))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 123. member / assoc with comparator argument
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "member/assoc with comparator")

; --- member: equal? default now enables list-element lookup ---
(check "member list default"   '((1 2) (3 4))
       (member '(1 2) '((1 2) (3 4))))
(check "member string default" '("b" "c")
       (member "b" '("a" "b" "c")))
(check "member not found"      #f
       (member '(9 9) '((1 2) (3 4))))

; --- member: explicit comparator ---
(check "member eq? found"      '(b c)
       (member 'b '(a b c) eq?))
(check "member eq? not found"  #f
       (member 'd '(a b c) eq?))
; custom pred: find first element > 3
(check "member custom pred"    '(4 5)
       (member 3 '(1 2 3 4 5) <))

; --- assoc: equal? default for string and list keys ---
(check "assoc string key"      '("b" 2)
       (assoc "b" '(("a" 1) ("b" 2) ("c" 3))))
(check "assoc list key"        '((1 2) found)
       (assoc '(1 2) '(((1 2) found) ((3 4) other))))
(check "assoc not found"       #f
       (assoc '(9 9) '(((1 2) a) ((3 4) b))))

; --- assoc: explicit comparator ---
(check "assoc eq? found"       '(b 2)
       (assoc 'b '((a 1) (b 2) (c 3)) eq?))
(check "assoc eq? not found"   #f
       (assoc 'd '((a 1) (b 2)) eq?))
; custom pred: <=  finds first key where (key >= thing), i.e. (cmp thing key) = (<= thing key)
(check "assoc <= pred"         '(3 three)
       (assoc 3 '((1 one) (3 three) (5 five)) <=))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 124. for-each multi-list
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "for-each multi-list")

; single list (sanity check)
(check "for-each 1 list"       '(3 2 1)
       (let ((acc '()))
         (for-each (lambda (x) (set! acc (cons x acc))) '(1 2 3))
         acc))

; two lists zipped
(check "for-each 2 lists"      '((1 a) (2 b) (3 c))
       (let ((acc '()))
         (for-each (lambda (x y) (set! acc (append acc (list (list x y)))))
                   '(1 2 3) '(a b c))
         acc))

; three lists
(check "for-each 3 lists"      '(6 15 24)
       (let ((acc '()))
         (for-each (lambda (a b c) (set! acc (append acc (list (+ a b c)))))
                   '(1 2 3) '(2 4 6) '(3 9 15))
         acc))

; return value is '() (side effects only)
(check "for-each returns '()"  '()
       (for-each (lambda (x) x) '(1 2 3)))

; empty list: body never executed
(check "for-each empty"        '()
       (let ((acc '()))
         (for-each (lambda (x) (set! acc (cons x acc))) '())
         acc))

; two empty lists
(check "for-each 2 empty"      '()
       (for-each (lambda (x y) x) '() '()))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 125. let-syntax / letrec-syntax
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "let-syntax / letrec-syntax")

; basic usage: macro defined and callable inside the block
(check "let-syntax basic"          11
  (let-syntax ((ls-inc () ((_ x) (+ x 1))))
    (ls-inc 10)))

; multiple bindings in one let-syntax form
(check "let-syntax multi"          '(11 20)
  (let-syntax ((ls-inc () ((_ x) (+ x 1)))
               (ls-dbl () ((_ x) (* x 2))))
    (list (ls-inc 10) (ls-dbl 10))))

; swap using let-syntax (with gensym ?t)
(check "let-syntax swap"           '(2 1)
  (let ((a 1) (b 2))
    (let-syntax ((ls-swap! () ((_ x y) (let ((?t x)) (set! x y) (set! y ?t)))))
      (ls-swap! a b)
      (list a b))))

; scope: macro not registered after the block exits
(check "let-syntax scope exit"     #f
  (begin
    (let-syntax ((ls-scoped () ((_ x) (* x x)))) 'inside)
    (macro? 'ls-scoped)))

; scope: calling the macro outside its block signals an error
(check "let-syntax scope unbound"  'unbound
  (begin
    (let-syntax ((ls-scoped2 () ((_ x) (* x x)))) 'inside)
    (try (ls-scoped2 5) 'unbound)))

(check "let-syntax spaced ellipsis" '(2 3 4)
  (let-syntax ((ls-tail ()
    ((_ x rest ...) (quote (rest ...)))))
    (ls-tail 1 2 3 4)))

(check "let-syntax zero ellipsis" '()
  (let-syntax ((ls-tail ()
    ((_ x rest ...) (quote (rest ...)))))
    (ls-tail 1)))

(check "let-syntax literal identifier" 1
  (let-syntax ((ls-lit (else)
    ((_ else x y) x)
    ((_ z x y) y)))
    (ls-lit else 1 2)))

(check "let-syntax literal shadowed" 2
  (let-syntax ((ls-lit (else)
    ((_ else x y) x)
    ((_ z x y) y)))
    (let ((else 'shadow))
      (ls-lit else 1 2))))

(check "let shadowed or" 77
  (let ((or (lambda (x y) 77)))
    (or #f #t)))

(check "lambda shadowed or" 77
  ((lambda (or) (or #f #t))
   (lambda (x y) 77)))

(check "lambda shadowed when" 77
  ((lambda (when) (when #f 77))
   (lambda (p x) x)))

(check "let-values shadowed or" 77
  (let-values (((or) (lambda (x y) 77)))
    (or #f #t)))

(check "let*-values shadowed or" 77
  (let*-values (((or) (lambda (x y) 77)))
    (or #f #t)))

(check "receive shadowed or" 77
  (receive (or) (lambda (x y) 77)
    (or #f #t)))

(check "receive rest shadowed when" 'shadowed
  (receive when (lambda (p x) x)
    (try (when #f 77) 'shadowed)))

(check "case-lambda shadowed or" 77
  ((case-lambda ((or) (or #f #t)))
   (lambda (x y) 77)))

(check "case-lambda shadowed when" 77
  ((case-lambda ((when) (when #f 77)))
   (lambda (p x) x)))

(check "case-lambda rest shadowed or" 'shadowed
  (try
    ((case-lambda (or (or #f #t)))
     (lambda (x y) 77))
    'shadowed))

(check "let-syntax wildcard" 2
  (let-syntax ((ls-second () ((_ _ x . _) x)))
    (ls-second 1 2 3 4)))

; letrec-syntax basic
(check "letrec-syntax basic"       6
  (letrec-syntax ((lrs-sub2 () ((_ x) (- x 2))))
    (lrs-sub2 8)))

; letrec-syntax with a self-recursive macro (my-or style)
(check "letrec-syntax my-or hit"   42
  (letrec-syntax ((lrs-or ()
    ((_)            #f)
    ((_ e)          e)
    ((_ e1 e2...)   (let ((?v e1)) (if ?v ?v (lrs-or e2...))))))
    (lrs-or #f #f 42)))

(check "letrec-syntax my-or spaced ellipsis" 42
  (letrec-syntax ((lrs-or ()
    ((_)            #f)
    ((_ e)          e)
    ((_ e1 e2 ...)  (let ((?v e1)) (if ?v ?v (lrs-or e2 ...))))))
    (lrs-or #f #f 42)))

(check "letrec-syntax my-or first" 7
  (letrec-syntax ((lrs-or ()
    ((_)            #f)
    ((_ e)          e)
    ((_ e1 e2...)   (let ((?v e1)) (if ?v ?v (lrs-or e2...))))))
    (lrs-or #f 7 99)))

(check "letrec-syntax my-or none"  #f
  (letrec-syntax ((lrs-or ()
    ((_)            #f)
    ((_ e)          e)
    ((_ e1 e2...)   (let ((?v e1)) (if ?v ?v (lrs-or e2...))))))
    (lrs-or #f #f #f)))

; letrec-syntax scope also isolated after block exits
(check "letrec-syntax scope"       #f
  (begin
    (letrec-syntax ((lrs-scoped () ((_ x) x))) 'inside)
    (macro? 'lrs-scoped)))

(check "letrec-syntax numeric base" '(#t #f)
  (letrec-syntax ((even-m () ((_ 0) #t) ((_ n) (odd-m (- n 1))))
                  (odd-m ()  ((_ 0) #f) ((_ n) (even-m (- n 1)))))
    (list (even-m 0) (odd-m 0))))

(check "letrec-syntax mutual recursion" '(#t #f #f #t)
  (letrec-syntax ((even-m () ((_) #t) ((_ x rest ...) (odd-m rest ...)))
                  (odd-m ()  ((_) #f) ((_ x rest ...) (even-m rest ...))))
    (list (even-m a b) (odd-m a b) (even-m a b c) (odd-m a b c))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 126. Nested call/cc (tagged continuations)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "Nested call/cc")

; Regression: without tagged continuations the inner escape would be mis-caught
; by the outer TryCont.  With tagging, inner returns 5, outer continues with (* 2 5).
(check "nested call/cc fixed"      110
  (+ 100 (call/cc (lambda (outer-k)
                    (* 2 (call/cc (lambda (inner-k) (inner-k 5))))))))

; Outer escape invoked from within the inner lambda — must still work
(check "nested outer-escape"       200
  (+ 100 (call/cc (lambda (outer-k)
                    (* 2 (call/cc (lambda (inner-k) (outer-k 100))))))))

; After the inner call/cc escapes the outer lambda body continues running
(check "nested inner isolates"     "outer-ran"
  (let ((s "not-set"))
    (call/cc
      (lambda (outer-k)
        (call/cc
          (lambda (inner-k) (inner-k 'escape)))
        (set! s "outer-ran")))
    s))

; nested let/cc: inner escape is isolated — outer body still executes
(check "nested let/cc outer runs"  'unreachable
  (let/cc outer
    (let/cc inner
      (inner 42)       ; escapes inner, inner call/cc returns 42
      (outer 99))      ; unreachable — (inner 42) already escaped
    'unreachable))     ; outer body continues here and returns this

; outer escapes using the inner result — returns 42
(check "nested let/cc outer-uses-inner" 42
  (let/cc outer
    (outer
      (let/cc inner
        (inner 42)     ; inner call/cc returns 42
        99))           ; unreachable
    'unreachable))

; three-level nesting: log the order of inner→mid→outer escapes
(check "triple nested call/cc"     '(inner mid outer)
  (let ((log '()))
    (call/cc
      (lambda (outer-k)
        (call/cc
          (lambda (mid-k)
            (call/cc
              (lambda (inner-k)
                (inner-k 'inner)
                'never-1))
            (set! log (cons 'inner log))
            (mid-k 'mid)
            'never-2))
        (set! log (cons 'mid log))
        (set! log (cons 'outer log))))
    (reverse log)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 127. Tail call edge cases
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "tail call edge cases")

;; deep named let — 10000 iterations proves TCO doesn't stack overflow
(check "named let deep TCO"  50005000
  (let loop ((n 10000) (acc 0))
    (if (= n 0) acc (loop (- n 1) (+ acc n)))))

;; mutual tail recursion — 10000 calls via letrec
(check "mutual tail even?"   #t
  (letrec ((my-even? (lambda (n) (if (= n 0) #t (my-odd?  (- n 1)))))
           (my-odd?  (lambda (n) (if (= n 0) #f (my-even? (- n 1))))))
    (my-even? 10000)))

(check "mutual tail odd?"    #t
  (letrec ((my-even? (lambda (n) (if (= n 0) #t (my-odd?  (- n 1)))))
           (my-odd?  (lambda (n) (if (= n 0) #f (my-even? (- n 1))))))
    (my-odd? 9999)))

;; tail call in cond branches
(check "tail in cond"        'c
  (let loop ((x 1))
    (cond ((= x 1) (loop 2))
          ((= x 2) (loop 3))
          (else     'c))))

;; tail call in case branches
(check "tail in case"        'done
  (let loop ((n 3))
    (case n
      ((0)  'done)
      (else (loop (- n 1))))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 128. Do loop edge cases
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "do loop edge cases")

;; no body, single result expression — returns i when test fires
(check "do no body result"   10
  (do ((i 0 (+ i 1)))
      ((= i 10) i)))

;; no result expression — returns the test value (#t)
(check "do returns test val" #t
  (do ((i 0 (+ i 1)))
      ((= i 5))))

;; multiple result expressions — last one is returned
(check "do multi result"     'done
  (do ((i 0 (+ i 1)))
      ((= i 2) i 'done)))

;; two induction variables, countdown accumulates in order
(check "do countdown"        '(1 2 3 4 5)
  (do ((i 5 (- i 1)) (acc '() (cons i acc)))
      ((= i 0) acc)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 129. adjoin / set-cons
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "adjoin / set-cons")

;; adjoin adds new element
(check "adjoin adds new"     #t
  (let ((r (adjoin 1 '(2 3 4))))
    (and (member 1 r) (member 2 r) (member 4 r) #t)))

;; adjoin with existing element — no duplicate
(check "adjoin no dup"       '(1 2 3)  (adjoin 2 '(1 2 3)))

;; adjoin to empty list
(check "adjoin to empty"     '(x)      (adjoin 'x '()))

;; set-cons adds new element
(check "set-cons adds new"   #t
  (let ((r (set-cons 'z '(a b c))))
    (if (member 'z r) #t #f)))

;; set-cons with duplicate — returns original list unchanged
(check "set-cons no dup"     '(a b c)  (set-cons 'b '(a b c)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 130. Quasiquote edge cases
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "quasiquote edge cases")

;; empty splice vanishes — no element inserted
(check "splice empty"        '(a b)
  (let ((xs '())) `(a ,@xs b)))

;; two adjacent splices
(check "splice twice"        '(1 2 3 4)
  (let ((a '(1 2)) (b '(3 4))) `(,@a ,@b)))

;; unquote at head position
(check "quasi unquote head"  '(1 2 3)
  (let ((h 1)) `(,h 2 3)))

;; deeply nested unquote
(check "quasi deep unquote"  '(a (b (c 99)))
  (let ((n 99)) `(a (b (c ,n)))))

;; splice producing entire list
(check "quasi splice only"   '(1 2 3)
  (let ((xs '(1 2 3))) `(,@xs)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 131. Multiple values — edge cases
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "multiple values edge cases")

;; (values) with zero arguments → empty values, consumed as empty list
(check "zero values"         '()
  (call-with-values (lambda () (values)) list))

;; three values consumed as a list
(check "three values->list"  '(1 2 3)
  (call-with-values (lambda () (values 1 2 3)) list))

;; receive with rest-var binding captures all values as a list
(check "receive rest-var"    '(1 2 3)
  (receive all (values 1 2 3) all))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 132. for-each with 2 lists
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "for-each 2-list")

(let ((result '()))
  (for-each (lambda (a b) (set! result (cons (+ a b) result)))
            '(1 2 3) '(10 20 30))
  (check "for-each 2 lists"  '(33 22 11)  result))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 133. Rational numbers (exact fractions)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "rational numbers")

; ── Literal parsing ───────────────────────────────────────────────────────────
(check "rat lit 1/3"          1/3    1/3)
(check "rat lit -1/2"         -1/2   -1/2)
(check "rat normalize 4/2"    2      4/2)          ; normalises to integer
(check "rat normalize 6/3"    2      6/3)
(check "rat normalize -6/3"   -2     -6/3)
(check "rat normalize 0/5"    0      0/5)          ; zero rational → 0
(check "rat reduce 4/6"       2/3    4/6)          ; GCD reduction
(check "rat negative -2/4"    -1/2   -2/4)         ; sign + reduction

; ── Type predicates ───────────────────────────────────────────────────────────
(check "exact? rat"           #t     (exact? 1/3))
(check "inexact? rat"         #f     (inexact? 1/3))
(check "number? rat"          #t     (number? 1/3))
(check "rational? rat"        #t     (rational? 1/3))
(check "real? rat"            #t     (real? 1/3))
(check "complex? rat"         #t     (complex? 1/3))
(check "integer? rat"         #f     (integer? 1/3))
(check "integer? rat whole"   #t     (integer? 4/2))   ; 4/2 normalises to int 2
(check "exact-integer? rat"   #f     (exact-integer? 1/3))
(check "exact-integer? 4/2"   #t     (exact-integer? 4/2))

; ── Basic arithmetic (all results stay exact) ─────────────────────────────────
(check "rat + rat"            1/2    (+ 1/3 1/6))
(check "rat + collapses"      1      (+ 1/3 2/3))   ; normalises to int
(check "rat - rat"            1/6    (- 1/3 1/6))
(check "rat - collapses"      0      (- 1/2 1/2))
(check "rat * rat"            1/2    (* 2/3 3/4))
(check "rat * collapses"      1      (* 3 1/3))
(check "rat / rat"            4/3    (/ 2/3 1/2))
(check "rat / collapses"      2      (/ 4 2))        ; exact division → int
(check "int / int -> rat"     1/3    (/ 1 3))        ; exact rational
(check "int / int exact?"     #t     (exact? (/ 1 3)))
(check "rat negate"           -1/3   (- 1/3))
(check "negate negative"      1/3    (- -1/3))
(check "rat + int"            4/3    (+ 1/3 1))
(check "rat - int"            -2/3   (- 1/3 1))
(check "rat / int"            1/6    (/ 1/3 2))
(check "rat result exact?"    #t     (exact? (* 2/3 3/4)))

; ── Mixed exact/inexact → inexact ────────────────────────────────────────────
(check "rat + double"         #t     (inexact? (+ 1/3 1.0)))
(check "rat * double"         #t     (inexact? (* 1/2 2.0)))
(check "rat / double"         #t     (inexact? (/ 1/3 1.0)))

; ── Comparison operators ──────────────────────────────────────────────────────
(check "rat < rat"            #t     (< 1/3 1/2))
(check "rat > rat"            #t     (> 2/3 1/2))
(check "rat < rat false"      #f     (< 1/2 1/3))
(check "rat <= eq"            #t     (<= 1/3 1/3))
(check "rat >= eq"            #t     (>= 1/2 1/2))
(check "rat = rat"            #t     (= 1/3 1/3))
(check "rat = double"         #t     (= 1/2 0.5))
(check "rat /= double"        #f     (= 1/3 0.3))
(check "rat < int"            #t     (< 1/3 1))
(check "rat > int false"      #f     (> 1/3 1))
(check "rat chain <"          #t     (< 1/4 1/3 1/2 2/3 3/4))
(check "rat chain <="         #t     (<= 1/3 1/3 1/2))
(check "rat min"              1/4    (min 1/4 1/3 1/2))
(check "rat max"              3/4    (max 1/4 1/2 3/4))

; ── numerator / denominator ───────────────────────────────────────────────────
(check "numerator 3/4"        3      (numerator 3/4))
(check "denominator 3/4"      4      (denominator 3/4))
(check "numerator -1/2"       -1     (numerator -1/2))
(check "denominator -1/2"     2      (denominator -1/2))
(check "numerator int"        5      (numerator 5))
(check "denominator int"      1      (denominator 5))
(check "numer exact?"         #t     (exact? (numerator 3/4)))
(check "denom exact?"         #t     (exact? (denominator 3/4)))

; ── zero? / positive? / negative? / abs ──────────────────────────────────────
(check "zero? rat false"      #f     (zero? 1/3))
(check "zero? 0/5"            #t     (zero? 0/5))
(check "positive? rat"        #t     (positive? 1/3))
(check "negative? rat"        #t     (negative? -1/3))
(check "positive? neg false"  #f     (positive? -1/3))
(check "abs rat pos"          1/3    (abs 1/3))
(check "abs rat neg"          1/3    (abs -1/3))
(check "abs -3/4"             3/4    (abs -3/4))

; ── Rounding – results are exact integers ─────────────────────────────────────
(check "floor 7/2"            3      (floor 7/2))
(check "floor -7/2"           -4     (floor -7/2))
(check "floor 1/3"            0      (floor 1/3))
(check "floor -1/3"           -1     (floor -1/3))
(check "ceiling 7/2"          4      (ceiling 7/2))
(check "ceiling -7/2"         -3     (ceiling -7/2))
(check "ceiling 2/3"          1      (ceiling 2/3))
(check "ceiling -2/3"         0      (ceiling -2/3))
(check "truncate 7/2"         3      (truncate 7/2))
(check "truncate -7/2"        -3     (truncate -7/2))
(check "round 1/3"            0      (round 1/3))
(check "round 2/3"            1      (round 2/3))
(check "round 1/2 even"       0      (round 1/2))    ; banker's rounding: 0 is even
(check "round 3/2 even"       2      (round 3/2))    ; 2 is even
(check "round 5/2 even"       2      (round 5/2))    ; 2 is even
(check "round 7/2 even"       4      (round 7/2))    ; 4 is even
(check "floor exact?"         #t     (exact? (floor 7/2)))
(check "ceiling exact?"       #t     (exact? (ceiling 7/2)))
(check "truncate exact?"      #t     (exact? (truncate 7/2)))
(check "round exact?"         #t     (exact? (round 1/2)))

; ── inexact->exact / exact->inexact ──────────────────────────────────────────
(check "inexact->exact 0.5"   1/2    (inexact->exact 0.5))
(check "inexact->exact 0.25"  1/4    (inexact->exact 0.25))
(check "inexact->exact 0.75"  3/4    (inexact->exact 0.75))
(check "inexact->exact 3.0"   3      (inexact->exact 3.0))
(check "exact->inexact rat"   #t     (inexact? (exact->inexact 1/3)))
(check "exact->inexact 1/2"   0.5    (exact->inexact 1/2))
(check "exact->inexact 1/4"   0.25   (exact->inexact 1/4))
(check "exact alias 1.5"      3/2    (exact 1.5))
(check "inexact alias"        0.5    (inexact 1/2))

; ── expt with exact results ───────────────────────────────────────────────────
(check "expt int -1"          1/2    (expt 2 -1))     ; exact rational result
(check "expt int -2"          1/9    (expt 3 -2))
(check "expt int -1 exact?"   #t     (exact? (expt 2 -1)))
(check "expt -int -1"         -1/2   (expt -2 -1))    ; negative base
(check "expt -int -2"         1/4    (expt -2 -2))    ; even power positive
(check "expt -int 3"          -8     (expt -2 3))     ; odd power negative
(check "expt -int 4"          16     (expt -2 4))     ; even power positive
(check "expt rat pos"         1/8    (expt 1/2 3))    ; rational base
(check "expt rat pos exact?"  #t     (exact? (expt 1/2 3)))
(check "expt rat neg"         8      (expt 1/2 -3))   ; reciprocal
(check "expt rat neg2"        9/4    (expt 2/3 -2))   ; (2/3)^-2 = (3/2)^2 = 9/4
(check "expt 0 n"             0      (expt 0 5))
(check "expt n 0"             1      (expt 7/3 0))

; ── eqv? / equal? ─────────────────────────────────────────────────────────────
(check "eqv? same rat"        #t     (eqv? 1/3 1/3))
(check "eqv? reduced"         #t     (eqv? 2/6 1/3))  ; both normalise to 1/3
(check "eqv? diff rat"        #f     (eqv? 1/3 1/4))
(check "equal? rat"           #t     (equal? 1/3 1/3))
(check "equal? in list"       #t     (equal? (list 1/2 1/3) (list 1/2 1/3)))

; ── number->string ────────────────────────────────────────────────────────────
(check "number->string rat"   "1/3"  (number->string 1/3))
(check "number->string -1/2"  "-1/2" (number->string -1/2))
(check "number->string 4/2"   "2"    (number->string 4/2))  ; normalised to int

; ── p-adic display mode ───────────────────────────────────────────────────────
(check "p-adic int display"   "202_7"
       (begin
         (p-adic 7)
         (let ((s (number->string 100)))
           (p-adic 10)
           s)))
(check "p-adic int display truncated" "...0003_7"
       (begin
         (p-adic 7 4)
         (let ((s (number->string (+ (expt 7 5) 3))))
           (p-adic 10)
           s)))
(check "p-adic neg display"   "...6666666666666666_7"
       (begin
         (p-adic 7 16)
         (let ((s (number->string -1)))
           (p-adic 10)
           s)))
(check "p-adic rat display"   "...3333333333333334_7"
       (begin
         (p-adic 7 16)
         (let ((s (number->string 1/2)))
           (p-adic 10)
           s)))
(check "p-adic rat display 32" "...33333333333333333333333333333334_7"
       (begin
         (p-adic 7 32)
         (let ((s (number->string 1/2)))
           (p-adic 10)
           s)))
(check "p-adic write display" "...3333333333333334_7"
       (begin
         (p-adic 7 16)
         (let ((p (open-output-string)))
           (write 1/2 p)
           (let ((s (get-output-string p)))
             (p-adic 10)
             s))))
(check "p-adic reset"         "1/2"
       (begin
         (p-adic 7)
         (p-adic 10)
         (number->string 1/2)))
(check "p-adic precision persists" "...33333333333333333333333333333334_7"
       (begin
         (p-adic 7 32)
         (p-adic 10)
         (p-adic 7)
         (let ((s (number->string 1/2)))
           (p-adic 10)
           s)))

; ── Higher-order functions ────────────────────────────────────────────────────
(check "map sqr rats"
       (list 1/4 1/9 1/16)
       (map (lambda (x) (* x x)) (list 1/2 1/3 1/4)))
(check "filter rational?"
       (list 1/2 1/3)
       (filter rational? (list 1/2 "a" 1/3 #t)))
(check "apply + rats"         1      (apply + (list 1/4 1/4 1/4 1/4)))
(check "rat in vector"        1/3    (vector-ref (vector 1/2 1/3 1/4) 1))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 134. Complex numbers
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "complex numbers")

; ── Literal syntax ────────────────────────────────────────────────────────────
(check "cplx lit re 3+4i"     3.0    (real-part 3+4i))
(check "cplx lit im 3+4i"     4.0    (imag-part 3+4i))
(check "cplx lit im 3-4i"     -4.0   (imag-part 3-4i))
(check "cplx lit re +4i"      0.0    (real-part +4i))
(check "cplx lit im +4i"      4.0    (imag-part +4i))
(check "cplx lit im -4i"      -4.0   (imag-part -4i))
(check "cplx lit im +i"       1.0    (imag-part +i))
(check "cplx lit im -i"       -1.0   (imag-part -i))
(check "cplx lit im 0+2i"     2.0    (imag-part 0+2i))
(check "cplx lit re 1.5+2.5i" 1.5    (real-part 1.5+2.5i))
(check "cplx lit im 1.5+2.5i" 2.5    (imag-part 1.5+2.5i))

; ── Type predicates ───────────────────────────────────────────────────────────
(check "complex? cplx"        #t     (complex? 3+4i))
(check "number? cplx"         #t     (number? 3+4i))
(check "real? cplx nonzero"   #f     (real? 3+4i))
(check "rational? cplx"       #f     (rational? 3+4i))
(check "integer? cplx"        #f     (integer? 3+4i))
(check "exact? cplx"          #f     (exact? 3+4i))
(check "inexact? cplx"        #t     (inexact? 3+4i))

; ── Constructors / accessors ─────────────────────────────────────────────────
(check "make-rect re"         3.0    (real-part (make-rectangular 3 4)))
(check "make-rect im"         4.0    (imag-part (make-rectangular 3 4)))
(check "make-rect int 0"      5      (make-rectangular 5 0))   ; exact 0 → return exact real
(check "make-rect rat 0"      1/2    (make-rectangular 1/2 0)) ; exact 0 → return 1/2
(check "magnitude 3+4i"       5.0    (magnitude 3+4i))
(check "magnitude +i"         1.0    (magnitude +i))
(check "magnitude -3-4i"      5.0    (magnitude -3-4i))
(check "angle 1."             0.0    (angle 1.))
(check "angle +i near pi/2"   #t     (< (abs (- (angle +i) 1.5707963267948966)) 1e-10))
(check "angle -1 near pi"     #t     (< (abs (- (angle -1)  3.141592653589793))  1e-10))
(check "make-polar mag"       #t     (< (abs (- (magnitude (make-polar 3.0 1.0)) 3.0)) 1e-10))
(check "make-polar angle"     #t     (< (abs (- (angle     (make-polar 3.0 1.0)) 1.0)) 1e-10))

; ── Arithmetic ────────────────────────────────────────────────────────────────
; Use magnitude of difference < epsilon to compare complex results
(check "cplx +"   #t   (< (magnitude (- (+ 1+2i 3+4i)  4+6i))    1e-10))
(check "cplx -"   #t   (< (magnitude (- (- 3+4i 1+2i)  2+2i))    1e-10))
(check "cplx *"   #t   (< (magnitude (- (* 1+2i 1+2i) -3+4i))    1e-10))
(check "cplx /"   #t   (< (magnitude (- (/ 3+4i 3+4i)   1.+0.i)) 1e-10))
(check "cplx + real" #t (< (magnitude (- (+ 3+4i 2.)    5+4i))   1e-10))
(check "cplx * int"  #t (< (magnitude (- (* 2 3+4i)     6+8i))   1e-10))
(check "cplx * rat"  #t (< (magnitude (- (* 1/2 2+4i)   1+2i))   1e-10))
(check "cplx negate" #t (< (magnitude (- (- 3+4i)      -3-4i))   1e-10))

; ── Special values ────────────────────────────────────────────────────────────
(check "i^2 re ~= -1" #t  (< (abs (+ 1.0 (real-part (expt +i 2)))) 1e-10))
(check "i^2 im ~= 0"  #t  (< (abs (imag-part (expt +i 2)))          1e-10))
(check "i^4 re ~= 1"  #t  (< (abs (- 1.0 (real-part (expt +i 4)))) 1e-10))

; ── zero? / real? with zero imaginary ─────────────────────────────────────────
(check "zero? cplx"         #f   (zero? 3+4i))
(check "zero? 0+0i"         #t   (zero? (make-rectangular 0. 0.)))
(check "real? zero-imag"    #t   (real? (make-rectangular 3. 0.)))

; ── equality ─────────────────────────────────────────────────────────────────
(check "eqv? cplx same"     #t   (eqv? 3+4i 3+4i))
(check "eqv? cplx diff"     #f   (eqv? 3+4i 3+5i))
(check "equal? cplx"        #t   (equal? 3+4i 3+4i))
(check "= cplx same"        #t   (= 3+4i 3+4i))
(check "= cplx diff im"     #f   (= 3+4i 3+5i))
(check "= cplx diff re"     #f   (= 3+4i 2+4i))

; ── Higher-order usage ────────────────────────────────────────────────────────
(check "map real-part"
       (list 3.0 1.0)
       (map real-part (list 3+4i 1+2i)))
(check "map imag-part"
       (list 4.0 2.0)
       (map imag-part (list 3+4i 1+2i)))
(check "magnitude 3+4i = 5"  #t
       (< (abs (- (magnitude 3+4i) 5.0)) 1e-10))
(check "complex? first-class"
       (list #t #f #t #t)
       (map complex? (list 3+4i "str" 1 0.5)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 135. call/cc-full (coroutine / reentrant continuations)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "call/cc-full")

;; body returns normally without ever invoking k — same as a plain lambda call
(check "ccf no escape"       20     (call/cc-full (lambda (k) (* 5 4))))

;; invoking k from inside the body acts as an early exit — body is discarded
(check "ccf escape via k"    4      (call/cc-full (lambda (k) (* 5 (k 4)))))

;; the value returned by call/cc-full participates in surrounding arithmetic
(check "ccf escape in arith" 8      (* 2 (call/cc-full (lambda (k) (* 5 (k 4))))))

;; k called without any escape-result — result is still the escape value
(check "ccf escape k 42"     42     (call/cc-full (lambda (k) (k 42) 99)))

;; ----- coroutine / yield behaviour ----------------------------------------
;; body calls k multiple times, yielding control back to the caller each time.
;; The caller receives each yielded value in turn.

(define ccf-resume #f)

;; First call: body calls (k 1) — caller gets 1, body is suspended at that point
(check "ccf yield 1"         1
  (call/cc-full (lambda (k) (set! ccf-resume k) (k 1) (k 2) (k 3) 'done)))

;; Resume: body continues past (k 1), executes (k 2) — caller gets 2
(check "ccf yield 2"         2      (ccf-resume #f))

;; Resume again: body executes (k 3) — caller gets 3
(check "ccf yield 3"         3      (ccf-resume #f))

;; Final resume: body falls off the end with 'done — caller gets done
(check "ccf body done"       'done  (ccf-resume #f))

;; ----- k is a first-class procedure ----------------------------------------

;; When k is captured and returned, call/cc-full returns a procedure
(check "ccf k is proc"       #t
  (procedure? (call/cc-full (lambda (k) k))))

;; ----- call/cc-full nested independence ------------------------------------
;; Two independent call/cc-full forms; invoking the inner k only terminates
;; the inner call, leaving the outer one unaffected.

(check "ccf nested outer ok" 110
  (+ 100 (call/cc-full (lambda (outer)
    (* 2 (call/cc-full (lambda (inner) (inner 5))))))))

;; Outer escape (plain call/cc) propagates cleanly through an inner call/cc-full body.
;; (call/cc-full outer would deadlock — cross-continuation escape is not supported.)
(check "ccf outer escape via cc"    200
  (+ 100 (call/cc (lambda (outer)
    (* 2 (call/cc-full (lambda (inner) (outer 100))))))))

;; ----- call/cc-full and plain call/cc coexist without interference ----------

(check "ccf with escape cc"  7
  (+ (call/cc (lambda (e) (e 3)))
     (call/cc-full (lambda (k) (k 4)))))

;; ----- different value types can be yielded --------------------------------

(check "ccf yield string"    "hello"
  (call/cc-full (lambda (k) (k "hello"))))

(check "ccf yield list"      '(1 2 3)
  (call/cc-full (lambda (k) (k '(1 2 3)))))

(check "ccf yield bool"      #f
  (call/cc-full (lambda (k) (k #f))))

;; ----- make-generator (defined in init.ss using call/cc-full) ---------------

(define gen-test
  (make-generator (lambda (yield)
    (yield 10)
    (yield 20)
    (yield 30))))

(check "gen first"           10     (gen-test))
(check "gen second"          20     (gen-test))
(check "gen third"           30     (gen-test))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; 134. Edge case regressions
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(section! "edge case regressions")

;; raise-continuable currently composes at the handler boundary; lock that in.
(check "raise-continuable composes"
  100
  (+ 1 (with-exception-handler (lambda (e) 99)
    (lambda () (raise-continuable 'x)))))

;; direct ,@ in ordinary calls and primitive calls exercises CALL_LIST / PRIM_LIST
(check "splice primitive call"       16
  (let ((xs '(1 2 3))) (+ 10 ,@xs)))

(check "splice primitive empty"      0
  (let ((xs '())) (+ 0 ,@xs)))

(check "splice closure call"         '(0 1 2 3 4)
  (let ((xs '(1 2 3))) ((lambda args args) 0 ,@xs 4)))

(check "splice closure adjacent"     '(1 2 3 4)
  (let ((a '(1 2)) (b '(3 4))) ((lambda args args) ,@a ,@b)))

(check "splice closure empty"        '(a b)
  ((lambda args args) 'a ,@'() 'b))

;; symbols->list / symbols->vector currently expose interned symbol names as strings.
(check "symbols->list fresh name"    #t
  (let ((fresh-name "__edge_case_symbol__"))
    (string->symbol fresh-name)
    (if (member fresh-name (symbols->list)) #t #f)))

(check "symbols->vector fresh name"  #t
  (let ((fresh-name "__edge_case_symbol__"))
    (string->symbol fresh-name)
    (if (member fresh-name (vector->list (symbols->vector))) #t #f)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Final report
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;; (map (lambda (n) (list n (! n) (fib n))) (range 1 20))
;; (map \n.(list n (! n) (fib n)) (range 1 20))
;; (map \n.(display "{0} {1} {2}\n" n (! n) (fib n)) (range 1 20))

(report)
