// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Diagnostics;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Test;

public sealed class LoggingSinkTests
{
    private static readonly object TraceSyncRoot = new();

    [Fact]
    public void DelegateSinkForwardsEntry()
    {
        ExtensionHostLogEntry? receivedEntry = null;
        var expectedEntry = CreateEntry();
        var sink = new DelegateExtensionHostLogSink(entry => receivedEntry = entry);

        sink.Write(expectedEntry);

        Assert.Same(expectedEntry, receivedEntry);
    }

    [Fact]
    public void CompositeSinkContinuesAfterSinkFailure()
    {
        ExtensionHostLogEntry? receivedEntry = null;
        var expectedEntry = CreateEntry();
        var sink = new CompositeExtensionHostLogSink(
        [
            new DelegateExtensionHostLogSink(_ => throw new InvalidOperationException("Expected test failure")),
            new DelegateExtensionHostLogSink(entry => receivedEntry = entry),
        ]);

        sink.Write(expectedEntry);

        Assert.Same(expectedEntry, receivedEntry);
    }

    [Fact]
    public void TraceSinkWritesFormattedEntry()
    {
        lock (TraceSyncRoot)
        {
            using var writer = new StringWriter();
            using var listener = new TextWriterTraceListener(writer);
            Trace.Listeners.Add(listener);

            try
            {
                TraceExtensionHostLogSink.Instance.Write(CreateEntry());
                Trace.Flush();

                var output = writer.ToString();
                Assert.Contains("[WRN]", output, StringComparison.Ordinal);
                Assert.Contains("TestCategory: Test message", output, StringComparison.Ordinal);
                Assert.Contains("Test exception", output, StringComparison.Ordinal);
            }
            finally
            {
                Trace.Listeners.Remove(listener);
            }
        }
    }

    [Fact]
    public void DailyFileSinkWritesExpectedFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CmdPalExtToolkit", Guid.NewGuid().ToString("N"));
        var baseFilePath = Path.Combine(directory, "log.txt");

        try
        {
            using (var sink = new DailyFileExtensionHostLogSink(baseFilePath))
            {
                sink.Write(CreateEntry());
            }

            var dailyFilePath = Path.Combine(directory, "log20260728.txt");
            var output = File.ReadAllText(dailyFilePath);
            Assert.Contains("[WRN] TestCategory: Test message", output, StringComparison.Ordinal);
            Assert.Contains("Test exception", output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static ExtensionHostLogEntry CreateEntry()
    {
        return new ExtensionHostLogEntry(
            new DateTimeOffset(2026, 7, 28, 12, 34, 56, TimeSpan.FromHours(2)),
            ExtensionHostLogLevel.Warning,
            "TestCategory",
            42,
            "Test message",
            new InvalidOperationException("Test exception"));
    }
}
