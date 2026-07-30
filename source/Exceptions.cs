namespace Lisp;

public readonly record struct SourceSpan(string? SourceName, int StartLine, int StartColumn, int EndLine, int EndColumn)
{
    public string FormatLocation()
    {
        string name = string.IsNullOrWhiteSpace(SourceName) ? "<input>" : SourceName!;
        return EndLine == StartLine && EndColumn == StartColumn
            ? $"{name}:{StartLine}:{StartColumn}"
            : $"{name}:{StartLine}:{StartColumn}-{EndLine}:{EndColumn}";
    }

    public override string ToString() => FormatLocation();
}

public sealed record SchemeStackFrame(string Procedure, string Expression, SourceSpan? Source)
{
    public string Format()
    {
        string proc = string.IsNullOrWhiteSpace(Procedure) ? "<anonymous>" : Procedure;
        string expr = string.IsNullOrWhiteSpace(Expression) ? string.Empty : $"  {Expression}";
        return Source is { } source
            ? $"{proc} at {source.FormatLocation()}{expr}"
            : $"{proc}{expr}";
    }
}

public sealed class LispException : Exception
{
    public SourceSpan? SchemeSource { get; private set; }
    public IReadOnlyList<SchemeStackFrame> SchemeStack { get; private set; } = [];

    public LispException(string message) : base(message) { }

    public LispException(string message, Exception? innerException) : base(message, innerException) { }

    public LispException AttachSchemeContext(SourceSpan? source, IReadOnlyList<SchemeStackFrame>? schemeStack)
    {
        if (SchemeSource == null && source != null)
            SchemeSource = source;
        if (SchemeStack.Count == 0 && schemeStack is { Count: > 0 })
            SchemeStack = [.. schemeStack];
        return this;
    }
}

public sealed class UserInterruptException() : Exception("evaluation interrupted by user") { }

public sealed class DebuggerPauseException : Exception
{
    public DebuggerPauseException(string message, Expression expression, string? procedureName, SourceSpan? source, IReadOnlyList<(string Name, object? Value)> locals)
        : base(message)
    {
        Expression = expression;
        ProcedureName = procedureName;
        Source = source;
        Locals = locals;
    }

    public Expression Expression { get; }
    public string? ProcedureName { get; }
    public new SourceSpan? Source { get; }
    public IReadOnlyList<(string Name, object? Value)> Locals { get; }
}

public sealed class ContinuationException(object? value, object tag) : Exception("continuation escape")
{
    public object? Value { get; } = value;
    public object Tag { get; } = tag;
}

public sealed record ErrorObject(string Message, object Irritants)
{
    public override string ToString() =>
        Pair.IsNull(Irritants as Pair)
            ? $"#<error \"{Message}\">"
            : $"#<error \"{Message}\" {Util.Dump(Irritants)}>";
}

public sealed class RaiseException(object value) : Exception($"raise: {Util.Dump(value)}")
{
    public object Value { get; } = value;

    public SourceSpan? SchemeSource { get; private set; }
    public IReadOnlyList<SchemeStackFrame> SchemeStack { get; private set; } = [];

    public RaiseException AttachSchemeContext(SourceSpan? source, IReadOnlyList<SchemeStackFrame>? schemeStack)
    {
        if (SchemeSource == null && source != null)
            SchemeSource = source;
        if (SchemeStack.Count == 0 && schemeStack is { Count: > 0 })
            SchemeStack = [.. schemeStack];
        return this;
    }
}

public static class ExceptionDisplay
{
    public static bool IsCatchableByTry(Exception exception) =>
        exception is LispException or RaiseException;

    public static Exception WrapHostException(Exception exception, string? messagePrefix = null)
    {
        if (exception is ContinuationException or LispException or RaiseException)
            return exception;

        string message = string.IsNullOrWhiteSpace(messagePrefix)
            ? exception.Message
            : $"{messagePrefix}: {exception.Message}";
        return new LispException(message, exception);
    }

    public static Exception Attach(Exception exception, SourceSpan? source, IReadOnlyList<SchemeStackFrame>? schemeStack)
    {
        if (exception is ContinuationException)
            return exception;

        return exception switch
        {
            LispException lispException => lispException.AttachSchemeContext(source, schemeStack),
            RaiseException raiseException => raiseException.AttachSchemeContext(source, schemeStack),
            _ => new LispException(exception.Message, exception).AttachSchemeContext(source, schemeStack),
        };
    }

    public static string FormatForConsole(string prefix, Exception exception)
    {
        List<string> lines = [$"{prefix}{exception.Message}"];

        if (exception is not (LispException or RaiseException))
            lines.Add($"  type: {exception.GetType().Name}");

        AppendInnerExceptions(lines, exception);

        switch (exception)
        {
            case LispException lispException:
                AppendLocation(lines, lispException.SchemeSource);
                AppendStack(lines, lispException.SchemeStack);
                break;
            case RaiseException raiseException:
                AppendLocation(lines, raiseException.SchemeSource);
                AppendStack(lines, raiseException.SchemeStack);
                break;
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendLocation(List<string> lines, SourceSpan? source)
    {
        if (source != null)
        {
            lines.Add($"  at {source.Value.FormatLocation()}");
            AppendSourceSnippet(lines, source.Value);
        }
    }

    private static void AppendStack(List<string> lines, IReadOnlyList<SchemeStackFrame>? schemeStack)
    {
        if (schemeStack is not { Count: > 0 })
            return;

        lines.Add("  Scheme stack:");
        foreach (var frame in schemeStack)
            lines.Add($"    {frame.Format()}");
    }

    private static void AppendInnerExceptions(List<string> lines, Exception exception)
    {
        Exception? current = exception.InnerException;
        int depth = 0;
        while (current != null && depth < 8)
        {
            lines.Add($"  caused by ({current.GetType().Name}): {current.Message}");
            current = current.InnerException;
            depth++;
        }
    }

    private static void AppendSourceSnippet(List<string> lines, SourceSpan source)
    {
        var context = InterpreterContext.Current;
        if (context == null)
            return;

        if (!context.TryGetSourceDocument(source.SourceName, out var document) || document == null)
            return;

        if (!document.TryGetLine(source.StartLine, out var lineText))
            return;

        int startColumn = Math.Max(1, source.StartColumn);
        int endColumn = source.StartLine == source.EndLine
            ? Math.Max(startColumn + 1, source.EndColumn)
            : Math.Max(startColumn + 1, document.GetLineLength(source.StartLine) + 1);
        int markerWidth = Math.Max(1, endColumn - startColumn);

        lines.Add($"      {source.StartLine,4} | {lineText}");
        lines.Add($"           | {new string(' ', Math.Max(0, startColumn - 1))}^{new string('~', markerWidth - 1)}");
    }
}
