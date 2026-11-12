# Release notes

## Unreleased (draft)

### Breaking logging changes from v0.12.0-preview.1

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

See [Diagnostics and logging](logging.md) for sink and adapter configuration.

### Fixes

- Logging shutdown now waits for active writes to finish before disposing owned sinks or completing the runner.
  New writes during logging shutdown and after the runner completes are ignored. This wait has no timeout;
  a sink that never returns prevents runner completion.
- Shutdown triggered from inside a sink now awaits the active write asynchronously, avoiding a self-deadlock while
  still waiting for that write and owned-sink disposal before completing the runner.
