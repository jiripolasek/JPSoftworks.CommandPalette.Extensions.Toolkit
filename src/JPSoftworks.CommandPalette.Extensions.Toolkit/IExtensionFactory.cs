// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using Microsoft.CommandPalette.Extensions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Creates Command Palette extension instances with access to the process-owned disposal event.
/// </summary>
[Obsolete("Use IHostedExtensionFactory instead.")]
public interface IExtensionFactory
{
    /// <summary>
    /// Creates a fully initialized extension that is ready for COM registration.
    /// </summary>
    /// <param name="extensionDisposedEvent">
    /// The event that the extension must signal when it has been disposed.
    /// </param>
    /// <returns>A new extension instance.</returns>
    /// <remarks>
    /// This is the original factory contract and is retained for compatibility. New code should implement
    /// <see cref="IHostedExtensionFactory"/> to receive the complete host context.
    /// </remarks>
    IExtension CreateExtension(ManualResetEvent extensionDisposedEvent);
}
