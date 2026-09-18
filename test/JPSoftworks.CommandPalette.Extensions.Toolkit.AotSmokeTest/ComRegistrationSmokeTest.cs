// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Collections.Concurrent;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
using Microsoft.CommandPalette.Extensions;
using static JPSoftworks.CommandPalette.Extensions.Toolkit.AotSmokeTest.ComSmokeTestSupport;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.AotSmokeTest;

internal static class ComRegistrationSmokeTest
{
    internal static void Run(bool explicitFirst)
    {
        RunInMta(() => ExerciseServer(explicitFirst).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult());
    }

    private static Task ExerciseServer(bool explicitFirst)
    {
        var classId = typeof(LifetimeSmokeExtension).GUID;
        var failingClassId = new Guid("76F1405B-146C-4CE3-9568-BD78E64CABAB");
        var failure = new InvalidOperationException("Expected construction failure.");
        var entries = new ConcurrentQueue<ExtensionHostLogEntry>();
        var winnerCreations = 0;
        var loserCreations = 0;
        var winnerDisposals = 0;
        var loserDisposals = 0;
        var parameters = new ExtensionHostRunnerParameters
        {
            PublisherMoniker = "JPSoftworks",
            ProductMoniker = "ComRegistrationSmokeTest",
            EnableEfficiencyMode = false,
            IsDebug = true,
        };

        IExtension CreateWinner(ExtensionHostContext context)
        {
            winnerCreations++;
            return new LifetimeSmokeExtension(() => winnerDisposals++, () => { });
        }

        IExtension CreateLoser(ExtensionHostContext context)
        {
            loserCreations++;
            return new LifetimeSmokeExtension(() => loserDisposals++, () => { });
        }

        if (!explicitFirst)
        {
            parameters.HostedExtensionFactories.Add(new DelegateHostedExtensionFactory(CreateWinner));
        }

        var builder = ExtensionHostRunner.CreateBuilder(["-RegisterProcessAsComServer"], parameters)
            .ClearDefaultLogSinks()
            .AddLogSink(new DelegateExtensionHostLogSink(entries.Enqueue));
        if (explicitFirst)
        {
            builder.AddHostedExtensionFactory(classId, CreateWinner);
            builder.AddHostedExtensionFactory(CreateLoser);
        }
        else
        {
            builder.AddHostedExtensionFactory(classId, CreateLoser);
        }

        builder.AddHostedExtensionFactory(failingClassId, _ => throw failure);
        var running = builder.RunAsync();
        nint winner = 0;
        try
        {
            Require(!running.IsCompleted, "The COM server exited before activation.");
            Require(winnerCreations == (explicitFirst ? 0 : 1), "The winning registration did not honor its creation policy.");
            Require(loserCreations == (explicitFirst ? 1 : 0) && loserDisposals == loserCreations,
                "The skipped registration was created or disposed incorrectly.");
            Require(entries.Count(entry => entry.Level == ExtensionHostLogLevel.Warning
                && entry.Message.Contains($"Duplicate extension CLSID {classId}", StringComparison.Ordinal)) == 1,
                "The duplicate registration was not identified in diagnostics.");
            foreach (var registeredClassId in new[] { classId, failingClassId })
            {
                Require(entries.Count(entry => entry.Level == ExtensionHostLogLevel.Debug
                    && entry.Message == $"Registered extension CLSID {registeredClassId}") == 1,
                    "Successful registration diagnostics did not identify the CLSID.");
            }

            winner = ActivateExtension(classId, typeof(IExtension).GUID);
            Require(winnerCreations == 1 && loserCreations == (explicitFirst ? 1 : 0),
                "COM did not activate the first registration.");
            nint unexpected = 0;
            try
            {
                unexpected = ActivateExtension(failingClassId, typeof(IExtension).GUID);
                throw new InvalidOperationException("COM accepted a failed construction.");
            }
            catch (Exception ex) when (unexpected == 0 && ex.HResult == failure.HResult)
            {
            }
            finally
            {
                Release(unexpected);
            }

            var errors = entries.Where(entry => entry.Level == ExtensionHostLogLevel.Error).ToArray();
            Require(errors.Length == 1 && ReferenceEquals(errors[0].Exception, failure)
                && errors[0].Message.Contains(failingClassId.ToString(), StringComparison.Ordinal)
                && !string.IsNullOrEmpty(failure.StackTrace), "The activation failure lost its CLSID or exception diagnostics.");
            Require(!running.IsCompleted, "A failed activation shut down a server with a live extension.");
        }
        finally
        {
            try
            {
                if (winner != 0)
                {
                    DisposeExtension(winner);
                }
            }
            finally
            {
                Release(winner);
            }
        }

        Require(winnerDisposals == 1, "The winning extension was not disposed exactly once.");
        return running;
    }
}