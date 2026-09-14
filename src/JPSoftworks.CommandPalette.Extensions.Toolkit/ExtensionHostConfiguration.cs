// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Represents the immutable configuration for one extension host run.
/// </summary>
public sealed class ExtensionHostConfiguration
{
    private const string LogFileName = "log.txt";

    private readonly IHostedExtensionFactory[] _hostedExtensionFactories;
#pragma warning disable CS0618 // Snapshot the compatibility contract.
    private readonly IExtensionFactory[] _extensionFactories;
#pragma warning restore CS0618

    /// <summary>
    /// Initializes an extension host configuration from explicitly supplied values.
    /// </summary>
    /// <param name="arguments">Command-line arguments passed to the application.</param>
    /// <param name="parameters">Configuration parameters for running the server.</param>
    /// <param name="logFilePath">The base path used by the Toolkit's daily file diagnostics.</param>
    /// <remarks>
    /// This constructor uses <see cref="ExtensionHostRunnerParameters.IsDebug"/> and
    /// <paramref name="logFilePath"/> exactly as supplied. Use <see cref="Resolve"/> to apply the Toolkit's
    /// <c>-Debug</c> convention and canonical local-application-data log path.
    /// </remarks>
    public ExtensionHostConfiguration(
        IEnumerable<string> arguments,
        ExtensionHostRunnerParameters parameters,
        string logFilePath)
        : this(arguments, parameters, parameters?.IsDebug ?? false, logFilePath)
    {
    }

    private ExtensionHostConfiguration(
        IEnumerable<string> arguments,
        ExtensionHostRunnerParameters parameters,
        bool isDebug,
        string logFilePath)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameters.HostedExtensionFactories);
#pragma warning disable CS0618 // Validate the compatibility contract.
        ArgumentNullException.ThrowIfNull(parameters.ExtensionFactories);
#pragma warning restore CS0618
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

        ValidatePathSegment(parameters.PublisherMoniker, nameof(parameters.PublisherMoniker));
        ValidatePathSegment(parameters.ProductMoniker, nameof(parameters.ProductMoniker));

        this.Arguments = Array.AsReadOnly<string>([.. arguments]);
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

    /// <summary>
    /// Gets a snapshot of the command-line arguments for this host run.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets whether debug diagnostics are enabled for this host run.
    /// </summary>
    public bool IsDebug { get; }

    /// <summary>
    /// Gets the base path used by the Toolkit's daily file diagnostics.
    /// </summary>
    public string LogFilePath { get; }

    /// <summary>
    /// Gets the non-display, path-safe publisher identifier.
    /// </summary>
    public string PublisherMoniker { get; }

    /// <summary>
    /// Gets the non-display, path-safe product identifier.
    /// </summary>
    public string ProductMoniker { get; }

    /// <summary>
    /// Gets whether the host lowers process priority and enables Efficiency Mode.
    /// </summary>
    public bool EnableEfficiencyMode { get; }

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
