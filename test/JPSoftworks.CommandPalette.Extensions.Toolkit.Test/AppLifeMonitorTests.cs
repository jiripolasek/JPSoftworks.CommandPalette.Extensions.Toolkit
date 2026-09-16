// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.ComponentModel;
using System.Runtime.InteropServices;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Helpers;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Test;

public sealed class AppLifeMonitorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task RegistrationFailureReachesTheCallerWithoutRequestingExit()
    {
        var className = NewClassName();
        using var owner = new AppLifeMonitor(className);
        owner.StartMonitoring();
        var ownerWindow = FindWindow(className, null);
        Assert.NotEqual(nint.Zero, ownerWindow);
        using var failing = new AppLifeMonitor(className);
        var exits = 0;
        failing.ExitRequested += (_, _) => Interlocked.Increment(ref exits);

        var exception = await Assert.ThrowsAsync<Win32Exception>(
            () => Task.Run(failing.StartMonitoring).WaitAsync(Timeout));
        Assert.Equal(1410, exception.NativeErrorCode);
        Assert.Equal(1410, Assert.Throws<Win32Exception>(failing.StartMonitoring).NativeErrorCode);
        failing.Dispose();

        Assert.Equal(0, Volatile.Read(ref exits));
        Assert.Equal(ownerWindow, FindWindow(className, null));
    }

    [Fact]
    public void DisposalReleasesTheWindowAndClassWithoutRequestingExit()
    {
        var className = NewClassName();
        var exits = 0;
        using (var monitor = new AppLifeMonitor(className))
        {
            monitor.ExitRequested += (_, _) => Interlocked.Increment(ref exits);
            monitor.StartMonitoring();
            var hwnd = FindWindow(className, null);
            Assert.NotEqual(nint.Zero, hwnd);
            monitor.StartMonitoring();
            Assert.Equal(hwnd, FindWindow(className, null));
            monitor.Dispose();
            monitor.Dispose();
            Assert.False(AppLifeMonitor.PInvoke.IsWindow(hwnd));
            Assert.Throws<ObjectDisposedException>(monitor.StartMonitoring);
        }

        Assert.Equal(0, Volatile.Read(ref exits));
        using var replacement = new AppLifeMonitor(className);
        replacement.StartMonitoring();
        Assert.NotEqual(nint.Zero, FindWindow(className, null));
    }

    [Fact]
    public async Task WindowCloseRequestsExitAndReleasesItsClass()
    {
        var className = NewClassName();
        var exited = NewSignal();
        using (var monitor = new AppLifeMonitor(className))
        {
            monitor.ExitRequested += (_, _) => exited.TrySetResult();
            monitor.StartMonitoring();
            var hwnd = FindWindow(className, null);
            Assert.NotEqual(nint.Zero, hwnd);
            Assert.True(AppLifeMonitor.PInvoke.PostMessage(hwnd, AppLifeMonitor.PInvoke.WM_CLOSE, 0, 0));
            await exited.Task.WaitAsync(Timeout);
        }

        using var replacement = new AppLifeMonitor(className);
        replacement.StartMonitoring();
        Assert.NotEqual(nint.Zero, FindWindow(className, null));
    }

    [Fact]
    public void CanceledSessionKeepsMonitoringAndConfirmedSessionRequestsExit()
    {
        var className = NewClassName();
        using var monitor = new AppLifeMonitor(className);
        var exits = 0;
        monitor.ExitRequested += (_, _) =>
        {
            Interlocked.Increment(ref exits);
            monitor.CompleteShutdown();
        };
        monitor.StartMonitoring();
        var hwnd = FindWindow(className, null);
        Assert.NotEqual(nint.Zero, hwnd);

        Assert.Equal((nuint)1, Send(hwnd, AppLifeMonitor.PInvoke.WM_QUERYENDSESSION, 0));
        Send(hwnd, AppLifeMonitor.PInvoke.WM_ENDSESSION, 0);
        Assert.Equal(0, Volatile.Read(ref exits));
        Assert.True(AppLifeMonitor.PInvoke.IsWindow(hwnd));

        Send(hwnd, AppLifeMonitor.PInvoke.WM_ENDSESSION, 1);
        Assert.Equal(1, Volatile.Read(ref exits));
    }

    [Fact]
    public void ProcedureSettersReturnTheFullPreviousPointer()
    {
        var className = NewClassName();
        using var monitor = new AppLifeMonitor(className);
        monitor.StartMonitoring();
        var hwnd = FindWindow(className, null);
        Assert.NotEqual(nint.Zero, hwnd);
        var windowProc = GetWindowLongPtr(hwnd, AppLifeMonitor.PInvoke.GWL_WNDPROC);
        var classProc = GetClassLongPtr(hwnd, AppLifeMonitor.PInvoke.GCL_WNDPROC);

        Assert.NotEqual(nint.Zero, windowProc);
        Assert.NotEqual(nuint.Zero, classProc);
        Assert.Equal(windowProc, AppLifeMonitor.PInvoke.SetWindowLongPtr(hwnd, AppLifeMonitor.PInvoke.GWL_WNDPROC, windowProc));
        Assert.Equal(classProc, AppLifeMonitor.PInvoke.SetClassLongPtr(hwnd, AppLifeMonitor.PInvoke.GCL_WNDPROC, (nint)classProc));
    }

    [Fact]
    public async Task ConfirmedSessionWaitsForShutdownAcknowledgement()
    {
        var className = NewClassName();
        using var monitor = new AppLifeMonitor(className);
        var exited = NewSignal();
        monitor.ExitRequested += (_, _) => exited.TrySetResult();
        monitor.StartMonitoring();
        var hwnd = FindWindow(className, null);
        var message = Task.Run(() => Send(hwnd, AppLifeMonitor.PInvoke.WM_ENDSESSION, 1));
        try
        {
            await exited.Task.WaitAsync(Timeout);
            await Task.WhenAny(message, Task.Delay(TimeSpan.FromMilliseconds(100)));
            Assert.False(message.IsCompleted);
        }
        finally
        {
            monitor.CompleteShutdown();
        }

        Assert.Equal(nuint.Zero, await message.WaitAsync(Timeout));
    }

    [Theory]
    [InlineData(AppLifeMonitor.PInvoke.WM_CLOSE)]
    [InlineData(AppLifeMonitor.PInvoke.WM_ENDSESSION)]
    public async Task ShutdownAcknowledgementDoesNotDispatchNestedShutdownMessages(uint nestedMessage)
    {
        var className = NewClassName();
        using var monitor = new AppLifeMonitor(className);
        var exited = NewSignal();
        var exits = 0;
        monitor.ExitRequested += (_, _) =>
        {
            Interlocked.Increment(ref exits);
            exited.TrySetResult();
        };
        monitor.StartMonitoring();
        var hwnd = FindWindow(className, null);
        var message = Task.Run(() => Send(hwnd, AppLifeMonitor.PInvoke.WM_ENDSESSION, 1));
        try
        {
            await exited.Task.WaitAsync(Timeout);
            Assert.Equal(nint.Zero, SendMessageTimeout(hwnd, nestedMessage, 1, 0, 2, 200, out _));
            Assert.True(AppLifeMonitor.PInvoke.IsWindow(hwnd));
            Assert.Equal(1, Volatile.Read(ref exits));
            Assert.False(message.IsCompleted);
        }
        finally
        {
            monitor.CompleteShutdown();
            await message.WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task ShutdownAcknowledgementWaitIsBounded()
    {
        var className = NewClassName();
        using var monitor = new AppLifeMonitor(className, TimeSpan.FromMilliseconds(50));
        monitor.StartMonitoring();
        var hwnd = FindWindow(className, null);

        var message = Task.Run(() => Send(hwnd, AppLifeMonitor.PInvoke.WM_ENDSESSION, 1));

        Assert.Equal(nuint.Zero, await message.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task DisposalReleasesPendingShutdownAcknowledgement()
    {
        var className = NewClassName();
        using var monitor = new AppLifeMonitor(className);
        var exited = NewSignal();
        monitor.ExitRequested += (_, _) => exited.TrySetResult();
        monitor.StartMonitoring();
        var hwnd = FindWindow(className, null);
        var message = Task.Run(() => Send(hwnd, AppLifeMonitor.PInvoke.WM_ENDSESSION, 1));
        await exited.Task.WaitAsync(Timeout);

        await Task.Run(monitor.Dispose).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(nuint.Zero, await message.WaitAsync(Timeout));
        Assert.False(AppLifeMonitor.PInvoke.IsWindow(hwnd));
    }

    private static string NewClassName() => $"AppLifeMonitorTest_{Guid.NewGuid():N}";

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static nuint Send(nint hwnd, uint message, nint wParam)
    {
        Assert.NotEqual(nint.Zero, SendMessageTimeout(hwnd, message, wParam, 0, 2, 5000, out var result));
        return result;
    }

    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string className, string? title);

    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static extern nint SendMessageTimeout(nint hwnd, uint message, nint wParam, nint lParam, uint flags, uint timeout, out nuint result);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static extern nuint GetClassLongPtr(nint hwnd, int index);
}