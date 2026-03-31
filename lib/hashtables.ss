;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Hash tables (backed by System.Collections.Hashtable)
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Keys and values may be any Lisp object.
;; Equality uses the host .NET Hashtable default (Object.Equals / GetHashCode).

(call-static 'System.Console 'Write ", hashTables")

;; (make-hash-table) -- create a new, empty hash table.
;; make-eq-hash-table / make-eqv-hash-table are aliases.
(define (make-hash-table . args)       (new 'System.Collections.Hashtable))
(define make-eq-hash-table             make-hash-table)
(define make-eqv-hash-table            make-hash-table)
;; (hash-table? x) -- #t if x is a hash table.
(define (hash-table? x)                (eqv? (call x 'GetType) (get-type "System.Collections.Hashtable")))
;; (hash-table-set! ht key val) -- associate key with val in ht (mutating).
;; hash-table-put! is an alias.
(define (hash-table-set! ht k v)       (set ht 'Item k v))
(define hash-table-put!                hash-table-set!)
;; (hash-table-ref ht key [default-thunk]) -- retrieve value for key.
;; If key is absent and default-thunk is given, call it; otherwise raise an error.
;; Example: (hash-table-ref ht 'missing (lambda () 0)) ==> 0
(define (hash-table-ref ht k . default)
  (if (call ht 'ContainsKey k)
      (get ht 'Item k)
      (if (null? default)
          (error "hash-table-ref: key not found" k)
          ((car default)))))
;; (hash-table-ref/default ht key default) -- return value for key, or default if absent.
(define (hash-table-ref/default ht k default)
  (if (call ht 'ContainsKey k) (get ht 'Item k) default))
(define hash-table-get                 hash-table-ref/default)
;; (hash-table-delete! ht key) -- remove key and its value from ht.
(define (hash-table-delete! ht k)      (call ht 'Remove k))
;; (hash-table-clear! ht) -- remove all entries from ht.
(define (hash-table-clear! ht)         (call ht 'Clear))
;; (hash-table-size ht) -- number of key/value pairs in ht.
(define (hash-table-size ht)           (get ht 'Count))
;; (hash-table-exists? ht key) -- #t if key is present in ht.
;; hash-table-contains? is an alias.
(define (hash-table-exists? ht k)      (call ht 'ContainsKey k))
(define hash-table-contains?           hash-table-exists?)
;; (hash-table-keys ht) -- list of all keys in ht.
(define (hash-table-keys ht)
  (vector->list (new 'System.Collections.ArrayList (get ht 'Keys))))
;; (hash-table-values ht) -- list of all values in ht.
(define (hash-table-values ht)
  (vector->list (new 'System.Collections.ArrayList (get ht 'Values))))
;; (hash-table->alist ht) -- convert ht to an association list ((key . val)...).
;; Example: (hash-table->alist ht) ==> ((a . 1) (b . 2))
(define (hash-table->alist ht)
  (map (lambda (k) (cons k (get ht 'Item k))) (hash-table-keys ht)))
;; (alist->hash-table alist) -- create a hash table from an association list.
;; Example: (alist->hash-table '((a 1) (b 2))) ==> hash table with a=>1, b=>2
(define (alist->hash-table alist)
  (let ((ht (make-hash-table)))
    (for-each (lambda (pair) (hash-table-set! ht (car pair) (cadr pair))) alist)
    ht))
;; (hash-table-walk ht proc) -- call (proc key val) for every entry in ht.
;; hash-table-for-each is an alias.
(define (hash-table-walk ht proc)
  (for-each (lambda (k) (proc k (get ht 'Item k))) (hash-table-keys ht)))
(define (hash-table-for-each ht proc)  (hash-table-walk ht proc))
;; (hash-table-update! ht key f [default-thunk]) -- replace value at key with (f old-val).
;; If key absent, optional default-thunk is called to supply the initial value.
;; Example: (hash-table-update! ht 'count (lambda (n) (+ n 1)) (lambda () 0))
(define (hash-table-update! ht k f . default)
  (hash-table-set! ht k (f (apply hash-table-ref ht k default))))
;; (hash-table-update!/default ht key f default) -- like hash-table-update! with plain default value.
(define (hash-table-update!/default ht k f default)
  (hash-table-set! ht k (f (hash-table-ref/default ht k default))))
;; (hash-table-merge! ht1 ht2) -- add all entries from ht2 into ht1 (mutating ht1); return ht1.
(define (hash-table-merge! ht1 ht2)
  (hash-table-walk ht2 (lambda (k v) (hash-table-set! ht1 k v)))
  ht1)
;; (hash-table-copy ht) -- return a shallow copy of ht.
(define (hash-table-copy ht)           (alist->hash-table (hash-table->alist ht)))
;; (hash-table-map ht f) -- return a new hash table with each value replaced by (f key val).
;; Example: (hash-table-map ht (lambda (k v) (* v 2))) -- doubles all values
(define (hash-table-map ht f)
  (let ((result (make-hash-table)))
    (hash-table-walk ht (lambda (k v) (hash-table-set! result k (f k v))))
    result))

; (define ht (make-hash-table))
; (hash-table-set! ht 'a 1)
; (hash-table-set! ht 'b 2)
; (hash-table-ref ht 'a)          ==> 1
; (hash-table-ref/default ht 'x 0) ==> 0
; (hash-table-keys ht)             ==> (a b)
; (hash-table->alist ht)           ==> ((a . 1) (b . 2))

