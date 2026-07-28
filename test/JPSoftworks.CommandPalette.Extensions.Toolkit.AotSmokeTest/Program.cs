// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Runtime.InteropServices;
using JPSoftworks.CommandPalette.Extensions.Toolkit;
using Microsoft.CommandPalette.Extensions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.AotSmokeTest;

internal static class Program
{
    [MTAThread]
    private static async Task Main(string[] args)
    {
        await ExtensionHostRunner.RunAsync(
            args,
            new ExtensionHostRunnerParameters
            {
                PublisherMoniker = "JPSoftworks",
                ProductMoniker = "AotSmokeTest",
                ExtensionFactories =
                [
                    new DelegateExtensionFactory(_ => new SmokeTestExtension()),
                ],
            });
    }
}

[Guid("A62D57D3-3185-464D-97C7-E08D49A8C88A")]
internal sealed class SmokeTestExtension : IExtension
{
    public object GetProvider(ProviderType providerType) => new();

    public void Dispose()
    {
    }
}
