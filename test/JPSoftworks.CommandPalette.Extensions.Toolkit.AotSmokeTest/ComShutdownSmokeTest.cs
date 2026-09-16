// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using Microsoft.CommandPalette.Extensions;
using static JPSoftworks.CommandPalette.Extensions.Toolkit.AotSmokeTest.ComSmokeTestSupport;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.AotSmokeTest;

internal static partial class ComShutdownSmokeTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    internal static void Run(bool useLegacyFactory, bool endSession, bool throwOnDispose)
    {
        RunInMta(() => ExerciseServer(useLegacyFactory, endSession, throwOnDispose));
    }

    private static void ExerciseServer(bool useLegacyFactory, bool endSession, bool throwOnDispose)
    {
        using var disposing = new ManualResetEventSlim();
        using var finishDisposal = new ManualResetEventSlim();
        var disposals = 0;
        var errors = 0;
        var diagnosticsCompleted = 0;
        var parameters = new ExtensionHostRunnerParameters
        {
            PublisherMoniker = "JPSoftworks",
            ProductMoniker = "ComShutdownSmokeTest",
            EnableEfficiencyMode = false,
            IsDebug = true,
        };

        IExtension CreateExtension(ManualResetEvent disposedEvent) => new LifetimeSmokeExtension(() =>
        {
            var count = Interlocked.Increment(ref disposals);
            if (count == 1)
            {
                disposing.Set();
                Require(finishDisposal.Wait(Timeout), "The test did not release extension disposal.");
                if (throwOnDispose)
                {
                    throw new InvalidOperationException("Expected disposal failure.");
                }
            }

            if (useLegacyFactory)
            {
                disposedEvent.Set();
            }
        }, () => { });

        if (useLegacyFactory)
        {
#pragma warning disable CS0618 // Exercise forced teardown through the legacy factory contract.
            parameters.ExtensionFactories.Add(new DelegateExtensionFactory(CreateExtension));
#pragma warning restore CS0618
        }

        var builder = ExtensionHostRunner.CreateBuilder(["-RegisterProcessAsComServer"], parameters)
            .ClearDefaultLogSinks()
            .AddLogSink(new DelegateExtensionHostLogSink(entry =>
            {
                if (entry.Exception != null)
                {
                    Interlocked.Increment(ref errors);
                }

                if (entry.Message == "Done")
                {
                    Volatile.Write(ref diagnosticsCompleted, 1);
                }
            }));
        if (!useLegacyFactory)
        {
            builder.AddHostedExtensionFactory(context => CreateExtension(context.ExtensionDisposedEvent));
        }

        var running = builder.RunAsync();
        var hwnd = FindMonitorWindow();
        nint first = 0;
        nint second = 0;
        Task? message = null;
        var completed = false;
        try
        {
            Require(!running.IsCompleted, "The COM server exited before activation.");
            Require(hwnd != 0, "The test process has no lifetime monitor window.");
            first = ActivateExtension(typeof(LifetimeSmokeExtension).GUID, typeof(IExtension).GUID);
            second = ActivateExtension(typeof(LifetimeSmokeExtension).GUID, typeof(IExtension).GUID);
            message = Task.Run(() =>
            {
                Require(SendMessageTimeout(hwnd, endSession ? 0x16u : 0x10u, endSession ? 1 : 0, 0, 2, 10000, out _) != 0,
                    "The shutdown message was not handled.");
            });
            Require(disposing.Wait(Timeout), "Forced shutdown did not dispose the active extensions.");
            Require(!running.IsCompleted, "The runner completed before extension disposal finished.");
            if (endSession)
            {
                Require(!message.Wait(TimeSpan.FromMilliseconds(100)), "WM_ENDSESSION returned before cleanup finished.");
                Require(SendMessageTimeout(hwnd, 0x10, 0, 0, 2, 200, out _) == 0,
                    "The shutdown wait dispatched a nested WM_CLOSE.");
                Require(FindMonitorWindow() == hwnd, "A nested WM_CLOSE destroyed the lifetime monitor.");
            }

            finishDisposal.Set();
            message.WaitAsync(Timeout).GetAwaiter().GetResult();
            if (endSession)
            {
                Require(Volatile.Read(ref diagnosticsCompleted) == 1, "WM_ENDSESSION returned before diagnostics completed.");
            }

            running.WaitAsync(Timeout).GetAwaiter().GetResult();
            Require(disposals == 2, "Forced shutdown did not dispose both active instances.");
            Require(errors == (throwOnDispose ? 1 : 0), "Disposal failures were not isolated and logged correctly.");
            DisposeExtension(first);
            DisposeExtension(second);
            Require(disposals == 2, "Late client disposal reached an extension twice.");
            completed = true;
        }
        finally
        {
            finishDisposal.Set();
            List<Exception> cleanupErrors = [];
            Cleanup(() =>
            {
                if (!running.IsCompleted)
                {
                    SendMessageTimeout(hwnd, 0x10, 0, 0, 2, 1000, out _);
                    running.WaitAsync(Timeout).GetAwaiter().GetResult();
                }
            });
            Cleanup(() => message?.WaitAsync(Timeout).GetAwaiter().GetResult());
            Cleanup(() => Release(second));
            Cleanup(() => Release(first));
            if (completed && cleanupErrors.Count != 0)
            {
                throw new AggregateException("COM smoke test cleanup failed.", cleanupErrors);
            }

            void Cleanup(Action cleanup)
            {
                try
                {
                    cleanup();
                }
                catch (Exception ex)
                {
                    cleanupErrors.Add(ex);
                    Console.Error.WriteLine($"COM smoke test cleanup failed: {ex}");
                }
            }
        }
    }

    private static unsafe nint FindMonitorWindow()
    {
        nint hwnd = 0;
        EnumWindows(&FindMonitor, (nint)(&hwnd));
        return hwnd;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe int FindMonitor(nint hwnd, nint state)
    {
        GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == Environment.ProcessId)
        {
            var name = stackalloc char[256];
            var length = GetClassName(hwnd, name, 256);
            if (new ReadOnlySpan<char>(name, length).StartsWith("AppLifeMonitor_", StringComparison.Ordinal))
            {
                *(nint*)state = hwnd;
                return 0;
            }
        }

        return 1;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool EnumWindows(delegate* unmanaged[Stdcall]<nint, nint, int> callback, nint state);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
    private static unsafe partial int GetClassName(nint hwnd, char* text, int count);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static partial nint SendMessageTimeout(nint hwnd, uint message, nint wParam, nint lParam, uint flags, uint timeout, out nuint result);
}