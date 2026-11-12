// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Test;

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
        List<ExtensionHostLogEntry> entries = [];
        var exception = new InvalidOperationException("Failure");

        LegacyLoggerBridge.UseSink(new DelegateExtensionHostLogSink(entries.Add), isDebug: true);

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
            entry => AssertEntry(entry, ExtensionHostLogLevel.Debug, "Debug"),
            entry => AssertEntry(entry, ExtensionHostLogLevel.Information, "Information"),
            entry => AssertEntry(entry, ExtensionHostLogLevel.Warning, "Warning"),
            entry => AssertEntry(entry, ExtensionHostLogLevel.Error, "Error"),
            entry =>
            {
                AssertEntry(entry, ExtensionHostLogLevel.Error, "InvalidOperationException: Failure");
                Assert.Same(exception, entry.Exception);
            },
            entry =>
            {
                AssertEntry(entry, ExtensionHostLogLevel.Error, "Operation failed: Failure");
                Assert.Same(exception, entry.Exception);
            });
    }

    [Fact]
    public void LoggerSuppressesSinkFailures()
    {
        LegacyLoggerBridge.UseSink(
            new DelegateExtensionHostLogSink(_ => throw new InvalidOperationException()),
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
            new DelegateExtensionHostLogSink(_ =>
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

    private static void AssertEntry(
        ExtensionHostLogEntry entry,
        ExtensionHostLogLevel expectedLevel,
        string expectedMessage)
    {
        Assert.Equal(expectedLevel, entry.Level);
        Assert.Equal("Logger", entry.Category);
        Assert.Equal(expectedMessage, entry.Message);
    }
}
