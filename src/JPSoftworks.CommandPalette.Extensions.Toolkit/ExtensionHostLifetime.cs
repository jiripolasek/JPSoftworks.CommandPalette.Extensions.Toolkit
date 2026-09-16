// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Runtime.InteropServices;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

internal sealed partial class ExtensionHostLifetime
{
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource _shutdown = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action _suspendClassObjects;
    private readonly HashSet<HostedExtension> _activeExtensions = [];
    private bool _draining;
    private int _referenceCount;
    private bool _suspended;

    internal Task Shutdown => this._shutdown.Task;

    internal ExtensionHostLifetime()
        : this(static () => Marshal.ThrowExceptionForHR(CoSuspendClassObjects()))
    {
    }

    internal ExtensionHostLifetime(Action suspendClassObjects)
    {
        this._suspendClassObjects = suspendClassObjects;
    }

    internal void AcquireReference(HostedExtension? extension = null)
    {
        lock (this._gate)
        {
            if (this._draining)
            {
                throw new COMException("The extension host is shutting down.", unchecked((int)0x80040111));
            }

            if (extension != null)
            {
                this._activeExtensions.Add(extension);
            }

            this._referenceCount++;
        }
    }

    internal void ReleaseReference(HostedExtension? extension = null)
    {
        lock (this._gate)
        {
            if (extension != null)
            {
                this._activeExtensions.Remove(extension);
            }

            if (--this._referenceCount == 0)
            {
                this.DrainCore();
            }
        }
    }

    internal void Drain()
    {
        lock (this._gate)
        {
            this.DrainCore();
        }
    }

    /// <summary>Disposes remaining active instances after activation has been drained.</summary>
    internal void DisposeActiveExtensions()
    {
        HostedExtension[] extensions;
        lock (this._gate)
        {
            if (!this._draining)
            {
                throw new InvalidOperationException("Activation must be drained before disposing active extensions.");
            }

            extensions = [.. this._activeExtensions];
        }

        List<Exception>? errors = null;
        foreach (var extension in extensions)
        {
            try
            {
                extension.Dispose();
            }
            catch (Exception ex)
            {
                (errors ??= []).Add(ex);
            }
        }

        if (errors != null)
        {
            throw new AggregateException("Failed to dispose active extension instances.", errors);
        }
    }

    private void DrainCore()
    {
        this._draining = true;
        if (this._suspended)
        {
            return;
        }

        try
        {
            this._suspendClassObjects();
            this._suspended = true;
            this._shutdown.TrySetResult();
        }
        catch (Exception ex)
        {
            this._shutdown.TrySetException(ex);
            throw;
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoSuspendClassObjects();
}