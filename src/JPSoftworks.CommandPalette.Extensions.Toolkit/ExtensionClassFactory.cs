// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

internal sealed class ExtensionClassFactory : BaseClassFactory, IDisposable
{
    private readonly Func<ExtensionHostContext, IExtension> _createExtension;
    private readonly Lock _gate = new();
    private readonly ExtensionHostLifetime _lifetime;
    private bool _disposed;
    private HostedExtension? _preparedExtension;

    internal Guid ClassId { get; }

    protected override Guid Clsid => this.ClassId;

    protected override Guid Iid => typeof(IExtension).GUID;

    internal ExtensionClassFactory(
        Func<ExtensionHostContext, IExtension> createExtension,
        ExtensionHostLifetime lifetime)
    {
        this._createExtension = createExtension;
        this._lifetime = lifetime;
        // Existing factory contracts expose the CLSID only through the extension type.
        this._preparedExtension = HostedExtension.Create(createExtension, lifetime);
        this.ClassId = this._preparedExtension.ClassId;
    }

    public void Dispose()
    {
        lock (this._gate)
        {
            this._disposed = true;
            var extension = this._preparedExtension;
            this._preparedExtension = null;
            extension?.Dispose();
        }
    }

    protected override object CreateInstance() => this.Activate();

    internal IExtension Activate()
    {
        // Reserve the activation before running extension code, which can block or fail.
        this._lifetime.AcquireReference();
        try
        {
            lock (this._gate)
            {
                ObjectDisposedException.ThrowIf(this._disposed, this);
                var extension = this._preparedExtension ??
                                HostedExtension.Create(this._createExtension, this._lifetime);
                this._preparedExtension = null;
                try
                {
                    if (extension.ClassId != this.ClassId)
                    {
                        throw new InvalidOperationException("The extension factory returned a different CLSID.");
                    }

                    extension.Activate();
                    return extension;
                }
                catch
                {
                    extension.Dispose();
                    throw;
                }
            }
        }
        finally
        {
            this._lifetime.ReleaseReference();
        }
    }
}