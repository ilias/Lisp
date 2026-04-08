namespace Lisp;

public static partial class Util
{
    private sealed class SourceHolder(SourceSpan span) { public SourceSpan Span { get; } = span; }
    public sealed class SourceDocument
    {
        private readonly int[] _lineStarts;

        public string Text { get; }
        public string? SourceName { get; }

        public SourceDocument(string text, string? sourceName)
        {
            Text = text;
            SourceName = sourceName;

            List<int> lineStarts = [0];
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] == '\r')
                {
                    if (index + 1 < text.Length && text[index + 1] == '\n') index++;
                    lineStarts.Add(index + 1);
                }
                else if (text[index] == '\n')
                {
                    lineStarts.Add(index + 1);
                }
            }
            _lineStarts = [.. lineStarts];
        }

        public SourceSpan GetSpan(int startOffset, int endOffset)
        {
            startOffset = Math.Clamp(startOffset, 0, Text.Length);
            endOffset = Math.Clamp(Math.Max(startOffset, endOffset), 0, Text.Length);
            var (startLine, startColumn) = GetLineColumn(startOffset);
            var (endLine, endColumn) = GetLineColumn(endOffset);
            return new SourceSpan(SourceName, startLine, startColumn, endLine, endColumn);
        }

        private (int Line, int Column) GetLineColumn(int offset)
        {
            int index = Array.BinarySearch(_lineStarts, offset);
            if (index < 0) index = ~index - 1;
            index = Math.Clamp(index, 0, _lineStarts.Length - 1);
            int lineStart = _lineStarts[index];
            return (index + 1, (offset - lineStart) + 1);
        }
    }

    private sealed class ParseContext(SourceDocument document, int baseOffset, ParseContext? previous)
    {
        public SourceDocument Document { get; } = document;
        public int BaseOffset { get; } = baseOffset;
        public ParseContext? Previous { get; } = previous;
    }

    private sealed class ParseScope(ParseContext? previous) : IDisposable
    {
        public void Dispose() => _parseContext = previous;
    }

    private static readonly ConditionalWeakTable<Pair, SourceHolder> _pairSources = new();

    public static Type[] GetTypes(object[] objs) =>
        objs.Select(o => o?.GetType() ?? typeof(object)).ToArray();

    [ThreadStatic] private static ParseContext? _parseContext;

    public static IDisposable PushSourceContext(SourceDocument document, int baseOffset)
    {
        var previous = _parseContext;
        _parseContext = new ParseContext(document, baseOffset, previous);
        return new ParseScope(previous);
    }

    public static SourceSpan? GetSource(object? obj) =>
        obj is Pair pair && _pairSources.TryGetValue(pair, out var holder) ? holder.Span : null;

    public static void PropagateSource(object? from, object? to)
    {
        if (to is not Pair pair || GetSource(pair) != null || GetSource(from) is not { } source)
            return;

        _pairSources.Remove(pair);
        _pairSources.Add(pair, new SourceHolder(source));
    }

    public static void PropagateSourceDeep(object? from, object? to)
    {
        if (GetSource(from) is not { } source)
            return;

        HashSet<Pair> visited = new(ReferenceEqualityComparer.Instance);
        ApplySourceDeep(to, source, visited);
    }

    public static SourceSpan? GetCurrentSourceSpan(int localStartOffset, int localEndOffset)
    {
        if (_parseContext == null)
            return null;

        return _parseContext.Document.GetSpan(_parseContext.BaseOffset + localStartOffset, _parseContext.BaseOffset + localEndOffset);
    }

    private static void RegisterPairSource(Pair pair, int localStartOffset, int localEndOffset)
    {
        if (GetCurrentSourceSpan(localStartOffset, localEndOffset) is not { } source)
            return;

        _pairSources.Remove(pair);
        _pairSources.Add(pair, new SourceHolder(source));
    }

    private static void ApplySourceDeep(object? obj, SourceSpan source, HashSet<Pair> visited)
    {
        if (obj is not Pair pair || !visited.Add(pair))
            return;

        if (GetSource(pair) == null)
        {
            _pairSources.Remove(pair);
            _pairSources.Add(pair, new SourceHolder(source));
        }

        ApplySourceDeep(pair.car, source, visited);
        ApplySourceDeep(pair.cdr, source, visited);
    }

    public static object CallMethod(Pair args, bool staticCall)
    {
        var objs = args.CdrPair?.CdrPair != null ? args.CdrPair.CdrPair.ToArray() : null;
        var types = objs != null ? GetTypes(objs) : Type.EmptyTypes;
        var type = staticCall ? GetType(args.car!.ToString()!) : args.car!.GetType();
        string methodName = args.CdrPair!.car!.ToString()!;
        if (type == null)
            throw new LispException($"Unknown type: {args.car}");
        try
        {
            var method = type.GetMethod(methodName, types);
            if (method != null)
                return method.Invoke(args.car, objs)!;

            var flags = BindingFlags.InvokeMethod
                      | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            return type.InvokeMember(methodName, flags, null, args.car, objs)!;
        }
        catch (TargetInvocationException tie)
        {
            throw ExceptionDisplay.WrapHostException(tie.InnerException ?? tie, methodName);
        }
        catch (Exception ex)
        {
            throw ExceptionDisplay.WrapHostException(ex, methodName);
        }
    }

    public static Type? GetType(string tname)
    {
        Type? type = Type.GetType(tname);
        if (type != null) return type;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            if ((type = asm.GetType(tname)) != null) return type;

        // Support "path/to/assembly.dll@Full.Type.Name" for loading from an explicit path.
        var comp = tname.Split('@');
        if (comp.Length == 2)
            try
            {
                if ((type = Assembly.LoadFrom(comp[0]).GetType(comp[1])) != null)
                    return type;
            }
            catch (Exception ex)
            {
                throw new LispException($"Failed to load assembly '{comp[0]}': {ex.Message}", ex);
            }

        return null;
    }

    public static void Throw(string message) => throw new LispException(message);
}
