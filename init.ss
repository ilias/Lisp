;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; init.ss -- Scheme standard library
;; Sections live in lib/<name>.ss
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(call-static 'System.Console 'Write "start [")

(load "lib/macros.ss")
(load "lib/utils.ss")
(load "lib/debug.ss")
(load "lib/pairs.ss")
(load "lib/chars.ss")
(load "lib/strings.ss")
(load "lib/types.ss")
(load "lib/numbers.ss")
(load "lib/vectors.ss")
(load "lib/ports.ss")
(load "lib/errors.ss")
(load "lib/records.ss")
(load "lib/values.ss")
(load "lib/continuations.ss")
(load "lib/algorithms.ss")
(load "lib/hashtables.ss")
(load "lib/filesystem.ss")
(load "lib/parameters.ss")
(load "lib/random.ss")
(load "lib/extras.ss")

(call-static 'System.Console 'WriteLine " ] done.\n")