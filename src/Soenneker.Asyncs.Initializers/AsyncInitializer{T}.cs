using System;
using System.Runtime.CompilerServices;
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
    private readonly byte _initializerKind;
    private ValueAtomicBool _disposed;

    private readonly AsyncLock _lock = new();

    private Delegate? _initializer;

    public AsyncInitializer(Action<T> init)
    {
        _initializer = init ?? throw new ArgumentNullException(nameof(init));
        _initializerKind = 0;
    }

    public AsyncInitializer(Action<T, CancellationToken> init)
    {
        _initializer = init ?? throw new ArgumentNullException(nameof(init));
        _initializerKind = 1;
    }

    public AsyncInitializer(Func<T, ValueTask> initAsync)
    {
        _initializer = initAsync ?? throw new ArgumentNullException(nameof(initAsync));
        _initializerKind = 2;
    }

    public AsyncInitializer(Func<T, CancellationToken, ValueTask> initAsync)
    {
        _initializer = initAsync ?? throw new ArgumentNullException(nameof(initAsync));
        _initializerKind = 3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask Init(T value, CancellationToken cancellationToken = default)
    {
        if (_disposed.Value)
            throw new ObjectDisposedException(nameof(AsyncInitializer<T>));

        if (_initialized.Read())
            return ValueTask.CompletedTask;

        return InitSlowAsync(value, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InitSync(T value, CancellationToken cancellationToken = default)
    {
        if (_disposed.Value)
            throw new ObjectDisposedException(nameof(AsyncInitializer<T>));

        if (_initialized.Read())
            return;

        InitSlowSync(value, cancellationToken);
    }

    public bool IsInitialized => _initialized.Read();

    public void Dispose()
    {
        if (!_disposed.CompareAndSet(false, true))
            return;

        using (_lock.LockSync())
        {
            ClearInitializer_NoLock();
            _initialized.VolatileWrite(false);
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
            _initialized.VolatileWrite(false);
        }
    }

    private ValueTask InvokeInitializer(T value, CancellationToken ct)
    {
        // Each constructor fixes the callback type. The tag occupies existing field
        // padding and avoids repeated delegate type tests during initialization.
        switch (_initializerKind)
        {
            case 0:
                Unsafe.As<Action<T>>(_initializer)!(value);
                return default;
            case 1:
                Unsafe.As<Action<T, CancellationToken>>(_initializer)!(value, ct);
                return default;
            case 2:
                return Unsafe.As<Func<T, ValueTask>>(_initializer)!(value);
            default:
                return Unsafe.As<Func<T, CancellationToken, ValueTask>>(_initializer)!(value, ct);
        }
    }

    private async ValueTask InitSlowAsync(T value, CancellationToken ct)
    {
        using (await _lock.Lock(ct)
                          .NoSync())
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(nameof(AsyncInitializer<T>));

            if (_initialized.Read())
                return;

            await InvokeInitializer(value, ct)
                .NoSync();

            _initialized.VolatileWrite(true);

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

            if (_initialized.Read())
                return;

            InvokeInitializer(value, cancellationToken).AwaitSync();

            _initialized.VolatileWrite(true);

            // allow GC of captured graphs / callbacks
            ClearInitializer_NoLock();
        }
    }

    private void ClearInitializer_NoLock() => _initializer = null;
}
