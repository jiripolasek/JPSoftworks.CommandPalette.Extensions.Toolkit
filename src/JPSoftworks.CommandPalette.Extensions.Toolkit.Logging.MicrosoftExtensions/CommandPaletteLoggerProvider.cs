// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using Microsoft.Extensions.Logging;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions;

/// <summary>
/// Writes Microsoft.Extensions.Logging events to the Command Palette host log.
/// </summary>
public sealed class CommandPaletteLoggerProvider : ILoggerProvider
{
    private readonly ExtensionHostLoggerProviderCore _provider;

    /// <summary>
    /// Initializes a provider that accepts all enabled Microsoft logging levels.
    /// </summary>
    public CommandPaletteLoggerProvider()
        : this(LogLevel.Trace)
    {
    }

    /// <summary>
    /// Initializes a provider that accepts entries at or above <paramref name="minimumLevel"/>.
    /// </summary>
    /// <param name="minimumLevel">The lowest Microsoft logging level that the provider accepts.</param>
    public CommandPaletteLoggerProvider(LogLevel minimumLevel)
    {
        this._provider = new ExtensionHostLoggerProviderCore(
            CommandPaletteExtensionHostLogSink.Instance,
            minimumLevel);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return this._provider.CreateLogger(categoryName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this._provider.Dispose();
    }
}
