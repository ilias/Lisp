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

        if (InterpreterContext.IsStatsEnabled)
        {
            StatsReportFormatter.WriteReport(
                ConsoleOutput.WriteStats,
                ConsoleOutput.WriteStatsSegments,
                title: "  stats:",
                snapshot);
        }
        else if (InterpreterContext.IsProfileEnabled)
        {
            StatsReportFormatter.WriteProfileReport(
                ConsoleOutput.WriteProfile,
                ConsoleOutput.WriteProfileSegments,
                title: "  profile:",
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

    public static void PrintProfileTotals()
    {
        var context = InterpreterContext.RequireCurrent();
        ConsoleOutput.WriteProfileTotal($"  profile totals ({context.TotalExprs:N0} exprs):");
        var snapshot = InterpreterContext.GetTotalsSnapshot();
        StatsReportFormatter.WriteProfileReport(
            ConsoleOutput.WriteProfileTotal,
            ConsoleOutput.WriteProfileTotalSegments,
            title: null,
            snapshot);
    }
}