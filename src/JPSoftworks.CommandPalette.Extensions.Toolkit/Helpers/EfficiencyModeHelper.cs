// ------------------------------------------------------------
// 
// Copyright (c) Jiří Polášek. All rights reserved.
// 
// ------------------------------------------------------------

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Helpers;

/// <summary>
/// This class provides methods to enable Windows Efficiency Mode (EcoQoS) for the current process.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static partial class EfficiencyModeHelper
{
    internal sealed partial class PInvoke
    {
        internal const int ProcessPowerThrottling = 4;
        internal const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [LibraryImport("kernel32.dll", SetLastError = true)]
        internal static partial IntPtr GetCurrentProcess();

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetProcessInformation(
            IntPtr hProcess,
            int ProcessInformationClass,
            ref PROCESS_POWER_THROTTLING_STATE ProcessInformation,
            int ProcessInformationSize);
    }

    /// <summary>
    /// Enables Windows Efficiency Mode (EcoQoS) for the current process.
    /// Throws an exception on failure.
    /// </summary>
    private static void EnableProcessEfficiencyMode()
    {
        if (!IsSupported())
            throw new PlatformNotSupportedException("Efficiency Mode (EcoQoS) is supported on Windows 11 (build 22000+) only.");

        var process = PInvoke.GetCurrentProcess();
        var ecoQosState = new PInvoke.PROCESS_POWER_THROTTLING_STATE
        {
            Version = 1,
            ControlMask = PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            StateMask = PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED
        };

        var success = PInvoke.SetProcessInformation(
            process,
            PInvoke.ProcessPowerThrottling,
            ref ecoQosState,
            Marshal.SizeOf<PInvoke.PROCESS_POWER_THROTTLING_STATE>());

        if (!success)
        {
            var win32Error = Marshal.GetLastWin32Error();
            throw new Win32Exception(win32Error, "Failed to enable Efficiency Mode (EcoQoS) for process.");
        }
    }

    public static bool TryEnableProcessEfficiencyMode()
    {
        try
        {
            EnableProcessEfficiencyMode();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns true if the current OS is Windows 11 (build 22000) or newer.
    /// </summary>
    private static bool IsSupported()
    {
        return Environment.OSVersion is { Platform: PlatformID.Win32NT, Version: { Major: >= 10, Build: >= 22000 } };
    }
}