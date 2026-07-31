# Microsoft.Extensions.Logging adapter

This package bridges Command Palette extension host diagnostics and `Microsoft.Extensions.Logging` without making
that abstraction a dependency of the main toolkit. The extension owns and disposes its `ILoggerFactory`.

Use one application-owned factory for runner and extension diagnostics:

```csharp
var parameters = new ExtensionHostRunnerParameters
{
    PublisherMoniker = "MyCompany",
    ProductMoniker = "MyExtension",
};

var host = ExtensionHostConfiguration.Resolve(args, parameters);
using var loggerFactory = LoggerFactory.Create(builder =>
    builder
        .AddDailyFile(host)
        .AddCommandPalette(host));

await ExtensionHostRunner.CreateBuilder(host)
    .AddHostedExtensionFactory(context =>
        new MyExtension(
            context.ExtensionDisposedEvent,
            loggerFactory))
    .UseMicrosoftExtensionsLogging(loggerFactory)
    .RunAsync();
```

`ExtensionHostConfiguration.Resolve` applies the Toolkit's `-Debug` convention and canonical local-app-data log path
once. `AddDailyFile` and `AddCommandPalette` consume that immutable configuration, register only the destinations the
application selected, and apply the effective level. The factory owns and disposes providers registered by these
extensions.

`UseMicrosoftExtensionsLogging` replaces the Toolkit's default sinks and forwards host diagnostics into the supplied
factory while preserving categories. Explicitly added runner sinks remain active, and `ILoggerFactory` fans each
event out to all registered destinations.

The concrete `DailyFileLoggerProvider` and `CommandPaletteLoggerProvider` types remain available for advanced use.
Provider instances passed directly to `AddProvider` remain caller-owned and should be disposed after the factory.
Trace and critical levels map to the Toolkit's debug and error levels respectively.
Bridge markers bound accidental feedback when both adapters are connected to the same logging pipeline.
