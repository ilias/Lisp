(call-static 'System.Console 'Write ", pair")

;; --- Core pair / list constructors and accessors ---

;; (append list...) -- concatenate zero or more lists into one.
;; The last argument need not be a list (creates an improper list).
;; Example: (append '(1 2) '(3 4) '(5)) ==> (1 2 3 4 5)
(define (append . args) 
    (let f ((ls '()) (args args))
      (if (null? args)
          ls
          (let g ((ls ls))
            (if (null? ls)
                (f (car args) (cdr args))
                (cons (car ls) (g (cdr ls))) )))))

;; (assoc key alist [cmp]) -- search alist for a pair whose car matches key.
;; Uses equal? by default; supply cmp for a custom comparator.
;; Returns the matching pair, or #f if not found.
;; Example: (assoc 'b '((a 1) (b 2) (c 3))) ==> (b 2)
(define (assoc thing alist . rest)
  (let ((cmp (if (null? rest) equal? (car rest))))
    (let loop ((l alist))
      (cond ((null? l)                  #f)
            ((cmp thing (car (car l)))  (car l))
            (else                       (loop (cdr l)))))))

;; (car pair) -- return the first element (head) of a pair.
(define (car ls)        (get ls 'car))
;; (cdr pair) -- return the second element (tail) of a pair.
(define (cdr ls)        (get ls 'cdr))
;; caar..cddddr -- composed car/cdr accessors (R7RS §6.4).
;; These are shorthand for common nested car/cdr combinations.
;; Example: (cadr '(a b c)) ==> b   (same as (car (cdr ...)))
;; Example: (caddr '(a b c)) ==> c
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

;; (cons a ls) -- create a new pair with car=a and cdr=ls.
(define (cons a ls)     (call-static 'Lisp.Pair 'Cons a ls))
;; (length lst) -- return the number of elements in list lst.
(define (length x)      (get x 'Count))
;; (list x...) -- create a proper list from the given arguments.
;; Example: (list 1 2 3) ==> (1 2 3)
(define (list . x)      x)

;; (list? x) -- #t if x is a proper list (including the empty list).
;; Uses the tortoise-and-hare algorithm to detect cycles; circular lists return #f.
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

;; (list-ref lst n) -- return the element at zero-based index n.
;; Example: (list-ref '(a b c d) 2) ==> c
(define (list-ref l n)  (if (= n 0) (car l) (list-ref (cdr l) (- n 1))))
;; (list-tail lst n) -- return the sublist starting at index n.
;; Example: (list-tail '(a b c d) 2) ==> (c d)
(define (list-tail l n) (if (= n 0) l (list-tail (cdr l) (- n 1))))

;; (member x lst [cmp]) -- search for x in lst using equal? (or custom cmp).
;; Returns the first tail of lst whose car matches x, or #f.
;; Example: (member 3 '(1 2 3 4)) ==> (3 4)
(define (member x y . rest)
  (let ((cmp (if (null? rest) equal? (car rest))))
    (let loop ((l y))
      (cond ((null? l)        #f)
            ((cmp x (car l))  l)
            (else             (loop (cdr l)))))))

;; nil -- the empty list '().
(define nil             '())
;; (null? x) -- #t if x is the empty list.
(define (null? x)       (call-static 'Lisp.Pair 'IsNull x))
;; (pair? x) -- #t if x is a cons pair (non-null).
(define (pair? obj) 
  (cond ((null? obj) #f) 
	    ((eqv? (call obj 'GetType) (get-type "Lisp.Pair")) #t)
	    (else #f)))
;; (reverse lst) -- return a new list with elements in reverse order.
;; Example: (reverse '(1 2 3)) ==> (3 2 1)
(define (reverse ls)
    (let rev ((ls ls) (new '()))
      (if (null? ls)
          new
          (rev (cdr ls) (cons (car ls) new)))))

;; (set-car! pair val) -- mutate the car of pair to val.
(define (set-car! ls a) (set ls 'car a))
;; (set-cdr! pair val) -- mutate the cdr of pair to val.
(define (set-cdr! ls a) (set ls 'cdr a))

;; (assq key alist) -- like assoc but uses eq? (identity) for comparison.
(define (assq thing alist)
   (cond ((null? alist)                #f)
         ((eq?  (car (car alist)) thing) (car alist))
         (else                           (assq thing (cdr alist)))))

;; (assv key alist) -- like assoc but uses eqv? for comparison.
(define (assv thing alist)
   (cond ((null? alist)                #f)
         ((eqv? (car (car alist)) thing) (car alist))
         (else                           (assv thing (cdr alist)))))

;; (memq x lst) -- like member but uses eq? (identity) for comparison.
(define (memq x y)
  (cond ((null? y)      #f)
        ((eq? x (car y)) y)
        (else             (memq x (cdr y)))))

;; (memv x lst) -- like member but uses eqv? for comparison.
(define (memv x y)
  (cond ((null? y)       #f)
        ((eqv? x (car y)) y)
        (else              (memv x (cdr y)))))

;; (list-copy lst) -- return a shallow copy of lst.
(define (list-copy ls)  (map (lambda (x) x) ls))

;; (iota n [start [step]]) -- return a list of n numbers starting from start (default 0),
;; incrementing by step (default 1).
;; Example: (iota 5)        ==> (0 1 2 3 4)
;; Example: (iota 4 1)      ==> (1 2 3 4)
;; Example: (iota 5 0 2)    ==> (0 2 4 6 8)
(define (iota n . rest)
  (let ((start (if (null? rest) 0 (car rest)))
        (step  (if (or (null? rest) (null? (cdr rest))) 1 (cadr rest))))
    (let loop ((i 0) (acc '()))
      (if (= i n)
          (reverse acc)
          (loop (+ i 1) (cons (+ start (* i step)) acc))))))

;; (adjoin e lst) -- return lst with e prepended only if e is not already a member.
(define (adjoin e l)    (if (member e l) l (cons e l)))
;; (union l1 l2) -- return a list containing every element from either l1 or l2 (no duplicates).
;; Example: (union '(a b c) '(b c d)) ==> (a b c d)
(define (union l1 l2)
  (cond ((null? l1) l2)
	    ((null? l2) l1)
	    (else       (union (cdr l1) (adjoin (car l1) l2)))))

;; (intersection l1 l2) -- return elements present in both l1 and l2.
;; Example: (intersection '(a b c) '(b c d)) ==> (b c)
(define (intersection l1 l2)
  (cond ((null? l1)           l1)
	    ((null? l2)           l2)
	    ((member (car l1) l2) (cons (car l1) (intersection (cdr l1) l2)))
	    (else                 (intersection (cdr l1) l2))))

;; (difference l1 l2) -- return elements in l1 that are not in l2.
;; Example: (difference '(a b c d) '(b d)) ==> (a c)
(define (difference l1 l2)
  (cond ((null? l1)           l1)
	    ((member (car l1) l2) (difference (cdr l1) l2))
	    (else                 (cons (car l1) (difference (cdr l1) l2)))))

;; --- Higher-order list utilities ---

;; (filter pred lst) -- return list of elements for which pred returns true.
;; Example: (filter even? '(1 2 3 4 5 6)) ==> (2 4 6)
(define (filter pred lst)
  (cond ((null? lst) '())
        ((pred (car lst)) (cons (car lst) (filter pred (cdr lst))))
        (else (filter pred (cdr lst)))))

;; (foldl f init lst) -- left fold: (f (f (f init e1) e2) e3) ...
;; f receives (accumulator element).
;; Example: (foldl + 0 '(1 2 3 4)) ==> 10
;; Example: (foldl cons '() '(1 2 3)) ==> (((). 1) . 2) . 3) -- builds reverse
(define (foldl f init lst)
  (if (null? lst) init (foldl f (f init (car lst)) (cdr lst))))

;; (foldr f init lst) -- right fold: (f e1 (f e2 (f e3 init)))
;; f receives (element accumulator).
;; Example: (foldr cons '() '(1 2 3)) ==> (1 2 3)
(define (foldr f init lst)
  (if (null? lst) init (f (car lst) (foldr f init (cdr lst)))))

;; (any pred lst) -- return #t if pred is true for at least one element.
;; Example: (any even? '(1 3 5 6)) ==> #t
(define (any pred lst)
  (cond ((null? lst) #f) ((pred (car lst)) #t) (else (any pred (cdr lst)))))

;; (every pred lst) -- return #t if pred is true for every element.
;; Example: (every even? '(2 4 6)) ==> #t
(define (every pred lst)
  (cond ((null? lst) #t) ((not (pred (car lst))) #f) (else (every pred (cdr lst)))))

;; (take lst n) -- return the first n elements of lst.
;; Example: (take '(a b c d e) 3) ==> (a b c)
(define (take lst n)
  (if (or (= n 0) (null? lst)) '() (cons (car lst) (take (cdr lst) (- n 1)))))

;; drop is an alias for list-tail: skip the first n elements.
(define drop list-tail)

;; (last lst) -- return the last element of lst.
;; Example: (last '(1 2 3)) ==> 3
(define (last lst)
  (if (null? (cdr lst)) (car lst) (last (cdr lst))))

;; (last-pair lst) -- return the last pair (cons cell) of lst.
;; Example: (last-pair '(1 2 3)) ==> (3)
(define (last-pair lst)
  (if (null? (cdr lst)) lst (last-pair (cdr lst))))

;; (zip list...) -- transpose a list of lists into a list of tuples.
;; Example: (zip '(1 2 3) '(a b c)) ==> ((1 a) (2 b) (3 c))
(define (zip . lists)     (apply map list lists))

;; (flatten lst) -- recursively flatten a nested list into a flat list.
;; Example: (flatten '(1 (2 3) (4 (5 6)))) ==> (1 2 3 4 5 6)
(define (flatten lst)
  (cond ((null? lst) '())
        ((pair? (car lst)) (append (flatten (car lst)) (flatten (cdr lst))))
        (else (cons (car lst) (flatten (cdr lst))))))

;; (count pred lst) -- count elements for which pred returns true.
;; Example: (count even? '(1 2 3 4 5)) ==> 2
(define (count pred lst)  (foldl (lambda (acc x) (if (pred x) (+ acc 1) acc)) 0 lst))

;; (flat-map f lst) -- map f over lst and concatenate the results.
;; f must return a list.  Equivalent to (apply append (map f lst)).
;; Example: (flat-map (lambda (x) (list x (* x x))) '(1 2 3)) ==> (1 1 2 4 3 9)
(define (flat-map f lst)  (apply append (map f lst)))

;; Positional selectors for list elements (R7RS names).
(define second  cadr)
(define third   caddr)
(define fourth  cadddr)
(define (fifth x)         (car (cddddr x)))

;; (atom? x) -- #t if x is not a pair (i.e., is an atomic value).
(define (atom? x)         (not (pair? x)))

;; --- Functional combinators ---

;; (identity x) -- return x unchanged.
(define (identity x) x)

;; (compose f g ...) -- return a function that applies the functions right-to-left.
;; Example: ((compose string-upcase symbol->string) 'hello) ==> "HELLO"
;; Example: ((compose (lambda (x) (* x 2)) (lambda (x) (+ x 1))) 3) ==> 8
(define (compose . fns)
  (reduce (lambda (f g) (lambda (x) (f (g x)))) identity fns))

;; (negate pred) -- return a predicate that is the logical inverse of pred.
;; Example: ((negate even?) 3) ==> #t
(define (negate pred)     (lambda args (not (apply pred args))))

;; --- Additional list operations ---

;; (make-list n [fill]) -- return a list of n elements, each equal to fill (default #f).
;; Example: (make-list 3 0) ==> (0 0 0)
(define (make-list n . rest)
  (let ((fill (if (null? rest) #f (car rest))))
    (let loop ((i n) (acc '()))
      (if (= i 0) acc (loop (- i 1) (cons fill acc))))))

;; (find pred lst) -- return the first element satisfying pred, or #f.
;; Example: (find even? '(1 3 4 7)) ==> 4
(define (find pred lst)
  (cond ((null? lst) #f) ((pred (car lst)) (car lst)) (else (find pred (cdr lst)))))

;; (remove pred lst) -- return lst without elements satisfying pred.
;; Example: (remove even? '(1 2 3 4 5)) ==> (1 3 5)
(define (remove pred lst)          (filter (negate pred) lst))

;; (filter-map f lst) -- map f over lst keeping only truthy results.
;; Example: (filter-map (lambda (x) (and (even? x) (* x 10))) '(1 2 3 4)) ==> (20 40)
(define (filter-map f lst)
  (let loop ((lst lst) (acc '()))
    (if (null? lst)
        (reverse acc)
        (let ((v (f (car lst))))
          (loop (cdr lst) (if v (cons v acc) acc))))))

;; (delete-duplicates lst) -- return lst with repeated occurrences removed (preserves order).
;; Example: (delete-duplicates '(1 2 1 3 2 4)) ==> (1 2 3 4)
(define (delete-duplicates lst)
  (let loop ((lst lst) (seen '()))
    (cond ((null? lst)              (reverse seen))
          ((member (car lst) seen)  (loop (cdr lst) seen))
          (else                     (loop (cdr lst) (cons (car lst) seen))))))

;; (list-set lst n val) -- return a new list equal to lst but with index n replaced by val.
;; Example: (list-set '(a b c d) 2 'X) ==> (a b X d)
(define (list-set lst n val)
  (let loop ((lst lst) (i 0) (acc '()))
    (if (null? lst)
        (reverse acc)
        (loop (cdr lst) (+ i 1) (cons (if (= i n) val (car lst)) acc)))))

;; (concatenate lsts) -- concatenate a list of lists (alias for (apply append lsts)).
;; Example: (concatenate '((1 2) (3 4) (5))) ==> (1 2 3 4 5)
(define (concatenate lsts)         (apply append lsts))

;; append-map is an alias for flat-map.
(define append-map                 flat-map)
;; for-all is an alias for every.
(define for-all                    every)
;; exists is an alias for any.
(define exists                     any)
