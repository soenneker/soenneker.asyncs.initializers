using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Asyncs.Initializers.Tests;

public sealed class InitializerRegressionTests
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task Every_callback_shape_runs_once(int shape)
    {
        int calls = 0;
        using var cancellation = new CancellationTokenSource();
        CancellationToken observed = default;
        using AsyncInitializer initializer = shape switch
        {
            0 => new((Action)(() => calls++)),
            1 => new((Action<CancellationToken>)(ct => { observed = ct; calls++; })),
            2 => new((Func<ValueTask>)(async () => { await Task.Yield(); calls++; })),
            _ => new((Func<CancellationToken, ValueTask>)(async ct => { await Task.Yield(); observed = ct; calls++; }))
        };

        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(async () => await initializer.Init(cancellation.Token))));
        initializer.InitSync(cancellation.Token);
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(initializer.IsInitialized).IsTrue();
        if (shape is 1 or 3)
            await Assert.That(observed).IsEqualTo(cancellation.Token);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task Generic_callback_shapes_preserve_state_and_token(int shape)
    {
        int calls = 0, observedValue = 0;
        CancellationToken observedToken = default;
        using var cancellation = new CancellationTokenSource();
        await using AsyncInitializer<int> initializer = shape switch
        {
            0 => new((Action<int>)(value => { observedValue = value; calls++; })),
            1 => new((Action<int, CancellationToken>)((value, ct) => { observedValue = value; observedToken = ct; calls++; })),
            2 => new((Func<int, ValueTask>)(async value => { await Task.Yield(); observedValue = value; calls++; })),
            _ => new((Func<int, CancellationToken, ValueTask>)(async (value, ct) => { await Task.Yield(); observedValue = value; observedToken = ct; calls++; }))
        };

        initializer.InitSync(42, cancellation.Token);
        await initializer.Init(99, cancellation.Token);
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(observedValue).IsEqualTo(42);
        if (shape is 1 or 3)
            await Assert.That(observedToken).IsEqualTo(cancellation.Token);
    }

    [Test]
    public async Task Failure_can_retry_and_disposal_rejects_further_initialization()
    {
        int calls = 0;
        var initializer = new AsyncInitializer((Action)(() =>
        {
            if (++calls == 1)
                throw new InvalidOperationException();
        }));
        await Assert.That(async () => await initializer.Init()).Throws<InvalidOperationException>();
        await initializer.Init();
        await initializer.DisposeAsync();
        await Assert.That(() => initializer.InitSync()).Throws<ObjectDisposedException>();
        await Assert.That(calls).IsEqualTo(2);
    }
}
