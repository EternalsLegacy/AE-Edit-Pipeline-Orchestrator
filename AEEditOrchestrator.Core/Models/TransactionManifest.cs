using System.Text.Json.Serialization;

namespace AEEditOrchestrator.Core.Models;

public sealed class TransactionManifest
{
    #region Fields

    private string TransactionIdField = string.Empty;
    private DateTimeOffset CreatedAtUtcField = DateTimeOffset.UtcNow;
    private DateTimeOffset? CompletedAtUtcField = null;
    private string OriginalProjectPathField = string.Empty;
    private string BackupDirectoryPathField = string.Empty;
    private string InitialSnapshotFileNameField = "00_Initial_Snapshot.aep";
    private string InitialSnapshotSha256Field = string.Empty;
    private List<MilestoneSnapshot> MilestonesField = new List<MilestoneSnapshot>();
    private List<string> SelectedStepIdsField = new List<string>();
    private TransactionStatus StatusField = TransactionStatus.Active;
    private string NotesField = string.Empty;

    #endregion

    #region Properties

    public string TransactionId
    {
        get
        {
            return this.TransactionIdField;
        }
        set
        {
            this.TransactionIdField = value ?? string.Empty;
        }
    }

    public DateTimeOffset CreatedAtUtc
    {
        get
        {
            return this.CreatedAtUtcField;
        }
        set
        {
            this.CreatedAtUtcField = value;
        }
    }

    public DateTimeOffset? CompletedAtUtc
    {
        get
        {
            return this.CompletedAtUtcField;
        }
        set
        {
            this.CompletedAtUtcField = value;
        }
    }

    public string OriginalProjectPath
    {
        get
        {
            return this.OriginalProjectPathField;
        }
        set
        {
            this.OriginalProjectPathField = value ?? string.Empty;
        }
    }

    public string BackupDirectoryPath
    {
        get
        {
            return this.BackupDirectoryPathField;
        }
        set
        {
            this.BackupDirectoryPathField = value ?? string.Empty;
        }
    }

    public string InitialSnapshotFileName
    {
        get
        {
            return this.InitialSnapshotFileNameField;
        }
        set
        {
            this.InitialSnapshotFileNameField = value ?? "00_Initial_Snapshot.aep";
        }
    }

    public string InitialSnapshotSha256
    {
        get
        {
            return this.InitialSnapshotSha256Field;
        }
        set
        {
            this.InitialSnapshotSha256Field = value ?? string.Empty;
        }
    }

    public List<MilestoneSnapshot> Milestones
    {
        get
        {
            return this.MilestonesField;
        }
        set
        {
            this.MilestonesField = value ?? new List<MilestoneSnapshot>();
        }
    }

    public List<string> SelectedStepIds
    {
        get
        {
            return this.SelectedStepIdsField;
        }
        set
        {
            this.SelectedStepIdsField = value ?? new List<string>();
        }
    }

    public TransactionStatus Status
    {
        get
        {
            return this.StatusField;
        }
        set
        {
            this.StatusField = value;
        }
    }

    public string Notes
    {
        get
        {
            return this.NotesField;
        }
        set
        {
            this.NotesField = value ?? string.Empty;
        }
    }

    [JsonIgnore]
    public string InitialSnapshotFullPath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(this.BackupDirectoryPathField))
            {
                return string.Empty;
            }
            return Path.Combine(this.BackupDirectoryPathField, this.InitialSnapshotFileNameField);
        }
    }

    #endregion
}

public sealed class MilestoneSnapshot
{
    #region Fields

    private string NameField = string.Empty;
    private string FileNameField = string.Empty;
    private string Sha256Field = string.Empty;
    private DateTimeOffset CreatedAtUtcField = DateTimeOffset.UtcNow;

    #endregion

    #region Properties

    public string Name
    {
        get
        {
            return this.NameField;
        }
        set
        {
            this.NameField = value ?? string.Empty;
        }
    }

    public string FileName
    {
        get
        {
            return this.FileNameField;
        }
        set
        {
            this.FileNameField = value ?? string.Empty;
        }
    }

    public string Sha256
    {
        get
        {
            return this.Sha256Field;
        }
        set
        {
            this.Sha256Field = value ?? string.Empty;
        }
    }

    public DateTimeOffset CreatedAtUtc
    {
        get
        {
            return this.CreatedAtUtcField;
        }
        set
        {
            this.CreatedAtUtcField = value;
        }
    }

    #endregion
}
