namespace Lisp;

public sealed class Continuation
{
    private readonly SemaphoreSlim _callerReady = new(0, 1);
    private readonly SemaphoreSlim _bodyReady = new(0, 1);
    private object? _value;
    private Exception? _bodyException;
    private bool _done;
    private int _bodyThreadId;
    public ContinuationClosure K { get; }
    private readonly Thread _thread;

    public Continuation(Closure f)
    {
        K = new ContinuationClosure(this);
        _thread = new Thread(() =>
        {
            _bodyThreadId = Thread.CurrentThread.ManagedThreadId;
            _bodyReady.Wait();
            try
            {
                _value = EvaluateClosure(f, new Pair(K));
            }
            catch (ContinuationBodyUnwindSignal)
            {
                return;
            }
            catch (Exception ex)
            {
                _bodyException = ex;
            }
            _done = true;
            _callerReady.Release();
        })
        {
            IsBackground = true,
        };
        _thread.Start();
    }

    public object Run()
    {
        _bodyReady.Release();
        return AwaitCallerResult();
    }

    public object Resume(object? val)
    {
        if (_done)
            return _value ?? Pair.Empty;
        _value = val;
        _bodyReady.Release();
        return AwaitCallerResult();
    }

    private object AwaitCallerResult()
    {
        _callerReady.Wait();
        RethrowBodyException();
        return _value!;
    }

    private void RethrowBodyException()
    {
        if (_bodyException != null)
            ExceptionDispatchInfo.Capture(_bodyException).Throw();
    }

    internal object ApplyK(object? val) =>
        Thread.CurrentThread.ManagedThreadId == _bodyThreadId ? InvokeKFromBody(val) : Resume(val);

    private object InvokeKFromBody(object? val)
    {
        _value = val;
        _done = false;
        _callerReady.Release();
        _bodyReady.Wait();
        return _value!;
    }

    private static object EvaluateClosure(Closure c, Pair? args) =>
        DrainTailCalls(c.Eval(args));

    private static object DrainTailCalls(object value)
    {
        var result = value;
        while (result is TailCall tc)
            result = tc.Closure.Eval(tc.Args);
        return result;
    }

    private sealed class ContinuationBodyUnwindSignal() : Exception("continuation body completed");
}

public sealed class ContinuationClosure(Continuation cont) : Closure(
    ids: new Pair(Symbol.Create("_k_arg_")),
    body: null,
    env: new Env(),
    rawBody: null)
{
    private readonly Continuation _cont = cont;

    public override object Eval(Pair? args)
    {
        var val = args?.car;
        return _cont.ApplyK(val);
    }

    public override string ToString() => "#<continuation>";
}
