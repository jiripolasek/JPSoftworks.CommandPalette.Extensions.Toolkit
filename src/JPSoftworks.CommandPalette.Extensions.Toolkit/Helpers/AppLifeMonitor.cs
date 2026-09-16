// ------------------------------------------------------------
// 
// Copyright (c) Jiří Polášek. All rights reserved.
// 
// ------------------------------------------------------------

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Helpers;


/// <summary>
/// Monitors the application lifecycle events such as shutdown and end session.
/// </summary>
internal sealed partial class AppLifeMonitor : IDisposable
{
    public event EventHandler? ExitRequested;

    private static readonly Lazy<IntPtr> DefWindowProcAddress = new(() => PInvoke.GetProcAddress(PInvoke.GetModuleHandle("user32.dll"), "DefWindowProcW"));

    private readonly Lock _syncLock = new();
    private readonly TaskCompletionSource _initialization = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _shutdownCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string _className;
    private readonly TimeSpan _shutdownTimeout;

    private volatile bool _disposed;
    private nint _hwnd;
    private Thread? _messageLoopThread;
    private uint _nativeThreadId;
    private PInvoke.WindowProc? _windowProc;
    private ushort _classAtom;

    public AppLifeMonitor()
        : this($"AppLifeMonitor_{Guid.NewGuid():N}")
    {
    }

    internal AppLifeMonitor(string className)
        : this(className, TimeSpan.FromSeconds(4))
    {
    }

    internal AppLifeMonitor(string className, TimeSpan shutdownTimeout)
    {
        this._className = className;
        this._shutdownTimeout = shutdownTimeout;
    }

    /// <summary>Acknowledges host cleanup before the monitor thread is joined.</summary>
    internal void CompleteShutdown() => this._shutdownCompleted.TrySetResult();

    public void Dispose()
    {
        if (this._disposed)
        {
            return;
        }

        lock (this._syncLock)
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
            this.CompleteShutdown();

            if (this._messageLoopThread?.IsAlive == true)
            {
                PInvoke.PostThreadMessage(this._nativeThreadId, PInvoke.WM_QUIT, 0, 0);
                if (Thread.CurrentThread != this._messageLoopThread)
                {
                    this._messageLoopThread.Join(TimeSpan.FromSeconds(5));
                }
            }

            // CleanupWindow owns the callback until the native window and class are gone.
        }
    }



    public void StartMonitoring()
    {
        lock (this._syncLock)
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);

            if (this._messageLoopThread == null)
            {
                // MTA prevents the shutdown acknowledgement wait from dispatching nested messages.
                this._messageLoopThread = new(this.MessageLoopThread) { IsBackground = true, Name = "AppLifeMonitor Thread" };
                this._messageLoopThread.SetApartmentState(ApartmentState.MTA);
                this._messageLoopThread.Start();
            }

            this._initialization.Task.GetAwaiter().GetResult();
        }
    }



    private void MessageLoopThread()
    {
        var initialized = false;

        try
        {
            this._nativeThreadId = PInvoke.GetCurrentThreadId();
            this.InitializeWindow();
            initialized = true;
            this._initialization.SetResult();

            if (this._hwnd != nint.Zero)
            {
                while (true)
                {
                    // Include thread messages so Dispose can stop the loop with WM_QUIT.
                    var result = PInvoke.GetMessage(out PInvoke.MSG msg, nint.Zero, 0, 0);
                    if (result == nint.Zero)
                    {
                        break; // WM_QUIT received, exit the loop
                    }

                    if (result == -1)
                    {
                        break; // GetMessage failed
                    }

                    PInvoke.TranslateMessage(in msg);
                    PInvoke.DispatchMessage(in msg);
                }
            }
        }
        catch (Exception ex)
        {
            this._initialization.TrySetException(ex);
        }
        finally
        {
            try
            {
                this.CleanupWindow();
            }
            finally
            {
                if (initialized)
                {
                    this.SignalTermination();
                }
            }
        }
    }



    private void InitializeWindow()
    {
        this._windowProc = this.WndProc;
        var wndClass = new PInvoke.WndClass { lpfnWndProc = this._windowProc, hInstance = PInvoke.GetModuleHandle(null), lpszClassName = this._className };

        this._classAtom = PInvoke.RegisterClass(in wndClass);
        if (this._classAtom == 0)
        {
            var error = Marshal.GetLastPInvokeError();
            this._windowProc = null;
            throw new Win32Exception(error, "Failed to register the application lifetime monitor window class.");
        }

        // Message-only windows do not receive session shutdown messages.
        this._hwnd = PInvoke.CreateWindowEx(0, this._className, "AppLifeMonitor", PInvoke.WS_POPUP, 0, 0, 0, 0, 0, nint.Zero, wndClass.hInstance, nint.Zero);
        if (this._hwnd == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to create the application lifetime monitor window.");
        }
    }



    private void CleanupWindow()
    {
        var hwnd = this._hwnd;
        this._hwnd = nint.Zero;

        // prevent the window from calling our WndProc
        if (PInvoke.IsWindow(hwnd) && DefWindowProcAddress.Value != IntPtr.Zero)
        {
            PInvoke.SetWindowLongPtr(hwnd, PInvoke.GWL_WNDPROC, DefWindowProcAddress.Value);
            PInvoke.SetClassLongPtr(hwnd, PInvoke.GCL_WNDPROC, DefWindowProcAddress.Value);
        }

        // Keep the callback rooted if native teardown fails.
        if (PInvoke.IsWindow(hwnd) && !PInvoke.DestroyWindow(hwnd))
        {
            return;
        }

        if (this._classAtom != 0 && PInvoke.UnregisterClass(this._className, PInvoke.GetModuleHandle(null)))
        {
            this._classAtom = 0;
            this._windowProc = null;
        }
    }



    private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case PInvoke.WM_CLOSE:
                if (this._hwnd != nint.Zero)
                {
                    PInvoke.DestroyWindow(this._hwnd);
                    this._hwnd = nint.Zero;
                }

                break;

            case PInvoke.WM_DESTROY:
                PInvoke.PostQuitMessage(0);
                this._hwnd = nint.Zero;
                break;

            case PInvoke.WM_QUERYENDSESSION:
                // TODO: implement callback to allow application to veto the shutdown
                break;

            case PInvoke.WM_ENDSESSION:
                if (wParam != nint.Zero)
                {
                    this.SignalTermination();
                    this._shutdownCompleted.Task.Wait(this._shutdownTimeout);
                }

                return 0;
        }

        return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
    }



    private void SignalTermination()
    {
        if (this._messageLoopThread != null && !this._disposed)
        {
            this.ExitRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}