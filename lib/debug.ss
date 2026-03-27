(call-static 'System.Console 'Write ", trace")

(define (*traceHash*)      (get 'Lisp.Expression 'traceHash))
(define (trace x)          (set 'Lisp.Expression 'Trace (if x #t #f)))
(define (trace-add . x)    (map (lambda (a) (call (*traceHash*) 'Add a)) x))
(define (trace-all)        (trace-add '_all_))
(define (trace-clear)      (call (*traceHash*) 'Clear))
(define (trace-remove . x) (map (lambda (a) (call (*traceHash*) 'Remove a)) x))
                              
; (trace #t)
; (trace-all)
; or
; (trace #t)
; (trace-clear)
; (trace-add 'member 'macro 'cdr 'null?)

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Stats -- Lisp.Program.Stats / Iterations
;; (stats #t)  -- enable timing + iteration count per top-level eval
;; (stats #f)  -- disable
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(define (stats x)
  (set 'Lisp.Program 'Stats (if x #t #f))
  (if x (call-static 'Lisp.Program 'ResetTotals)))
(define (stats-reset)   (call-static 'Lisp.Program 'ResetTotals))
(define (stats-total)   (call-static 'Lisp.Program 'PrintTotals))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; show-lines -- echo each top-level form as it executes
;; (show-lines #t)  -- enable
;; (show-lines #f)  -- disable
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(define (show-input-lines x)
  (set 'Lisp.Program 'ShowInputLines (if x #t #f)))

(define (showlines x)
  (show-input-lines x))

(define (help)
  (begin
    (consoleLine "Available commands:")
    (consoleLine "  (stats #t/#f)        Enable or disable per-expression timing and counters")
    (consoleLine "  (stats-reset)        Clear accumulated stats totals")
    (consoleLine "  (stats-total)        Show the accumulated stats summary")
    (consoleLine "  (trace #t/#f)        Enable or disable tracing output")
    (consoleLine "  (trace-add 'name)    Trace a specific symbol after trace is enabled")
    (consoleLine "  (colors #t/#f)       Enable or disable console colors")
    (consoleLine "  (p-adic 7 [digits])  Display exact results in 7-adic form; optional digits set precision")
    (consoleLine "  (disasm-verbose #t/#f) Show or hide trivial disassembly source labels")
    (consoleLine "  (show-lines #t/#f)   Echo each top-level form before it runs")
    (consoleLine "  (env)                List global procedures")
    (consoleLine "  (env 'name)          Show a specific global procedure")
    (consoleLine "  (disasm proc)        Disassemble a compiled procedure")
    (consoleLine "  (exit)               Leave the REPL")
    '()))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Macros -- c# type Lisp.Macro
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", macros")

(define (macro? x)      (call (get 'Lisp.Macro 'macros) 'ContainsKey x))
(define (macros->vector) 
  (new 'System.Collections.ArrayList (get (get 'Lisp.Macro 'macros) 'Keys)))
(define (macros->list)  (vector->list (macros->vector)))
(define (macro-body x)  (cdr (get (get 'Lisp.Macro 'macros) 'Item x)))
(define (macro-const x) (car (get (get 'Lisp.Macro 'macros) 'Item x)))
(define *displayMacros* 
  (lambda ()
    (let ((x (macros->vector)))
         (do ((y (- (vector-length x) 1) (- y 1)))
             ((< y 0) "")
             (display "\nMACRO {0} " (vector-ref x y))
             (letrec ((const (macro-const (vector-ref x y)))
                      (body  (macro-body  (vector-ref x y))))
                     (display (if (null? const) "()" const))
                     (map (lambda (x1) 
                                  (if (null? x1)
                                      (display "---")
                                      (display "\n      {0}\n          {1}" (car x1) (car (cdr x1)))))
                          body ))))))
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Procedures -- c# type Lisp.Closure
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", procedures")

(define (PROCEDURE? x)   (call (get (get 'Lisp.Program 
                                         'current) 
                                    'initEnv) 
                               'Apply x))
(define (procedure? x)   (closure? x))
(define (closure? x)     (call (get-type "Lisp.Closure") 'IsInstanceOfType x))
(define (closure-args x) (get (PROCEDURE? x) 'ids))
(define (closure-body x) (get (PROCEDURE? x) 'body))
(define (procedures->vector)
  (new 'System.Collections.ArrayList (get (get (get (get 'Lisp.Program 'current) 'initEnv) 'table) 'Keys)))
(define (procedures->list) (vector->list (procedures->vector)))
(define *displayProcedures* 
  (lambda ()
    (let ((x (procedures->vector)))
         (do ((y (- (vector-length x) 1) (- y 1)))
             ((< y 0) "Done")
             (let ((val (PROCEDURE? (vector-ref x y))))
               (when (closure? val)
                 (display "\nCLOSURE {0} " (vector-ref x y))
                 (let ((args (get val 'ids))
                       (body (get val 'body)))
                   (display (if (null? args) "()" args))
                   (if (null? body)
                       (display " ()\n")
                       (display "\n{0}\n" body)))))))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Lists -- c# type Lisp.Pair
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

