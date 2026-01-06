using System;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Asyncs.Initializers.Abstract;
using Soenneker.Asyncs.Locks;
using Soenneker.Atomics.ValueBools;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Asyncs.Initializers;

///<inheritdoc cref="IAsyncInitializer{T}"/>
public sealed class AsyncInitializer<T> : IAsyncInitializer<T>
{
    private ValueAtomicBool _initialized;
    private ValueAtomicBool _disposed;

    private readonly AsyncLock _lock = new();

    // Unified initializer entry point
    private Func<T, CancellationToken, ValueTask>? _initAsync;

    // Backing delegates (avoid ctor closures)
    private Action<T>? _init;
    private Action<T, CancellationToken>? _initCt;
    private Func<T, ValueTask>? _initVt;

    public AsyncInitializer(Action<T> init)
    {
        _init = init ?? throw new ArgumentNullException(nameof(init));
        _initAsync = InitFromAction;
    }

    public AsyncInitializer(Action<T, CancellationToken> init)
    {
        _initCt = init ?? throw new ArgumentNullException(nameof(init));
        _initAsync = InitFromActionCt;
    }

    public AsyncInitializer(Func<T, ValueTask> initAsync)
    {
        _initVt = initAsync ?? throw new ArgumentNullException(nameof(initAsync));
        _initAsync = InitFromFuncVt;
    }

    public AsyncInitializer(Func<T, CancellationToken, ValueTask> initAsync) => _initAsync = initAsync ?? throw new ArgumentNullException(nameof(initAsync));

    public ValueTask Init(T value, CancellationToken cancellationToken = default)
    {
        if (_disposed.Value)
            throw new ObjectDisposedException(nameof(AsyncInitializer<T>));

        if (_initialized.Value)
            return ValueTask.CompletedTask;

        return InitSlowAsync(value, cancellationToken);
    }

    public void InitSync(T value, CancellationToken cancellationToken = default)
    {
        if (_disposed.Value)
            throw new ObjectDisposedException(nameof(AsyncInitializer<T>));

        if (_initialized.Value)
            return;

        InitSlowSync(value, cancellationToken);
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
    }

    private ValueTask InitFromAction(T value, CancellationToken _)
    {
        _init!.Invoke(value);
        return ValueTask.CompletedTask;
    }

    private ValueTask InitFromActionCt(T value, CancellationToken ct)
    {
        _initCt!.Invoke(value, ct);
        return ValueTask.CompletedTask;
    }

    private ValueTask InitFromFuncVt(T value, CancellationToken _) => _initVt!.Invoke(value);

    private async ValueTask InitSlowAsync(T value, CancellationToken ct)
    {
        using (await _lock.Lock(ct)
                          .NoSync())
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(nameof(AsyncInitializer<T>));

            if (_initialized.Value)
                return;

            Func<T, CancellationToken, ValueTask> init = _initAsync ?? throw new InvalidOperationException("No initializer configured.");

            await init(value, ct)
                .NoSync();

            _initialized.Value = true;

            // allow GC of captured graphs / callbacks
            ClearInitializer_NoLock();
        }
    }

    private void InitSlowSync(T value, CancellationToken cancellationToken)
    {
        using (_lock.LockSync(cancellationToken))
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(nameof(AsyncInitializer<T>));

            if (_initialized.Value)
                return;

            Func<T, CancellationToken, ValueTask> init = _initAsync ?? throw new InvalidOperationException("No initializer configured.");

            init(value, cancellationToken).AwaitSync();

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