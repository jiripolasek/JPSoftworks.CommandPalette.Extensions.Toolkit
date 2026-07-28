// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using Microsoft.CommandPalette.Extensions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Creates Command Palette extension instances through a delegate.
/// </summary>
/// <example>
/// <code>
/// var factory = new DelegateExtensionFactory(context =>
///     new MyExtension(context.ExtensionDisposedEvent));
/// </code>
/// </example>
public sealed class DelegateExtensionFactory : IExtensionFactory
{
    private readonly Func<ExtensionHostContext, IExtension> _createExtension;

    /// <summary>
    /// Initializes a new delegate-backed extension factory.
    /// </summary>
    /// <param name="createExtension">The function that creates an extension from its host context.</param>
    public DelegateExtensionFactory(Func<ExtensionHostContext, IExtension> createExtension)
    {
        ArgumentNullException.ThrowIfNull(createExtension);
        this._createExtension = createExtension;
    }

    /// <inheritdoc />
    public IExtension CreateExtension(ExtensionHostContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return this._createExtension(context);
    }
}