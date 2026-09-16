// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using JPSoftworks.CommandPalette.Extensions.Toolkit.Helpers;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Test;

public sealed class StartupHelperTests
{
    [Fact]
    public async Task RetailSuccessDoesNotLaunchDev()
    {
        var retail = new TestApp(() => Task.FromResult(true));
        var dev = new TestApp(() => Task.FromResult(true));

        Assert.True(await StartupHelper.TryStartCommandPaletteAsync(retail, dev, NullExtensionHostLogSink.Instance));
        Assert.Equal(1, retail.Attempts);
        Assert.Equal(0, dev.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRetailLaunchFallsBackToDev(bool throwException)
    {
        List<string> attempts = [];
        List<ExtensionHostLogEntry> entries = [];
        var failure = new InvalidOperationException("Retail launch failed.");
        var retail = new TestApp(() =>
        {
            attempts.Add("retail");
            return throwException ? Task.FromException<bool>(failure) : Task.FromResult(false);
        });
        var dev = new TestApp(() =>
        {
            attempts.Add("dev");
            return Task.FromResult(true);
        });

        Assert.True(await StartupHelper.TryStartCommandPaletteAsync(retail, dev, new DelegateExtensionHostLogSink(entries.Add)));
        Assert.Equal(["retail", "dev"], attempts);
        if (throwException)
        {
            Assert.Same(failure, Assert.Single(entries).Exception);
        }
        else
        {
            Assert.Empty(entries);
        }
    }

    [Fact]
    public async Task MissingRetailLaunchesDev()
    {
        var dev = new TestApp(() => Task.FromResult(true));

        Assert.True(await StartupHelper.TryStartCommandPaletteAsync(null, dev, NullExtensionHostLogSink.Instance));
        Assert.Equal(1, dev.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExhaustedLaunchesReportFailure(bool throwException)
    {
        var retail = new TestApp(() => throw new InvalidOperationException("Retail launch failed."));
        var dev = new TestApp(() => throwException
            ? Task.FromException<bool>(new InvalidOperationException("Dev launch failed."))
            : Task.FromResult(false));

        Assert.False(await StartupHelper.TryStartCommandPaletteAsync(retail, dev, NullExtensionHostLogSink.Instance));
        Assert.Equal(1, retail.Attempts);
        Assert.Equal(1, dev.Attempts);
    }

    [Fact]
    public async Task MissingAppsReportFailure()
    {
        Assert.False(await StartupHelper.TryStartCommandPaletteAsync(null, null, NullExtensionHostLogSink.Instance));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FoundAppSurvivesOtherChannelDiscoveryFailure(bool failRetail)
    {
        var app = new TestApp(() => Task.FromResult(true));
        var failure = new InvalidOperationException("Discovery failed.");
        List<ExtensionHostLogEntry> entries = [];
        var catalog = new TestCatalog(
            () => failRetail ? throw failure : app,
            () => failRetail ? app : throw failure);
        var logSink = new DelegateExtensionHostLogSink(entries.Add);

        var result = StartupHelper.FindCommandPaletteApps(catalog, logSink);

        Assert.Same(app, failRetail ? result.DevApp : result.RetailApp);
        Assert.Null(failRetail ? result.RetailApp : result.DevApp);
        Assert.Same(failure, Assert.Single(entries).Exception);
        Assert.True(await StartupHelper.TryStartCommandPaletteAsync(result.RetailApp, result.DevApp, logSink));
        Assert.Equal(1, app.Attempts);
        Assert.Equal(["retail", "dev"], catalog.Queries);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DiscoveryFailureWithoutAnAppIsNotReportedAsAbsence(bool failRetail, bool failDev)
    {
        var retailFailure = new InvalidOperationException("Retail discovery failed.");
        var devFailure = new InvalidOperationException("Dev discovery failed.");
        List<ExtensionHostLogEntry> entries = [];
        var catalog = new TestCatalog(
            () => failRetail ? throw retailFailure : null,
            () => failDev ? throw devFailure : null);

        var exception = Assert.Throws<AggregateException>(() => StartupHelper.FindCommandPaletteApps(
            catalog, new DelegateExtensionHostLogSink(entries.Add)));

        Assert.Equal((failRetail ? 1 : 0) + (failDev ? 1 : 0), exception.InnerExceptions.Count);
        if (failRetail)
        {
            Assert.Same(retailFailure, exception.InnerExceptions[0]);
        }

        if (failDev)
        {
            Assert.Same(devFailure, exception.InnerExceptions[^1]);
        }

        Assert.Empty(entries);
        Assert.Equal(["retail", "dev"], catalog.Queries);
    }

    [Fact]
    public void SuccessfulEmptyDiscoveryReportsAbsence()
    {
        var catalog = new TestCatalog(() => null, () => null);

        var result = StartupHelper.FindCommandPaletteApps(catalog, NullExtensionHostLogSink.Instance);

        Assert.Null(result.RetailApp);
        Assert.Null(result.DevApp);
        Assert.Equal(["retail", "dev"], catalog.Queries);
    }

    private sealed class TestCatalog(Func<ICommandPaletteApp?> retail, Func<ICommandPaletteApp?> dev) : ICommandPaletteAppCatalog
    {
        internal List<string> Queries { get; } = [];

        public ICommandPaletteApp? FindRetailApp()
        {
            this.Queries.Add("retail");
            return retail();
        }

        public ICommandPaletteApp? FindDevApp()
        {
            this.Queries.Add("dev");
            return dev();
        }
    }

    private sealed class TestApp(Func<Task<bool>> launch) : ICommandPaletteApp
    {
        internal int Attempts { get; private set; }

        public Task<bool> LaunchAsync()
        {
            this.Attempts++;
            return launch();
        }
    }
}