# Extension hosting and lifecycle

`ExtensionHostRunner` provides the common process and COM-server behavior needed by Command Palette extensions.

## What the runner owns

- The COM server message loop and class-factory registration.
- Extension disposal and host-process lifetime monitoring.
- Process shutdown priority so Command Palette can release the extension first during system shutdown.
- Optional process priority reduction and Windows Efficiency Mode (EcoQoS).
- Direct-launch fallback for Start menu, taskbar, and Microsoft Store certification scenarios.
- Default daily-file and Command Palette diagnostics.

## Basic hosting

```csharp
[MTAThread]
public static async Task Main(string[] args)
{
    await ExtensionHostRunner
        .CreateBuilder(
            args,
            new ExtensionHostRunnerParameters
            {
                PublisherMoniker = "MyCompany",
                ProductMoniker = "MyExtension",
            })
        .AddHostedExtensionFactory(context =>
            new MyExtension(context.ExtensionDisposedEvent))
        .RunAsync();
}
```

`PublisherMoniker` and `ProductMoniker` must each be a valid, single path segment. They identify the extension and
form part of its canonical local-application-data path.

`EnableEfficiencyMode` defaults to `true`. `IsDebug` defaults to `false`; the `-Debug` command-line argument also
enables debug diagnostics.

## Hosted extension factories

The runner builder accepts either an `IHostedExtensionFactory` or a delegate:

```csharp
runner.AddHostedExtensionFactory(context =>
    new MyExtension(context.ExtensionDisposedEvent));
```

Each call receives a separate `ExtensionHostContext` and lifetime lease. The toolkit wraps `IExtension.Dispose()`
and releases that instance's lease after disposal completes, including when disposal throws. Extensions no longer
need to signal `ExtensionDisposedEvent`; it remains available for compatibility and belongs only to that instance.
Signaling the event alone does not shut down the process. Do not share or dispose the event.

Factories must return a new instance of the same COM class on every call. Because the existing factory APIs do not
expose a CLSID, the runner prepares the first instance during registration and hands it out at most once. Later
activations create new instances. Prepared instances that are never activated are disposed during teardown and do
not keep the process alive.

For reusable factory objects, implement `IHostedExtensionFactory` or use `DelegateHostedExtensionFactory`:

```csharp
var factory = new DelegateHostedExtensionFactory(context =>
    new MyExtension(context.ExtensionDisposedEvent));
```

The event-only `IExtensionFactory`, `DelegateExtensionFactory`, and
`ExtensionHostRunnerParameters.ExtensionFactories` APIs remain available for binary compatibility, but are obsolete.

## Resolved host configuration

Applications that construct a custom logging pipeline should resolve host policy once and share the resulting
configuration with the runner and logging adapters:

```csharp
var host = ExtensionHostConfiguration.Resolve(args, parameters);
var runner = ExtensionHostRunner.CreateBuilder(host);
```

`ExtensionHostConfiguration` is an immutable token. It keeps argument parsing and canonical path construction inside
the Toolkit instead of making every extension duplicate those conventions.

Extensions that need to supply already-resolved policy or a custom log location can construct the configuration
directly:

```csharp
var host = new ExtensionHostConfiguration(
    args,
    parameters with { IsDebug = useDebugDiagnostics },
    customLogFilePath);
```

Direct construction snapshots the arguments and factory collections, uses `ExtensionHostRunnerParameters.IsDebug`
exactly as supplied, and does not parse `-Debug` or replace the supplied log path. All resolved values remain
available through public getters.

See [Diagnostics and logging](logging.md) for complete Microsoft.Extensions.Logging and Serilog examples.

## Direct launch

When the process is not registered as a COM server, the runner delegates to `StartupHelper`. The helper can open
Command Palette or prompt the user to install PowerToys. This makes direct executable launches useful instead of
leaving an apparently unresponsive background process.

Retail and development packages are discovered independently, with retail preferred for launch. Discovery errors
do not discard a package found in the other channel. If neither channel produces a package and either query fails,
the helper reports an error rather than prompting installation; a failed retail launch still falls back to dev.

## Process lifetime

The runner starts the COM server on an MTA thread and waits until either:

- the final active extension instance is disposed; or
- the monitored application lifetime requests termination.

All registered factories share one activation gate. An accepted activation holds a lifetime reference while its
factory runs, so concurrent disposal cannot shut down a process that is creating another instance. Disposing one
instance keeps the server running while other instances or accepted activations remain.

The final instance's disposal synchronously closes the activation gate and calls `CoSuspendClassObjects` before
returning to the client. Only after suspension succeeds does the runner begin normal teardown. Calls through an
already acquired class factory are rejected while draining. COM can route a subsequent activation to a new server
process, as described in [Microsoft's COM server lifetime guidance](https://learn.microsoft.com/en-us/windows/win32/com/out-of-process-server-implementation-helpers).

Application termination also closes the gate and suspends COM before unregistering factories. The runner then
disposes any remaining active and prepared instances sequentially, continuing cleanup if an instance throws.
Concurrent client disposal and forced teardown share the wrapper's exactly-once disposal path.

During `WM_ENDSESSION`, the monitor waits for extension and diagnostics cleanup before acknowledging shutdown,
with a four-second limit so a blocked extension cannot hold the Windows shutdown response indefinitely. Completion
is acknowledged before joining the monitor thread. This is a total best-effort budget, not a per-instance guarantee;
a blocked disposal can prevent later instances from finishing before Windows terminates the process. The monitor
uses an MTA thread so the acknowledgement wait does not dispatch nested shutdown messages. Caller-owned logging
factories, loggers, and custom sinks remain owned by the application.

## Supporting utilities

- `StartupHelper` handles direct application launches.
- `ShutdownHelper` adjusts process shutdown priority.
- `AppLifeMonitor` observes application lifetime termination.
- `EfficiencyModeHelper` enables process Efficiency Mode where supported.

[Back to the project README](../README.md)
