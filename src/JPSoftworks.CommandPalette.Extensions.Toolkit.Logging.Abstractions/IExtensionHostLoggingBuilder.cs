// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

/// <summary>
/// Defines the logging-composition surface exposed by an extension host runner builder.
/// </summary>
public interface IExtensionHostLoggingBuilder
{
    /// <summary>
    /// Removes the host's default logging sinks.
    /// </summary>
    void ClearDefaultLogSinks();

    /// <summary>
    /// Adds a sink to the effective extension host logging pipeline.
    /// </summary>
    /// <param name="sink">The sink to add.</param>
    void AddHostLogSink(IExtensionHostLogSink sink);
}
