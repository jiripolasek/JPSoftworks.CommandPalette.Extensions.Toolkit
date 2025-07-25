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
/// Provides functionality for running self-contained Command Palette extension servers.
/// This static class handles the complete lifecycle of extension hosting, including COM server setup,
/// extension factory registration, application lifecycle monitoring, and graceful shutdown handling.
/// </summary>
/// <remarks>
/// <para>
/// The server supports both COM server mode (activated by the -RegisterProcessAsComServer argument) 
/// and direct launch mode. In COM server mode, it sets up a WinRT COM server to host Command Palette 
/// extensions, manages process efficiency settings, and monitors for application shutdown events.
/// </para>
/// <para>
/// In direct launch mode, it contains a fallback mechanism that will attempt to launch
/// Command Palette or bring it to the foreground if it is already running to give the user
/// at least some feedback.
/// </para>
/// </remarks>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static class ExtensionHostRunner
{
    /// <summary>
    /// Runs the self-contained extension server with the specified configuration.
    /// </summary>
    /// <param name="args">Command line arguments passed to the application.</param>
    /// <param name="runParams">Configuration parameters for running the server.</param>
    /// <returns>A task that represents the asynchronous server operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is null or <paramref name="runParams"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the COM server is not running in an MTA thread.</exception>
    public static async Task RunAsync(string[] args, ExtensionHostRunnerParameters runParams)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(runParams);

        try
        {
            var isExplicitlyDebug = args.Length > 0 && args.Any(arg => arg == "-Debug");
            var isComServer = args.Length > 0 && args.Any(arg => arg == "-RegisterProcessAsComServer");

            Logger.Initialize(runParams.PublisherMoniker, runParams.ProductMoniker, runParams.IsDebug || isExplicitlyDebug);

            if (isComServer)
            {
                Logger.LogDebug("Running as COM server");

                ManualResetEvent extensionDisposedEvent = new(false);

                var server = new ComServer();

                ManualResetEvent appLifeMonitorTerminationEvent = new(false);

                TrySetAppLifeMonitor(appLifeMonitorTerminationEvent);

                TrySetShutdownPriority();

                TryEnableEfficiencyMode(runParams);

                if (runParams.ExtensionFactories?.Count > 0)
                {
                    DefaultComWrappers? comWrappers = null;

                    Logger.LogDebug("Creating extensions from factories");
                    foreach (var factory in runParams.ExtensionFactories)
                    {
                        if (factory == null)
                        {
                            Logger.LogWarning("Extension factory is null, skipping");
                            continue;
                        }
                        try
                        {
                            var extension = factory.CreateExtension(extensionDisposedEvent);
                            if (extension == null)
                            {
                                Logger.LogError("Extension factory returned null, skipping");
                                continue;
                            }

                            server.RegisterClassFactory(new SingletonExtensionFactory(extension), comWrappers ??= new());
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to create extension from factory {factory.GetType().Name}", ex);
                        }
                    }
                }
                else
                {
                    Logger.LogDebug("No extension factories provided, using default extension");
                }

                if (Thread.CurrentThread.GetApartmentState() != ApartmentState.MTA)
                {
                    throw new InvalidOperationException("The COM server must be run in MTA thread.");
                }

                Logger.LogDebug("Starting COM server");

                server.Start();

                Logger.LogDebug("Waiting for extension to be release or extension app be closed");

                await WaitForAnyEventAsync(extensionDisposedEvent, appLifeMonitorTerminationEvent);

                Logger.LogDebug("Extension disposed or app closed, shutting down COM server");

                server.UnsafeDispose();
            }
            else
            {
                await StartupHelper.HandleDirectLaunchAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Unhandled fatal exception", ex);
        }
        finally
        {
            Logger.LogDebug("Done");
            Logger.CloseAndFlush();
        }
    }

    /// <summary>
    /// Attempts to enable efficiency mode for the current process if configured to do so.
    /// This includes setting the process priority to idle and enabling EcoQoS efficiency mode.
    /// </summary>
    /// <param name="extensionHostRunnerParameters">The server configuration parameters containing the efficiency mode setting.</param>
    private static void TryEnableEfficiencyMode(ExtensionHostRunnerParameters extensionHostRunnerParameters)
    {
        if (extensionHostRunnerParameters.EnableEfficiencyMode)
        {
            // Run with low priority
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Idle;

            // Enable Efficiency Mode (EcoQoS) if supported
            EfficiencyModeHelper.TryEnableProcessEfficiencyMode();
        }
    }

    /// <summary>
    /// Attempts to create and start an application lifecycle monitor that responds to system shutdown events.
    /// </summary>
    /// <param name="appLifeMonitorTerminationEvent">The manual reset event to signal when termination is requested.</param>
    /// <returns>The created <see cref="AppLifeMonitor"/> instance, or null if creation failed.</returns>
    private static AppLifeMonitor? TrySetAppLifeMonitor(ManualResetEvent appLifeMonitorTerminationEvent)
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
            Logger.LogError(ex);
        }

        return null;
    }

    /// <summary>
    /// Attempts to set the shutdown priority for the current process to ensure graceful termination.
    /// </summary>
    private static void TrySetShutdownPriority()
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
            Logger.LogError(ex);
        }
    }

    /// <summary>
    /// Waits for any of the specified wait handles to be signaled asynchronously.
    /// </summary>
    /// <param name="waitHandles">The wait handles to monitor for signaling.</param>
    /// <returns>A task that completes when any of the wait handles is signaled.</returns>
    private static async Task WaitForAnyEventAsync(params WaitHandle[] waitHandles)
    {
        ArgumentNullException.ThrowIfNull(waitHandles);
        if (waitHandles.Length == 0)
        {
            throw new ArgumentException("At least one wait handle must be provided.", nameof(waitHandles));
        }
        if (waitHandles.Any(handle => handle == null))
        {
            throw new ArgumentException("All wait handles must be non-null.", nameof(waitHandles));
        }

        var taskCompletionSource = new TaskCompletionSource<int>();
        var waitRegistrations = new RegisteredWaitHandle[waitHandles.Length];

        try
        {
            for (var i = 0; i < waitHandles.Length; i++)
            {
                var index = i;
                waitRegistrations[i] = ThreadPool.RegisterWaitForSingleObject(
                    waitHandles[i],
                    (_, timedOut) =>
                    {
                        if (!timedOut)
                        {
                            taskCompletionSource.TrySetResult(index);
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
            for (var i = 0; i < waitRegistrations.Length; i++)
            {
                waitRegistrations[i]?.Unregister(null);
            }
        }
    }

}