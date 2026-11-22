namespace AEEditOrchestrator.Core.Models;

public sealed class ValidationResult
{
    #region Fields

    private bool IsValidField = true;
    private List<string> ErrorsField = new List<string>();
    private List<string> WarningsField = new List<string>();
    private string BadgeTextField = string.Empty;

    #endregion

    #region Properties

    public bool IsValid
    {
        get
        {
            return this.IsValidField;
        }
        set
        {
            this.IsValidField = value;
        }
    }

    public List<string> Errors
    {
        get
        {
            return this.ErrorsField;
        }
        set
        {
            this.ErrorsField = value ?? new List<string>();
        }
    }

    public List<string> Warnings
    {
        get
        {
            return this.WarningsField;
        }
        set
        {
            this.WarningsField = value ?? new List<string>();
        }
    }

    public string BadgeText
    {
        get
        {
            return this.BadgeTextField;
        }
        set
        {
            this.BadgeTextField = value ?? string.Empty;
        }
    }

    #endregion

    #region Public Methods

    public static ValidationResult Success(string BadgeText = "")
    {
        ValidationResult Result = new ValidationResult
        {
            IsValid = true,
            BadgeText = BadgeText
        };
        return Result;
    }

    public static ValidationResult Failure(params string[] Errors)
    {
        ValidationResult Result = new ValidationResult
        {
            IsValid = false,
            Errors = new List<string>(Errors)
        };
        return Result;
    }

    public static ValidationResult Failure(
        IEnumerable<string> Errors,
        IEnumerable<string>? Warnings = null,
        string BadgeText = "")
    {
        List<string> WarningList = new List<string>();
        if (Warnings != null)
        {
            WarningList.AddRange(Warnings);
        }

        ValidationResult Result = new ValidationResult
        {
            IsValid = false,
            Errors = new List<string>(Errors),
            Warnings = WarningList,
            BadgeText = BadgeText
        };
        return Result;
    }

    #endregion
}

public sealed class StepExecutionResult
{
    #region Fields

    private string StepIdField = string.Empty;
    private bool SucceededField = false;
    private int TotalClipsField = 0;
    private int ProcessedClipsField = 0;
    private int SkippedClipsField = 0;
    private string MessageField = string.Empty;
    private string ErrorMessageField = string.Empty;
    private ProjectPreflightState? UpdatedPreflightStateField = null;

    #endregion

    #region Properties

    public string StepId
    {
        get
        {
            return this.StepIdField;
        }
        set
        {
            this.StepIdField = value ?? string.Empty;
        }
    }

    public bool Succeeded
    {
        get
        {
            return this.SucceededField;
        }
        set
        {
            this.SucceededField = value;
        }
    }

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

    public int ProcessedClips
    {
        get
        {
            return this.ProcessedClipsField;
        }
        set
        {
            this.ProcessedClipsField = value;
        }
    }

    public int SkippedClips
    {
        get
        {
            return this.SkippedClipsField;
        }
        set
        {
            this.SkippedClipsField = value;
        }
    }

    public string Message
    {
        get
        {
            return this.MessageField;
        }
        set
        {
            this.MessageField = value ?? string.Empty;
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

    public ProjectPreflightState? UpdatedPreflightState
    {
        get
        {
            return this.UpdatedPreflightStateField;
        }
        set
        {
            this.UpdatedPreflightStateField = value;
        }
    }

    #endregion

    #region Public Methods

    public static StepExecutionResult Success(
        string StepId,
        int TotalClips,
        int ProcessedClips,
        int SkippedClips,
        string Message = "",
        ProjectPreflightState? UpdatedPreflightState = null)
    {
        StepExecutionResult Result = new StepExecutionResult
        {
            StepId = StepId,
            Succeeded = true,
            TotalClips = TotalClips,
            ProcessedClips = ProcessedClips,
            SkippedClips = SkippedClips,
            Message = Message,
            UpdatedPreflightState = UpdatedPreflightState
        };
        return Result;
    }

    public static StepExecutionResult Failure(string StepId, string ErrorMessage)
    {
        StepExecutionResult Result = new StepExecutionResult
        {
            StepId = StepId,
            Succeeded = false,
            ErrorMessage = ErrorMessage
        };
        return Result;
    }

    #endregion
}

public sealed class PipelineExecutionResult
{
    #region Fields

    private bool SucceededField = false;
    private bool WasCancelledField = false;
    private bool WasRolledBackField = false;
    private string TransactionIdField = string.Empty;
    private string MessageField = string.Empty;
    private string ErrorMessageField = string.Empty;
    private List<StepExecutionResult> StepResultsField = new List<StepExecutionResult>();

    #endregion

    #region Properties

    public bool Succeeded
    {
        get
        {
            return this.SucceededField;
        }
        set
        {
            this.SucceededField = value;
        }
    }

    public bool WasCancelled
    {
        get
        {
            return this.WasCancelledField;
        }
        set
        {
            this.WasCancelledField = value;
        }
    }

    public bool WasRolledBack
    {
        get
        {
            return this.WasRolledBackField;
        }
        set
        {
            this.WasRolledBackField = value;
        }
    }

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

    public string Message
    {
        get
        {
            return this.MessageField;
        }
        set
        {
            this.MessageField = value ?? string.Empty;
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

    public List<StepExecutionResult> StepResults
    {
        get
        {
            return this.StepResultsField;
        }
        set
        {
            this.StepResultsField = value ?? new List<StepExecutionResult>();
        }
    }

    #endregion
}

public sealed class PipelineProgressUpdate
{
    #region Fields

    private string StepIdField = string.Empty;
    private string StepDisplayNameField = string.Empty;
    private int CurrentStepNumberField = 0;
    private int TotalStepsField = 0;
    private int CurrentBatchIndexField = 0;
    private int CurrentClipInBatchField = 0;
    private int TotalClipsField = 0;
    private double PercentageField = 0.0;
    private string MessageField = string.Empty;
    private bool IsErrorField = false;
    private DateTimeOffset TimestampUtcField = DateTimeOffset.UtcNow;

    #endregion

    #region Properties

    public string StepId
    {
        get
        {
            return this.StepIdField;
        }
        set
        {
            this.StepIdField = value ?? string.Empty;
        }
    }

    public string StepDisplayName
    {
        get
        {
            return this.StepDisplayNameField;
        }
        set
        {
            this.StepDisplayNameField = value ?? string.Empty;
        }
    }

    public int CurrentStepNumber
    {
        get
        {
            return this.CurrentStepNumberField;
        }
        set
        {
            this.CurrentStepNumberField = value;
        }
    }

    public int TotalSteps
    {
        get
        {
            return this.TotalStepsField;
        }
        set
        {
            this.TotalStepsField = value;
        }
    }

    public int CurrentBatchIndex
    {
        get
        {
            return this.CurrentBatchIndexField;
        }
        set
        {
            this.CurrentBatchIndexField = value;
        }
    }

    public int CurrentClipInBatch
    {
        get
        {
            return this.CurrentClipInBatchField;
        }
        set
        {
            this.CurrentClipInBatchField = value;
        }
    }

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

    public double Percentage
    {
        get
        {
            return this.PercentageField;
        }
        set
        {
            this.PercentageField = value;
        }
    }

    public string Message
    {
        get
        {
            return this.MessageField;
        }
        set
        {
            this.MessageField = value ?? string.Empty;
        }
    }

    public bool IsError
    {
        get
        {
            return this.IsErrorField;
        }
        set
        {
            this.IsErrorField = value;
        }
    }

    public DateTimeOffset TimestampUtc
    {
        get
        {
            return this.TimestampUtcField;
        }
        set
        {
            this.TimestampUtcField = value;
        }
    }

    #endregion
}

public sealed class PipelineContext
{
    #region Fields

    private PipelineSettings SettingsField = new PipelineSettings();
    private ProjectPreflightState PreflightStateField = new ProjectPreflightState();
    private TransactionManifest? TransactionField = null;
    private IProgress<PipelineProgressUpdate>? ProgressField = null;
    private IReadOnlyList<string> SelectedStepIdsField = Array.Empty<string>();
    private int StepNumberField = 1;
    private int TotalStepsField = 1;

    #endregion

    #region Properties

    public PipelineSettings Settings
    {
        get
        {
            return this.SettingsField;
        }
        set
        {
            this.SettingsField = value ?? new PipelineSettings();
        }
    }

    public ProjectPreflightState PreflightState
    {
        get
        {
            return this.PreflightStateField;
        }
        set
        {
            this.PreflightStateField = value ?? new ProjectPreflightState();
        }
    }

    public TransactionManifest? Transaction
    {
        get
        {
            return this.TransactionField;
        }
        set
        {
            this.TransactionField = value;
        }
    }

    public IProgress<PipelineProgressUpdate>? Progress
    {
        get
        {
            return this.ProgressField;
        }
        set
        {
            this.ProgressField = value;
        }
    }

    public IReadOnlyList<string> SelectedStepIds
    {
        get
        {
            return this.SelectedStepIdsField;
        }
        set
        {
            this.SelectedStepIdsField = value ?? Array.Empty<string>();
        }
    }

    public int StepNumber
    {
        get
        {
            return this.StepNumberField;
        }
        set
        {
            this.StepNumberField = value;
        }
    }

    public int TotalSteps
    {
        get
        {
            return this.TotalStepsField;
        }
        set
        {
            this.TotalStepsField = value;
        }
    }

    #endregion
}
