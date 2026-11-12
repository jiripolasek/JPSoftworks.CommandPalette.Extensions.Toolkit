// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Forwards entries at or above a minimum level to another sink.
/// </summary>
internal sealed class MinimumLevelLogSink(IExtensionLogSink sink, ExtensionLogLevel minimumLevel) : IExtensionLogSink
{
    internal IExtensionLogSink Sink => sink;

    internal ExtensionLogLevel MinimumLevel => minimumLevel;

    public void Write(ExtensionLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Level >= minimumLevel)
        {
            sink.Write(entry);
        }
    }
}
