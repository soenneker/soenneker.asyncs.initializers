using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Asyncs.Initializers.Abstract;

public interface IAsyncInitializer<in T> : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Executes the initialization routine if it has not yet run; otherwise returns immediately.
    /// Concurrent callers will await the same initialization.
    /// </summary>
    /// <param name="value">The value to pass to the initialization method.</param>
    /// <param name="cancellationToken">A token used to cancel waiting for initialization.</param>
    /// <returns>A task that completes when the init operation is complete.</returns>
    ValueTask Init(T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously initializes the instance.
    /// </summary>
    /// <param name="value">Value used to initialize the instance.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    void InitSync(T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether initialization has completed successfully.
    /// </summary>
    bool IsInitialized { get; }
}
