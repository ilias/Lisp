(call-static 'System.Console 'Write ", numbers")

;; --- Arithmetic operators (variadic) ---
;; (+) ==> 0  ;  (+ 1 2 3) ==> 6
(define (+ . lst)       (reduce (lambda (a b)(CALLNATIVE 'AddObj a b)) 0 lst))
;; (-) negate a single number or subtract left-to-right.
;; (- 5) ==> -5  ;  (- 10 3 2) ==> 5
(define (- . lst)       (if (null? (cdr lst)) (CALLNATIVE 'NegObj (car lst)) (reduce (lambda (a b)(CALLNATIVE 'SubObj a b)) 0 lst)))
;; (/) ==> 1  ;  (/ 12 3 2) ==> 2
(define (/ . lst)       (reduce (lambda (a b)(CALLNATIVE 'DivObj a b)) 1 lst))
;; (*) ==> 1  ;  (* 2 3 4) ==> 24
(define (* . lst)       (reduce (lambda (a b)(CALLNATIVE 'MulObj a b)) 1 lst))

;; --- Comparison operators (variadic, chain-comparable) ---
;; (< 1 2 3) ==> #t  ;  (<= 1 1 2) ==> #t
(define (< . lst)       (COMPARE-ALL (lambda (a b) (LESSTHAN a b))       ,@lst))
(define (<= . lst)      (COMPARE-ALL (lambda (a b) (or (< a b) (= a b))) ,@lst))
(define (= . lst)       (COMPARE-ALL (lambda (a b) (try (eqv? (todouble a) (todouble b)) (eqv? a b))) ,@lst))
(define (<> . lst)      (COMPARE-ALL (lambda (a b) (not (= a b)))        ,@lst))
(define (>= . lst)      (COMPARE-ALL (lambda (a b) (not (< a b)))        ,@lst))
(define (>  . lst)      (COMPARE-ALL (lambda (a b) (not (<= a b)))       ,@lst))

;; --- Numeric predicates and type tests ---

;; (abs a) -- absolute value.
(define (abs a)         (if (< a 0) (neg a) a))

;; Trigonometric functions (input in radians).
(define (acos a)        (call-static 'System.Math 'Acos (todouble a)))
(define (asin a)        (call-static 'System.Math 'Asin (todouble a)))
(define (atan a)        (call-static 'System.Math 'Atan (todouble a)))

;; Bitwise integer operations.
;; (bit-and a b) -- bitwise AND of two integers.
;; (bit-or a b)  -- bitwise OR.
;; (bit-xor a b) -- bitwise XOR.
;; Example: (bit-and 12 10) ==> 8  (1100 AND 1010 = 1000)
(define (bit-and a b)   (CALLNATIVE 'BitAndObj a b))
(define (bit-or a b)    (CALLNATIVE 'BitOrObj a b))
(define (bit-xor a b)   (CALLNATIVE 'BitXorObj a b))

;; (ceiling x) -- smallest integer >= x; exact integers are returned unchanged.
(define (ceiling x)     (if (exact? x) x (call-static 'System.Math 'Ceiling (todouble x))))
;; Trigonometric: cos, sin, tan, acos, asin, atan (radians).
(define (cos a)         (call-static 'System.Math 'Cos (todouble a)))

;; E -- Euler's number (2.718...).
(define E               (get 'System.Math 'E))

;; (even? x) -- #t if x is divisible by 2.
(define (even? x)       (= (remainder x 2) 0))

;; (exact? x) -- #t if x is an exact integer (Int32 or BigInteger).
(define (exact? x)      (let ((t (call x 'GetType)))
                          (or (eqv? t (get-type "System.Int32"))
                              (eqv? t (get-type "System.Numerics.BigInteger")))))
(define (exp a)         (call-static 'System.Math 'Exp (todouble a)))

;; (expt a b) -- a raised to the power b.  Also aliased as (** a b).
;; Example: (expt 2 10) ==> 1024
(define (expt a b)      (CALLNATIVE 'PowObj a b))
(define (** a b)        (expt a b))

;; (! n) -- factorial of n.
;; Example: (! 5) ==> 120
(define (! a)           (if (<= a 1) 1 (* a (! (- a 1)))))

;; (fib n) -- nth Fibonacci number (iterative, O(n) time).
;; Example: (fib 10) ==> 55
;; (define (fib n)         (if (<= n 2) 1 (+ (fib (- n 1)) (fib (- n 2)))))
(define (fib n)         (define (loop a b k)
                          (if (= k 0)
                              a
                              (loop b (+ a b) (- k 1))))
                        (loop 0 1 n))

;; (floor a) -- largest integer <= a; exact integers are returned unchanged.
(define (floor a)       (if (exact? a) a (call-static 'System.Math 'Floor (todouble a))))

;; (gcd a...) -- greatest common divisor of all arguments.
;; Example: (gcd 12 8 6) ==> 2
(define (%gcd2 a b)     (if (= b 0) (abs a) (%gcd2 b (remainder a b))))
(define (gcd . args)    (if (null? args) 0 (foldl %gcd2 (abs (car args)) (cdr args))))

;; (lcm a...) -- least common multiple of all arguments.
;; Example: (lcm 4 6) ==> 12
(define (lcm . args)
  (if (null? args) 1
      (foldl (lambda (acc x)
               (let ((g (%gcd2 acc x)))
                 (if (= g 0) 0 (quotient (* (abs acc) (abs x)) g))))
             (abs (car args)) (cdr args))))

;; (inexact? x) -- #t if x is not exact (i.e., a floating-point number).
(define (inexact? x)    (not (exact? x)))

;; (integer? n) -- #t if n is an integer (exact or inexact whole number).
(define (integer? n)
  (and (number? n)
       (or (exact? n)
           (try (= (todouble n) (todouble (tointeger n))) #f))))
(define (int? n)        (integer? n))

(define (log a)         (call-static 'System.Math 'Log (todouble a)))
;; (log10 a) -- base-10 logarithm of a.
(define (log10 a)       (call-static 'System.Math 'Log10 (todouble a)))

;; (max x ...) -- return the largest of the given numbers.
(define (max x . lst)
  (if (null? lst) 
      x
      (let ((n (max ,@lst)))
        (if (< x n) n x))))

;; (min x ...) -- return the smallest of the given numbers.
(define (min x . lst)
  (if (null? lst) 
      x
      (let ((n (min ,@lst)))
        (if (< x n) x n))))

;; (modulo x y) -- modulo with the sign of the divisor (y).
;; Differs from remainder when the signs differ.
;; Example: (modulo -7 3) ==> 2   ;  (remainder -7 3) ==> -1
(define (modulo x y)
  (let ((r (remainder x y)))
    (if (or (zero? r)
            (and (positive? r) (positive? y))
            (and (negative? r) (negative? y)))
        r
        (+ r y))))

;; (neg a) -- negate a (multiply by -1).
(define (neg a)         (CALLNATIVE 'NegObj a))
;; (negative? a) -- #t if a < 0.
(define (negative? a)   (< a 0))

;; (number? n) -- #t if n is any numeric type (Int32, Double, or BigInteger).
(define (number? n)     (let ((t (call n 'GetType)))
                          (or (eqv? t (get-type "System.Int32"))
                              (eqv? t (get-type "System.Double"))
                              (eqv? t (get-type "System.Numerics.BigInteger")))))
(define (odd? x)        (if (even? x) #f #t))

;; PI -- the mathematical constant π (3.14159...).
(define PI              (get 'System.Math 'PI))
;; (positive? x) -- #t if x > 0.
(define (positive? x)   (> x 0))
;; (pow x y) -- alias for (expt x y) using System.Math.Pow (always returns double).
(define (pow x y)       (call-static 'System.Math 'Pow (todouble x) (todouble y)))

;; (quotient x y) -- integer division truncating toward zero.
;; Example: (quotient 13 4) ==> 3
(define (quotient x y)  (CALLNATIVE 'IDivObj x y))
;; (real? n) -- alias for number? (all numbers in this implementation are real).
(define (real? n)       (number? n))
;; (reciprocal n) -- 1/n ; returns "oops!" if n is zero.
(define (reciprocal n)  (if (= n 0) "oops!" (/ 1 n)))

;; (remainder x y) -- remainder with the sign of the dividend (x).
;; Example: (remainder 13 4) ==> 1  ;  (remainder -13 4) ==> -1
(define (remainder x y) (CALLNATIVE 'ModObj x y))
(define (round a)       (if (exact? a) a (call-static 'System.Math 'Round (todouble a))))
(define (sin a)         (call-static 'System.Math 'Sin (todouble a)))
;; (sort lst [less?]) -- return a sorted copy of lst.
;; The optional less? predicate defaults to <=.
;; Example: (sort '(3 1 4 1 5 9 2 6)) ==> (1 1 2 3 4 5 6 9)
;; Example: (sort '(3 1 4) >=) ==> (4 3 1)
(define (sort l . opts)
  (let ((less? (if (null? opts) (lambda (a b) (<= a b)) (car opts))))
    (define (sort* l)
      (define (insert x l)
        (if (null? l)
            (list x)
            (if (less? x (car l))
                (cons x l)
                (cons (car l) (insert x (cdr l))))))
      (if (null? l)
          '()
          (insert (car l) (sort* (cdr l)))))
    (sort* l)))

;; (sort-by f lst) -- sort lst by the key function f (ascending).
;; Example: (sort-by string-length '("banana" "fig" "apple")) ==> ("fig" "apple" "banana")
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

(define (sqrt a)        (call-static 'System.Math 'Sqrt (todouble a)))
;; PHI -- the golden ratio φ = (1 + √5) / 2 ≈ 1.61803...
(define PHI             (/ (+ 1 (sqrt 5)) 2))
(define (tan a)         (call-static 'System.Math 'Tan (todouble a)))
;; (todouble a) -- coerce a to a System.Double (inexact real).
(define (todouble a)      (call-static 'System.Convert 'ToDouble a))
;; (tointeger a) -- coerce a to a System.Int32 (truncates decimals).
(define (tointeger a)     (call-static 'System.Convert 'ToInt32 a))
(define (truncate x)      (if (exact? x) x (exact->inexact (tointeger (call-static 'System.Math 'Truncate x)))))
;; (zero? x) -- #t if x is zero.
(define (zero? x)         (= x 0))
;; (square x) -- return x * x.
(define (square x)        (* x x))
;; (complex? n) / (rational? n) -- always #t for any number (this implementation supports only reals).
(define (complex? n)      (number? n))
(define (rational? n)     (number? n))

;; (exact->inexact n) -- convert exact integer to inexact double.
;; (inexact->exact n) -- convert inexact double to nearest exact rational.
;; Example: (exact->inexact 3) ==> 3.0
;; Example: (inexact->exact 3.5) ==> (exact rational representation, if supported)
(define (exact->inexact n) (todouble n))
; inexact->exact: use the C# DoubleToExact for inexact inputs to get an exact Rational
(define (inexact->exact n) (if (exact? n) n (CALLNATIVE 'DoubleToExact (todouble n))))
;; exact / inexact -- R7RS short names for the same conversions.
(define exact   inexact->exact)
(define inexact exact->inexact)

;; (number->string n [radix]) -- convert number n to its string representation.
;; Optional radix (e.g. 2, 8, 16) selects the base.  Default is base 10.
;; Example: (number->string 255 16) ==> "ff"
;; Example: (number->string -255 16) ==> "-ff"
(define (number->string n . rest)
  (if (null? rest)
      (if (number? n)
          (call-static 'Lisp.Util 'NumberToString n)
          (call n 'ToString))
      (let ((radix (car rest)))
        (cond
          ((= radix 10) (number->string n))
          ((negative? n) (string-append "-" (number->string (neg n) radix)))
          ((zero? n) "0")
          (else
           (let loop ((k n) (digits '()))
             (if (zero? k)
                 (list->string
                   (map (lambda (d)
                          (integer->char (if (< d 10) (+ d 48) (+ d 87))))
                        digits))
                 (loop (quotient k radix)
                       (cons (remainder k radix) digits)))))))))

;; Internal helper: value of character c as a digit in radix, or #f if invalid.
;; Uses char->integer (ordinal) comparisons to avoid culture-sensitive char<? ordering.
(define (%digit-val c radix)
  (let* ((i (char->integer c))
         (d (cond ((and (<= 48 i) (<= i  57)) (- i 48))          ; '0'-'9'
                  ((and (<= 97 i) (<= i 122)) (+ 10 (- i 97)))   ; 'a'-'z'
                  ((and (<= 65 i) (<= i  90)) (+ 10 (- i 65)))   ; 'A'-'Z'
                  (else radix))))
    (if (< d radix) d #f)))

;; Internal helper: parse a non-empty unsigned integer string in the given radix.
(define (%parse-unsigned s radix)
  (let ((len (string-length s)))
    (if (= len 0)
        #f
        (let loop ((i 0) (n 0))
          (if (= i len)
              n
              (let ((d (%digit-val (string-ref s i) radix)))
                (if d (loop (+ i 1) (+ (* n radix) d)) #f)))))))

;; (string->number s [radix]) -- parse a numeric string s and return the number,
;; or #f if the string is not a valid number.
;; Handles "+nan.0", "+inf.0", "-inf.0", integers, floats, and radix prefixes.
;; Example: (string->number "3.14")     ==> 3.14
;; Example: (string->number "ff" 16)    ==> 255
;; Example: (string->number "-ff" 16)   ==> -255
;; Example: (string->number "abc")      ==> #f
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
           (let* ((radix (car rest))
                  (len   (string-length s)))
             (cond
               ((= len 0) #f)
               ((char=? (string-ref s 0) #\-)
                (let ((n (%parse-unsigned (substring s 1 len) radix)))
                  (and n (neg n))))
               ((char=? (string-ref s 0) #\+)
                (%parse-unsigned (substring s 1 len) radix))
               (else (%parse-unsigned s radix)))))
       #f))

;; --- Numeric predicates (R7RS) ---

;; (exact-integer? x) -- #t if x is both exact and an integer.
(define (exact-integer? x)    (and (integer? x) (exact? x)))
;; (infinite? x) -- #t if x is positive or negative infinity.
(define (infinite? x)         (call-static 'System.Double 'IsInfinity (todouble x)))
;; (nan? x) -- #t if x is IEEE NaN (not-a-number).
(define (nan? x)              (call-static 'System.Double 'IsNaN (todouble x)))
;; (finite? x) -- #t if x is a finite number (not infinite and not NaN).
(define (finite? x)           (not (or (infinite? x) (nan? x))))
;; (atan2 y x) -- two-argument arc-tangent; returns angle in radians.
(define (atan2 y x)           (call-static 'System.Math 'Atan2 (todouble y) (todouble x)))
;; (floor-quotient x y) -- greatest integer q such that q*y <= x (floor toward -inf).
;; (floor-remainder x y) -- x - y * (floor-quotient x y).
;; Works correctly for negative operands and for exact BigInteger arguments.
;; Example: (floor-quotient  10  3) ==>  3
;; Example: (floor-quotient -10  3) ==> -4
;; Example: (floor-remainder 10  3) ==>  1
;; Example: (floor-remainder -10 3) ==>  2
(define (floor-quotient x y)
  (let ((q (quotient  x y))
        (r (remainder x y)))
    (if (or (zero? r)
            (and (positive? r) (positive? y))
            (and (negative? r) (negative? y)))
        q
        (- q 1))))
(define (floor-remainder x y) (- x (* y (floor-quotient x y))))
;; floor-quotient / floor-remainder: division rounding toward negative infinity.
;; truncate-quotient / truncate-remainder: aliases for quotient/remainder (toward zero).
(define (truncate-quotient x y)  (quotient  x y))
(define (truncate-remainder x y) (remainder x y))
;; (bit-not x) -- bitwise complement: ~x  (equals -(x+1) for signed integers).
;; Example: (bit-not 0) ==> -1
(define (bit-not x)           (- -1 x))
;; (arithmetic-shift x n) -- shift x left by n bits (right shift for n < 0).
;; Uses floor division for right shifts so that negative numbers shift correctly.
;; Example: (arithmetic-shift  1  4) ==>  16
;; Example: (arithmetic-shift 16 -2) ==>   4
;; Example: (arithmetic-shift -1 -1) ==>  -1  (sign-extending right shift)
(define (arithmetic-shift x n)
  (if (>= n 0)
      (* x (expt 2 n))
      (floor-quotient x (expt 2 (neg n)))))
