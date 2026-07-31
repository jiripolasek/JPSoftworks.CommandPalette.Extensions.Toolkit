# Command Palette extension logging abstractions

This package provides dependency-free diagnostics contracts for Command Palette extension hosts and logging adapters.
It does not include concrete sinks or require Microsoft.Extensions.Logging, Serilog, or another application logging
framework.

Implement `IExtensionHostLogSink` to route host diagnostics to the logging system selected by an extension:

```csharp
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

public sealed class MyExtensionHostLogSink : IExtensionHostLogSink
{
    public void Write(ExtensionHostLogEntry entry)
    {
        // Map the entry to the application's logging system.
    }
}
```

The main `JPSoftworks.CommandPalette.Extensions.Toolkit` package provides dependency-free delegate, trace, daily file,
composite, null, and Command Palette sinks. Framework-specific adapters are available separately:

- `JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions`
- `JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog`

These contracts are an integration boundary for extension-host diagnostics. Application and core-service projects
should continue to use whichever logging abstraction best fits them.

`IExtensionHostLoggingBuilder` is the small composition contract used by optional adapter packages. Host builders
implement it without depending on a particular logging framework.
