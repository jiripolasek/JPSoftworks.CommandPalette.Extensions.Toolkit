// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Configures and runs an extension host while retaining the toolkit's default behavior unless explicitly replaced.
/// </summary>
public sealed class ExtensionHostRunnerBuilder
{
    private readonly string[] _args;
    private readonly ExtensionHostRunnerParameters _parameters;
    private readonly List<IExtensionHostLogSink> _additionalLogSinks = [];

    private bool _includeDefaultLogSinks = true;
    private int _hasRun;

    internal ExtensionHostRunnerBuilder(
        string[] args,
        ExtensionHostRunnerParameters parameters)
    {
        this._args = args;
        this._parameters = parameters;
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
            this._args,
            this._parameters,
            this._includeDefaultLogSinks,
            [.. this._additionalLogSinks]);
    }

    private void ThrowIfAlreadyRun()
    {
        if (Volatile.Read(ref this._hasRun) != 0)
        {
            throw new InvalidOperationException("The extension host runner builder has already been run.");
        }
    }
}
