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

            bool firstHas = WithProgram(first, () => Macro.macros.ContainsKey(Symbol.Create("isolated-macro")));
            bool secondHas = WithProgram(second, () => Macro.macros.ContainsKey(Symbol.Create("isolated-macro")));

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
                Program.Stats = true;
                Program.ShowInputLines = true;
                Program.lastValue = false;
                Program.Iterations = 123;
                return 0;
            });

            bool secondDefaults = WithProgram(second, () =>
                !Program.Stats && !Program.ShowInputLines && Program.lastValue && Program.Iterations == 0);

            bool firstRetained = WithProgram(first, () =>
                Program.Stats && Program.ShowInputLines && !Program.lastValue && Program.Iterations == 123);

            return secondDefaults && firstRetained;
        }
        finally
        {
            InterpreterContext.Current = outerContext;
        }
    }
}