using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Asyncs.Initializers.Abstract;

/// <summary>
/// A lightweight, async-safe, allocation-free one-time initialization gate. Ensures a given asynchronous initialization routine runs exactly once, even under concurrent callers, with support for cancellation, safe publication, and disposal.
/// </summary>
public interface IAsyncInitializer : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Executes the initialization routine if it has not yet run; otherwise returns immediately.
    /// Concurrent callers will await the same initialization.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel waiting for initialization.</param>
    ValueTask Init(CancellationToken cancellationToken = default);

    void InitSync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether initialization has completed successfully.
    /// </summary>
    bool IsInitialized { get; }
}
