using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.App.ViewModels;

public sealed class SnapshotItemViewModel
{
    #region Fields

    private readonly TransactionManifest ManifestField;
    private readonly bool IsIntegrityValidField;

    #endregion

    #region Properties

    public TransactionManifest Manifest
    {
        get
        {
            return this.ManifestField;
        }
    }

    public string TransactionId
    {
        get
        {
            return this.ManifestField.TransactionId;
        }
    }

    public string CreatedAtText
    {
        get
        {
            return this.ManifestField.CreatedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
        }
    }

    public string StatusText
    {
        get
        {
            if (this.ManifestField.Status == TransactionStatus.Committed)
            {
                return "Committed";
            }

            if (this.ManifestField.Status == TransactionStatus.RolledBack)
            {
                return "Rolled Back";
            }

            if (this.ManifestField.Status == TransactionStatus.Failed)
            {
                return "Failed";
            }

            return "Active";
        }
    }

    public string ShortSha256Text
    {
        get
        {
            string Hash = this.ManifestField.InitialSnapshotSha256;
            if (string.IsNullOrWhiteSpace(Hash))
            {
                return "—";
            }

            if (Hash.Length > 12)
            {
                return Hash.Substring(0, 12) + "…";
            }

            return Hash;
        }
    }

    public bool IsIntegrityValid
    {
        get
        {
            return this.IsIntegrityValidField;
        }
    }

    public string IntegrityText
    {
        get
        {
            if (this.IsIntegrityValidField)
            {
                return "SHA-256 OK";
            }

            return "Integritätsfehler";
        }
    }

    public string DetailsText
    {
        get
        {
            int StepCount = this.ManifestField.SelectedStepIds.Count;
            int MilestoneCount = this.ManifestField.Milestones.Count;
            string Summary = $"{StepCount} Steps · {MilestoneCount} Meilensteine";
            if (!string.IsNullOrWhiteSpace(this.ManifestField.Notes))
            {
                Summary = $"{Summary} · {this.ManifestField.Notes}";
            }

            return Summary;
        }
    }

    #endregion

    #region Constructors

    public SnapshotItemViewModel(TransactionManifest Manifest, bool IsIntegrityValid)
    {
        this.ManifestField = Manifest;
        this.IsIntegrityValidField = IsIntegrityValid;
    }

    #endregion
}
