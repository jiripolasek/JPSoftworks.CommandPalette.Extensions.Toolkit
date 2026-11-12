# Diagnostics and logging

The Toolkit keeps host policy consistent while letting each extension choose its application logging abstraction and
destinations.

## Default destination

`ExtensionHostRunner.RunAsync` and an unmodified runner builder write to a daily rolling file beneath the extension's
canonical local-application-data directory. If the file cannot be opened, entries go to `System.Diagnostics.Trace`.

Nothing is sent to Command Palette by default. See [Forwarding to Command Palette](#forwarding-to-command-palette).

Each run writes an information-level startup entry. Debug entries are enabled when either
`ExtensionHostRunnerParameters.IsDebug` is `true` or the command line contains `-Debug`.

## Logging-neutral sinks

The main package includes:

- `DailyFileLogSink`
- `CommandPaletteLogSink`
- `TraceLogSink`
- `DelegateLogSink`
- `CompositeLogSink`
- `NullLogSink`

The shared `IExtensionLogSink`, `ExtensionLogEntry`, and `ExtensionLogLevel` contracts live in
`JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions`.

Additional sinks retain the defaults:

```csharp
await ExtensionHostRunner
    .CreateBuilder(args, parameters)
    .AddLogSink(new DelegateLogSink(entry => targetLogger.Write(entry)))
    .RunAsync();
```

Call `ClearDefaultLogSinks()` before `AddLogSink` when the custom sink should replace the daily-file destination.

## Forwarding to Command Palette

`CommandPaletteLogSink` forwards entries to the Command Palette log through the Command Palette SDK's
`IExtensionHost.LogMessage`. Each entry allocates a WinRT log message object and makes a cross-process COM call, and
Command Palette then calls back into the extension to read the message. Entries are discarded until the extension's
command provider receives the Command Palette host.

The sink is opt-in. Reserve it for low-volume entries that users should see in Command Palette, such as errors:

```csharp
await ExtensionHostRunner
    .CreateBuilder(args, parameters)
    .AddLogSink(CommandPaletteLogSink.Instance, ExtensionLogLevel.Error)
    .RunAsync();
```

The Microsoft.Extensions.Logging `AddCommandPalette` and Serilog `WriteTo.CommandPalette` destinations use the same
path and have the same cost. Give them a minimum level, for example
`.AddFilter<CommandPaletteLoggerProvider>(level => level >= LogLevel.Critical)` or
`.WriteTo.CommandPalette(LogEventLevel.Fatal)`.

## Microsoft.Extensions.Logging

Install `JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions` and construct the application
factory normally:

```csharp
var host = ExtensionHostConfiguration.Resolve(args, parameters);

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddDailyFile(host));

await ExtensionHostRunner.CreateBuilder(host)
    .AddHostedExtensionFactory(context =>
        new MyExtension(
            context.ExtensionDisposedEvent,
            loggerFactory))
    .UseMicrosoftExtensionsLogging(loggerFactory)
    .RunAsync();
```

`AddDailyFile` and `AddCommandPalette` apply the resolved debug policy and register only the destinations selected by
the application. The `ILoggerFactory` owns providers registered through these methods and fans each event out to all
of them. Their providers accept every Microsoft logging level internally, so later provider-specific `AddFilter`
rules can replace the resolved minimum without being blocked by a second provider-level threshold.
The resolved state is available through `ExtensionHostConfiguration.IsDebug`; consumers should not parse `-Debug`
again.

`UseMicrosoftExtensionsLogging` replaces the runner's default sinks and forwards host diagnostics into the same
factory while preserving categories. Explicitly added runner sinks remain active.

The concrete `DailyFileLoggerProvider` and `CommandPaletteLoggerProvider` types remain available for advanced use.
Provider instances passed directly to `ILoggingBuilder.AddProvider` remain caller-owned.

## Serilog

Install `JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog` and apply the same resolved configuration:

```csharp
var host = ExtensionHostConfiguration.Resolve(args, parameters);

using var logger = new LoggerConfiguration()
    .MinimumLevel.FromExtensionHost(host)
    .WriteTo.DailyFile(host)
    .CreateLogger();

await ExtensionHostRunner.CreateBuilder(host)
    .UseSerilog(logger)
    .RunAsync();
```

`FromExtensionHost` applies the effective minimum level. `DailyFile` uses the canonical Toolkit path, and
`CommandPalette` writes to the host destination. The application owns and disposes the Serilog logger.

## Bridge behavior

Microsoft.Extensions.Logging trace maps to the Toolkit's debug level, and critical maps to critical in both
directions. Serilog verbose maps to debug, and fatal maps to critical in both directions.

Bridge-originated entries are marked so connecting host and application logging in both directions cannot create a
feedback loop.

## Static logger

The static `JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Logger` API is a supported convenience for simple
extensions. The runner configures it automatically, so extension code can call `Logger.LogInformation("Extension initialized")`
without passing a sink through its constructors. Calls use the runner's configured destinations and debug policy.

Use a logging adapter or `IExtensionLogSink` when explicit dependencies are more useful than process-wide state.

`Logger.Initialize` configures only the daily log file. Without initialization or a runner, entries go to `System.Diagnostics.Trace`.

[Back to the project README](../README.md)
