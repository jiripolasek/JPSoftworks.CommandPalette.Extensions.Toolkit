// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions;

/// <summary>
/// Forwards extension host diagnostics to a Microsoft.Extensions.Logging logger.
/// </summary>
public sealed class MicrosoftLoggerExtensionHostLogSink : IExtensionHostLogSink
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a sink that writes to <paramref name="logger"/>.
    /// </summary>
    /// <param name="logger">The target Microsoft.Extensions.Logging logger.</param>
    public MicrosoftLoggerExtensionHostLogSink(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        this._logger = logger;
    }

    /// <inheritdoc />
    public void Write(ExtensionHostLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (MicrosoftLoggingBridgeMarker.IsMarked(entry))
        {
            return;
        }

        var state = new ExtensionHostMicrosoftLogState(entry.Category, entry.Message);
        this._logger.Log(
            ToMicrosoftLogLevel(entry.Level),
            new EventId(entry.EventId),
            state,
            entry.Exception,
            static (logState, _) => logState.ToString());
    }

    private static LogLevel ToMicrosoftLogLevel(ExtensionHostLogLevel level)
    {
        return level switch
        {
            ExtensionHostLogLevel.Debug => LogLevel.Debug,
            ExtensionHostLogLevel.Information => LogLevel.Information,
            ExtensionHostLogLevel.Warning => LogLevel.Warning,
            ExtensionHostLogLevel.Error => LogLevel.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown extension host log level."),
        };
    }
}