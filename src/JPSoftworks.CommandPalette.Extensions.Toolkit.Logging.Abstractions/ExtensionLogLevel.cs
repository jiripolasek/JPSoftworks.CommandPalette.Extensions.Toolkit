// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

/// <summary>
/// Identifies the severity of an extension log entry.
/// </summary>
public enum ExtensionLogLevel
{
    /// <summary>
    /// Detailed information useful while diagnosing an extension.
    /// </summary>
    Debug,

    /// <summary>
    /// General information about normal extension operation.
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

    /// <summary>
    /// A failure that requires immediate attention, such as one that stops the extension or the host.
    /// </summary>
    Critical,
}
