// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions;

internal sealed class ExtensionHostLoggerProviderCore : ILoggerProvider
{
    private readonly IExtensionHostLogSink _sink;
    private readonly LogLevel _minimumLevel;

    public ExtensionHostLoggerProviderCore(IExtensionHostLogSink sink)
        : this(sink, LogLevel.Trace)
    {
    }

    public ExtensionHostLoggerProviderCore(IExtensionHostLogSink sink, LogLevel minimumLevel)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (minimumLevel is < LogLevel.Trace or > LogLevel.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumLevel),
                minimumLevel,
                "The minimum level must be a defined Microsoft logging level.");
        }

        this._sink = sink;
        this._minimumLevel = minimumLevel;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return new ExtensionHostLogger(categoryName, this._sink, this._minimumLevel);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class ExtensionHostLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly IExtensionHostLogSink _sink;
        private readonly LogLevel _minimumLevel;

        public ExtensionHostLogger(string categoryName, IExtensionHostLogSink sink, LogLevel minimumLevel)
        {
            this._categoryName = categoryName;
            this._sink = sink;
            this._minimumLevel = minimumLevel;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= this._minimumLevel && logLevel < LogLevel.None;
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
