// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

internal sealed class ExtensionLifetimeLease(ExtensionHostLifetime lifetime) : IDisposable
{
    private readonly Lock _gate = new();
    private HostedExtension? _extension;
    private bool _disposed;

    internal ManualResetEvent DisposedEvent { get; } = new(false);

    public void Dispose()
    {
        lock (this._gate)
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
            try
            {
                if (this._extension != null)
                {
                    lifetime.ReleaseReference(this._extension);
                }
            }
            finally
            {
                this._extension = null;
                this.DisposedEvent.Dispose();
            }
        }
    }

    internal void Activate(HostedExtension extension)
    {
        lock (this._gate)
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            if (this._extension != null)
            {
                return;
            }

            lifetime.AcquireReference(extension);
            this._extension = extension;
        }
    }
}