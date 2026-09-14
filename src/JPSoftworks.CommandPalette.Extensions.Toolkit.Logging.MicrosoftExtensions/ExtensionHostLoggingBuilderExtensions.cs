// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Adds Toolkit-owned destinations to a Microsoft logging pipeline.
/// </summary>
public static class ExtensionHostLoggingBuilderExtensions
{
    /// <summary>
    /// Adds the Toolkit's canonical daily rolling file destination.
    /// </summary>
    /// <param name="builder">The Microsoft logging builder.</param>
    /// <param name="configuration">The resolved extension host configuration.</param>
    /// <returns><paramref name="builder"/> for further configuration.</returns>
    public static ILoggingBuilder AddDailyFile(
        this ILoggingBuilder builder,
        ExtensionHostConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        var minimumLevel = GetMinimumLevel(configuration);
        builder.Services.AddSingleton<ILoggerProvider>(
            _ => new DailyFileLoggerProvider(configuration.LogFilePath));
        builder.AddFilter<DailyFileLoggerProvider>(
            (_, level) => level >= minimumLevel);
        return builder;
    }

    /// <summary>
    /// Adds the Command Palette host log destination.
    /// </summary>
    /// <param name="builder">The Microsoft logging builder.</param>
    /// <param name="configuration">The resolved extension host configuration.</param>
    /// <returns><paramref name="builder"/> for further configuration.</returns>
    public static ILoggingBuilder AddCommandPalette(
        this ILoggingBuilder builder,
        ExtensionHostConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        var minimumLevel = GetMinimumLevel(configuration);
        builder.Services.AddSingleton<ILoggerProvider>(
            _ => new CommandPaletteLoggerProvider());
        builder.AddFilter<CommandPaletteLoggerProvider>(
            (_, level) => level >= minimumLevel);
        return builder;
    }

    private static LogLevel GetMinimumLevel(ExtensionHostConfiguration configuration)
    {
        return configuration.IsDebug
            ? LogLevel.Trace
            : LogLevel.Information;
    }
}
