// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using Microsoft.CommandPalette.Extensions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Creates Command Palette extension instances with access to their own disposal event.
/// </summary>
[Obsolete("Use IHostedExtensionFactory instead.")]
public interface IExtensionFactory
{
    /// <summary>
    /// Creates a fully initialized extension for one COM activation.
    /// </summary>
    /// <param name="extensionDisposedEvent">
    /// An instance-owned compatibility event. Signaling is optional and does not request process shutdown.
    /// </param>
    /// <returns>A new extension instance.</returns>
    /// <remarks>
    /// This is the original factory contract and is retained for compatibility. New code should implement
    /// <see cref="IHostedExtensionFactory"/> to receive the complete host context.
    /// Return a new instance of the same COM class on every call. The runner prepares the first instance to discover
    /// its CLSID and hands it out once. Do not share or dispose the event; the toolkit releases it after wrapped disposal.
    /// </remarks>
    IExtension CreateExtension(ManualResetEvent extensionDisposedEvent);
}