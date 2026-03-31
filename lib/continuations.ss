(call-static 'System.Console 'Write ", delayEvaluation")

;; (delay exp) -- create a promise that evaluates exp lazily.
;; The expression is not evaluated until (force promise) is called.
;; The result is cached: subsequent (force) calls return the same value.
;; Example:
;;   (define p (delay (begin (display "eval!") 42)))
;;   (force p)  ==> prints "eval!" and returns 42
;;   (force p)  ==> returns 42 (no display; cached)
(macro delay ()
  ((_ exp)    (make-promise (lambda () exp)))) 
  
;; make-promise: wraps a thunk in a promise object.
;; The returned lambda memoises the result on first call.
(define make-promise
   (lambda (thunk)
      (let ((value #f) (set? #f))
         (lambda ()
            (unless set?
               (let ((v (thunk)))
                  (unless set?
                     (set! value v)
                     (set! set? #t))))
            value))))

;; (force promise) -- evaluate and return the value of a promise.
;; If the promise has already been forced, return the cached value.
(define force (lambda (promise) (promise)))

; (define (stream-car s) (car (force s)))
; (define (stream-cdr s) (cdr (force s)))
; (define counters
;   (let next ((n 1))
;     (delay (cons n (next (+ n 1)))))) 
; (stream-car counters)              ==> 1
; (stream-car (stream-cdr counters)) ==> 2

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Continuations
;;
;; Two flavours are provided:
;;
;; 1. call/cc (escape-only, the original)
;;    Works via ContinuationException.  Fast; supports nested call/cc correctly.
;;    Limitation: k cannot be called after the body returns (escape only).
;;
;; 2. call/cc-full (reentrant / coroutine-style)
;;    Backed by a real OS thread per continuation.  The body and the caller
;;    alternate execution: calling k suspends the body and yields a value to
;;    the caller; the caller can then resume the body by calling k again.
;;    This supports generators, coroutines, and cooperative multitasking.
;;
;;    Note: full "upward" continuations (calling k after the body has returned)
;;    are not supported because the interpreter is a tree-walker, not CPS.
;;    For that, a full CPS transform of the evaluator would be required.
;;
;; Tagged call/cc: each invocation gets a unique tag
;; so nested continuations don't interfere with each other.
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", continuation")

;; Each call/cc invocation allocates a fresh tag (a unique cons cell)
;; so that nested call/cc continuations are completely independent.
;; The continuation procedure k, when called, throws a tagged
;; ContinuationException that is only caught by THIS call/cc's TRY-CONT.
(macro call/cc ()
  ((_ exp)  (let ((?tag  (cons #f #f))
                  (?value '()))
               (TRY-CONT ?tag
                 (exp (lambda (?x)
                        (set! ?value ?x)
                        (escape-continuation/tag ?x ?tag)))
                 ?value))))

;; (let/cc var body...) -- bind the current continuation to var and evaluate body.
;; Shorthand for (call/cc (lambda (var) body...)).
;; Example: (+ 1 (let/cc k (* 5 (k 3)))) ==> 4   ; k escapes with 3
(macro let/cc ()
  ((_ var exp...) (call/cc (lambda (var) exp...))))

;; call-with-current-continuation -- standard R7RS alias for call/cc.
(macro call-with-current-continuation ()
  ((_ exp) (call/cc exp)))

;; call/cc-full: reentrant continuation (coroutine/generator style).
;; Uses a real OS thread per continuation.  The body and the caller
;; alternate: calling k suspends the body and yields to the caller;
;; calling the returned k-handle resumes the body.
;;
;; Example — generator:
;;   (define gen
;;     (let ((k #f))
;;       (call/cc-full (lambda (yield)
;;         (set! k yield)
;;         (yield 1) (yield 2) (yield 3) 'done))))
;;   gen       ; => 1  (first yield)
;;   (k #f)    ; => 2
;;   (k #f)    ; => 3
;;   (k #f)    ; => done  (body returned)
;;
;; (call/cc-full f) is a direct call to the C# primitive.
;; It is intentionally not aliased to call/cc so existing code is unaffected.

;; make-generator: wraps call/cc-full into a simple generator interface.
;; Returns a zero-argument thunk; each call advances to the next yield.
;; The body calls (yield v) to produce values; when the body returns, the
;; returned value becomes the last value from the generator.
(define (make-generator proc)
  ;; Returns a thunk; each call advances the generator to the next yield.
  ;; First call starts the body; subsequent calls resume it via the continuation.
  (let ((k #f) (started #f))
    (lambda ()
      (if (not started)
          (begin
            (set! started #t)
            (call/cc-full
              (lambda (cont)
                (set! k cont)
                (proc cont))))
          (k #f)))))

;; dynamic-wind: before, thunk, after — after ALWAYS runs (escape or error).
;; Uses dynamic-wind-body C# primitive which catches any exception, runs after,
;; then re-throws, giving correct behaviour with escape continuations and errors.
(define (dynamic-wind before thunk after)
  (before)
  (dynamic-wind-body thunk after))

; (call/cc (lambda (k) (* 5 4)))
; (call/cc (lambda (k) (* 5 (k 4))))
; (* 2 (call/cc (lambda (k) (* 5 (k 4)))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; A Unification Algorithm
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;    A unification algorithm attempts to make two symbolic expressions equal by computing
;; a unifying substitution for the expressions. A substitution is a function that replaces
;; variables with other expressions. A substitution must treat all occurrences of a variable
;; the same way, e.g., if it replaces one occurrence of the variable x by a, it must replace
;; all occurrences of x by a. A unifying substitution, or unifier, for two expressions e1 
;; and e2 is a substitution, , such that . 
;;    For example, the two expressions f(x) and f(y) can be unified by substituting x for
;; y (or y for x). In this case, the unifier  could be described as the function that replaces
;; y with x and leaves other variables unchanged. On the other hand, the two expressions
;; x + 1 and y + 2 cannot be unified. It might appear that substituting 3 for x and 2 for
;; y would make both expressions equal to 4 and hence equal to each other. The symbolic 
;; expressions, 3 + 1 and 2 + 2, however, still differ. 
;;    Two expressions may have more than one unifier. For example, the expressions f(x,y)
;; and f(1,y) can be unified to f(1,y) with the substitution of 1 for x. They may also be
;; unified to f(1,5) with the substitution of 1 for x and 5 for y. The first substitution
;; is preferable, since it does not commit to the unnecessary replacement of y. Unification
;; algorithms typically produce the most general unifier, or mgu, for two expressions. The
;; mgu for two expressions makes no unnecessary substitutions; all other unifiers for the
;; expressions are special cases of the mgu. In the example above, the first substitution
;; is the mgu and the second is a special case. 
;;    For the purposes of this program, a symbolic expression can be a variable, a constant,
;; or a function application. Variables are represented by Scheme symbols, e.g., x; a function
;; application is represented by a list with the function name in the first position and its
;; arguments in the remaining positions, e.g., (f x); and constants are represented by
;; zero-argument functions, e.g., (a). 
;;    The algorithm presented here finds the mgu for two terms, if it exists, using a
;; continuation passing style, or CPS (see Section 3.4), approach to recursion on subterms.
;; The procedure unify takes two terms and passes them to a help procedure, uni, along with
;; an initial (identity) substitution, a success continuation, and a failure continuation.
;; The success continuation returns the result of applying its argument, a substitution,
;; to one of the terms, i.e., the unified result. The failure continuation simply returns
;; its argument, a message. Because control passes by explicit continuation within unify
;; (always with tail calls), a return from the success or failure continuation is a return
;; from unify itself. 
;;    Substitutions are procedures. Whenever a variable is to be replaced by another term,
;; a new substitution is formed from the variable, the term, and the existing substitution.
;; Given a term as an argument, the new substitution replaces occurrences of its saved
;; variable with its saved term in the result of invoking the saved substitution on the
;; argument expression. Intuitively, a substitution is a chain of procedures, one for each
;; variable in the substitution. The chain is terminated by the initial, identity
;; substitution. 

