// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

public static class Logger
{
    private static readonly object SyncRoot = new();

    private static string? _logFilePath;
    private static DateOnly _logFileDate;
    private static StreamWriter? _writer;
    private static bool _isDebug;

    public static void Initialize(string publisherName, string productName, bool isDebug = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publisherName);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);

        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify);
            string logFile = Path.Combine(localAppData, publisherName, productName, "log.txt");
            string? logDirectory = Path.GetDirectoryName(logFile);
            if (logDirectory != null && !Directory.Exists(logDirectory)) Directory.CreateDirectory(logDirectory);

            lock (SyncRoot)
            {
                CloseWriter();
                _logFilePath = logFile;
                _isDebug = isDebug;
                EnsureWriter();
            }

            LogDebug("Logger initialized");
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
    }

    public static void LogDebug(string message)
    {
        if (_isDebug)
        {
            WriteToFile("DBG", message);
        }
#if DEBUG
        ExtensionHost.LogMessage(new LogMessage(message) { State = MessageState.Info });
#endif
    }

    public static void LogInformation(string message)
    {
        WriteToFile("INF", message);
        ExtensionHost.LogMessage(new LogMessage(message) { State = MessageState.Info });
    }

    public static void LogError(string message)
    {
        WriteToFile("ERR", message);
        ExtensionHost.LogMessage(new LogMessage(message) { State = MessageState.Error });
    }

    public static void LogWarning(string message)
    {
        WriteToFile("WRN", message);
        ExtensionHost.LogMessage(new LogMessage(message) { State = MessageState.Warning });
    }

    public static void LogError(Exception exception)
    {
        string message = string.Format(CultureInfo.InvariantCulture, "{0}: {1}", exception.GetType().Name,
            exception.Message);
        WriteToFile("ERR", exception.ToString());
        ExtensionHost.LogMessage(new LogMessage(message) { State = MessageState.Error });
    }

    public static void LogError(string message, Exception exception)
    {
        string formattedMessage = string.Format(CultureInfo.InvariantCulture, "{0}: {1}", message, exception.Message);
        WriteToFile("ERR", string.Format(CultureInfo.InvariantCulture, "{0}: {1}", message, exception));
        ExtensionHost.LogMessage(new LogMessage(formattedMessage) { State = MessageState.Error });
    }

    public static void CloseAndFlush()
    {
        try
        {
            lock (SyncRoot)
            {
                CloseWriter();
                _logFilePath = null;
            }
        }
        catch
        {
            // Logging shutdown must never interrupt the extension host.
        }
    }

    private static void WriteToFile(string level, string message)
    {
        try
        {
            lock (SyncRoot)
            {
                EnsureWriter();
                if (_writer == null)
                {
                    return;
                }

                string timestamp = DateTimeOffset.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss.fff zzz",
                    CultureInfo.InvariantCulture);
                _writer.WriteLine(
                    string.Format(CultureInfo.InvariantCulture, "{0} [{1}] {2}", timestamp, level, message));
            }
        }
        catch
        {
            // Logging must never interrupt the extension host.
        }
    }

    private static void EnsureWriter()
    {
        if (_logFilePath == null)
        {
            return;
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        if (_writer != null && _logFileDate == today)
        {
            return;
        }

        CloseWriter();

        string? logDirectory = Path.GetDirectoryName(_logFilePath);
        string logFileName = Path.GetFileNameWithoutExtension(_logFilePath);
        string logExtension = Path.GetExtension(_logFilePath);
        string dateSuffix = today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string dailyLogFile = Path.Combine(logDirectory ?? string.Empty, $"{logFileName}{dateSuffix}{logExtension}");

        var stream = new FileStream(dailyLogFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream) { AutoFlush = true };
        _logFileDate = today;
    }

    private static void CloseWriter()
    {
        _writer?.Dispose();
        _writer = null;
    }
}
