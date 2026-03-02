;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; examples
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(define (get-page page) 
  (call (new 'System.IO.StreamReader (call (call (call-static 'System.Net.WebRequest 'Create page) 'GetResponse) 'GetResponseStream)) 'ReadToEnd))
(define (find srch text) 
  (if (= (call text 'IndexOf srch) -1) 'NoResults (call text 'Substring (call text 'IndexOf srch) 256)  ) ) 

(define make-counter
  (lambda ()
    (let ((next 0))
      (lambda ()
        (let ((v next))
          (set! next (+ next 1))
          v))))) 

(define lazy
  (lambda (t)
    (let ((val #f) (flag #f))
      (lambda ()
        (if (not flag)
            (begin (set! val (t))
                   (set! flag #t)))
        val)))) 
        
(define *displayProcedures* 
  (lambda ()
    (let ((x (procedures->vector)))
         (do ((y (- (vector-length x) 1) (- y 1)))
             ((< y 0) "Done")
             (display "\nCLOSURE {0} " (vector-ref x y))
             (letrec ((args (closure-args (vector-ref x y)))
                      (body (closure-body (vector-ref x y))))
                     (display (if (null? args) "()" args))
                     (if (null? body)
                         (display " ()\n")
                         (display "\n{0}\n" body)))))))
             
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
                                      (display "\n==>   {0}\n      {1}" (car x1) (car (cdr x1)))))
                          body ))))))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;:quine: /kwi:n/ /n./ [from the name of the logician Willard van Orman Quine, via Douglas Hofstadter] A program that generates a copy of its own source text as its complete output. Devising the shortest possible quine in some given programming language is a common hackish amusement. Here is one classic quine: 

     ((lambda (x)
       (list x (list (quote quote) x)))
      (quote
         (lambda (x)
           (list x (list (quote quote) x)))))
;This one works in LISP or Scheme. It's relatively easy to write quines in other languages such as Postscript which readily handle programs as data; much harder (and thus more challenging!) in languages like C which do not. Here is a classic C quine for ASCII machines: 
;     char*f="char*f=%c%s%c;main()
;     {printf(f,34,f,34,10);}%c";
;     main(){printf(f,34,f,34,10);}
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;quine:
(define (line-write x) (write x) (newline))
(define (d l) (map line-write l))
(define (mid) (display "(do '(") (newline))
(define (end) (display "))") (newline))
(define (do l) (d l) (mid) (d l) (end))
(do '(
(define (line-write x) (write x) (newline))
(define (d l) (map line-write l))
(define (mid) (display "(do '(") (newline))
(define (end) (display "))") (newline))
(define (do l) (d l) (mid) (d l) (end))
))
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
