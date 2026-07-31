// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions;

/// <summary>
/// Configures extension host logging to use an application-owned Microsoft logging pipeline.
/// </summary>
public static class MicrosoftExtensionsLoggingBuilderExtensions
{
    /// <summary>
    /// Uses <paramref name="loggerFactory"/> as the host diagnostic pipeline.
    /// </summary>
    /// <typeparam name="TBuilder">The concrete extension host logging builder type.</typeparam>
    /// <param name="builder">The extension host logging builder.</param>
    /// <param name="loggerFactory">The application-owned logger factory.</param>
    /// <returns><paramref name="builder"/> for further configuration.</returns>
    /// <remarks>
    /// The logger factory and its providers remain caller-owned. This method replaces the host's default sinks
    /// and forwards host diagnostics to the factory while preserving their categories. Explicitly added host
    /// sinks remain active.
    /// </remarks>
    public static TBuilder UseMicrosoftExtensionsLogging<TBuilder>(
        this TBuilder builder,
        ILoggerFactory loggerFactory)
        where TBuilder : class, IExtensionHostLoggingBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        builder.ClearDefaultLogSinks();
        builder.AddHostLogSink(new MicrosoftLoggerExtensionHostLogSink(loggerFactory));
        return builder;
    }
}
