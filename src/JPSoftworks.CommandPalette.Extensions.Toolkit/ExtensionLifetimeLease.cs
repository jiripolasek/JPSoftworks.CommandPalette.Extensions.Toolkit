// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

internal sealed class ExtensionLifetimeLease(ExtensionHostLifetime lifetime) : IDisposable
{
    private readonly Lock _gate = new();
    private bool _active;
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
                if (this._active)
                {
                    lifetime.ReleaseReference();
                }
            }
            finally
            {
                this.DisposedEvent.Dispose();
            }
        }
    }

    internal void Activate()
    {
        lock (this._gate)
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            if (this._active)
            {
                return;
            }

            lifetime.AcquireReference();
            this._active = true;
        }
    }
}