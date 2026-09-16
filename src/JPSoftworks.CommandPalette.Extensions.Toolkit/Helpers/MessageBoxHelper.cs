// ------------------------------------------------------------
// 
// Copyright (c) Jiří Polášek. All rights reserved.
// 
// ------------------------------------------------------------

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Helpers;


public static partial class MessageBoxHelper
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, int type);

    /// <summary>Displays a message box and returns the selected button.</summary>
    /// <exception cref="Win32Exception">Windows could not display the message box.</exception>
    public static MessageBoxResult Show(string text, string caption, IconType iconType, MessageBoxType type)
    {
        var result = MessageBox(IntPtr.Zero, text, caption, (int)type | (int)iconType);
        if (result == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to display the message box.");
        }

        return (MessageBoxResult)result;
    }
}
