// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions;

/// <summary>
/// Forwards extension host diagnostics to a Microsoft.Extensions.Logging logger.
/// </summary>
public sealed class MicrosoftLoggerExtensionHostLogSink : IExtensionHostLogSink
{
    private readonly ILogger? _logger;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ConcurrentDictionary<string, ILogger>? _loggers;

    /// <summary>
    /// Initializes a sink that writes to <paramref name="logger"/>.
    /// </summary>
    /// <param name="logger">The target Microsoft.Extensions.Logging logger.</param>
    public MicrosoftLoggerExtensionHostLogSink(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        this._logger = logger;
    }

    /// <summary>
    /// Initializes a sink that writes each host category to a matching logger created by
    /// <paramref name="loggerFactory"/>.
    /// </summary>
    /// <param name="loggerFactory">The target application-owned logger factory.</param>
    /// <remarks>The factory and the loggers it creates remain caller-owned.</remarks>
    public MicrosoftLoggerExtensionHostLogSink(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        this._loggerFactory = loggerFactory;
        this._loggers = new(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public void Write(ExtensionHostLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (MicrosoftLoggingBridgeMarker.IsMarked(entry))
        {
            return;
        }

        var preservesCategory = this._loggerFactory is not null;
        var logger = preservesCategory
            ? this._loggers!.GetOrAdd(entry.Category, this._loggerFactory!.CreateLogger)
            : this._logger!;
        var state = new ExtensionHostMicrosoftLogState(
            entry.Category,
            entry.Message,
            includeCategoryInMessage: !preservesCategory);
        logger.Log(
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
