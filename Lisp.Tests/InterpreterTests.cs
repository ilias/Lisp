using Lisp;
using Xunit;

namespace Lisp.Tests;

public sealed class InterpreterTests
{
    private static InterpreterHost CreateHost()
    {
        var host = new InterpreterHost(primitiveProfile: "full");
        host.LoadInitFromBaseDirectory();
        return host;
    }

    [Fact]
    public void EvaluatesArithmeticAndTailRecursiveLoop()
    {
        var host = CreateHost();

        var result = host.Eval("(let loop ((i 0) (acc 0)) (if (= i 10000) acc (loop (+ i 1) (+ acc i))))");

        Assert.Equal(49_995_000, result);
    }

    [Fact]
    public void PreservesStateAcrossHostEvaluations()
    {
        var host = CreateHost();

        host.Eval("(define embedded-value 41)");

        Assert.Equal(42, host.Eval("(+ embedded-value 1)"));
    }

    [Fact]
    public void KeepsSeparateHostsIndependent()
    {
        var first = CreateHost();
        var second = CreateHost();

        first.Eval("(define embedded-value 41)");

        Assert.Equal(42, first.Eval("(+ embedded-value 1)"));
        var exception = Assert.Throws<LispException>(() => second.Eval("embedded-value"));
        Assert.Contains("embedded-value", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EvaluatesFilesWithTheirSourcePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lisp-host-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "embedded.ss");

        try
        {
            File.WriteAllText(filePath, "(DEFINE)");
            var host = CreateHost();

            var exception = Assert.Throws<LispException>(() => host.EvalFile(filePath));

            Assert.Equal(filePath, exception.SchemeSource?.SourceName);
            Assert.Contains("define", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ResolvesRelativeLoadsFromTheEvaluatedFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lisp-host-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var childPath = Path.Combine(directory, "child.ss");
        var parentPath = Path.Combine(directory, "parent.ss");

        try
        {
            File.WriteAllText(childPath, "(define loaded-from-child 41)");
            File.WriteAllText(parentPath, "(load \"child.ss\") (+ loaded-from-child 1)");
            var host = CreateHost();

            Assert.Equal(42, host.EvalFile(parentPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CancelsLongRunningEvaluationAndAllowsHostReuse()
    {
        var host = CreateHost();
        using var cancellation = new CancellationTokenSource();
        var evaluation = Task.Run(() => host.Eval("(let loop () (loop))", cancellation.Token));

        cancellation.Cancel();

        await Assert.ThrowsAsync<UserInterruptException>(() => evaluation);
        Assert.Equal(42, host.Eval("(+ 40 2)"));
    }

    [Fact]
    public void KeepsInterpreterStateIsolated()
    {
        Assert.True(RuntimeIsolationChecks.RuntimeStateIsIsolated());
        Assert.True(RuntimeIsolationChecks.MacroTablesAreIsolated());
        Assert.True(RuntimeIsolationChecks.ModuleTablesAreIsolated());
        Assert.True(RuntimeIsolationChecks.MacroDocCommentsAreIsolated());
    }

    [Fact]
    public void ReportsMalformedFormsAsSchemeErrors()
    {
        Assert.True(RuntimeIsolationChecks.MalformedSpecialFormsReportSchemeErrors());
        Assert.True(RuntimeIsolationChecks.InvalidIfReportsSourceLocation());
        Assert.True(RuntimeIsolationChecks.InvalidDefineReportsSourceLocation());
    }

    [Fact]
    public void KeepsOrdinaryListsDistinctFromMultipleValues()
    {
        var host = CreateHost();

        var result = host.Eval("(call-with-values (lambda () '(1 2)) (lambda (value) (list 'one value)))");

        Assert.Equal("(one (1 2))", Util.Dump(result));
    }

    [Fact]
    public void PreservesZeroAndMultipleValues()
    {
        var host = CreateHost();

        Assert.Equal("()", Util.Dump(host.Eval("(call-with-values (lambda () (values)) list)")));
        Assert.Equal("(1 2 3)", Util.Dump(host.Eval("(call-with-values (lambda () (values 1 2 3)) list)")));
    }

    [Fact]
    public void DoesNotTreatLegacySentinelShapedListsAsMultipleValues()
    {
        var host = CreateHost();

        var result = host.Eval("(call-with-values (lambda () (cons '*multiple-values* '(1 2))) (lambda (value) value))");

        Assert.Equal("(*multiple-values* 1 2)", Util.Dump(result));
    }

    [Fact]
    public void RejectsInvalidCallWithValuesArity()
    {
        var host = CreateHost();

        var exception = Assert.Throws<LispException>(() => host.Eval("(call-with-values (lambda () 1))"));

        Assert.Contains("call-with-values: expected 2 arguments", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RaiseContinuableResumesAtTheCallSiteWithTheHandlersResult()
    {
        var host = CreateHost();

        var result = host.Eval(
            "(with-exception-handler (lambda (e) 99) (lambda () (+ 1 (+ 10 (raise-continuable 'x)))))");

        Assert.Equal(110, result);
    }

    [Fact]
    public void RaiseContinuableEscapesToTheNextOuterHandlerWhenAHandlerRaisesAgain()
    {
        var host = CreateHost();

        var result = host.Eval(
            "(let ((log '())) " +
            "  (with-exception-handler " +
            "    (lambda (e) (set! log (append log (list 'outer))) 'outer-handled) " +
            "    (lambda () " +
            "      (with-exception-handler " +
            "        (lambda (e) (set! log (append log (list 'inner))) (raise-continuable 'again)) " +
            "        (lambda () (raise-continuable 'first))))) " +
            "  log)");

        Assert.Equal("(inner outer)", Util.Dump(result));
    }

    [Fact]
    public void RaiseContinuableWithNoHandlerIsCatchableByTry()
    {
        var host = CreateHost();

        var result = host.Eval("(try (raise-continuable 'oops) 'caught)");

        Assert.Equal("caught", Util.Dump(result));
    }

    [Fact]
    public void KeepsExceptionHandlerStackIsolated()
    {
        Assert.True(RuntimeIsolationChecks.ExceptionHandlerStackIsIsolated());
    }

    [Theory]
    [InlineData("(+ 1 2)", 3)]
    [InlineData("(car (let ((xs '(2 3 4))) `(1 ,@xs 5)))", 1)]
    [InlineData("(let-syntax ((twice (syntax-rules () ((_ x) (+ x x))))) (twice 21))", 42)]
    [InlineData("(let ((x 20)) (eval '(+ x 22)))", 42)]
    public void RepresentativeWorkloadsStayOnTheVm(string expression, int expected)
    {
        var host = CreateHost();
        host.Eval("(call-static 'Lisp.Program 'ResetTotals)");

        var result = host.Eval(expression);
        var emits = Convert.ToInt64(host.Eval("(call-static 'Lisp.Program 'GetTotalInterpEmits)"));
        var executions = Convert.ToInt64(host.Eval("(call-static 'Lisp.Program 'GetTotalInterpExecs)"));
        var treeWalkCalls = Convert.ToInt64(host.Eval("(call-static 'Lisp.Program 'GetTotalTreeWalkCalls)"));

        Assert.Equal(expected, result);
        Assert.Equal(0, emits);
        Assert.Equal(0, executions);
        Assert.Equal(0, treeWalkCalls);
    }
}
