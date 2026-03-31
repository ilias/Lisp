;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Characters (backed by System.Char)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", char")

;; (char? c) -- #t if c is a character object.
(define (char? c)            (= (call c 'GetType) (get-type "System.Char")))

;; Case-sensitive character comparisons (variadic -- all consecutive pairs are compared).
;; (char<? c1 c2 ...)  -- strict ascending order
;; (char<=? c1 c2 ...) -- non-strict ascending
;; (char=? c1 c2 ...)  -- all equal
;; (char<>? c1 c2 ...) -- all unequal (pairwise)
;; (char>=? c1 c2 ...) -- non-strict descending
;; (char>? c1 c2 ...)  -- strict descending
(define (char<? . l)         (COMPARE-ALL (lambda (a b) (<  (STRCMP? a b #f) 0)) ,@l))
(define (char<=? . l)        (COMPARE-ALL (lambda (a b) (<= (STRCMP? a b #f) 0)) ,@l))
(define (char=? . l)         (COMPARE-ALL (lambda (a b) (=  (STRCMP? a b #f) 0)) ,@l))
(define (char<>? . l)        (COMPARE-ALL (lambda (a b) (<> (STRCMP? a b #f) 0)) ,@l))
(define (char>=? . l)        (COMPARE-ALL (lambda (a b) (>= (STRCMP? a b #f) 0)) ,@l))
(define (char>?  . l)        (COMPARE-ALL (lambda (a b) (>  (STRCMP? a b #f) 0)) ,@l))
;; Case-insensitive variants (char-ci*?):
(define (char-ci<? . l)      (COMPARE-ALL (lambda (a b) (<  (STRCMP? a b #t) 0)) ,@l))
(define (char-ci<=? . l)     (COMPARE-ALL (lambda (a b) (<= (STRCMP? a b #t) 0)) ,@l))
(define (char-ci=? . l)      (COMPARE-ALL (lambda (a b) (=  (STRCMP? a b #t) 0)) ,@l))
(define (char-ci<>? . l)     (COMPARE-ALL (lambda (a b) (<> (STRCMP? a b #t) 0)) ,@l))
(define (char-ci>=? . l)     (COMPARE-ALL (lambda (a b) (>= (STRCMP? a b #t) 0)) ,@l))
(define (char-ci>?  . l)     (COMPARE-ALL (lambda (a b) (>  (STRCMP? a b #t) 0)) ,@l))

;; Character classification predicates:
;; (char-alphabetic? c) -- #t if c is a Unicode letter
;; (char-numeric? c)    -- #t if c is a decimal digit
;; (char-lower-case? c) -- #t if c is lowercase
;; (char-upper-case? c) -- #t if c is uppercase
;; (char-whitespace? c) -- #t if c is whitespace (space, tab, newline, etc.)
(define (char-alphabetic? c) (call-static 'System.Char    'IsLetter c))
(define (char-numeric? c)    (call-static 'System.Char    'IsDigit c))
(define (char-lower-case? c) (call-static 'System.Char    'IsLower c))
(define (char-upper-case? c) (call-static 'System.Char    'IsUpper c))
(define (char-whitespace? c) (call-static 'System.Char    'IsWhiteSpace c))

;; (char-upcase c)   -- return the uppercase version of c.
;; (char-downcase c) -- return the lowercase version of c.
(define (char-upcase c)      (call-static 'System.Char    'ToUpper c))
(define (char-downcase c)    (call-static 'System.Char    'ToLower c))

;; (char->integer c) -- Unicode code point of c.
;; (integer->char i) -- character with Unicode code point i.
;; Example: (char->integer #\A) ==> 65
;; Example: (integer->char 65)  ==> #\A
(define (char->integer c)    (call-static 'System.Convert 'ToInt32 c))
(define (integer->char i)    (call-static 'System.Convert 'ToChar i))

;; (char->digit c [radix]) -- convert character to a numeric digit value.
;; Returns the digit value in [0, radix) or #f if c is not a valid digit.
;; Default radix is 10.
;; Example: (char->digit #\7)     ==> 7
;; Example: (char->digit #\b 16)  ==> 11
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
;; R7RS digit-value: decimal digit value (0-9) or #f for non-decimal chars.
;; Example: (digit-value #\5) ==> 5  ;  (digit-value #\a) ==> #f
(define (digit-value c)
  (let ((n (char->integer c)))
    (if (and (>= n 48) (<= n 57)) (- n 48) #f)))
;; (char-punctuation? c) -- #t if c is a punctuation character (e.g. . , ! ?).
(define (char-punctuation? c)  (call-static 'System.Char 'IsPunctuation c))
;; (char-symbol? c)      -- #t if c is a mathematical or currency symbol.
(define (char-symbol? c)       (call-static 'System.Char 'IsSymbol c))
