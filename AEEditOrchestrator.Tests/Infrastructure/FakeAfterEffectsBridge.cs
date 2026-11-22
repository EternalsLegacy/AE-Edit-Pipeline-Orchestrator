using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Models.Ipc;

namespace AEEditOrchestrator.Tests.Infrastructure;

internal sealed class FakeAfterEffectsBridge : IAfterEffectsBridge
{
    #region Fields

    private bool IsRunningField = true;
    private bool CloseProjectResultField = true;
    private bool OpenProjectResultField = true;
    private bool SaveProjectResultField = true;
    private bool RestartResultField = true;
    private readonly List<string> RecordedCallsField = new List<string>();
    private readonly List<string> OpenedProjectPathsField = new List<string>();

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

    public IReadOnlyList<string> RecordedCalls
    {
        get
        {
            return this.RecordedCallsField;
        }
    }

    public IReadOnlyList<string> OpenedProjectPaths
    {
        get
        {
            return this.OpenedProjectPathsField;
        }
    }

    #endregion

    #region Public Methods

    public bool IsAfterEffectsRunning()
    {
        this.RecordedCallsField.Add(nameof(this.IsAfterEffectsRunning));
        return this.IsRunningField;
    }

    public Task<StepResult> ExecuteJobAsync(
        JobPayload Payload,
        PipelineSettings Settings,
        IProgress<HeartbeatMessage>? HeartbeatProgress = null,
        CancellationToken CancellationToken = default)
    {
        this.RecordedCallsField.Add(nameof(this.ExecuteJobAsync));
        StepResult Result = new StepResult
        {
            JobId = Payload.JobId,
            Status = "ok"
        };
        return Task.FromResult(Result);
    }

    public Task<bool> CloseProjectWithoutSavingAsync(
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        this.RecordedCallsField.Add(nameof(this.CloseProjectWithoutSavingAsync));
        return Task.FromResult(this.CloseProjectResultField);
    }

    public Task<bool> OpenProjectAsync(
        string ProjectPath,
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        this.RecordedCallsField.Add(nameof(this.OpenProjectAsync));
        this.OpenedProjectPathsField.Add(ProjectPath);
        return Task.FromResult(this.OpenProjectResultField);
    }

    public Task<bool> SaveProjectAsync(
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        this.RecordedCallsField.Add(nameof(this.SaveProjectAsync));
        return Task.FromResult(this.SaveProjectResultField);
    }

    public Task<bool> TryRestartAfterEffectsAsync(
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        this.RecordedCallsField.Add(nameof(this.TryRestartAfterEffectsAsync));
        return Task.FromResult(this.RestartResultField);
    }

    public void CleanupIpcFiles()
    {
        this.RecordedCallsField.Add(nameof(this.CleanupIpcFiles));
    }

    #endregion
}
