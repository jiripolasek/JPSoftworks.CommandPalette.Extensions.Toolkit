// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

/// <summary>
/// Describes one diagnostic entry emitted by an extension host.
/// </summary>
public sealed class ExtensionHostLogEntry
{
    /// <summary>
    /// Initializes a new diagnostic entry.
    /// </summary>
    public ExtensionHostLogEntry(
        DateTimeOffset timestamp,
        ExtensionHostLogLevel level,
        string category,
        int eventId,
        string message,
        Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(message);

        this.Timestamp = timestamp;
        this.Level = level;
        this.Category = category;
        this.EventId = eventId;
        this.Message = message;
        this.Exception = exception;
    }

    /// <summary>
    /// Gets the time at which the entry was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the severity of the entry.
    /// </summary>
    public ExtensionHostLogLevel Level { get; }

    /// <summary>
    /// Gets the component that emitted the entry.
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Gets the stable identifier for the event within its category.
    /// </summary>
    public int EventId { get; }

    /// <summary>
    /// Gets the human-readable message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the exception associated with the entry, if any.
    /// </summary>
    public Exception? Exception { get; }
}
