// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

/// <summary>
/// Receives diagnostics emitted by an extension host.
/// </summary>
/// <remarks>
/// This contract is an integration boundary for host diagnostics, not an application-wide logging abstraction.
/// Implementations may forward entries to any logging system selected by the consuming application.
/// </remarks>
public interface IExtensionHostLogSink
{
    /// <summary>
    /// Writes one diagnostic entry.
    /// </summary>
    /// <param name="entry">The entry to write.</param>
    void Write(ExtensionHostLogEntry entry);
}
