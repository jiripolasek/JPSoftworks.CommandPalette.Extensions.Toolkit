// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

internal sealed class ExtensionClassFactory : BaseClassFactory, IDisposable
{
    private readonly Func<ExtensionHostContext, IExtension> _createExtension;
    private readonly Lock _activationGate = new();
    private readonly Lock _gate = new();
    private readonly ExtensionHostLifetime _lifetime;
    private readonly IExtensionHostLogSink? _logSink;
    private readonly bool _inferClassId;
    private bool _disposed;
    private HostedExtension? _preparedExtension;

    internal Guid ClassId { get; }

    protected override Guid Clsid => this.ClassId;

    protected override Guid Iid => typeof(IExtension).GUID;

    internal ExtensionClassFactory(
        Func<ExtensionHostContext, IExtension> createExtension,
        ExtensionHostLifetime lifetime,
        Guid? classId = null,
        IExtensionHostLogSink? logSink = null)
    {
        if (classId == Guid.Empty)
        {
            throw new ArgumentException("The COM class ID must not be empty.", nameof(classId));
        }

        this._createExtension = createExtension;
        this._lifetime = lifetime;
        this._logSink = logSink;
        this._inferClassId = !classId.HasValue;
        if (classId.HasValue)
        {
            this.ClassId = classId.Value;
        }
        else
        {
            this._preparedExtension = HostedExtension.Create(createExtension, lifetime);
            this.ClassId = this._preparedExtension.GetImplementationClassId();
        }
    }

    public void Dispose()
    {
        HostedExtension? extension;
        lock (this._gate)
        {
            this._disposed = true;
            extension = this._preparedExtension;
            this._preparedExtension = null;
        }

        extension?.Dispose();
    }

    protected override object CreateInstance() => this.Activate();

    internal IExtension Activate()
    {
        // Reserve the activation before running extension code, which can block or fail.
        this._lifetime.AcquireReference();
        try
        {
            // Serialize callbacks without blocking factory teardown.
            lock (this._activationGate)
            {
                HostedExtension? extension;
                lock (this._gate)
                {
                    ObjectDisposedException.ThrowIf(this._disposed, this);
                    extension = this._preparedExtension;
                    this._preparedExtension = null;
                }

                extension ??= this.CreateExtension();
                try
                {
                    lock (this._gate)
                    {
                        ObjectDisposedException.ThrowIf(this._disposed, this);
                        if (this._inferClassId && extension.GetImplementationClassId() != this.ClassId)
                        {
                            throw new InvalidOperationException("The extension factory returned a different CLSID.");
                        }

                        extension.Activate();
                        return extension;
                    }
                }
                catch (Exception ex)
                {
                    this._logSink?.LogError(nameof(ExtensionClassFactory), $"Failed to activate extension for CLSID {this.ClassId}", ex);
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

    private HostedExtension CreateExtension()
    {
        try
        {
            return HostedExtension.Create(this._createExtension, this._lifetime);
        }
        catch (Exception ex)
        {
            this._logSink?.LogError(nameof(ExtensionClassFactory), $"Failed to create extension for CLSID {this.ClassId}", ex);
            throw;
        }
    }
}