// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Identifies the severity of an extension host diagnostic entry.
/// </summary>
public enum ExtensionHostLogLevel
{
    /// <summary>
    /// Detailed information useful while diagnosing an extension.
    /// </summary>
    Debug,

    /// <summary>
    /// General information about normal extension host operation.
    /// </summary>
    Information,

    /// <summary>
    /// A recoverable or potentially problematic condition.
    /// </summary>
    Warning,

    /// <summary>
    /// A failure that prevented an operation from completing.
    /// </summary>
    Error,
}