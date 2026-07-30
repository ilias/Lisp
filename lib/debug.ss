
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Debugger helpers
;; (debug #t/#f) -- enable or disable the runtime debugger hooks.
;; (debug-step #t/#f) -- emit a debugger message before each evaluated form.
;; (debug-break 'name) -- register a breakpoint label for the debugger.
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(define (debug x)
  (set 'Lisp.Program 'Debug (if x #t #f)))

(define (debug-step x)
  (set 'Lisp.Program 'DebugSingleStep (if x #t #f)))

(define (debug-break x)
  (call-static 'Lisp.Program 'AddBreakpoint x))

;; --- Tracing support ---

;; (trace #t/#f) -- enable or disable execution tracing globally.
;; When enabled, every function call and return is printed.
(define (*traceHash*)      (get 'Lisp.Expression 'traceHash))
(define (trace x)          (set 'Lisp.Expression 'Trace (if x #t #f)))
;; (trace-add 'name ...) -- trace specific symbols (after (trace #t)).
;; Example: (trace #t) (trace-add 'member 'map)
(define (trace-add . x)    (map (lambda (a) (call (*traceHash*) 'Add a)) x))
;; (trace-all) -- trace every symbol (verbose; best combined with trace-clear first).
(define (trace-all)        (trace-add '_all_))
;; (trace-clear) -- remove all per-name trace entries.
(define (trace-clear)      (call (*traceHash*) 'Clear))
;; (trace-remove 'name ...) -- stop tracing specific symbols.
(define (trace-remove . x) (map (lambda (a) (call (*traceHash*) 'Remove a)) x))

;; (trace-indent #t/#f) -- indent trace output by call depth.
(define (trace-indent x)
  (set 'Lisp.Program 'TraceIndent (if x #t #f)))

;; (trace-code #t/#f) -- append the call-site expression to trace lines (when available).
(define (trace-code x)
  (set 'Lisp.Program 'TraceShowCode (if x #t #f)))

;; (trace-source #t/#f) -- append source location metadata to trace lines (when available).
(define (trace-source x)
  (set 'Lisp.Program 'TraceShowSource (if x #t #f)))

;; (trace-compact #t/#f) -- collapse repeated primitive return lines into compact summaries.
(define (trace-compact x)
  (set 'Lisp.Program 'TraceCompact (if x #t #f)))

;; (trace-compact-min [n]) -- get or set compact summary threshold (default 4, minimum 2).
(define (trace-compact-min . rest)
  (if (null? rest)
      (get 'Lisp.Program 'TraceCompactMinRun)
      (set 'Lisp.Program 'TraceCompactMinRun (car rest))))
                              
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

;; (stats x) -- enable (#t) or disable (#f) per-expression timing and iteration counters.
;; When enabled, each top-level eval prints elapsed time and iteration count.
;; Also resets accumulated totals when enabled.
(define (stats x)
  (set 'Lisp.Program 'Stats (if x #t #f))
  (if x (call-static 'Lisp.Program 'ResetTotals)))
;; (stats-reset) -- clear accumulated timing totals without toggling stats mode.
(define (stats-reset)   (call-static 'Lisp.Program 'ResetTotals))
;; (stats-total) -- print the accumulated stats summary to the console.
(define (stats-total)   (call-static 'Lisp.Program 'PrintTotals))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Profiling -- Lisp.Program.Profile
;; (profile #t) -- enable compact profiling summaries for each top-level eval
;; (profile #f) -- disable profiling output
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;; (profile x) -- enable (#t) or disable (#f) compact profiling output.
(define (profile x)
  (set 'Lisp.Program 'Profile (if x #t #f))
  (if x (call-static 'Lisp.Program 'ResetProfile)))
;; (profile-reset) -- clear accumulated profile totals without toggling profile mode.
(define (profile-reset) (call-static 'Lisp.Program 'ResetProfile))
;; (profile-total) -- print the accumulated profile summary to the console.
(define (profile-total) (call-static 'Lisp.Program 'PrintProfileTotals))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; show-lines -- echo each top-level form as it executes
;; (show-lines #t)  -- enable
;; (show-lines #f)  -- disable
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;; (show-input-lines x) / (showlines x) -- echo (#t) or suppress (#f) each top-level form
;; before it is evaluated.  Useful for tracking execution progress in script files.
(define (show-input-lines x)
  (set 'Lisp.Program 'ShowInputLines (if x #t #f)))

(define (showlines x)
  (show-input-lines x))

;; (help) -- print a summary of all interactive REPL debugging commands.
(define (help)
  (begin
    (consoleLine "Available commands:")
    (consoleLine "  (stats #t/#f)        Enable or disable per-expression timing and counters")
    (consoleLine "  (stats-reset)        Clear accumulated stats totals")
    (consoleLine "  (stats-total)        Show the accumulated stats summary")
    (consoleLine "  (profile #t/#f)      Enable or disable compact profiling summaries")
    (consoleLine "  (profile-reset)      Clear accumulated profile totals")
    (consoleLine "  (profile-total)      Show the accumulated profile summary")
    (consoleLine "  (debug #t/#f)        Enable or disable the runtime debugger hooks")
    (consoleLine "  (debug-step #t/#f)   Emit a debugger message before each evaluated form")
    (consoleLine "  (debug-break 'name)  Register a breakpoint label for the debugger")
    (consoleLine "  (trace #t/#f)        Enable or disable tracing output")
    (consoleLine "  (trace-add 'name)    Trace a specific symbol after trace is enabled")
    (consoleLine "  (trace-indent #t/#f) Indent trace lines by call depth")
    (consoleLine "  (trace-code #t/#f)   Include the call-site expression in trace lines")
    (consoleLine "  (trace-source #t/#f) Include source location in trace lines")
    (consoleLine "  (trace-compact #t/#f) Collapse noisy repeated primitive trace lines")
    (consoleLine "  (trace-compact-min [n]) Get/set compact summary threshold")
    (consoleLine "  (colors #t/#f)       Enable or disable console colors")
    (consoleLine "  (pretty-print #t/#f) Enable or disable pretty-printing of results")
    (consoleLine "  (pp x)               Pretty-print x once without toggling the global flag")
    (consoleLine "  (p-adic 7 [digits])  Display exact results in 7-adic form; optional digits set precision")
    (consoleLine "  (show-lines #t/#f)   Echo each top-level form before it runs")
    (consoleLine "  (env)                List global procedures and built-ins")
    (consoleLine "  (env 'name)          Show a specific global procedure or built-in")
    (consoleLine "  (env 'prefix*)       Show all procedures/built-ins matching a prefix wildcard")
    (consoleLine "  (env '*sub*)         Show all procedures/built-ins containing a substring")
    (consoleLine "  (env-all)            Display help, all global procedures, built-ins and macros")
    (consoleLine "  (doc 'name)          Show doc comment and signature for a procedure, macro, or built-in")
    (consoleLine "  (apropos \"str\")      List all procedures/macros/built-ins whose name contains str")
    (consoleLine "  (macros-env)         List all defined macros and their patterns")
    (consoleLine "  (disasm proc)        Disassemble a compiled procedure")
    (consoleLine "  (disasm-all)         Disassemble all compiled procedures")
    (consoleLine "  (disasm-verbose #t/#f) Show or hide trivial disassembly source labels")
    (consoleLine "  (exit)               Leave the REPL")
    '()))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Macros -- c# type Lisp.Macro
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;


;; --- Macro inspection ---

;; (macro? x) -- #t if x is the name of a currently-defined macro.
(define (macro? x)      (call (get 'Lisp.Macro 'CurrentDefinitions) 'ContainsKey x))
;; (macros->vector) / (macros->list) -- return all currently-defined macro names.
(define (macros->vector) 
  (new 'System.Collections.ArrayList (get (get 'Lisp.Macro 'CurrentDefinitions) 'Keys)))
(define (macros->list)  (vector->list (macros->vector)))
;; (macro-body x) -- return the pattern/template pairs of macro x.
(define (macro-body x)  (cdr (get (get 'Lisp.Macro 'CurrentDefinitions) 'Item x)))
;; (macro-const x) -- return the literal keywords list of macro x.
(define (macro-const x) (car (get (get 'Lisp.Macro 'CurrentDefinitions) 'Item x)))

(define *displayMacros* 
  (lambda ()
    (let ((x (macros->vector)))
      (do ((y (- (vector-length x) 1) (- y 1)))
          ((< y 0) "")
        (let* ((name  (vector-ref x y))
               (const (macro-const name))
               (body  (macro-body  name))
               (doc   (call-static 'Lisp.Macro 'GetDocComment name)))
          (when (not (equal? doc ""))
            (display "\n{0}" doc))
          (display "\n(macro {0} {1}" name (if (null? const) "()" const))
          (map (lambda (clause)
                 (if (null? clause)
                     (display "\n  ---")
                     (display "\n  ({0}\n    {1})" (car clause) (car (cdr clause)))))
               body)
          (display "\n)\n"))))))
;; (macro-env) / (macros-env) -- display all currently-defined macros and their pattern/template pairs.
(define (macro-env) (*displayMacros*))
(define (macros-env) (*displayMacros*))
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Procedures -- c# type Lisp.Closure
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;


;; --- Procedure / closure inspection ---

;; (procedure? x) / (closure? x) -- #t if x is a compiled closure.
(define (PROCEDURE? x)   (call (get 'Lisp.Program 'CurrentInitEnv) 'Apply x))
(define (procedure? x)
  (or (closure? x)
      (call (get-type "Lisp.Primitive") 'IsInstanceOfType x)))
(define (closure? x)     (call (get-type "Lisp.Closure") 'IsInstanceOfType x))
;; (closure-args f) -- return the formal parameter list of closure f.
(define (closure-args x) (get (PROCEDURE? x) 'ids))
;; (closure-body f) -- return the body expression list of closure f.
(define (closure-body x) (get (PROCEDURE? x) 'body))
;; (procedures->vector) / (procedures->list) -- all named closures in the environment.
(define (procedures->vector)
  (new 'System.Collections.ArrayList (get (get (get 'Lisp.Program 'CurrentInitEnv) 'table) 'Keys)))
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

;; (disasm-all) -- disassemble every compiled closure defined in the environment.
(define (disasm-all)
  (let ((x (procedures->vector)))
    (do ((y (- (vector-length x) 1) (- y 1)))
        ((< y 0) 'done)
      (let ((val (PROCEDURE? (vector-ref x y))))
        (when (closure? val)
          (disasm val))))))

;; display help, all global procedures, built-ins and macros.
(define (env-all) (list (help) (env) (macros-env)))