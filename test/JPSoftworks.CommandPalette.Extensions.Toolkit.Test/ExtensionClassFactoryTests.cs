// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Runtime.InteropServices;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
using Microsoft.CommandPalette.Extensions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Test;

public sealed class ExtensionClassFactoryTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void ExplicitClsidRegistrationAndDisposalDoNotCreateAnInstance()
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        var creations = 0;
        using var factory = new ExtensionClassFactory(_ =>
        {
            creations++;
            return new TestExtension();
        }, lifetime, typeof(TestExtension).GUID);

        Assert.Equal(typeof(TestExtension).GUID, factory.ClassId);
        Assert.Equal(0, creations);
        factory.Dispose();
        factory.Dispose();
        Assert.Equal(0, creations);
        Assert.False(lifetime.Shutdown.IsCompleted);
    }

    [Fact]
    public void EmptyClsidIsRejectedBeforeCreatingAnInstance()
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        var creations = 0;

        var error = Assert.Throws<ArgumentException>(() => new ExtensionClassFactory(_ =>
        {
            creations++;
            return new TestExtension();
        }, lifetime, Guid.Empty));

        Assert.Equal("classId", error.ParamName);
        Assert.Equal(0, creations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActivationsHaveDistinctInstancesContextsAndDisposalEvents(bool useExplicitClassId)
    {
        var suspensions = 0;
        var lifetime = new ExtensionHostLifetime(() => suspensions++);
        List<ExtensionHostContext> contexts = [];
        List<TestExtension> extensions = [];
        using var factory = new ExtensionClassFactory(context =>
        {
            contexts.Add(context);
            var extension = new TestExtension(() => context.ExtensionDisposedEvent.Set());
            extensions.Add(extension);
            return extension;
        }, lifetime, useExplicitClassId ? typeof(TestExtension).GUID : null);

        Assert.Equal(useExplicitClassId ? 0 : 1, extensions.Count);
        var first = factory.Activate();
        var second = factory.Activate();

        Assert.Equal(2, extensions.Count);
        Assert.NotSame(first, second);
        Assert.NotSame(contexts[0], contexts[1]);
        Assert.NotSame(contexts[0].ExtensionDisposedEvent, contexts[1].ExtensionDisposedEvent);
        Assert.Same(extensions[0].Provider, first.GetProvider(default));
        Assert.Same(extensions[1].Provider, second.GetProvider(default));

        first.Dispose();
        first.Dispose();
        Assert.Equal(1, extensions[0].DisposeCount);
        Assert.Equal(0, suspensions);
        Assert.False(lifetime.Shutdown.IsCompleted);
        Assert.False(contexts[1].ExtensionDisposedEvent.WaitOne(0));
        Assert.Throws<ObjectDisposedException>(() => first.GetProvider(default));

        second.Dispose();
        Assert.Equal(1, suspensions);
        Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
        Assert.Equal(unchecked((int)0x80040111), Assert.Throws<COMException>(() => factory.Activate()).HResult);
        Assert.Equal(2, extensions.Count);
    }

    [Fact]
    public void AllFactoriesShareLifetimeAndUnactivatedInstancesDoNotKeepItAlive()
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        using var firstFactory = new ExtensionClassFactory(_ => new TestExtension(), lifetime);
        using var secondFactory = new ExtensionClassFactory(_ => new OtherExtension(), lifetime);
        var unused = new TestExtension();
        using var unusedFactory = new ExtensionClassFactory(_ => unused, lifetime);
        var first = firstFactory.Activate();
        var second = secondFactory.Activate();

        first.Dispose();
        Assert.False(lifetime.Shutdown.IsCompleted);
        second.Dispose();
        Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
        Assert.Throws<COMException>(() => unusedFactory.Activate());
        unusedFactory.Dispose();
        Assert.Equal(1, unused.DisposeCount);
    }

    [Fact]
    public async Task AcceptedActivationKeepsProcessAliveWhileFactoryIsRunning()
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        var constructing = NewSignal();
        using var finishConstruction = new ManualResetEventSlim();
        var creations = 0;
        using var factory = new ExtensionClassFactory(_ =>
        {
            if (++creations == 2)
            {
                constructing.SetResult();
                Assert.True(finishConstruction.Wait(Timeout));
            }

            return new TestExtension();
        }, lifetime);
        var first = factory.Activate();
        var activation = Task.Run(factory.Activate);
        try
        {
            await constructing.Task.WaitAsync(Timeout);
            first.Dispose();
            Assert.False(lifetime.Shutdown.IsCompleted);
        }
        finally
        {
            finishConstruction.Set();
        }

        var second = await activation.WaitAsync(Timeout);
        Assert.False(lifetime.Shutdown.IsCompleted);
        second.Dispose();
        Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task FirstLazyActivationKeepsProcessAliveWhileAnotherFactoryIsDisposed()
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        using var activeFactory = new ExtensionClassFactory(_ => new OtherExtension(), lifetime);
        var active = activeFactory.Activate();
        var constructing = NewSignal();
        using var finishConstruction = new ManualResetEventSlim();
        using var lazyFactory = new ExtensionClassFactory(_ =>
        {
            constructing.SetResult();
            Assert.True(finishConstruction.Wait(Timeout));
            return new TestExtension();
        }, lifetime, typeof(TestExtension).GUID);
        var activation = Task.Run(lazyFactory.Activate);
        try
        {
            await constructing.Task.WaitAsync(Timeout);
            active.Dispose();
            Assert.False(lifetime.Shutdown.IsCompleted);
        }
        finally
        {
            finishConstruction.Set();
        }

        var extension = await activation.WaitAsync(Timeout);
        extension.Dispose();
        Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedFirstLazyActivationReleasesReservationAndDisposalEvent(bool returnNull)
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        ManualResetEvent? failedEvent = null;
        using var factory = new ExtensionClassFactory(context =>
        {
            failedEvent = context.ExtensionDisposedEvent;
            return returnNull ? null! : throw new InvalidOperationException("Creation failed.");
        }, lifetime, typeof(TestExtension).GUID);

        Assert.Throws<InvalidOperationException>(() => factory.Activate());
        Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
        Assert.NotNull(failedEvent);
        Assert.Throws<ObjectDisposedException>(() => failedEvent.Set());
        Assert.Throws<COMException>(() => factory.Activate());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ConstructionFailuresAreLoggedWithClsidBeforeDraining(bool useExplicitClassId, bool returnNull)
    {
        List<ExtensionHostLogEntry> entries = [];
        var lifetime = new ExtensionHostLifetime(() => Assert.Single(entries));
        var failure = new InvalidOperationException("Creation failed.");
        var creations = 0;
        using var factory = new ExtensionClassFactory(_ =>
        {
            if (++creations == 1 && !useExplicitClassId)
            {
                return new TestExtension();
            }

            return returnNull ? null! : throw failure;
        }, lifetime, useExplicitClassId ? typeof(TestExtension).GUID : null, new DelegateExtensionHostLogSink(entries.Add));
        var first = useExplicitClassId ? null : factory.Activate();

        var error = Assert.Throws<InvalidOperationException>(() => factory.Activate());

        var entry = Assert.Single(entries);
        Assert.Equal(ExtensionHostLogLevel.Error, entry.Level);
        Assert.Contains(factory.ClassId.ToString(), entry.Message);
        Assert.Same(error, entry.Exception);
        Assert.False(string.IsNullOrEmpty(error.StackTrace));
        if (!returnNull)
        {
            Assert.Same(failure, error);
        }

        first?.Dispose();
        Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
    }

    [Fact]
    public void ExplicitRegistrationAllowsDifferentImplementationTypes()
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        var classId = new Guid("43E83895-C8D6-47B3-B030-7E0C2DC9C936");
        var other = new OtherExtension();
        var inner = new TestExtension();
        var creations = 0;
        using var factory = new ExtensionClassFactory(
            _ => ++creations == 1 ? other : inner, lifetime, classId);

        var first = factory.Activate();
        var second = factory.Activate();

        Assert.Equal(classId, factory.ClassId);
        Assert.Equal(2, creations);
        Assert.Same(inner.Provider, second.GetProvider(default));
        first.Dispose();
        Assert.Equal(1, other.DisposeCount);
        Assert.False(lifetime.Shutdown.IsCompleted);
        second.Dispose();
        Assert.Equal(1, inner.DisposeCount);
        Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task DisposalEventCannotStartShutdownBeforeExtensionCleanupCompletes()
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        var disposing = NewSignal();
        using var finishDisposal = new ManualResetEventSlim();
        using var factory = new ExtensionClassFactory(context => new TestExtension(() =>
        {
            context.ExtensionDisposedEvent.Set();
            disposing.SetResult();
            Assert.True(finishDisposal.Wait(Timeout));
        }), lifetime);
        var extension = factory.Activate();
        var disposal = Task.Run(extension.Dispose);
        try
        {
            await disposing.Task.WaitAsync(Timeout);
            Assert.False(lifetime.Shutdown.IsCompleted);
            Assert.False(disposal.IsCompleted);
        }
        finally
        {
            finishDisposal.Set();
        }

        await disposal.WaitAsync(Timeout);
        Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task FinalAndRepeatedDisposalWaitForSuspensionAndRejectRacingActivation()
    {
        var suspending = NewSignal();
        using var finishSuspension = new ManualResetEventSlim();
        var lifetime = new ExtensionHostLifetime(() =>
        {
            suspending.SetResult();
            Assert.True(finishSuspension.Wait(Timeout));
        });
        var inner = new TestExtension();
        using var factory = new ExtensionClassFactory(_ => inner, lifetime);
        var extension = factory.Activate();
        var firstDisposal = Task.Run(extension.Dispose);
        Task? repeatedDisposal = null;
        Task? activation = null;
        try
        {
            await suspending.Task.WaitAsync(Timeout);
            Assert.False(lifetime.Shutdown.IsCompleted);
            Assert.False(firstDisposal.IsCompleted);
            var disposingAgain = NewSignal();
            repeatedDisposal = Task.Run(() =>
            {
                disposingAgain.SetResult();
                extension.Dispose();
            });
            await disposingAgain.Task.WaitAsync(Timeout);
            Assert.False(repeatedDisposal.IsCompleted);
            activation = Task.Run(() => Assert.Throws<COMException>(() => factory.Activate()));
        }
        finally
        {
            finishSuspension.Set();
        }

        await Task.WhenAll(firstDisposal, repeatedDisposal!, activation!).WaitAsync(Timeout);
        Assert.Equal(1, inner.DisposeCount);
        Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedActivationReleasesReservationAndDisposalEvent(bool returnNull)
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        var constructing = NewSignal();
        using var finishConstruction = new ManualResetEventSlim();
        ManualResetEvent? failedEvent = null;
        var creations = 0;
        using var factory = new ExtensionClassFactory(context =>
        {
            if (++creations == 1)
            {
                return new TestExtension();
            }

            failedEvent = context.ExtensionDisposedEvent;
            constructing.SetResult();
            Assert.True(finishConstruction.Wait(Timeout));
            return returnNull ? null! : throw new InvalidOperationException("Creation failed.");
        }, lifetime);
        var first = factory.Activate();
        var activation = Task.Run(factory.Activate);
        try
        {
            await constructing.Task.WaitAsync(Timeout);
            first.Dispose();
            Assert.False(lifetime.Shutdown.IsCompleted);
        }
        finally
        {
            finishConstruction.Set();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => activation.WaitAsync(Timeout));
        Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
        Assert.NotNull(failedEvent);
        Assert.Throws<ObjectDisposedException>(() => failedEvent.Set());
    }

    [Fact]
    public void ThrowingDisposalStillDrainsAndClosesTheInstanceEvent()
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        ManualResetEvent? disposedEvent = null;
        using var factory = new ExtensionClassFactory(context =>
        {
            disposedEvent = context.ExtensionDisposedEvent;
            return new TestExtension(() => throw new InvalidOperationException("Disposal failed."));
        }, lifetime);
        var extension = factory.Activate();

        Assert.Throws<InvalidOperationException>(extension.Dispose);
        Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
        Assert.Throws<ObjectDisposedException>(() => disposedEvent!.Set());
        extension.Dispose();
        Assert.Throws<COMException>(() => factory.Activate());
    }

    [Fact]
    public void SignaledPreparedInstanceIsNeverReturnedToAClient()
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        var inner = new TestExtension();
        using var factory = new ExtensionClassFactory(context =>
        {
            context.ExtensionDisposedEvent.Set();
            return inner;
        }, lifetime);

        Assert.Throws<ObjectDisposedException>(() => factory.Activate());
        Assert.Equal(1, inner.DisposeCount);
        Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
    }

    [Fact]
    public void DifferentClsidIsRejectedAndDisposedWithoutLeakingAnActiveLease()
    {
        List<ExtensionHostLogEntry> entries = [];
        var lifetime = new ExtensionHostLifetime(() => { });
        var creations = 0;
        var other = new OtherExtension();
        using var factory = new ExtensionClassFactory(
            _ => ++creations == 1 ? new TestExtension() : other, lifetime, logSink: new DelegateExtensionHostLogSink(entries.Add));
        var first = factory.Activate();

        var error = Assert.Throws<InvalidOperationException>(() => factory.Activate());
        var entry = Assert.Single(entries);
        Assert.Equal(ExtensionHostLogLevel.Error, entry.Level);
        Assert.Contains(factory.ClassId.ToString(), entry.Message);
        Assert.Same(error, entry.Exception);
        Assert.Equal(1, other.DisposeCount);
        Assert.False(lifetime.Shutdown.IsCompleted);
        first.Dispose();
        Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
    }

    [Fact]
    public void UnregisteredFactoryDisposesItsPreparedInstanceWithoutRequestingShutdown()
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        ManualResetEvent? disposedEvent = null;
        var inner = new TestExtension();
        var factory = new ExtensionClassFactory(context =>
        {
            disposedEvent = context.ExtensionDisposedEvent;
            return inner;
        }, lifetime);

        factory.Dispose();
        factory.Dispose();
        Assert.Equal(1, inner.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => disposedEvent!.Set());
        Assert.False(lifetime.Shutdown.IsCompleted);
    }

    [Fact]
    public async Task ForcedDrainRejectsAndCleansUpAnActivationStillBeingConstructed()
    {
        var suspensions = 0;
        var lifetime = new ExtensionHostLifetime(() => suspensions++);
        var constructing = NewSignal();
        using var finishConstruction = new ManualResetEventSlim();
        var rejected = new TestExtension();
        var creations = 0;
        using var factory = new ExtensionClassFactory(_ =>
        {
            if (++creations == 1)
            {
                return new TestExtension();
            }

            constructing.SetResult();
            Assert.True(finishConstruction.Wait(Timeout));
            return rejected;
        }, lifetime);
        var first = factory.Activate();
        var activation = Task.Run(factory.Activate);
        try
        {
            await constructing.Task.WaitAsync(Timeout);
            lifetime.Drain();
            Assert.True(lifetime.Shutdown.IsCompletedSuccessfully);
        }
        finally
        {
            finishConstruction.Set();
        }

        await Assert.ThrowsAsync<COMException>(() => activation.WaitAsync(Timeout));
        Assert.Equal(1, rejected.DisposeCount);
        first.Dispose();
        lifetime.Drain();
        Assert.Equal(1, suspensions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FactoryTeardownDisposesLiveInstancesWithoutWaitingForConstruction(bool useExplicitClassId)
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        var liveInner = new OtherExtension();
        using var liveFactory = new ExtensionClassFactory(_ => liveInner, lifetime);
        var live = liveFactory.Activate();
        var constructing = NewSignal();
        using var finishConstruction = new ManualResetEventSlim();
        var rejected = new TestExtension();
        ManualResetEvent? rejectedEvent = null;
        var creations = 0;
        using var factory = new ExtensionClassFactory(context =>
        {
            if (++creations == 1 && !useExplicitClassId)
            {
                return new TestExtension();
            }

            rejectedEvent = context.ExtensionDisposedEvent;
            constructing.SetResult();
            Assert.True(finishConstruction.Wait(Timeout));
            return rejected;
        }, lifetime, useExplicitClassId ? typeof(TestExtension).GUID : null);
        var prepared = useExplicitClassId ? null : factory.Activate();
        var activation = Task.Run(() => Record.Exception(() => factory.Activate()));
        Task? teardown = null;
        try
        {
            await constructing.Task.WaitAsync(Timeout);
            teardown = Task.Run(() =>
            {
                lifetime.Drain();
                factory.Dispose();
                liveFactory.Dispose();
                lifetime.DisposeActiveExtensions();
            });

            await teardown.WaitAsync(Timeout);
            Assert.Equal(1, liveInner.DisposeCount);
            Assert.False(activation.IsCompleted);
            Assert.Equal(0, rejected.DisposeCount);
        }
        finally
        {
            finishConstruction.Set();
            await Task.WhenAll(activation, teardown ?? Task.CompletedTask).WaitAsync(Timeout);
        }

        Assert.IsType<ObjectDisposedException>(await activation);
        Assert.Equal(1, rejected.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => rejectedEvent!.Set());
        prepared?.Dispose();
        live.Dispose();
        Assert.Equal(1, liveInner.DisposeCount);
    }

    [Fact]
    public async Task FailedSuspensionFaultsShutdownAndRemainsClosedUntilTeardownRetries()
    {
        var attempts = 0;
        var lifetime = new ExtensionHostLifetime(() =>
        {
            if (++attempts == 1)
            {
                throw new COMException("Suspension failed.");
            }
        });
        using var factory = new ExtensionClassFactory(_ => new TestExtension(), lifetime);
        var extension = factory.Activate();

        Assert.Throws<COMException>(extension.Dispose);
        await Assert.ThrowsAsync<COMException>(() => lifetime.Shutdown);
        Assert.Throws<COMException>(() => factory.Activate());
        lifetime.Drain();
        Assert.Equal(2, attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForcedTeardownDisposesAllActiveInstancesEvenWhenOneThrows(bool throwOnDispose)
    {
        var suspensions = 0;
        var lifetime = new ExtensionHostLifetime(() => suspensions++);
        var failure = new InvalidOperationException("Disposal failed.");
        var inner = new TestExtension(() =>
        {
            if (throwOnDispose)
            {
                throw failure;
            }
        });
        var other = new OtherExtension();
        using var firstFactory = new ExtensionClassFactory(_ => inner, lifetime);
        using var secondFactory = new ExtensionClassFactory(_ => other, lifetime);
        var first = firstFactory.Activate();
        var second = secondFactory.Activate();

        lifetime.Drain();
        firstFactory.Dispose();
        secondFactory.Dispose();
        if (throwOnDispose)
        {
            var exception = Assert.Throws<AggregateException>(lifetime.DisposeActiveExtensions);
            Assert.Same(failure, Assert.Single(exception.InnerExceptions));
        }
        else
        {
            lifetime.DisposeActiveExtensions();
        }

        Assert.Equal(1, inner.DisposeCount);
        Assert.Equal(1, other.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => first.GetProvider(default));
        Assert.Throws<ObjectDisposedException>(() => second.GetProvider(default));
        first.Dispose();
        second.Dispose();
        lifetime.DisposeActiveExtensions();
        Assert.Equal(1, inner.DisposeCount);
        Assert.Equal(1, other.DisposeCount);
        Assert.Equal(1, suspensions);
    }

    [Fact]
    public void ForcedDisposalDoesNotHoldTheActivationGateWhileCallingExtensionCode()
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        using var factory = new ExtensionClassFactory(_ => new TestExtension(() =>
        {
            var activation = Task.Run(() => Assert.Throws<COMException>(() => lifetime.AcquireReference()));
            Assert.True(activation.Wait(Timeout));
        }), lifetime);
        factory.Activate();

        lifetime.Drain();
        lifetime.DisposeActiveExtensions();
    }

    [Fact]
    public async Task ForcedTeardownWaitsForConcurrentClientDisposal()
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        var disposing = NewSignal();
        using var finishDisposal = new ManualResetEventSlim();
        var inner = new TestExtension(() =>
        {
            disposing.TrySetResult();
            Assert.True(finishDisposal.Wait(Timeout));
        });
        using var factory = new ExtensionClassFactory(_ => inner, lifetime);
        var extension = factory.Activate();
        var clientDisposal = Task.Run(extension.Dispose);
        Task? teardown = null;
        try
        {
            await disposing.Task.WaitAsync(Timeout);
            lifetime.Drain();
            var teardownStarted = NewSignal();
            teardown = Task.Run(() =>
            {
                teardownStarted.SetResult();
                lifetime.DisposeActiveExtensions();
            });
            await teardownStarted.Task.WaitAsync(Timeout);
            await Task.WhenAny(teardown, Task.Delay(TimeSpan.FromMilliseconds(100)));
            Assert.False(teardown.IsCompleted);
        }
        finally
        {
            finishDisposal.Set();
        }

        await Task.WhenAll(clientDisposal, teardown!).WaitAsync(Timeout);
        Assert.Equal(1, inner.DisposeCount);
    }

    [Fact]
    public void ActiveDisposalRequiresDrainingFirst()
    {
        var lifetime = new ExtensionHostLifetime(() => { });
        Assert.Throws<InvalidOperationException>(lifetime.DisposeActiveExtensions);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Guid("7740310E-51C0-4B81-B356-60EAE8DEAE7F")]
    private sealed class TestExtension(Action? dispose = null) : IExtension
    {
        internal object Provider { get; } = new();

        internal int DisposeCount { get; private set; }

        public object GetProvider(ProviderType providerType) => this.Provider;

        public void Dispose()
        {
            this.DisposeCount++;
            dispose?.Invoke();
        }
    }

    [Guid("250A320A-121A-4684-80D1-05456E9A3C53")]
    private sealed class OtherExtension : IExtension
    {
        internal int DisposeCount { get; private set; }

        public object GetProvider(ProviderType providerType) => new();

        public void Dispose() => this.DisposeCount++;
    }
}