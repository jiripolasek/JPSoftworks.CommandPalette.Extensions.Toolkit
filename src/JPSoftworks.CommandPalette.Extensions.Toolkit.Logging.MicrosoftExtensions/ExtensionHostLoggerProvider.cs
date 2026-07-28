// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions;

/// <summary>
/// Creates Microsoft.Extensions.Logging loggers that forward application diagnostics to an extension host sink.
/// </summary>
/// <remarks>
/// The supplied extension host sink is caller-owned and is not disposed by this provider.
/// Microsoft trace events are represented as extension host debug events, and critical events as error events.
/// </remarks>
public sealed class ExtensionHostLoggerProvider : ILoggerProvider
{
    private readonly IExtensionHostLogSink _sink;

    /// <summary>
    /// Initializes a provider that writes to <paramref name="sink"/>.
    /// </summary>
    /// <param name="sink">The target extension host sink.</param>
    public ExtensionHostLoggerProvider(IExtensionHostLogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        this._sink = sink;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return new ExtensionHostLogger(categoryName, this._sink);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class ExtensionHostLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly IExtensionHostLogSink _sink;

        public ExtensionHostLogger(string categoryName, IExtensionHostLogSink sink)
        {
            this._categoryName = categoryName;
            this._sink = sink;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (!this.IsEnabled(logLevel))
            {
                return;
            }

            if (state is ExtensionHostMicrosoftLogState)
            {
                return;
            }

            try
            {
                var entry = new ExtensionHostLogEntry(
                    DateTimeOffset.Now,
                    ToExtensionHostLogLevel(logLevel),
                    this._categoryName,
                    eventId.Id,
                    formatter(state, exception),
                    exception);
                MicrosoftLoggingBridgeMarker.Mark(entry);
                this._sink.Write(entry);
            }
            catch
            {
                // Logging must never interrupt the application.
            }
        }

        private static ExtensionHostLogLevel ToExtensionHostLogLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace or LogLevel.Debug => ExtensionHostLogLevel.Debug,
                LogLevel.Information => ExtensionHostLogLevel.Information,
                LogLevel.Warning => ExtensionHostLogLevel.Warning,
                LogLevel.Error or LogLevel.Critical => ExtensionHostLogLevel.Error,
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown Microsoft log level."),
            };
        }
    }

    private sealed class NullScope : IDisposable
    {
        private NullScope()
        {
        }

        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
