[![](https://img.shields.io/nuget/v/soenneker.asyncs.initializers.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.asyncs.initializers/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.asyncs.initializers/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.asyncs.initializers/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.asyncs.initializers.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.asyncs.initializers/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.asyncs.initializers/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.asyncs.initializers/actions/workflows/codeql.yml)

# Soenneker.Asyncs.Initializers

One-time initialization gates for coordinating synchronous or asynchronous setup across concurrent callers.

`AsyncInitializer` runs a parameterless callback once. `AsyncInitializer<T>` passes one caller-supplied value into the callback that wins initialization. A successful initialization is published to later callers; a failed or cancelled attempt can be retried.

## Installation

```bash
dotnet add package Soenneker.Asyncs.Initializers
```

## Initialize once

Create the initializer with the work that must run once, then call `Init` anywhere that requires the setup to be complete:

```csharp
using Soenneker.Asyncs.Initializers;

public sealed class SearchIndex
{
    private readonly AsyncInitializer _initializer;

    public SearchIndex()
    {
        _initializer = new AsyncInitializer(InitializeCore);
    }

    public ValueTask EnsureInitialized(
        CancellationToken cancellationToken = default)
    {
        return _initializer.Init(cancellationToken);
    }

    private async ValueTask InitializeCore(
        CancellationToken cancellationToken)
    {
        // Create mappings, warm metadata, or perform other one-time work.
        await Task.Delay(10, cancellationToken);
    }
}
```

If several callers arrive together, one executes the callback while the others wait. Once it completes successfully, later calls return immediately.

## Pass initialization input

Use `AsyncInitializer<T>` when the one-time callback needs a value supplied at initialization time:

```csharp
private readonly AsyncInitializer<string> _initializer =
    new(async (connectionString, cancellationToken) =>
    {
        await Connect(connectionString, cancellationToken);
    });

await _initializer.Init(connectionString, cancellationToken);
```

The value from the caller that acquires the initialization gate is used. Values supplied by concurrent callers waiting behind a successful initialization are ignored. Use a parameterless initializer over immutable constructor state when callers must not compete to choose configuration.

## Supported callbacks

Both initializer types accept synchronous and asynchronous callbacks:

```csharp
new AsyncInitializer(Action callback);
new AsyncInitializer(Action<CancellationToken> callback);
new AsyncInitializer(Func<ValueTask> callback);
new AsyncInitializer(Func<CancellationToken, ValueTask> callback);
```

The generic type provides the corresponding `Action<T>`, `Action<T, CancellationToken>`, `Func<T, ValueTask>`, and `Func<T, CancellationToken, ValueTask>` overloads.

## Failure and cancellation

`IsInitialized` becomes `true` only after the callback completes successfully. If the callback throws or is cancelled:

- that caller observes the exception or cancellation;
- the initializer remains uninitialized;
- the callback is retained;
- a later caller can attempt initialization again.

A cancellation token can cancel waiting for the gate and is passed to callbacks that accept a token. Cancellation cannot undo side effects already performed by the callback.

## Synchronous use

`InitSync` acquires the same gate as `Init`, so synchronous and asynchronous callers cannot run initialization simultaneously:

```csharp
initializer.InitSync(cancellationToken);
```

When the configured callback is asynchronous, `InitSync` blocks until its `ValueTask` completes. Prefer `Init` in asynchronous code to avoid blocking a thread and potential synchronization-context problems.

## Lifetime

After successful initialization, the callback reference is cleared so captured objects can be collected. Disposal also clears callback state and causes future `Init` or `InitSync` calls to throw `ObjectDisposedException`.

Disposing the initializer does not reverse initialization or dispose resources created by the callback. The owner remains responsible for those resources.

## API

| Member | Behavior |
| --- | --- |
| `Init(...)` | Waits for or performs asynchronous one-time initialization. |
| `InitSync(...)` | Waits for or performs initialization synchronously. |
| `IsInitialized` | `true` only after successful completion. |
| `Dispose()` / `DisposeAsync()` | Closes the gate and releases captured callback references. |
