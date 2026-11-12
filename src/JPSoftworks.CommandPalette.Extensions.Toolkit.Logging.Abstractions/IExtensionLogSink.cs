// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

/// <summary>
/// Receives log entries written by an extension and by the host process that runs it.
/// </summary>
/// <remarks>
/// Extensions without a logging framework can log through this contract directly. Implementations may forward entries
/// to any logging system selected by the consuming application, so extensions that already use another logging
/// abstraction can keep it and connect the host through an adapter. <see cref="Write" /> can be called concurrently
/// from multiple threads, so implementations must be thread-safe.
/// </remarks>
public interface IExtensionLogSink
{
    /// <summary>
    /// Writes one log entry.
    /// </summary>
    /// <param name="entry">The entry to write.</param>
    void Write(ExtensionLogEntry entry);
}
