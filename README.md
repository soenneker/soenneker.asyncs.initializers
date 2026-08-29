[![](https://img.shields.io/nuget/v/soenneker.asyncs.initializers.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.asyncs.initializers/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.asyncs.initializers/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.asyncs.initializers/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.asyncs.initializers.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.asyncs.initializers/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.asyncs.initializers/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.asyncs.initializers/actions/workflows/codeql.yml)

# Soenneker.Asyncs.Initializers

A lightweight, async-safe, allocation-free one-time initialization gate. Ensures a given asynchronous initialization routine runs exactly once, even under concurrent callers, with support for cancellation, safe publication, and disposal.

## Install

```bash
dotnet add package Soenneker.Asyncs.Initializers
```

## Quick start

```csharp
using Soenneker.Asyncs.Initializers.Abstract;

IAsyncInitializer asyncInitializer = /* resolve from DI */;
await asyncInitializer.Init(default);
```

Executes the initialization routine if it has not yet run; otherwise returns immediately. Concurrent callers will await the same initialization.

## What you get

- `IAsyncInitializer` — A lightweight, async-safe, allocation-free one-time initialization gate. Ensures a given asynchronous initialization routine runs exactly once, even under concurrent callers, with support for cancellation, safe publication, and disposal.
- `IAsyncInitializer<T>` — Initializes a value asynchronously and exposes it after initialization completes.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `IAsyncInitializer.Init(cancellationToken)` | Executes the initialization routine if it has not yet run; otherwise returns immediately. Concurrent callers will await the same initialization. | A task that completes when the init operation is complete. |
| `IAsyncInitializer.InitSync(cancellationToken)` | Synchronously initializes the instance. | Returns no value; the requested change is complete when the method returns. |
| `IAsyncInitializer.IsInitialized` | Gets whether initialization has completed successfully. | Gets whether initialization has completed successfully. |
| `IAsyncInitializer<T>.Init(value, cancellationToken)` | Executes the initialization routine if it has not yet run; otherwise returns immediately. Concurrent callers will await the same initialization. | A task that completes when the init operation is complete. |
| `IAsyncInitializer<T>.InitSync(value, cancellationToken)` | Synchronously initializes the instance. | Returns no value; the requested change is complete when the method returns. |
| `IAsyncInitializer<T>.IsInitialized` | Gets whether initialization has completed successfully. | Gets whether initialization has completed successfully. |

## Practical notes

- Cancellation stops pending work; it does not undo work that has already completed.
- Dispose instances you own when their scope ends so held resources can be released.
