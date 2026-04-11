namespace Lisp;

public sealed class InterpreterRuntime
{
    public const int MaxHistoryEntries = 2000;

    private readonly List<string> _sessionHistory = [];
    private volatile bool _isEvaluating;
    private bool _cancelHandlerRegistered;
    private bool _endProgram;
    private CancellationTokenSource? _activeEvaluationCts;

    private sealed class EvaluationScope : IDisposable
    {
        private readonly CancellationTokenSource _current;
        private readonly CancellationTokenSource? _previous;
        private readonly IDisposable _tokenScope;
        private readonly InterpreterRuntime _owner;

        public EvaluationScope(InterpreterRuntime owner)
        {
            _owner = owner;
            _current = new CancellationTokenSource();
            _previous = Interlocked.Exchange(ref owner._activeEvaluationCts, _current);
            _tokenScope = InterpreterContext.PushCancellationToken(_current.Token);
        }

        public void Dispose()
        {
            _tokenScope.Dispose();
            Interlocked.Exchange(ref _owner._activeEvaluationCts, _previous);
            _current.Dispose();
        }
    }

    public IReadOnlyList<string> SessionHistory => _sessionHistory;

    public bool EndProgram
    {
        get => _endProgram;
        set => _endProgram = value;
    }

    public void AddSessionHistory(string entry) => _sessionHistory.Add(entry);

    private static string GetHistoryFilePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(root, "Lisp");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "history.txt");
    }

    public void LoadPersistentHistory(bool isInteractive, Action<string> addHistoryEntry)
    {
        if (!isInteractive) return;
        try
        {
            var path = GetHistoryFilePath();
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadLines(path))
                if (!string.IsNullOrWhiteSpace(line))
                    addHistoryEntry(line);
        }
        catch
        {
            // Ignore history persistence failures; REPL should still run.
        }
    }

    public void FlushPersistentHistory(bool isInteractive)
    {
        if (!isInteractive || _sessionHistory.Count == 0) return;
        try
        {
            var path = GetHistoryFilePath();
            var previous = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>();
            previous.AddRange(_sessionHistory);
            if (previous.Count > MaxHistoryEntries)
                previous = previous[^MaxHistoryEntries..];
            File.WriteAllLines(path, previous);
        }
        catch
        {
            // Ignore history persistence failures on shutdown.
        }
    }

    public T ExecuteWithEvaluationScope<T>(Func<T> action)
    {
        using var scope = new EvaluationScope(this);
        _isEvaluating = true;
        try
        {
            return action();
        }
        finally
        {
            _isEvaluating = false;
        }
    }

    public void ExecuteWithEvaluationScope(Action action)
        => ExecuteWithEvaluationScope(() =>
        {
            action();
            return 0;
        });

    public void EnsureCancelHandlerRegistered()
    {
        if (_cancelHandlerRegistered)
            return;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            if (_isEvaluating)
            {
                Volatile.Read(ref _activeEvaluationCts)?.Cancel();
                InterpreterContext.InterruptRequested = true;
                return;
            }

            _endProgram = true;
            Console.WriteLine();
        };

        _cancelHandlerRegistered = true;
    }
}
