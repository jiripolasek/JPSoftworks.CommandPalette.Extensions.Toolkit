![Logo](https://raw.githubusercontent.com/jiripolasek/JPSoftworks.CommandPalette.Extensions.Toolkit/refs/heads/master/art/StoreLogo.png)

# Extension Toolkit for Command Palette

A set of extensions and utilities for building [Command Palette](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview) extensions, extending the [Microsoft.CommandPalette.Extensions](https://www.nuget.org/packages/Microsoft.CommandPalette.Extensions/) NuGet package with an opinionated feature set.

The implementation may change in the future. As Command Palette evolves, so will this toolkit. Use at your own risk.

## Features

### Extension Host Runner

`ExtensionHostRunner` simplifies creating and running Command Palette extensions.

It provides:
- A message loop that handles OS messages and shutdown requests—helping prevent hangs where the OS might otherwise treat the extension as unresponsive and terminate it.
- Optional Efficiency Mode to reduce CPU usage when the extension is idle.  
  - Lowers process priority and attempts to enable Windows Efficiency Mode (EcoQoS).
- A simple logger that writes to a custom log file and to the extension host log.  
  - See the `Logger` class.
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
             ExtensionFactories = [
                 new DelegateExtensionFactory(manualResetEvent => new MyExtension(manualResetEvent))
             ]
         });
 }
```

### Logger

`Logger` is a simple logging utility that writes messages to a custom log file and to the extension host log. It supports different log levels and can be used to log debug, info, warning, and error messages.

Debug-level logging can be enabled by passing the `-Debug` argument to the process or manually by settings IsDebug property of `ExtensionHostRunnerParameters`. 

Usage:
```csharp
Logger.LogDebug("This is a debug message.");
Logger.LogInformation("This is an info message.");
Logger.LogWarning("This is a warning message.");
Logger.LogError("This is an error message.");
```

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