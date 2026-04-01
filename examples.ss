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

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;:quine: /kwi:n/ /n./ [from the name of the logician Willard van Orman Quine, via Douglas Hofstadter] A program that generates a copy of its own source text as its complete output. Devising the shortest possible quine in some given programming language is a common hackish amusement. Here is one classic quine: 

((LAMBDA (x) (list x (list 'quote x))) '(LAMBDA (x) (list x (list 'quote x))))


;This one works in LISP or Scheme. It's relatively easy to write quines in other languages such as Postscript which readily handle programs as data; much harder (and thus more challenging!) in languages like C which do not. Here is a classic C quine for ASCII machines: 
;     char*f="char*f=%c%s%c;main()
;     {printf(f,34,f,34,10);}%c";
;     main(){printf(f,34,f,34,10);}
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;quine:
(DEFINE line-write (LAMBDA (x) (write x) (newline)))
(DEFINE d (LAMBDA (l) (map line-write l)))
(DEFINE mid (LAMBDA () (display "(do '(") (newline)))
(DEFINE end (LAMBDA () (display "))") (newline)))
(DEFINE do (LAMBDA (l) (d l) (mid) (d l) (end)))
(do '(
(DEFINE line-write (LAMBDA (x) (write x) (newline)))
(DEFINE d (LAMBDA (l) (map line-write l)))
(DEFINE mid (LAMBDA () (display "(do '(") (newline)))
(DEFINE end (LAMBDA () (display "))") (newline)))
(DEFINE do (LAMBDA (l) (d l) (mid) (d l) (end)))
))


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
