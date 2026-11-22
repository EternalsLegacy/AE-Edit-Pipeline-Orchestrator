using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Models.Ipc;

namespace AEEditOrchestrator.Core.Interfaces;

public interface IAfterEffectsBridge
{
    #region Public Methods

    bool IsAfterEffectsRunning();

    Task<StepResult> ExecuteJobAsync(
        JobPayload Payload,
        PipelineSettings Settings,
        IProgress<HeartbeatMessage>? HeartbeatProgress = null,
        CancellationToken CancellationToken = default);

    Task<bool> CloseProjectWithoutSavingAsync(
        PipelineSettings Settings,
        CancellationToken CancellationToken = default);

    Task<bool> OpenProjectAsync(
        string ProjectPath,
        PipelineSettings Settings,
        CancellationToken CancellationToken = default);

    Task<bool> SaveProjectAsync(
        PipelineSettings Settings,
        CancellationToken CancellationToken = default);

    Task<bool> TryRestartAfterEffectsAsync(
        PipelineSettings Settings,
        CancellationToken CancellationToken = default);

    void CleanupIpcFiles();

    #endregion
}
