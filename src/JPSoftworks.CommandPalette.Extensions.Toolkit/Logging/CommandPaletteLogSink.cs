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
public sealed class CommandPaletteLogSink : IExtensionLogSink
{
    private CommandPaletteLogSink()
    {
    }

    /// <summary>
    /// Gets the shared stateless sink instance.
    /// </summary>
    public static CommandPaletteLogSink Instance { get; } = new();

    /// <inheritdoc />
    public void Write(ExtensionLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var state = entry.Level switch
        {
            ExtensionLogLevel.Warning => MessageState.Warning,
            ExtensionLogLevel.Error => MessageState.Error,
            _ => MessageState.Info,
        };

        ExtensionHost.LogMessage(new LogMessage(entry.Message) { State = state });
    }
}
