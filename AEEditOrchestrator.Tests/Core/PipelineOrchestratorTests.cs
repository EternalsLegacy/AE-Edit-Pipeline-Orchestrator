using AEEditOrchestrator.Core.Engine;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Models.Ipc;
using AEEditOrchestrator.Tests.Core.TestDoubles;

namespace AEEditOrchestrator.Tests.Core;

public sealed class PipelineOrchestratorTests
{
    #region Private Methods

    private static PipelineOrchestrator CreateOrchestrator(
        FakeAfterEffectsBridge Bridge,
        FakeTransactionManager TransactionManager)
    {
        StepRegistry Registry = StepRegistry.CreateDefault(Bridge);
        StepDependencyValidator Validator = new StepDependencyValidator(Registry);
        return new PipelineOrchestrator(Registry, Validator, Bridge, TransactionManager);
    }

    #endregion

    #region Public Methods

    [Fact]
    public async Task ExecutePipelineAsyncHappyPathStage1AndStage2CreatesMilestoneSnapshotCommitsAndRotates()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FakeTransactionManager TransactionManager = new FakeTransactionManager();
        PipelineOrchestrator Orchestrator = PipelineOrchestratorTests.CreateOrchestrator(Bridge, TransactionManager);

        PipelineSettings Settings = new PipelineSettings
        {
            MaxBackupsToKeep = 3
        };

        string[] UnorderedSelectedSteps = new string[]
        {
            PipelineStepIds.BlurZoom,
            PipelineStepIds.ZoomGroup,
            PipelineStepIds.EndBlurs,
            PipelineStepIds.Precomp,
            PipelineStepIds.Shakes,
            PipelineStepIds.Fade,
            PipelineStepIds.StartBlur,
            PipelineStepIds.Beats
        };

        RecordingProgress<PipelineProgressUpdate> Progress = new RecordingProgress<PipelineProgressUpdate>();

        PipelineExecutionResult Result = await Orchestrator.ExecutePipelineAsync(
            Settings,
            UnorderedSelectedSteps,
            Progress,
            CancellationToken.None);

        Assert.True(Result.Succeeded);
        Assert.False(Result.WasCancelled);
        Assert.False(Result.WasRolledBack);
        Assert.Equal("tx-0001", Result.TransactionId);
        Assert.Equal(8, Result.StepResults.Count);

        Assert.Equal(1, TransactionManager.BeginCallCount);
        Assert.Equal(1, TransactionManager.MilestoneCallCount);
        Assert.Equal(1, TransactionManager.CommitCallCount);
        Assert.Equal(0, TransactionManager.RollbackCallCount);
        Assert.Equal(1, TransactionManager.RotateCallCount);
        Assert.Equal(3, TransactionManager.LastRotateMaxBackups);

        Assert.Single(TransactionManager.CreatedMilestones);
        Assert.Equal("01_After_Stage1.aep", TransactionManager.CreatedMilestones[0].FileName);
        Assert.Equal(TransactionStatus.Committed, TransactionManager.CreatedManifests[0].Status);

        Assert.Equal(9, Bridge.RecordedJobs.Count);
        Assert.Equal("00_PreflightCheck.jsx", Bridge.RecordedJobs[0].ScriptFile);
        Assert.Equal(7, Bridge.RecordedJobs[0].RequiredPresets.Count);
        Assert.Equal("01_BeatDetection.jsx", Bridge.RecordedJobs[1].ScriptFile);
        Assert.Equal("02_PrecomposeClips.jsx", Bridge.RecordedJobs[2].ScriptFile);
        Assert.Equal("03_ApplyFadeInOut.jsx", Bridge.RecordedJobs[3].ScriptFile);
        Assert.Equal("04_ApplyMotionTileAndShakes.jsx", Bridge.RecordedJobs[4].ScriptFile);
        Assert.Equal("05_CreateStartBlurs.jsx", Bridge.RecordedJobs[5].ScriptFile);
        Assert.Equal("06_CreateEndBlurs.jsx", Bridge.RecordedJobs[6].ScriptFile);
        Assert.Equal("07_GroupClipsForZoom.jsx", Bridge.RecordedJobs[7].ScriptFile);
        Assert.Equal("08_ApplyBlurZoom.jsx", Bridge.RecordedJobs[8].ScriptFile);

        Assert.Equal(3, Bridge.SaveCallCount);
        Assert.True(Bridge.CleanupCallCount >= 2);

        int MilestoneIndex = -1;
        int Stage1EndIndex = -1;
        int Stage2StartIndex = -1;

        for (int Index = 0; Index < Bridge.CallLog.Count; Index++)
        {
            if (Bridge.CallLog[Index].Contains("06_CreateEndBlurs.jsx", StringComparison.Ordinal))
            {
                Stage1EndIndex = Index;
            }

            if (Bridge.CallLog[Index].Contains("07_GroupClipsForZoom.jsx", StringComparison.Ordinal))
            {
                Stage2StartIndex = Index;
            }
        }

        for (int Index = 0; Index < TransactionManager.CallLog.Count; Index++)
        {
            if (TransactionManager.CallLog[Index].Contains("01_After_Stage1.aep", StringComparison.Ordinal))
            {
                MilestoneIndex = Index;
            }
        }

        Assert.True(Stage1EndIndex >= 0);
        Assert.True(Stage2StartIndex > Stage1EndIndex);
        Assert.True(MilestoneIndex >= 0);
        Assert.Equal(100.0, Progress.Items[Progress.Items.Count - 1].Percentage);
    }

    [Fact]
    public async Task ExecutePipelineAsyncStage1OnlyDoesNotCreateStage2MilestoneSnapshot()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FakeTransactionManager TransactionManager = new FakeTransactionManager();
        PipelineOrchestrator Orchestrator = PipelineOrchestratorTests.CreateOrchestrator(Bridge, TransactionManager);

        PipelineExecutionResult Result = await Orchestrator.ExecutePipelineAsync(
            new PipelineSettings(),
            new string[] { PipelineStepIds.Precomp, PipelineStepIds.Fade },
            null,
            CancellationToken.None);

        Assert.True(Result.Succeeded);
        Assert.Equal(0, TransactionManager.MilestoneCallCount);
        Assert.Equal(1, TransactionManager.CommitCallCount);
        Assert.Equal(2, Bridge.SaveCallCount);
    }

    [Fact]
    public async Task ExecutePipelineAsyncStage2OnlyWithStage1TagsDoesNotCreateMilestoneSnapshot()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FakeTransactionManager TransactionManager = new FakeTransactionManager();
        PipelineOrchestrator Orchestrator = PipelineOrchestratorTests.CreateOrchestrator(Bridge, TransactionManager);

        Bridge.JobHandler = (Payload, Settings, HeartbeatProgress, CancellationToken) =>
        {
            StepResult Result = FakeAfterEffectsBridge.CreateDefaultOkResult(Payload);
            Result.DetectedState!.HasStage1Tags = true;
            return Task.FromResult(Result);
        };

        PipelineExecutionResult Result = await Orchestrator.ExecutePipelineAsync(
            new PipelineSettings(),
            new string[] { PipelineStepIds.ZoomGroup, PipelineStepIds.BlurZoom },
            null,
            CancellationToken.None);

        Assert.True(Result.Succeeded);
        Assert.Equal(0, TransactionManager.MilestoneCallCount);
        Assert.Equal(1, TransactionManager.CommitCallCount);
    }

    [Fact]
    public async Task ExecutePipelineAsyncBatchingStepWithHasMoreBatchesProcessesAllBatchesAndCommits()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FakeTransactionManager TransactionManager = new FakeTransactionManager();
        PipelineOrchestrator Orchestrator = PipelineOrchestratorTests.CreateOrchestrator(Bridge, TransactionManager);

        Bridge.JobHandler = (Payload, Settings, HeartbeatProgress, CancellationToken) =>
        {
            if (Payload.ScriptFile == "00_PreflightCheck.jsx")
            {
                return Task.FromResult(FakeAfterEffectsBridge.CreateDefaultOkResult(Payload));
            }

            if (Payload.BatchStartIndex == 0)
            {
                return Task.FromResult(new StepResult
                {
                    JobId = Payload.JobId,
                    Status = "ok",
                    TotalClips = 22,
                    ProcessedInBatch = 10,
                    SkippedInBatch = 0,
                    HasMoreBatches = true,
                    NextBatchIndex = 10
                });
            }

            if (Payload.BatchStartIndex == 10)
            {
                return Task.FromResult(new StepResult
                {
                    JobId = Payload.JobId,
                    Status = "ok",
                    TotalClips = 22,
                    ProcessedInBatch = 10,
                    SkippedInBatch = 0,
                    HasMoreBatches = true,
                    NextBatchIndex = 20
                });
            }

            return Task.FromResult(new StepResult
            {
                JobId = Payload.JobId,
                Status = "ok",
                TotalClips = 22,
                ProcessedInBatch = 2,
                SkippedInBatch = 0,
                HasMoreBatches = false,
                NextBatchIndex = 22
            });
        };

        PipelineExecutionResult Result = await Orchestrator.ExecutePipelineAsync(
            new PipelineSettings(),
            new string[] { PipelineStepIds.Precomp },
            null,
            CancellationToken.None);

        Assert.True(Result.Succeeded);
        Assert.Single(Result.StepResults);
        Assert.Equal(22, Result.StepResults[0].TotalClips);
        Assert.Equal(22, Result.StepResults[0].ProcessedClips);
        Assert.Equal(4, Bridge.RecordedJobs.Count);
        Assert.Equal(1, TransactionManager.CommitCallCount);
    }

    [Fact]
    public async Task ExecutePipelineAsyncStepErrorAutomaticallyRollsBackAndCleansUpIpc()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FakeTransactionManager TransactionManager = new FakeTransactionManager();
        PipelineOrchestrator Orchestrator = PipelineOrchestratorTests.CreateOrchestrator(Bridge, TransactionManager);

        Bridge.JobHandler = (Payload, Settings, HeartbeatProgress, CancellationToken) =>
        {
            if (Payload.ScriptFile == "04_ApplyMotionTileAndShakes.jsx")
            {
                return Task.FromResult(new StepResult
                {
                    JobId = Payload.JobId,
                    Status = "error",
                    Error = "Preset VerticalShake konnte nicht angewendet werden.",
                    ErrorLine = 42
                });
            }

            return Task.FromResult(FakeAfterEffectsBridge.CreateDefaultOkResult(Payload));
        };

        RecordingProgress<PipelineProgressUpdate> Progress = new RecordingProgress<PipelineProgressUpdate>();

        PipelineExecutionResult Result = await Orchestrator.ExecutePipelineAsync(
            new PipelineSettings(),
            new string[] { PipelineStepIds.Precomp, PipelineStepIds.Shakes, PipelineStepIds.ZoomGroup },
            Progress,
            CancellationToken.None);

        Assert.False(Result.Succeeded);
        Assert.True(Result.WasRolledBack);
        Assert.False(Result.WasCancelled);
        Assert.Contains("VerticalShake", Result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Zeile 42", Result.ErrorMessage, StringComparison.Ordinal);

        Assert.Equal(1, TransactionManager.BeginCallCount);
        Assert.Equal(1, TransactionManager.RollbackCallCount);
        Assert.Equal(0, TransactionManager.CommitCallCount);
        Assert.Equal(0, TransactionManager.RotateCallCount);
        Assert.Equal(TransactionStatus.RolledBack, TransactionManager.CreatedManifests[0].Status);

        Assert.True(Bridge.CleanupCallCount >= 2);
        Assert.DoesNotContain(Bridge.RecordedJobs, Job => Job.ScriptFile == "07_GroupClipsForZoom.jsx");
        Assert.Contains(Progress.Items, Item => Item.IsError);
    }

    [Fact]
    public async Task ExecutePipelineAsyncUnexpectedStepExceptionAutomaticallyRollsBack()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FakeTransactionManager TransactionManager = new FakeTransactionManager();
        PipelineOrchestrator Orchestrator = PipelineOrchestratorTests.CreateOrchestrator(Bridge, TransactionManager);

        Bridge.JobHandler = (Payload, Settings, HeartbeatProgress, CancellationToken) =>
        {
            if (Payload.ScriptFile == "02_PrecomposeClips.jsx")
            {
                throw new IOException("IPC Schreibfehler");
            }

            return Task.FromResult(FakeAfterEffectsBridge.CreateDefaultOkResult(Payload));
        };

        PipelineExecutionResult Result = await Orchestrator.ExecutePipelineAsync(
            new PipelineSettings(),
            new string[] { PipelineStepIds.Precomp },
            null,
            CancellationToken.None);

        Assert.False(Result.Succeeded);
        Assert.True(Result.WasRolledBack);
        Assert.False(Result.WasCancelled);
        Assert.Contains("IPC Schreibfehler", Result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(1, TransactionManager.RollbackCallCount);
        Assert.Equal(0, TransactionManager.CommitCallCount);
    }

    [Fact]
    public async Task ExecutePipelineAsyncCancelledMidPipelineAutomaticallyRollsBackAndMarksWasCancelled()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FakeTransactionManager TransactionManager = new FakeTransactionManager();
        PipelineOrchestrator Orchestrator = PipelineOrchestratorTests.CreateOrchestrator(Bridge, TransactionManager);
        CancellationTokenSource CancellationSource = new CancellationTokenSource();

        Bridge.JobHandler = (Payload, Settings, HeartbeatProgress, CancellationToken) =>
        {
            if (Payload.ScriptFile == "03_ApplyFadeInOut.jsx")
            {
                CancellationSource.Cancel();
                CancellationToken.ThrowIfCancellationRequested();
            }

            return Task.FromResult(FakeAfterEffectsBridge.CreateDefaultOkResult(Payload));
        };

        PipelineExecutionResult Result = await Orchestrator.ExecutePipelineAsync(
            new PipelineSettings(),
            new string[] { PipelineStepIds.Precomp, PipelineStepIds.Fade, PipelineStepIds.ZoomGroup },
            null,
            CancellationSource.Token);

        Assert.False(Result.Succeeded);
        Assert.True(Result.WasCancelled);
        Assert.True(Result.WasRolledBack);
        Assert.Equal(1, TransactionManager.BeginCallCount);
        Assert.Equal(1, TransactionManager.RollbackCallCount);
        Assert.Equal(0, TransactionManager.CommitCallCount);
        Assert.Equal(TransactionStatus.RolledBack, TransactionManager.CreatedManifests[0].Status);
        Assert.True(Bridge.CleanupCallCount >= 2);
    }

    [Fact]
    public async Task ExecutePipelineAsyncInternalTimeoutCancellationRollsBackWithoutMarkingUserCancelled()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FakeTransactionManager TransactionManager = new FakeTransactionManager();
        PipelineOrchestrator Orchestrator = PipelineOrchestratorTests.CreateOrchestrator(Bridge, TransactionManager);

        Bridge.JobHandler = (Payload, Settings, HeartbeatProgress, CancellationToken) =>
        {
            if (Payload.ScriptFile == "02_PrecomposeClips.jsx")
            {
                throw new OperationCanceledException("Watchdog timeout");
            }

            return Task.FromResult(FakeAfterEffectsBridge.CreateDefaultOkResult(Payload));
        };

        PipelineExecutionResult Result = await Orchestrator.ExecutePipelineAsync(
            new PipelineSettings(),
            new string[] { PipelineStepIds.Precomp },
            null,
            CancellationToken.None);

        Assert.False(Result.Succeeded);
        Assert.False(Result.WasCancelled);
        Assert.True(Result.WasRolledBack);
        Assert.Equal(1, TransactionManager.RollbackCallCount);
    }

    [Fact]
    public async Task RunPreflightAsyncAfterEffectsNotRunningReturnsNotReadyWithoutCallingBridgeJob()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge
        {
            IsRunning = false
        };
        FakeTransactionManager TransactionManager = new FakeTransactionManager();
        PipelineOrchestrator Orchestrator = PipelineOrchestratorTests.CreateOrchestrator(Bridge, TransactionManager);

        ProjectPreflightState State = await Orchestrator.RunPreflightAsync(new PipelineSettings());

        Assert.False(State.IsAfterEffectsRunning);
        Assert.False(State.IsReady);
        Assert.NotEmpty(State.ErrorMessage);
        Assert.Empty(Bridge.RecordedJobs);
    }

    [Fact]
    public async Task ExecutePipelineAsyncPreflightNotReadyAbortsBeforeTransaction()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FakeTransactionManager TransactionManager = new FakeTransactionManager();
        PipelineOrchestrator Orchestrator = PipelineOrchestratorTests.CreateOrchestrator(Bridge, TransactionManager);

        Bridge.JobHandler = (Payload, Settings, HeartbeatProgress, CancellationToken) =>
        {
            StepResult Result = FakeAfterEffectsBridge.CreateDefaultOkResult(Payload);
            Result.DetectedState!.IsProjectSaved = false;
            return Task.FromResult(Result);
        };

        PipelineExecutionResult Result = await Orchestrator.ExecutePipelineAsync(
            new PipelineSettings(),
            new string[] { PipelineStepIds.Beats },
            null,
            CancellationToken.None);

        Assert.False(Result.Succeeded);
        Assert.False(Result.WasRolledBack);
        Assert.Equal(0, TransactionManager.BeginCallCount);
        Assert.Equal(0, Bridge.SaveCallCount);
    }

    [Fact]
    public async Task ExecutePipelineAsyncMissingPresetForSelectedStepFailsWhileUnselectedStepMissingPresetPasses()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FakeTransactionManager TransactionManager = new FakeTransactionManager();
        PipelineOrchestrator Orchestrator = PipelineOrchestratorTests.CreateOrchestrator(Bridge, TransactionManager);

        Bridge.JobHandler = (Payload, Settings, HeartbeatProgress, CancellationToken) =>
        {
            StepResult Result = FakeAfterEffectsBridge.CreateDefaultOkResult(Payload);
            Result.DetectedState!.MissingPresets = new List<string> { "BlurZoomIn" };
            return Task.FromResult(Result);
        };

        PipelineExecutionResult Stage1Result = await Orchestrator.ExecutePipelineAsync(
            new PipelineSettings(),
            new string[] { PipelineStepIds.Precomp, PipelineStepIds.Fade },
            null,
            CancellationToken.None);

        Assert.True(Stage1Result.Succeeded);

        PipelineExecutionResult Stage2Result = await Orchestrator.ExecutePipelineAsync(
            new PipelineSettings(),
            new string[] { PipelineStepIds.Precomp, PipelineStepIds.ZoomGroup, PipelineStepIds.BlurZoom },
            null,
            CancellationToken.None);

        Assert.False(Stage2Result.Succeeded);
        Assert.Contains("BlurZoomIn", Stage2Result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(1, TransactionManager.BeginCallCount);
    }

    [Fact]
    public async Task ExecuteReverseRollbackAsyncUsesRequestedModeWithoutMutatingSettings()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FakeTransactionManager TransactionManager = new FakeTransactionManager();
        PipelineOrchestrator Orchestrator = PipelineOrchestratorTests.CreateOrchestrator(Bridge, TransactionManager);

        PipelineSettings Settings = new PipelineSettings
        {
            ReverseMode = ReverseRollbackMode.Stage2Only
        };

        StepExecutionResult Result = await Orchestrator.ExecuteReverseRollbackAsync(
            Settings,
            ReverseRollbackMode.Full,
            null,
            CancellationToken.None);

        Assert.True(Result.Succeeded);
        Assert.Single(Bridge.RecordedJobs);
        Assert.Equal("99_ReverseRollback.jsx", Bridge.RecordedJobs[0].ScriptFile);
        Assert.Equal("Full", Bridge.RecordedJobs[0].Settings.ReverseMode);
        Assert.Equal(ReverseRollbackMode.Stage2Only, Settings.ReverseMode);
        Assert.Equal(1, Bridge.CleanupCallCount);
    }

    [Fact]
    public async Task ExecutePipelineAsyncCallsBeginAndEndSessionOnceOnSuccessAndFailure()
    {
        FakeAfterEffectsBridge SuccessBridge = new FakeAfterEffectsBridge();
        FakeTransactionManager SuccessTxManager = new FakeTransactionManager();
        PipelineOrchestrator SuccessOrchestrator = PipelineOrchestratorTests.CreateOrchestrator(SuccessBridge, SuccessTxManager);

        PipelineExecutionResult SuccessResult = await SuccessOrchestrator.ExecutePipelineAsync(
            new PipelineSettings(),
            new string[] { PipelineStepIds.Precomp },
            null,
            CancellationToken.None);

        Assert.True(SuccessResult.Succeeded);
        Assert.Equal(1, SuccessBridge.BeginSessionCallCount);
        Assert.Equal(1, SuccessBridge.EndSessionCallCount);

        FakeAfterEffectsBridge FailBridge = new FakeAfterEffectsBridge();
        FailBridge.JobHandler = (Payload, Settings, HeartbeatProgress, CancellationToken) =>
        {
            if (Payload.ScriptFile == "02_PrecomposeClips.jsx")
            {
                return Task.FromResult(new StepResult
                {
                    JobId = Payload.JobId,
                    Status = "error",
                    Error = "Simulierter Step-Fehler"
                });
            }

            return Task.FromResult(FakeAfterEffectsBridge.CreateDefaultOkResult(Payload));
        };

        FakeTransactionManager FailTxManager = new FakeTransactionManager();
        PipelineOrchestrator FailOrchestrator = PipelineOrchestratorTests.CreateOrchestrator(FailBridge, FailTxManager);

        PipelineExecutionResult FailResult = await FailOrchestrator.ExecutePipelineAsync(
            new PipelineSettings(),
            new string[] { PipelineStepIds.Precomp },
            null,
            CancellationToken.None);

        Assert.False(FailResult.Succeeded);
        Assert.True(FailResult.WasRolledBack);
        Assert.Equal(1, FailBridge.BeginSessionCallCount);
        Assert.Equal(1, FailBridge.EndSessionCallCount);
    }

    #endregion
}
