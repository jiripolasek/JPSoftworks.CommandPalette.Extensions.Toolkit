// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
using Serilog.Core;
using Serilog.Events;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog;

/// <summary>
/// Forwards extension host diagnostics to a Serilog logger.
/// </summary>
public sealed class SerilogLogSink : IExtensionLogSink
{
    private const string EventIdPropertyName = "EventId";
    private readonly global::Serilog.ILogger _logger;

    /// <summary>
    /// Initializes a sink that writes to <paramref name="logger"/>.
    /// </summary>
    /// <param name="logger">The target Serilog logger.</param>
    public SerilogLogSink(global::Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        this._logger = logger;
    }

    /// <inheritdoc />
    public void Write(ExtensionLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (SerilogLoggingBridgeMarker.IsMarked(entry))
        {
            return;
        }

        this._logger
            .ForContext(Constants.SourceContextPropertyName, entry.Category)
            .ForContext(EventIdPropertyName, entry.EventId)
            .ForContext(SerilogLoggingBridgeMarker.PropertyName, true)
            .Write(
                ToSerilogLevel(entry.Level),
                entry.Exception,
                "{ExtensionHostMessage:l}",
                entry.Message);
    }

    private static LogEventLevel ToSerilogLevel(ExtensionLogLevel level)
    {
        return level switch
        {
            ExtensionLogLevel.Debug => LogEventLevel.Debug,
            ExtensionLogLevel.Information => LogEventLevel.Information,
            ExtensionLogLevel.Warning => LogEventLevel.Warning,
            ExtensionLogLevel.Error => LogEventLevel.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown extension host log level."),
        };
    }
}
