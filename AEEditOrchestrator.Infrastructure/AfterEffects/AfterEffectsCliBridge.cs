using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Models.Ipc;

namespace AEEditOrchestrator.Infrastructure.AfterEffects;

public sealed class AfterEffectsCliBridge : IAfterEffectsBridge
{
    #region Constants

    private const int DefaultWatchdogTimeoutSeconds = 45;
    private const int GracefulAbortWaitSeconds = 15;
    private const int GracefulCloseWaitMs = 3000;
    private const int ProcessExitWaitTimeoutMs = 8000;
    private const int DirectOpenReadyTimeoutMs = 10000;
    private const int RestartPollIntervalMs = 250;
    private const int CloseHandleReleaseDelayMs = 150;
    private const string OpenProjectScriptCommand = "__open_project__";
    private const string SaveProjectScriptCommand = "__save_project__";
    private const string CloseNoSaveScriptCommand = "__close_no_save__";

    #endregion

    #region Fields

    private readonly IpcPaths PathsField;
    private readonly IpcFileWatcher WatcherField;
    private readonly UTF8Encoding Utf8NoBomEncodingField = new UTF8Encoding(false);
    private readonly SemaphoreSlim ExecutionLockField = new SemaphoreSlim(1, 1);

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

    public AfterEffectsCliBridge()
        : this(new IpcPaths())
    {
    }

    public AfterEffectsCliBridge(IpcPaths Paths)
    {
        ArgumentNullException.ThrowIfNull(Paths);
        this.PathsField = Paths;
        this.WatcherField = new IpcFileWatcher(Paths);
    }

    #endregion

    #region Public Methods

    public bool IsAfterEffectsRunning()
    {
        Process[] Processes = Process.GetProcesses();
        try
        {
            foreach (Process CurrentProcess in Processes)
            {
                if (AfterEffectsCliBridge.IsAfterEffectsProcess(CurrentProcess))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            foreach (Process CurrentProcess in Processes)
            {
                CurrentProcess.Dispose();
            }
        }
    }

    public async Task<StepResult> ExecuteJobAsync(
        JobPayload Payload,
        PipelineSettings Settings,
        IProgress<HeartbeatMessage>? HeartbeatProgress = null,
        CancellationToken CancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Payload);
        ArgumentNullException.ThrowIfNull(Settings);

        await this.ExecutionLockField.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ExecuteJobCoreAsync(
                Payload,
                Settings,
                HeartbeatProgress,
                CancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.ExecutionLockField.Release();
        }
    }

    public async Task<bool> CloseProjectWithoutSavingAsync(
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Settings);

        await this.ExecutionLockField.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            CancellationToken.ThrowIfCancellationRequested();

            if (!this.IsAfterEffectsRunning())
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(Settings.AfterEffectsExecutablePath) || !File.Exists(Settings.AfterEffectsExecutablePath))
            {
                return false;
            }

            string ScriptToRun = this.PathsField.CloseNoSaveScriptPath;
            if (!File.Exists(ScriptToRun))
            {
                ScriptToRun = this.PathsField.RunnerScriptPath;
            }

            if (!File.Exists(ScriptToRun))
            {
                return false;
            }

            string JobId = Guid.NewGuid().ToString("N");
            JobPayload Payload = JobPayload.FromSettings(
                JobId,
                AfterEffectsCliBridge.CloseNoSaveScriptCommand,
                Settings,
                0,
                SaveAfterBatchOverride: false,
                PurgeAfterBatchOverride: false);

            await this.PrepareAndWriteJobFilesAsync(Payload, CancellationToken).ConfigureAwait(false);

            StepResult Result = await this.ExecuteScriptAndWaitAsync(
                JobId,
                ScriptToRun,
                Settings,
                null,
                CancellationToken).ConfigureAwait(false);

            if (Result.IsOk)
            {
                await Task.Delay(AfterEffectsCliBridge.CloseHandleReleaseDelayMs, CancellationToken).ConfigureAwait(false);
                return true;
            }

            return false;
        }
        finally
        {
            this.ExecutionLockField.Release();
        }
    }

    public async Task<bool> OpenProjectAsync(
        string ProjectPath,
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Settings);

        await this.ExecutionLockField.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            CancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(ProjectPath) || !File.Exists(ProjectPath))
            {
                return false;
            }

            string FullProjectPath = Path.GetFullPath(ProjectPath);

            if (this.IsAfterEffectsRunning())
            {
                string JobId = Guid.NewGuid().ToString("N");
                JobPayload Payload = JobPayload.FromSettings(
                    JobId,
                    AfterEffectsCliBridge.OpenProjectScriptCommand,
                    Settings,
                    0,
                    SaveAfterBatchOverride: false,
                    PurgeAfterBatchOverride: false,
                    ProjectToOpen: FullProjectPath);

                StepResult Result = await this.ExecuteJobCoreAsync(
                    Payload,
                    Settings,
                    null,
                    CancellationToken).ConfigureAwait(false);
                return Result.IsOk;
            }

            return await this.LaunchAfterEffectsWithProjectAsync(
                FullProjectPath,
                Settings,
                CancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.ExecutionLockField.Release();
        }
    }

    public async Task<bool> SaveProjectAsync(
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Settings);

        await this.ExecutionLockField.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            CancellationToken.ThrowIfCancellationRequested();

            if (!this.IsAfterEffectsRunning())
            {
                return false;
            }

            string JobId = Guid.NewGuid().ToString("N");
            JobPayload Payload = JobPayload.FromSettings(
                JobId,
                AfterEffectsCliBridge.SaveProjectScriptCommand,
                Settings,
                0,
                SaveAfterBatchOverride: false,
                PurgeAfterBatchOverride: false);

            StepResult Result = await this.ExecuteJobCoreAsync(
                Payload,
                Settings,
                null,
                CancellationToken).ConfigureAwait(false);
            return Result.IsOk;
        }
        finally
        {
            this.ExecutionLockField.Release();
        }
    }

    public async Task<bool> TryRestartAfterEffectsAsync(
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Settings);

        await this.ExecutionLockField.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            CancellationToken.ThrowIfCancellationRequested();

            await this.ShutdownRunningAfterEffectsProcessesAsync(CancellationToken).ConfigureAwait(false);

            Stopwatch Timer = Stopwatch.StartNew();
            while (this.IsAfterEffectsRunning() && Timer.ElapsedMilliseconds < AfterEffectsCliBridge.ProcessExitWaitTimeoutMs)
            {
                CancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(AfterEffectsCliBridge.RestartPollIntervalMs, CancellationToken).ConfigureAwait(false);
            }

            return !this.IsAfterEffectsRunning();
        }
        finally
        {
            this.ExecutionLockField.Release();
        }
    }

    public void CleanupIpcFiles()
    {
        AfterEffectsCliBridge.TryDeleteFile(this.PathsField.JobFilePath);
        AfterEffectsCliBridge.TryDeleteFile(this.PathsField.HeartbeatFilePath);
        AfterEffectsCliBridge.TryDeleteFile(this.PathsField.ResultFilePath);
        AfterEffectsCliBridge.TryDeleteFile(this.PathsField.AbortFlagFilePath);
    }

    #endregion

    #region Private Methods

    private async Task<StepResult> ExecuteJobCoreAsync(
        JobPayload Payload,
        PipelineSettings Settings,
        IProgress<HeartbeatMessage>? HeartbeatProgress,
        CancellationToken CancellationToken)
    {
        CancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(Payload.JobId))
        {
            Payload.JobId = Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(Settings.AfterEffectsExecutablePath) || !File.Exists(Settings.AfterEffectsExecutablePath))
        {
            return new StepResult
            {
                JobId = Payload.JobId,
                Status = "error",
                Error = $"After Effects executable was not found at '{Settings.AfterEffectsExecutablePath}'."
            };
        }

        if (!File.Exists(this.PathsField.RunnerScriptPath))
        {
            return new StepResult
            {
                JobId = Payload.JobId,
                Status = "error",
                Error = $"Runner JSX script was not found at '{this.PathsField.RunnerScriptPath}'."
            };
        }

        await this.PrepareAndWriteJobFilesAsync(Payload, CancellationToken).ConfigureAwait(false);

        return await this.ExecuteScriptAndWaitAsync(
            Payload.JobId,
            this.PathsField.RunnerScriptPath,
            Settings,
            HeartbeatProgress,
            CancellationToken).ConfigureAwait(false);
    }

    private async Task PrepareAndWriteJobFilesAsync(JobPayload Payload, CancellationToken CancellationToken)
    {
        this.PathsField.EnsureBaseDirectoryExists();

        AfterEffectsCliBridge.TryDeleteFile(this.PathsField.HeartbeatFilePath);
        AfterEffectsCliBridge.TryDeleteFile(this.PathsField.ResultFilePath);
        AfterEffectsCliBridge.TryDeleteFile(this.PathsField.AbortFlagFilePath);

        string JsonContent = JsonSerializer.Serialize(Payload, IpcJsonOptions.Default);
        string TempJobPath = Path.Combine(
            this.PathsField.BaseDirectory,
            string.Concat(".job_", Guid.NewGuid().ToString("N"), ".tmp"));

        try
        {
            await File.WriteAllTextAsync(TempJobPath, JsonContent, this.Utf8NoBomEncodingField, CancellationToken).ConfigureAwait(false);
            File.Move(TempJobPath, this.PathsField.JobFilePath, overwrite: true);
        }
        finally
        {
            AfterEffectsCliBridge.TryDeleteFile(TempJobPath);
        }
    }

    private async Task<StepResult> ExecuteScriptAndWaitAsync(
        string JobId,
        string ScriptPath,
        PipelineSettings Settings,
        IProgress<HeartbeatMessage>? HeartbeatProgress,
        CancellationToken CancellationToken)
    {
        bool WasAeRunningBeforeLaunch = this.IsAfterEffectsRunning();
        string? WorkingDirectory = Path.GetDirectoryName(Settings.AfterEffectsExecutablePath);
        string EffectiveWorkingDirectory = this.PathsField.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            EffectiveWorkingDirectory = WorkingDirectory;
        }

        ProcessStartInfo StartInfo = new ProcessStartInfo
        {
            FileName = Settings.AfterEffectsExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = EffectiveWorkingDirectory
        };
        StartInfo.ArgumentList.Add("-r");
        StartInfo.ArgumentList.Add(ScriptPath);

        Process? LaunchedProcess = Process.Start(StartInfo);
        if (LaunchedProcess == null)
        {
            return new StepResult
            {
                JobId = JobId,
                Status = "error",
                Error = $"Failed to start process '{Settings.AfterEffectsExecutablePath}'."
            };
        }

        int TimeoutSeconds = Settings.WatchdogTimeoutSeconds;
        if (TimeoutSeconds <= 0)
        {
            TimeoutSeconds = AfterEffectsCliBridge.DefaultWatchdogTimeoutSeconds;
        }

        using (LaunchedProcess)
        {
            Func<bool> ProcessAliveCheck = () =>
            {
                if (File.Exists(this.PathsField.ResultFilePath))
                {
                    return true;
                }

                if (WasAeRunningBeforeLaunch)
                {
                    return this.IsAfterEffectsRunning();
                }

                if (LaunchedProcess.HasExited)
                {
                    if (LaunchedProcess.ExitCode != 0 && !this.IsAfterEffectsRunning())
                    {
                        return false;
                    }
                }

                return true;
            };

            using (HeartbeatWatchdog Watchdog = new HeartbeatWatchdog(
                TimeoutSeconds,
                CancellationToken,
                ProcessAliveCheck))
            {
                Watchdog.Start();

                try
                {
                    StepResult Result = await this.WatcherField.WaitForResultAsync(
                        JobId,
                        HeartbeatProgress,
                        Watchdog.NotifyHeartbeat,
                        Watchdog.Token).ConfigureAwait(false);

                    Watchdog.MarkCompleted();
                    return Result;
                }
                catch (OperationCanceledException)
                {
                    WatchdogTerminationReason Reason = Watchdog.TerminationReason;

                    if (Reason == WatchdogTerminationReason.UserCancelled || CancellationToken.IsCancellationRequested)
                    {
                        await this.SignalAbortAndDrainAsync(JobId).ConfigureAwait(false);
                        throw new OperationCanceledException(CancellationToken);
                    }

                    if (Reason == WatchdogTerminationReason.HeartbeatTimeout)
                    {
                        await this.SignalAbortAndDrainAsync(JobId).ConfigureAwait(false);
                        return new StepResult
                        {
                            JobId = JobId,
                            Status = "timeout",
                            Error = $"Watchdog timeout after {TimeoutSeconds} seconds while waiting for After Effects job '{JobId}'.",
                            Message = "Watchdog-Timeout"
                        };
                    }

                    if (Reason == WatchdogTerminationReason.ProcessCrashed)
                    {
                        return new StepResult
                        {
                            JobId = JobId,
                            Status = "crashed",
                            Error = $"After Effects process terminated unexpectedly during job '{JobId}'.",
                            Message = "AE-Crash"
                        };
                    }

                    return new StepResult
                    {
                        JobId = JobId,
                        Status = "error",
                        Error = $"After Effects job '{JobId}' was interrupted unexpectedly."
                    };
                }
            }
        }
    }

    private async Task SignalAbortAndDrainAsync(string JobId)
    {
        if (!this.IsAfterEffectsRunning())
        {
            AfterEffectsCliBridge.TryDeleteFile(this.PathsField.AbortFlagFilePath);
            return;
        }

        try
        {
            this.PathsField.EnsureBaseDirectoryExists();
            await File.WriteAllTextAsync(
                this.PathsField.AbortFlagFilePath,
                JobId,
                this.Utf8NoBomEncodingField,
                CancellationToken.None).ConfigureAwait(false);

            using (CancellationTokenSource DrainCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(AfterEffectsCliBridge.GracefulAbortWaitSeconds)))
            {
                try
                {
                    await this.WatcherField.WaitForResultAsync(
                        JobId,
                        null,
                        null,
                        DrainCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        catch (IOException)
        {
        }
        finally
        {
            AfterEffectsCliBridge.TryDeleteFile(this.PathsField.AbortFlagFilePath);
        }
    }

    private async Task<bool> LaunchAfterEffectsWithProjectAsync(
        string FullProjectPath,
        PipelineSettings Settings,
        CancellationToken CancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Settings.AfterEffectsExecutablePath) || !File.Exists(Settings.AfterEffectsExecutablePath))
        {
            return false;
        }

        string? WorkingDirectory = Path.GetDirectoryName(Settings.AfterEffectsExecutablePath);
        string EffectiveWorkingDirectory = this.PathsField.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            EffectiveWorkingDirectory = WorkingDirectory;
        }

        ProcessStartInfo StartInfo = new ProcessStartInfo
        {
            FileName = Settings.AfterEffectsExecutablePath,
            UseShellExecute = false,
            WorkingDirectory = EffectiveWorkingDirectory
        };
        StartInfo.ArgumentList.Add(FullProjectPath);

        Process? StartedProcess = Process.Start(StartInfo);
        if (StartedProcess == null)
        {
            return false;
        }

        using (StartedProcess)
        {
            Stopwatch Timer = Stopwatch.StartNew();
            while (Timer.ElapsedMilliseconds < AfterEffectsCliBridge.DirectOpenReadyTimeoutMs)
            {
                CancellationToken.ThrowIfCancellationRequested();

                if (StartedProcess.HasExited)
                {
                    return this.IsAfterEffectsRunning();
                }

                if (this.IsAfterEffectsRunning())
                {
                    return true;
                }

                await Task.Delay(AfterEffectsCliBridge.RestartPollIntervalMs, CancellationToken).ConfigureAwait(false);
            }

            return this.IsAfterEffectsRunning();
        }
    }

    private async Task ShutdownRunningAfterEffectsProcessesAsync(CancellationToken CancellationToken)
    {
        Process[] Processes = Process.GetProcesses();
        try
        {
            foreach (Process CurrentProcess in Processes)
            {
                if (!AfterEffectsCliBridge.IsAfterEffectsProcess(CurrentProcess))
                {
                    continue;
                }

                try
                {
                    if (!CurrentProcess.HasExited)
                    {
                        CurrentProcess.CloseMainWindow();
                    }
                }
                catch (InvalidOperationException)
                {
                }
                catch (Win32Exception)
                {
                }
            }

            Stopwatch CloseTimer = Stopwatch.StartNew();
            while (this.IsAfterEffectsRunning() && CloseTimer.ElapsedMilliseconds < AfterEffectsCliBridge.GracefulCloseWaitMs)
            {
                CancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(AfterEffectsCliBridge.RestartPollIntervalMs, CancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            foreach (Process CurrentProcess in Processes)
            {
                CurrentProcess.Dispose();
            }
        }

        if (!this.IsAfterEffectsRunning())
        {
            return;
        }

        Process[] RemainingProcesses = Process.GetProcesses();
        try
        {
            foreach (Process CurrentProcess in RemainingProcesses)
            {
                if (!AfterEffectsCliBridge.IsAfterEffectsProcess(CurrentProcess))
                {
                    continue;
                }

                try
                {
                    if (!CurrentProcess.HasExited)
                    {
                        CurrentProcess.Kill(entireProcessTree: true);
                        CurrentProcess.WaitForExit(5000);
                    }
                }
                catch (InvalidOperationException)
                {
                }
                catch (Win32Exception)
                {
                }
                catch (NotSupportedException)
                {
                }
            }
        }
        finally
        {
            foreach (Process CurrentProcess in RemainingProcesses)
            {
                CurrentProcess.Dispose();
            }
        }
    }

    private static bool IsAfterEffectsProcess(Process CandidateProcess)
    {
        try
        {
            string Name = CandidateProcess.ProcessName;
            if (string.IsNullOrWhiteSpace(Name))
            {
                return false;
            }

            if (string.Equals(Name, "AfterFX", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Name, "AfterFX.exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Name, "Adobe After Effects", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Name, "After Effects", StringComparison.OrdinalIgnoreCase)
                || Name.StartsWith("Adobe After Effects", StringComparison.OrdinalIgnoreCase)
                || Name.StartsWith("After Effects", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return !CandidateProcess.HasExited;
                }
                catch (Win32Exception)
                {
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }

            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static void TryDeleteFile(string FilePath)
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    #endregion
}
