// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

/// <summary>
/// Convenience methods for emitting extension host diagnostics.
/// </summary>
public static class ExtensionLogSinkExtensions
{
    /// <summary>
    /// Emits a debug diagnostic.
    /// </summary>
    public static void LogDebug(
        this IExtensionLogSink sink,
        string category,
        string message)
    {
        Write(sink, ExtensionLogLevel.Debug, category, message, null);
    }

    /// <summary>
    /// Emits an informational diagnostic.
    /// </summary>
    public static void LogInformation(
        this IExtensionLogSink sink,
        string category,
        string message)
    {
        Write(sink, ExtensionLogLevel.Information, category, message, null);
    }

    /// <summary>
    /// Emits a warning diagnostic.
    /// </summary>
    public static void LogWarning(
        this IExtensionLogSink sink,
        string category,
        string message)
    {
        Write(sink, ExtensionLogLevel.Warning, category, message, null);
    }

    /// <summary>
    /// Emits an error diagnostic.
    /// </summary>
    public static void LogError(
        this IExtensionLogSink sink,
        string category,
        string message)
    {
        Write(sink, ExtensionLogLevel.Error, category, message, null);
    }

    /// <summary>
    /// Emits an error diagnostic with an exception.
    /// </summary>
    public static void LogError(
        this IExtensionLogSink sink,
        string category,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Write(
            sink,
            ExtensionLogLevel.Error,
            category,
            $"{exception.GetType().Name}: {exception.Message}",
            exception);
    }

    /// <summary>
    /// Emits an error diagnostic with a message and exception.
    /// </summary>
    public static void LogError(
        this IExtensionLogSink sink,
        string category,
        string message,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Write(sink, ExtensionLogLevel.Error, category, $"{message}: {exception.Message}", exception);
    }

    private static void Write(
        IExtensionLogSink sink,
        ExtensionLogLevel level,
        string category,
        string message,
        Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Write(new ExtensionLogEntry(DateTimeOffset.Now, level, category, 0, message, exception));
    }
}
