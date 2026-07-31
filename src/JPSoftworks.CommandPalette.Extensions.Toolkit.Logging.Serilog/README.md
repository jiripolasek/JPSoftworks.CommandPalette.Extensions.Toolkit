# Serilog adapter

This package bridges Command Palette extension host diagnostics and Serilog without making Serilog a dependency of
the main toolkit.

Use one application-owned logger for runner and application diagnostics:

```csharp
var host = ExtensionHostConfiguration.Resolve(args, parameters);
using var logger = new LoggerConfiguration()
    .MinimumLevel.FromExtensionHost(host)
    .WriteTo.DailyFile(host)
    .WriteTo.CommandPalette()
    .CreateLogger();

var runner = ExtensionHostRunner.CreateBuilder(host);
runner.UseSerilog(logger);
await runner.RunAsync();
```

`ExtensionHostConfiguration.Resolve` applies the Toolkit's `-Debug` convention and canonical local-app-data log path
once. `FromExtensionHost` and `DailyFile` consume that immutable configuration, while the application chooses which
destinations to register. The logger owns and disposes its daily-file sink.

`UseSerilog` replaces the Toolkit's default sinks and forwards host diagnostics into the application logger.
Explicitly added runner sinks remain active. `WriteTo.CommandPalette` adds the Command Palette host destination.

Verbose and fatal events are mapped to the logging-neutral debug and error levels respectively.
Bridge markers bound accidental feedback when both adapters are connected to the same logging pipeline.
The sink and logger passed to the adapters remain caller-owned.
