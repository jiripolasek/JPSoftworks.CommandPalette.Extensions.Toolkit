// ------------------------------------------------------------
// 
// Copyright (c) Jiří Polášek. All rights reserved.
// 
// ------------------------------------------------------------

using System.ComponentModel;
using System.Diagnostics;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Resources;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Helpers;

/// <summary>
/// Helper class for user experience related tasks.
/// </summary>
public static class StartupHelper
{
    private const string LogCategory = nameof(StartupHelper);
    private const string CommandPalettePackageFamilyName = "Microsoft.CommandPalette_8wekyb3d8bbwe";
    private const string CommandPaletteDevPackageFamilyName = "Microsoft.CommandPalette.Dev_8wekyb3d8bbwe";
    private const string StorePowerToysLink = "ms-windows-store://pdp/?productid=XP89DCGQ3K6VLD";

    /// <summary>
    /// Handles direct launch of the application by checking if PowerToys Command Palette is installed
    /// and attempts to launch it. Shows appropriate messages to the user based on the outcome.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task HandleDirectLaunchAsync()
    {
        return HandleDirectLaunchAsync(TraceExtensionHostLogSink.Instance);
    }

    /// <summary>
    /// Handles direct launch while forwarding diagnostics to the supplied sink.
    /// </summary>
    /// <param name="logSink">The caller-owned diagnostics sink.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task HandleDirectLaunchAsync(IExtensionHostLogSink logSink)
    {
        ArgumentNullException.ThrowIfNull(logSink);

        try
        {
            await HandleDirectLaunchCoreAsync(logSink);
        }
        catch (Exception ex)
        {
            logSink.LogError(LogCategory, ex);
            try
            {
                MessageBoxHelper.Show(
                    Strings.UserExperienceHelper_GeneralErrorOnStart!,
                    Strings.UserExperienceHelper_ErrorCaption!,
                    MessageBoxHelper.IconType.Error,
                    MessageBoxHelper.MessageBoxType.OK);
            }
            catch (Win32Exception displayException)
            {
                logSink.LogError(LogCategory, displayException);
            }
        }
    }

    private static async Task HandleDirectLaunchCoreAsync(IExtensionHostLogSink logSink)
    {
        // Let's add something meaningful for the end-user experience.
        // 1. We are not running as a COM server, so we can show a message box.
        // 2. We can check if PowerToys Command Palette is installed.

        var (retailCommandPalettePackage, devCommandPalettePackage) = FindCommandPaletteApps(new PackagedCommandPaletteAppCatalog(), logSink);

        if (retailCommandPalettePackage != null || devCommandPalettePackage != null)
        {
            var started = await TryStartCommandPaletteAsync(
                retailCommandPalettePackage,
                devCommandPalettePackage,
                logSink);

            if (!started)
            {
                MessageBoxHelper.Show(
                    Strings.UserExperienceHelper_CmdPalIsInstalledButFailedToStart!,
                    Strings.UserExperienceHelper_InfoCaption!,
                    MessageBoxHelper.IconType.Warning,
                    MessageBoxHelper.MessageBoxType.OK);
            }
        }
        else
        {
            MessageBoxHelper.Show(
                Strings.UserExperienceHelper_CmdPalIsNotInstalled!,
                Strings.UserExperienceHelper_InfoCaption!,
                MessageBoxHelper.IconType.Warning,
                MessageBoxHelper.MessageBoxType.OK);
            try
            {
                Process.Start(new ProcessStartInfo(StorePowerToysLink) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                // ignore exception, we just want to open the store link
                logSink.LogError(LogCategory, ex);
            }
        }
    }

    internal static async Task<bool> TryStartCommandPaletteAsync(
        ICommandPaletteApp? retailApp,
        ICommandPaletteApp? devApp,
        IExtensionHostLogSink logSink)
    {
        return await TryStartAsync(retailApp, logSink) || await TryStartAsync(devApp, logSink);
    }

    private static async Task<bool> TryStartAsync(ICommandPaletteApp? app, IExtensionHostLogSink logSink)
    {
        if (app == null)
        {
            return false;
        }

        try
        {
            return await app.LaunchAsync();
        }
        catch (Exception ex)
        {
            logSink.LogError(LogCategory, ex);
        }

        return false;
    }

    internal static PackageDetectionResult FindCommandPaletteApps(ICommandPaletteAppCatalog catalog, IExtensionHostLogSink logSink)
    {
        List<Exception>? discoveryErrors = null;
        var retailApp = TryFind(catalog.FindRetailApp);
        var devApp = TryFind(catalog.FindDevApp);
        if (discoveryErrors != null)
        {
            if (retailApp == null && devApp == null)
            {
                throw new AggregateException("Could not determine whether Command Palette is installed.", discoveryErrors);
            }

            foreach (var error in discoveryErrors)
            {
                logSink.LogError(LogCategory, error);
            }
        }

        return new PackageDetectionResult(retailApp, devApp);

        ICommandPaletteApp? TryFind(Func<ICommandPaletteApp?> find)
        {
            try
            {
                return find();
            }
            catch (Exception ex)
            {
                (discoveryErrors ??= []).Add(ex);
                return null;
            }
        }
    }

    internal readonly record struct PackageDetectionResult(ICommandPaletteApp? RetailApp, ICommandPaletteApp? DevApp);

    private sealed class PackagedCommandPaletteAppCatalog : ICommandPaletteAppCatalog
    {
        private PackageManager? _packageManager;

        public ICommandPaletteApp? FindRetailApp() => this.FindApp(CommandPalettePackageFamilyName);

        public ICommandPaletteApp? FindDevApp() => this.FindApp(CommandPaletteDevPackageFamilyName);

        private ICommandPaletteApp? FindApp(string familyName)
        {
            var package = (this._packageManager ??= new PackageManager()).FindPackagesForUser("", familyName)?.FirstOrDefault();
            return package == null ? null : new PackagedCommandPaletteApp(package);
        }
    }

    private sealed class PackagedCommandPaletteApp(Package package) : ICommandPaletteApp
    {
        public async Task<bool> LaunchAsync()
        {
            var appEntries = await package.GetAppListEntriesAsync()! ?? [];
            if (appEntries.Count > 0 && appEntries[0] != null)
            {
                return await appEntries[0]!.LaunchAsync()!;
            }

            return false;
        }
    }
}
