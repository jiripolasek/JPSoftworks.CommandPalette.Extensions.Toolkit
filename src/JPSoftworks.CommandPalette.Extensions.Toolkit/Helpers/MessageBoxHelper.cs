// ------------------------------------------------------------
// 
// Copyright (c) Jiří Polášek. All rights reserved.
// 
// ------------------------------------------------------------

using System.Runtime.InteropServices;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Helpers;


public static partial class MessageBoxHelper
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, int type);

    public static MessageBoxResult Show(string text, string caption, IconType iconType, MessageBoxType type)
    {
        return (MessageBoxResult)MessageBox(IntPtr.Zero, text, caption, (int)type | (int)iconType);
    }
}
