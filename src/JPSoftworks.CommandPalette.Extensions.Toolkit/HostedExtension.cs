// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using Microsoft.CommandPalette.Extensions;
using WinRT;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

[GeneratedWinRTExposedType]
internal sealed partial class HostedExtension : IExtension
{
    private readonly Lock _gate = new();
    private readonly ExtensionLifetimeLease _lease;
    private IExtension? _extension;

    private HostedExtension(IExtension extension, ExtensionLifetimeLease lease)
    {
        this._extension = extension;
        this._lease = lease;
    }

    internal Guid GetImplementationClassId()
    {
        lock (this._gate)
        {
            ObjectDisposedException.ThrowIf(this._extension is null, this);
            return this._extension.GetType().GUID;
        }
    }

    public object GetProvider(ProviderType providerType)
    {
        lock (this._gate)
        {
            ObjectDisposedException.ThrowIf(this._extension is null, this);
            return this._extension.GetProvider(providerType);
        }
    }

    public void Dispose()
    {
        lock (this._gate)
        {
            var extension = this._extension;
            if (extension is null)
            {
                return;
            }

            this._extension = null;
            try
            {
                extension.Dispose();
            }
            finally
            {
                this._lease.Dispose();
            }
        }
    }

    internal static HostedExtension Create(
        Func<ExtensionHostContext, IExtension> createExtension,
        ExtensionHostLifetime lifetime)
    {
        var lease = new ExtensionLifetimeLease(lifetime);
        try
        {
            var extension = createExtension(new ExtensionHostContext(lease.DisposedEvent))
                            ?? throw new InvalidOperationException("The extension factory returned null.");
            return new HostedExtension(extension, lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal void Activate()
    {
        lock (this._gate)
        {
            ObjectDisposedException.ThrowIf(this._extension is null || this._lease.DisposedEvent.WaitOne(0), this);
            this._lease.Activate(this);
        }
    }
}