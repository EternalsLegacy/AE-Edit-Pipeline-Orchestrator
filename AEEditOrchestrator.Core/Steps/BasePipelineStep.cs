using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Models.Ipc;

namespace AEEditOrchestrator.Core.Steps;

public abstract class BasePipelineStep : IPipelineStep
{
    #region Fields

    private readonly IAfterEffectsBridge BridgeField;

    #endregion

    #region Properties

    public abstract string StepId { get; }

    public abstract string DisplayName { get; }

    public abstract string Description { get; }

    public abstract int ExecutionOrder { get; }

    public virtual IReadOnlyList<string> RequiredStepIds
    {
        get
        {
            return Array.Empty<string>();
        }
    }

    public abstract string JsxScriptFileName { get; }

    public abstract bool SupportsBatching { get; }

    protected IAfterEffectsBridge Bridge
    {
        get
        {
            return this.BridgeField;
        }
    }

    #endregion

    #region Constructors

    protected BasePipelineStep(IAfterEffectsBridge Bridge)
    {
        if (Bridge == null)
        {
            throw new ArgumentNullException(nameof(Bridge));
        }

        this.BridgeField = Bridge;
    }

    #endregion

    #region Public Methods

    public virtual ValidationResult ValidateSettings(
        PipelineSettings Settings,
        ProjectPreflightState PreflightState)
    {
        if (Settings == null)
        {
            return ValidationResult.Failure("Einstellungen dürfen nicht null sein.");
        }

        if (this.SupportsBatching && Settings.BatchSize < 1)
        {
            return ValidationResult.Failure($"Schritt '{this.DisplayName}' benötigt eine BatchSize von mindestens 1.");
        }

        return ValidationResult.Success();
    }

    public virtual IReadOnlyList<string> GetRequiredPresets(PipelineSettings Settings)
    {
        return Array.Empty<string>();
    }

    public async Task<StepExecutionResult> ExecuteAsync(
        PipelineContext Context,
        CancellationToken CancellationToken)
    {
        if (Context == null)
        {
            throw new ArgumentNullException(nameof(Context));
        }

        CancellationToken.ThrowIfCancellationRequested();

        PipelineSettings Settings = Context.Settings;
        int EffectiveTotalSteps = 1;
        if (Context.TotalSteps > 0)
        {
            EffectiveTotalSteps = Context.TotalSteps;
        }

        int EffectiveStepNumber = Context.StepNumber;
        if (EffectiveStepNumber < 1)
        {
            EffectiveStepNumber = 1;
        }
        else if (EffectiveStepNumber > EffectiveTotalSteps)
        {
            EffectiveStepNumber = EffectiveTotalSteps;
        }

        int BatchStartIndex = 0;
        int BatchNumber = 0;
        int TotalClips = 0;
        int TotalProcessedClips = 0;
        int TotalSkippedClips = 0;
        string LastMessage = string.Empty;
        ProjectPreflightState? UpdatedPreflightState = null;

        this.ReportProgress(
            Context.Progress,
            EffectiveStepNumber,
            EffectiveTotalSteps,
            BatchNumber,
            0,
            0,
            0.0,
            $"Starte {this.DisplayName}...",
            false);

        while (true)
        {
            CancellationToken.ThrowIfCancellationRequested();

            JobPayload Payload = this.CreateJobPayload(Context, BatchStartIndex);
            HeartbeatProgressAdapter HeartbeatAdapter = new HeartbeatProgressAdapter(
                this,
                Context.Progress,
                EffectiveStepNumber,
                EffectiveTotalSteps,
                BatchNumber,
                BatchStartIndex,
                TotalClips);

            StepResult Result = await this.BridgeField.ExecuteJobAsync(
                Payload,
                Settings,
                HeartbeatAdapter,
                CancellationToken);

            if (Result == null)
            {
                string NullResultError = $"Schritt '{this.StepId}' hat kein Ergebnis von After Effects erhalten.";
                this.ReportProgress(
                    Context.Progress,
                    EffectiveStepNumber,
                    EffectiveTotalSteps,
                    BatchNumber,
                    0,
                    TotalClips,
                    this.CalculateOverallPercentage(EffectiveStepNumber, EffectiveTotalSteps, 0.0),
                    NullResultError,
                    true);
                return StepExecutionResult.Failure(this.StepId, NullResultError);
            }

            if (Result.DetectedState != null)
            {
                UpdatedPreflightState = BasePipelineStep.MapToPreflightState(Result);
            }

            if (!Result.IsOk)
            {
                string ErrorText = BasePipelineStep.FormatStepResultError(Result, this.StepId);
                this.ReportProgress(
                    Context.Progress,
                    EffectiveStepNumber,
                    EffectiveTotalSteps,
                    BatchNumber,
                    0,
                    Result.TotalClips,
                    this.CalculateOverallPercentage(EffectiveStepNumber, EffectiveTotalSteps, 0.0),
                    ErrorText,
                    true);

                StepExecutionResult FailureResult = StepExecutionResult.Failure(this.StepId, ErrorText);
                FailureResult.UpdatedPreflightState = UpdatedPreflightState;
                return FailureResult;
            }

            if (Result.TotalClips > TotalClips)
            {
                TotalClips = Result.TotalClips;
            }

            TotalProcessedClips += Result.ProcessedInBatch;
            TotalSkippedClips += Result.SkippedInBatch;

            if (!string.IsNullOrWhiteSpace(Result.Message))
            {
                LastMessage = Result.Message;
            }

            double StepFraction = 1.0;
            if (this.SupportsBatching && Result.HasMoreBatches && TotalClips > 0)
            {
                int CompletedSoFar = TotalProcessedClips + TotalSkippedClips;
                if (Result.NextBatchIndex > CompletedSoFar)
                {
                    CompletedSoFar = Result.NextBatchIndex;
                }

                StepFraction = Math.Clamp((double)CompletedSoFar / TotalClips, 0.0, 1.0);
            }

            double OverallPercentage = this.CalculateOverallPercentage(
                EffectiveStepNumber,
                EffectiveTotalSteps,
                StepFraction);

            string BatchMessage = LastMessage;
            if (string.IsNullOrWhiteSpace(BatchMessage))
            {
                BatchMessage = $"{this.DisplayName}: Batch {BatchNumber + 1} abgeschlossen.";
            }

            this.ReportProgress(
                Context.Progress,
                EffectiveStepNumber,
                EffectiveTotalSteps,
                BatchNumber,
                Result.ProcessedInBatch,
                TotalClips,
                OverallPercentage,
                BatchMessage,
                false);

            if (!this.SupportsBatching || !Result.HasMoreBatches)
            {
                break;
            }

            CancellationToken.ThrowIfCancellationRequested();

            int NextIndex = Result.NextBatchIndex;
            if (NextIndex <= BatchStartIndex)
            {
                int BatchDelta = Result.ProcessedInBatch + Result.SkippedInBatch;
                if (BatchDelta > 0)
                {
                    NextIndex = BatchStartIndex + BatchDelta;
                }
                else if (Settings.BatchSize > 0)
                {
                    NextIndex = BatchStartIndex + Settings.BatchSize;
                }
                else
                {
                    string InfiniteLoopError = $"Schritt '{this.StepId}' meldet weitere Batches ohne Fortschritt (BatchStartIndex={BatchStartIndex}).";
                    return StepExecutionResult.Failure(this.StepId, InfiniteLoopError);
                }
            }

            BatchStartIndex = NextIndex;
            BatchNumber++;
        }

        if (string.IsNullOrWhiteSpace(LastMessage))
        {
            LastMessage = $"{this.DisplayName} abgeschlossen ({TotalProcessedClips} verarbeitet, {TotalSkippedClips} übersprungen).";
        }

        return StepExecutionResult.Success(
            this.StepId,
            TotalClips,
            TotalProcessedClips,
            TotalSkippedClips,
            LastMessage,
            UpdatedPreflightState);
    }

    #endregion

    #region Protected Methods

    protected virtual JobPayload CreateJobPayload(PipelineContext Context, int BatchStartIndex)
    {
        string JobId = Guid.NewGuid().ToString("N");
        IReadOnlyList<string> Presets = this.GetRequiredPresets(Context.Settings);

        return JobPayload.FromSettings(
            JobId,
            this.JsxScriptFileName,
            Context.Settings,
            BatchStartIndex,
            Presets);
    }

    protected static bool ContainsStepId(IEnumerable<string>? StepIds, string TargetStepId)
    {
        if (StepIds == null || string.IsNullOrWhiteSpace(TargetStepId))
        {
            return false;
        }

        foreach (string StepId in StepIds)
        {
            if (string.Equals(StepId?.Trim(), TargetStepId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    protected static bool HasStage1ClipPrerequisites(
        PipelineSettings Settings,
        ProjectPreflightState? PreflightState)
    {
        if (BasePipelineStep.ContainsStepId(Settings.SelectedStepIds, PipelineStepIds.Precomp))
        {
            return true;
        }

        if (PreflightState != null)
        {
            if (PreflightState.HasEditClipTags || PreflightState.HasStage1Tags)
            {
                return true;
            }
        }

        return false;
    }

    protected static bool HasZoomGroupPrerequisites(
        PipelineSettings Settings,
        ProjectPreflightState? PreflightState)
    {
        if (BasePipelineStep.ContainsStepId(Settings.SelectedStepIds, PipelineStepIds.ZoomGroup))
        {
            return true;
        }

        if (PreflightState != null && PreflightState.HasZoomGroupTags)
        {
            return true;
        }

        return false;
    }

    protected static void ValidatePresetConfigured(
        string PresetName,
        string PresetLabel,
        List<string> Errors)
    {
        if (string.IsNullOrWhiteSpace(PresetName))
        {
            Errors.Add($"Preset-Name für '{PresetLabel}' darf nicht leer sein.");
        }
    }

    protected static IReadOnlyList<string> CollectDistinctPresets(params string?[] Candidates)
    {
        List<string> Presets = new List<string>();
        HashSet<string> Seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Candidates == null)
        {
            return Presets;
        }

        foreach (string? Candidate in Candidates)
        {
            if (!string.IsNullOrWhiteSpace(Candidate))
            {
                string Trimmed = Candidate.Trim();
                if (Seen.Add(Trimmed))
                {
                    Presets.Add(Trimmed);
                }
            }
        }

        return Presets;
    }

    protected static ProjectPreflightState MapToPreflightState(StepResult Result)
    {
        DetectedProjectState? Detected = Result.DetectedState;
        bool HasActiveComp = false;
        bool IsProjectSaved = false;
        bool HasEditClipTags = false;
        bool HasStage1Tags = false;
        bool HasZoomGroupTags = false;
        List<string> MissingPresets = new List<string>();

        if (Detected != null)
        {
            HasActiveComp = Detected.HasActiveComp;
            IsProjectSaved = Detected.IsProjectSaved;
            HasEditClipTags = Detected.HasEditClipTags;
            HasStage1Tags = Detected.HasStage1Tags;
            HasZoomGroupTags = Detected.HasZoomGroupTags;
            if (Detected.MissingPresets != null)
            {
                MissingPresets = new List<string>(Detected.MissingPresets);
            }
        }

        string ErrorMessage = string.Empty;
        if (!Result.IsOk)
        {
            ErrorMessage = BasePipelineStep.FormatStepResultError(Result, PipelineStepIds.Preflight);
        }

        ProjectPreflightState State = new ProjectPreflightState
        {
            IsAfterEffectsRunning = true,
            HasActiveComposition = HasActiveComp,
            CompositionName = Result.CompositionName,
            IsProjectSaved = IsProjectSaved,
            ProjectPath = Result.ProjectPath,
            ValidClipCount = Result.TotalClips,
            HasEditClipTags = HasEditClipTags,
            HasStage1Tags = HasStage1Tags,
            HasZoomGroupTags = HasZoomGroupTags,
            MissingPresets = MissingPresets,
            CheckedAtUtc = DateTimeOffset.UtcNow,
            ErrorMessage = ErrorMessage
        };

        return State;
    }

    #endregion

    #region Private Methods

    private static string FormatStepResultError(StepResult Result, string StepId)
    {
        string BaseError = string.Empty;
        if (!string.IsNullOrWhiteSpace(Result.Error))
        {
            BaseError = Result.Error;
        }
        else if (!string.IsNullOrWhiteSpace(Result.Message))
        {
            BaseError = Result.Message;
        }
        else
        {
            BaseError = $"Schritt '{StepId}' wurde mit Status '{Result.Status}' beendet.";
        }

        if (Result.ErrorLine.HasValue)
        {
            return $"{BaseError} (Zeile {Result.ErrorLine.Value})";
        }

        return BaseError;
    }

    private double CalculateOverallPercentage(
        int EffectiveStepNumber,
        int EffectiveTotalSteps,
        double StepFraction)
    {
        double ClampedFraction = Math.Clamp(StepFraction, 0.0, 1.0);
        double StepStartPercent = ((EffectiveStepNumber - 1) * 100.0) / EffectiveTotalSteps;
        double StepWeightPercent = 100.0 / EffectiveTotalSteps;
        return Math.Clamp(StepStartPercent + (ClampedFraction * StepWeightPercent), 0.0, 100.0);
    }

    private void ReportProgress(
        IProgress<PipelineProgressUpdate>? Progress,
        int EffectiveStepNumber,
        int EffectiveTotalSteps,
        int BatchIndex,
        int CurrentClipInBatch,
        int TotalClips,
        double Percentage,
        string Message,
        bool IsError)
    {
        if (Progress == null)
        {
            return;
        }

        PipelineProgressUpdate Update = new PipelineProgressUpdate
        {
            StepId = this.StepId,
            StepDisplayName = this.DisplayName,
            CurrentStepNumber = EffectiveStepNumber,
            TotalSteps = EffectiveTotalSteps,
            CurrentBatchIndex = BatchIndex,
            CurrentClipInBatch = CurrentClipInBatch,
            TotalClips = TotalClips,
            Percentage = Percentage,
            Message = Message,
            IsError = IsError,
            TimestampUtc = DateTimeOffset.UtcNow
        };

        Progress.Report(Update);
    }

    private sealed class HeartbeatProgressAdapter : IProgress<HeartbeatMessage>
    {
        #region Fields

        private readonly BasePipelineStep OwnerField;
        private readonly IProgress<PipelineProgressUpdate>? TargetProgressField;
        private readonly int EffectiveStepNumberField;
        private readonly int EffectiveTotalStepsField;
        private readonly int BatchIndexField;
        private readonly int BatchStartIndexField;
        private readonly int KnownTotalClipsField;

        #endregion

        #region Constructors

        public HeartbeatProgressAdapter(
            BasePipelineStep Owner,
            IProgress<PipelineProgressUpdate>? TargetProgress,
            int EffectiveStepNumber,
            int EffectiveTotalSteps,
            int BatchIndex,
            int BatchStartIndex,
            int KnownTotalClips)
        {
            this.OwnerField = Owner;
            this.TargetProgressField = TargetProgress;
            this.EffectiveStepNumberField = EffectiveStepNumber;
            this.EffectiveTotalStepsField = EffectiveTotalSteps;
            this.BatchIndexField = BatchIndex;
            this.BatchStartIndexField = BatchStartIndex;
            this.KnownTotalClipsField = KnownTotalClips;
        }

        #endregion

        #region Public Methods

        public void Report(HeartbeatMessage Value)
        {
            if (this.TargetProgressField == null || Value == null)
            {
                return;
            }

            if (Value.TotalItems <= 0 && Value.CurrentIndex <= 0 && string.IsNullOrWhiteSpace(Value.Message))
            {
                return;
            }

            int TotalItems = this.KnownTotalClipsField;
            if (Value.TotalItems > 0)
            {
                TotalItems = Value.TotalItems;
            }

            double StepFraction = 0.0;
            if (Value.TotalItems > 0)
            {
                StepFraction = Math.Clamp((double)Value.CurrentIndex / Value.TotalItems, 0.0, 1.0);
            }
            else if (this.KnownTotalClipsField > 0)
            {
                int GlobalIndex = this.BatchStartIndexField + Value.CurrentIndex;
                StepFraction = Math.Clamp((double)GlobalIndex / this.KnownTotalClipsField, 0.0, 1.0);
            }

            double Percentage = this.OwnerField.CalculateOverallPercentage(
                this.EffectiveStepNumberField,
                this.EffectiveTotalStepsField,
                StepFraction);

            this.OwnerField.ReportProgress(
                this.TargetProgressField,
                this.EffectiveStepNumberField,
                this.EffectiveTotalStepsField,
                this.BatchIndexField,
                Value.CurrentIndex,
                TotalItems,
                Percentage,
                Value.Message,
                false);
        }

        #endregion
    }

    #endregion
}
