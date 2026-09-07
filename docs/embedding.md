# Embedding Lisp in .NET

`InterpreterHost` provides an isolated Scheme runtime for applications that want to evaluate expressions or files without using the interactive REPL.

## Structured configuration

Use `InterpreterHostOptions` when configuring an embedded runtime:

```csharp
using Lisp;

var host = new InterpreterHost(new InterpreterHostOptions
{
    PrimitiveProfile = "full",
    InitPath = Path.Combine(AppContext.BaseDirectory, "init.ss"),
    LibraryPaths = [Path.Combine(AppContext.BaseDirectory, "lib")],
    DefaultSourceName = "<application>",
    StatsEnabled = false,
    ProfileEnabled = false
});

var result = host.Eval("(+ 20 22)");
Console.WriteLine(result); // 42
```

`InitPath` should point to a complete bootstrap file such as the repository's `init.ss`. Use `EvalFile` for application files loaded after initialization.

## Legacy constructor

The original constructor remains available for simple cases:

```csharp
var host = new InterpreterHost(primitiveProfile: "full");
host.LoadInitFromBaseDirectory();
var result = host.Eval("(* 6 7)");
```

## Host lifetime

A host preserves Scheme state across calls and keeps its runtime isolated from other hosts. A host is intended for serial use: do not evaluate concurrently on the same instance. Create one host per independent evaluation flow when parallel work is required.

`Eval` and `EvalFile` accept a `CancellationToken`. Cancellation raises `UserInterruptException`; the host can be reused after the evaluation has stopped.

The host exposes `Output` and `Error` writers for embedding integrations that need to retain their own output streams. `DefaultSourceName` is used for diagnostics when `Eval` is called without an explicit source name.
