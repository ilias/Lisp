(define (fallback-emits)
  (->int (call-static 'Lisp.Program 'GetTotalInterpEmits)))

(define (fallback-execs)
  (->int (call-static 'Lisp.Program 'GetTotalInterpExecs)))

(define (fallback-tree-walk)
  (->int (call-static 'Lisp.Program 'GetTotalTreeWalkCalls)))

(define (print-fallback-row label)
  (display "{0,-28} emits={1} execs={2} tree-walk={3}"
           label
           (fallback-emits)
           (fallback-execs)
           (fallback-tree-walk))
  (newline))

(define (run-fallback-case label thunk)
  (stats-reset)
  (thunk)
  (print-fallback-row label))

(display "Fallback smoke report")
(newline)
(display "====================")
(newline)

(run-fallback-case
  "arithmetic / closures"
  (lambda ()
    (let loop ((i 0) (acc 0))
      (if (= i 500)
          acc
          (loop (+ i 1) (+ acc i))))))

(run-fallback-case
  "quasiquote splice"
  (lambda ()
    (let ((xs '(2 3 4)))
      `(1 ,@xs 5))))

(run-fallback-case
  "let-syntax"
  (lambda ()
    (let-syntax ((twice (syntax-rules () ((_ x) (+ x x)))))
      (twice 21))))

(run-fallback-case
  "define-library"
  (lambda ()
    (eval '(define-library 'fallback-smoke-lib
             (export smoke-value)
             (begin
               (define smoke-value 99))))
    (eval '(import '(only fallback-smoke-lib smoke-value)))
    smoke-value))

(run-fallback-case
  "dynamic eval"
  (lambda ()
    (let ((x 20))
      (eval '(+ x 22)))))

(run-fallback-case
  "call/cc-full"
  (lambda ()
    (let ((resume #f))
      (call/cc-full (lambda (k)
                      (set! resume k)
                      (k 1)
                      'done))
      (resume #f))))

(display "\nTip: any non-zero count here is a candidate for the next VM reduction pass.")
(newline)