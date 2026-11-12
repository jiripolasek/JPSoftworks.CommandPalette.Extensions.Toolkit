// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Forwards log entries to a delegate.
/// </summary>
public sealed class DelegateLogSink : IExtensionLogSink
{
    private readonly Action<ExtensionLogEntry> _write;

    /// <summary>
    /// Initializes a new delegate-backed sink.
    /// </summary>
    /// <param name="write">The delegate that receives log entries. It can be called concurrently from multiple threads.</param>
    public DelegateLogSink(Action<ExtensionLogEntry> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        this._write = write;
    }

    /// <inheritdoc />
    public void Write(ExtensionLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        this._write(entry);
    }
}
