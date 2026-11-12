// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Runtime.CompilerServices;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog;

internal static class SerilogLoggingBridgeMarker
{
    public const string PropertyName = "JPSoftworksExtensionHostBridge";

    private static readonly ConditionalWeakTable<ExtensionLogEntry, object> MarkedEntries = new();

    public static bool IsMarked(ExtensionLogEntry entry)
    {
        return MarkedEntries.TryGetValue(entry, out _);
    }

    public static void Mark(ExtensionLogEntry entry)
    {
        _ = MarkedEntries.GetValue(entry, static _ => new object());
    }
}
