// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.InteropServices;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using Microsoft.CommandPalette.Extensions;
using static JPSoftworks.CommandPalette.Extensions.Toolkit.AotSmokeTest.ComSmokeTestSupport;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.AotSmokeTest;

internal static partial class ComLifetimeSmokeTest
{
    internal static void Run(bool useLegacyFactory, bool enableEfficiencyMode, bool useExplicitClassId)
    {
        RunInMta(() => ExerciseServer(useLegacyFactory, enableEfficiencyMode, useExplicitClassId)
            .WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult());
    }

    private static unsafe Task ExerciseServer(bool useLegacyFactory, bool enableEfficiencyMode, bool useExplicitClassId)
    {
        var creations = 0;
        var unusedCreations = 0;
        var disposals = 0;
        var providerRequests = 0;
        var clsid = useExplicitClassId
            ? new Guid("A670B3D0-6802-4162-977C-FD6843B9F2C0")
            : typeof(LifetimeSmokeExtension).GUID;
        var parameters = new ExtensionHostRunnerParameters
        {
            PublisherMoniker = "JPSoftworks",
            ProductMoniker = "ComLifetimeSmokeTest",
            EnableEfficiencyMode = enableEfficiencyMode,
        };

        IExtension CreateExtension(ManualResetEvent disposedEvent)
        {
            creations++;
            return new LifetimeSmokeExtension(
                () =>
                {
                    disposals++;
                    if (useLegacyFactory)
                    {
                        disposedEvent.Set();
                    }
                },
                () => providerRequests++);
        }

        if (useLegacyFactory)
        {
#pragma warning disable CS0618 // Exercise the event-only compatibility path through COM.
            parameters.ExtensionFactories.Add(new DelegateExtensionFactory(CreateExtension));
#pragma warning restore CS0618
        }

        var hostedFactory = new LifetimeSmokeFactory(context => CreateExtension(context.ExtensionDisposedEvent));
        if (!useLegacyFactory && !useExplicitClassId)
        {
            parameters.HostedExtensionFactories.Add(hostedFactory);
        }

        var builder = ExtensionHostRunner.CreateBuilder(["-RegisterProcessAsComServer"], parameters)
            .ClearDefaultLogSinks()
            .AddLogSink(new DelegateExtensionHostLogSink(entry => Console.WriteLine(entry.Message)));
        if (useExplicitClassId)
        {
            builder.AddHostedExtensionFactory(
                clsid,
                hostedFactory.CreateExtension);
            builder.AddHostedExtensionFactory(typeof(SmokeTestExtension).GUID, _ =>
            {
                unusedCreations++;
                return new SmokeTestExtension();
            });
        }

        var running = builder.RunAsync();
        Require(!running.IsCompleted, "The COM server exited before activation.");
        Require(creations == (useExplicitClassId ? 0 : 1), "Registration did not honor the class ID policy.");
        Require(unusedCreations == 0, "Registration created an unused extension.");
        Require(GetProcessShutdownParameters(out var shutdownLevel, out var shutdownFlags)
            && shutdownLevel == 0x200 && shutdownFlags == 1, "The process shutdown priority was not applied.");
        if (enableEfficiencyMode)
        {
            using var process = Process.GetCurrentProcess();
            try
            {
                Require(process.PriorityClass == ProcessPriorityClass.Idle, "The process priority was not lowered.");
            }
            finally
            {
                // Do not run the timeout-based COM checks at Idle priority.
                process.PriorityClass = ProcessPriorityClass.Normal;
            }
        }

        var iid = typeof(IExtension).GUID;
        var factoryIid = new Guid("00000001-0000-0000-C000-000000000046");
        nint factory = 0;
        nint first = 0;
        nint second = 0;
        nint third = 0;
        try
        {
            Marshal.ThrowExceptionForHR(CoGetClassObject(in clsid, 4, 0, in factoryIid, out factory));
            Require(creations == (useExplicitClassId ? 0 : 1), "Getting the class factory created an extension.");
            first = ActivateExtension(clsid, iid);
            second = ActivateExtension(clsid, iid);
            Require(first != second && creations == 2, "COM reused an extension instance.");

            nint provider = 0;
            var getProvider = (delegate* unmanaged[Stdcall]<nint, ProviderType, nint*, int>)(*(nint**)first)[6];
            Marshal.ThrowExceptionForHR(getProvider(first, ProviderType.Commands, &provider));
            Require(provider == 0 && providerRequests == 1, "The wrapper did not forward GetProvider.");

            DisposeExtension(first);
            Require(disposals == 1, "COM disposal did not reach the extension.");
            var create = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)(*(nint**)factory)[3];
            Marshal.ThrowExceptionForHR(create(factory, 0, &iid, &third));
            Require(creations == 3 && !running.IsCompleted, "Disposing one instance shut down a live server.");

            DisposeExtension(second);
            DisposeExtension(third);
            Require(disposals == 3, "The final COM disposal did not complete synchronously.");
            nint rejected = 0;
            var result = create(factory, 0, &iid, &rejected);
            Require(result == unchecked((int)0x80040111) && rejected == 0, "The draining class factory accepted activation.");
            DisposeExtension(third);
            Require(disposals == 3, "Repeated COM disposal reached the extension twice.");
        }
        finally
        {
            Release(third);
            Release(second);
            Release(first);
            Release(factory);
        }

        Require(unusedCreations == 0, "The server created an unused extension.");
        return running;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessShutdownParameters(out uint level, out uint flags);

    [LibraryImport("ole32.dll")]
    private static partial int CoGetClassObject(in Guid clsid, uint context, nint reserved, in Guid iid, out nint instance);
}

internal sealed class LifetimeSmokeFactory(Func<ExtensionHostContext, IExtension> createExtension) : IHostedExtensionFactory
{
    public IExtension CreateExtension(ExtensionHostContext context) => createExtension(context);
}

[Guid("949291B4-C07F-4D34-85C0-069DC49D62ED")]
internal sealed partial class LifetimeSmokeExtension(Action dispose, Action getProvider) : IExtension
{
    public object GetProvider(ProviderType providerType)
    {
        getProvider();
        return null!;
    }

    public void Dispose() => dispose();
}