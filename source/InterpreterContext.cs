namespace Lisp;

public sealed class InterpreterContext
{
    [ThreadStatic] private static InterpreterContext? _current;

    public static InterpreterContext? Current
    {
        get => _current;
        set => _current = value;
    }

    public static InterpreterContext RequireCurrent() =>
        _current ?? throw new InvalidOperationException("No active interpreter context");

    public Program? Program { get; set; }

    public Dictionary<object, object?> Macros { get; } = [];
}