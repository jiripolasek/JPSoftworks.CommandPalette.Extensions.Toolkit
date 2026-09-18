// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Test;

public sealed class ExtensionHostRunnerBuilderTests
{
    [Fact]
    public void CreateBuilderRejectsNullArguments()
    {
        var parameters = CreateParameters();

        Assert.Throws<ArgumentNullException>(() => ExtensionHostRunner.CreateBuilder(null!, parameters));
        Assert.Throws<ArgumentNullException>(() => ExtensionHostRunner.CreateBuilder([], null!));
        Assert.Throws<ArgumentNullException>(
            () => ExtensionHostRunner.CreateBuilder((ExtensionHostConfiguration)null!));
        Assert.Throws<ArgumentNullException>(() => ExtensionHostConfiguration.Resolve(null!, parameters));
    }

    [Fact]
    public void AddLogSinkRejectsNull()
    {
        var builder = ExtensionHostRunner.CreateBuilder([], CreateParameters());

        Assert.Throws<ArgumentNullException>(() => builder.AddLogSink(null!));
    }

    [Fact]
    public void AddHostedExtensionFactoryRejectsNull()
    {
        var builder = ExtensionHostRunner.CreateBuilder([], CreateParameters());

        Assert.Throws<ArgumentNullException>(
            () => builder.AddHostedExtensionFactory((IHostedExtensionFactory)null!));
        Assert.Throws<ArgumentNullException>(
            () => builder.AddHostedExtensionFactory(
                (Func<ExtensionHostContext, Microsoft.CommandPalette.Extensions.IExtension>)null!));
        Assert.Throws<ArgumentNullException>(
            () => builder.AddHostedExtensionFactory(typeof(TestExtension).GUID, null!));
    }

    [Fact]
    public void ExplicitClsidRegistrationsRejectEmptyIds()
    {
        var builder = ExtensionHostRunner.CreateBuilder([], CreateParameters());

        Assert.Throws<ArgumentException>(
            () => builder.AddHostedExtensionFactory(Guid.Empty, _ => new TestExtension()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddHostedExtensionFactoryAcceptsDelegateWithoutInvokingIt(bool useExplicitClassId)
    {
        var builder = ExtensionHostRunner.CreateBuilder([], CreateParameters());

        var result = useExplicitClassId
            ? builder.AddHostedExtensionFactory(typeof(TestExtension).GUID, _ => throw new InvalidOperationException())
            : builder.AddHostedExtensionFactory(_ => throw new InvalidOperationException());

        Assert.Same(builder, result);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void ConfigurationResolvesEffectiveHostPolicy(
        bool parameterIsDebug,
        bool argumentIsDebug,
        bool expectedIsDebug)
    {
        var args = argumentIsDebug ? new[] { "-Debug" } : [];
        var configuration = ExtensionHostConfiguration.Resolve(
            args,
            CreateParameters(isDebug: parameterIsDebug));

        Assert.Equal(expectedIsDebug, configuration.IsDebug);
        var logDirectoryPath = Path.GetDirectoryName(configuration.LogFilePath);
        Assert.NotNull(logDirectoryPath);
        Assert.EndsWith(
            Path.Combine("JPSoftworks", "Test"),
            logDirectoryPath,
            StringComparison.Ordinal);
        Assert.Equal(
            Path.Combine(logDirectoryPath, "log.txt"),
            configuration.LogFilePath);
        Assert.NotNull(ExtensionHostRunner.CreateBuilder(configuration));
    }

    [Fact]
    public void ConfigurationSnapshotsArgumentsAndRunnerParameters()
    {
        var args = new[] { "-Debug" };
        var originalFactory = new DelegateHostedExtensionFactory(_ => new TestExtension());
        var parameters = CreateParameters();
        parameters.HostedExtensionFactories.Add(originalFactory);

        var configuration = ExtensionHostConfiguration.Resolve(args, parameters);

        args[0] = "-RegisterProcessAsComServer";
        parameters.HostedExtensionFactories.Clear();
        parameters.HostedExtensionFactories.Add(
            new DelegateHostedExtensionFactory(_ => new TestExtension()));

        Assert.Equal("-Debug", Assert.Single(configuration.Arguments));
        var snapshot = configuration.CreateRunnerParameters();
        Assert.Same(originalFactory, Assert.Single(snapshot.HostedExtensionFactories));
    }

    [Fact]
    public void ConfigurationCanBeConstructedFromExplicitValues()
    {
        var args = new[] { "-Debug" };
        var originalFactory = new DelegateHostedExtensionFactory(_ => new TestExtension());
        var parameters = CreateParameters(isDebug: false) with
        {
            EnableEfficiencyMode = false,
            HostedExtensionFactories = [originalFactory],
        };
        var logFilePath = Path.Combine("CustomLogs", "host.log");

        var configuration = new ExtensionHostConfiguration(args, parameters, logFilePath);

        args[0] = "-RegisterProcessAsComServer";
        parameters.HostedExtensionFactories.Clear();

        Assert.Equal("-Debug", Assert.Single(configuration.Arguments));
        Assert.False(configuration.IsDebug);
        Assert.Equal(logFilePath, configuration.LogFilePath);
        Assert.Equal(parameters.PublisherMoniker, configuration.PublisherMoniker);
        Assert.Equal(parameters.ProductMoniker, configuration.ProductMoniker);
        Assert.False(configuration.EnableEfficiencyMode);
        Assert.Same(
            originalFactory,
            Assert.Single(configuration.CreateRunnerParameters().HostedExtensionFactories));
        Assert.NotNull(ExtensionHostRunner.CreateBuilder(configuration));
    }

    [Fact]
    public void ConfigurationConstructorRejectsInvalidInputs()
    {
        var parameters = CreateParameters();

        Assert.Throws<ArgumentNullException>(
            () => new ExtensionHostConfiguration(null!, parameters, "log.txt"));
        Assert.Throws<ArgumentNullException>(
            () => new ExtensionHostConfiguration([], null!, "log.txt"));
        Assert.Throws<ArgumentException>(
            () => new ExtensionHostConfiguration([], parameters, " "));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(@"Publisher\Product")]
    [InlineData("Publisher/Product")]
    public void ConfigurationRejectsInvalidPublisherPathSegment(string publisherMoniker)
    {
        var parameters = CreateParameters() with { PublisherMoniker = publisherMoniker };

        Assert.Throws<ArgumentException>(() => ExtensionHostRunner.CreateBuilder([], parameters));
        Assert.Throws<ArgumentException>(() => new ExtensionHostConfiguration([], parameters, "log.txt"));
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
        var context = new ExtensionHostContext(disposalEvent);
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

    private static ExtensionHostRunnerParameters CreateParameters(bool isDebug = false)
    {
        return new ExtensionHostRunnerParameters
        {
            PublisherMoniker = "JPSoftworks",
            ProductMoniker = "Test",
            HostedExtensionFactories = [],
            IsDebug = isDebug,
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
