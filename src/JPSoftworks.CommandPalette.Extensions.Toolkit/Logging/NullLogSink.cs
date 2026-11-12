// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Discards all log entries.
/// </summary>
public sealed class NullLogSink : IExtensionLogSink
{
    private NullLogSink()
    {
    }

    /// <summary>
    /// Gets the shared sink that discards all entries.
    /// </summary>
    public static NullLogSink Instance { get; } = new();

    /// <inheritdoc />
    public void Write(ExtensionLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
    }
}
