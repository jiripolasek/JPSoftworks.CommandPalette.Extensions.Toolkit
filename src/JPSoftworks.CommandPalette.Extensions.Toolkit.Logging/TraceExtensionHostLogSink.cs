// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Diagnostics;
using System.Globalization;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Writes extension host diagnostics through <see cref="Trace"/>.
/// </summary>
public sealed class TraceExtensionHostLogSink : IExtensionHostLogSink
{
    private TraceExtensionHostLogSink()
    {
    }

    /// <summary>
    /// Gets the shared trace sink.
    /// </summary>
    public static TraceExtensionHostLogSink Instance { get; } = new();

    /// <inheritdoc />
    public void Write(ExtensionHostLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var message = string.Format(
            CultureInfo.InvariantCulture,
            "{0:O} [{1}] {2}: {3}",
            entry.Timestamp,
            GetLevelName(entry.Level),
            entry.Category,
            entry.Message);

        if (entry.Exception != null)
        {
            message = string.Concat(message, Environment.NewLine, entry.Exception);
        }

        Trace.WriteLine(message);
    }

    private static string GetLevelName(ExtensionHostLogLevel level)
    {
        return level switch
        {
            ExtensionHostLogLevel.Debug => "DBG",
            ExtensionHostLogLevel.Information => "INF",
            ExtensionHostLogLevel.Warning => "WRN",
            ExtensionHostLogLevel.Error => "ERR",
            _ => level.ToString(),
        };
    }
}