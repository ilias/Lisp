namespace Lisp;

internal static class RuntimeStats
{
    public static void ResetTotals() =>
        InterpreterContext.ResetTotals();

    public static Stopwatch? StartExpression()
    {
        // Always track totals; Stats flag only controls per-expression output
        InterpreterContext.BeginStats();
        return Stopwatch.StartNew();
    }

    public static void EndExpression(Stopwatch? stopwatch)
    {
        if (stopwatch == null)
            return;

        var snapshot = InterpreterContext.EndStats(stopwatch);

        // Only print per-expression breakdown when Stats mode is active
        if (InterpreterContext.IsStatsEnabled)
        {
            StatsReportFormatter.WriteReport(
                ConsoleOutput.WriteStats,
                ConsoleOutput.WriteStatsSegments,
                title: "  stats:",
                snapshot);
        }
    }

    public static void PrintTotals()
    {
        var context = InterpreterContext.RequireCurrent();
        ConsoleOutput.WriteStatsTotal($"  totals ({context.TotalExprs:N0} exprs):");
        var snapshot = InterpreterContext.GetTotalsSnapshot();
        StatsReportFormatter.WriteReport(
            ConsoleOutput.WriteStatsTotal,
            ConsoleOutput.WriteStatsTotalSegments,
            title: null,
            snapshot);
    }
}