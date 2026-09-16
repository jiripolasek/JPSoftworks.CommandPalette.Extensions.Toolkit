// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using Microsoft.CommandPalette.Extensions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Creates Command Palette extension instances with access to their own host context.
/// </summary>
public interface IHostedExtensionFactory
{
    /// <summary>
    /// Creates a fully initialized extension for one COM activation.
    /// </summary>
    /// <param name="context">The lifecycle state for this instance, supplied by the extension host.</param>
    /// <returns>A new extension instance.</returns>
    /// <remarks>
    /// Return a new instance of the same COM class on every call. The runner prepares the first instance to discover
    /// its CLSID and hands it out once; subsequent activations call this factory again. The toolkit wraps Dispose
    /// to release the instance's lifetime lease. Signaling <see cref="ExtensionHostContext.ExtensionDisposedEvent" /> is optional.
    /// Only composition code should depend on the host context; core services can continue to use their selected
    /// logging abstraction.
    /// </remarks>
    IExtension CreateExtension(ExtensionHostContext context);
}