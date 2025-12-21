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

    // Unified initializer; sync overloads wrap into this.
    private Func<T, CancellationToken, ValueTask>? _initAsync;

    public AsyncInitializer(Action<T> init)
    {
        if (init is null)
            throw new ArgumentNullException(nameof(init));

        _initAsync = (value, _) =>
        {
            init(value);
            return ValueTask.CompletedTask;
        };
    }

    public AsyncInitializer(Action<T, CancellationToken> init)
    {
        if (init is null)
            throw new ArgumentNullException(nameof(init));

        _initAsync = (value, ct) =>
        {
            init(value, ct);
            return ValueTask.CompletedTask;
        };
    }

    public AsyncInitializer(Func<T, ValueTask> initAsync)
    {
        if (initAsync is null)
            throw new ArgumentNullException(nameof(initAsync));

        _initAsync = (value, _) => initAsync(value);
    }

    public AsyncInitializer(Func<T, CancellationToken, ValueTask> initAsync) => _initAsync = initAsync ?? throw new ArgumentNullException(nameof(initAsync));

    public ValueTask Init(T value, CancellationToken cancellationToken = default)
    {
        if (_disposed.Value)
            throw new ObjectDisposedException(nameof(AsyncInitializer<T>));

        if (_initialized.Value)
            return ValueTask.CompletedTask;

        return Slow(value, cancellationToken);

        async ValueTask Slow(T val, CancellationToken ct)
        {
            using (await _lock.Lock(ct)
                              .NoSync())
            {
                if (_disposed.Value)
                    throw new ObjectDisposedException(nameof(AsyncInitializer<T>));

                if (_initialized.Value)
                    return;

                Func<T, CancellationToken, ValueTask> init = _initAsync ?? throw new InvalidOperationException("No initializer configured.");
                await init(val, ct)
                    .NoSync();

                _initialized.Value = true;
                _initAsync = null; // allow GC
            }
        }
    }

    public void InitSync(T value, CancellationToken cancellationToken = default)
    {
        if (_disposed.Value)
            throw new ObjectDisposedException(nameof(AsyncInitializer<T>));

        if (_initialized.Value)
            return;

        using (_lock.LockSync(cancellationToken))
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(nameof(AsyncInitializer<T>));

            if (_initialized.Value)
                return;

            Func<T, CancellationToken, ValueTask> init = _initAsync ?? throw new InvalidOperationException("No initializer configured.");

            ValueTask vt = init(value, cancellationToken);
            Wait(vt);

            _initialized.Value = true;
            _initAsync = null; // allow GC
        }
    }

    public bool IsInitialized => _initialized.Value;

    public void Dispose()
    {
        if (!_disposed.CompareAndSet(false, true))
            return;

        using (_lock.LockSync())
        {
            _initAsync = null;
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
            _initAsync = null;
            _initialized.Value = false;
        }

        GC.SuppressFinalize(this);
    }

    private static void Wait(ValueTask valueTask)
    {
        if (valueTask.IsCompletedSuccessfully)
        {
            valueTask.GetAwaiter()
                     .GetResult();
            return;
        }

        if (SynchronizationContext.Current is null)
        {
            valueTask.AsTask()
                     .GetAwaiter()
                     .GetResult();
        }
    }
}