// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

internal sealed class ExtensionHostLogRouter : IExtensionHostLogSink, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly CompositeExtensionHostLogSink _sink;
    private readonly IDisposable[] _ownedResources;
    private readonly bool _isDebugEnabled;

    private bool _isDisposed;

    public ExtensionHostLogRouter(
        IEnumerable<IExtensionHostLogSink> sinks,
        IEnumerable<IDisposable> ownedResources,
        bool isDebugEnabled)
    {
        this._sink = new CompositeExtensionHostLogSink(sinks);
        this._ownedResources = ownedResources.ToArray();
        this._isDebugEnabled = isDebugEnabled;
    }

    public void Write(ExtensionHostLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Level == ExtensionHostLogLevel.Debug && !this._isDebugEnabled)
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
}
