(call-static 'System.Console 'Write ", fileSystem")

;; --- Filesystem operations (backed by System.IO) ---

;; (file-exists? path) -- #t if a file at path exists.
(define (file-exists?  path)       (call-static 'System.IO.File      'Exists path))
;; (delete-file path) -- delete the file at path.
(define (delete-file   path)       (call-static 'System.IO.File      'Delete path))
;; (rename-file old-path new-path) -- rename/move a file.
(define (rename-file   old-path new-path) (call-static 'System.IO.File 'Move old-path new-path))
;; (copy-file src dst) -- copy file from src to dst.
(define (copy-file     src dst)    (call-static 'System.IO.File      'Copy src dst))
;; (directory-exists? path) -- #t if a directory at path exists.
(define (directory-exists?  path)  (call-static 'System.IO.Directory 'Exists path))
;; (create-directory path) -- create a directory (including any missing parents).
(define (create-directory   path)  (call-static 'System.IO.Directory 'CreateDirectory path))
;; (current-directory) -- return the current working directory as a string.
(define (current-directory)        (call-static 'System.IO.Directory 'GetCurrentDirectory))
;; (set-current-directory! p) -- change the current working directory to p.
(define (set-current-directory! p) (call-static 'System.IO.Directory 'SetCurrentDirectory p))
;; (directory-list path) -- return a list of file paths in directory path.
;; Example: (directory-list ".") ==> (".\foo.ss" ".\bar.ss" ...)
(define (directory-list path)
  (vector->list (new 'System.Collections.ArrayList
                     (call-static 'System.IO.Directory 'GetFiles path))))
;; (directory-list-subdirs path) -- return a list of subdirectory paths in path.
(define (directory-list-subdirs path)
  (vector->list (new 'System.Collections.ArrayList
                     (call-static 'System.IO.Directory 'GetDirectories path))))
;; (file-size path) -- return the size of the file in bytes.
;; Example: (file-size "init.ss")
(define (file-size path)
  (get (new 'System.IO.FileInfo path) 'Length))

; (file-exists?  "init.ss")          ==> #t
; (directory-exists? "bin")           ==> #t
; (current-directory)                 ==> current path string
