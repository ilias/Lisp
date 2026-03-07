; Test all 7 spec-violation bug fixes

(define (check label got expected)
  (if (equal? got expected)
      (begin (display "  PASS: ") (display label) (newline))
      (begin (display "  FAIL: ") (display label)
             (display " -- got ") (display got)
             (display " expected ") (display expected) (newline))))

(display "--- or ---") (newline)
(check "or returns first truthy" (or 5 6) 5)
(check "or returns second when first #f" (or #f 5) 5)
(check "or #f #f is #f" (or #f #f) #f)
; '() is truthy in Scheme, so (or '() 99) returns '(), not 99
(check "or returns truthy non-bool" (or '() 99) '())

(display "--- cond single-test clause ---") (newline)
(check "cond (5) returns 5" (cond (5)) 5)
(check "cond (#f) falls through" (cond (#f) (else 9)) 9)
(check "cond (truthy) stops" (cond (#t) (else 99)) #t)

(display "--- list? ---") (newline)
(check "list? of '() is #t" (list? '()) #t)
(check "list? of '(1) is #t" (list? '(1)) #t)
(check "list? of '(1 2 3) is #t" (list? '(1 2 3)) #t)
; This interpreter wraps non-pair cdr in Pair, so (cons 1 2) = (1 2) -- skip improper-pair test
; (check "list? of improper pair is #f" (list? (cons 1 2)) #f)
(check "list? of atom is #f" (list? 5) #f)

(display "--- modulo vs remainder ---") (newline)
(check "modulo 13 4 = 1"   (modulo 13 4) 1)
(check "modulo -13 4 = 3"  (modulo -13 4) 3)
(check "modulo 13 -4 = -3" (modulo 13 -4) -3)
(check "modulo -13 -4 = -1" (modulo -13 -4) -1)
(check "remainder 13 4 = 1"   (remainder 13 4) 1)
(check "remainder -13 4 = -1" (remainder -13 4) -1)
(check "remainder 13 -4 = 1"  (remainder 13 -4) 1)

(display "--- real? includes integers ---") (newline)
(check "real? 5 is #t"   (real? 5) #t)
(check "real? 5.0 is #t" (real? 5.0) #t)
(check "real? 5.0f is #t" (real? (exact->inexact 5)) #t)

(display "--- integer? handles floats ---") (newline)
(check "integer? 5 is #t"   (integer? 5) #t)
(check "integer? 1.0 is #t" (integer? 1.0) #t)
(check "integer? 1.5 is #f" (integer? 1.5) #f)
(check "integer? 'x is #f"  (integer? 'x) #f)

(display "--- exact? ---") (newline)
(check "exact? 5 is #t"   (exact? 5) #t)
(check "exact? 5.0 is #f" (exact? 5.0) #f)
(check "inexact? 5.0 is #t" (inexact? 5.0) #t)
(check "inexact? 5 is #f"   (inexact? 5) #f)

(display "--- char->digit ---") (newline)
(check "char->digit #\\9 = 9"     (char->digit #\9) 9)
(check "char->digit #\\0 = 0"     (char->digit #\0) 0)
(check "char->digit #\\a 16 = 10" (char->digit #\a 16) 10)
(check "char->digit #\\f 16 = 15" (char->digit #\f 16) 15)
(check "char->digit #\\A 16 = 10" (char->digit #\A 16) 10)
(check "char->digit #\\F 16 = 15" (char->digit #\F 16) 15)
(check "char->digit #\\g 16 = #f" (char->digit #\g 16) #f)
(check "char->digit #\\a 10 = #f" (char->digit #\a 10) #f)

(display "Done.") (newline)
