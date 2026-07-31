<div align="center">

<p>
	<img src="art/StoreLogo.png" alt="Logo">
</p>

<h1 align="center"><span style="font-weight: bold">Extension Toolkit</span> <br /><span style="font-weight: 300; opacity: 0.5">for Command Palette</span></h1>

</div>

An opinionated set of hosting, lifecycle, startup, and diagnostics utilities for
[Command Palette](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview) extensions built on
[Microsoft.CommandPalette.Extensions](https://www.nuget.org/packages/Microsoft.CommandPalette.Extensions/).

> [!WARNING]
> The API may evolve alongside Command Palette. Preview releases can contain breaking changes.

## Highlights

- **Complete host bootstrap** — register the COM class factories and run the server message loop with minimal
  application code.
- **Correct lifetime and shutdown handling** — monitor application lifetime, stop when the extension is disposed,
  unregister the COM server cleanly, and give Command Palette priority to release the extension during system shutdown.
- **Efficiency Mode support** — optionally lower process priority and enable Windows Efficiency Mode (EcoQoS) for the
  extension process.
- **A useful direct-launch experience** — open Command Palette or guide users to PowerToys instead of leaving an
  apparently unresponsive process.
- **Consistent host conventions** — centralize debug detection and canonical data paths across every extension.
- **Logging without lock-in** — use the built-in rolling file and Command Palette diagnostics, logging-neutral sinks,
  or optional Microsoft.Extensions.Logging and Serilog integrations.
- **Modern deployment support** — target .NET 9 or .NET 10, including trimming and Native AOT on x64 and Arm64.

## Packages

| Package | NuGet | Purpose |
| --- | --- | --- |
| [`JPSoftworks.CommandPalette.Extensions.Toolkit`][toolkit-nuget] | [![Stable][toolkit-stable]][toolkit-nuget] [![Preview][toolkit-preview]][toolkit-nuget] | Hosting, lifecycle, startup, and built-in diagnostic sinks. |
| [`JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions`][abstractions-nuget] | [![Stable][abstractions-stable]][abstractions-nuget] [![Preview][abstractions-preview]][abstractions-nuget] | Logging-neutral contracts. |
| [`JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions`][microsoft-nuget] | [![Stable][microsoft-stable]][microsoft-nuget] [![Preview][microsoft-preview]][microsoft-nuget] | Microsoft.Extensions.Logging providers and host bridge. |
| [`JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog`][serilog-nuget] | [![Stable][serilog-stable]][serilog-nuget] [![Preview][serilog-preview]][serilog-nuget] | Serilog sinks and host bridge. |

## Compatibility

The Toolkit targets .NET 9 and .NET 10 on Windows. It is Native AOT-compatible and trimmable, with `win-x64` and
`win-arm64` publishes verified for both target frameworks.

The main package does not pin a Windows SDK package version and does not depend on Windows App SDK, WebView2,
Microsoft.Extensions.Logging, or Serilog.

## Quick start

```csharp
[MTAThread]
public static async Task Main(string[] args)
{
    await ExtensionHostRunner
        .CreateBuilder(
            args,
            new ExtensionHostRunnerParameters
            {
                PublisherMoniker = "MyCompany",
                ProductMoniker = "MyExtension",
            })
        .AddHostedExtensionFactory(context =>
            new MyExtension(context.ExtensionDisposedEvent))
        .RunAsync();
}
```

The default runner provides the COM server lifecycle, process monitoring, direct-launch fallback, Efficiency Mode,
daily rolling files, and Command Palette diagnostics. Pass `-Debug` or set
`ExtensionHostRunnerParameters.IsDebug` to enable debug entries consistently.

## Documentation

- [Extension hosting and lifecycle](docs/extension-hosting.md)
- [Diagnostics and logging](docs/logging.md)

## Local development

Repository-local outputs are written beneath the ignored `artifacts` directory:

```powershell
.\eng\build.ps1
.\eng\test.ps1 -NoBuild -NoRestore
.\eng\pack.ps1
.\eng\publish-local.ps1
.\eng\verify-aot.ps1
```

Restore operations used by `build.ps1`, `test.ps1`, and `pack.ps1` run in locked mode. Package names, paths, and the
AOT smoke-test project are declared in `eng\Package.config.psd1`. `pack.ps1` produces every Toolkit `.nupkg` together
with a matching `.snupkg`, and `publish-local.ps1` copies both package types to `artifacts\local-feed`.

`verify-aot.ps1` treats warnings as errors, publishes both target frameworks for `win-x64` and `win-arm64` beneath
`artifacts\aot`, and verifies that every output is native rather than framework-dependent.

## License

Apache 2.0

## Author

[Jiří Polášek](https://jiripolasek.com)

[toolkit-nuget]: https://www.nuget.org/packages/JPSoftworks.CommandPalette.Extensions.Toolkit/
[toolkit-stable]: https://img.shields.io/nuget/v/JPSoftworks.CommandPalette.Extensions.Toolkit?style=for-the-badge&logo=nuget&label=stable&color=004880
[toolkit-preview]: https://img.shields.io/nuget/vpre/JPSoftworks.CommandPalette.Extensions.Toolkit?style=for-the-badge&logo=nuget&label=preview&color=purple

[abstractions-nuget]: https://www.nuget.org/packages/JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions/
[abstractions-stable]: https://img.shields.io/nuget/v/JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions?style=for-the-badge&logo=nuget&label=stable&color=004880
[abstractions-preview]: https://img.shields.io/nuget/vpre/JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions?style=for-the-badge&logo=nuget&label=preview&color=purple

[microsoft-nuget]: https://www.nuget.org/packages/JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions/
[microsoft-stable]: https://img.shields.io/nuget/v/JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions?style=for-the-badge&logo=nuget&label=stable&color=004880
[microsoft-preview]: https://img.shields.io/nuget/vpre/JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions?style=for-the-badge&logo=nuget&label=preview&color=purple

[serilog-nuget]: https://www.nuget.org/packages/JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog/
[serilog-stable]: https://img.shields.io/nuget/v/JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog?style=for-the-badge&logo=nuget&label=stable&color=004880
[serilog-preview]: https://img.shields.io/nuget/vpre/JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Serilog?style=for-the-badge&logo=nuget&label=preview&color=purple
