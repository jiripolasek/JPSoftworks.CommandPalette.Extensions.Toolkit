// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Provides lifecycle state for one extension instance.
/// </summary>
public sealed class ExtensionHostContext
{
    /// <summary>
    /// Gets the compatibility disposal event owned by this extension instance's lifetime lease.
    /// </summary>
    /// <remarks>
    /// Signaling is optional and does not request process shutdown. The toolkit tracks COM disposal through its
    /// wrapper. Do not share or dispose this event; it is valid until the wrapped extension's Dispose method returns.
    /// </remarks>
    public ManualResetEvent ExtensionDisposedEvent { get; }

    internal ExtensionHostContext(ManualResetEvent extensionDisposedEvent)
    {
        this.ExtensionDisposedEvent = extensionDisposedEvent;
    }
}