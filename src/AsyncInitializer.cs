using System;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Asyncs.Initializers.Abstract;
using Soenneker.Asyncs.Locks;
using Soenneker.Atomics.ValueBools;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Asyncs.Initializers;

///<inheritdoc cref="IAsyncInitializer"/>
public sealed class AsyncInitializer : IAsyncInitializer
{
    private ValueAtomicBool _initialized;
    private ValueAtomicBool _disposed;

    private readonly AsyncLock _lock = new();

    // Primary unified entry point
    private Func<CancellationToken, ValueTask>? _initAsync;

    // Backing delegates to avoid constructor-closure captures
    private Action? _init;
    private Action<CancellationToken>? _initCt;
    private Func<ValueTask>? _initVt;

    public AsyncInitializer(Action init)
    {
        _init = init ?? throw new ArgumentNullException(nameof(init));
        _initAsync = InitFromAction;
    }

    public AsyncInitializer(Action<CancellationToken> init)
    {
        _initCt = init ?? throw new ArgumentNullException(nameof(init));
        _initAsync = InitFromActionCt;
    }

    public AsyncInitializer(Func<ValueTask> initAsync)
    {
        _initVt = initAsync ?? throw new ArgumentNullException(nameof(initAsync));
        _initAsync = InitFromFuncVt;
    }

    public AsyncInitializer(Func<CancellationToken, ValueTask> initAsync) => _initAsync = initAsync ?? throw new ArgumentNullException(nameof(initAsync));

    public ValueTask Init(CancellationToken cancellationToken = default)
    {
        if (_disposed.Value)
            throw new ObjectDisposedException(nameof(AsyncInitializer));

        if (_initialized.Value)
            return ValueTask.CompletedTask;

        return InitSlowAsync(cancellationToken);
    }

    public void InitSync(CancellationToken cancellationToken = default)
    {
        if (_disposed.Value)
            throw new ObjectDisposedException(nameof(AsyncInitializer));

        if (_initialized.Value)
            return;

        InitSlowSync(cancellationToken);
    }

    public bool IsInitialized => _initialized.Value;

    public void Dispose()
    {
        if (!_disposed.CompareAndSet(false, true))
            return;

        using (_lock.LockSync())
        {
            ClearInitializer_NoLock();
            _initialized.Value = false;
        }

        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed.CompareAndSet(false, true))
            return;

        using (await _lock.Lock()
                          .NoSync())
        {
            ClearInitializer_NoLock();
            _initialized.Value = false;
        }

        GC.SuppressFinalize(this);
    }

    private ValueTask InitFromAction(CancellationToken _)
    {
        _init!.Invoke();
        return ValueTask.CompletedTask;
    }

    private ValueTask InitFromActionCt(CancellationToken ct)
    {
        _initCt!.Invoke(ct);
        return ValueTask.CompletedTask;
    }

    private ValueTask InitFromFuncVt(CancellationToken _) => _initVt!.Invoke();

    private async ValueTask InitSlowAsync(CancellationToken ct)
    {
        using (await _lock.Lock(ct)
                          .NoSync())
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(nameof(AsyncInitializer));

            if (_initialized.Value)
                return;

            Func<CancellationToken, ValueTask> init = _initAsync ?? throw new InvalidOperationException("No initializer configured.");

            await init(ct)
                .NoSync();

            _initialized.Value = true;

            // allow GC of captured graphs / callbacks
            ClearInitializer_NoLock();
        }
    }

    private void InitSlowSync(CancellationToken cancellationToken)
    {
        using (_lock.LockSync(cancellationToken))
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(nameof(AsyncInitializer));

            if (_initialized.Value)
                return;

            Func<CancellationToken, ValueTask> init = _initAsync ?? throw new InvalidOperationException("No initializer configured.");

            init(cancellationToken).AwaitSync();

            _initialized.Value = true;

            // allow GC of captured graphs / callbacks
            ClearInitializer_NoLock();
        }
    }

    private void ClearInitializer_NoLock()
    {
        _initAsync = null;

        _init = null;
        _initCt = null;
        _initVt = null;
    }
}