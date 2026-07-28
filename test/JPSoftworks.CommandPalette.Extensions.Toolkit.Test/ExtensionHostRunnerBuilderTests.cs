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
    public void DelegateExtensionFactoryRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DelegateExtensionFactory(null!));
    }

    private static ExtensionHostRunnerParameters CreateParameters()
    {
        return new ExtensionHostRunnerParameters
        {
            PublisherMoniker = "JPSoftworks",
            ProductMoniker = "Test",
            ExtensionFactories = [],
        };
    }
}