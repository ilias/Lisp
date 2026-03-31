(call-static 'System.Console 'Write ", records")

;; --- Record types (struct-like compound values) ---
;;
;; Records are represented as special 5-element vectors:
;;   #(record  type-symbol  instance-id  #(fields...)  #(values...))
;;
;; The (record ...) macro is the primary interface.  Synopsis:
;;
;;   (record define <point> (x 0) (y 0))    -- define record type with default values
;;   (record define <point> x y)             -- shorthand: fields default to 0
;;   (record p1 new <point>)                 -- create a new instance named p1
;;   (record p1 x)                           -- read field x of p1
;;   (record p1 x y)                         -- read several fields: returns a list
;;   (record p1 ! x 10)                      -- set field x to 10
;;   (record p1 ! (x 10) (y 20))             -- set multiple fields at once
;;   (record p1 ? <point>)                   -- #t if p1 is a <point> record
;;   (record p1 ! act (lambda (n) (* n n)))  -- store a lambda in field act
;;   (record p1 call act 3)                  -- call the lambda stored in act with arg 3
;;   (record p1)                             -- return the values vector of p1
;;
;; Example:
;;   (record define <point> (x 0) (y 0))
;;   (record p new <point>)
;;   (record p ! x 3  y 4)
;;   (record p x y)          ==>  (3 4)
;;   (record p ? <point>)    ==>  #t
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

;; (record? r) -- #t if r is a record (a vector whose first element is the symbol 'record).
(define (record? r)            (and (vector? r) (= (vector-ref r 0) 'record)))
;; (record-name r) -- return the type symbol of record r.
(define (record-name r)        (vector-ref r 1))
;; (record-instance r) -- return the unique instance-id of r (a generated symbol).
(define (record-instance r)    (vector-ref r 2))
;; (record-fields r) -- return the vector of field name symbols of r.
(define (record-fields r)      (vector-ref r 3))
;; (record-values r) -- return the vector of current field values of r.
(define (record-values r)      (vector-ref r 4))
;; (record-field-get r field) -- return the value of named field in record r.
(define (record-field-get r f) 
   (let ((fields (member f (vector->list (record-fields r))))
         (len    (vector-length (record-fields r))))
        (if (pair? fields) 
            (vector-ref (record-values r) (- len (length fields)))
            '() )))
;; (record-field-set! r field val) -- mutate named field in record r to val.
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
