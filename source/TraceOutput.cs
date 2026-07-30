namespace Lisp;

public static class TraceOutput
{
    private static void ClearCompactState(InterpreterContext context)
    {
        context.TraceCompactSymbol = null;
        context.TraceCompactDepth = 0;
        context.TraceCompactCount = 0;
    }

    public static void Reset()
    {
        if (InterpreterContext.Current is not { } context)
            return;

        context.TraceDepth = 0;
        ClearCompactState(context);
    }

    public static void FlushPending()
    {
        if (InterpreterContext.Current is not { } context)
            return;

        if (context.TraceCompactCount > 0 && !string.IsNullOrEmpty(context.TraceCompactSymbol))
        {
            string indent = context.TraceIndent ? new string(' ', context.TraceCompactDepth * 2) : string.Empty;
            if (context.TraceCompactCount >= context.TraceCompactMinRun)
                ConsoleOutput.WriteTrace($"{indent}[trace: {context.TraceCompactSymbol} repeated {context.TraceCompactCount} times]");
            else
                ConsoleOutput.WriteTrace($"{indent}[trace: {context.TraceCompactSymbol} repeated {context.TraceCompactCount} times (compact)]");
        }

        ClearCompactState(context);
    }

    private static string BuildSuffix(Expression? sourceExpr, InterpreterContext? context)
    {
        if (context == null || sourceExpr == null)
            return string.Empty;

        List<string> parts = [];

        if (context.TraceShowCode)
        {
            string text = sourceExpr.ToString() ?? "<unknown>";
            if (text.Length > 160)
                text = text[..157] + "...";
            parts.Add($"expr={text}");
        }

        if (context.TraceShowSource && sourceExpr.Source is { } source)
            parts.Add($"at {source.FormatLocation()}");

        return parts.Count == 0 ? string.Empty : "  ; " + string.Join(" | ", parts);
    }

    public static void EmitCall(Symbol symbol, object? args, Expression? sourceExpr)
    {
        var context = InterpreterContext.Current;
        bool isPrimitiveEvent = sourceExpr == null;

        if (context is { TraceCompact: true } && isPrimitiveEvent)
        {
            // Compact mode suppresses primitive call lines and optionally groups returns.
            return;
        }

        FlushPending();
        int depth = context?.TraceDepth ?? 0;
        string indent = context?.TraceIndent == false ? string.Empty : new string(' ', depth * 2);
        string line = indent + Util.Dump("call: ", symbol, args) + BuildSuffix(sourceExpr, context);
        ConsoleOutput.WriteTrace(line);
        if (context != null)
            context.TraceDepth = depth + 1;
    }

    public static void EmitReturn(Symbol symbol, object? result, Expression? sourceExpr)
    {
        var context = InterpreterContext.Current;
        bool isPrimitiveEvent = sourceExpr == null;

        if (context is { TraceCompact: true } && isPrimitiveEvent)
        {
            int compactDepth = context.TraceDepth;
            string symbolName = symbol.ToString();
            if (context.TraceCompactCount > 0
                && string.Equals(context.TraceCompactSymbol, symbolName, StringComparison.Ordinal)
                && context.TraceCompactDepth == compactDepth)
            {
                context.TraceCompactCount++;
            }
            else
            {
                FlushPending();
                context.TraceCompactSymbol = symbolName;
                context.TraceCompactDepth = compactDepth;
                context.TraceCompactCount = 1;
            }
            return;
        }

        FlushPending();
        if (context != null)
            context.TraceDepth = Math.Max(0, context.TraceDepth - 1);

        int depth = context?.TraceDepth ?? 0;
        string indent = context?.TraceIndent == false ? string.Empty : new string(' ', depth * 2);
        string line = indent + Util.Dump("ret:  ", symbol, result) + BuildSuffix(sourceExpr, context);
        ConsoleOutput.WriteTrace(line);
    }
}
