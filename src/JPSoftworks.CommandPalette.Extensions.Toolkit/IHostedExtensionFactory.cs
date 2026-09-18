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
    /// Return a new instance on every call. Without an explicit CLSID, the runner prepares the first instance during
    /// registration and requires the same type GUID on later instances. An explicit registration defers creation until
    /// activation and owns the CLSID independently of the implementation type. The toolkit wraps Dispose
    /// to release the instance's lifetime lease. Signaling <see cref="ExtensionHostContext.ExtensionDisposedEvent" /> is optional.
    /// Only composition code should depend on the host context; core services can continue to use their selected
    /// logging abstraction.
    /// </remarks>
    IExtension CreateExtension(ExtensionHostContext context);
}