// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Collections.Concurrent;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Test;

public sealed class ExtensionLogRouterTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void SinkThatLogsBackIntoThePipelineDoesNotRecurse()
    {
        var writes = 0;
        ExtensionLogRouter? router = null;
        router = new ExtensionLogRouter(
            [
                new DelegateLogSink(entry =>
                {
                    writes++;
                    router!.LogInformation("Feedback", $"Relayed {entry.Message}");
                }),
            ],
            [],
            isDebugEnabled: false);
        using (router)
        {
            router.LogInformation("Test", "Original");
            router.LogInformation("Test", "Second");
        }

        Assert.Equal(2, writes);
    }

    [Fact]
    public void ConcurrentWritersReachEverySink()
    {
        var entries = new ConcurrentQueue<ExtensionLogEntry>();
        using var router = new ExtensionLogRouter([new DelegateLogSink(entries.Enqueue)], [], isDebugEnabled: false);

        Parallel.For(0, 200, index => router.LogInformation("Test", index.ToString()));

        Assert.Equal(200, entries.Count);
    }

    [Fact]
    public async Task DisposalInsideWriteDefersOwnedCleanupUntilTheWriteReturns()
    {
        var disposedInsideWrite = -1;
        var downstreamWrites = 0;
        ExtensionLogRouter? router = null;
        var owned = new DisposeProbe(() => router!.Dispose());
        router = new ExtensionLogRouter(
            [
                new DelegateLogSink(_ =>
                {
                    router!.Dispose();
                    router.Dispose();
                    disposedInsideWrite = owned.DisposeCount;
                }),
                new DelegateLogSink(_ => downstreamWrites++),
            ],
            [owned],
            isDebugEnabled: false);

        await RunOnDedicatedThread(() => router.LogInformation("Test", "Shutdown")).WaitAsync(Timeout);
        router.Dispose();

        Assert.Equal(0, disposedInsideWrite);
        Assert.Equal(1, downstreamWrites);
        Assert.Equal(1, owned.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposalWaitsForEveryActiveWrite(bool asynchronousDisposal)
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseFirst = new ManualResetEventSlim();
        using var releaseSecond = new ManualResetEventSlim();
        var entries = new ConcurrentQueue<ExtensionLogEntry>();
        var writesAfterDisposal = 0;
        var owned = new DisposeProbe();
        using var router = new ExtensionLogRouter(
            [
                new DelegateLogSink(entry =>
                {
                    if (entry.Message == "First")
                    {
                        firstEntered.SetResult();
                        releaseFirst.Wait();
                    }
                    else if (entry.Message == "Second")
                    {
                        secondEntered.SetResult();
                        releaseSecond.Wait();
                    }
                }),
                new DelegateLogSink(entry =>
                {
                    if (owned.DisposeCount != 0)
                    {
                        Interlocked.Increment(ref writesAfterDisposal);
                    }

                    entries.Enqueue(entry);
                }),
            ],
            [owned],
            isDebugEnabled: false);
        var firstWrite = RunOnDedicatedThread(() => router.LogInformation("Test", "First"));
        var secondWrite = RunOnDedicatedThread(() => router.LogInformation("Test", "Second"));
        var dispose = Task.CompletedTask;
        try
        {
            await Task.WhenAll(firstEntered.Task, secondEntered.Task).WaitAsync(Timeout);
            if (asynchronousDisposal)
            {
                dispose = router.DisposeAsync().AsTask();
                disposalStarted.SetResult();
            }
            else
            {
                dispose = RunOnDedicatedThread(() =>
                {
                    disposalStarted.SetResult();
                    router.Dispose();
                });
            }
            await disposalStarted.Task.WaitAsync(Timeout);
            await Assert.ThrowsAsync<TimeoutException>(() => dispose.WaitAsync(TimeSpan.FromMilliseconds(100)));
            Assert.Equal(0, owned.DisposeCount);
            router.LogInformation("Test", "During shutdown");

            releaseFirst.Set();
            await firstWrite.WaitAsync(Timeout);
            await Assert.ThrowsAsync<TimeoutException>(() => dispose.WaitAsync(TimeSpan.FromMilliseconds(100)));
            Assert.Equal(0, owned.DisposeCount);

            releaseSecond.Set();
            await Task.WhenAll(secondWrite, dispose).WaitAsync(Timeout);
            router.LogInformation("Test", "After shutdown");
            Assert.Equal(2, entries.Count);
            Assert.Equal(0, writesAfterDisposal);
            Assert.Equal(1, owned.DisposeCount);
        }
        finally
        {
            releaseFirst.Set();
            releaseSecond.Set();
            await Task.WhenAll(firstWrite, secondWrite, dispose).WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task ShutdownRacingWithWritersDoesNotDisposeAnActiveSink()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var activeCallbacks = 0;
            var disposalWhileWriting = 0;
            var writesAfterDisposal = 0;
            var owned = new DisposeProbe(() => disposalWhileWriting = Volatile.Read(ref activeCallbacks));
            using var router = new ExtensionLogRouter(
                [new DelegateLogSink(_ =>
                {
                    Interlocked.Increment(ref activeCallbacks);
                    Thread.Yield();
                    if (owned.DisposeCount != 0)
                    {
                        Interlocked.Increment(ref writesAfterDisposal);
                    }

                    Interlocked.Decrement(ref activeCallbacks);
                })],
                [owned],
                isDebugEnabled: false);
            var writers = Enumerable.Range(0, 4)
                .Select(_ => Task.Run(() =>
                {
                    for (var write = 0; write < 100; write++)
                    {
                        router.LogInformation("Test", "Concurrent write");
                    }
                }))
                .ToArray();
            await Task.WhenAll([.. writers, Task.Run(router.Dispose)]).WaitAsync(Timeout);

            Assert.Equal(0, disposalWhileWriting);
            Assert.Equal(0, writesAfterDisposal);
            Assert.Equal(1, owned.DisposeCount);
        }
    }

    [Fact]
    public async Task ConcurrentDisposalWaitsForOwnedResources()
    {
        var disposalEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseDisposal = new ManualResetEventSlim();
        var owned = new DisposeProbe(() =>
        {
            disposalEntered.SetResult();
            releaseDisposal.Wait();
        });
        using var router = new ExtensionLogRouter([], [owned], isDebugEnabled: false);
        var firstDispose = RunOnDedicatedThread(router.Dispose);
        var secondDispose = Task.CompletedTask;
        try
        {
            await disposalEntered.Task.WaitAsync(Timeout);
            secondDispose = RunOnDedicatedThread(() =>
            {
                secondStarted.SetResult();
                router.Dispose();
            });
            await secondStarted.Task.WaitAsync(Timeout);
            await Assert.ThrowsAsync<TimeoutException>(() => secondDispose.WaitAsync(TimeSpan.FromMilliseconds(100)));
        }
        finally
        {
            releaseDisposal.Set();
            await Task.WhenAll(firstDispose, secondDispose).WaitAsync(Timeout);
        }

        Assert.Equal(1, owned.DisposeCount);
    }

    [Fact]
    public void DebugPolicyAndDisposalGateEveryWrite()
    {
        List<ExtensionLogLevel> levels = [];
        var owned = new DisposeProbe();
        var router = new ExtensionLogRouter([new DelegateLogSink(entry => levels.Add(entry.Level))], [owned], isDebugEnabled: false);

        router.LogDebug("Test", "Dropped");
        router.LogCritical("Test", "Kept");
        router.Dispose();
        router.Dispose();
        router.LogError("Test", "After disposal");

        Assert.Equal([ExtensionLogLevel.Critical], levels);
        Assert.Equal(1, owned.DisposeCount);
    }

    private static Task RunOnDedicatedThread(Action action) =>
        Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private sealed class DisposeProbe(Action? dispose = null) : IDisposable
    {
        private int _disposeCount;

        internal int DisposeCount => Volatile.Read(ref this._disposeCount);

        public void Dispose()
        {
            Interlocked.Increment(ref this._disposeCount);
            dispose?.Invoke();
        }
    }
}
