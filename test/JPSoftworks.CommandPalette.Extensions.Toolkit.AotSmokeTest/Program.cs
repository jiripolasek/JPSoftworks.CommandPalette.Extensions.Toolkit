// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Runtime.InteropServices;
using JPSoftworks.CommandPalette.Extensions.Toolkit;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.AotSmokeTest;

internal static class Program
{
    [MTAThread]
    private static async Task Main(string[] args)
    {
        if (args.Contains("--exercise-legacy-logger", StringComparer.Ordinal))
        {
            ExerciseLegacyLogger();
        }

        using var serilogLogger = new LoggerConfiguration().CreateLogger();
        var parameters = new ExtensionHostRunnerParameters
        {
            PublisherMoniker = "JPSoftworks",
            ProductMoniker = "AotSmokeTest",
            ExtensionFactories =
            [
                new DelegateExtensionFactory(_ => new SmokeTestExtension()),
            ],
        };

        await ExtensionHostRunner
            .CreateBuilder(args, parameters)
            .AddLogSink(new DelegateExtensionHostLogSink(static _ => { }))
            .AddLogSink(new MicrosoftLoggerExtensionHostLogSink(NullLogger.Instance))
            .AddLogSink(new SerilogExtensionHostLogSink(serilogLogger))
            .RunAsync();
    }

    private static void ExerciseLegacyLogger()
    {
#pragma warning disable CS0618
        Logger.Initialize("JPSoftworks", "AotSmokeTest", isDebug: true);
        Logger.LogDebug("Debug");
        Logger.LogInformation("Information");
        Logger.LogWarning("Warning");
        Logger.LogError("Error");
        Logger.LogError(new InvalidOperationException("Failure"));
        Logger.LogError("Operation failed", new InvalidOperationException("Failure"));
        Logger.CloseAndFlush();
#pragma warning restore CS0618
    }
}

[Guid("A62D57D3-3185-464D-97C7-E08D49A8C88A")]
internal sealed class SmokeTestExtension : IExtension
{
    public object GetProvider(ProviderType providerType) => new();

    public void Dispose()
    {
    }
}
