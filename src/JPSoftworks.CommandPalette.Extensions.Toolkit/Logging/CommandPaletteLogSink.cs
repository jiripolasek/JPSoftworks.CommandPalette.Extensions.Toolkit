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
/// Forwards entries to the Command Palette log through the Command Palette SDK's <c>IExtensionHost.LogMessage</c>.
/// </summary>
/// <remarks>
/// Each entry allocates a WinRT log message object and makes a cross-process COM call, and Command Palette then calls
/// back into the extension to read the message. The runner does not add this sink by default; add it explicitly only
/// for low-volume entries that should be visible in Command Palette. Entries are discarded until the extension's command
/// provider receives the Command Palette host.
/// </remarks>
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
            ExtensionLogLevel.Error or ExtensionLogLevel.Critical => MessageState.Error,
            _ => MessageState.Info,
        };

        ExtensionHost.LogMessage(new LogMessage(entry.Message) { State = state });
    }
}
