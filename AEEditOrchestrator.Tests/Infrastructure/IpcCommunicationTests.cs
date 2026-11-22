using System.Text;
using System.Text.Json;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Models.Ipc;
using AEEditOrchestrator.Infrastructure.AfterEffects;

namespace AEEditOrchestrator.Tests.Infrastructure;

public sealed class IpcCommunicationTests : IDisposable
{
    #region Fields

    private readonly string TempIpcDirField;
    private readonly string TempJsxDirField;

    #endregion

    #region Constructors

    public IpcCommunicationTests()
    {
        string UniqueSuffix = Guid.NewGuid().ToString("N");
        this.TempIpcDirField = Path.Combine(Path.GetTempPath(), "AEEditOrchestrator_IpcTests_" + UniqueSuffix, "ipc");
        this.TempJsxDirField = Path.Combine(Path.GetTempPath(), "AEEditOrchestrator_IpcTests_" + UniqueSuffix, "jsx");
    }

    #endregion

    #region Public Methods

    public void Dispose()
    {
        string? ParentDir = Path.GetDirectoryName(this.TempIpcDirField);
        if (!string.IsNullOrWhiteSpace(ParentDir) && Directory.Exists(ParentDir))
        {
            Directory.Delete(ParentDir, recursive: true);
        }
    }

    [Fact]
    public void IpcPathsResolvesDefaultAndOverridePathsCorrectly()
    {
        IpcPaths DefaultPaths = new IpcPaths();
        string ExpectedLocalAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AEEditOrchestrator",
            "ipc");

        Assert.Equal(ExpectedLocalAppData, DefaultPaths.BaseDirectory);
        Assert.Equal(Path.Combine(ExpectedLocalAppData, "job.json"), DefaultPaths.JobFilePath);
        Assert.Equal(Path.Combine(ExpectedLocalAppData, "heartbeat.json"), DefaultPaths.HeartbeatFilePath);
        Assert.Equal(Path.Combine(ExpectedLocalAppData, "result.json"), DefaultPaths.ResultFilePath);
        Assert.Equal(Path.Combine(ExpectedLocalAppData, "abort.flag"), DefaultPaths.AbortFlagFilePath);
        Assert.Equal(Path.Combine(ExpectedLocalAppData, "session_keepalive.flag"), DefaultPaths.SessionKeepAliveFilePath);
        Assert.Equal(Path.Combine(ExpectedLocalAppData, "session_ended.flag"), DefaultPaths.SessionEndedFilePath);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "Resources", "Scripts", "jsx"), DefaultPaths.JsxScriptsDirectory);
        Assert.Equal(Path.Combine(DefaultPaths.JsxScriptsDirectory, "runner.jsx"), DefaultPaths.RunnerScriptPath);
        Assert.Equal(Path.Combine(DefaultPaths.JsxScriptsDirectory, "close_no_save.jsx"), DefaultPaths.CloseNoSaveScriptPath);

        IpcPaths CustomPaths = new IpcPaths(this.TempIpcDirField, this.TempJsxDirField);
        Assert.Equal(Path.GetFullPath(this.TempIpcDirField), CustomPaths.BaseDirectory);
        Assert.Equal(Path.Combine(Path.GetFullPath(this.TempIpcDirField), "job.json"), CustomPaths.JobFilePath);
        Assert.Equal(Path.Combine(Path.GetFullPath(this.TempIpcDirField), "session_keepalive.flag"), CustomPaths.SessionKeepAliveFilePath);
        Assert.Equal(Path.Combine(Path.GetFullPath(this.TempIpcDirField), "session_ended.flag"), CustomPaths.SessionEndedFilePath);
        Assert.Equal(Path.Combine(Path.GetFullPath(this.TempJsxDirField), "runner.jsx"), CustomPaths.RunnerScriptPath);
        Assert.Equal(Path.Combine(Path.GetFullPath(this.TempJsxDirField), "close_no_save.jsx"), CustomPaths.CloseNoSaveScriptPath);
    }

    [Fact]
    public async Task IpcFileWatcherReportsHeartbeatsAndReturnsMatchingResult()
    {
        IpcPaths Paths = new IpcPaths(this.TempIpcDirField, this.TempJsxDirField);
        IpcFileWatcher Watcher = new IpcFileWatcher(Paths, PollIntervalMs: 30);

        List<HeartbeatMessage> ReceivedHeartbeats = new List<HeartbeatMessage>();
        int CallbackInvocations = 0;

        InlineHeartbeatProgress ProgressReporter = new InlineHeartbeatProgress((HeartbeatMessage Message) =>
        {
            lock (ReceivedHeartbeats)
            {
                ReceivedHeartbeats.Add(Message);
            }
        });

        using (CancellationTokenSource TimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            Task<StepResult> WaitTask = Watcher.WaitForResultAsync(
                "job-abc-123",
                ProgressReporter,
                () =>
                {
                    Interlocked.Increment(ref CallbackInvocations);
                },
                TimeoutCts.Token);

            await Task.Delay(60, TimeoutCts.Token);

            HeartbeatMessage Heartbeat1 = new HeartbeatMessage
            {
                JobId = "job-abc-123",
                TimestampUtc = "2026-11-22T11:00:00Z",
                CurrentIndex = 1,
                TotalItems = 5,
                Message = "Clip 1"
            };
            await File.WriteAllTextAsync(
                Paths.HeartbeatFilePath,
                JsonSerializer.Serialize(Heartbeat1, IpcJsonOptions.Default),
                new UTF8Encoding(false),
                TimeoutCts.Token);

            await Task.Delay(80, TimeoutCts.Token);

            HeartbeatMessage Heartbeat2 = new HeartbeatMessage
            {
                JobId = "job-abc-123",
                TimestampUtc = "2026-11-22T11:00:01Z",
                CurrentIndex = 2,
                TotalItems = 5,
                Message = "Clip 2"
            };
            await File.WriteAllTextAsync(
                Paths.HeartbeatFilePath,
                JsonSerializer.Serialize(Heartbeat2, IpcJsonOptions.Default),
                new UTF8Encoding(false),
                TimeoutCts.Token);

            await Task.Delay(80, TimeoutCts.Token);

            StepResult ExpectedResult = new StepResult
            {
                JobId = "job-abc-123",
                Status = "ok",
                TotalClips = 5,
                ProcessedInBatch = 5,
                HasMoreBatches = false
            };
            await File.WriteAllTextAsync(
                Paths.ResultFilePath,
                JsonSerializer.Serialize(ExpectedResult, IpcJsonOptions.Default),
                new UTF8Encoding(false),
                TimeoutCts.Token);

            StepResult ActualResult = await WaitTask;

            Assert.True(ActualResult.IsOk);
            Assert.Equal("job-abc-123", ActualResult.JobId);
            Assert.Equal(5, ActualResult.ProcessedInBatch);
            Assert.Equal(2, CallbackInvocations);
            Assert.Equal(2, ReceivedHeartbeats.Count);
        }
    }

    [Fact]
    public async Task IpcFileWatcherIgnoresOldJobIdAndStripsUtf8Bom()
    {
        IpcPaths Paths = new IpcPaths(this.TempIpcDirField, this.TempJsxDirField);
        Paths.EnsureBaseDirectoryExists();

        StepResult StaleResult = new StepResult
        {
            JobId = "old-job-000",
            Status = "error",
            Error = "Stale result"
        };
        await File.WriteAllTextAsync(
            Paths.ResultFilePath,
            JsonSerializer.Serialize(StaleResult, IpcJsonOptions.Default),
            new UTF8Encoding(true));

        IpcFileWatcher Watcher = new IpcFileWatcher(Paths, PollIntervalMs: 30);

        using (CancellationTokenSource TimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            Task<StepResult> WaitTask = Watcher.WaitForResultAsync("fresh-job-999", null, null, TimeoutCts.Token);

            await Task.Delay(80, TimeoutCts.Token);
            Assert.False(WaitTask.IsCompleted);

            StepResult FreshResult = new StepResult
            {
                JobId = "fresh-job-999",
                Status = "ok",
                TotalClips = 12,
                ProcessedInBatch = 10,
                HasMoreBatches = true,
                NextBatchIndex = 10
            };
            string FreshJson = JsonSerializer.Serialize(FreshResult, IpcJsonOptions.Default);
            await File.WriteAllTextAsync(Paths.ResultFilePath, FreshJson, new UTF8Encoding(true), TimeoutCts.Token);

            StepResult Received = await WaitTask;
            Assert.True(Received.IsOk);
            Assert.Equal("fresh-job-999", Received.JobId);
            Assert.True(Received.HasMoreBatches);
            Assert.Equal(10, Received.NextBatchIndex);
        }
    }

    [Fact]
    public async Task HeartbeatWatchdogTriggersHeartbeatTimeoutWhenNoHeartbeatArrives()
    {
        using (HeartbeatWatchdog Watchdog = new HeartbeatWatchdog(
            TimeSpan.FromMilliseconds(140),
            CancellationToken.None,
            IsProcessAliveCheck: null,
            CheckIntervalOverride: TimeSpan.FromMilliseconds(20)))
        {
            Watchdog.Start();

            await Task.Delay(350);

            Assert.True(Watchdog.Token.IsCancellationRequested);
            Assert.Equal(WatchdogTerminationReason.HeartbeatTimeout, Watchdog.TerminationReason);
        }
    }

    [Fact]
    public async Task HeartbeatWatchdogResetsTimeoutWhenNotifyHeartbeatCalled()
    {
        using (HeartbeatWatchdog Watchdog = new HeartbeatWatchdog(
            TimeSpan.FromMilliseconds(180),
            CancellationToken.None,
            IsProcessAliveCheck: null,
            CheckIntervalOverride: TimeSpan.FromMilliseconds(20)))
        {
            Watchdog.Start();

            for (int Index = 0; Index < 4; Index++)
            {
                await Task.Delay(75);
                Watchdog.NotifyHeartbeat();
                Assert.False(Watchdog.Token.IsCancellationRequested);
            }

            Watchdog.MarkCompleted();
            Assert.Equal(WatchdogTerminationReason.Completed, Watchdog.TerminationReason);
        }
    }

    [Fact]
    public async Task HeartbeatWatchdogDistinguishesProcessCrashAndUserCancellation()
    {
        bool IsProcessAlive = true;

        using (HeartbeatWatchdog CrashWatchdog = new HeartbeatWatchdog(
            TimeSpan.FromSeconds(10),
            CancellationToken.None,
            IsProcessAliveCheck: () => IsProcessAlive,
            CheckIntervalOverride: TimeSpan.FromMilliseconds(20)))
        {
            CrashWatchdog.Start();
            await Task.Delay(50);
            Assert.False(CrashWatchdog.Token.IsCancellationRequested);

            IsProcessAlive = false;
            await Task.Delay(120);

            Assert.True(CrashWatchdog.Token.IsCancellationRequested);
            Assert.Equal(WatchdogTerminationReason.ProcessCrashed, CrashWatchdog.TerminationReason);
        }

        using (CancellationTokenSource UserCts = new CancellationTokenSource())
        {
            using (HeartbeatWatchdog UserWatchdog = new HeartbeatWatchdog(
                TimeSpan.FromSeconds(10),
                UserCts.Token,
                IsProcessAliveCheck: () => true,
                CheckIntervalOverride: TimeSpan.FromMilliseconds(20)))
            {
                UserWatchdog.Start();
                UserCts.Cancel();

                await Task.Delay(60);

                Assert.True(UserWatchdog.Token.IsCancellationRequested);
                Assert.Equal(WatchdogTerminationReason.UserCancelled, UserWatchdog.TerminationReason);
            }
        }
    }

    [Fact]
    public void AfterEffectsCliBridgeCleanupIpcFilesRemovesAllIpcFilesSafely()
    {
        IpcPaths Paths = new IpcPaths(this.TempIpcDirField, this.TempJsxDirField);
        Paths.EnsureBaseDirectoryExists();

        File.WriteAllText(Paths.JobFilePath, "{}");
        File.WriteAllText(Paths.HeartbeatFilePath, "{}");
        File.WriteAllText(Paths.ResultFilePath, "{}");
        File.WriteAllText(Paths.AbortFlagFilePath, "job-1");

        AfterEffectsCliBridge Bridge = new AfterEffectsCliBridge(Paths);
        Bridge.CleanupIpcFiles();

        Assert.False(File.Exists(Paths.JobFilePath));
        Assert.False(File.Exists(Paths.HeartbeatFilePath));
        Assert.False(File.Exists(Paths.ResultFilePath));
        Assert.False(File.Exists(Paths.AbortFlagFilePath));
    }

    [Fact]
    public async Task AfterEffectsCliBridgeExecuteJobAsyncReturnsErrorWhenExecutableMissing()
    {
        IpcPaths Paths = new IpcPaths(this.TempIpcDirField, this.TempJsxDirField);
        AfterEffectsCliBridge Bridge = new AfterEffectsCliBridge(Paths);
        PipelineSettings Settings = new PipelineSettings
        {
            AfterEffectsExecutablePath = Path.Combine(this.TempIpcDirField, "MissingAfterFX.exe")
        };

        JobPayload Payload = JobPayload.FromSettings("job-missing-exe", "00_PreflightCheck.jsx", Settings, 0);
        StepResult Result = await Bridge.ExecuteJobAsync(Payload, Settings);

        Assert.False(Result.IsOk);
        Assert.Equal("error", Result.Status);
        Assert.False(string.IsNullOrWhiteSpace(Result.Error));
    }

    [Fact]
    public async Task IpcFileWatcherTriggersOnHeartbeatReceivedForSilentStartupAckHeartbeat()
    {
        IpcPaths Paths = new IpcPaths(this.TempIpcDirField, this.TempJsxDirField);
        IpcFileWatcher Watcher = new IpcFileWatcher(Paths, PollIntervalMs: 30);

        int CallbackInvocations = 0;
        List<HeartbeatMessage> ReceivedHeartbeats = new List<HeartbeatMessage>();
        InlineHeartbeatProgress ProgressReporter = new InlineHeartbeatProgress((HeartbeatMessage Message) =>
        {
            lock (ReceivedHeartbeats)
            {
                ReceivedHeartbeats.Add(Message);
            }
        });

        using (CancellationTokenSource TimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            Task<StepResult> WaitTask = Watcher.WaitForResultAsync(
                "job-ack-1",
                ProgressReporter,
                () =>
                {
                    Interlocked.Increment(ref CallbackInvocations);
                },
                TimeoutCts.Token);

            await Task.Delay(60, TimeoutCts.Token);

            HeartbeatMessage AckHeartbeat = new HeartbeatMessage
            {
                JobId = "job-ack-1",
                TimestampUtc = "2026-11-22T12:00:00Z",
                CurrentIndex = 0,
                TotalItems = 0,
                Message = string.Empty
            };
            await File.WriteAllTextAsync(
                Paths.HeartbeatFilePath,
                JsonSerializer.Serialize(AckHeartbeat, IpcJsonOptions.Default),
                new UTF8Encoding(false),
                TimeoutCts.Token);

            await Task.Delay(80, TimeoutCts.Token);

            StepResult ExpectedResult = new StepResult
            {
                JobId = "job-ack-1",
                Status = "ok",
                TotalClips = 1,
                ProcessedInBatch = 1,
                HasMoreBatches = false
            };
            await File.WriteAllTextAsync(
                Paths.ResultFilePath,
                JsonSerializer.Serialize(ExpectedResult, IpcJsonOptions.Default),
                new UTF8Encoding(false),
                TimeoutCts.Token);

            StepResult ActualResult = await WaitTask;

            Assert.True(ActualResult.IsOk);
            Assert.Equal(1, CallbackInvocations);
            Assert.Single(ReceivedHeartbeats);
            Assert.Equal(string.Empty, ReceivedHeartbeats[0].Message);
        }
    }

    [Fact]
    public async Task AfterEffectsCliBridgeSessionFlagsRespectNestingAndPreserveKeepAliveOnCleanup()
    {
        IpcPaths Paths = new IpcPaths(this.TempIpcDirField, this.TempJsxDirField);
        AfterEffectsCliBridge Bridge = new AfterEffectsCliBridge(Paths);

        Bridge.BeginSession();
        Assert.True(File.Exists(Paths.SessionKeepAliveFilePath));

        await File.WriteAllTextAsync(Paths.JobFilePath, "{}", new UTF8Encoding(false));
        Bridge.CleanupIpcFiles();

        Assert.False(File.Exists(Paths.JobFilePath));
        Assert.True(File.Exists(Paths.SessionKeepAliveFilePath));

        Bridge.BeginSession();
        Assert.True(File.Exists(Paths.SessionKeepAliveFilePath));

        await Bridge.EndSessionAsync();
        Assert.True(File.Exists(Paths.SessionKeepAliveFilePath));

        await Bridge.EndSessionAsync();
        Assert.False(File.Exists(Paths.SessionKeepAliveFilePath));

        Bridge.CleanupIpcFiles();
        Assert.False(File.Exists(Paths.SessionKeepAliveFilePath));
        Assert.False(File.Exists(Paths.SessionEndedFilePath));
    }

    #endregion

    #region Nested Types

    private sealed class InlineHeartbeatProgress : IProgress<HeartbeatMessage>
    {
        #region Fields

        private readonly Action<HeartbeatMessage> HandlerField;

        #endregion

        #region Constructors

        public InlineHeartbeatProgress(Action<HeartbeatMessage> Handler)
        {
            this.HandlerField = Handler;
        }

        #endregion

        #region Public Methods

        public void Report(HeartbeatMessage Value)
        {
            this.HandlerField(Value);
        }

        #endregion
    }

    #endregion
}
