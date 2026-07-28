// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Test;

public sealed class ExtensionHostRunnerBuilderTests
{
    [Fact]
    public void CreateBuilderRejectsNullArguments()
    {
        var parameters = CreateParameters();

        Assert.Throws<ArgumentNullException>(() => ExtensionHostRunner.CreateBuilder(null!, parameters));
        Assert.Throws<ArgumentNullException>(() => ExtensionHostRunner.CreateBuilder([], null!));
    }

    [Fact]
    public void AddLogSinkRejectsNull()
    {
        var builder = ExtensionHostRunner.CreateBuilder([], CreateParameters());

        Assert.Throws<ArgumentNullException>(() => builder.AddLogSink(null!));
    }

    [Fact]
    public void DelegateHostedExtensionFactoryRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DelegateHostedExtensionFactory(null!));
    }

    [Fact]
#pragma warning disable CS0618 // Exercise the compatibility contract explicitly.
    public void DelegateExtensionFactoryRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DelegateExtensionFactory(null!));
    }
#pragma warning restore CS0618

    [Fact]
#pragma warning disable CS0618 // Exercise the compatibility contract explicitly.
    public void DelegateExtensionFactoryForwardsDisposalEvent()
    {
        using var disposalEvent = new ManualResetEvent(false);
        ManualResetEvent? receivedEvent = null;
        var extension = new TestExtension();
        var factory = new DelegateExtensionFactory(disposedEvent =>
        {
            receivedEvent = disposedEvent;
            return extension;
        });

        var result = factory.CreateExtension(disposalEvent);

        Assert.Same(disposalEvent, receivedEvent);
        Assert.Same(extension, result);
    }
#pragma warning restore CS0618

    [Fact]
    public void DelegateHostedExtensionFactoryForwardsContext()
    {
        using var disposalEvent = new ManualResetEvent(false);
        var context = new ExtensionHostContext(disposalEvent, NullExtensionHostLogSink.Instance);
        ExtensionHostContext? receivedContext = null;
        var extension = new TestExtension();
        var factory = new DelegateHostedExtensionFactory(hostContext =>
        {
            receivedContext = hostContext;
            return extension;
        });

        var result = factory.CreateExtension(context);

        Assert.Same(context, receivedContext);
        Assert.Same(extension, result);
    }

    private static ExtensionHostRunnerParameters CreateParameters()
    {
        return new ExtensionHostRunnerParameters
        {
            PublisherMoniker = "JPSoftworks",
            ProductMoniker = "Test",
            HostedExtensionFactories = [],
        };
    }

    private sealed class TestExtension : Microsoft.CommandPalette.Extensions.IExtension
    {
        public object GetProvider(Microsoft.CommandPalette.Extensions.ProviderType providerType) => new();

        public void Dispose()
        {
        }
    }
}
