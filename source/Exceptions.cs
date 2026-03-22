namespace Lisp;

public sealed class LispException(string message) : Exception(message);

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
}
