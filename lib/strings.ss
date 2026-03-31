(call-static 'System.Console 'Write ", string")

;; --- String constructors and predicates ---

;; (string char...) -- create a string from a list of characters.
(define (string . lst)      (list->string lst))
;; (string? x) -- #t if x is a System.String.
(define (string? x)         (= (call x 'GetType) (get-type "System.String")))

;; --- String comparison (case-sensitive) ---
;; All comparison functions accept two or more strings and chain like numeric comparisons.
;; (string<? "abc" "abd") ==> #t
(define (string<? . l)      (COMPARE-ALL (lambda (a b) (<  (STRCMP? a b #f) 0)) ,@l))
(define (string<=? . l)     (COMPARE-ALL (lambda (a b) (<= (STRCMP? a b #f) 0)) ,@l))
(define (string=? . l)      (COMPARE-ALL (lambda (a b) (=  (STRCMP? a b #f) 0)) ,@l))
(define (string<>? . l)     (COMPARE-ALL (lambda (a b) (<> (STRCMP? a b #f) 0)) ,@l))
(define (string>=? . l)     (COMPARE-ALL (lambda (a b) (>= (STRCMP? a b #f) 0)) ,@l))
(define (string>?  . l)     (COMPARE-ALL (lambda (a b) (>  (STRCMP? a b #f) 0)) ,@l))

;; --- String comparison (case-insensitive) ---
(define (string-ci<? . l)   (COMPARE-ALL (lambda (a b) (<  (STRCMP? a b #t) 0)) ,@l))
(define (string-ci<=? . l)  (COMPARE-ALL (lambda (a b) (<= (STRCMP? a b #t) 0)) ,@l))
(define (string-ci=? . l)   (COMPARE-ALL (lambda (a b) (=  (STRCMP? a b #t) 0)) ,@l))
(define (string-ci<>? . l)  (COMPARE-ALL (lambda (a b) (<> (STRCMP? a b #t) 0)) ,@l))
(define (string-ci>=? . l)  (COMPARE-ALL (lambda (a b) (>= (STRCMP? a b #t) 0)) ,@l))
(define (string-ci>?  . l)  (COMPARE-ALL (lambda (a b) (>  (STRCMP? a b #t) 0)) ,@l))

;; --- String accessors and basic operations ---

;; (string-length s) -- number of characters in s.
(define (string-length s)   (get s 'Length))
;; (string-ref s n) -- character at zero-based index n.
(define (string-ref s n)    (get s 'Chars (inexact->exact n)))
;; (string-set! s n c) -- return a new string with character at index n replaced by c.
;; Note: strings are immutable in .NET; this returns a new string.
(define (string-set! s n c) 
  (string-append (substring s 0 n) 
                 (call c 'ToString) 
                 (substring s (+ n 1) (string-length s)))) 

(define (STRAPPEND s a)     (call s 'Insert (string-length s) (call a 'ToString)))
;; (string-copy s) -- return a copy of string s.
(define (string-copy a)     (string-append a))
;; (string-append s...) -- concatenate all strings.
;; Example: (string-append "Hello" ", " "World!") ==> "Hello, World!"
(define (string-append . l) (reduce STRAPPEND "" l))
;; (string-upcase s) -- return s converted to upper case.
(define (string-upcase s)   (call s 'ToUpper))
;; (string-downcase s) -- return s converted to lower case.
(define (string-downcase s) (call s 'ToLower))
;; (string-trim s) -- remove leading and trailing whitespace.
(define (string-trim s)     (call s 'Trim))
;; (string-trim-right s) -- remove trailing whitespace.
(define (string-trim-right s) (call s 'TrimEnd))
;; (string-trim-left s) -- remove leading whitespace.
(define (string-trim-left s)  (call s 'TrimStart))
;; (string-contains s sub) -- #t if sub appears anywhere in s.
;; Example: (string-contains "hello world" "world") ==> #t
(define (string-contains s sub) (not (= -1 (call s 'IndexOf sub))))
;; (substring s from to) -- extract characters [from, to) from s.
;; Example: (substring "hello" 1 4) ==> "ell"
(define (substring s f l)   (call s 'Substring (inexact->exact f) (inexact->exact (- l f))))
;; (string-fill! s c) -- return a new string of the same length with every character replaced by c.
(define (string-fill! s c)
  (let ((n (string-length s)))
    (do ((i 0 (+ i 1)))
        ((= i n) s)
        (set! s (string-set! s i c))))) 

;; (string->list s) -- convert string to a list of characters.
;; Example: (string->list "abc") ==> (#\a #\b #\c)
(define (string->list s)
  (do ((i (- (string-length s) 1) (- i 1))
       (ls '() (cons (string-ref s i) ls)))
      ((< i 0) ls)
      "nothing")) 
;; (list->string chars) -- convert a list of characters to a string.
;; Example: (list->string '(#\a #\b #\c)) ==> "abc"
(define (list->string ls)   (string-append ,@(map (lambda (x) (call x 'ToString)) ls)))

;; (make-string n [char]) -- return a string of n copies of char (default space).
;; Example: (make-string 5 #\x) ==> "xxxxx"
(define (make-string n . rest)
  (if (null? rest)
      (new 'System.String #\  n)
      (new 'System.String (car rest) n)))

;; (string->integer s) -- parse s as an integer (Int32 or BigInteger).
(define (string->integer s) (try (call-static 'System.Convert 'ToInt32 s)
                                 (call-static 'System.Numerics.BigInteger 'Parse s)))
;; (string->real s) -- parse s as a double-precision float.
(define (string->real s)    (call-static 'System.Convert 'ToDouble s))

;; (string-split s delim) -- split string s on the delimiter string delim.
;; Returns a list of substrings.
;; Example: (string-split "a,b,c" ",") ==> ("a" "b" "c")
(define (string-split s delim)
  (let loop ((s s) (acc '()))
    (let ((i (call s 'IndexOf delim)))
      (if (< i 0)
          (reverse (cons s acc))
          (loop (substring s (+ i (string-length delim)) (string-length s))
                (cons (substring s 0 i) acc))))))

;; (string-join lst [sep]) -- join a list of strings with sep between them (default "").
;; Example: (string-join '("Hello" "World") ", ") ==> "Hello, World"
(define (string-join lst . rest)
  (let ((sep (if (null? rest) "" (car rest))))
    (if (null? lst)
        ""
        (foldl (lambda (acc x) (string-append acc sep x)) (car lst) (cdr lst)))))

;; (string-for-each f s) -- call f on each character of s in order (for side effects).
(define (string-for-each f s)
  (let ((n (string-length s)))
    (do ((i 0 (+ i 1))) ((= i n)) (f (string-ref s i)))))

;; (string-repeat s n) -- return s concatenated n times.
;; Example: (string-repeat "ab" 3) ==> "ababab"
(define (string-repeat s n)
  (let loop ((i 0) (acc ""))
    (if (= i n) acc (loop (+ i 1) (string-append acc s)))))

;; (string-index s pred) -- return the index of the first character satisfying pred, or #f.
;; Example: (string-index "hello" char-upper-case?) ==> #f
;; Example: (string-index "heLLo" char-upper-case?) ==> 2
(define (string-index s pred)
  (let ((n (string-length s)))
    (let loop ((i 0))
      (cond ((= i n) #f)
            ((pred (string-ref s i)) i)
            (else (loop (+ i 1)))))))

;; (string-map f s) -- apply f to each character of s and return the resulting string.
;; Example: (string-map char-upcase "hello") ==> "HELLO"
(define (string-map f s)       (list->string (map f (string->list s))))
;; (string->vector s) -- convert a string to a vector of characters.
(define (string->vector s)     (list->vector (string->list s)))
;; (vector->string v) -- convert a vector of characters to a string.
(define (vector->string v)     (list->string (vector->list v)))

;; (string-char-frequencies s) -- return an alist of (char count) pairs.
;; Example: (string-char-frequencies "banana") ==> ((#\a 3) (#\n 2) (#\b 1))  (order varies)
(define (string-char-frequencies s)
  (let loop ((chars (string->list s)) (acc '()))
    (if (null? chars)
        acc
        (let* ((c     (car chars))
               (entry (assv c acc)))
          (if entry
              (loop (cdr chars)
                    (map (lambda (p) (if (eqv? (car p) c) (list c (+ (cadr p) 1)) p)) acc))
              (loop (cdr chars) (cons (list c 1) acc)))))))
