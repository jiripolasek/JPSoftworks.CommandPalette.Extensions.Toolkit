// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Forwards extension host diagnostics to the Command Palette host.
/// </summary>
public sealed class CommandPaletteExtensionHostLogSink : IExtensionHostLogSink
{
    private CommandPaletteExtensionHostLogSink()
    {
    }

    /// <summary>
    /// Gets the shared stateless sink instance.
    /// </summary>
    public static CommandPaletteExtensionHostLogSink Instance { get; } = new();

    /// <inheritdoc />
    public void Write(ExtensionHostLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var state = entry.Level switch
        {
            ExtensionHostLogLevel.Warning => MessageState.Warning,
            ExtensionHostLogLevel.Error => MessageState.Error,
            _ => MessageState.Info,
        };

        ExtensionHost.LogMessage(new LogMessage(entry.Message) { State = state });
    }
}
