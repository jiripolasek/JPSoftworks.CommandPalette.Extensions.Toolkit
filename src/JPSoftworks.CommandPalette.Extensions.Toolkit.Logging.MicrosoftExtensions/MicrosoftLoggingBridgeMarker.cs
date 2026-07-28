// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions;

internal static class MicrosoftLoggingBridgeMarker
{
    private static readonly ConditionalWeakTable<ExtensionHostLogEntry, object> MarkedEntries = new();

    public static bool IsMarked(ExtensionHostLogEntry entry)
    {
        return MarkedEntries.TryGetValue(entry, out _);
    }

    public static void Mark(ExtensionHostLogEntry entry)
    {
        _ = MarkedEntries.GetValue(entry, static _ => new object());
    }
}