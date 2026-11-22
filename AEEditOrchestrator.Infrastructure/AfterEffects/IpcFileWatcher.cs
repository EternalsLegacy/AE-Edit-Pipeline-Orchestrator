using System.Globalization;
using System.Text;
using System.Text.Json;
using AEEditOrchestrator.Core.Models.Ipc;

namespace AEEditOrchestrator.Infrastructure.AfterEffects;

public sealed class IpcFileWatcher
{
    #region Constants

    private const int DefaultPollIntervalMs = 100;
    private const int MaxReadRetries = 5;
    private const int ReadRetryDelayMs = 25;

    #endregion

    #region Fields

    private readonly IpcPaths PathsField;
    private readonly int PollIntervalMsField;

    #endregion

    #region Properties

    public IpcPaths Paths
    {
        get
        {
            return this.PathsField;
        }
    }

    #endregion

    #region Constructors

    public IpcFileWatcher(IpcPaths Paths, int PollIntervalMs = DefaultPollIntervalMs)
    {
        ArgumentNullException.ThrowIfNull(Paths);
        this.PathsField = Paths;

        if (PollIntervalMs <= 0)
        {
            this.PollIntervalMsField = IpcFileWatcher.DefaultPollIntervalMs;
        }
        else
        {
            this.PollIntervalMsField = PollIntervalMs;
        }
    }

    #endregion

    #region Public Methods

    public async Task<StepResult> WaitForResultAsync(
        string ExpectedJobId,
        IProgress<HeartbeatMessage>? HeartbeatProgress = null,
        Action? OnHeartbeatReceived = null,
        CancellationToken CancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ExpectedJobId))
        {
            throw new ArgumentException("ExpectedJobId must not be empty.", nameof(ExpectedJobId));
        }

        this.PathsField.EnsureBaseDirectoryExists();

        string LastHeartbeatFingerprint = string.Empty;

        using (SemaphoreSlim Signal = new SemaphoreSlim(0))
        {
            using (FileSystemWatcher Watcher = new FileSystemWatcher(this.PathsField.BaseDirectory, "*.json"))
            {
                Watcher.NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
                    | NotifyFilters.CreationTime;

                FileSystemEventHandler ChangedHandler = (object Sender, FileSystemEventArgs Args) =>
                {
                    this.ReleaseSignalIfRelevant(Signal, Args.Name);
                };

                RenamedEventHandler RenamedHandler = (object Sender, RenamedEventArgs Args) =>
                {
                    this.ReleaseSignalIfRelevant(Signal, Args.Name);
                };

                ErrorEventHandler ErrorHandler = (object Sender, ErrorEventArgs Args) =>
                {
                    this.SafeReleaseSignal(Signal);
                };

                Watcher.Created += ChangedHandler;
                Watcher.Changed += ChangedHandler;
                Watcher.Renamed += RenamedHandler;
                Watcher.Error += ErrorHandler;
                Watcher.EnableRaisingEvents = true;

                try
                {
                    while (true)
                    {
                        CancellationToken.ThrowIfCancellationRequested();

                        LastHeartbeatFingerprint = await this.PollHeartbeatAsync(
                            ExpectedJobId,
                            LastHeartbeatFingerprint,
                            HeartbeatProgress,
                            OnHeartbeatReceived,
                            CancellationToken).ConfigureAwait(false);

                        StepResult? Result = await this.TryReadMatchingResultAsync(
                            ExpectedJobId,
                            CancellationToken).ConfigureAwait(false);

                        if (Result != null)
                        {
                            await this.PollHeartbeatAsync(
                                ExpectedJobId,
                                LastHeartbeatFingerprint,
                                HeartbeatProgress,
                                OnHeartbeatReceived,
                                CancellationToken).ConfigureAwait(false);

                            return Result;
                        }

                        await Signal.WaitAsync(this.PollIntervalMsField, CancellationToken).ConfigureAwait(false);

                        while (Signal.Wait(0))
                        {
                        }
                    }
                }
                finally
                {
                    Watcher.EnableRaisingEvents = false;
                    Watcher.Created -= ChangedHandler;
                    Watcher.Changed -= ChangedHandler;
                    Watcher.Renamed -= RenamedHandler;
                    Watcher.Error -= ErrorHandler;
                }
            }
        }
    }

    #endregion

    #region Private Methods

    private void ReleaseSignalIfRelevant(SemaphoreSlim Signal, string? FileName)
    {
        if (string.IsNullOrWhiteSpace(FileName))
        {
            this.SafeReleaseSignal(Signal);
            return;
        }

        bool IsHeartbeat = string.Equals(FileName, Path.GetFileName(this.PathsField.HeartbeatFilePath), StringComparison.OrdinalIgnoreCase);
        bool IsResult = string.Equals(FileName, Path.GetFileName(this.PathsField.ResultFilePath), StringComparison.OrdinalIgnoreCase);

        if (IsHeartbeat || IsResult)
        {
            this.SafeReleaseSignal(Signal);
        }
    }

    private void SafeReleaseSignal(SemaphoreSlim Signal)
    {
        try
        {
            Signal.Release();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task<string> PollHeartbeatAsync(
        string ExpectedJobId,
        string LastHeartbeatFingerprint,
        IProgress<HeartbeatMessage>? HeartbeatProgress,
        Action? OnHeartbeatReceived,
        CancellationToken CancellationToken)
    {
        HeartbeatMessage? Heartbeat = await this.TryReadJsonWithRetryAsync<HeartbeatMessage>(
            this.PathsField.HeartbeatFilePath,
            CancellationToken).ConfigureAwait(false);

        if (Heartbeat == null)
        {
            return LastHeartbeatFingerprint;
        }

        if (!string.Equals(Heartbeat.JobId, ExpectedJobId, StringComparison.OrdinalIgnoreCase))
        {
            return LastHeartbeatFingerprint;
        }

        string Fingerprint = string.Concat(
            Heartbeat.JobId,
            "|",
            Heartbeat.TimestampUtc,
            "|",
            Heartbeat.CurrentIndex.ToString(CultureInfo.InvariantCulture),
            "|",
            Heartbeat.TotalItems.ToString(CultureInfo.InvariantCulture),
            "|",
            Heartbeat.Message);

        if (!string.Equals(Fingerprint, LastHeartbeatFingerprint, StringComparison.Ordinal))
        {
            if (HeartbeatProgress != null)
            {
                HeartbeatProgress.Report(Heartbeat);
            }

            if (OnHeartbeatReceived != null)
            {
                OnHeartbeatReceived();
            }

            return Fingerprint;
        }

        return LastHeartbeatFingerprint;
    }

    private async Task<StepResult?> TryReadMatchingResultAsync(
        string ExpectedJobId,
        CancellationToken CancellationToken)
    {
        StepResult? Result = await this.TryReadJsonWithRetryAsync<StepResult>(
            this.PathsField.ResultFilePath,
            CancellationToken).ConfigureAwait(false);

        if (Result == null)
        {
            return null;
        }

        if (!string.Equals(Result.JobId, ExpectedJobId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Result;
    }

    private async Task<T?> TryReadJsonWithRetryAsync<T>(string FilePath, CancellationToken CancellationToken)
    {
        if (!File.Exists(FilePath))
        {
            return default;
        }

        for (int Attempt = 0; Attempt < IpcFileWatcher.MaxReadRetries; Attempt++)
        {
            CancellationToken.ThrowIfCancellationRequested();

            try
            {
                using (FileStream Stream = new FileStream(
                    FilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    using (StreamReader Reader = new StreamReader(Stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true))
                    {
                        string RawJson = await Reader.ReadToEndAsync(CancellationToken).ConfigureAwait(false);
                        string CleanedJson = RawJson.Trim().TrimStart('\uFEFF');

                        if (string.IsNullOrWhiteSpace(CleanedJson))
                        {
                            if (Attempt < IpcFileWatcher.MaxReadRetries - 1)
                            {
                                await Task.Delay(IpcFileWatcher.ReadRetryDelayMs, CancellationToken).ConfigureAwait(false);
                                continue;
                            }

                            return default;
                        }

                        T? Deserialized = JsonSerializer.Deserialize<T>(CleanedJson, IpcJsonOptions.Default);
                        return Deserialized;
                    }
                }
            }
            catch (FileNotFoundException)
            {
                return default;
            }
            catch (DirectoryNotFoundException)
            {
                return default;
            }
            catch (IOException)
            {
                if (Attempt < IpcFileWatcher.MaxReadRetries - 1)
                {
                    await Task.Delay(IpcFileWatcher.ReadRetryDelayMs, CancellationToken).ConfigureAwait(false);
                }
            }
            catch (JsonException)
            {
                if (Attempt < IpcFileWatcher.MaxReadRetries - 1)
                {
                    await Task.Delay(IpcFileWatcher.ReadRetryDelayMs, CancellationToken).ConfigureAwait(false);
                }
            }
        }

        return default;
    }

    #endregion
}
