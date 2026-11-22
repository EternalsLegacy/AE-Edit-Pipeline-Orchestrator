using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Core.Interfaces;

public interface IPipelineStep
{
    #region Properties

    string StepId { get; }
    string DisplayName { get; }
    string Description { get; }
    int ExecutionOrder { get; }
    IReadOnlyList<string> RequiredStepIds { get; }
    string JsxScriptFileName { get; }
    bool SupportsBatching { get; }

    #endregion

    #region Public Methods

    ValidationResult ValidateSettings(PipelineSettings Settings, ProjectPreflightState PreflightState);
    Task<StepExecutionResult> ExecuteAsync(PipelineContext Context, CancellationToken CancellationToken);

    #endregion
}
