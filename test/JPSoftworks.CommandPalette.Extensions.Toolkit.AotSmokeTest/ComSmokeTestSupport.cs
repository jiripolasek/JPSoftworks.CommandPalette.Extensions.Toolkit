// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Runtime.InteropServices;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.AotSmokeTest;

internal static partial class ComSmokeTestSupport
{
    internal static void RunInMta(Action test)
    {
        Marshal.ThrowExceptionForHR(CoInitializeEx(0, 0));
        try
        {
            test();
        }
        finally
        {
            CoUninitialize();
        }
    }

    internal static unsafe void DisposeExtension(nint extension)
    {
        var dispose = (delegate* unmanaged[Stdcall]<nint, int>)(*(nint**)extension)[7];
        Marshal.ThrowExceptionForHR(dispose(extension));
    }

    internal static unsafe nint ActivateExtension(Guid clsid, Guid iid)
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

    internal static void Release(nint instance)
    {
        if (instance != 0)
        {
            Marshal.Release(instance);
        }
    }

    internal static void Require(bool condition, string message)
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
    private static partial int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint instance);
}