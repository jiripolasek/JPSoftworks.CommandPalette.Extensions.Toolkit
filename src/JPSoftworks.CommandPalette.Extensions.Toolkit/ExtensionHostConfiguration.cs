// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Represents the immutable, resolved configuration for one extension host run.
/// </summary>
public sealed class ExtensionHostConfiguration
{
    private const string LogFileName = "log.txt";

    private readonly IHostedExtensionFactory[] _hostedExtensionFactories;
#pragma warning disable CS0618 // Snapshot the compatibility contract.
    private readonly IExtensionFactory[] _extensionFactories;
#pragma warning restore CS0618

    private ExtensionHostConfiguration(
        string[] arguments,
        ExtensionHostRunnerParameters parameters,
        bool isDebug,
        string logFilePath)
    {
        this.Arguments = arguments;
        this.PublisherMoniker = parameters.PublisherMoniker;
        this.ProductMoniker = parameters.ProductMoniker;
        this.EnableEfficiencyMode = parameters.EnableEfficiencyMode;
        this._hostedExtensionFactories = [.. parameters.HostedExtensionFactories];
#pragma warning disable CS0618 // Snapshot the compatibility contract.
        this._extensionFactories = [.. parameters.ExtensionFactories];
#pragma warning restore CS0618
        this.IsDebug = isDebug;
        this.LogFilePath = logFilePath;
    }

    internal string[] Arguments { get; }

    internal bool IsDebug { get; }

    internal string LogFilePath { get; }

    internal string PublisherMoniker { get; }

    internal string ProductMoniker { get; }

    internal bool EnableEfficiencyMode { get; }

    /// <summary>
    /// Resolves command-line and runner parameters into one immutable host configuration.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    /// <param name="parameters">Configuration parameters for running the server.</param>
    /// <returns>The resolved configuration for the host run.</returns>
    public static ExtensionHostConfiguration Resolve(
        string[] args,
        ExtensionHostRunnerParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameters.HostedExtensionFactories);
#pragma warning disable CS0618 // Validate the compatibility contract.
        ArgumentNullException.ThrowIfNull(parameters.ExtensionFactories);
#pragma warning restore CS0618

        ValidatePathSegment(parameters.PublisherMoniker, nameof(parameters.PublisherMoniker));
        ValidatePathSegment(parameters.ProductMoniker, nameof(parameters.ProductMoniker));

        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException("The local application data directory is unavailable.");
        }

        var logDirectoryPath = Path.GetFullPath(
            Path.Combine(
                localApplicationData,
                parameters.PublisherMoniker,
                parameters.ProductMoniker));

        return new ExtensionHostConfiguration(
            [.. args],
            parameters,
            parameters.IsDebug || args.Contains("-Debug", StringComparer.Ordinal),
            Path.Combine(logDirectoryPath, LogFileName));
    }

    internal ExtensionHostRunnerParameters CreateRunnerParameters()
    {
        return new ExtensionHostRunnerParameters
        {
            PublisherMoniker = this.PublisherMoniker,
            ProductMoniker = this.ProductMoniker,
            EnableEfficiencyMode = this.EnableEfficiencyMode,
            IsDebug = this.IsDebug,
            HostedExtensionFactories = [.. this._hostedExtensionFactories],
#pragma warning disable CS0618 // Recreate the compatibility contract for the runner.
            ExtensionFactories = [.. this._extensionFactories],
#pragma warning restore CS0618
        };
    }

    private static void ValidatePathSegment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        if (value is "." or ".."
            || Path.IsPathRooted(value)
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || value.Contains(Path.DirectorySeparatorChar)
            || value.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException(
                "The moniker must be a single valid path segment.",
                parameterName);
        }
    }
}
