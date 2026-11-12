// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Globalization;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
using Serilog.Core;
using Serilog.Events;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog;

internal sealed class ExtensionHostSerilogSinkCore : ILogEventSink
{
    private const string DefaultCategory = "Serilog";
    private const string EventIdPropertyName = "EventId";
    private readonly IExtensionLogSink _sink;

    public ExtensionHostSerilogSinkCore(IExtensionLogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        this._sink = sink;
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (logEvent.Properties.ContainsKey(SerilogLoggingBridgeMarker.PropertyName))
        {
            return;
        }

        try
        {
            var entry = new ExtensionLogEntry(
                logEvent.Timestamp,
                ToExtensionLogLevel(logEvent.Level),
                GetCategory(logEvent),
                GetEventId(logEvent),
                logEvent.RenderMessage(CultureInfo.InvariantCulture),
                logEvent.Exception);
            SerilogLoggingBridgeMarker.Mark(entry);
            this._sink.Write(entry);
        }
        catch
        {
            // Logging must never interrupt the application.
        }
    }

    private static string GetCategory(LogEvent logEvent)
    {
        if (logEvent.Properties.TryGetValue(Constants.SourceContextPropertyName, out var value)
            && value is ScalarValue { Value: string category }
            && !string.IsNullOrWhiteSpace(category))
        {
            return category;
        }

        return DefaultCategory;
    }

    private static int GetEventId(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue(EventIdPropertyName, out var value))
        {
            return 0;
        }

        if (value is ScalarValue { Value: int eventId })
        {
            return eventId;
        }

        if (value is StructureValue structure)
        {
            foreach (var property in structure.Properties)
            {
                if (property.Name == "Id" && property.Value is ScalarValue { Value: int structuredEventId })
                {
                    return structuredEventId;
                }
            }
        }

        return 0;
    }

    private static ExtensionLogLevel ToExtensionLogLevel(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose or LogEventLevel.Debug => ExtensionLogLevel.Debug,
            LogEventLevel.Information => ExtensionLogLevel.Information,
            LogEventLevel.Warning => ExtensionLogLevel.Warning,
            LogEventLevel.Error => ExtensionLogLevel.Error,
            LogEventLevel.Fatal => ExtensionLogLevel.Critical,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown Serilog level."),
        };
    }
}
