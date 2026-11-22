using System.Collections.ObjectModel;
using AEEditOrchestrator.Core.Engine;
using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;

namespace AEEditOrchestrator.App.ViewModels;

public sealed partial class PipelineViewModel : ObservableObject
{
    #region Constants

    private const string BeatModeRedOverlayLabel = "Rot-Overlay";
    private const string BeatModeUniversalLabel = "Universal 4-Zonen";
    private const string StartDirectionVerticalLabel = "Vertical";
    private const string StartDirectionHorizontalLabel = "Horizontal";
    private const string BlurZoomInLabel = "BlurZoomIn";
    private const string BlurZoomOutLabel = "BlurZoomOut";

    #endregion

    #region Fields

    private readonly OrchestratorAppState AppStateField;
    private readonly StepRegistry StepRegistryField;
    private readonly StepDependencyValidator ValidatorField;
    private readonly PipelineOrchestrator OrchestratorField;

    private readonly ObservableCollection<PipelineStepItemViewModel> StepsField = new ObservableCollection<PipelineStepItemViewModel>();
    private readonly ObservableCollection<PipelineLogEntry> LogEntriesField = new ObservableCollection<PipelineLogEntry>();
    private readonly IReadOnlyList<string> StartDirectionOptionsField = new List<string>
    {
        StartDirectionVerticalLabel,
        StartDirectionHorizontalLabel
    };
    private readonly IReadOnlyList<string> BlurZoomStartOptionsField = new List<string>
    {
        BlurZoomInLabel,
        BlurZoomOutLabel
    };
    private readonly IReadOnlyList<string> BeatModeOptionsField = new List<string>
    {
        BeatModeRedOverlayLabel,
        BeatModeUniversalLabel
    };

    private CancellationTokenSource? ActiveCancellationTokenSourceField = null;
    private bool IsInitializedField = false;
    private bool IsSyncingFromStateField = false;
    private bool IsBusyField = false;
    private bool IsPipelineRunningField = false;
    private bool IsCancellationRequestedField = false;

    private bool IsAfterEffectsConnectedField = false;
    private string ConnectionStatusTextField = "Nicht geprüft";
    private string ProjectNameTextField = "Kein Projekt geladen";
    private string ProjectFullPathTextField = string.Empty;
    private string CompositionNameTextField = "—";
    private string ClipCountTextField = "0 Clips";
    private bool HasEditClipTagsField = false;
    private bool HasStage1TagsField = false;
    private bool HasZoomGroupTagsField = false;
    private bool IsProjectUnsavedWarningField = false;
    private bool HasPreflightWarningField = false;
    private string PreflightWarningTextField = string.Empty;

    private string SelectedStartDirectionOptionField = StartDirectionVerticalLabel;
    private string SelectedBlurZoomStartOptionField = BlurZoomInLabel;
    private string SelectedBeatModeOptionField = BeatModeRedOverlayLabel;
    private bool UseSelectedOnlyField = false;

    private double ProgressRatioField = 0.0;
    private string ProgressPercentTextField = "0 %";
    private string CurrentStepStatusTextField = "Bereit – Preflight prüfen oder Pipeline starten";
    private string CurrentBatchStatusTextField = "Kein aktiver Lauf";
    private bool HasRollbackStatusField = false;
    private string RollbackStatusTextField = string.Empty;
    private bool IsRollbackErrorField = false;

    #endregion

    #region Properties

    public ObservableCollection<PipelineStepItemViewModel> Steps
    {
        get
        {
            return this.StepsField;
        }
    }

    public ObservableCollection<PipelineLogEntry> LogEntries
    {
        get
        {
            return this.LogEntriesField;
        }
    }

    public IReadOnlyList<string> StartDirectionOptions
    {
        get
        {
            return this.StartDirectionOptionsField;
        }
    }

    public IReadOnlyList<string> BlurZoomStartOptions
    {
        get
        {
            return this.BlurZoomStartOptionsField;
        }
    }

    public IReadOnlyList<string> BeatModeOptions
    {
        get
        {
            return this.BeatModeOptionsField;
        }
    }

    public string CapsLockHintText
    {
        get
        {
            return "Performance-Tipp: CAPS LOCK aktivieren (pausiert AE-Vorschau)";
        }
    }

    public bool IsBusy
    {
        get
        {
            return this.IsBusyField;
        }
        private set
        {
            if (this.SetProperty(ref this.IsBusyField, value))
            {
                this.OnPropertyChanged(nameof(this.IsNotBusy));
                this.UpdateStepSelectionEnabledState();
                this.NotifyCommandStates();
            }
        }
    }

    public bool IsNotBusy
    {
        get
        {
            return !this.IsBusyField;
        }
    }

    public bool IsPipelineRunning
    {
        get
        {
            return this.IsPipelineRunningField;
        }
        private set
        {
            if (this.SetProperty(ref this.IsPipelineRunningField, value))
            {
                this.NotifyCommandStates();
            }
        }
    }

    public bool IsCancellationRequested
    {
        get
        {
            return this.IsCancellationRequestedField;
        }
        private set
        {
            if (this.SetProperty(ref this.IsCancellationRequestedField, value))
            {
                this.NotifyCommandStates();
            }
        }
    }

    public bool IsAfterEffectsConnected
    {
        get
        {
            return this.IsAfterEffectsConnectedField;
        }
        private set
        {
            this.SetProperty(ref this.IsAfterEffectsConnectedField, value);
        }
    }

    public string ConnectionStatusText
    {
        get
        {
            return this.ConnectionStatusTextField;
        }
        private set
        {
            this.SetProperty(ref this.ConnectionStatusTextField, value ?? string.Empty);
        }
    }

    public string ProjectNameText
    {
        get
        {
            return this.ProjectNameTextField;
        }
        private set
        {
            this.SetProperty(ref this.ProjectNameTextField, value ?? string.Empty);
        }
    }

    public string ProjectFullPathText
    {
        get
        {
            return this.ProjectFullPathTextField;
        }
        private set
        {
            this.SetProperty(ref this.ProjectFullPathTextField, value ?? string.Empty);
        }
    }

    public string CompositionNameText
    {
        get
        {
            return this.CompositionNameTextField;
        }
        private set
        {
            this.SetProperty(ref this.CompositionNameTextField, value ?? string.Empty);
        }
    }

    public string ClipCountText
    {
        get
        {
            return this.ClipCountTextField;
        }
        private set
        {
            this.SetProperty(ref this.ClipCountTextField, value ?? string.Empty);
        }
    }

    public bool HasEditClipTags
    {
        get
        {
            return this.HasEditClipTagsField;
        }
        private set
        {
            this.SetProperty(ref this.HasEditClipTagsField, value);
        }
    }

    public bool HasStage1Tags
    {
        get
        {
            return this.HasStage1TagsField;
        }
        private set
        {
            this.SetProperty(ref this.HasStage1TagsField, value);
        }
    }

    public bool HasZoomGroupTags
    {
        get
        {
            return this.HasZoomGroupTagsField;
        }
        private set
        {
            this.SetProperty(ref this.HasZoomGroupTagsField, value);
        }
    }

    public bool IsProjectUnsavedWarning
    {
        get
        {
            return this.IsProjectUnsavedWarningField;
        }
        private set
        {
            this.SetProperty(ref this.IsProjectUnsavedWarningField, value);
        }
    }

    public bool HasPreflightWarning
    {
        get
        {
            return this.HasPreflightWarningField;
        }
        private set
        {
            this.SetProperty(ref this.HasPreflightWarningField, value);
        }
    }

    public string PreflightWarningText
    {
        get
        {
            return this.PreflightWarningTextField;
        }
        private set
        {
            this.SetProperty(ref this.PreflightWarningTextField, value ?? string.Empty);
        }
    }

    public string SelectedStartDirectionOption
    {
        get
        {
            return this.SelectedStartDirectionOptionField;
        }
        set
        {
            string Normalized = value ?? StartDirectionVerticalLabel;
            if (this.SetProperty(ref this.SelectedStartDirectionOptionField, Normalized) && !this.IsSyncingFromStateField)
            {
                if (string.Equals(Normalized, StartDirectionHorizontalLabel, StringComparison.Ordinal))
                {
                    this.AppStateField.CurrentSettings.StartDirection = ShakeStartDirection.Horizontal;
                }
                else
                {
                    this.AppStateField.CurrentSettings.StartDirection = ShakeStartDirection.Vertical;
                }

                this.RefreshStepBadges();
                this.AppStateField.NotifySettingsUpdated();
            }
        }
    }

    public string SelectedBlurZoomStartOption
    {
        get
        {
            return this.SelectedBlurZoomStartOptionField;
        }
        set
        {
            string Normalized = value ?? BlurZoomInLabel;
            if (this.SetProperty(ref this.SelectedBlurZoomStartOptionField, Normalized) && !this.IsSyncingFromStateField)
            {
                if (string.Equals(Normalized, BlurZoomOutLabel, StringComparison.Ordinal))
                {
                    this.AppStateField.CurrentSettings.BlurZoomStart = BlurZoomStartPreset.BlurZoomOut;
                }
                else
                {
                    this.AppStateField.CurrentSettings.BlurZoomStart = BlurZoomStartPreset.BlurZoomIn;
                }

                this.AppStateField.NotifySettingsUpdated();
            }
        }
    }

    public string SelectedBeatModeOption
    {
        get
        {
            return this.SelectedBeatModeOptionField;
        }
        set
        {
            string Normalized = value ?? BeatModeRedOverlayLabel;
            if (this.SetProperty(ref this.SelectedBeatModeOptionField, Normalized) && !this.IsSyncingFromStateField)
            {
                if (string.Equals(Normalized, BeatModeUniversalLabel, StringComparison.Ordinal))
                {
                    this.AppStateField.CurrentSettings.BeatDetection.Mode = BeatDetectionMode.UniversalFourZone;
                }
                else
                {
                    this.AppStateField.CurrentSettings.BeatDetection.Mode = BeatDetectionMode.RedOverlay;
                }

                this.AppStateField.NotifySettingsUpdated();
            }
        }
    }

    public bool UseSelectedOnly
    {
        get
        {
            return this.UseSelectedOnlyField;
        }
        set
        {
            if (this.SetProperty(ref this.UseSelectedOnlyField, value) && !this.IsSyncingFromStateField)
            {
                this.AppStateField.CurrentSettings.UseSelectedOnly = value;
                this.AppStateField.NotifySettingsUpdated();
            }
        }
    }

    public double ProgressRatio
    {
        get
        {
            return this.ProgressRatioField;
        }
        private set
        {
            this.SetProperty(ref this.ProgressRatioField, value);
        }
    }

    public string ProgressPercentText
    {
        get
        {
            return this.ProgressPercentTextField;
        }
        private set
        {
            this.SetProperty(ref this.ProgressPercentTextField, value ?? "0 %");
        }
    }

    public string CurrentStepStatusText
    {
        get
        {
            return this.CurrentStepStatusTextField;
        }
        private set
        {
            this.SetProperty(ref this.CurrentStepStatusTextField, value ?? string.Empty);
        }
    }

    public string CurrentBatchStatusText
    {
        get
        {
            return this.CurrentBatchStatusTextField;
        }
        private set
        {
            this.SetProperty(ref this.CurrentBatchStatusTextField, value ?? string.Empty);
        }
    }

    public bool HasRollbackStatus
    {
        get
        {
            return this.HasRollbackStatusField;
        }
        private set
        {
            this.SetProperty(ref this.HasRollbackStatusField, value);
        }
    }

    public string RollbackStatusText
    {
        get
        {
            return this.RollbackStatusTextField;
        }
        private set
        {
            this.SetProperty(ref this.RollbackStatusTextField, value ?? string.Empty);
        }
    }

    public bool IsRollbackError
    {
        get
        {
            return this.IsRollbackErrorField;
        }
        private set
        {
            this.SetProperty(ref this.IsRollbackErrorField, value);
        }
    }

    #endregion

    #region Constructors

    public PipelineViewModel(
        OrchestratorAppState AppState,
        StepRegistry Registry,
        StepDependencyValidator Validator,
        PipelineOrchestrator Orchestrator)
    {
        this.AppStateField = AppState;
        this.StepRegistryField = Registry;
        this.ValidatorField = Validator;
        this.OrchestratorField = Orchestrator;
        this.AppStateField.SettingsUpdated += this.OnAppStateSettingsUpdated;
    }

    #endregion

    #region Public Methods

    public async Task InitializeAsync()
    {
        await this.AppStateField.EnsureInitializedAsync();

        this.RunOnMainThread(() =>
        {
            if (!this.IsInitializedField)
            {
                this.BuildStepList();
                this.IsInitializedField = true;
                this.AppendLog("Orchestrator bereit. Preflight prüfen vor dem Start empfohlen.", false, false);
            }

            this.SyncQuickOptionsFromSettings();
            this.RefreshStepBadges();
            this.NotifyCommandStates();
        });
    }

    #endregion

    #region Private Methods

    private void OnAppStateSettingsUpdated(object? Sender, EventArgs Args)
    {
        if (this.IsSyncingFromStateField)
        {
            return;
        }

        this.RunOnMainThread(() =>
        {
            this.SyncQuickOptionsFromSettings();
            this.RefreshStepBadges();
        });
    }

    private void BuildStepList()
    {
        this.IsSyncingFromStateField = true;
        try
        {
            this.StepsField.Clear();
            IReadOnlyList<IPipelineStep> SelectableSteps = this.StepRegistryField.GetSelectablePipelineSteps();
            HashSet<string> SavedSelection = new HashSet<string>(
                this.AppStateField.CurrentSettings.SelectedStepIds,
                StringComparer.OrdinalIgnoreCase);

            foreach (IPipelineStep Step in SelectableSteps)
            {
                bool IsStepSelected = SavedSelection.Contains(Step.StepId);
                PipelineStepItemViewModel Item = new PipelineStepItemViewModel(
                    Step.StepId,
                    Step.DisplayName,
                    Step.Description,
                    Step.ExecutionOrder,
                    IsStepSelected,
                    this.OnStepSelectionToggled);
                Item.IsSelectionEnabled = !this.IsBusyField;
                this.StepsField.Add(Item);
            }
        }
        finally
        {
            this.IsSyncingFromStateField = false;
        }
    }

    private void SyncQuickOptionsFromSettings()
    {
        this.IsSyncingFromStateField = true;
        try
        {
            PipelineSettings CurrentSettings = this.AppStateField.CurrentSettings;

            if (CurrentSettings.StartDirection == ShakeStartDirection.Horizontal)
            {
                this.SelectedStartDirectionOption = StartDirectionHorizontalLabel;
            }
            else
            {
                this.SelectedStartDirectionOption = StartDirectionVerticalLabel;
            }

            if (CurrentSettings.BlurZoomStart == BlurZoomStartPreset.BlurZoomOut)
            {
                this.SelectedBlurZoomStartOption = BlurZoomOutLabel;
            }
            else
            {
                this.SelectedBlurZoomStartOption = BlurZoomInLabel;
            }

            if (CurrentSettings.BeatDetection.Mode == BeatDetectionMode.UniversalFourZone)
            {
                this.SelectedBeatModeOption = BeatModeUniversalLabel;
            }
            else
            {
                this.SelectedBeatModeOption = BeatModeRedOverlayLabel;
            }

            this.UseSelectedOnly = CurrentSettings.UseSelectedOnly;
        }
        finally
        {
            this.IsSyncingFromStateField = false;
        }
    }

    private void OnStepSelectionToggled(PipelineStepItemViewModel ChangedItem)
    {
        if (this.IsSyncingFromStateField)
        {
            return;
        }

        this.IsSyncingFromStateField = true;
        try
        {
            List<string> CurrentSelectedIds = this.GetSelectedStepIds();
            IReadOnlyList<string> NormalizedIds;

            if (ChangedItem.IsSelected)
            {
                NormalizedIds = this.ValidatorField.NormalizeSelectionOnEnable(
                    ChangedItem.StepId,
                    CurrentSelectedIds,
                    this.AppStateField.LastPreflightState);
            }
            else
            {
                NormalizedIds = this.ValidatorField.NormalizeSelectionOnDisable(
                    ChangedItem.StepId,
                    CurrentSelectedIds,
                    this.AppStateField.LastPreflightState);
            }

            HashSet<string> NormalizedSet = new HashSet<string>(NormalizedIds, StringComparer.OrdinalIgnoreCase);
            foreach (PipelineStepItemViewModel Item in this.StepsField)
            {
                Item.SetSelectedWithoutCallback(NormalizedSet.Contains(Item.StepId));
            }

            this.AppStateField.CurrentSettings.SelectedStepIds = new List<string>(NormalizedIds);
        }
        finally
        {
            this.IsSyncingFromStateField = false;
        }

        this.RefreshStepBadges();
        this.NotifyCommandStates();
    }

    private List<string> GetSelectedStepIds()
    {
        List<string> SelectedIds = new List<string>();
        foreach (PipelineStepItemViewModel Item in this.StepsField)
        {
            if (Item.IsSelected)
            {
                SelectedIds.Add(Item.StepId);
            }
        }

        return SelectedIds;
    }

    private void RefreshStepBadges()
    {
        List<string> SelectedIds = this.GetSelectedStepIds();
        ProjectPreflightState PreflightState = this.AppStateField.LastPreflightState;
        PipelineSettings CurrentSettings = this.AppStateField.CurrentSettings;

        foreach (PipelineStepItemViewModel Item in this.StepsField)
        {
            string BadgeText = this.ValidatorField.GetDependencyBadgeText(
                Item.StepId,
                SelectedIds,
                PreflightState,
                CurrentSettings);
            Item.UpdateBadge(BadgeText);
        }
    }

    private void UpdateStepSelectionEnabledState()
    {
        bool CanEdit = !this.IsBusyField;
        foreach (PipelineStepItemViewModel Item in this.StepsField)
        {
            Item.IsSelectionEnabled = CanEdit;
        }
    }

    private void ApplyPreflightStateToBanner(ProjectPreflightState State)
    {
        this.IsAfterEffectsConnected = State.IsAfterEffectsRunning;
        if (State.IsAfterEffectsRunning)
        {
            this.ConnectionStatusText = "AE 2026 verbunden";
        }
        else
        {
            this.ConnectionStatusText = "AE 2026 nicht verbunden";
        }

        this.ProjectFullPathText = State.ProjectPath;
        if (!string.IsNullOrWhiteSpace(State.ProjectPath))
        {
            this.ProjectNameText = Path.GetFileName(State.ProjectPath);
        }
        else
        {
            this.ProjectNameText = "Kein gespeichertes Projekt";
        }

        if (State.HasActiveComposition && !string.IsNullOrWhiteSpace(State.CompositionName))
        {
            this.CompositionNameText = State.CompositionName;
        }
        else
        {
            this.CompositionNameText = "Keine aktive Comp";
        }

        this.ClipCountText = $"{State.ValidClipCount} Clips";
        this.HasEditClipTags = State.HasEditClipTags;
        this.HasStage1Tags = State.HasStage1Tags;
        this.HasZoomGroupTags = State.HasZoomGroupTags;
        this.IsProjectUnsavedWarning = State.HasActiveComposition && !State.IsProjectSaved;

        List<string> Warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(State.ErrorMessage))
        {
            Warnings.Add(State.ErrorMessage);
        }

        if (State.HasActiveComposition && !State.IsProjectSaved)
        {
            Warnings.Add("Projekt ist nicht gespeichert (.aep).");
        }

        if (State.MissingPresets.Count > 0)
        {
            string MissingJoined = string.Join(", ", State.MissingPresets);
            Warnings.Add($"Fehlende Presets: {MissingJoined}");
        }

        if (Warnings.Count > 0)
        {
            this.HasPreflightWarning = true;
            this.PreflightWarningText = string.Join(" | ", Warnings);
        }
        else
        {
            this.HasPreflightWarning = false;
            this.PreflightWarningText = string.Empty;
        }
    }

    private bool CanRunPreflight()
    {
        return !this.IsBusyField;
    }

    [RelayCommand(CanExecute = nameof(CanRunPreflight))]
    private async Task RunPreflightAsync()
    {
        await this.AppStateField.EnsureInitializedAsync();

        this.IsBusy = true;
        this.HasRollbackStatus = false;
        this.RollbackStatusText = string.Empty;
        this.CurrentStepStatusText = "Preflight-Check läuft...";
        this.CurrentBatchStatusText = "Verbinde mit After Effects 2026...";
        this.AppendLog("Starte Preflight-Prüfung...", false, false);

        IProgress<PipelineProgressUpdate> ProgressReporter = new Progress<PipelineProgressUpdate>(
            (PipelineProgressUpdate Update) =>
            {
                this.RunOnMainThread(() =>
                {
                    this.ApplyProgressUpdate(Update);
                });
            });

        try
        {
            ProjectPreflightState ResultState = await this.OrchestratorField.RunPreflightAsync(
                this.AppStateField.CurrentSettings,
                ProgressReporter,
                CancellationToken.None);

            this.AppStateField.UpdatePreflightState(ResultState);

            this.RunOnMainThread(() =>
            {
                this.ApplyPreflightStateToBanner(ResultState);
                this.RefreshStepBadges();

                if (ResultState.IsReady && ResultState.MissingPresets.Count == 0)
                {
                    this.CurrentStepStatusText = "Preflight OK – bereit für Pipeline-Start";
                    this.CurrentBatchStatusText = $"Comp '{ResultState.CompositionName}' · {ResultState.ValidClipCount} Clips";
                    this.AppendLog(
                        $"Preflight OK: {ResultState.CompositionName} ({ResultState.ValidClipCount} Clips).",
                        false,
                        true);
                }
                else
                {
                    this.CurrentStepStatusText = "Preflight mit Hinweisen/Fehlern beendet";
                    this.CurrentBatchStatusText = ResultState.ErrorMessage;
                    string LogDetail = string.IsNullOrWhiteSpace(ResultState.ErrorMessage)
                        ? this.PreflightWarningTextField
                        : ResultState.ErrorMessage;
                    this.AppendLog($"Preflight-Warnung: {LogDetail}", true, false);
                }
            });
        }
        catch (Exception Ex)
        {
            this.RunOnMainThread(() =>
            {
                this.CurrentStepStatusText = "Preflight fehlgeschlagen";
                this.CurrentBatchStatusText = Ex.Message;
                this.AppendLog($"Preflight-Fehler: {Ex.Message}", true, false);
            });
        }
        finally
        {
            this.RunOnMainThread(() =>
            {
                this.IsBusy = false;
            });
        }
    }

    private bool CanStartPipeline()
    {
        if (this.IsBusyField)
        {
            return false;
        }

        foreach (PipelineStepItemViewModel Item in this.StepsField)
        {
            if (Item.IsSelected)
            {
                return true;
            }
        }

        return false;
    }

    [RelayCommand(CanExecute = nameof(CanStartPipeline))]
    private async Task StartPipelineAsync()
    {
        await this.AppStateField.EnsureInitializedAsync();

        List<string> SelectedStepIds = this.GetSelectedStepIds();
        ValidationResult PreCheck = this.ValidatorField.ValidateSelection(
            SelectedStepIds,
            this.AppStateField.CurrentSettings,
            this.AppStateField.LastPreflightState);

        if (!PreCheck.IsValid)
        {
            this.RunOnMainThread(() =>
            {
                foreach (string ErrorText in PreCheck.Errors)
                {
                    this.AppendLog($"Validierung: {ErrorText}", true, false);
                }

                if (PreCheck.Errors.Count > 0)
                {
                    this.CurrentStepStatusText = PreCheck.Errors[0];
                }
            });
            return;
        }

        foreach (string WarningText in PreCheck.Warnings)
        {
            this.AppendLog($"Hinweis: {WarningText}", false, false);
        }

        this.AppStateField.CurrentSettings.SelectedStepIds = SelectedStepIds;
        try
        {
            await this.AppStateField.SaveSettingsAsync();
        }
        catch (Exception SaveEx)
        {
            this.AppendLog($"Warnung beim Speichern der Einstellungen: {SaveEx.Message}", true, false);
        }

        CancellationTokenSource RunCts = new CancellationTokenSource();
        this.ActiveCancellationTokenSourceField = RunCts;

        this.IsBusy = true;
        this.IsPipelineRunning = true;
        this.IsCancellationRequested = false;
        this.HasRollbackStatus = false;
        this.IsRollbackError = false;
        this.RollbackStatusText = string.Empty;
        this.ProgressRatio = 0.0;
        this.ProgressPercentText = "0 %";
        this.CurrentStepStatusText = "Starte Pipeline-Transaktion...";
        this.CurrentBatchStatusText = $"{SelectedStepIds.Count} Schritte gewählt";
        this.AppendLog($"Pipeline gestartet ({SelectedStepIds.Count} Schritte).", false, false);

        IProgress<PipelineProgressUpdate> ProgressReporter = new Progress<PipelineProgressUpdate>(
            (PipelineProgressUpdate Update) =>
            {
                this.RunOnMainThread(() =>
                {
                    this.ApplyProgressUpdate(Update);
                });
            });

        try
        {
            PipelineExecutionResult Result = await this.OrchestratorField.ExecutePipelineAsync(
                this.AppStateField.CurrentSettings,
                SelectedStepIds,
                ProgressReporter,
                RunCts.Token);

            this.RunOnMainThread(() =>
            {
                this.HandlePipelineCompletion(Result);
            });
        }
        catch (Exception Ex)
        {
            this.RunOnMainThread(() =>
            {
                this.CurrentStepStatusText = "Unerwarteter Pipeline-Fehler";
                this.CurrentBatchStatusText = Ex.Message;
                this.HasRollbackStatus = true;
                this.IsRollbackError = true;
                this.RollbackStatusText = $"Ausnahme: {Ex.Message}";
                this.AppendLog($"Kritischer Fehler: {Ex.Message}", true, false);
            });
        }
        finally
        {
            RunCts.Dispose();
            this.ActiveCancellationTokenSourceField = null;

            this.RunOnMainThread(() =>
            {
                this.IsCancellationRequested = false;
                this.IsPipelineRunning = false;
                this.IsBusy = false;
            });
        }
    }

    private bool CanCancelAndRollback()
    {
        return this.IsPipelineRunningField && !this.IsCancellationRequestedField;
    }

    [RelayCommand(CanExecute = nameof(CanCancelAndRollback))]
    private void CancelAndRollback()
    {
        CancellationTokenSource? Cts = this.ActiveCancellationTokenSourceField;
        if (Cts == null || this.IsCancellationRequestedField)
        {
            return;
        }

        this.IsCancellationRequested = true;
        this.CurrentStepStatusText = "Abbruch angefordert – Rollback läuft...";
        this.HasRollbackStatus = true;
        this.IsRollbackError = false;
        this.RollbackStatusText = "Abbruch wird durchgeführt, Snapshot wird wiederhergestellt...";
        this.AppendLog("Benutzerabbruch angefordert – Snapshot-Rollback gestartet.", true, false);
        Cts.Cancel();
    }

    [RelayCommand]
    private void ClearLog()
    {
        this.LogEntriesField.Clear();
    }

    private void HandlePipelineCompletion(PipelineExecutionResult Result)
    {
        ProjectPreflightState? LatestState = null;
        foreach (StepExecutionResult StepResultItem in Result.StepResults)
        {
            if (StepResultItem.UpdatedPreflightState != null)
            {
                LatestState = StepResultItem.UpdatedPreflightState;
            }
        }

        if (LatestState != null)
        {
            this.AppStateField.UpdatePreflightState(LatestState);
            this.ApplyPreflightStateToBanner(LatestState);
            this.RefreshStepBadges();
        }

        if (Result.Succeeded)
        {
            this.ProgressRatio = 1.0;
            this.ProgressPercentText = "100 %";
            this.CurrentStepStatusText = "Pipeline erfolgreich abgeschlossen";
            this.CurrentBatchStatusText = $"Transaktion {Result.TransactionId} committed";
            this.HasRollbackStatus = false;
            this.IsRollbackError = false;
            this.RollbackStatusText = string.Empty;

            string SummaryMsg = string.IsNullOrWhiteSpace(Result.Message)
                ? $"Pipeline abgeschlossen (Transaktion {Result.TransactionId})."
                : Result.Message;
            this.AppendLog(SummaryMsg, false, true);
            return;
        }

        if (Result.WasCancelled)
        {
            this.CurrentStepStatusText = "Pipeline abgebrochen";
            this.HasRollbackStatus = true;

            if (Result.WasRolledBack)
            {
                this.IsRollbackError = false;
                this.RollbackStatusText = "Abbruch erfolgreich · Original-Snapshot wiederhergestellt";
                this.CurrentBatchStatusText = "Rollback abgeschlossen";
                this.AppendLog("Pipeline abgebrochen und Ausgangszustand per Rollback wiederhergestellt.", false, false);
            }
            else
            {
                this.IsRollbackError = true;
                this.RollbackStatusText = $"Abgebrochen · Rollback-Hinweis: {Result.ErrorMessage}";
                this.CurrentBatchStatusText = "Abbruch ohne vollständigen Rollback";
                this.AppendLog($"Abbruch ohne Rollback: {Result.ErrorMessage}", true, false);
            }

            return;
        }

        this.CurrentStepStatusText = "Pipeline fehlgeschlagen";
        this.HasRollbackStatus = true;

        if (Result.WasRolledBack)
        {
            this.IsRollbackError = false;
            this.RollbackStatusText = "Fehler erkannt · Automatischer Rollback erfolgreich";
            this.CurrentBatchStatusText = "Originalprojekt wiederhergestellt";
        }
        else
        {
            this.IsRollbackError = true;
            this.RollbackStatusText = "Fehler erkannt · Rollback fehlgeschlagen oder nicht gestartet";
            this.CurrentBatchStatusText = "Siehe Log / Recovery-Tab";
        }

        string ErrorInfo = string.IsNullOrWhiteSpace(Result.ErrorMessage)
            ? Result.Message
            : Result.ErrorMessage;
        this.AppendLog($"Pipeline-Fehler: {ErrorInfo}", true, false);
    }

    private void ApplyProgressUpdate(PipelineProgressUpdate Update)
    {
        double NormalizedRatio = Math.Clamp(Update.Percentage / 100.0, 0.0, 1.0);

        this.ProgressRatio = NormalizedRatio;
        int RoundedPercent = (int)Math.Round(NormalizedRatio * 100.0);
        this.ProgressPercentText = $"{RoundedPercent} %";

        if (!string.IsNullOrWhiteSpace(Update.StepDisplayName))
        {
            if (Update.TotalSteps > 0 && Update.CurrentStepNumber > 0)
            {
                this.CurrentStepStatusText = $"Schritt {Update.CurrentStepNumber}/{Update.TotalSteps}: {Update.StepDisplayName}";
            }
            else
            {
                this.CurrentStepStatusText = Update.StepDisplayName;
            }
        }

        if (Update.TotalClips > 0)
        {
            int BatchNumber = Update.CurrentBatchIndex + 1;
            this.CurrentBatchStatusText = $"Batch {BatchNumber} · Clip {Update.CurrentClipInBatch}/{Update.TotalClips}";
        }
        else if (!string.IsNullOrWhiteSpace(Update.Message))
        {
            this.CurrentBatchStatusText = Update.Message;
        }

        if (!string.IsNullOrWhiteSpace(Update.Message))
        {
            this.AppendLog(Update.Message, Update.IsError, false);
        }
    }

    private void AppendLog(string Message, bool IsError, bool IsSuccess)
    {
        string Timestamp = DateTime.Now.ToString("HH:mm:ss");
        PipelineLogEntry Entry = new PipelineLogEntry
        {
            TimestampText = Timestamp,
            Message = Message,
            IsError = IsError,
            IsSuccess = IsSuccess
        };

        this.LogEntriesField.Add(Entry);

        while (this.LogEntriesField.Count > 250)
        {
            this.LogEntriesField.RemoveAt(0);
        }
    }

    private void NotifyCommandStates()
    {
        this.RunPreflightCommand.NotifyCanExecuteChanged();
        this.StartPipelineCommand.NotifyCanExecuteChanged();
        this.CancelAndRollbackCommand.NotifyCanExecuteChanged();
    }

    private void RunOnMainThread(Action ActionToRun)
    {
        MainThread.BeginInvokeOnMainThread(ActionToRun);
    }

    #endregion
}
