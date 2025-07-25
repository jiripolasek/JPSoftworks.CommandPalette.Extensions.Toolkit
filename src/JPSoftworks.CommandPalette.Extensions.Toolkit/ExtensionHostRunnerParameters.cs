// ------------------------------------------------------------
// 
// Copyright (c) Jiří Polášek. All rights reserved.
// 
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Parameters for running the self-contained extension server.
/// </summary>
public record ExtensionHostRunnerParameters
{
    /// <summary>
    /// Runs the server in debug mode.
    /// </summary>
    public bool IsDebug { get; init; }

    /// <summary>
    /// Non-display, unique, path-safe identifier for the publisher.
    /// </summary>
    public required string PublisherMoniker { get; init; }

    /// <summary>
    /// Non-display, unique, path-safe identifier for the publisher.
    /// </summary>
    public required string ProductMoniker { get; init; }

    /// <summary>
    /// Factories for creating extensions.
    /// </summary>
    public required List<IExtensionFactory> ExtensionFactories { get; init; } = [];

    /// <summary>
    /// Lowers the process priority and enables QoS Efficiency Mode for the process.
    /// </summary>
    public bool EnableEfficiencyMode { get; init; } = true;
}