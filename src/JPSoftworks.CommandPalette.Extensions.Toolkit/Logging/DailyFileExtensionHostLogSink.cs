// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Globalization;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.Abstractions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging;

/// <summary>
/// Writes diagnostics to an automatically flushed file that rolls each local calendar day.
/// </summary>
public sealed class DailyFileExtensionHostLogSink : IExtensionHostLogSink, IDisposable
{
    private readonly string _baseFilePath;
    private readonly Lock _syncRoot = new();

    private DateOnly _fileDate;
    private bool _isDisposed;
    private StreamWriter? _writer;

    /// <summary>
    /// Initializes a daily rolling file sink.
    /// </summary>
    /// <param name="baseFilePath">
    /// The base path used to derive daily file names. For example, <c>log.txt</c> produces
    /// <c>log20260728.txt</c>.
    /// </param>
    public DailyFileExtensionHostLogSink(string baseFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseFilePath);
        this._baseFilePath = Path.GetFullPath(baseFilePath);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this._syncRoot)
        {
            if (this._isDisposed)
            {
                return;
            }

            this._writer?.Dispose();
            this._writer = null;
            this._isDisposed = true;
        }
    }

    /// <inheritdoc />
    public void Write(ExtensionHostLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (this._syncRoot)
        {
            ObjectDisposedException.ThrowIf(this._isDisposed, this);
            this.EnsureWriter(DateOnly.FromDateTime(entry.Timestamp.LocalDateTime));

            this._writer!.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:yyyy-MM-dd HH:mm:ss.fff zzz} [{1}] {2}: {3}",
                    entry.Timestamp,
                    GetLevelName(entry.Level),
                    entry.Category,
                    entry.Message));

            if (entry.Exception != null)
            {
                this._writer.WriteLine(entry.Exception);
            }
        }
    }

    private static string GetLevelName(ExtensionHostLogLevel level)
    {
        return level switch
        {
            ExtensionHostLogLevel.Debug => "DBG",
            ExtensionHostLogLevel.Information => "INF",
            ExtensionHostLogLevel.Warning => "WRN",
            ExtensionHostLogLevel.Error => "ERR",
            _ => level.ToString()
        };
    }

    private void EnsureWriter(DateOnly date)
    {
        if (this._writer != null && this._fileDate == date)
        {
            return;
        }

        this._writer?.Dispose();
        this._writer = null;

        var directory = Path.GetDirectoryName(this._baseFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var fileName = Path.GetFileNameWithoutExtension(this._baseFilePath);
        var extension = Path.GetExtension(this._baseFilePath);
        var suffix = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var dailyFilePath = Path.Combine(directory ?? string.Empty, $"{fileName}{suffix}{extension}");

        var stream = new FileStream(dailyFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        this._writer = new StreamWriter(stream) { AutoFlush = true };
        this._fileDate = date;
    }
}