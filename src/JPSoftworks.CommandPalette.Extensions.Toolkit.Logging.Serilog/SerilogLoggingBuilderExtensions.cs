// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog;

/// <summary>
/// Configures extension host logging to use an application-owned Serilog pipeline.
/// </summary>
public static class SerilogLoggingBuilderExtensions
{
    /// <summary>
    /// Uses <paramref name="logger"/> as the host diagnostic pipeline.
    /// </summary>
    /// <typeparam name="TBuilder">The concrete extension host logging builder type.</typeparam>
    /// <param name="builder">The extension host logging builder.</param>
    /// <param name="logger">The application-owned Serilog logger.</param>
    /// <returns><paramref name="builder"/> for further configuration.</returns>
    /// <remarks>
    /// The logger and its sinks remain caller-owned. This method replaces the host's default sinks
    /// and forwards host diagnostics to the logger while preserving their categories. Explicitly
    /// added host sinks remain active.
    /// </remarks>
    public static TBuilder UseSerilog<TBuilder>(
        this TBuilder builder,
        global::Serilog.ILogger logger)
        where TBuilder : class, IExtensionHostLoggingBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(logger);

        builder.ClearDefaultLogSinks();
        builder.AddHostLogSink(new SerilogExtensionHostLogSink(logger));
        return builder;
    }
}
