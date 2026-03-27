(call-static 'System.Console 'Write ", records")

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
  
(define (record? r)            (and (vector? r) (= (vector-ref r 0) 'record)))
(define (record-name r)        (vector-ref r 1))
(define (record-instance r)    (vector-ref r 2))
(define (record-fields r)      (vector-ref r 3))
(define (record-values r)      (vector-ref r 4))
(define (record-field-get r f) 
   (let ((fields (member f (vector->list (record-fields r))))
         (len    (vector-length (record-fields r))))
        (if (pair? fields) 
            (vector-ref (record-values r) (- len (length fields)))
            '() )))
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

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Multiple values -- more work
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

