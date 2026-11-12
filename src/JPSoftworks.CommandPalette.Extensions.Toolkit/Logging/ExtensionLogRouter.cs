// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

internal sealed class ExtensionLogRouter : IExtensionLogSink, IDisposable, IAsyncDisposable
{
    [ThreadStatic]
    private static ExtensionLogRouter? _writingRouter;

    private readonly Lock _gate = new();
    private readonly bool _isDebugEnabled;
    private readonly IDisposable[] _ownedResources;
    private readonly CompositeLogSink _sink;
    private readonly TaskCompletionSource _disposeCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _activeWrites;
    private bool _disposeStarted;
    private int _disposeResourcesStarted;
    private int _disposingThreadId;

    public ExtensionLogRouter(
        IEnumerable<IExtensionLogSink> sinks,
        IEnumerable<IDisposable> ownedResources,
        bool isDebugEnabled)
    {
        this._sink = new CompositeLogSink(sinks);
        this._ownedResources = ownedResources.ToArray();
        this._isDebugEnabled = isDebugEnabled;
    }

    internal IReadOnlyList<IExtensionLogSink> Sinks => this._sink.Sinks;

    public void Dispose()
    {
        var disposal = this.DisposeAsync();
        // A callback cannot wait for itself; the last writer completes deferred cleanup.
        if (_writingRouter != this && Volatile.Read(ref this._disposingThreadId) != Environment.CurrentManagedThreadId)
        {
            disposal.GetAwaiter().GetResult();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (this._gate)
        {
            Volatile.Write(ref this._disposeStarted, true);
        }

        if (Volatile.Read(ref this._activeWrites) == 0)
        {
            this.DisposeOwnedResources();
        }

        return new ValueTask(this._disposeCompleted.Task);
    }

    private void DisposeOwnedResources()
    {
        // Another writer may have entered between the last decrement and the start of shutdown.
        if (Volatile.Read(ref this._activeWrites) != 0 || Interlocked.Exchange(ref this._disposeResourcesStarted, 1) != 0)
        {
            return;
        }

        Volatile.Write(ref this._disposingThreadId, Environment.CurrentManagedThreadId);
        try
        {
            for (var index = this._ownedResources.Length - 1; index >= 0; index--)
            {
                try
                {
                    this._ownedResources[index].Dispose();
                }
                catch
                {
                    // Logging shutdown must never interrupt the extension host.
                }
            }
        }
        finally
        {
            Volatile.Write(ref this._disposingThreadId, 0);
            this._disposeCompleted.SetResult();
        }
    }

    public void Write(ExtensionLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // A sink that logs back into this pipeline on the same thread would otherwise recurse until the stack overflows.
        if (_writingRouter is not null || (entry.Level == ExtensionLogLevel.Debug && !this._isDebugEnabled))
        {
            return;
        }

        lock (this._gate)
        {
            if (this._disposeStarted)
            {
                return;
            }

            Interlocked.Increment(ref this._activeWrites);
        }

        _writingRouter = this;
        try
        {
            // Keep callbacks outside the gate so writes remain concurrent and can reenter other threads.
            this._sink.Write(entry);
        }
        finally
        {
            _writingRouter = null;
            if (Interlocked.Decrement(ref this._activeWrites) == 0 && Volatile.Read(ref this._disposeStarted))
            {
                this.DisposeOwnedResources();
            }
        }
    }
}
