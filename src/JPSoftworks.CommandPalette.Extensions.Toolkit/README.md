![Logo](https://raw.githubusercontent.com/jiripolasek/JPSoftworks.CommandPalette.Extensions.Toolkit/refs/heads/master/art/StoreLogo.png)

# Extension Toolkit for Command Palette

A set of extensions and utilities for building [Command Palette](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview) extensions, extending the [Microsoft.CommandPalette.Extensions](https://www.nuget.org/packages/Microsoft.CommandPalette.Extensions/) NuGet package with an opinionated feature set.

The implementation may change in the future. As Command Palette evolves, so will this toolkit. Use at your own risk.

## Compatibility

The toolkit targets .NET 9 and .NET 10 on Windows. It is marked as Native AOT-compatible and trimmable, with both
target frameworks verified through executable `win-x64` and `win-arm64` Native AOT publishes.

The package does not pin a Windows SDK package version and does not depend on the Windows App SDK or WebView2. Its
runtime package dependencies are limited to `JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions`, the
Command Palette SDK, and the WinRT server used by the extension host.

## Features

### Extension Host Runner

`ExtensionHostRunner` simplifies creating and running Command Palette extensions.

It provides:
- A message loop that handles OS messages and shutdown requests—helping prevent hangs where the OS might otherwise treat the extension as unresponsive and terminate it.
- Optional Efficiency Mode to reduce CPU usage when the extension is idle.  
  - Lowers process priority and attempts to enable Windows Efficiency Mode (EcoQoS).
- Default daily file and Command Palette diagnostics with configurable, logging-neutral sinks.
- Debug-level logging that can be enabled manually or via a command-line argument.  
  - The `-Debug` argument enables debug-level logging at runtime.
- Graceful fallback when started without arguments:  
  - Either opens Command Palette or prompts the user to install PowerToys.  
  - Useful for passing Microsoft Store certification.
  - See the `StartupHelper` class.
 - Overrides the process shutdown priority to ensure the extension host process is not terminated while the extension is still running.  
   - See the `ShutdownHelper` class.

Usage:

```csharp
 [MTAThread]
 public static async Task Main(string[] args)
 {
     await ExtensionHostRunner.RunAsync(
         args,
         new ExtensionHostRunnerParameters
         {
             PublisherMoniker = "MyCompany",
             ProductMoniker = "MyExtension",
             IsDebug = false,                        // default is false
             EnableEfficiencyMode = true,            // default is true
             HostedExtensionFactories = [
                 new DelegateHostedExtensionFactory(context =>
                     new MyExtension(context.ExtensionDisposedEvent))
             ]
         });
 }
```

`IHostedExtensionFactory` is the primary extension-creation contract. It supplies both the disposal event and the
effective diagnostics sink through `ExtensionHostContext`. The event-only `IExtensionFactory`,
`DelegateExtensionFactory`, and `ExtensionFactories` members remain available as obsolete compatibility APIs.

### Diagnostics and logging

The default `RunAsync` overload writes daily files under the extension's local application data directory and forwards
messages to Command Palette. Debug diagnostics can be enabled with `-Debug` or `ExtensionHostRunnerParameters.IsDebug`.

The runner does not require the extension to adopt a particular application logging abstraction. Its builder can add a
delegate, trace, or custom sink while preserving the default sinks:

```csharp
var targetLogger = CreateLoggerUsingYourPreferredFramework();

await ExtensionHostRunner
    .CreateBuilder(args, parameters)
    .AddLogSink(new DelegateExtensionHostLogSink(entry => targetLogger.Write(entry)))
    .RunAsync();
```

Call `ClearDefaultLogSinks()` before `AddLogSink` to replace the defaults. `TraceExtensionHostLogSink.Instance`,
`DailyFileExtensionHostLogSink`, `NullExtensionHostLogSink.Instance`, and
`CommandPaletteExtensionHostLogSink.Instance` are built into this package and included by the default runner as
appropriate. The `IExtensionHostLogSink`, `ExtensionHostLogEntry`, and `ExtensionHostLogLevel` contracts live in the
`JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions` namespace.

Optional packages provide bidirectional adapters without making either logging framework a dependency of this package:

```csharp
// Host diagnostics -> Microsoft.Extensions.Logging
runnerBuilder.AddLogSink(new MicrosoftLoggerExtensionHostLogSink(logger));

// Host diagnostics -> Serilog
runnerBuilder.AddLogSink(new SerilogExtensionHostLogSink(Log.Logger));
```

Application and core-service logs can flow in the other direction through the same context sink:

```csharp
var parameters = new ExtensionHostRunnerParameters
{
    PublisherMoniker = "MyCompany",
    ProductMoniker = "MyExtension",
    HostedExtensionFactories =
    [
        new DelegateHostedExtensionFactory(context =>
        {
            loggerFactory.AddProvider(new ExtensionHostLoggerProvider(context.LogSink));
            return new MyExtension(context.ExtensionDisposedEvent);
        }),
    ],
};
```

Both adapter packages mark bridge-originated entries, so connecting both directions to the same pipeline does not feed
the same log back into itself.

The original static `Logger` remains available as an obsolete compatibility facade. Existing calls are forwarded to
the runner's effective sink, but new code should use `IExtensionHostLogSink` or one of the optional adapters.

### StartupHelper

`StartupHelper` provides a user interface when the extension is launched without arguments—for example, when the user clicks the extension icon in the Start menu or taskbar. It can be used to open Command Palette or prompt the user to install PowerToys.

### ShutdownHelper

`ShutdownHelper` adjusts the extension process’s shutdown priority relative to the extension host. This ensures the operating system does not terminate the extension process while the host is still running.

### AppLifeMonitor

`AppLifeMonitor` monitors the application’s lifetime and signals when the operating system attempts to close it.

### EfficiencyModeHelper

`EfficiencyModeHelper` enables Windows Efficiency Mode (EcoQoS) for the extension process, reducing CPU usage when the extension is idle. It can also lower process priority and enable EcoQoS.

## License

Apache 2.0

## Author

[Jiří Polášek](https://jiripolasek.com)
