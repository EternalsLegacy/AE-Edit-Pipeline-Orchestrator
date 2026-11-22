using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Models.Ipc;
using AEEditOrchestrator.Core.Steps;
using AEEditOrchestrator.Tests.Core.TestDoubles;

namespace AEEditOrchestrator.Tests.Core;

public sealed class PipelineStepsTests
{
    #region Public Methods

    [Fact]
    public void AllStepsExposeExpectedMetadataAndBatchSupport()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();

        PreflightCheckStep Step00 = new PreflightCheckStep(Bridge);
        BeatDetectionStep Step01 = new BeatDetectionStep(Bridge);
        PrecomposeClipsStep Step02 = new PrecomposeClipsStep(Bridge);
        ApplyFadeInOutStep Step03 = new ApplyFadeInOutStep(Bridge);
        ApplyMotionTileAndShakesStep Step04 = new ApplyMotionTileAndShakesStep(Bridge);
        CreateStartBlursStep Step05 = new CreateStartBlursStep(Bridge);
        CreateEndBlursStep Step06 = new CreateEndBlursStep(Bridge);
        GroupClipsForZoomStep Step07 = new GroupClipsForZoomStep(Bridge);
        ApplyBlurZoomStep Step08 = new ApplyBlurZoomStep(Bridge);
        ReverseRollbackStep Step99 = new ReverseRollbackStep(Bridge);

        Assert.Equal(PipelineStepIds.Preflight, Step00.StepId);
        Assert.Equal(0, Step00.ExecutionOrder);
        Assert.Equal("00_PreflightCheck.jsx", Step00.JsxScriptFileName);
        Assert.False(Step00.SupportsBatching);

        Assert.Equal(PipelineStepIds.Beats, Step01.StepId);
        Assert.Equal(10, Step01.ExecutionOrder);
        Assert.Equal("01_BeatDetection.jsx", Step01.JsxScriptFileName);
        Assert.False(Step01.SupportsBatching);

        Assert.Equal(PipelineStepIds.Precomp, Step02.StepId);
        Assert.Equal(20, Step02.ExecutionOrder);
        Assert.Equal("02_PrecomposeClips.jsx", Step02.JsxScriptFileName);
        Assert.True(Step02.SupportsBatching);

        Assert.Equal(PipelineStepIds.Fade, Step03.StepId);
        Assert.Equal(30, Step03.ExecutionOrder);
        Assert.Equal("03_ApplyFadeInOut.jsx", Step03.JsxScriptFileName);
        Assert.True(Step03.SupportsBatching);
        Assert.Equal(new string[] { PipelineStepIds.Precomp }, Step03.RequiredStepIds);

        Assert.Equal(PipelineStepIds.Shakes, Step04.StepId);
        Assert.Equal(40, Step04.ExecutionOrder);
        Assert.Equal("04_ApplyMotionTileAndShakes.jsx", Step04.JsxScriptFileName);
        Assert.True(Step04.SupportsBatching);
        Assert.Equal(new string[] { PipelineStepIds.Precomp }, Step04.RequiredStepIds);

        Assert.Equal(PipelineStepIds.StartBlur, Step05.StepId);
        Assert.Equal(50, Step05.ExecutionOrder);
        Assert.Equal("05_CreateStartBlurs.jsx", Step05.JsxScriptFileName);
        Assert.True(Step05.SupportsBatching);
        Assert.Equal(new string[] { PipelineStepIds.Precomp }, Step05.RequiredStepIds);

        Assert.Equal(PipelineStepIds.EndBlurs, Step06.StepId);
        Assert.Equal(60, Step06.ExecutionOrder);
        Assert.Equal("06_CreateEndBlurs.jsx", Step06.JsxScriptFileName);
        Assert.True(Step06.SupportsBatching);
        Assert.Equal(new string[] { PipelineStepIds.Precomp }, Step06.RequiredStepIds);

        Assert.Equal(PipelineStepIds.ZoomGroup, Step07.StepId);
        Assert.Equal(70, Step07.ExecutionOrder);
        Assert.Equal("07_GroupClipsForZoom.jsx", Step07.JsxScriptFileName);
        Assert.True(Step07.SupportsBatching);
        Assert.Equal(new string[] { PipelineStepIds.Precomp }, Step07.RequiredStepIds);

        Assert.Equal(PipelineStepIds.BlurZoom, Step08.StepId);
        Assert.Equal(80, Step08.ExecutionOrder);
        Assert.Equal("08_ApplyBlurZoom.jsx", Step08.JsxScriptFileName);
        Assert.True(Step08.SupportsBatching);
        Assert.Equal(new string[] { PipelineStepIds.ZoomGroup }, Step08.RequiredStepIds);

        Assert.Equal(PipelineStepIds.Reverse, Step99.StepId);
        Assert.Equal(99, Step99.ExecutionOrder);
        Assert.Equal("99_ReverseRollback.jsx", Step99.JsxScriptFileName);
        Assert.False(Step99.SupportsBatching);
    }

    [Fact]
    public async Task ExecuteAsyncBatchingStepLoopsUntilHasMoreBatchesIsFalseAndReportsProgress()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        Bridge.JobHandler = (Payload, Settings, HeartbeatProgress, CancellationToken) =>
        {
            if (HeartbeatProgress != null)
            {
                HeartbeatProgress.Report(new HeartbeatMessage
                {
                    JobId = Payload.JobId,
                    CurrentIndex = Payload.BatchStartIndex + 1,
                    TotalItems = 25,
                    Message = $"Clip {Payload.BatchStartIndex + 1}"
                });
            }

            if (Payload.BatchStartIndex == 0)
            {
                return Task.FromResult(new StepResult
                {
                    JobId = Payload.JobId,
                    Status = "ok",
                    TotalClips = 25,
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
                    TotalClips = 25,
                    ProcessedInBatch = 8,
                    SkippedInBatch = 2,
                    HasMoreBatches = true,
                    NextBatchIndex = 20
                });
            }

            return Task.FromResult(new StepResult
            {
                JobId = Payload.JobId,
                Status = "ok",
                TotalClips = 25,
                ProcessedInBatch = 5,
                SkippedInBatch = 0,
                HasMoreBatches = false,
                NextBatchIndex = 25
            });
        };

        PrecomposeClipsStep Step = new PrecomposeClipsStep(Bridge);
        RecordingProgress<PipelineProgressUpdate> Progress = new RecordingProgress<PipelineProgressUpdate>();
        PipelineContext Context = new PipelineContext
        {
            Settings = new PipelineSettings { BatchSize = 10 },
            Progress = Progress,
            StepNumber = 1,
            TotalSteps = 1
        };

        StepExecutionResult Result = await Step.ExecuteAsync(Context, CancellationToken.None);

        Assert.True(Result.Succeeded);
        Assert.Equal(25, Result.TotalClips);
        Assert.Equal(23, Result.ProcessedClips);
        Assert.Equal(2, Result.SkippedClips);
        Assert.Equal(3, Bridge.RecordedJobs.Count);
        Assert.Equal(0, Bridge.RecordedJobs[0].BatchStartIndex);
        Assert.Equal(10, Bridge.RecordedJobs[1].BatchStartIndex);
        Assert.Equal(20, Bridge.RecordedJobs[2].BatchStartIndex);
        Assert.True(Progress.Items.Count >= 6);
        Assert.Equal(100.0, Progress.Items[Progress.Items.Count - 1].Percentage);
    }

    [Fact]
    public async Task ExecuteAsyncNonBatchingStepRunsSingleJobEvenIfHasMoreBatchesTrue()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        Bridge.JobHandler = (Payload, Settings, HeartbeatProgress, CancellationToken) =>
        {
            return Task.FromResult(new StepResult
            {
                JobId = Payload.JobId,
                Status = "ok",
                TotalClips = 1,
                ProcessedInBatch = 1,
                HasMoreBatches = true,
                NextBatchIndex = 1
            });
        };

        BeatDetectionStep Step = new BeatDetectionStep(Bridge);
        PipelineContext Context = new PipelineContext
        {
            Settings = new PipelineSettings()
        };

        StepExecutionResult Result = await Step.ExecuteAsync(Context, CancellationToken.None);

        Assert.True(Result.Succeeded);
        Assert.Single(Bridge.RecordedJobs);
    }

    [Fact]
    public async Task ExecuteAsyncCancelledBetweenBatchesThrowsOperationCanceledException()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        CancellationTokenSource CancellationSource = new CancellationTokenSource();

        Bridge.JobHandler = (Payload, Settings, HeartbeatProgress, CancellationToken) =>
        {
            CancellationSource.Cancel();
            return Task.FromResult(new StepResult
            {
                JobId = Payload.JobId,
                Status = "ok",
                TotalClips = 20,
                ProcessedInBatch = 10,
                HasMoreBatches = true,
                NextBatchIndex = 10
            });
        };

        PrecomposeClipsStep Step = new PrecomposeClipsStep(Bridge);
        PipelineContext Context = new PipelineContext
        {
            Settings = new PipelineSettings()
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await Step.ExecuteAsync(Context, CancellationSource.Token);
        });

        Assert.Single(Bridge.RecordedJobs);
    }

    [Fact]
    public void ValidateSettingsSteps03To08ValidatePresetsAndPrerequisites()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        ApplyFadeInOutStep Step03 = new ApplyFadeInOutStep(Bridge);
        ApplyMotionTileAndShakesStep Step04 = new ApplyMotionTileAndShakesStep(Bridge);
        CreateStartBlursStep Step05 = new CreateStartBlursStep(Bridge);
        CreateEndBlursStep Step06 = new CreateEndBlursStep(Bridge);
        GroupClipsForZoomStep Step07 = new GroupClipsForZoomStep(Bridge);
        ApplyBlurZoomStep Step08 = new ApplyBlurZoomStep(Bridge);

        PipelineSettings SettingsWithoutPrecomp = new PipelineSettings
        {
            SelectedStepIds = new List<string>()
        };
        ProjectPreflightState EmptyPreflight = new ProjectPreflightState();

        Assert.False(Step03.ValidateSettings(SettingsWithoutPrecomp, EmptyPreflight).IsValid);
        Assert.False(Step04.ValidateSettings(SettingsWithoutPrecomp, EmptyPreflight).IsValid);
        Assert.False(Step05.ValidateSettings(SettingsWithoutPrecomp, EmptyPreflight).IsValid);
        Assert.False(Step06.ValidateSettings(SettingsWithoutPrecomp, EmptyPreflight).IsValid);
        Assert.False(Step07.ValidateSettings(SettingsWithoutPrecomp, EmptyPreflight).IsValid);
        Assert.False(Step08.ValidateSettings(SettingsWithoutPrecomp, EmptyPreflight).IsValid);

        ProjectPreflightState TagPreflight = new ProjectPreflightState
        {
            HasEditClipTags = true,
            HasZoomGroupTags = true
        };

        Assert.True(Step03.ValidateSettings(SettingsWithoutPrecomp, TagPreflight).IsValid);
        Assert.True(Step04.ValidateSettings(SettingsWithoutPrecomp, TagPreflight).IsValid);
        Assert.True(Step05.ValidateSettings(SettingsWithoutPrecomp, TagPreflight).IsValid);
        Assert.True(Step06.ValidateSettings(SettingsWithoutPrecomp, TagPreflight).IsValid);
        Assert.True(Step07.ValidateSettings(SettingsWithoutPrecomp, TagPreflight).IsValid);
        Assert.True(Step08.ValidateSettings(SettingsWithoutPrecomp, TagPreflight).IsValid);

        SettingsWithoutPrecomp.Presets.BlurZoomOut = string.Empty;
        Assert.False(Step08.ValidateSettings(SettingsWithoutPrecomp, TagPreflight).IsValid);
    }

    [Fact]
    public async Task ExecuteAsyncSteps04To06ShareSameStartDirectionInJobPayload()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        PipelineSettings Settings = new PipelineSettings
        {
            StartDirection = ShakeStartDirection.Horizontal
        };

        IPipelineStep[] Steps = new IPipelineStep[]
        {
            new ApplyMotionTileAndShakesStep(Bridge),
            new CreateStartBlursStep(Bridge),
            new CreateEndBlursStep(Bridge)
        };

        foreach (IPipelineStep Step in Steps)
        {
            PipelineContext Context = new PipelineContext
            {
                Settings = Settings
            };

            StepExecutionResult Result = await Step.ExecuteAsync(Context, CancellationToken.None);
            Assert.True(Result.Succeeded);
        }

        Assert.Equal(3, Bridge.RecordedJobs.Count);
        Assert.Equal("Horizontal", Bridge.RecordedJobs[0].Settings.StartDirection);
        Assert.Equal("Horizontal", Bridge.RecordedJobs[1].Settings.StartDirection);
        Assert.Equal("Horizontal", Bridge.RecordedJobs[2].Settings.StartDirection);
    }

    [Fact]
    public void ValidateSettingsBeatDetectionDefaultsAndSensitivityRangeAreEnforced()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        BeatDetectionStep Step01 = new BeatDetectionStep(Bridge);
        ProjectPreflightState Preflight = new ProjectPreflightState();

        PipelineSettings DefaultSettings = new PipelineSettings();
        Assert.Equal(BeatDetectionMode.UniversalFourZone, DefaultSettings.BeatDetection.Mode);
        Assert.Equal(65.0, DefaultSettings.BeatDetection.Sensitivity);
        Assert.True(Step01.ValidateSettings(DefaultSettings, Preflight).IsValid);

        DefaultSettings.BeatDetection.Sensitivity = 9.9;
        Assert.False(Step01.ValidateSettings(DefaultSettings, Preflight).IsValid);

        DefaultSettings.BeatDetection.Sensitivity = 100.1;
        Assert.False(Step01.ValidateSettings(DefaultSettings, Preflight).IsValid);

        DefaultSettings.BeatDetection.Sensitivity = 10.0;
        Assert.True(Step01.ValidateSettings(DefaultSettings, Preflight).IsValid);

        DefaultSettings.BeatDetection.Sensitivity = 100.0;
        Assert.True(Step01.ValidateSettings(DefaultSettings, Preflight).IsValid);
    }

    [Fact]
    public async Task ExecuteAsyncSilentStartupAckHeartbeatDoesNotEmitUiProgressUpdate()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        Bridge.JobHandler = (Payload, Settings, HeartbeatProgress, CancellationToken) =>
        {
            if (HeartbeatProgress != null)
            {
                HeartbeatProgress.Report(new HeartbeatMessage
                {
                    JobId = Payload.JobId,
                    CurrentIndex = 0,
                    TotalItems = 0,
                    Message = string.Empty
                });

                HeartbeatProgress.Report(new HeartbeatMessage
                {
                    JobId = Payload.JobId,
                    CurrentIndex = 1,
                    TotalItems = 3,
                    Message = "Pre-compose Clip 1 / 3"
                });
            }

            return Task.FromResult(new StepResult
            {
                JobId = Payload.JobId,
                Status = "ok",
                TotalClips = 3,
                ProcessedInBatch = 3,
                HasMoreBatches = false
            });
        };

        PrecomposeClipsStep Step = new PrecomposeClipsStep(Bridge);
        RecordingProgress<PipelineProgressUpdate> Progress = new RecordingProgress<PipelineProgressUpdate>();
        PipelineContext Context = new PipelineContext
        {
            Settings = new PipelineSettings(),
            Progress = Progress,
            StepNumber = 1,
            TotalSteps = 1
        };

        StepExecutionResult Result = await Step.ExecuteAsync(Context, CancellationToken.None);

        Assert.True(Result.Succeeded);
        Assert.Equal(3, Progress.Items.Count);
        Assert.DoesNotContain(Progress.Items, Update => string.IsNullOrEmpty(Update.Message));
        Assert.Contains(Progress.Items, Update => Update.Message == "Pre-compose Clip 1 / 3");
    }

    #endregion
}
