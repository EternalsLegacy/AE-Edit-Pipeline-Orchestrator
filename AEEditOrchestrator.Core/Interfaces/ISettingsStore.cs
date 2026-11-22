using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Core.Interfaces;

public interface ISettingsStore
{
    #region Public Methods

    Task<PipelineSettings> LoadAsync(CancellationToken CancellationToken = default);
    Task SaveAsync(PipelineSettings Settings, CancellationToken CancellationToken = default);
    string? DetectAfterEffectsExecutablePath();
    string? DetectUserPresetsFolderPath();
    IReadOnlyList<PresetValidationItem> ValidatePresetsOnDisk(PipelineSettings Settings);

    #endregion
}
