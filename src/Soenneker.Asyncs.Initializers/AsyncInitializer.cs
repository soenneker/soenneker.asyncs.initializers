using System;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Asyncs.Initializers.Abstract;
using Soenneker.Asyncs.Locks;
using Soenneker.Atomics.ValueBools;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Asyncs.Initializers;

public sealed class AsyncInitializer : IAsyncInitializer
{
    private ValueAtomicBool _initialized;
    private ValueAtomicBool _disposed;

    private readonly AsyncLock _lock = new();

    private Delegate? _initializer;

    public AsyncInitializer(Action init)
    {
        _initializer = init ?? throw new ArgumentNullException(nameof(init));
    }

    public AsyncInitializer(Action<CancellationToken> init)
    {
        _initializer = init ?? throw new ArgumentNullException(nameof(init));
    }

    public AsyncInitializer(Func<ValueTask> initAsync)
    {
        _initializer = initAsync ?? throw new ArgumentNullException(nameof(initAsync));
    }

    public AsyncInitializer(Func<CancellationToken, ValueTask> initAsync) => _initializer = initAsync ?? throw new ArgumentNullException(nameof(initAsync));

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

    private ValueTask InvokeInitializer(CancellationToken ct)
    {
        switch (_initializer)
        {
            case Func<CancellationToken, ValueTask> callback:
                return callback(ct);
            case Func<ValueTask> callback:
                return callback();
            case Action callback:
                callback();
                return ValueTask.CompletedTask;
            case Action<CancellationToken> callback:
                callback(ct);
                return ValueTask.CompletedTask;
            default:
                throw new InvalidOperationException("No initializer configured.");
        }
    }

    private async ValueTask InitSlowAsync(CancellationToken ct)
    {
        using (await _lock.Lock(ct)
                          .NoSync())
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(nameof(AsyncInitializer));

            if (_initialized.Value)
                return;

            await InvokeInitializer(ct)
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

            InvokeInitializer(cancellationToken).AwaitSync();

            _initialized.Value = true;

            // allow GC of captured graphs / callbacks
            ClearInitializer_NoLock();
        }
    }

    private void ClearInitializer_NoLock() => _initializer = null;
}
