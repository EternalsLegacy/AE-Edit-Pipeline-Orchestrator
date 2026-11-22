using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Core.Interfaces;

public interface ITransactionManager
{
    #region Public Methods

    Task<TransactionManifest> BeginTransactionAsync(
        string ProjectFilePath,
        IReadOnlyList<string> SelectedStepIds,
        CancellationToken CancellationToken = default);

    Task<MilestoneSnapshot> CreateMilestoneSnapshotAsync(
        TransactionManifest Manifest,
        string MilestoneName,
        string FileName,
        CancellationToken CancellationToken = default);

    Task CommitTransactionAsync(
        TransactionManifest Manifest,
        PipelineSettings Settings,
        CancellationToken CancellationToken = default);

    Task<bool> RollbackTransactionAsync(
        TransactionManifest Manifest,
        PipelineSettings Settings,
        string Reason,
        CancellationToken CancellationToken = default);

    Task<bool> RestoreSnapshotAsync(
        TransactionManifest Manifest,
        PipelineSettings Settings,
        CancellationToken CancellationToken = default);

    Task<IReadOnlyList<TransactionManifest>> ListSnapshotsAsync(
        string ProjectFilePath,
        CancellationToken CancellationToken = default);

    Task<bool> VerifySnapshotIntegrityAsync(
        TransactionManifest Manifest,
        CancellationToken CancellationToken = default);

    Task RotateBackupsAsync(
        string ProjectFilePath,
        int MaxBackupsToKeep,
        CancellationToken CancellationToken = default);

    #endregion
}
