// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Discards all extension host diagnostics.
/// </summary>
public sealed class NullExtensionHostLogSink : IExtensionHostLogSink
{
    private NullExtensionHostLogSink()
    {
    }

    /// <summary>
    /// Gets the shared sink that discards all entries.
    /// </summary>
    public static NullExtensionHostLogSink Instance { get; } = new();

    /// <inheritdoc />
    public void Write(ExtensionHostLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
    }
}