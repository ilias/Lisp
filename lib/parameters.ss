(call-static 'System.Console 'Write ", parameters")

(define (make-parameter val . rest)
  (let* ((conv    (if (null? rest) (lambda (x) x) (car rest)))
         (current (conv val)))
    (lambda args
      (if (null? args)
          current
          (set! current (conv (car args)))))))

(macro parameterize ()
  ((_ () body...)               (begin body...))
  ((_ ((p v) rest...) body...)  (let* ((?p   p)
                                       (?old (?p)))
                                   (dynamic-wind
                                     (lambda () (?p v))
                                     (lambda () (parameterize (rest...) body...))
                                     (lambda () (?p ?old))))))

; (define current-indent (make-parameter 0))
; (current-indent)                         ==> 0
; (parameterize ((current-indent 4))
;   (current-indent))                      ==> 4
; (current-indent)                         ==> 0  (restored)

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Random numbers
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

