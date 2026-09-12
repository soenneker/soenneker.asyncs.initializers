using System;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Asyncs.Initializers.Abstract;
using Soenneker.Asyncs.Locks;
using Soenneker.Atomics.ValueBools;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Asyncs.Initializers;

public sealed class AsyncInitializer<T> : IAsyncInitializer<T>
{
    private ValueAtomicBool _initialized;
    private ValueAtomicBool _disposed;

    private readonly AsyncLock _lock = new();

    private Delegate? _initializer;

    public AsyncInitializer(Action<T> init)
    {
        _initializer = init ?? throw new ArgumentNullException(nameof(init));
    }

    public AsyncInitializer(Action<T, CancellationToken> init)
    {
        _initializer = init ?? throw new ArgumentNullException(nameof(init));
    }

    public AsyncInitializer(Func<T, ValueTask> initAsync)
    {
        _initializer = initAsync ?? throw new ArgumentNullException(nameof(initAsync));
    }

    public AsyncInitializer(Func<T, CancellationToken, ValueTask> initAsync) => _initializer = initAsync ?? throw new ArgumentNullException(nameof(initAsync));

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

    private ValueTask InvokeInitializer(T value, CancellationToken ct)
    {
        switch (_initializer)
        {
            case Func<T, CancellationToken, ValueTask> callback:
                return callback(value, ct);
            case Func<T, ValueTask> callback:
                return callback(value);
            case Action<T> callback:
                callback(value);
                return ValueTask.CompletedTask;
            case Action<T, CancellationToken> callback:
                callback(value, ct);
                return ValueTask.CompletedTask;
            default:
                throw new InvalidOperationException("No initializer configured.");
        }
    }

    private async ValueTask InitSlowAsync(T value, CancellationToken ct)
    {
        using (await _lock.Lock(ct)
                          .NoSync())
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(nameof(AsyncInitializer<T>));

            if (_initialized.Value)
                return;

            await InvokeInitializer(value, ct)
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

            InvokeInitializer(value, cancellationToken).AwaitSync();

            _initialized.Value = true;

            // allow GC of captured graphs / callbacks
            ClearInitializer_NoLock();
        }
    }

    private void ClearInitializer_NoLock() => _initializer = null;
}
