// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Provides simple process-wide logging for extensions.
/// </summary>
/// <remarks>
/// When an <see cref="ExtensionHostRunner" /> is active, entries are forwarded to its effective log sink.
/// Calling <see cref="Initialize" /> directly configures the daily log file. Without either, entries go to
/// <see cref="System.Diagnostics.Trace" />.
/// </remarks>
public static class Logger
{
    /// <summary>
    /// Initializes the compatibility logger with the daily log file.
    /// </summary>
    public static void Initialize(string publisherName, string productName, bool isDebug = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publisherName);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);

        LegacyLoggerBridge.Initialize(publisherName, productName, isDebug);
    }

    /// <summary>
    /// Logs a debug message when debug logging is enabled.
    /// </summary>
    public static void LogDebug(string message)
    {
        LegacyLoggerBridge.Write(ExtensionLogLevel.Debug, message);
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public static void LogInformation(string message)
    {
        LegacyLoggerBridge.Write(ExtensionLogLevel.Information, message);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public static void LogWarning(string message)
    {
        LegacyLoggerBridge.Write(ExtensionLogLevel.Warning, message);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public static void LogError(string message)
    {
        LegacyLoggerBridge.Write(ExtensionLogLevel.Error, message);
    }

    /// <summary>
    /// Logs an exception as an error.
    /// </summary>
    public static void LogError(Exception exception)
    {
        LegacyLoggerBridge.Write(
            ExtensionLogLevel.Error,
            $"{exception.GetType().Name}: {exception.Message}",
            exception);
    }

    /// <summary>
    /// Logs an error message and its associated exception.
    /// </summary>
    public static void LogError(string message, Exception exception)
    {
        LegacyLoggerBridge.Write(
            ExtensionLogLevel.Error,
            $"{message}: {exception.Message}",
            exception);
    }

    /// <summary>
    /// Flushes and closes resources owned by the compatibility logger.
    /// </summary>
    public static void CloseAndFlush()
    {
        LegacyLoggerBridge.CloseAndFlush();
    }
}

internal static class LegacyLoggerBridge
{
    private const string Category = "Logger";

    private static readonly Lock SyncRoot = new();

    [ThreadStatic]
    private static bool _isWriting;

    private static IExtensionLogSink? _sink;
    private static IDisposable? _ownedResource;
    private static bool _isDebugEnabled;

    internal static void Initialize(string publisherName, string productName, bool isDebug)
    {
        try
        {
            var localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify);
            var logFilePath = Path.Combine(localAppData, publisherName, productName, "log.txt");
            var fileSink = new DailyFileLogSink(logFilePath);

            SetSink(fileSink, fileSink, isDebug);
            Write(ExtensionLogLevel.Debug, "Logger initialized");
        }
        catch (Exception ex)
        {
            Write(
                ExtensionLogLevel.Error,
                $"{ex.GetType().Name}: {ex.Message}",
                ex);
        }
    }

    internal static void UseSink(IExtensionLogSink sink, bool isDebug)
    {
        ArgumentNullException.ThrowIfNull(sink);
        SetSink(sink, null, isDebug);
    }

    internal static void Write(
        ExtensionLogLevel level,
        string message,
        Exception? exception = null)
    {
        if (_isWriting)
        {
            return;
        }

        IExtensionLogSink? sink;
        bool isDebugEnabled;

        lock (SyncRoot)
        {
            sink = _sink;
            isDebugEnabled = _isDebugEnabled;
        }

        if (level == ExtensionLogLevel.Debug && !isDebugEnabled)
        {
#if DEBUG
            TryWrite(TraceLogSink.Instance, level, message, exception);
#endif
            return;
        }

        TryWrite(sink ?? TraceLogSink.Instance, level, message, exception);
    }

    internal static void CloseAndFlush()
    {
        IDisposable? ownedResource;

        lock (SyncRoot)
        {
            _sink = null;
            _isDebugEnabled = false;
            ownedResource = _ownedResource;
            _ownedResource = null;
        }

        TryDispose(ownedResource);
    }

    private static void SetSink(
        IExtensionLogSink sink,
        IDisposable? ownedResource,
        bool isDebug)
    {
        IDisposable? previousOwnedResource;

        lock (SyncRoot)
        {
            previousOwnedResource = _ownedResource;
            _sink = sink;
            _ownedResource = ownedResource;
            _isDebugEnabled = isDebug;
        }

        TryDispose(previousOwnedResource);
    }

    private static void TryWrite(
        IExtensionLogSink sink,
        ExtensionLogLevel level,
        string message,
        Exception? exception)
    {
        try
        {
            _isWriting = true;
            sink.Write(new ExtensionLogEntry(
                DateTimeOffset.Now,
                level,
                Category,
                0,
                message,
                exception));
        }
        catch
        {
            // Compatibility logging must never interrupt the extension host.
        }
        finally
        {
            _isWriting = false;
        }
    }

    private static void TryDispose(IDisposable? resource)
    {
        try
        {
            resource?.Dispose();
        }
        catch
        {
            // Logging shutdown must never interrupt the extension host.
        }
    }
}
