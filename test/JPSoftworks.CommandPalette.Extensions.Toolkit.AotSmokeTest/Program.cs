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
using Microsoft.Extensions.Logging;
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

        if (args.Contains("--exercise-application-adapters", StringComparer.Ordinal))
        {
            ExerciseApplicationAdapters();
        }

        var parameters = new ExtensionHostRunnerParameters
        {
            PublisherMoniker = "JPSoftworks",
            ProductMoniker = "AotSmokeTest",
        };

        var serilogHost = ExtensionHostConfiguration.Resolve(
            args,
            new ExtensionHostRunnerParameters
            {
                PublisherMoniker = "JPSoftworks",
                ProductMoniker = "AotSmokeTestSerilog",
            });
        using var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.FromExtensionHost(serilogHost)
            .WriteTo.DailyFile(serilogHost)
            .WriteTo.CommandPalette()
            .CreateLogger();
        _ = ExtensionHostRunner
            .CreateBuilder(serilogHost)
            .UseSerilog(serilogLogger);

        var host = ExtensionHostConfiguration.Resolve(args, parameters);
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder
                .AddDailyFile(host)
                .AddCommandPalette(host));
        var runner = ExtensionHostRunner
            .CreateBuilder(host)
            .AddHostedExtensionFactory(_ => new SmokeTestExtension())
            .UseMicrosoftExtensionsLogging(loggerFactory)
            .AddLogSink(new DelegateExtensionHostLogSink(static _ => { }));
        await runner.RunAsync();
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

    private static void ExerciseApplicationAdapters()
    {
        using var commandPaletteProvider = new CommandPaletteLoggerProvider();
        _ = commandPaletteProvider.CreateLogger("AotSmokeTest");

        using var dailyFileProvider = new DailyFileLoggerProvider(
            Path.Combine(Path.GetTempPath(), "CmdPalToolkitAotSmokeTest", "log.txt"));
        _ = dailyFileProvider.CreateLogger("AotSmokeTest");

        using var serilogLogger = new LoggerConfiguration()
            .WriteTo.CommandPalette()
            .CreateLogger();
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
