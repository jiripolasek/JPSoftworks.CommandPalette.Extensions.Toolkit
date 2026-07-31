// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using Serilog.Core;
using Serilog.Events;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog;

internal sealed class DailyFileSerilogSink : ILogEventSink, IDisposable
{
    private readonly DailyFileExtensionHostLogSink _fileSink;
    private readonly ExtensionHostSerilogSinkCore _sink;

    public DailyFileSerilogSink(string baseFilePath)
    {
        this._fileSink = new DailyFileExtensionHostLogSink(baseFilePath);
        this._sink = new ExtensionHostSerilogSinkCore(this._fileSink);
    }

    public void Emit(LogEvent logEvent)
    {
        this._sink.Emit(logEvent);
    }

    public void Dispose()
    {
        this._fileSink.Dispose();
    }
}
