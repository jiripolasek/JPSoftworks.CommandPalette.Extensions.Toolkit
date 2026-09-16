// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.ComponentModel;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Helpers;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Test;

public sealed class MessageBoxHelperTests
{
    [Fact]
    public void NativeFailureThrowsWithTheOriginalError()
    {
        // An invalid button type fails before Windows displays a dialog.
        var exception = Assert.Throws<Win32Exception>(() => MessageBoxHelper.Show(
            "Toolkit helper test",
            "Toolkit helper test",
            MessageBoxHelper.IconType.Info,
            (MessageBoxHelper.MessageBoxType)15));

        Assert.Equal(1438, exception.NativeErrorCode);
    }
}