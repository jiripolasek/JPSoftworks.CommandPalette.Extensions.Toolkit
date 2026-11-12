# Release notes

## 0.12.0-preview.2

This preview updates logging and framework support while retaining the
`Shmuelie.WinRTServer` COM backend.

### Framework support

- Adds .NET 11 targets to all four packages, alongside .NET 9 and .NET 10.
- Uses Microsoft.Extensions.Logging 11.0.0 for the .NET 11 adapter.
- Builds with the .NET 11 SDK and includes .NET 11 in tests and Native AOT checks.

### Logging improvements

- Keeps the static `Logger` supported as a convenience API, without obsolete warnings.
- Adds a minimum-level filter to `AddLogSink` and preserves critical levels in logging adapters.

### Breaking logging changes from v0.12.0-preview.1

- Renames the logging contracts and sinks to remove `Host` from their names. Update custom sinks,
  logging builders, and adapter references using the table below; the old names are not aliases.
- Custom sinks can now receive concurrent writes. The v0.12.0-preview.1 runner serialized writes to its sinks,
  so existing sinks that relied on that behavior must add synchronization or use thread-safe destinations.
- The static `Logger` no longer forwards entries to Command Palette by default. `Logger.Initialize`
  configures only the daily log file. Without an active runner or initialization, entries go to
  `System.Diagnostics.Trace`.
- Standalone applications using only `Logger.Initialize` and `Logger.Log*` have no configuration switch to
  restore Command Palette forwarding. Retaining those calls without a runner means accepting the file or Trace
  destinations. To restore forwarding, migrate the calls to `CommandPaletteLogSink` or a logging adapter, or
  adopt `ExtensionHostRunner` and configure `CommandPaletteLogSink` on it. Adding a sink to the runner is not
  a migration solution for applications that continue to use the standalone static logger.

| Preview 1 name | Preview 2 name |
| --- | --- |
| `ExtensionHostLogEntry` | `ExtensionLogEntry` |
| `ExtensionHostLogLevel` | `ExtensionLogLevel` |
| `ExtensionHostLogSinkExtensions` | `ExtensionLogSinkExtensions` |
| `IExtensionHostLogSink` | `IExtensionLogSink` |
| `IExtensionHostLoggingBuilder` | `IExtensionLoggingBuilder` |
| `CommandPaletteExtensionHostLogSink` | `CommandPaletteLogSink` |
| `CompositeExtensionHostLogSink` | `CompositeLogSink` |
| `DailyFileExtensionHostLogSink` | `DailyFileLogSink` |
| `DelegateExtensionHostLogSink` | `DelegateLogSink` |
| `NullExtensionHostLogSink` | `NullLogSink` |
| `TraceExtensionHostLogSink` | `TraceLogSink` |
| `MicrosoftLoggerExtensionHostLogSink` | `MicrosoftLoggerLogSink` |
| `SerilogExtensionHostLogSink` | `SerilogLogSink` |

See [Diagnostics and logging](logging.md) for sink and adapter configuration.

### Fixes

- Logging shutdown now waits for active writes to finish before disposing owned sinks or completing the runner.
  New writes during logging shutdown and after the runner completes are ignored. This wait has no timeout;
  a sink that never returns prevents runner completion.
- Shutdown triggered from inside a sink now awaits the active write asynchronously, avoiding a self-deadlock while
  still waiting for that write and owned-sink disposal before completing the runner.
- `AddLogSink(sink, minimumLevel)` rejects undefined log levels instead of silently filtering entries incorrectly.
