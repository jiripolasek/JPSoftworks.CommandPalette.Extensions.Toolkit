// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
using Microsoft.CommandPalette.Extensions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Configures and runs an extension host while retaining the toolkit's default behavior unless explicitly replaced.
/// </summary>
public sealed class ExtensionHostRunnerBuilder : IExtensionHostLoggingBuilder
{
    private readonly ExtensionHostConfiguration _configuration;
    private readonly ExtensionHostRunnerParameters _parameters;
    private readonly List<HostedExtensionRegistration> _hostedExtensionRegistrations = [];
    private readonly List<IExtensionHostLogSink> _additionalLogSinks = [];

    private bool _includeDefaultLogSinks = true;
    private int _hasRun;

    internal ExtensionHostRunnerBuilder(
        ExtensionHostConfiguration configuration)
    {
        this._configuration = configuration;
        this._parameters = configuration.CreateRunnerParameters();
        foreach (var factory in this._parameters.HostedExtensionFactories)
        {
            this._hostedExtensionRegistrations.Add(new HostedExtensionRegistration(factory));
        }
    }

    /// <summary>
    /// Adds a hosted extension factory.
    /// </summary>
    /// <param name="factory">The factory to add.</param>
    /// <returns>This builder.</returns>
    public ExtensionHostRunnerBuilder AddHostedExtensionFactory(IHostedExtensionFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        this.ThrowIfAlreadyRun();
        this._hostedExtensionRegistrations.Add(new HostedExtensionRegistration(factory));
        return this;
    }

    /// <summary>
    /// Adds a delegate-backed hosted extension factory.
    /// </summary>
    /// <param name="createExtension">The function that creates an extension from its host context.</param>
    /// <returns>This builder.</returns>
    public ExtensionHostRunnerBuilder AddHostedExtensionFactory(
        Func<ExtensionHostContext, IExtension> createExtension)
    {
        ArgumentNullException.ThrowIfNull(createExtension);
        return this.AddHostedExtensionFactory(
            new DelegateHostedExtensionFactory(createExtension));
    }

    /// <summary>
    /// Adds a factory that creates extensions only when COM activates them.
    /// </summary>
    /// <param name="classId">The nonempty COM class ID to register, independent of the returned implementation type.</param>
    /// <param name="createExtension">The function that creates a new extension for each activation.</param>
    /// <returns>This builder.</returns>
    public ExtensionHostRunnerBuilder AddHostedExtensionFactory(
        Guid classId,
        Func<ExtensionHostContext, IExtension> createExtension)
    {
        ArgumentNullException.ThrowIfNull(createExtension);
        this.ThrowIfAlreadyRun();
        if (classId == Guid.Empty)
        {
            throw new ArgumentException("The COM class ID must not be empty.", nameof(classId));
        }

        this._hostedExtensionRegistrations.Add(new HostedExtensionRegistration(
            new DelegateHostedExtensionFactory(createExtension), classId));
        return this;
    }

    /// <summary>
    /// Adds a caller-owned sink while retaining the default sinks.
    /// </summary>
    /// <param name="sink">The sink that should receive extension host diagnostics.</param>
    /// <returns>This builder.</returns>
    /// <remarks>
    /// The runner does not dispose caller-supplied sinks.
    /// </remarks>
    public ExtensionHostRunnerBuilder AddLogSink(IExtensionHostLogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        this.ThrowIfAlreadyRun();
        this._additionalLogSinks.Add(sink);
        return this;
    }

    /// <summary>
    /// Removes the toolkit's daily file and Command Palette sinks.
    /// </summary>
    /// <returns>This builder.</returns>
    public ExtensionHostRunnerBuilder ClearDefaultLogSinks()
    {
        this.ThrowIfAlreadyRun();
        this._includeDefaultLogSinks = false;
        return this;
    }

    /// <summary>
    /// Runs the configured extension host.
    /// </summary>
    /// <returns>A task representing the extension host lifetime.</returns>
    public Task RunAsync()
    {
        if (Interlocked.Exchange(ref this._hasRun, 1) != 0)
        {
            throw new InvalidOperationException("An extension host runner builder can only be run once.");
        }

        return ExtensionHostRunner.RunCoreAsync(
            this._configuration,
            this._parameters,
            [.. this._hostedExtensionRegistrations],
            this._includeDefaultLogSinks,
            [.. this._additionalLogSinks]);
    }

    void IExtensionHostLoggingBuilder.AddHostLogSink(IExtensionHostLogSink sink)
    {
        this.AddLogSink(sink);
    }

    void IExtensionHostLoggingBuilder.ClearDefaultLogSinks()
    {
        this.ClearDefaultLogSinks();
    }

    private void ThrowIfAlreadyRun()
    {
        if (Volatile.Read(ref this._hasRun) != 0)
        {
            throw new InvalidOperationException("The extension host runner builder has already been run.");
        }
    }
}
