namespace AEEditOrchestrator.Core.Models;

public sealed class ProjectPreflightState
{
    #region Fields

    private bool IsAfterEffectsRunningField = false;
    private bool HasActiveCompositionField = false;
    private string CompositionNameField = string.Empty;
    private bool IsProjectSavedField = false;
    private string ProjectPathField = string.Empty;
    private int ValidClipCountField = 0;
    private bool HasEditClipTagsField = false;
    private bool HasStage1TagsField = false;
    private bool HasZoomGroupTagsField = false;
    private List<string> MissingPresetsField = new List<string>();
    private DateTimeOffset CheckedAtUtcField = DateTimeOffset.MinValue;
    private string ErrorMessageField = string.Empty;

    #endregion

    #region Properties

    public bool IsAfterEffectsRunning
    {
        get
        {
            return this.IsAfterEffectsRunningField;
        }
        set
        {
            this.IsAfterEffectsRunningField = value;
        }
    }

    public bool HasActiveComposition
    {
        get
        {
            return this.HasActiveCompositionField;
        }
        set
        {
            this.HasActiveCompositionField = value;
        }
    }

    public string CompositionName
    {
        get
        {
            return this.CompositionNameField;
        }
        set
        {
            this.CompositionNameField = value ?? string.Empty;
        }
    }

    public bool IsProjectSaved
    {
        get
        {
            return this.IsProjectSavedField;
        }
        set
        {
            this.IsProjectSavedField = value;
        }
    }

    public string ProjectPath
    {
        get
        {
            return this.ProjectPathField;
        }
        set
        {
            this.ProjectPathField = value ?? string.Empty;
        }
    }

    public int ValidClipCount
    {
        get
        {
            return this.ValidClipCountField;
        }
        set
        {
            this.ValidClipCountField = value;
        }
    }

    public bool HasEditClipTags
    {
        get
        {
            return this.HasEditClipTagsField;
        }
        set
        {
            this.HasEditClipTagsField = value;
        }
    }

    public bool HasStage1Tags
    {
        get
        {
            return this.HasStage1TagsField;
        }
        set
        {
            this.HasStage1TagsField = value;
        }
    }

    public bool HasZoomGroupTags
    {
        get
        {
            return this.HasZoomGroupTagsField;
        }
        set
        {
            this.HasZoomGroupTagsField = value;
        }
    }

    public List<string> MissingPresets
    {
        get
        {
            return this.MissingPresetsField;
        }
        set
        {
            this.MissingPresetsField = value ?? new List<string>();
        }
    }

    public DateTimeOffset CheckedAtUtc
    {
        get
        {
            return this.CheckedAtUtcField;
        }
        set
        {
            this.CheckedAtUtcField = value;
        }
    }

    public string ErrorMessage
    {
        get
        {
            return this.ErrorMessageField;
        }
        set
        {
            this.ErrorMessageField = value ?? string.Empty;
        }
    }

    public bool IsReady
    {
        get
        {
            return this.IsAfterEffectsRunningField
                && this.HasActiveCompositionField
                && this.IsProjectSavedField
                && !string.IsNullOrWhiteSpace(this.ProjectPathField)
                && string.IsNullOrWhiteSpace(this.ErrorMessageField);
        }
    }

    #endregion
}
