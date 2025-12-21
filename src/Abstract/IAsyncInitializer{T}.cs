using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Asyncs.Initializers.Abstract;

/// <summary>
/// A lightweight, async-safe, allocation-free one-time initialization gate with a typed parameter. Ensures a given asynchronous initialization routine runs exactly once, even under concurrent callers, with support for cancellation, safe publication, and disposal.
/// </summary>
/// <typeparam name="T">The type of the parameter passed to the initialization method.</typeparam>
public interface IAsyncInitializer<in T> : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Executes the initialization routine if it has not yet run; otherwise returns immediately.
    /// Concurrent callers will await the same initialization.
    /// </summary>
    /// <param name="value">The value to pass to the initialization method.</param>
    /// <param name="cancellationToken">A token used to cancel waiting for initialization.</param>
    ValueTask Init(T value, CancellationToken cancellationToken = default);

    void InitSync(T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether initialization has completed successfully.
    /// </summary>
    bool IsInitialized { get; }
}

