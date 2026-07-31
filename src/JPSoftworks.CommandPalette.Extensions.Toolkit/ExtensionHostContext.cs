// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Provides process-owned services to an extension factory.
/// </summary>
public sealed class ExtensionHostContext
{
    internal ExtensionHostContext(ManualResetEvent extensionDisposedEvent)
    {
        this.ExtensionDisposedEvent = extensionDisposedEvent;
    }

    /// <summary>
    /// Gets the event that an extension must signal when it has been disposed.
    /// </summary>
    public ManualResetEvent ExtensionDisposedEvent { get; }
}
