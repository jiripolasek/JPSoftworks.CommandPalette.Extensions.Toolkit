// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Provides the original static logging API retained for compatibility.
/// </summary>
/// <remarks>
/// When an <see cref="ExtensionHostRunner" /> is active, entries are forwarded to its effective log sink.
/// Calling <see cref="Initialize" /> directly configures the original daily-file and Command Palette destinations.
/// </remarks>
[Obsolete("Use IExtensionHostLogSink and configure sinks through ExtensionHostRunner.CreateBuilder instead.")]
public static class Logger
{
    /// <summary>
    /// Initializes the compatibility logger with its original default destinations.
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
        LegacyLoggerBridge.Write(ExtensionHostLogLevel.Debug, message);
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public static void LogInformation(string message)
    {
        LegacyLoggerBridge.Write(ExtensionHostLogLevel.Information, message);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public static void LogWarning(string message)
    {
        LegacyLoggerBridge.Write(ExtensionHostLogLevel.Warning, message);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public static void LogError(string message)
    {
        LegacyLoggerBridge.Write(ExtensionHostLogLevel.Error, message);
    }

    /// <summary>
    /// Logs an exception as an error.
    /// </summary>
    public static void LogError(Exception exception)
    {
        LegacyLoggerBridge.Write(
            ExtensionHostLogLevel.Error,
            $"{exception.GetType().Name}: {exception.Message}",
            exception);
    }

    /// <summary>
    /// Logs an error message and its associated exception.
    /// </summary>
    public static void LogError(string message, Exception exception)
    {
        LegacyLoggerBridge.Write(
            ExtensionHostLogLevel.Error,
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

    private static IExtensionHostLogSink? _sink;
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
            var fileSink = new DailyFileExtensionHostLogSink(logFilePath);
            var sink = new CompositeExtensionHostLogSink(
            [
                fileSink,
                CommandPaletteExtensionHostLogSink.Instance
            ]);

            SetSink(sink, fileSink, isDebug);
            Write(ExtensionHostLogLevel.Debug, "Logger initialized");
        }
        catch (Exception ex)
        {
            Write(
                ExtensionHostLogLevel.Error,
                $"{ex.GetType().Name}: {ex.Message}",
                ex);
        }
    }

    internal static void UseSink(IExtensionHostLogSink sink, bool isDebug)
    {
        ArgumentNullException.ThrowIfNull(sink);
        SetSink(sink, null, isDebug);
    }

    internal static void Write(
        ExtensionHostLogLevel level,
        string message,
        Exception? exception = null)
    {
        if (_isWriting)
        {
            return;
        }

        IExtensionHostLogSink? sink;
        bool isDebugEnabled;

        lock (SyncRoot)
        {
            sink = _sink;
            isDebugEnabled = _isDebugEnabled;
        }

        if (level == ExtensionHostLogLevel.Debug && !isDebugEnabled)
        {
#if DEBUG
            TryWrite(CommandPaletteExtensionHostLogSink.Instance, level, message, exception);
#endif
            return;
        }

        TryWrite(sink ?? CommandPaletteExtensionHostLogSink.Instance, level, message, exception);
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
        IExtensionHostLogSink sink,
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
        IExtensionHostLogSink sink,
        ExtensionHostLogLevel level,
        string message,
        Exception? exception)
    {
        try
        {
            _isWriting = true;
            sink.Write(new ExtensionHostLogEntry(
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