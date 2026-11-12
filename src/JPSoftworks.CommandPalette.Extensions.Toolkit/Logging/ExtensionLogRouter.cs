// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

internal sealed class ExtensionLogRouter : IExtensionLogSink, IDisposable
{
    private readonly bool _isDebugEnabled;
    private readonly IDisposable[] _ownedResources;
    private readonly CompositeLogSink _sink;
    private readonly Lock _syncRoot = new();

    private bool _isDisposed;

    public ExtensionLogRouter(
        IEnumerable<IExtensionLogSink> sinks,
        IEnumerable<IDisposable> ownedResources,
        bool isDebugEnabled)
    {
        this._sink = new CompositeLogSink(sinks);
        this._ownedResources = ownedResources.ToArray();
        this._isDebugEnabled = isDebugEnabled;
    }

    public void Dispose()
    {
        lock (this._syncRoot)
        {
            if (this._isDisposed)
            {
                return;
            }

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

            this._isDisposed = true;
        }
    }

    public void Write(ExtensionLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Level == ExtensionLogLevel.Debug && !this._isDebugEnabled)
        {
            return;
        }

        lock (this._syncRoot)
        {
            if (!this._isDisposed)
            {
                this._sink.Write(entry);
            }
        }
    }
}