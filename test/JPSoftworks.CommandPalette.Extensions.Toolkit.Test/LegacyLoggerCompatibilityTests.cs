// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Diagnostics;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Test;

// The legacy logger routes through process-wide state shared with the runner.
[Collection(nameof(LegacyLoggerBridge))]
public sealed class LegacyLoggerCompatibilityTests
{
    [Fact]
    public void LoggerRemainsSupported()
    {
        Assert.Empty(
            typeof(Logger).GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false));
    }

    [Fact]
    public void LoggerForwardsExistingMethodsToEffectiveSink()
    {
        List<ExtensionLogEntry> entries = [];
        var exception = new InvalidOperationException("Failure");

        LegacyLoggerBridge.UseSink(new DelegateLogSink(entries.Add), isDebug: true);

        try
        {
            Logger.LogDebug("Debug");
            Logger.LogInformation("Information");
            Logger.LogWarning("Warning");
            Logger.LogError("Error");
            Logger.LogError(exception);
            Logger.LogError("Operation failed", exception);
        }
        finally
        {
            LegacyLoggerBridge.CloseAndFlush();
        }

        Assert.Collection(
            entries,
            entry => AssertEntry(entry, ExtensionLogLevel.Debug, "Debug"),
            entry => AssertEntry(entry, ExtensionLogLevel.Information, "Information"),
            entry => AssertEntry(entry, ExtensionLogLevel.Warning, "Warning"),
            entry => AssertEntry(entry, ExtensionLogLevel.Error, "Error"),
            entry =>
            {
                AssertEntry(entry, ExtensionLogLevel.Error, "InvalidOperationException: Failure");
                Assert.Same(exception, entry.Exception);
            },
            entry =>
            {
                AssertEntry(entry, ExtensionLogLevel.Error, "Operation failed: Failure");
                Assert.Same(exception, entry.Exception);
            });
    }

    [Fact]
    public void LoggerSuppressesSinkFailures()
    {
        LegacyLoggerBridge.UseSink(
            new DelegateLogSink(_ => throw new InvalidOperationException()),
            isDebug: true);

        try
        {
            var exception = Record.Exception(() => Logger.LogInformation("Information"));

            Assert.Null(exception);
        }
        finally
        {
            LegacyLoggerBridge.CloseAndFlush();
        }
    }

    [Fact]
    public void LoggerStopsSinkFeedback()
    {
        var sinkInvocations = 0;

        LegacyLoggerBridge.UseSink(
            new DelegateLogSink(_ =>
            {
                sinkInvocations++;
                Logger.LogInformation("Relayed");
            }),
            isDebug: true);

        try
        {
            Logger.LogInformation("Original");
        }
        finally
        {
            LegacyLoggerBridge.CloseAndFlush();
        }

        Assert.Equal(1, sinkInvocations);
    }

    [Fact]
    public void LoggerWithoutSinkWritesToTrace()
    {
        using var writer = new StringWriter();
        using var listener = new TextWriterTraceListener(writer);
        LegacyLoggerBridge.CloseAndFlush();
        Trace.Listeners.Add(listener);

        try
        {
            Logger.LogWarning("Without sink");
            Trace.Flush();
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        Assert.Contains("Logger: Without sink", writer.ToString(), StringComparison.Ordinal);
    }

    private static void AssertEntry(
        ExtensionLogEntry entry,
        ExtensionLogLevel expectedLevel,
        string expectedMessage)
    {
        Assert.Equal(expectedLevel, entry.Level);
        Assert.Equal("Logger", entry.Category);
        Assert.Equal(expectedMessage, entry.Message);
    }
}
