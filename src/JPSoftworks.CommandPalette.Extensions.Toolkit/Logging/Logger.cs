// ------------------------------------------------------------
// 
// Copyright (c) Jiří Polášek. All rights reserved.
// 
// ------------------------------------------------------------

using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Serilog;
using Serilog.Events;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

public static class Logger
{
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

            var minLevel = isDebug ? LogEventLevel.Debug : LogEventLevel.Information;

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(
                    logFile,
                    buffered: false,
                    rollingInterval: RollingInterval.Day,
                    formatProvider: CultureInfo.InvariantCulture,
                    restrictedToMinimumLevel: minLevel)
                .MinimumLevel.Is(minLevel)
                .CreateLogger();
            LogDebug("Logger initialized");
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
    }

    public static void LogDebug(string message)
    {
        Log.Logger.Debug(message);
#if DEBUG
        ExtensionHost.LogMessage(new LogMessage(message) { State = MessageState.Info });
#endif
    }

    public static void LogInformation(string message)
    {
        Log.Logger.Information(message);
        ExtensionHost.LogMessage(new LogMessage(message) { State = MessageState.Info });
    }

    public static void LogError(string message)
    {
        Log.Logger.Error(message);
        ExtensionHost.LogMessage(new LogMessage(message) { State = MessageState.Error });
    }

    public static void LogWarning(string message)
    {
        Log.Logger.Warning(message);
        ExtensionHost.LogMessage(new LogMessage(message) { State = MessageState.Warning });
    }

    public static void LogError(Exception exception)
    {
        string message = string.Format(CultureInfo.InvariantCulture, "{0}: {1}", exception.GetType().Name,
            exception.Message);
        Log.Logger.Error(exception, message);
        ExtensionHost.LogMessage(new LogMessage(message) { State = MessageState.Error });
    }

    public static void LogError(string message, Exception exception)
    {
        string formattedMessage = string.Format(CultureInfo.InvariantCulture, "{0}: {1}", message, exception.Message);
        Log.Logger.Error(exception, formattedMessage);
        ExtensionHost.LogMessage(new LogMessage(formattedMessage) { State = MessageState.Error });
    }
    
    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}