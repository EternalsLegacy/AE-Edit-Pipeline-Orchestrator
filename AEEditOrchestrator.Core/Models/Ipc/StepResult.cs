using System.Text.Json.Serialization;

namespace AEEditOrchestrator.Core.Models.Ipc;

public sealed class StepResult
{
    #region Fields

    private string JobIdField = string.Empty;
    private string StatusField = string.Empty;
    private string ProjectPathField = string.Empty;
    private string CompositionNameField = string.Empty;
    private int TotalClipsField = 0;
    private int ProcessedInBatchField = 0;
    private int SkippedInBatchField = 0;
    private bool HasMoreBatchesField = false;
    private int NextBatchIndexField = 0;
    private DetectedProjectState? DetectedStateField = null;
    private string? ErrorField = null;
    private int? ErrorLineField = null;
    private string? MessageField = null;

    #endregion

    #region Properties

    [JsonPropertyName("jobId")]
    public string JobId
    {
        get
        {
            return this.JobIdField;
        }
        set
        {
            this.JobIdField = value ?? string.Empty;
        }
    }

    [JsonPropertyName("status")]
    public string Status
    {
        get
        {
            return this.StatusField;
        }
        set
        {
            this.StatusField = value ?? string.Empty;
        }
    }

    [JsonPropertyName("projectPath")]
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

    [JsonPropertyName("compositionName")]
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

    [JsonPropertyName("totalClips")]
    public int TotalClips
    {
        get
        {
            return this.TotalClipsField;
        }
        set
        {
            this.TotalClipsField = value;
        }
    }

    [JsonPropertyName("processedInBatch")]
    public int ProcessedInBatch
    {
        get
        {
            return this.ProcessedInBatchField;
        }
        set
        {
            this.ProcessedInBatchField = value;
        }
    }

    [JsonPropertyName("skippedInBatch")]
    public int SkippedInBatch
    {
        get
        {
            return this.SkippedInBatchField;
        }
        set
        {
            this.SkippedInBatchField = value;
        }
    }

    [JsonPropertyName("hasMoreBatches")]
    public bool HasMoreBatches
    {
        get
        {
            return this.HasMoreBatchesField;
        }
        set
        {
            this.HasMoreBatchesField = value;
        }
    }

    [JsonPropertyName("nextBatchIndex")]
    public int NextBatchIndex
    {
        get
        {
            return this.NextBatchIndexField;
        }
        set
        {
            this.NextBatchIndexField = value;
        }
    }

    [JsonPropertyName("detectedState")]
    public DetectedProjectState? DetectedState
    {
        get
        {
            return this.DetectedStateField;
        }
        set
        {
            this.DetectedStateField = value;
        }
    }

    [JsonPropertyName("error")]
    public string? Error
    {
        get
        {
            return this.ErrorField;
        }
        set
        {
            this.ErrorField = value;
        }
    }

    [JsonPropertyName("errorLine")]
    public int? ErrorLine
    {
        get
        {
            return this.ErrorLineField;
        }
        set
        {
            this.ErrorLineField = value;
        }
    }

    [JsonPropertyName("message")]
    public string? Message
    {
        get
        {
            return this.MessageField;
        }
        set
        {
            this.MessageField = value;
        }
    }

    [JsonIgnore]
    public bool IsOk
    {
        get
        {
            return string.Equals(this.StatusField, "ok", StringComparison.OrdinalIgnoreCase);
        }
    }

    #endregion
}

public sealed class DetectedProjectState
{
    #region Fields

    private bool HasEditClipTagsField = false;
    private bool HasStage1TagsField = false;
    private bool HasZoomGroupTagsField = false;
    private List<string> MissingPresetsField = new List<string>();
    private bool IsProjectSavedField = false;
    private bool HasActiveCompField = false;

    #endregion

    #region Properties

    [JsonPropertyName("hasEditClipTags")]
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

    [JsonPropertyName("hasStage1Tags")]
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

    [JsonPropertyName("hasZoomGroupTags")]
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

    [JsonPropertyName("missingPresets")]
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

    [JsonPropertyName("isProjectSaved")]
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

    [JsonPropertyName("hasActiveComp")]
    public bool HasActiveComp
    {
        get
        {
            return this.HasActiveCompField;
        }
        set
        {
            this.HasActiveCompField = value;
        }
    }

    #endregion
}
