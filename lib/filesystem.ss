(call-static 'System.Console 'Write ", fileSystem")

(define (file-exists?  path)       (call-static 'System.IO.File      'Exists path))
(define (delete-file   path)       (call-static 'System.IO.File      'Delete path))
(define (rename-file   old-path new-path) (call-static 'System.IO.File 'Move old-path new-path))
(define (copy-file     src dst)    (call-static 'System.IO.File      'Copy src dst))
(define (directory-exists?  path)  (call-static 'System.IO.Directory 'Exists path))
(define (create-directory   path)  (call-static 'System.IO.Directory 'CreateDirectory path))
(define (current-directory)        (call-static 'System.IO.Directory 'GetCurrentDirectory))
(define (set-current-directory! p) (call-static 'System.IO.Directory 'SetCurrentDirectory p))
(define (directory-list path)
  (vector->list (new 'System.Collections.ArrayList
                     (call-static 'System.IO.Directory 'GetFiles path))))
(define (directory-list-subdirs path)
  (vector->list (new 'System.Collections.ArrayList
                     (call-static 'System.IO.Directory 'GetDirectories path))))
(define (file-size path)
  (get (new 'System.IO.FileInfo path) 'Length))

; (file-exists?  "init.ss")          ==> #t
; (directory-exists? "bin")           ==> #t
; (current-directory)                 ==> current path string

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Parameter objects (SRFI-39)
;; A parameter is a closure encapsulating a dynamic cell.
;; (make-parameter val [converter])   -- creates a parameter
;; (param)                            -- reads the current value
;; (param new-val)                    -- sets the value (mutation)
;; (parameterize ((p v) ...) body...) -- temporarily rebinds
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

