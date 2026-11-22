using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Tests.Core.TestDoubles;

public sealed class FakeTransactionManager : ITransactionManager
{
    #region Fields

    private int BeginCallCountField = 0;
    private int MilestoneCallCountField = 0;
    private int CommitCallCountField = 0;
    private int RollbackCallCountField = 0;
    private int RotateCallCountField = 0;
    private int LastRotateMaxBackupsField = 0;
    private bool RollbackResultField = true;
    private Exception? ThrowOnBeginField = null;
    private Exception? ThrowOnCommitField = null;
    private readonly List<TransactionManifest> CreatedManifestsField = new List<TransactionManifest>();
    private readonly List<MilestoneSnapshot> CreatedMilestonesField = new List<MilestoneSnapshot>();
    private readonly List<string> RollbackReasonsField = new List<string>();
    private readonly List<string> CallLogField = new List<string>();

    #endregion

    #region Properties

    public int BeginCallCount
    {
        get
        {
            return this.BeginCallCountField;
        }
    }

    public int MilestoneCallCount
    {
        get
        {
            return this.MilestoneCallCountField;
        }
    }

    public int CommitCallCount
    {
        get
        {
            return this.CommitCallCountField;
        }
    }

    public int RollbackCallCount
    {
        get
        {
            return this.RollbackCallCountField;
        }
    }

    public int RotateCallCount
    {
        get
        {
            return this.RotateCallCountField;
        }
    }

    public int LastRotateMaxBackups
    {
        get
        {
            return this.LastRotateMaxBackupsField;
        }
    }

    public bool RollbackResult
    {
        get
        {
            return this.RollbackResultField;
        }
        set
        {
            this.RollbackResultField = value;
        }
    }

    public Exception? ThrowOnBegin
    {
        get
        {
            return this.ThrowOnBeginField;
        }
        set
        {
            this.ThrowOnBeginField = value;
        }
    }

    public Exception? ThrowOnCommit
    {
        get
        {
            return this.ThrowOnCommitField;
        }
        set
        {
            this.ThrowOnCommitField = value;
        }
    }

    public IReadOnlyList<TransactionManifest> CreatedManifests
    {
        get
        {
            return this.CreatedManifestsField.AsReadOnly();
        }
    }

    public IReadOnlyList<MilestoneSnapshot> CreatedMilestones
    {
        get
        {
            return this.CreatedMilestonesField.AsReadOnly();
        }
    }

    public IReadOnlyList<string> RollbackReasons
    {
        get
        {
            return this.RollbackReasonsField.AsReadOnly();
        }
    }

    public IReadOnlyList<string> CallLog
    {
        get
        {
            return this.CallLogField.AsReadOnly();
        }
    }

    #endregion

    #region Public Methods

    public Task<TransactionManifest> BeginTransactionAsync(
        string ProjectFilePath,
        IReadOnlyList<string> SelectedStepIds,
        CancellationToken CancellationToken = default)
    {
        CancellationToken.ThrowIfCancellationRequested();

        if (this.ThrowOnBeginField != null)
        {
            throw this.ThrowOnBeginField;
        }

        this.BeginCallCountField++;
        this.CallLogField.Add($"BeginTransaction:{ProjectFilePath}");

        TransactionManifest Manifest = new TransactionManifest
        {
            TransactionId = $"tx-{this.BeginCallCountField:D4}",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            OriginalProjectPath = ProjectFilePath,
            BackupDirectoryPath = "C:\\Temp\\_AEP_Backups\\tx",
            InitialSnapshotFileName = "00_Initial_Snapshot.aep",
            InitialSnapshotSha256 = "ABC123",
            SelectedStepIds = new List<string>(SelectedStepIds),
            Status = TransactionStatus.Active
        };

        this.CreatedManifestsField.Add(Manifest);
        return Task.FromResult(Manifest);
    }

    public Task<MilestoneSnapshot> CreateMilestoneSnapshotAsync(
        TransactionManifest Manifest,
        string MilestoneName,
        string FileName,
        CancellationToken CancellationToken = default)
    {
        CancellationToken.ThrowIfCancellationRequested();

        this.MilestoneCallCountField++;
        this.CallLogField.Add($"CreateMilestone:{FileName}");

        MilestoneSnapshot Snapshot = new MilestoneSnapshot
        {
            Name = MilestoneName,
            FileName = FileName,
            Sha256 = "MILESTONE123",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        Manifest.Milestones.Add(Snapshot);
        this.CreatedMilestonesField.Add(Snapshot);
        return Task.FromResult(Snapshot);
    }

    public Task CommitTransactionAsync(
        TransactionManifest Manifest,
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        CancellationToken.ThrowIfCancellationRequested();

        if (this.ThrowOnCommitField != null)
        {
            throw this.ThrowOnCommitField;
        }

        this.CommitCallCountField++;
        this.CallLogField.Add($"CommitTransaction:{Manifest.TransactionId}");

        Manifest.Status = TransactionStatus.Committed;
        Manifest.CompletedAtUtc = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<bool> RollbackTransactionAsync(
        TransactionManifest Manifest,
        PipelineSettings Settings,
        string Reason,
        CancellationToken CancellationToken = default)
    {
        CancellationToken.ThrowIfCancellationRequested();

        this.RollbackCallCountField++;
        this.RollbackReasonsField.Add(Reason);
        this.CallLogField.Add($"RollbackTransaction:{Manifest.TransactionId}:{Reason}");

        if (this.RollbackResultField)
        {
            Manifest.Status = TransactionStatus.RolledBack;
            Manifest.Notes = Reason;
            Manifest.CompletedAtUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            Manifest.Status = TransactionStatus.Failed;
            Manifest.Notes = Reason;
        }

        return Task.FromResult(this.RollbackResultField);
    }

    public Task<bool> RestoreSnapshotAsync(
        TransactionManifest Manifest,
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        this.CallLogField.Add($"RestoreSnapshot:{Manifest.TransactionId}");
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<TransactionManifest>> ListSnapshotsAsync(
        string ProjectFilePath,
        CancellationToken CancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<TransactionManifest>>(this.CreatedManifestsField.AsReadOnly());
    }

    public Task<bool> VerifySnapshotIntegrityAsync(
        TransactionManifest Manifest,
        CancellationToken CancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task RotateBackupsAsync(
        string ProjectFilePath,
        int MaxBackupsToKeep,
        CancellationToken CancellationToken = default)
    {
        CancellationToken.ThrowIfCancellationRequested();

        this.RotateCallCountField++;
        this.LastRotateMaxBackupsField = MaxBackupsToKeep;
        this.CallLogField.Add($"RotateBackups:{MaxBackupsToKeep}");
        return Task.CompletedTask;
    }

    #endregion
}
