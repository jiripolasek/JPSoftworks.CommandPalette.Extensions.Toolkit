# Command Palette extension logging

This package provides logging-neutral diagnostics contracts and dependency-free sinks for Command Palette extension hosts.
It does not require Microsoft.Extensions.Logging, Serilog, or another application logging framework.

Use `DelegateExtensionHostLogSink` to forward host diagnostics to the logging system selected by an extension:

```csharp
var sink = new DelegateExtensionHostLogSink(entry =>
{
    // Map the entry to Microsoft.Extensions.Logging, Serilog, or another logger.
});
```

`TraceExtensionHostLogSink.Instance` forwards diagnostics through `System.Diagnostics.Trace`.
`DailyFileExtensionHostLogSink` provides the toolkit's default daily rolling file output, and
`NullExtensionHostLogSink.Instance` explicitly discards diagnostics.

These contracts are intended as an integration boundary for extension host diagnostics. Application and core service
projects should continue to use whichever logging abstraction best fits them.

Framework-specific adapters are available separately:

- `JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions`
- `JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog`

The main `JPSoftworks.CommandPalette.Extensions.Toolkit` package also provides the built-in
`CommandPaletteExtensionHostLogSink.Instance`, which is part of the default runner configuration.
