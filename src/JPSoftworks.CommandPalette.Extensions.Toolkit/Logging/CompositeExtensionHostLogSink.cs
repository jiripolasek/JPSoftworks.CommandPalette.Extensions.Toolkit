// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Forwards each diagnostic entry to multiple sinks.
/// </summary>
/// <remarks>
/// Failures from an individual sink are suppressed so that logging cannot interrupt the extension host
/// or prevent the remaining sinks from receiving an entry.
/// </remarks>
public sealed class CompositeExtensionHostLogSink : IExtensionHostLogSink
{
    private readonly IExtensionHostLogSink[] _sinks;

    /// <summary>
    /// Initializes a composite sink from a snapshot of the supplied sinks.
    /// </summary>
    /// <param name="sinks">The sinks that should receive each entry.</param>
    public CompositeExtensionHostLogSink(IEnumerable<IExtensionHostLogSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        this._sinks = sinks.ToArray();

        if (this._sinks.Any(static sink => sink == null))
        {
            throw new ArgumentException("The sink collection cannot contain null entries.", nameof(sinks));
        }
    }

    /// <inheritdoc />
    public void Write(ExtensionHostLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        foreach (var sink in this._sinks)
        {
            try
            {
                sink.Write(entry);
            }
            catch
            {
                // Logging must never interrupt the extension host or the remaining sinks.
            }
        }
    }
}
