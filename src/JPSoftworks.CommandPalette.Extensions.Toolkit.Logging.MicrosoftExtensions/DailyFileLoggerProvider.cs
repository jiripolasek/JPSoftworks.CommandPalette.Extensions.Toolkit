// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using Microsoft.Extensions.Logging;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions;

/// <summary>
/// Writes Microsoft.Extensions.Logging events to a file that rolls each local calendar day.
/// </summary>
public sealed class DailyFileLoggerProvider : ILoggerProvider
{
    private readonly DailyFileExtensionHostLogSink _sink;
    private readonly ExtensionHostLoggerProviderCore _provider;

    /// <summary>
    /// Initializes a daily rolling file provider.
    /// </summary>
    /// <param name="baseFilePath">
    /// The base path used to derive daily file names. For example, <c>log.txt</c> produces
    /// <c>log20260729.txt</c>.
    /// </param>
    public DailyFileLoggerProvider(string baseFilePath)
        : this(baseFilePath, LogLevel.Trace)
    {
    }

    /// <summary>
    /// Initializes a daily rolling file provider that accepts entries at or above
    /// <paramref name="minimumLevel"/>.
    /// </summary>
    /// <param name="baseFilePath">
    /// The base path used to derive daily file names. For example, <c>log.txt</c> produces
    /// <c>log20260729.txt</c>.
    /// </param>
    /// <param name="minimumLevel">The lowest Microsoft logging level that the provider accepts.</param>
    public DailyFileLoggerProvider(string baseFilePath, LogLevel minimumLevel)
    {
        this._sink = new DailyFileExtensionHostLogSink(baseFilePath);
        this._provider = new ExtensionHostLoggerProviderCore(this._sink, minimumLevel);
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
        this._sink.Dispose();
    }
}
