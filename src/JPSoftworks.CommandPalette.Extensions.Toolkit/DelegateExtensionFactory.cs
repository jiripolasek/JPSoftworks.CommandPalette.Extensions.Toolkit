// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using Microsoft.CommandPalette.Extensions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Creates Command Palette extension instances from a disposal event through a delegate.
/// </summary>
/// <example>
/// <code>
/// var factory = new DelegateExtensionFactory(disposedEvent =>
///     new MyExtension(disposedEvent));
/// </code>
/// </example>
[Obsolete("Use DelegateHostedExtensionFactory instead.")]
public sealed class DelegateExtensionFactory : IExtensionFactory
{
    private readonly Func<ManualResetEvent, IExtension> _createExtension;

    /// <summary>
    /// Initializes a new delegate-backed extension factory.
    /// </summary>
    /// <param name="createExtension">The function that creates an extension from its disposal event.</param>
    public DelegateExtensionFactory(Func<ManualResetEvent, IExtension> createExtension)
    {
        ArgumentNullException.ThrowIfNull(createExtension);
        this._createExtension = createExtension;
    }

    /// <inheritdoc />
    public IExtension CreateExtension(ManualResetEvent extensionDisposedEvent)
    {
        ArgumentNullException.ThrowIfNull(extensionDisposedEvent);
        return this._createExtension(extensionDisposedEvent);
    }
}
