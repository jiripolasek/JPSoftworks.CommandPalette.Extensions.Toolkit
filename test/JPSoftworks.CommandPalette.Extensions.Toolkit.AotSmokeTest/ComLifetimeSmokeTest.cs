// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Runtime.InteropServices;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using Microsoft.CommandPalette.Extensions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.AotSmokeTest;

internal static partial class ComLifetimeSmokeTest
{
    internal static Task RunAsync(bool useLegacyFactory)
    {
        Marshal.ThrowExceptionForHR(CoInitializeEx(0, 0));
        try
        {
            return ExerciseServer(useLegacyFactory).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            CoUninitialize();
        }
    }

    private static unsafe Task ExerciseServer(bool useLegacyFactory)
    {
        var creations = 0;
        var disposals = 0;
        var providerRequests = 0;
        var parameters = new ExtensionHostRunnerParameters
        {
            PublisherMoniker = "JPSoftworks",
            ProductMoniker = "ComLifetimeSmokeTest",
            EnableEfficiencyMode = false,
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

        var builder = ExtensionHostRunner.CreateBuilder(["-RegisterProcessAsComServer"], parameters)
            .ClearDefaultLogSinks()
            .AddLogSink(new DelegateExtensionHostLogSink(entry => Console.WriteLine(entry.Message)));
        if (!useLegacyFactory)
        {
            builder.AddHostedExtensionFactory(context => CreateExtension(context.ExtensionDisposedEvent));
        }

        var running = builder.RunAsync();
        Require(!running.IsCompleted, "The COM server exited before activation.");

        var clsid = typeof(LifetimeSmokeExtension).GUID;
        var iid = typeof(IExtension).GUID;
        var factoryIid = new Guid("00000001-0000-0000-C000-000000000046");
        nint factory = 0;
        nint first = 0;
        nint second = 0;
        nint third = 0;
        try
        {
            Marshal.ThrowExceptionForHR(CoGetClassObject(in clsid, 4, 0, in factoryIid, out factory));
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

        return running;
    }

    private static unsafe void DisposeExtension(nint extension)
    {
        var dispose = (delegate* unmanaged[Stdcall]<nint, int>)(*(nint**)extension)[7];
        Marshal.ThrowExceptionForHR(dispose(extension));
    }

    private static unsafe nint ActivateExtension(Guid clsid, Guid iid)
    {
        // Activate through IUnknown so this unpackaged test needs no WinRT interface registration.
        var unknownIid = new Guid("00000000-0000-0000-C000-000000000046");
        Marshal.ThrowExceptionForHR(CoCreateInstance(in clsid, 0, 4, in unknownIid, out var unknown));
        try
        {
            nint extension = 0;
            var queryInterface = (delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)(*(nint**)unknown)[0];
            Marshal.ThrowExceptionForHR(queryInterface(unknown, &iid, &extension));
            return extension;
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    private static void Release(nint instance)
    {
        if (instance != 0)
        {
            Marshal.Release(instance);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(nint reserved, uint concurrencyModel);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    private static partial int CoGetClassObject(in Guid clsid, uint context, nint reserved, in Guid iid, out nint instance);

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint instance);
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