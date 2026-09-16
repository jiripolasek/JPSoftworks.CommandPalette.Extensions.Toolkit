// ------------------------------------------------------------
// 
// Copyright (c) Jiří Polášek. All rights reserved.
// 
// ------------------------------------------------------------

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Helpers;

internal static partial class ShutdownHelper
{
    [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local", Justification = "Allow use of Win32 naming conventions")]
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Allow use of Win32 naming conventions")]
    internal static partial class PInvoke
    {
        internal const uint SHUTDOWN_NORETRY = 0x00000001;

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetProcessShutdownParameters(uint dwLevel, uint dwFlags);
    }



    public static void SetShutdownPriority(int level)
    {
        if (level is < 0x100 or > 0x3FF)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Shutdown priority level must be between 0x100 and 0x3FF.");
        }
        if (!PInvoke.SetProcessShutdownParameters((uint)level, PInvoke.SHUTDOWN_NORETRY))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to set the process shutdown priority.");
        }
    }
}