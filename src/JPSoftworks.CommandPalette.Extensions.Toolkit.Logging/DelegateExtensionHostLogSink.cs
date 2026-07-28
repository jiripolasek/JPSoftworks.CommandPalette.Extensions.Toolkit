// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Forwards extension host diagnostics to a delegate.
/// </summary>
public sealed class DelegateExtensionHostLogSink : IExtensionHostLogSink
{
    private readonly Action<ExtensionHostLogEntry> _write;

    /// <summary>
    /// Initializes a new delegate-backed sink.
    /// </summary>
    /// <param name="write">The delegate that receives diagnostic entries.</param>
    public DelegateExtensionHostLogSink(Action<ExtensionHostLogEntry> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        this._write = write;
    }

    /// <inheritdoc />
    public void Write(ExtensionHostLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        this._write(entry);
    }
}