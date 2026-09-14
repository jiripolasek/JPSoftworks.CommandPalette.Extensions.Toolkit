// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Globalization;
using JPSoftworks.CommandPalette.Extensions.Toolkit;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
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
    public void MicrosoftFactorySinkPreservesExtensionHostCategory()
    {
        using var loggerFactory = new RecordingMicrosoftLoggerFactory();
        var exception = new InvalidOperationException("Test exception");
        var sink = new MicrosoftLoggerExtensionHostLogSink(loggerFactory);

        sink.Write(CreateEntry(exception));

        var logger = Assert.Single(loggerFactory.Loggers);
        Assert.Equal("TestCategory", logger.Key);
        var entry = Assert.Single(logger.Value.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(42, entry.EventId.Id);
        Assert.Equal("Test message", entry.Message);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public void UseMicrosoftExtensionsLoggingOnlyForwardsHostDiagnostics()
    {
        var builder = new RecordingHostLoggingBuilder();
        using var loggerFactory = new RecordingMicrosoftLoggerFactory();

        var result = builder.UseMicrosoftExtensionsLogging(loggerFactory);

        Assert.Same(builder, result);
        Assert.True(builder.DefaultLogSinksCleared);
        Assert.IsType<MicrosoftLoggerExtensionHostLogSink>(Assert.Single(builder.Sinks));
        Assert.Empty(loggerFactory.Providers);
    }

    [Fact]
    public void LoggerFactoryLeavesRegisteredProviderInstancesCallerOwned()
    {
        var provider = new RecordingDisposableLoggerProvider();

        using (LoggerFactory.Create(builder => builder.AddProvider(provider)))
        {
        }

        Assert.False(provider.IsDisposed);

        provider.Dispose();

        Assert.True(provider.IsDisposed);
    }

    [Fact]
    public void UseSerilogOnlyForwardsHostDiagnostics()
    {
        var builder = new RecordingHostLoggingBuilder();
        using var logger = new LoggerConfiguration().CreateLogger();

        var result = builder.UseSerilog(logger);

        Assert.Same(builder, result);
        Assert.True(builder.DefaultLogSinksCleared);
        Assert.IsType<SerilogExtensionHostLogSink>(Assert.Single(builder.Sinks));
    }

    [Fact]
    public void CommandPaletteSerilogConfigurationAddsDestination()
    {
        using var logger = new LoggerConfiguration()
            .WriteTo.CommandPalette()
            .CreateLogger();

        Assert.NotNull(logger);
    }

    [Fact]
    public void MicrosoftDestinationsApplyResolvedHostMinimumLevel()
    {
        using var debugFactory = LoggerFactory.Create(
            builder => builder.AddCommandPalette(
                CreateConfiguration($"Debug-{Guid.NewGuid():N}", isDebug: true)));
        using var releaseFactory = LoggerFactory.Create(
            builder => builder.AddCommandPalette(
                CreateConfiguration($"Release-{Guid.NewGuid():N}", isDebug: false)));

        Assert.True(debugFactory.CreateLogger("Application").IsEnabled(LogLevel.Debug));
        Assert.False(releaseFactory.CreateLogger("Application").IsEnabled(LogLevel.Debug));
        Assert.True(releaseFactory.CreateLogger("Application").IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void MicrosoftDestinationFiltersCanLowerResolvedHostMinimumLevel()
    {
        var configuration = CreateConfiguration(
            $"FilterOverride-{Guid.NewGuid():N}",
            isDebug: false);
        using var commandPaletteFactory = LoggerFactory.Create(
            builder => builder
                .AddCommandPalette(configuration)
                .AddFilter<CommandPaletteLoggerProvider>(
                    static (_, level) => level >= LogLevel.Trace));
        using var dailyFileFactory = LoggerFactory.Create(
            builder => builder
                .AddDailyFile(configuration)
                .AddFilter<DailyFileLoggerProvider>(
                    static (_, level) => level >= LogLevel.Trace));

        Assert.True(commandPaletteFactory.CreateLogger("Application").IsEnabled(LogLevel.Trace));
        Assert.True(dailyFileFactory.CreateLogger("Application").IsEnabled(LogLevel.Trace));
    }

    [Fact]
    public void SerilogMinimumLevelUsesResolvedHostPolicy()
    {
        var recordingSink = new RecordingSerilogSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.FromExtensionHost(
                CreateConfiguration($"SerilogDebug-{Guid.NewGuid():N}", isDebug: true))
            .WriteTo.Sink(recordingSink)
            .CreateLogger();

        logger.Debug("Debug message");

        Assert.Equal(LogEventLevel.Debug, Assert.Single(recordingSink.Events).Level);
    }

    [Fact]
    public void MicrosoftProviderCoreForwardsApplicationEntry()
    {
        ExtensionHostLogEntry? receivedEntry = null;
        var exception = new InvalidOperationException("Test exception");
        var sink = new CallbackLogSink(entry => receivedEntry = entry);
        using var provider = new ExtensionHostLoggerProviderCore(sink);
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
    public void MicrosoftProviderCoreAppliesMinimumLevel()
    {
        var receivedEntries = new List<ExtensionHostLogEntry>();
        var sink = new CallbackLogSink(receivedEntries.Add);
        using var provider = new ExtensionHostLoggerProviderCore(sink, LogLevel.Information);
        var logger = provider.CreateLogger("CoreService");

        logger.LogDebug("Debug message");
        logger.LogInformation("Information message");

        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Information));
        var entry = Assert.Single(receivedEntries);
        Assert.Equal(ExtensionHostLogLevel.Information, entry.Level);
        Assert.Equal("Information message", entry.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void MicrosoftProvidersRejectUnknownMinimumLevel(int value)
    {
        var coreException = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ExtensionHostLoggerProviderCore(
                new CallbackLogSink(_ => { }),
                (LogLevel)value));
        var commandPaletteException = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CommandPaletteLoggerProvider((LogLevel)value));

        Assert.Equal("minimumLevel", coreException.ParamName);
        Assert.Equal("minimumLevel", commandPaletteException.ParamName);
    }

    [Fact]
    public void MicrosoftProviderCoreReceivesForwardedHostEntry()
    {
        var receivedEntries = new List<ExtensionHostLogEntry>();
        using var provider = new ExtensionHostLoggerProviderCore(
            new CallbackLogSink(receivedEntries.Add));
        using var loggerFactory = new ProviderMicrosoftLoggerFactory(provider);
        var hostSink = new MicrosoftLoggerExtensionHostLogSink(loggerFactory);
        var exception = new InvalidOperationException("Test exception");

        hostSink.Write(CreateEntry(exception));

        var entry = Assert.Single(receivedEntries);
        Assert.Equal(ExtensionHostLogLevel.Warning, entry.Level);
        Assert.Equal("TestCategory", entry.Category);
        Assert.Equal(42, entry.EventId);
        Assert.Equal("Test message", entry.Message);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public void DailyFileProviderWritesAndOwnsItsDestination()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"CmdPalToolkitLoggingTest-{Guid.NewGuid():N}");
        var baseFilePath = Path.Combine(directoryPath, "log.txt");

        try
        {
            using (var provider = new DailyFileLoggerProvider(baseFilePath))
            {
                var logger = provider.CreateLogger("CoreService");
                logger.LogCritical(new EventId(17), "Failed item {ItemId}", 42);
            }

            var dailyFilePath = Path.Combine(
                directoryPath,
                $"log{DateTime.Now:yyyyMMdd}.txt");
            var contents = File.ReadAllText(dailyFilePath);

            Assert.Contains(
                "[ERR] CoreService: Failed item 42",
                contents,
                StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }

    [Fact]
    public void MicrosoftDailyFileDestinationUsesCanonicalPathAndIsFactoryOwned()
    {
        var productMoniker = $"LoggingAdapterMicrosoft-{Guid.NewGuid():N}";
        var directoryPath = GetLogDirectoryPath(productMoniker);

        try
        {
            var configuration = CreateConfiguration(productMoniker);
            using (var loggerFactory = LoggerFactory.Create(
                       builder => builder.AddDailyFile(configuration)))
            {
                loggerFactory
                    .CreateLogger("CoreService")
                    .LogInformation("Microsoft destination message");
            }

            var contents = File.ReadAllText(GetDailyLogFilePath(directoryPath));
            Assert.Contains(
                "[INF] CoreService: Microsoft destination message",
                contents,
                StringComparison.Ordinal);

            Directory.Delete(directoryPath, recursive: true);
            Assert.False(Directory.Exists(directoryPath));
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }

    [Fact]
    public void SerilogDailyFileDestinationUsesCanonicalPathAndIsLoggerOwned()
    {
        var productMoniker = $"LoggingAdapterSerilog-{Guid.NewGuid():N}";
        var directoryPath = GetLogDirectoryPath(productMoniker);

        try
        {
            var configuration = CreateConfiguration(productMoniker);
            using (var logger = new LoggerConfiguration()
                       .MinimumLevel.FromExtensionHost(configuration)
                       .WriteTo.DailyFile(configuration)
                       .CreateLogger())
            {
                logger.ForContext(Constants.SourceContextPropertyName, "CoreService")
                    .Information("Serilog destination message");
            }

            var contents = File.ReadAllText(GetDailyLogFilePath(directoryPath));
            Assert.Contains(
                "[INF] CoreService: Serilog destination message",
                contents,
                StringComparison.Ordinal);

            Directory.Delete(directoryPath, recursive: true);
            Assert.False(Directory.Exists(directoryPath));
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
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
        var extensionHostSink = new CallbackLogSink(entry => receivedEntry = entry);
        var sink = new ExtensionHostSerilogSinkCore(extensionHostSink);
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
        var extensionHostSink = new ExtensionHostSerilogSinkCore(
            new CallbackLogSink(entry => hostSink!.Write(entry)));
        using var logger = new LoggerConfiguration()
            .WriteTo.Sink(recordingSink)
            .WriteTo.Sink(extensionHostSink)
            .CreateLogger();
        hostSink = new CompositeLogSink(
        [
            new CallbackLogSink(hostEntries.Add),
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

    private static ExtensionHostConfiguration CreateConfiguration(
        string productMoniker,
        bool isDebug = false)
    {
        return ExtensionHostConfiguration.Resolve(
            [],
            new ExtensionHostRunnerParameters
            {
                PublisherMoniker = "JPSoftworks",
                ProductMoniker = productMoniker,
                IsDebug = isDebug,
            });
    }

    private static string GetLogDirectoryPath(string productMoniker)
    {
        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify),
            "JPSoftworks",
            productMoniker);
    }

    private static string GetDailyLogFilePath(string directoryPath)
    {
        return Path.Combine(
            directoryPath,
            $"log{DateTime.Now:yyyyMMdd}.txt");
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

    private sealed class RecordingMicrosoftLoggerFactory : ILoggerFactory
    {
        public Dictionary<string, RecordingMicrosoftLogger> Loggers { get; } = [];

        public List<ILoggerProvider> Providers { get; } = [];

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            return this.Loggers.TryGetValue(categoryName, out var logger)
                ? logger
                : this.Loggers[categoryName] = new();
        }

        public void AddProvider(ILoggerProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            this.Providers.Add(provider);
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingHostLoggingBuilder : IExtensionHostLoggingBuilder
    {
        public bool DefaultLogSinksCleared { get; private set; }

        public List<IExtensionHostLogSink> Sinks { get; } = [];

        public void AddHostLogSink(IExtensionHostLogSink sink)
        {
            this.Sinks.Add(sink);
        }

        public void ClearDefaultLogSinks()
        {
            this.DefaultLogSinksCleared = true;
        }
    }

    private sealed class ProviderMicrosoftLoggerFactory(ILoggerProvider provider) : ILoggerFactory
    {
        private readonly ILoggerProvider _provider = provider;

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            return this._provider.CreateLogger(categoryName);
        }

        public void AddProvider(ILoggerProvider addedProvider)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingDisposableLoggerProvider : ILoggerProvider
    {
        public bool IsDisposed { get; private set; }

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            return Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        }

        public void Dispose()
        {
            this.IsDisposed = true;
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

    private sealed class CallbackLogSink(Action<ExtensionHostLogEntry> write) : IExtensionHostLogSink
    {
        private readonly Action<ExtensionHostLogEntry> _write = write;

        public void Write(ExtensionHostLogEntry entry)
        {
            this._write(entry);
        }
    }

    private sealed class CompositeLogSink(IEnumerable<IExtensionHostLogSink> sinks) : IExtensionHostLogSink
    {
        private readonly IExtensionHostLogSink[] _sinks = sinks.ToArray();

        public void Write(ExtensionHostLogEntry entry)
        {
            foreach (var sink in this._sinks)
            {
                sink.Write(entry);
            }
        }
    }

    private sealed record MicrosoftLogEntry(
        LogLevel Level,
        EventId EventId,
        string Message,
        Exception? Exception);
}
