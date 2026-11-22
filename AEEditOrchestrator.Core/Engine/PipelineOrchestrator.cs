using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Steps;

namespace AEEditOrchestrator.Core.Engine;

public sealed class PipelineOrchestrator
{
    #region Constants

    private const int Stage1MaxExecutionOrder = 60;
    private const int Stage2MinExecutionOrder = 70;
    private const string Stage1MilestoneName = "AfterStage1";
    private const string Stage1MilestoneFileName = "01_After_Stage1.aep";

    #endregion

    #region Fields

    private readonly StepRegistry RegistryField;
    private readonly StepDependencyValidator ValidatorField;
    private readonly IAfterEffectsBridge BridgeField;
    private readonly ITransactionManager TransactionManagerField;

    #endregion

    #region Constructors

    public PipelineOrchestrator(
        StepRegistry Registry,
        StepDependencyValidator Validator,
        IAfterEffectsBridge Bridge,
        ITransactionManager TransactionManager)
    {
        if (Registry == null)
        {
            throw new ArgumentNullException(nameof(Registry));
        }

        if (Validator == null)
        {
            throw new ArgumentNullException(nameof(Validator));
        }

        if (Bridge == null)
        {
            throw new ArgumentNullException(nameof(Bridge));
        }

        if (TransactionManager == null)
        {
            throw new ArgumentNullException(nameof(TransactionManager));
        }

        this.RegistryField = Registry;
        this.ValidatorField = Validator;
        this.BridgeField = Bridge;
        this.TransactionManagerField = TransactionManager;
    }

    #endregion

    #region Public Methods

    public async Task<ProjectPreflightState> RunPreflightAsync(
        PipelineSettings Settings,
        IProgress<PipelineProgressUpdate>? Progress = null,
        CancellationToken CancellationToken = default)
    {
        if (Settings == null)
        {
            throw new ArgumentNullException(nameof(Settings));
        }

        CancellationToken.ThrowIfCancellationRequested();

        if (!this.BridgeField.IsAfterEffectsRunning())
        {
            ProjectPreflightState NotRunningState = new ProjectPreflightState
            {
                IsAfterEffectsRunning = false,
                HasActiveComposition = false,
                IsProjectSaved = false,
                CheckedAtUtc = DateTimeOffset.UtcNow,
                ErrorMessage = "Adobe After Effects läuft nicht. Bitte After Effects starten und ein gespeichertes Projekt öffnen."
            };

            this.ReportOrchestratorProgress(
                Progress,
                PipelineStepIds.Preflight,
                "Preflight-Prüfung",
                0.0,
                NotRunningState.ErrorMessage,
                true);

            return NotRunningState;
        }

        IPipelineStep? PreflightStep = this.RegistryField.GetStep(PipelineStepIds.Preflight);
        if (PreflightStep == null)
        {
            PreflightStep = new PreflightCheckStep(this.BridgeField);
        }

        PipelineContext Context = new PipelineContext
        {
            Settings = Settings,
            PreflightState = new ProjectPreflightState
            {
                IsAfterEffectsRunning = true
            },
            Progress = Progress,
            SelectedStepIds = Array.Empty<string>(),
            StepNumber = 1,
            TotalSteps = 1
        };

        try
        {
            StepExecutionResult StepResult = await PreflightStep.ExecuteAsync(Context, CancellationToken);
            ProjectPreflightState State;

            if (StepResult.UpdatedPreflightState != null)
            {
                State = StepResult.UpdatedPreflightState;
                State.IsAfterEffectsRunning = true;
                State.CheckedAtUtc = DateTimeOffset.UtcNow;

                if (!StepResult.Succeeded && string.IsNullOrWhiteSpace(State.ErrorMessage))
                {
                    if (!string.IsNullOrWhiteSpace(StepResult.ErrorMessage))
                    {
                        State.ErrorMessage = StepResult.ErrorMessage;
                    }
                    else
                    {
                        State.ErrorMessage = "Preflight-Prüfung fehlgeschlagen.";
                    }
                }
            }
            else
            {
                string ErrorMsg = string.Empty;
                if (!StepResult.Succeeded)
                {
                    if (!string.IsNullOrWhiteSpace(StepResult.ErrorMessage))
                    {
                        ErrorMsg = StepResult.ErrorMessage;
                    }
                    else
                    {
                        ErrorMsg = "Preflight-Prüfung fehlgeschlagen.";
                    }
                }
                else
                {
                    ErrorMsg = "Preflight-Prüfung lieferte keinen Projektstatus.";
                }

                State = new ProjectPreflightState
                {
                    IsAfterEffectsRunning = true,
                    CheckedAtUtc = DateTimeOffset.UtcNow,
                    ErrorMessage = ErrorMsg
                };
            }

            return State;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception Ex)
        {
            ProjectPreflightState FailedState = new ProjectPreflightState
            {
                IsAfterEffectsRunning = this.BridgeField.IsAfterEffectsRunning(),
                CheckedAtUtc = DateTimeOffset.UtcNow,
                ErrorMessage = Ex.Message
            };

            return FailedState;
        }
        finally
        {
            this.BridgeField.CleanupIpcFiles();
        }
    }

    public async Task<PipelineExecutionResult> ExecutePipelineAsync(
        PipelineSettings Settings,
        IReadOnlyCollection<string> SelectedStepIds,
        IProgress<PipelineProgressUpdate>? Progress = null,
        CancellationToken CancellationToken = default)
    {
        if (Settings == null)
        {
            throw new ArgumentNullException(nameof(Settings));
        }

        if (SelectedStepIds == null)
        {
            throw new ArgumentNullException(nameof(SelectedStepIds));
        }

        if (CancellationToken.IsCancellationRequested)
        {
            return new PipelineExecutionResult
            {
                Succeeded = false,
                WasCancelled = true,
                WasRolledBack = false,
                ErrorMessage = "Pipeline-Ausführung wurde vor dem Start abgebrochen.",
                Message = "Abgebrochen."
            };
        }

        ProjectPreflightState PreflightState;
        try
        {
            PreflightState = await this.RunPreflightAsync(Settings, Progress, CancellationToken);
        }
        catch (OperationCanceledException)
        {
            this.BridgeField.CleanupIpcFiles();
            return new PipelineExecutionResult
            {
                Succeeded = false,
                WasCancelled = true,
                WasRolledBack = false,
                ErrorMessage = "Pipeline-Ausführung während des Preflights abgebrochen.",
                Message = "Abgebrochen."
            };
        }

        if (!PreflightState.IsReady)
        {
            string NotReadyReason = PipelineOrchestrator.BuildNotReadyReason(PreflightState);
            this.ReportOrchestratorProgress(
                Progress,
                PipelineStepIds.Preflight,
                "Preflight-Prüfung",
                0.0,
                NotReadyReason,
                true);

            return new PipelineExecutionResult
            {
                Succeeded = false,
                WasCancelled = false,
                WasRolledBack = false,
                ErrorMessage = NotReadyReason,
                Message = "Preflight fehlgeschlagen."
            };
        }

        ValidationResult SelectionValidation = this.ValidatorField.ValidateSelection(
            SelectedStepIds,
            Settings,
            PreflightState);

        if (!SelectionValidation.IsValid)
        {
            string ValidationError = string.Join(" ", SelectionValidation.Errors);
            this.ReportOrchestratorProgress(
                Progress,
                PipelineStepIds.Preflight,
                "Abhängigkeitsprüfung",
                0.0,
                ValidationError,
                true);

            return new PipelineExecutionResult
            {
                Succeeded = false,
                WasCancelled = false,
                WasRolledBack = false,
                ErrorMessage = ValidationError,
                Message = "Schritt-Auswahl ungültig."
            };
        }

        IReadOnlyList<string> RequiredPresets = this.ValidatorField.GetEffectiveRequiredPresets(
            SelectedStepIds,
            Settings);

        List<string> MissingSelectedPresets = PipelineOrchestrator.FindMissingRequiredPresets(
            RequiredPresets,
            PreflightState.MissingPresets);

        if (MissingSelectedPresets.Count > 0)
        {
            string PresetError = $"Folgende für die gewählten Schritte benötigten Presets fehlen: {string.Join(", ", MissingSelectedPresets)}.";
            this.ReportOrchestratorProgress(
                Progress,
                PipelineStepIds.Preflight,
                "Preset-Prüfung",
                0.0,
                PresetError,
                true);

            return new PipelineExecutionResult
            {
                Succeeded = false,
                WasCancelled = false,
                WasRolledBack = false,
                ErrorMessage = PresetError,
                Message = "Fehlende Presets."
            };
        }

        List<IPipelineStep> OrderedSteps = this.ResolveOrderedSteps(SelectedStepIds);
        List<string> OrderedStepIds = new List<string>();
        foreach (IPipelineStep OrderedStep in OrderedSteps)
        {
            OrderedStepIds.Add(OrderedStep.StepId);
        }

        TransactionManifest Manifest;
        try
        {
            CancellationToken.ThrowIfCancellationRequested();

            bool SavedBeforeSnapshot = await this.BridgeField.SaveProjectAsync(Settings, CancellationToken);
            if (!SavedBeforeSnapshot)
            {
                return new PipelineExecutionResult
                {
                    Succeeded = false,
                    WasCancelled = false,
                    WasRolledBack = false,
                    ErrorMessage = "Projekt konnte vor dem Snapshot nicht gespeichert werden.",
                    Message = "Speichern fehlgeschlagen."
                };
            }

            Manifest = await this.TransactionManagerField.BeginTransactionAsync(
                PreflightState.ProjectPath,
                OrderedStepIds,
                CancellationToken);
        }
        catch (OperationCanceledException)
        {
            this.BridgeField.CleanupIpcFiles();
            return new PipelineExecutionResult
            {
                Succeeded = false,
                WasCancelled = true,
                WasRolledBack = false,
                ErrorMessage = "Pipeline-Ausführung vor Transaktionsbeginn abgebrochen.",
                Message = "Abgebrochen."
            };
        }
        catch (Exception Ex)
        {
            this.BridgeField.CleanupIpcFiles();
            return new PipelineExecutionResult
            {
                Succeeded = false,
                WasCancelled = false,
                WasRolledBack = false,
                ErrorMessage = $"Transaktion konnte nicht gestartet werden: {Ex.Message}",
                Message = "Transaktionsstart fehlgeschlagen."
            };
        }

        List<StepExecutionResult> StepResults = new List<StepExecutionResult>();
        ProjectPreflightState CurrentPreflightState = PreflightState;
        bool HasCompletedStage1Step = false;
        bool MilestoneCreated = false;

        try
        {
            for (int Index = 0; Index < OrderedSteps.Count; Index++)
            {
                CancellationToken.ThrowIfCancellationRequested();

                IPipelineStep CurrentStep = OrderedSteps[Index];

                if (!MilestoneCreated
                    && HasCompletedStage1Step
                    && CurrentStep.ExecutionOrder >= PipelineOrchestrator.Stage2MinExecutionOrder)
                {
                    this.ReportOrchestratorProgress(
                        Progress,
                        CurrentStep.StepId,
                        "Meilenstein-Snapshot",
                        ((double)Index * 100.0) / OrderedSteps.Count,
                        "Speichere Projektstand und erstelle Stage-1-Meilenstein-Snapshot...",
                        false);

                    bool SavedForMilestone = await this.BridgeField.SaveProjectAsync(Settings, CancellationToken);
                    if (!SavedForMilestone)
                    {
                        return await this.RollbackAndBuildFailureResultAsync(
                            Manifest,
                            Settings,
                            "Projekt konnte vor dem Stage-1-Meilenstein-Snapshot nicht gespeichert werden.",
                            false,
                            StepResults,
                            Progress);
                    }

                    await this.TransactionManagerField.CreateMilestoneSnapshotAsync(
                        Manifest,
                        PipelineOrchestrator.Stage1MilestoneName,
                        PipelineOrchestrator.Stage1MilestoneFileName,
                        CancellationToken);

                    MilestoneCreated = true;
                }

                PipelineContext StepContext = new PipelineContext
                {
                    Settings = Settings,
                    PreflightState = CurrentPreflightState,
                    Transaction = Manifest,
                    Progress = Progress,
                    SelectedStepIds = OrderedStepIds,
                    StepNumber = Index + 1,
                    TotalSteps = OrderedSteps.Count
                };

                StepExecutionResult StepExecResult = await CurrentStep.ExecuteAsync(StepContext, CancellationToken);
                StepResults.Add(StepExecResult);

                if (StepExecResult.UpdatedPreflightState != null)
                {
                    CurrentPreflightState = PipelineOrchestrator.MergePreflightState(
                        CurrentPreflightState,
                        StepExecResult.UpdatedPreflightState);
                }

                if (!StepExecResult.Succeeded)
                {
                    string StepError = StepExecResult.ErrorMessage;
                    if (string.IsNullOrWhiteSpace(StepError))
                    {
                        StepError = $"Schritt '{CurrentStep.DisplayName}' ({CurrentStep.StepId}) ist fehlgeschlagen.";
                    }

                    return await this.RollbackAndBuildFailureResultAsync(
                        Manifest,
                        Settings,
                        StepError,
                        false,
                        StepResults,
                        Progress);
                }

                if (CurrentStep.ExecutionOrder <= PipelineOrchestrator.Stage1MaxExecutionOrder)
                {
                    HasCompletedStage1Step = true;
                }
            }

            CancellationToken.ThrowIfCancellationRequested();

            bool FinalSaveOk = await this.BridgeField.SaveProjectAsync(Settings, CancellationToken);
            if (!FinalSaveOk)
            {
                return await this.RollbackAndBuildFailureResultAsync(
                    Manifest,
                    Settings,
                    "Projekt konnte nach Abschluss der Pipeline nicht gespeichert werden.",
                    false,
                    StepResults,
                    Progress);
            }

            await this.TransactionManagerField.CommitTransactionAsync(Manifest, Settings, CancellationToken);
            await this.TransactionManagerField.RotateBackupsAsync(
                PreflightState.ProjectPath,
                Settings.MaxBackupsToKeep,
                CancellationToken);

            this.BridgeField.CleanupIpcFiles();

            this.ReportOrchestratorProgress(
                Progress,
                OrderedStepIds[OrderedStepIds.Count - 1],
                "Pipeline abgeschlossen",
                100.0,
                "Alle ausgewählten Pipeline-Schritte wurden erfolgreich abgeschlossen und gespeichert.",
                false);

            return new PipelineExecutionResult
            {
                Succeeded = true,
                WasCancelled = false,
                WasRolledBack = false,
                TransactionId = Manifest.TransactionId,
                Message = "Pipeline erfolgreich abgeschlossen.",
                StepResults = StepResults
            };
        }
        catch (OperationCanceledException)
        {
            bool IsUserCancel = CancellationToken.IsCancellationRequested;
            string CancelReason = "Pipeline-Ausführung wurde wegen eines Timeouts abgebrochen.";
            if (IsUserCancel)
            {
                CancelReason = "Pipeline-Ausführung wurde vom Benutzer abgebrochen.";
            }

            return await this.RollbackAndBuildFailureResultAsync(
                Manifest,
                Settings,
                CancelReason,
                IsUserCancel,
                StepResults,
                Progress);
        }
        catch (Exception Ex)
        {
            return await this.RollbackAndBuildFailureResultAsync(
                Manifest,
                Settings,
                Ex.Message,
                false,
                StepResults,
                Progress);
        }
    }

    public async Task<StepExecutionResult> ExecuteReverseRollbackAsync(
        PipelineSettings Settings,
        ReverseRollbackMode Mode,
        IProgress<PipelineProgressUpdate>? Progress = null,
        CancellationToken CancellationToken = default)
    {
        if (Settings == null)
        {
            throw new ArgumentNullException(nameof(Settings));
        }

        CancellationToken.ThrowIfCancellationRequested();

        if (!this.BridgeField.IsAfterEffectsRunning())
        {
            string NotRunningMsg = "Adobe After Effects läuft nicht. Granulares Reverse nicht möglich.";
            this.ReportOrchestratorProgress(
                Progress,
                PipelineStepIds.Reverse,
                "Granulares Reverse",
                0.0,
                NotRunningMsg,
                true);
            return StepExecutionResult.Failure(PipelineStepIds.Reverse, NotRunningMsg);
        }

        PipelineSettings EffectiveSettings = PipelineSettingsCloner.Clone(
            Settings,
            null,
            Mode);

        IPipelineStep? ReverseStep = this.RegistryField.GetStep(PipelineStepIds.Reverse);
        if (ReverseStep == null)
        {
            ReverseStep = new ReverseRollbackStep(this.BridgeField);
        }

        PipelineContext Context = new PipelineContext
        {
            Settings = EffectiveSettings,
            PreflightState = new ProjectPreflightState
            {
                IsAfterEffectsRunning = true
            },
            Progress = Progress,
            SelectedStepIds = new string[] { PipelineStepIds.Reverse },
            StepNumber = 1,
            TotalSteps = 1
        };

        try
        {
            return await ReverseStep.ExecuteAsync(Context, CancellationToken);
        }
        finally
        {
            this.BridgeField.CleanupIpcFiles();
        }
    }

    #endregion

    #region Private Methods

    private List<IPipelineStep> ResolveOrderedSteps(IReadOnlyCollection<string> SelectedStepIds)
    {
        HashSet<string> Seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<IPipelineStep> Steps = new List<IPipelineStep>();

        foreach (string RawId in SelectedStepIds)
        {
            if (string.IsNullOrWhiteSpace(RawId))
            {
                continue;
            }

            IPipelineStep? Step = this.RegistryField.GetStep(RawId.Trim());
            if (Step != null && Seen.Add(Step.StepId))
            {
                Steps.Add(Step);
            }
        }

        Steps.Sort(StepRegistry.CompareStepsByExecutionOrder);
        return Steps;
    }

    private async Task<PipelineExecutionResult> RollbackAndBuildFailureResultAsync(
        TransactionManifest Manifest,
        PipelineSettings Settings,
        string Reason,
        bool WasCancelled,
        List<StepExecutionResult> StepResults,
        IProgress<PipelineProgressUpdate>? Progress)
    {
        bool RollbackSucceeded = false;
        string EffectiveError = Reason;

        try
        {
            this.ReportOrchestratorProgress(
                Progress,
                PipelineStepIds.Reverse,
                "Rollback läuft...",
                0.0,
                "Schließe After-Effects-Projekt und stelle Original-Snapshot wieder her...",
                false);

            RollbackSucceeded = await this.TransactionManagerField.RollbackTransactionAsync(
                Manifest,
                Settings,
                Reason,
                CancellationToken.None);

            if (!RollbackSucceeded)
            {
                EffectiveError = $"{Reason} (Automatischer Rollback fehlgeschlagen.)";
            }
        }
        catch (Exception RollbackEx)
        {
            RollbackSucceeded = false;
            EffectiveError = $"{Reason} (Rollback-Fehler: {RollbackEx.Message})";
        }
        finally
        {
            this.BridgeField.CleanupIpcFiles();
        }

        string StatusMessage = "Pipeline fehlgeschlagen und auf Ausgangszustand zurückgesetzt.";
        if (!RollbackSucceeded)
        {
            StatusMessage = "Pipeline fehlgeschlagen; Rollback konnte nicht vollständig abgeschlossen werden.";
        }
        else if (WasCancelled)
        {
            StatusMessage = "Pipeline abgebrochen und auf Ausgangszustand zurückgesetzt.";
        }

        this.ReportOrchestratorProgress(
            Progress,
            PipelineStepIds.Reverse,
            "Rollback",
            0.0,
            $"{StatusMessage} Grund: {EffectiveError}",
            true);

        return new PipelineExecutionResult
        {
            Succeeded = false,
            WasCancelled = WasCancelled,
            WasRolledBack = RollbackSucceeded,
            TransactionId = Manifest.TransactionId,
            Message = StatusMessage,
            ErrorMessage = EffectiveError,
            StepResults = StepResults
        };
    }

    private static string BuildNotReadyReason(ProjectPreflightState PreflightState)
    {
        if (!string.IsNullOrWhiteSpace(PreflightState.ErrorMessage))
        {
            return PreflightState.ErrorMessage;
        }

        if (!PreflightState.IsAfterEffectsRunning)
        {
            return "Adobe After Effects läuft nicht.";
        }

        if (!PreflightState.HasActiveComposition)
        {
            return "Keine aktive Komposition in After Effects geöffnet.";
        }

        if (!PreflightState.IsProjectSaved || string.IsNullOrWhiteSpace(PreflightState.ProjectPath))
        {
            return "Das After-Effects-Projekt muss vor dem Start gespeichert sein (.aep).";
        }

        return "Projekt ist laut Preflight nicht bereit.";
    }

    private static List<string> FindMissingRequiredPresets(
        IReadOnlyList<string> RequiredPresets,
        List<string> MissingPresetsFromPreflight)
    {
        List<string> Missing = new List<string>();
        if (RequiredPresets.Count == 0 || MissingPresetsFromPreflight == null || MissingPresetsFromPreflight.Count == 0)
        {
            return Missing;
        }

        foreach (string Required in RequiredPresets)
        {
            foreach (string MissingItem in MissingPresetsFromPreflight)
            {
                if (string.Equals(Required, MissingItem, StringComparison.OrdinalIgnoreCase))
                {
                    Missing.Add(Required);
                    break;
                }
            }
        }

        return Missing;
    }

    private static ProjectPreflightState MergePreflightState(
        ProjectPreflightState Current,
        ProjectPreflightState Update)
    {
        string ProjectPath = Current.ProjectPath;
        if (!string.IsNullOrWhiteSpace(Update.ProjectPath))
        {
            ProjectPath = Update.ProjectPath;
        }

        string CompositionName = Current.CompositionName;
        if (!string.IsNullOrWhiteSpace(Update.CompositionName))
        {
            CompositionName = Update.CompositionName;
        }

        int ValidClipCount = Current.ValidClipCount;
        if (Update.ValidClipCount > 0)
        {
            ValidClipCount = Update.ValidClipCount;
        }

        List<string> MissingPresets = Current.MissingPresets;
        if (Update.MissingPresets.Count > 0)
        {
            MissingPresets = new List<string>(Update.MissingPresets);
        }

        return new ProjectPreflightState
        {
            IsAfterEffectsRunning = Current.IsAfterEffectsRunning && Update.IsAfterEffectsRunning,
            HasActiveComposition = Current.HasActiveComposition || Update.HasActiveComposition,
            CompositionName = CompositionName,
            IsProjectSaved = Current.IsProjectSaved || Update.IsProjectSaved,
            ProjectPath = ProjectPath,
            ValidClipCount = ValidClipCount,
            HasEditClipTags = Current.HasEditClipTags || Update.HasEditClipTags,
            HasStage1Tags = Current.HasStage1Tags || Update.HasStage1Tags,
            HasZoomGroupTags = Current.HasZoomGroupTags || Update.HasZoomGroupTags,
            MissingPresets = MissingPresets,
            CheckedAtUtc = Update.CheckedAtUtc,
            ErrorMessage = Update.ErrorMessage
        };
    }

    private void ReportOrchestratorProgress(
        IProgress<PipelineProgressUpdate>? Progress,
        string StepId,
        string DisplayName,
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
            StepId = StepId,
            StepDisplayName = DisplayName,
            CurrentStepNumber = 0,
            TotalSteps = 0,
            CurrentBatchIndex = 0,
            CurrentClipInBatch = 0,
            TotalClips = 0,
            Percentage = Percentage,
            Message = Message,
            IsError = IsError,
            TimestampUtc = DateTimeOffset.UtcNow
        };

        Progress.Report(Update);
    }

    #endregion
}
