// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using Serilog.Core;
using Serilog.Events;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog;

internal sealed class CommandPaletteSerilogSink : ILogEventSink
{
    private readonly ExtensionHostSerilogSinkCore _sink = new(
        CommandPaletteExtensionHostLogSink.Instance);

    public void Emit(LogEvent logEvent)
    {
        this._sink.Emit(logEvent);
    }
}
