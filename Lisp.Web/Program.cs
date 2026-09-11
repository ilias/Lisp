using System.Collections.Concurrent;
using Lisp;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<LispSessionStore>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/eval", (EvalRequest request, LispSessionStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Code))
        return Results.BadRequest(new { error = "code is required" });

    var (sessionId, session) = store.GetOrCreate(request.SessionId);
    var evalResult = session.Eval(request.Code);
    return Results.Ok(new EvalResponse(sessionId, evalResult.Result, evalResult.Output, evalResult.Error));
});

app.MapPost("/api/reset", (ResetRequest request, LispSessionStore store) =>
{
    var sessionId = store.Reset(request.SessionId);
    return Results.Ok(new { sessionId });
});

app.Run();

/// <summary>Request payload for evaluating a Scheme expression in a browser session.</summary>
internal sealed record EvalRequest(string? SessionId, string Code);
/// <summary>Request payload for resetting (or creating) a browser session.</summary>
internal sealed record ResetRequest(string? SessionId);
/// <summary>Response payload with the evaluation result, captured stdout, and any error.</summary>
internal sealed record EvalResponse(string SessionId, string? Result, string? Output, string? Error);

/// <summary>One isolated Scheme runtime bound to a browser session, evaluated one request at a time.</summary>
internal sealed class LispSession
{
    private readonly InterpreterHost _host;
    private readonly object _gate = new();

    public DateTime LastUsedUtc { get; private set; } = DateTime.UtcNow;

    public LispSession()
    {
        _host = new InterpreterHost(new InterpreterHostOptions
        {
            // "core" excludes filesystem/process primitives; still not a hard sandbox since
            // the interpreter exposes .NET interop (call/call-static/new) at the language level.
            PrimitiveProfile = "core",
            InitPath = Path.Combine(AppContext.BaseDirectory, "init.ss"),
            LibraryPaths = [Path.Combine(AppContext.BaseDirectory, "lib")],
            DefaultSourceName = "<web>",
        });
    }

    public (string? Result, string? Output, string? Error) Eval(string code)
    {
        lock (_gate)
        {
            LastUsedUtc = DateTime.UtcNow;
            try
            {
                _host.Eval("(set! *OUTPUT* (open-output-string))", "<web-setup>");
                object? result;
                string? error = null;
                try
                {
                    result = _host.Eval(code, "<web>");
                }
                catch (Exception ex)
                {
                    result = null;
                    error = ex.Message;
                }

                var output = _host.Eval("(get-output-string *OUTPUT*)", "<web-setup>") as string;
                return (result is null ? null : Util.Dump(result), string.IsNullOrEmpty(output) ? null : output, error);
            }
            finally
            {
                try { _host.Eval("(set! *OUTPUT* '())", "<web-setup>"); } catch { /* best effort restore */ }
            }
        }
    }
}

/// <summary>Tracks one <see cref="LispSession"/> per browser session id and evicts idle sessions.</summary>
internal sealed class LispSessionStore
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, LispSession> _sessions = new();
    private readonly Timer _cleanupTimer;

    public LispSessionStore()
    {
        _cleanupTimer = new Timer(_ => EvictIdleSessions(), null, IdleTimeout, IdleTimeout);
    }

    public (string SessionId, LispSession Session) GetOrCreate(string? sessionId)
    {
        if (!string.IsNullOrEmpty(sessionId) && _sessions.TryGetValue(sessionId, out var existing))
            return (sessionId, existing);

        var id = Guid.NewGuid().ToString("n");
        var session = _sessions.GetOrAdd(id, _ => new LispSession());
        return (id, session);
    }

    public string Reset(string? sessionId)
    {
        if (!string.IsNullOrEmpty(sessionId))
            _sessions.TryRemove(sessionId, out _);

        var id = Guid.NewGuid().ToString("n");
        _sessions[id] = new LispSession();
        return id;
    }

    private void EvictIdleSessions()
    {
        var cutoff = DateTime.UtcNow - IdleTimeout;
        foreach (var (id, session) in _sessions)
        {
            if (session.LastUsedUtc < cutoff)
                _sessions.TryRemove(id, out _);
        }
    }
}
