// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Provides process-owned services to an extension factory.
/// </summary>
public sealed class ExtensionHostContext
{
    internal ExtensionHostContext(
        ManualResetEvent extensionDisposedEvent,
        IExtensionHostLogSink logSink)
    {
        this.ExtensionDisposedEvent = extensionDisposedEvent;
        this.LogSink = logSink;
    }

    /// <summary>
    /// Gets the event that an extension must signal when it has been disposed.
    /// </summary>
    public ManualResetEvent ExtensionDisposedEvent { get; }

    /// <summary>
    /// Gets the effective sink for extension host diagnostics.
    /// </summary>
    /// <remarks>
    /// Extension composition code can bridge this sink to its selected logging system. Core services do not need
    /// to reference the toolkit logging contracts.
    /// </remarks>
    public IExtensionHostLogSink LogSink { get; }
}
