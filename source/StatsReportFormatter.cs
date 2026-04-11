namespace Lisp;

internal static class StatsReportFormatter
{
    private const int MaxCounterSummaryItems = 4;

    public static void WriteReport(
        Action<string> writeLine,
        Action<IEnumerable<ConsoleOutput.Segment>> writeSegments,
        string? title,
        InterpreterContext.StatsReportSnapshot snapshot)
    {
        if (!string.IsNullOrEmpty(title))
            writeLine(title);

        WriteStatsField(writeSegments, "elapsed", $"{snapshot.ElapsedMs,10:F3} ms");
        WriteStatsField(writeSegments, "status", FormatStatusSummary(snapshot.InterpEmits, snapshot.InterpExecs, snapshot.TreeWalkCalls), GetStatusColor(snapshot.InterpEmits, snapshot.InterpExecs, snapshot.TreeWalkCalls));
        WriteStatsField(writeSegments, "runtime", FormatRuntimePathSummary(snapshot.InterpExecs, snapshot.TreeWalkCalls), GetRuntimeColor(snapshot.InterpExecs, snapshot.TreeWalkCalls));
        WriteStatsField(writeSegments, "work", $"closures={snapshot.Iterations:N0}, prims={snapshot.PrimCalls:N0}");
        WriteStatsField(writeSegments, "control", $"tail-calls={snapshot.TailCalls:N0}, env-frames={snapshot.EnvFrames:N0}");
        WriteStatsField(writeSegments, "throughput", FormatThroughputSummary(snapshot.ElapsedMs, snapshot.Iterations, snapshot.PrimCalls, snapshot.InterpExecs, snapshot.TreeWalkCalls));
        WriteStatsField(writeSegments, "fallback", FormatFallbackSummary(snapshot.InterpEmits, snapshot.InterpExecs, snapshot.TreeWalkCalls), GetFallbackColor(snapshot.InterpEmits, snapshot.InterpExecs, snapshot.TreeWalkCalls));
        WriteStatsField(writeSegments, "memory", $"allocated={FormatBytes(snapshot.AllocatedBytes)}{FormatOptionalMemory(snapshot.HeapBytes, snapshot.Gc0, snapshot.Gc1, snapshot.Gc2)}");

        if (snapshot.EmitKinds.Count > 0)
            WriteStatsField(writeSegments, "emit-kinds", FormatCounterSummary(snapshot.EmitKinds), ConsoleColor.DarkYellow);
        if (snapshot.ExecKinds.Count > 0)
            WriteStatsField(writeSegments, "exec-kinds", FormatCounterSummary(snapshot.ExecKinds), ConsoleColor.Yellow);
    }

    private static void WriteStatsField(Action<IEnumerable<ConsoleOutput.Segment>> writeSegments, string label, string value, ConsoleColor? valueColor = null)
    {
        writeSegments(
        [
            new("    "),
            new($"{label,-11}", ConsoleColor.Gray),
            new(value, valueColor),
        ]);
    }

    private static string FormatCounterSummary(Dictionary<string, long> counters)
    {
        var ordered = counters.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).ToArray();
        int shown = Math.Min(MaxCounterSummaryItems, ordered.Length);
        string summary = string.Join(", ", ordered.Take(shown).Select(kv => $"{kv.Key}={kv.Value:N0}"));
        if (ordered.Length > shown)
            summary += $", +{ordered.Length - shown} more";
        return summary;
    }

    private static string FormatFallbackSummary(long interpEmits, long interpExecs, long treeWalkCalls)
    {
        if (interpEmits == 0 && interpExecs == 0 && treeWalkCalls == 0)
            return "none";

        List<string> parts = [];
        if (interpEmits != 0) parts.Add($"sites={interpEmits:N0}");
        if (interpExecs != 0) parts.Add($"runs={interpExecs:N0}");
        if (treeWalkCalls != 0) parts.Add($"tree-walk={treeWalkCalls:N0}");
        return string.Join(", ", parts);
    }

    private static string FormatThroughputSummary(double elapsedMs, long iterations, long primCalls, long interpExecs, long treeWalkCalls)
    {
        if (elapsedMs <= 0.0)
            return "n/a";

        if (!HasMeaningfulThroughputSample(elapsedMs, iterations, primCalls))
            return "sample too small";

        double closuresPerMs = iterations / elapsedMs;
        double primsPerMs = primCalls / elapsedMs;
        double fallbackPerKClosures = iterations == 0 ? 0.0 : ((interpExecs + treeWalkCalls) * 1000.0) / iterations;
        string summary = $"closures/ms={closuresPerMs:F1}, prims/ms={primsPerMs:F1}";
        if (interpExecs != 0 || treeWalkCalls != 0)
            summary += $", fallbacks/1k-closures={fallbackPerKClosures:F2}";
        return summary;
    }

    private static bool HasMeaningfulThroughputSample(double elapsedMs, long iterations, long primCalls) =>
        elapsedMs >= 1.0 && (iterations >= 100 || primCalls >= 100);

    private static string FormatOptionalMemory(long? heapBytes, int? gc0, int? gc1, int? gc2)
    {
        List<string> parts = [];
        if (heapBytes != null) parts.Add($"heap={FormatBytes(heapBytes.Value)}");
        if (gc0 != null && gc1 != null && gc2 != null) parts.Add($"gc={gc0}/{gc1}/{gc2}");
        return parts.Count == 0 ? string.Empty : ", " + string.Join(", ", parts);
    }

    private static string FormatRuntimePathSummary(long interpExecs, long treeWalkCalls) =>
        (interpExecs, treeWalkCalls) switch
        {
            (0, 0) => "vm-only",
            (> 0, 0) => $"vm + interp fallback ({interpExecs:N0} run{(interpExecs == 1 ? string.Empty : "s")})",
            (0, > 0) => $"vm + tree-walk fallback ({treeWalkCalls:N0} call{(treeWalkCalls == 1 ? string.Empty : "s")})",
            _ => $"vm + fallback (interp={interpExecs:N0}, tree-walk={treeWalkCalls:N0})",
        };

    private static string FormatStatusSummary(long interpEmits, long interpExecs, long treeWalkCalls) =>
        (interpEmits, interpExecs, treeWalkCalls) switch
        {
            (0, 0, 0) => "clean vm path",
            (_, 0, 0) => $"clean run, fallback sites present ({interpEmits:N0})",
            (_, not 0, 0) => "interp fallback observed",
            (_, 0, not 0) => "tree-walk fallback observed",
            _ => "multiple fallback paths observed",
        };

    private static ConsoleColor GetFallbackStatusColor(bool isClean, bool hasBoth)
    {
        if (isClean) return ConsoleColor.Green;
        if (hasBoth) return ConsoleColor.Red;
        return ConsoleColor.Yellow;
    }

    private static ConsoleColor GetStatusColor(long interpEmits, long interpExecs, long treeWalkCalls) =>
        (interpEmits, interpExecs, treeWalkCalls) switch
        {
            (0, 0, 0) => ConsoleColor.Green,
            (_, 0, 0) => ConsoleColor.DarkYellow,
            _ => GetFallbackStatusColor(false, interpExecs != 0 && treeWalkCalls != 0),
        };

    private static ConsoleColor GetRuntimeColor(long interpExecs, long treeWalkCalls) =>
        GetFallbackStatusColor(interpExecs == 0 && treeWalkCalls == 0, interpExecs != 0 && treeWalkCalls != 0);

    private static ConsoleColor GetFallbackColor(long interpEmits, long interpExecs, long treeWalkCalls) =>
        (interpEmits, interpExecs, treeWalkCalls) switch
        {
            (0, 0, 0) => ConsoleColor.Green,
            (_, 0, 0) => ConsoleColor.DarkYellow,
            _ => GetFallbackStatusColor(false, interpExecs != 0 && treeWalkCalls != 0),
        };

    private static string FormatBytes(long bytes) =>
        bytes >= 1_048_576 ? $"{bytes / 1_048_576.0:F2} MB" :
        bytes >= 1_024 ? $"{bytes / 1_024.0:F1} KB" :
        $"{bytes} B";
}