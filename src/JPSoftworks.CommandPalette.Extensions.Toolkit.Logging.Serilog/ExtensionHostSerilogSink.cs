// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Globalization;
using Serilog.Core;
using Serilog.Events;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog;

/// <summary>
/// Forwards Serilog events to an extension host diagnostic sink.
/// </summary>
/// <remarks>
/// The supplied extension host sink is caller-owned. Serilog verbose events are represented as extension host
/// debug events, and fatal events as error events.
/// </remarks>
public sealed class ExtensionHostSerilogSink : ILogEventSink
{
    private const string DefaultCategory = "Serilog";
    private const string EventIdPropertyName = "EventId";
    private readonly IExtensionHostLogSink _sink;

    /// <summary>
    /// Initializes a Serilog sink that writes to <paramref name="sink"/>.
    /// </summary>
    /// <param name="sink">The target extension host sink.</param>
    public ExtensionHostSerilogSink(IExtensionHostLogSink sink)
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
            var entry = new ExtensionHostLogEntry(
                logEvent.Timestamp,
                ToExtensionHostLogLevel(logEvent.Level),
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

    private static ExtensionHostLogLevel ToExtensionHostLogLevel(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose or LogEventLevel.Debug => ExtensionHostLogLevel.Debug,
            LogEventLevel.Information => ExtensionHostLogLevel.Information,
            LogEventLevel.Warning => ExtensionHostLogLevel.Warning,
            LogEventLevel.Error or LogEventLevel.Fatal => ExtensionHostLogLevel.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown Serilog level."),
        };
    }
}