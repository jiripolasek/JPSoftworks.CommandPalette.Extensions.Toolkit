# Microsoft.Extensions.Logging adapter

This package bridges logging-neutral Command Palette extension host diagnostics and
`Microsoft.Extensions.Logging` without making that abstraction a dependency of the main toolkit.
It depends only on `JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions`, not on the main toolkit
package.

Forward runner diagnostics to an existing logger:

```csharp
await ExtensionHostRunner
    .CreateBuilder(args, parameters)
    .AddLogSink(new MicrosoftLoggerExtensionHostLogSink(logger))
    .RunAsync();
```

Forward application logs to the sink supplied through `ExtensionHostContext`:

```csharp
using var provider = new ExtensionHostLoggerProvider(context.LogSink);
loggerFactory.AddProvider(provider);
```

Trace and critical levels are mapped to the logging-neutral debug and error levels respectively.
Bridge markers prevent an entry from feeding back when both adapters are connected to the same logging pipeline.
The sink and logger passed to the adapters remain caller-owned.
