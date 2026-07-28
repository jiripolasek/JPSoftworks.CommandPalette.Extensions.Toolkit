// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Convenience methods for emitting extension host diagnostics.
/// </summary>
public static class ExtensionHostLogSinkExtensions
{
    /// <summary>
    /// Emits a debug diagnostic.
    /// </summary>
    public static void LogDebug(
        this IExtensionHostLogSink sink,
        string category,
        string message)
    {
        Write(sink, ExtensionHostLogLevel.Debug, category, message, null);
    }

    /// <summary>
    /// Emits an informational diagnostic.
    /// </summary>
    public static void LogInformation(
        this IExtensionHostLogSink sink,
        string category,
        string message)
    {
        Write(sink, ExtensionHostLogLevel.Information, category, message, null);
    }

    /// <summary>
    /// Emits a warning diagnostic.
    /// </summary>
    public static void LogWarning(
        this IExtensionHostLogSink sink,
        string category,
        string message)
    {
        Write(sink, ExtensionHostLogLevel.Warning, category, message, null);
    }

    /// <summary>
    /// Emits an error diagnostic.
    /// </summary>
    public static void LogError(
        this IExtensionHostLogSink sink,
        string category,
        string message)
    {
        Write(sink, ExtensionHostLogLevel.Error, category, message, null);
    }

    /// <summary>
    /// Emits an error diagnostic with an exception.
    /// </summary>
    public static void LogError(
        this IExtensionHostLogSink sink,
        string category,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Write(
            sink,
            ExtensionHostLogLevel.Error,
            category,
            $"{exception.GetType().Name}: {exception.Message}",
            exception);
    }

    /// <summary>
    /// Emits an error diagnostic with a message and exception.
    /// </summary>
    public static void LogError(
        this IExtensionHostLogSink sink,
        string category,
        string message,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Write(sink, ExtensionHostLogLevel.Error, category, $"{message}: {exception.Message}", exception);
    }

    private static void Write(
        IExtensionHostLogSink sink,
        ExtensionHostLogLevel level,
        string category,
        string message,
        Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Write(new ExtensionHostLogEntry(DateTimeOffset.Now, level, category, 0, message, exception));
    }
}