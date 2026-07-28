# Serilog adapter

This package bridges logging-neutral Command Palette extension host diagnostics and Serilog without making Serilog a
dependency of the main toolkit.

Forward runner diagnostics to an existing Serilog logger:

```csharp
await ExtensionHostRunner
    .CreateBuilder(args, parameters)
    .AddLogSink(new SerilogExtensionHostLogSink(Log.Logger))
    .RunAsync();
```

Forward Serilog events to the sink supplied through `ExtensionHostContext`:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Sink(new ExtensionHostSerilogSink(context.LogSink))
    .CreateLogger();
```

Verbose and fatal events are mapped to the logging-neutral debug and error levels respectively.
Bridge markers prevent an event from feeding back when both adapters are connected to the same logging pipeline.
The sink and logger passed to the adapters remain caller-owned.
