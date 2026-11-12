// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

/// <summary>
/// Defines the logging-composition surface exposed by an extension host runner builder.
/// </summary>
public interface IExtensionLoggingBuilder
{
    /// <summary>
    /// Removes the host's default logging sinks.
    /// </summary>
    void ClearDefaultLogSinks();

    /// <summary>
    /// Adds a sink to the runner's log pipeline.
    /// </summary>
    /// <param name="sink">The sink to add.</param>
    void AddLogSink(IExtensionLogSink sink);
}
