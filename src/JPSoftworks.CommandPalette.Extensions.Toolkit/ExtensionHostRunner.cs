// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Helpers;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using WinRT;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Runs self-contained Command Palette extension servers.
/// </summary>
/// <remarks>
/// The zero-configuration <see cref="RunAsync" /> overload uses the toolkit's daily file and Command Palette
/// diagnostic sinks. <see cref="M:ExtensionHostRunner.CreateBuilder(System.String[],ExtensionHostRunnerParameters)" /> exposes the same execution path with additional or replacement sinks.
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
        return CreateBuilder(ExtensionHostConfiguration.Resolve(args, runParams));
    }

    /// <summary>
    /// Creates an extension host runner builder from a resolved configuration.
    /// </summary>
    /// <param name="configuration">The resolved host configuration.</param>
    /// <returns>A builder initialized with the toolkit's default behavior.</returns>
    public static ExtensionHostRunnerBuilder CreateBuilder(ExtensionHostConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new ExtensionHostRunnerBuilder(configuration);
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
        ExtensionHostConfiguration configuration,
        ExtensionHostRunnerParameters runParams,
        bool includeDefaultLogSinks,
        IReadOnlyCollection<IExtensionHostLogSink> additionalLogSinks)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(runParams.PublisherMoniker);
        ArgumentException.ThrowIfNullOrWhiteSpace(runParams.ProductMoniker);
        ArgumentNullException.ThrowIfNull(runParams.HostedExtensionFactories);
#pragma warning disable CS0618 // The runner preserves the event-only factory contract for binary compatibility.
        ArgumentNullException.ThrowIfNull(runParams.ExtensionFactories);
#pragma warning restore CS0618

        var isComServer = configuration.Arguments.Contains(
            "-RegisterProcessAsComServer",
            StringComparer.Ordinal);

        var appLifeMonitorTermination = isComServer ? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        AppLifeMonitor? appLifeMonitor = null;
        try
        {
            using var logSink = CreateLogSink(configuration, includeDefaultLogSinks, additionalLogSinks);
            LegacyLoggerBridge.UseSink(logSink, configuration.IsDebug);

            try
            {
                logSink.LogDebug(LogCategory, "Diagnostics initialized");
                logSink.LogInformation(
                    LogCategory,
                    isComServer
                        ? "Starting extension host in COM-server mode"
                        : "Starting extension host in direct-launch mode");

                if (isComServer)
                {
                    if (Thread.CurrentThread.GetApartmentState() != ApartmentState.MTA)
                    {
                        throw new InvalidOperationException("The COM server must be run in MTA thread.");
                    }

                    appLifeMonitor = TrySetAppLifeMonitor(appLifeMonitorTermination!, logSink);
                    await RunComServerAsync(runParams, logSink, appLifeMonitorTermination!.Task);
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
        finally
        {
            appLifeMonitor?.Dispose();
        }
    }

    private static ExtensionHostLogRouter CreateLogSink(
        ExtensionHostConfiguration configuration,
        bool includeDefaultLogSinks,
        IReadOnlyCollection<IExtensionHostLogSink> additionalLogSinks)
    {
        List<IExtensionHostLogSink> sinks = [];
        List<IDisposable> ownedResources = [];

        if (includeDefaultLogSinks)
        {
            try
            {
                var fileSink = new DailyFileExtensionHostLogSink(configuration.LogFilePath);
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

        return new ExtensionHostLogRouter(sinks, ownedResources, configuration.IsDebug);
    }

    private static async Task RunComServerAsync(
        ExtensionHostRunnerParameters runParams,
        IExtensionHostLogSink logSink,
        Task appLifeMonitorTermination)
    {
        logSink.LogDebug(LogCategory, "Running as COM server");

        var server = new ComServer();
        var lifetime = new ExtensionHostLifetime();
        List<ExtensionClassFactory> registeredFactories = [];
#pragma warning disable CS0618 // The runner preserves the event-only factory contract for binary compatibility.
        var legacyExtensionFactories = runParams.ExtensionFactories;
#pragma warning restore CS0618

        try
        {
            TrySetShutdownPriority(logSink);
            TryEnableEfficiencyMode(runParams, logSink);

            DefaultComWrappers? comWrappers = null;

            logSink.LogDebug(LogCategory, "Registering extension factories");
            foreach (var factory in runParams.HostedExtensionFactories)
            {
                if (factory == null)
                {
                    logSink.LogWarning(LogCategory, "Hosted extension factory is null, skipping");
                    continue;
                }

                try
                {
                    RegisterFactory(factory.CreateExtension);
                }
                catch (Exception ex)
                {
                    logSink.LogError(
                        LogCategory,
                        $"Failed to register hosted factory {factory.GetType().Name}",
                        ex);
                }
            }

            foreach (var factory in legacyExtensionFactories)
            {
                if (factory == null)
                {
                    logSink.LogWarning(LogCategory, "Legacy extension factory is null, skipping");
                    continue;
                }

                try
                {
                    RegisterFactory(context => factory.CreateExtension(context.ExtensionDisposedEvent));
                }
                catch (Exception ex)
                {
                    logSink.LogError(
                        LogCategory,
                        $"Failed to register legacy factory {factory.GetType().Name}",
                        ex);
                }
            }

            if (registeredFactories.Count == 0)
            {
                logSink.LogWarning(LogCategory, "No extension factories registered");
                return;
            }

            logSink.LogDebug(LogCategory, "Starting COM server");
            server.Start();

            logSink.LogDebug(LogCategory, "Waiting for all extensions to be disposed or the extension app to close");
            await WaitForShutdownAsync(lifetime.Shutdown, appLifeMonitorTermination);

            logSink.LogDebug(LogCategory, "Shutting down COM server");

            void RegisterFactory(Func<ExtensionHostContext, IExtension> createExtension)
            {
                var classFactory = new ExtensionClassFactory(createExtension, lifetime);
                var registered = false;
                try
                {
                    registered = server.RegisterClassFactory(classFactory, comWrappers ??= new DefaultComWrappers());
                    if (registered)
                    {
                        registeredFactories.Add(classFactory);
                    }
                    else
                    {
                        logSink.LogWarning(LogCategory, $"Duplicate extension CLSID {classFactory.ClassId}, skipping");
                    }
                }
                finally
                {
                    if (!registered)
                    {
                        classFactory.Dispose();
                    }
                }
            }
        }
        finally
        {
            try
            {
                lifetime.Drain();
            }
            finally
            {
                try
                {
                    server.UnsafeDispose();
                }
                finally
                {
                    foreach (var factory in registeredFactories)
                    {
                        try
                        {
                            factory.Dispose();
                        }
                        catch (Exception ex)
                        {
                            logSink.LogError(LogCategory, "Failed to dispose an unused extension instance", ex);
                        }
                    }

                    try
                    {
                        lifetime.DisposeActiveExtensions();
                    }
                    catch (Exception ex)
                    {
                        logSink.LogError(LogCategory, "Failed to dispose active extension instances", ex);
                    }
                }
            }
        }
    }

    private static void TryEnableEfficiencyMode(
        ExtensionHostRunnerParameters extensionHostRunnerParameters,
        IExtensionHostLogSink logSink)
    {
        if (extensionHostRunnerParameters.EnableEfficiencyMode)
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                process.PriorityClass = ProcessPriorityClass.Idle;
            }
            catch (Exception ex)
            {
                logSink.LogError(LogCategory, "Failed to lower the process priority", ex);
            }

            EfficiencyModeHelper.TryEnableProcessEfficiencyMode();
        }
    }

    private static AppLifeMonitor? TrySetAppLifeMonitor(
        TaskCompletionSource appLifeMonitorTermination,
        IExtensionHostLogSink logSink)
    {
        AppLifeMonitor? appLifeMonitor = null;
        try
        {
            appLifeMonitor = new AppLifeMonitor();
            appLifeMonitor.ExitRequested += (_, _) => appLifeMonitorTermination.TrySetResult();
            appLifeMonitor.StartMonitoring();
            return appLifeMonitor;
        }
        catch (Exception ex)
        {
            appLifeMonitor?.Dispose();
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
            ShutdownHelper.SetShutdownPriority(0x200);
        }
        catch (Exception ex)
        {
            logSink.LogError(LogCategory, ex);
        }
    }

    private static async Task WaitForShutdownAsync(Task shutdown, Task appTermination)
    {
        var completed = await Task.WhenAny(shutdown, appTermination);
        await completed;
    }
}