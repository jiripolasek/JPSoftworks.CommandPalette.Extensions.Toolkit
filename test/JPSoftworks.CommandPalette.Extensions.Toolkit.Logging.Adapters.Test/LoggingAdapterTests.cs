// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Globalization;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Adapters.Test;

public sealed class LoggingAdapterTests
{
    [Fact]
    public void MicrosoftSinkForwardsExtensionHostEntry()
    {
        var logger = new RecordingMicrosoftLogger();
        var exception = new InvalidOperationException("Test exception");
        var sink = new MicrosoftLoggerExtensionHostLogSink(logger);

        sink.Write(CreateEntry(exception));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(42, entry.EventId.Id);
        Assert.Equal("TestCategory: Test message", entry.Message);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public void MicrosoftProviderForwardsApplicationEntry()
    {
        ExtensionHostLogEntry? receivedEntry = null;
        var exception = new InvalidOperationException("Test exception");
        var sink = new DelegateExtensionHostLogSink(entry => receivedEntry = entry);
        using var provider = new ExtensionHostLoggerProvider(sink);
        var logger = provider.CreateLogger("CoreService");

        logger.LogCritical(new EventId(17), exception, "Failed item {ItemId}", 42);

        Assert.NotNull(receivedEntry);
        Assert.Equal(ExtensionHostLogLevel.Error, receivedEntry.Level);
        Assert.Equal("CoreService", receivedEntry.Category);
        Assert.Equal(17, receivedEntry.EventId);
        Assert.Equal("Failed item 42", receivedEntry.Message);
        Assert.Same(exception, receivedEntry.Exception);
    }

    [Fact]
    public void MicrosoftAdaptersAvoidBidirectionalFeedback()
    {
        var hostEntries = new List<ExtensionHostLogEntry>();
        IExtensionHostLogSink? hostSink = null;
        using var provider = new ExtensionHostLoggerProvider(
            new DelegateExtensionHostLogSink(entry => hostSink!.Write(entry)));
        var applicationLogger = provider.CreateLogger("CoreService");
        var relayingLogger = new RelayingMicrosoftLogger(applicationLogger);
        hostSink = new CompositeExtensionHostLogSink(
        [
            new DelegateExtensionHostLogSink(hostEntries.Add),
            new MicrosoftLoggerExtensionHostLogSink(relayingLogger),
        ]);

        applicationLogger.LogInformation("Application message");

        Assert.Single(hostEntries);
        Assert.Empty(relayingLogger.Entries);

        hostSink.Write(CreateEntry(new InvalidOperationException("Test exception")));

        Assert.Equal(2, hostEntries.Count);
        Assert.Single(relayingLogger.Entries);
    }

    [Fact]
    public void SerilogSinkForwardsExtensionHostEntry()
    {
        var recordingSink = new RecordingSerilogSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(recordingSink)
            .CreateLogger();
        var exception = new InvalidOperationException("Test exception");
        var sink = new SerilogExtensionHostLogSink(logger);

        sink.Write(CreateEntry(exception));

        var logEvent = Assert.Single(recordingSink.Events);
        Assert.Equal(LogEventLevel.Warning, logEvent.Level);
        Assert.Equal("Test message", logEvent.RenderMessage(CultureInfo.InvariantCulture));
        Assert.Equal("TestCategory", GetScalarValue<string>(logEvent, Constants.SourceContextPropertyName));
        Assert.Equal(42, GetScalarValue<int>(logEvent, "EventId"));
        Assert.Same(exception, logEvent.Exception);
    }

    [Fact]
    public void SerilogAdapterForwardsApplicationEntry()
    {
        ExtensionHostLogEntry? receivedEntry = null;
        var extensionHostSink = new DelegateExtensionHostLogSink(entry => receivedEntry = entry);
        var sink = new ExtensionHostSerilogSink(extensionHostSink);
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        var exception = new InvalidOperationException("Test exception");

        logger
            .ForContext(Constants.SourceContextPropertyName, "CoreService")
            .ForContext("EventId", 17)
            .Fatal(exception, "Failed item {ItemId}", 42);

        Assert.NotNull(receivedEntry);
        Assert.Equal(ExtensionHostLogLevel.Error, receivedEntry.Level);
        Assert.Equal("CoreService", receivedEntry.Category);
        Assert.Equal(17, receivedEntry.EventId);
        Assert.Equal("Failed item 42", receivedEntry.Message);
        Assert.Same(exception, receivedEntry.Exception);
    }

    [Fact]
    public void SerilogAdaptersAvoidBidirectionalFeedback()
    {
        var hostEntries = new List<ExtensionHostLogEntry>();
        IExtensionHostLogSink? hostSink = null;
        var recordingSink = new RecordingSerilogSink();
        var extensionHostSink = new ExtensionHostSerilogSink(
            new DelegateExtensionHostLogSink(entry => hostSink!.Write(entry)));
        using var logger = new LoggerConfiguration()
            .WriteTo.Sink(recordingSink)
            .WriteTo.Sink(extensionHostSink)
            .CreateLogger();
        hostSink = new CompositeExtensionHostLogSink(
        [
            new DelegateExtensionHostLogSink(hostEntries.Add),
            new SerilogExtensionHostLogSink(logger),
        ]);

        logger.Information("Application message");

        Assert.Single(hostEntries);
        Assert.Single(recordingSink.Events);

        hostSink.Write(CreateEntry(new InvalidOperationException("Test exception")));

        Assert.Equal(2, hostEntries.Count);
        Assert.Equal(2, recordingSink.Events.Count);
    }

    private static ExtensionHostLogEntry CreateEntry(Exception exception)
    {
        return new ExtensionHostLogEntry(
            new DateTimeOffset(2026, 7, 28, 12, 34, 56, TimeSpan.FromHours(2)),
            ExtensionHostLogLevel.Warning,
            "TestCategory",
            42,
            "Test message",
            exception);
    }

    private static T GetScalarValue<T>(LogEvent logEvent, string propertyName)
    {
        var value = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
        return Assert.IsType<T>(value.Value);
    }

    private sealed class RecordingMicrosoftLogger : Microsoft.Extensions.Logging.ILogger
    {
        public List<MicrosoftLogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            this.Entries.Add(new MicrosoftLogEntry(logLevel, eventId, formatter(state, exception), exception));
        }
    }

    private sealed class RelayingMicrosoftLogger(Microsoft.Extensions.Logging.ILogger target)
        : Microsoft.Extensions.Logging.ILogger
    {
        private readonly Microsoft.Extensions.Logging.ILogger _target = target;

        public List<MicrosoftLogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return this._target.IsEnabled(logLevel);
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            this.Entries.Add(new MicrosoftLogEntry(logLevel, eventId, formatter(state, exception), exception));
            this._target.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    private sealed class RecordingSerilogSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            this.Events.Add(logEvent);
        }
    }

    private sealed record MicrosoftLogEntry(
        LogLevel Level,
        EventId EventId,
        string Message,
        Exception? Exception);
}