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
