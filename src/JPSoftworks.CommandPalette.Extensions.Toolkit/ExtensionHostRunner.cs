// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Helpers;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using Shmuelie.WinRTServer;
using WinRT;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Runs self-contained Command Palette extension servers.
/// </summary>
/// <remarks>
/// The zero-configuration <see cref="RunAsync"/> overload uses the toolkit's daily file and Command Palette
/// diagnostic sinks. <see cref="CreateBuilder"/> exposes the same execution path with additional or replacement sinks.
/// </remarks>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static class ExtensionHostRunner
{
    private const string LogCategory = nameof(ExtensionHostRunner);

    /// <summary>
    /// Creates an extension host runner builder.
    /// </summary>
    /// <param name="args">Command line arguments passed to the application.</param>
    /// <param name="runParams">Configuration parameters for running the server.</param>
    /// <returns>A builder initialized with the toolkit's default behavior.</returns>
    public static ExtensionHostRunnerBuilder CreateBuilder(
        string[] args,
        ExtensionHostRunnerParameters runParams)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(runParams);
        return new ExtensionHostRunnerBuilder(args, runParams);
    }

    /// <summary>
    /// Runs the self-contained extension server with the toolkit's default behavior.
    /// </summary>
    /// <param name="args">Command line arguments passed to the application.</param>
    /// <param name="runParams">Configuration parameters for running the server.</param>
    /// <returns>A task that represents the asynchronous server operation.</returns>
    public static Task RunAsync(
        string[] args,
        ExtensionHostRunnerParameters runParams)
    {
        return CreateBuilder(args, runParams).RunAsync();
    }

    internal static async Task RunCoreAsync(
        string[] args,
        ExtensionHostRunnerParameters runParams,
        bool includeDefaultLogSinks,
        IReadOnlyCollection<IExtensionHostLogSink> additionalLogSinks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runParams.PublisherMoniker);
        ArgumentException.ThrowIfNullOrWhiteSpace(runParams.ProductMoniker);
        ArgumentNullException.ThrowIfNull(runParams.ExtensionFactories);

        var isExplicitlyDebug = args.Any(static arg => arg == "-Debug");
        var isDebug = runParams.IsDebug || isExplicitlyDebug;
        var isComServer = args.Any(static arg => arg == "-RegisterProcessAsComServer");

        using var logSink = CreateLogSink(runParams, includeDefaultLogSinks, additionalLogSinks, isDebug);
        LegacyLoggerBridge.UseSink(logSink, isDebug);

        try
        {
            logSink.LogDebug(LogCategory, "Diagnostics initialized");

            if (isComServer)
            {
                await RunComServerAsync(runParams, logSink);
            }
            else
            {
                await StartupHelper.HandleDirectLaunchAsync(logSink);
            }
        }
        catch (Exception ex)
        {
            logSink.LogError(LogCategory, "Unhandled fatal exception", ex);
        }
        finally
        {
            logSink.LogDebug(LogCategory, "Done");
            LegacyLoggerBridge.CloseAndFlush();
        }
    }

    private static ExtensionHostLogRouter CreateLogSink(
        ExtensionHostRunnerParameters runParams,
        bool includeDefaultLogSinks,
        IReadOnlyCollection<IExtensionHostLogSink> additionalLogSinks,
        bool isDebug)
    {
        List<IExtensionHostLogSink> sinks = [];
        List<IDisposable> ownedResources = [];

        if (includeDefaultLogSinks)
        {
            try
            {
                var localAppData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData,
                    Environment.SpecialFolderOption.DoNotVerify);
                var logFilePath = Path.Combine(
                    localAppData,
                    runParams.PublisherMoniker,
                    runParams.ProductMoniker,
                    "log.txt");
                var fileSink = new DailyFileExtensionHostLogSink(logFilePath);
                sinks.Add(fileSink);
                ownedResources.Add(fileSink);
            }
            catch
            {
                sinks.Add(TraceExtensionHostLogSink.Instance);
            }

            sinks.Add(CommandPaletteExtensionHostLogSink.Instance);
        }

        sinks.AddRange(additionalLogSinks);

        return new ExtensionHostLogRouter(sinks, ownedResources, isDebug);
    }

    private static async Task RunComServerAsync(
        ExtensionHostRunnerParameters runParams,
        IExtensionHostLogSink logSink)
    {
        logSink.LogDebug(LogCategory, "Running as COM server");

        using ManualResetEvent extensionDisposedEvent = new(false);
        using ManualResetEvent appLifeMonitorTerminationEvent = new(false);
        using var appLifeMonitor = TrySetAppLifeMonitor(appLifeMonitorTerminationEvent, logSink);

        var context = new ExtensionHostContext(extensionDisposedEvent, logSink);
        var server = new ComServer();

        TrySetShutdownPriority(logSink);
        TryEnableEfficiencyMode(runParams);

        if (runParams.ExtensionFactories.Count > 0)
        {
            DefaultComWrappers? comWrappers = null;

            logSink.LogDebug(LogCategory, "Creating extensions from factories");
            foreach (var factory in runParams.ExtensionFactories)
            {
                if (factory == null)
                {
                    logSink.LogWarning(LogCategory, "Extension factory is null, skipping");
                    continue;
                }

                try
                {
                    var extension = factory.CreateExtension(context);
                    if (extension == null)
                    {
                        logSink.LogError(LogCategory, "Extension factory returned null, skipping");
                        continue;
                    }

                    server.RegisterClassFactory(new SingletonExtensionFactory(extension), comWrappers ??= new());
                }
                catch (Exception ex)
                {
                    logSink.LogError(
                        LogCategory,
                        $"Failed to create extension from factory {factory.GetType().Name}",
                        ex);
                }
            }
        }
        else
        {
            logSink.LogDebug(LogCategory, "No extension factories provided");
        }

        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.MTA)
        {
            throw new InvalidOperationException("The COM server must be run in MTA thread.");
        }

        logSink.LogDebug(LogCategory, "Starting COM server");
        server.Start();

        logSink.LogDebug(LogCategory, "Waiting for the extension to be released or the extension app to close");
        await WaitForAnyEventAsync(extensionDisposedEvent, appLifeMonitorTerminationEvent);

        logSink.LogDebug(LogCategory, "Extension disposed or app closed, shutting down COM server");
        server.UnsafeDispose();
    }

    private static void TryEnableEfficiencyMode(ExtensionHostRunnerParameters extensionHostRunnerParameters)
    {
        if (extensionHostRunnerParameters.EnableEfficiencyMode)
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Idle;
            EfficiencyModeHelper.TryEnableProcessEfficiencyMode();
        }
    }

    private static AppLifeMonitor? TrySetAppLifeMonitor(
        ManualResetEvent appLifeMonitorTerminationEvent,
        IExtensionHostLogSink logSink)
    {
        try
        {
            var appLifeMonitor = new AppLifeMonitor();
            appLifeMonitor.StartMonitoring();
            appLifeMonitor.ExitRequested += (_, _) => appLifeMonitorTerminationEvent.Set();
            return appLifeMonitor;
        }
        catch (Exception ex)
        {
            logSink.LogError(LogCategory, ex);
        }

        return null;
    }

    private static void TrySetShutdownPriority(IExtensionHostLogSink logSink)
    {
        try
        {
            // Set a lower priority, so system shutdown's other apps first (it goes from high dwLevel to low). Particularly we want
            // to allow host CmdPal to shut down before us. This has two effects:
            //    1. It allows CmdPal to release us, and we then shut down naturally.
            //    2. System won't shut us down before CmdPal, so if user cancels shutdown and CmdPal is still running, we are too.
            ShutdownHelper.TrySetShutdownPriority(0x200);
        }
        catch (Exception ex)
        {
            logSink.LogError(LogCategory, ex);
        }
    }

    private static async Task WaitForAnyEventAsync(params WaitHandle[] waitHandles)
    {
        ArgumentNullException.ThrowIfNull(waitHandles);
        if (waitHandles.Length == 0)
        {
            throw new ArgumentException("At least one wait handle must be provided.", nameof(waitHandles));
        }

        if (waitHandles.Any(static handle => handle == null))
        {
            throw new ArgumentException("All wait handles must be non-null.", nameof(waitHandles));
        }

        var taskCompletionSource = new TaskCompletionSource<int>();
        var waitRegistrations = new RegisteredWaitHandle[waitHandles.Length];

        try
        {
            for (var index = 0; index < waitHandles.Length; index++)
            {
                var resultIndex = index;
                waitRegistrations[index] = ThreadPool.RegisterWaitForSingleObject(
                    waitHandles[index],
                    (_, timedOut) =>
                    {
                        if (!timedOut)
                        {
                            taskCompletionSource.TrySetResult(resultIndex);
                        }
                    },
                    null,
                    Timeout.Infinite,
                    executeOnlyOnce: true);
            }

            await taskCompletionSource.Task;
        }
        finally
        {
            foreach (var registration in waitRegistrations)
            {
                registration?.Unregister(null);
            }
        }
    }
}
