using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Models.Ipc;

namespace AEEditOrchestrator.Tests.Core.TestDoubles;

public sealed class FakeAfterEffectsBridge : IAfterEffectsBridge
{
    #region Fields

    private bool IsRunningField = true;
    private bool SaveProjectResultField = true;
    private bool CloseProjectResultField = true;
    private bool OpenProjectResultField = true;
    private bool RestartResultField = true;
    private int SaveCallCountField = 0;
    private int CleanupCallCountField = 0;
    private int BeginSessionCallCountField = 0;
    private int EndSessionCallCountField = 0;
    private readonly List<JobPayload> RecordedJobsField = new List<JobPayload>();
    private readonly List<string> CallLogField = new List<string>();
    private Func<JobPayload, PipelineSettings, IProgress<HeartbeatMessage>?, CancellationToken, Task<StepResult>>? JobHandlerField = null;

    #endregion

    #region Properties

    public bool IsRunning
    {
        get
        {
            return this.IsRunningField;
        }
        set
        {
            this.IsRunningField = value;
        }
    }

    public bool SaveProjectResult
    {
        get
        {
            return this.SaveProjectResultField;
        }
        set
        {
            this.SaveProjectResultField = value;
        }
    }

    public bool CloseProjectResult
    {
        get
        {
            return this.CloseProjectResultField;
        }
        set
        {
            this.CloseProjectResultField = value;
        }
    }

    public bool OpenProjectResult
    {
        get
        {
            return this.OpenProjectResultField;
        }
        set
        {
            this.OpenProjectResultField = value;
        }
    }

    public bool RestartResult
    {
        get
        {
            return this.RestartResultField;
        }
        set
        {
            this.RestartResultField = value;
        }
    }

    public int SaveCallCount
    {
        get
        {
            return this.SaveCallCountField;
        }
    }

    public int CleanupCallCount
    {
        get
        {
            return this.CleanupCallCountField;
        }
    }

    public int BeginSessionCallCount
    {
        get
        {
            return this.BeginSessionCallCountField;
        }
    }

    public int EndSessionCallCount
    {
        get
        {
            return this.EndSessionCallCountField;
        }
    }

    public IReadOnlyList<JobPayload> RecordedJobs
    {
        get
        {
            return this.RecordedJobsField.AsReadOnly();
        }
    }

    public IReadOnlyList<string> CallLog
    {
        get
        {
            return this.CallLogField.AsReadOnly();
        }
    }

    public Func<JobPayload, PipelineSettings, IProgress<HeartbeatMessage>?, CancellationToken, Task<StepResult>>? JobHandler
    {
        get
        {
            return this.JobHandlerField;
        }
        set
        {
            this.JobHandlerField = value;
        }
    }

    #endregion

    #region Public Methods

    public void BeginSession()
    {
        this.BeginSessionCallCountField++;
        this.CallLogField.Add("BeginSession");
    }

    public Task EndSessionAsync(CancellationToken CancellationToken = default)
    {
        this.EndSessionCallCountField++;
        this.CallLogField.Add("EndSession");
        return Task.CompletedTask;
    }

    public bool IsAfterEffectsRunning()
    {
        return this.IsRunningField;
    }

    public async Task<StepResult> ExecuteJobAsync(
        JobPayload Payload,
        PipelineSettings Settings,
        IProgress<HeartbeatMessage>? HeartbeatProgress = null,
        CancellationToken CancellationToken = default)
    {
        CancellationToken.ThrowIfCancellationRequested();

        this.RecordedJobsField.Add(Payload);
        this.CallLogField.Add($"ExecuteJob:{Payload.ScriptFile}:batch={Payload.BatchStartIndex}");

        if (this.JobHandlerField != null)
        {
            return await this.JobHandlerField(Payload, Settings, HeartbeatProgress, CancellationToken);
        }

        return FakeAfterEffectsBridge.CreateDefaultOkResult(Payload);
    }

    public Task<bool> CloseProjectWithoutSavingAsync(
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        this.CallLogField.Add("CloseProjectWithoutSaving");
        return Task.FromResult(this.CloseProjectResultField);
    }

    public Task<bool> OpenProjectAsync(
        string ProjectPath,
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        this.CallLogField.Add($"OpenProject:{ProjectPath}");
        return Task.FromResult(this.OpenProjectResultField);
    }

    public Task<bool> SaveProjectAsync(
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        CancellationToken.ThrowIfCancellationRequested();
        this.SaveCallCountField++;
        this.CallLogField.Add("SaveProject");
        return Task.FromResult(this.SaveProjectResultField);
    }

    public Task<bool> TryRestartAfterEffectsAsync(
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        this.CallLogField.Add("TryRestartAfterEffects");
        return Task.FromResult(this.RestartResultField);
    }

    public void CleanupIpcFiles()
    {
        this.CleanupCallCountField++;
        this.CallLogField.Add("CleanupIpcFiles");
    }

    public static StepResult CreateDefaultOkResult(JobPayload Payload)
    {
        return new StepResult
        {
            JobId = Payload.JobId,
            Status = "ok",
            ProjectPath = "C:\\Temp\\TestProject.aep",
            CompositionName = "MainEdit",
            TotalClips = 5,
            ProcessedInBatch = 5,
            SkippedInBatch = 0,
            HasMoreBatches = false,
            NextBatchIndex = 5,
            DetectedState = new DetectedProjectState
            {
                HasActiveComp = true,
                IsProjectSaved = true,
                HasEditClipTags = false,
                HasStage1Tags = false,
                HasZoomGroupTags = false,
                MissingPresets = new List<string>()
            },
            Message = "OK"
        };
    }

    #endregion
}
