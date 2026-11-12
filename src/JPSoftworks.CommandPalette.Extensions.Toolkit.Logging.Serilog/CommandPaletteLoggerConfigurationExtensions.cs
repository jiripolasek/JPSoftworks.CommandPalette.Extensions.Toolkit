// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using Serilog.Configuration;
using Serilog.Events;

namespace Serilog;

/// <summary>
/// Adds Command Palette destinations to a Serilog logger configuration.
/// </summary>
public static class CommandPaletteLoggerConfigurationExtensions
{
    /// <summary>
    /// Writes Serilog events to the Command Palette host log.
    /// </summary>
    /// <param name="sinkConfiguration">The Serilog sink configuration.</param>
    /// <param name="restrictedToMinimumLevel">The minimum level written to Command Palette.</param>
    /// <returns>The logger configuration.</returns>
    /// <remarks>
    /// Each event is a cross-process COM call into Command Palette. Raise <paramref name="restrictedToMinimumLevel" />
    /// so that only low-volume events that should be visible in Command Palette are forwarded.
    /// </remarks>
    public static LoggerConfiguration CommandPalette(
        this LoggerSinkConfiguration sinkConfiguration,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
    {
        ArgumentNullException.ThrowIfNull(sinkConfiguration);
        return sinkConfiguration.Sink(
            new JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog.CommandPaletteSerilogSink(),
            restrictedToMinimumLevel);
    }
}
