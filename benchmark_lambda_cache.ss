(stats #t)
(stats-reset)

(define (bench-inline-lambda n)
  (let loop ((i n) (sum 0))
    (if (= i 0)
        sum
        (loop (- i 1)
              (+ sum ((lambda (x) (+ x 1)) i))))))

(define (bench-returned-lambda n)
  (let loop ((i n) (sum 0))
    (if (= i 0)
        sum
        (loop (- i 1)
              (+ sum (((lambda (k) (lambda (x) (+ x k))) 1) i))))))

(define (bench-mixed n)
  (let loop ((i n) (sum 0))
    (if (= i 0)
        sum
        (loop (- i 1)
              (+ sum
                 ((lambda (f x) (f x))
                  (lambda (x) (+ x 2))
                  i))))))

(bench-inline-lambda 50000)
(bench-returned-lambda 25000)
(bench-mixed 25000)
(stats-total)