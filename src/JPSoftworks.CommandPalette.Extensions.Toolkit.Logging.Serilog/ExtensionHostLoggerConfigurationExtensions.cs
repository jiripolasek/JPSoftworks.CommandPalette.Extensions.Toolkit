// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit;
using Serilog.Configuration;
using Serilog.Events;

namespace Serilog;

/// <summary>
/// Applies extension host policy and destinations to a Serilog logger configuration.
/// </summary>
public static class ExtensionHostLoggerConfigurationExtensions
{
    /// <summary>
    /// Applies the Toolkit's effective minimum level for the resolved host run.
    /// </summary>
    /// <param name="minimumLevelConfiguration">The Serilog minimum-level configuration.</param>
    /// <param name="configuration">The resolved extension host configuration.</param>
    /// <returns>The logger configuration.</returns>
    public static LoggerConfiguration FromExtensionHost(
        this LoggerMinimumLevelConfiguration minimumLevelConfiguration,
        ExtensionHostConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(minimumLevelConfiguration);
        ArgumentNullException.ThrowIfNull(configuration);

        return minimumLevelConfiguration.Is(
            configuration.IsDebug
                ? LogEventLevel.Verbose
                : LogEventLevel.Information);
    }

    /// <summary>
    /// Adds the Toolkit's canonical daily rolling file destination.
    /// </summary>
    /// <param name="sinkConfiguration">The Serilog sink configuration.</param>
    /// <param name="configuration">The resolved extension host configuration.</param>
    /// <param name="restrictedToMinimumLevel">The minimum level written to the file.</param>
    /// <returns>The logger configuration.</returns>
    public static LoggerConfiguration DailyFile(
        this LoggerSinkConfiguration sinkConfiguration,
        ExtensionHostConfiguration configuration,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
    {
        ArgumentNullException.ThrowIfNull(sinkConfiguration);
        ArgumentNullException.ThrowIfNull(configuration);

        return sinkConfiguration.Sink(
            new JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog.DailyFileSerilogSink(
                configuration.LogFilePath),
            restrictedToMinimumLevel);
    }
}
