// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using Microsoft.CommandPalette.Extensions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Creates Command Palette extension instances with access to the process-owned host context.
/// </summary>
public interface IExtensionFactory
{
    /// <summary>
    /// Creates a fully initialized extension that is ready for COM registration.
    /// </summary>
    /// <param name="context">The lifecycle state and diagnostics sink supplied by the extension host.</param>
    /// <returns>A new extension instance.</returns>
    /// <remarks>
    /// The extension must signal <see cref="ExtensionHostContext.ExtensionDisposedEvent"/> when it has been disposed.
    /// Only composition code should depend on the host context; core services can continue to use their selected
    /// logging abstraction.
    /// </remarks>
    IExtension CreateExtension(ExtensionHostContext context);
}