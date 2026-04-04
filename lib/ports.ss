(call-static 'System.Console 'Write ", input")

;; --- Input ports ---

;; (input-port? x) -- #t if x is a StreamReader or StringReader.
(define (input-port? x)
  (or (= (call x 'GetType) (get-type "System.IO.StreamReader"))
      (= (call x 'GetType) (get-type "System.IO.StringReader"))))
(define *INPUT*                           '())
(define *INPUT-BUFFER*                    "")
;; (current-input-port) -- return the active input port (default: System.Console.In).
(define (current-input-port)              (if (null? *INPUT*) (get 'System.Console 'In) *INPUT*))
;; (open-input-file fname) -- open file fname for reading; sets it as the current input port.
(define (open-input-file fname)
  (let ((sr (new 'System.IO.StreamReader fname)))
    (set! *INPUT* sr)
    (set! *INPUT-BUFFER* (call sr 'ReadToEnd))
    sr))
;; (close-input-port iport) -- close an open input port.
(define (close-input-port iport)          (call iport 'Close))
;; (call-with-input-file fname proc) -- open fname, call (proc port), then close.
;; Restores the previous input port and buffer when proc returns.
;; Example: (call-with-input-file "data.txt" (lambda (p) *INPUT-BUFFER*))
(define (call-with-input-file fname proc)
    (let ((old-input *INPUT*) (old-buf *INPUT-BUFFER*))
      (let ((p (open-input-file fname)))
        (let ((v (proc p)))
          (close-input-port p)
          (set! *INPUT* old-input)
          (set! *INPUT-BUFFER* old-buf)
          v))))
(define (INPUT-PORT x)                    (if (null? x) (current-input-port) (car x)))
;; (read [port]) -- parse and return one Lisp datum from the input buffer.
;; Example: (with-input-from-file "data.ss" read) ==> first form in file
(define (read . iport)
  (let ((result (call-static 'Lisp.Util 'ParseOne *INPUT-BUFFER*)))
    (set! *INPUT-BUFFER* (get 'Lisp.Util 'ParseRemainder))
    result))
;; (read-line [port]) -- read and return the next line as a string (without newline).
(define (read-line . iport)               (call (INPUT-PORT iport) 'ReadLine))
;; (read-toend [port]) -- read and return the entire remaining content as a string.
(define (read-toend . iport)              (call (INPUT-PORT iport) 'ReadToEnd))
;; (read-char [port]) -- read the next character from the input.
;; Returns (eof-object) when the stream is exhausted.
(define (read-char . iport)
  (if (> (string-length *INPUT-BUFFER*) 0)
      (let ((c (string-ref *INPUT-BUFFER* 0)))
        (set! *INPUT-BUFFER* (substring *INPUT-BUFFER* 1 (string-length *INPUT-BUFFER*)))
        c)
      (let ((n (call (INPUT-PORT iport) 'Read)))
        (integer->char (if (< n 0) 65535 n)))))
;; (peek-char [port]) -- return the next character without consuming it.
;; Returns (eof-object) at end-of-stream.
(define (peek-char . iport)
  (if (> (string-length *INPUT-BUFFER*) 0)
      (string-ref *INPUT-BUFFER* 0)
      (let ((n (call (INPUT-PORT iport) 'Peek)))
        (integer->char (if (< n 0) 65535 n)))))
;; (eof-object? x) -- #t if x is the end-of-file character (code point 65535).
(define (eof-object? x)                   (and (char? x) (= (char->integer x) 65535)))
;; (eof-object) -- return the canonical end-of-file sentinel value.
(define (eof-object)                      (integer->char 65535))
;; (char-ready? [port]) -- always returns #t (no async I/O support).
(define (char-ready? . rest)              #t)  ; streams are always ready (no async I/O)
;; (read-string k [port]) -- read up to k characters; return a string or eof-object.
(define (read-string k . rest)
  ;; Read up to k characters from port (default: current-input-port).
  (let loop ((i 0) (acc '()))
    (if (= i k)
        (list->string (reverse acc))
        (let ((c (apply read-char rest)))
          (if (eof-object? c)
              (if (null? acc) c (list->string (reverse acc)))
              (loop (+ i 1) (cons c acc)))))))
;; (open-input-string s) -- create an input port that reads from string s.
;; Example: (read (open-input-string "(+ 1 2)")) ==> (+ 1 2)
(define (open-input-string s)     (new 'System.IO.StringReader s))
;; (string-port? x) -- #t if x is a StringReader or StringWriter.
(define (string-port? x)
  (or (= (call x 'GetType) (get-type "System.IO.StringReader"))
      (= (call x 'GetType) (get-type "System.IO.StringWriter"))))
;; (with-input-from-file fname thunk) -- evaluate thunk with fname as the current input.
;; Restores the previous input port when the thunk returns.
(define (with-input-from-file fname thunk)
  (let ((old *INPUT*) (old-buf *INPUT-BUFFER*))
    (open-input-file fname)
    (let ((result (thunk)))
      (close-input-port *INPUT*)
      (set! *INPUT* old)
      (set! *INPUT-BUFFER* old-buf)
      result)))

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Output Ports -- c# type == System.IO.StreamWriter
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write ", output")

;; (output-port? x) -- #t if x is any TextWriter (StreamWriter, StringWriter, Console.Out, etc.).
(define (output-port? obj)
  (let ((tw (get-type "System.IO.TextWriter")))
    (call (call obj 'GetType) 'IsAssignableTo tw)))
(define *OUTPUT*                           '())
;; (current-output-port) -- return the active output port (default: System.Console.Out).
(define (current-output-port)              (if (null? *OUTPUT*) (get 'System.Console 'Out) *OUTPUT*))
;; (current-error-port) -- return the standard error port (System.Console.Error).
(define (current-error-port)               (get 'System.Console 'Error))
;; (port? x) -- #t if x is either an input or output port.
(define (port? x)                          (or (input-port? x) (output-port? x)))
;; (open-output-file fname) -- open/create fname for writing; sets it as the current output port.
(define (open-output-file fname)
  (let ((sw (new 'System.IO.StreamWriter fname)))
    (set! *OUTPUT* sw)
    sw))
;; (close-output-port oport) -- flush and close the output port.
(define (close-output-port oport)          (call oport 'Close))
;; (call-with-output-file fname proc) -- open fname, call (proc port), then close.
;; Restores the previous output port when proc returns.
(define (call-with-output-file fname proc)
    (let ((old *OUTPUT*))
      (let ((p (open-output-file fname)))
        (let ((v (proc p)))
          (close-output-port p)
          (set! *OUTPUT* old)
          v))))
;; (console x) -- write x to System.Console (bypasses current-output-port).
(define (console . x)                      (call-static 'System.Console 'Write ,@x))
;; (consoleLine x) -- write x followed by a newline to System.Console.
(define (consoleLine . x)                  (call-static 'System.Console 'WriteLine ,@x))
;; (display obj [port]) -- write obj in human-readable form (strings/chars without quotes).
;; Writes to the current output port if no port is given.
;; Example: (display "hello") -- prints hello (no quotes)
(define (display obj . rest)
  (let ((s (if (or (string? obj) (char? obj)) obj (call-static 'Lisp.Util 'Dump obj))))
    (cond
      ((null? rest)
       (call (current-output-port) 'Write s))
      ((output-port? (car rest))
       (call (car rest) 'Write s))
      (else
       (call-static 'System.Console 'Write obj ,@rest)))))
;; (write obj [port]) -- write obj in machine-readable form (strings quoted, chars as #\x).
;; Example: (write "hi") -- prints "hi" (with quotes)
(define (write obj . rest)
  (let ((port (if (null? rest) (current-output-port) (car rest))))
    (call port 'Write (call-static 'Lisp.Util 'Dump obj))))
;; (write-char char [port]) -- write a single character to the port.
(define (write-char char . rest)
  (let ((port (if (null? rest) (current-output-port) (car rest))))
    (call port 'Write char)))
;; (writeline x) / (newline [port]) -- write a newline to the current output port.
(define (writeline . x)                    (call (current-output-port) 'WriteLine ,@x))
(define (OUTPUT-PORT x)                    (if (null? x) (current-output-port) (car x)))
(define (newline . rest)
  (let ((port (if (null? rest) (current-output-port) (car rest))))
    (call port 'WriteLine "")))
;; (write-string s [port]) -- write string s to the port without any quoting.
(define (write-string s . rest)
  (let ((port (if (null? rest) (current-output-port) (car rest))))
    (call port 'Write s)))
;; (flush-output-port [port]) -- flush any buffered output to the underlying stream.
(define (flush-output-port . rest)
  (let ((port (if (null? rest) (current-output-port) (car rest))))
    (call port 'Flush)))
;; (open-output-string) -- create an output port that accumulates characters in memory.
;; Use (get-output-string port) to retrieve the accumulated string.
;; Example: (let ((p (open-output-string))) (display "hi" p) (get-output-string p)) ==> "hi"
(define (open-output-string)      (new 'System.IO.StringWriter))
;; (get-output-string port) -- return the string accumulated in an output-string port.
(define (get-output-string port)  (call port 'ToString))
;; (with-output-to-file fname thunk) -- evaluate thunk with fname as the current output.
;; Restores the previous output port when thunk returns.
(define (with-output-to-file fname thunk)
  (let ((old *OUTPUT*))
    (open-output-file fname)
    (let ((result (thunk)))
      (close-output-port *OUTPUT*)
      (set! *OUTPUT* old)
      result)))

