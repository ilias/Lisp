namespace Lisp;

public static class RuntimeIsolationChecks
{
    private static T WithProgram<T>(Program program, Func<T> action)
    {
        var previous = InterpreterContext.Current;
        try
        {
            InterpreterContext.Current = program.Context;
            return action();
        }
        finally
        {
            InterpreterContext.Current = previous;
        }
    }

    public static bool MacroTablesAreIsolated()
    {
        var outerContext = InterpreterContext.Current;
        try
        {
            var first = new Program();
            var second = new Program();

            WithProgram(first, () => first.Eval("(macro isolated-macro () ((_ ) 11))", "<isolation-1>"));

            bool firstHas = WithProgram(first, () => Macro.CurrentDefinitions.ContainsKey(Symbol.Create("isolated-macro")));
            bool secondHas = WithProgram(second, () => Macro.CurrentDefinitions.ContainsKey(Symbol.Create("isolated-macro")));

            WithProgram(second, () => second.Eval("(macro isolated-macro () ((_ ) 22))", "<isolation-2>"));

            object firstResult = WithProgram(first, () => first.Eval("(isolated-macro)", "<isolation-run-1>"));
            object secondResult = WithProgram(second, () => second.Eval("(isolated-macro)", "<isolation-run-2>"));

            return firstHas && !secondHas && Equals(firstResult, 11) && Equals(secondResult, 22);
        }
        finally
        {
            InterpreterContext.Current = outerContext;
        }
    }

    public static bool RuntimeStateIsIsolated()
    {
        var outerContext = InterpreterContext.Current;
        try
        {
            var first = new Program();
            var second = new Program();

            WithProgram(first, () =>
            {
                var context = InterpreterContext.RequireCurrent();
                context.Stats = true;
                context.ShowInputLines = true;
                context.LastValue = false;
                context.Iterations = 123;
                return 0;
            });

            bool secondDefaults = WithProgram(second, () =>
            {
                var context = InterpreterContext.RequireCurrent();
                return !context.Stats && !context.ShowInputLines && context.LastValue && context.Iterations == 0;
            });

            bool firstRetained = WithProgram(first, () =>
            {
                var context = InterpreterContext.RequireCurrent();
                return context.Stats && context.ShowInputLines && !context.LastValue && context.Iterations == 123;
            });

            return secondDefaults && firstRetained;
        }
        finally
        {
            InterpreterContext.Current = outerContext;
        }
    }

    public static bool MacroDocCommentsAreIsolated()
    {
        var outerContext = InterpreterContext.Current;
        try
        {
            var first = new Program();
            var second = new Program();

            const string firstSyntax = ";;; first macro doc\n(define-syntax isolated-doc\n  (syntax-rules ()\n    ((_ ) 'first)))";
            const string secondSyntax = ";;; second macro doc\n(define-syntax isolated-doc\n  (syntax-rules ()\n    ((_ ) 'second)))";

            WithProgram(first, () => first.Eval(firstSyntax, "<doc-1>"));

            bool secondMissing = WithProgram(second, () => string.IsNullOrEmpty(Macro.GetDocComment(Symbol.Create("isolated-doc"))));

            WithProgram(second, () => second.Eval(secondSyntax, "<doc-2>"));

            string firstDoc = WithProgram(first, () => Macro.GetDocComment(Symbol.Create("isolated-doc")));
            string secondDoc = WithProgram(second, () => Macro.GetDocComment(Symbol.Create("isolated-doc")));

            return secondMissing
                && firstDoc.Contains("first macro doc", StringComparison.Ordinal)
                && secondDoc.Contains("second macro doc", StringComparison.Ordinal)
                && !string.Equals(firstDoc, secondDoc, StringComparison.Ordinal);
        }
        finally
        {
            InterpreterContext.Current = outerContext;
        }
    }

    private static bool EvalFailsWith(string source, string expectedMessageFragment)
    {
        var outerContext = InterpreterContext.Current;
        try
        {
            var program = new Program();
            return WithProgram(program, () =>
            {
                try
                {
                    program.Eval(source, "<malformed-special-form>");
                    return false;
                }
                catch (LispException ex)
                {
                    return ex.Message.Contains(expectedMessageFragment, StringComparison.Ordinal);
                }
            });
        }
        finally
        {
            InterpreterContext.Current = outerContext;
        }
    }

    private static bool EvalFailsWithSource(
        string source,
        string sourceName,
        string expectedMessageFragment,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn)
    {
        var outerContext = InterpreterContext.Current;
        try
        {
            var program = new Program();
            return WithProgram(program, () =>
            {
                try
                {
                    program.Eval(source, sourceName);
                    return false;
                }
                catch (LispException ex)
                {
                    var sourceSpan = ex.SchemeSource;
                    return ex.Message.Contains(expectedMessageFragment, StringComparison.Ordinal)
                        && sourceSpan is { }
                        && string.Equals(sourceSpan.Value.SourceName, sourceName, StringComparison.Ordinal)
                        && sourceSpan.Value.StartLine == startLine
                        && sourceSpan.Value.StartColumn == startColumn
                        && sourceSpan.Value.EndLine == endLine
                        && sourceSpan.Value.EndColumn == endColumn;
                }
            });
        }
        finally
        {
            InterpreterContext.Current = outerContext;
        }
    }

    public static bool InvalidIfReportsSchemeError() =>
        EvalFailsWith("(IF #t)", "if: expected 2 or 3 arguments, got 1");

    public static bool InvalidQuoteReportsSchemeError() =>
        EvalFailsWith("(quote a b)", "quote: expected exactly 1 argument, got 2");

    public static bool InvalidLambdaReportsSchemeError() =>
        EvalFailsWith("(LAMBDA (x))", "lambda: expected at least 2 arguments, got 1");

    public static bool InvalidSetReportsSchemeError() =>
        EvalFailsWith("(set! 1 2)", "set!: expected a symbol as the first argument");

    public static bool InvalidDefineReportsSchemeError() =>
        EvalFailsWith("(DEFINE)", "define: expected at least 2 arguments, got 0");

    public static bool InvalidDefineTargetReportsSchemeError() =>
        EvalFailsWith("(DEFINE 1 2)", "define: expected a symbol as the first argument");

    public static bool InvalidMacroReportsSchemeError() =>
        EvalFailsWith("(macro)", "macro: expected at least 3 arguments, got 0");

    public static bool InvalidMacroTargetReportsSchemeError() =>
        EvalFailsWith("(macro 1 () ((_ ) 42))", "macro: expected a symbol as the first argument");

    public static bool InvalidDefineSyntaxReportsSchemeError() =>
        EvalFailsWith("(define-syntax bad)", "define-syntax: expected exactly 2 arguments, got 1");

    public static bool InvalidDefineSyntaxTransformerReportsSchemeError() =>
        EvalFailsWith("(define-syntax bad 1)", "define-syntax: expected a syntax-rules transformer");

    public static bool InvalidLetSyntaxClauseReportsSchemeError() =>
        EvalFailsWith(
            "(LET-SYNTAX ((m (syntax-rules () ((_ x))))) 1)",
            "syntax-rules: each syntax-rules clause must contain exactly a pattern and template");

    public static bool MalformedSpecialFormsReportSchemeErrors() =>
        InvalidIfReportsSchemeError()
        && InvalidQuoteReportsSchemeError()
        && InvalidLambdaReportsSchemeError()
        && InvalidSetReportsSchemeError()
        && InvalidDefineReportsSchemeError()
        && InvalidDefineTargetReportsSchemeError()
        && InvalidMacroReportsSchemeError()
        && InvalidMacroTargetReportsSchemeError()
        && InvalidDefineSyntaxReportsSchemeError()
        && InvalidDefineSyntaxTransformerReportsSchemeError()
        && InvalidLetSyntaxClauseReportsSchemeError();

    public static bool MalformedSpecialFormsReportSourceLocations() =>
        EvalFailsWithSource(
            source: "\n(IF #t)",
            sourceName: "malformed-if.ss",
            expectedMessageFragment: "if: expected 2 or 3 arguments, got 1",
            startLine: 2,
            startColumn: 1,
            endLine: 2,
            endColumn: 8)
        && EvalFailsWithSource(
            source: "\n(DEFINE)",
            sourceName: "malformed-define.ss",
            expectedMessageFragment: "define: expected at least 2 arguments, got 0",
            startLine: 2,
            startColumn: 1,
            endLine: 2,
            endColumn: 9)
        && EvalFailsWithSource(
            source: "\n(macro)",
            sourceName: "malformed-macro.ss",
            expectedMessageFragment: "macro: expected at least 3 arguments, got 0",
            startLine: 2,
            startColumn: 1,
            endLine: 2,
            endColumn: 8)
        && EvalFailsWithSource(
            source: "\n(define-syntax bad)",
            sourceName: "malformed-define-syntax.ss",
            expectedMessageFragment: "define-syntax: expected exactly 2 arguments, got 1",
            startLine: 2,
            startColumn: 1,
            endLine: 2,
            endColumn: 20)
        && EvalFailsWithSource(
            source: "\n(LET-SYNTAX ((m (syntax-rules () ((_ x))))) 1)",
            sourceName: "malformed-let-syntax.ss",
            expectedMessageFragment: "syntax-rules: each syntax-rules clause must contain exactly a pattern and template",
            startLine: 2,
            startColumn: 34,
            endLine: 2,
            endColumn: 41);
}