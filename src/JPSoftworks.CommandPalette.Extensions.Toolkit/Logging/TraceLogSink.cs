// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Diagnostics;
using System.Globalization;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Writes log entries through <see cref="Trace"/>.
/// </summary>
public sealed class TraceLogSink : IExtensionLogSink
{
    private TraceLogSink()
    {
    }

    /// <summary>
    /// Gets the shared trace sink.
    /// </summary>
    public static TraceLogSink Instance { get; } = new();

    /// <inheritdoc />
    public void Write(ExtensionLogEntry entry)
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

    private static string GetLevelName(ExtensionLogLevel level)
    {
        return level switch
        {
            ExtensionLogLevel.Debug => "DBG",
            ExtensionLogLevel.Information => "INF",
            ExtensionLogLevel.Warning => "WRN",
            ExtensionLogLevel.Error => "ERR",
            ExtensionLogLevel.Critical => "CRT",
            _ => level.ToString(),
        };
    }
}
