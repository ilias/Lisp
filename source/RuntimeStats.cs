namespace Lisp;

internal static class RuntimeStats
{
    public static void ResetTotals() =>
        InterpreterContext.ResetTotals();

    public static Stopwatch? StartExpression()
    {
        if (!InterpreterContext.IsStatsEnabled)
            return null;

        InterpreterContext.BeginStats();
        return Stopwatch.StartNew();
    }

    public static void EndExpression(Stopwatch? stopwatch)
    {
        if (stopwatch == null)
            return;

        var snapshot = InterpreterContext.EndStats(stopwatch);
        StatsReportFormatter.WriteReport(
            ConsoleOutput.WriteStats,
            ConsoleOutput.WriteStatsSegments,
            title: "  stats:",
            snapshot);
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