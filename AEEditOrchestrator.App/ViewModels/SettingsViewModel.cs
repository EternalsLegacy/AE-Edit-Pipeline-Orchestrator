using System.Collections.ObjectModel;
using System.Globalization;
using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;

namespace AEEditOrchestrator.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    #region Constants

    private const string MarkerTargetCompLabel = "Komposition";
    private const string MarkerTargetLayerLabel = "Layer";

    #endregion

    #region Fields

    private readonly OrchestratorAppState AppStateField;
    private readonly ISettingsStore SettingsStoreField;

    private readonly ObservableCollection<string> ExcludedLayerNamesField = new ObservableCollection<string>();
    private readonly ObservableCollection<PresetValidationItemViewModel> PresetValidationItemsField = new ObservableCollection<PresetValidationItemViewModel>();
    private readonly IReadOnlyList<string> MarkerTargetOptionsField = new List<string>
    {
        MarkerTargetCompLabel,
        MarkerTargetLayerLabel
    };

    private bool IsBusyField = false;
    private bool IsSyncingField = false;
    private bool HasStatusMessageField = false;
    private bool IsStatusErrorField = false;
    private string StatusMessageField = string.Empty;

    private string AfterEffectsExecutablePathField = string.Empty;
    private string UserPresetsRootPathField = string.Empty;
    private string BatchSizeTextField = "10";
    private string WatchdogTimeoutSecondsTextField = "45";
    private string MaxBackupsToKeepTextField = "5";
    private bool SaveAfterBatchField = true;
    private bool PurgeAfterBatchField = true;

    private string NewExcludedLayerNameField = string.Empty;

    private string FadeInOutPresetNameField = "FadeInOut";
    private string VerticalShakePresetNameField = "VerticalShake";
    private string VerticalBlurPresetNameField = "VerticalBlur";
    private string HorizontalShakePresetNameField = "HorizontalShake";
    private string HorizontalBlurPresetNameField = "HorizontalBlur";
    private string BlurZoomInPresetNameField = "BlurZoomIn";
    private string BlurZoomOutPresetNameField = "BlurZoomOut";

    private string MotionTileWidthTextField = "370";
    private string MotionTileHeightTextField = "370";
    private string StartBlurFramesTextField = "10";
    private string EndBlur1LengthTextField = "200";
    private string EndBlur2LengthTextField = "80";
    private string VerticalBlurDirectionTextField = "90";
    private string HorizontalBlurDirectionTextField = "180";
    private bool AddMotionTileToBlurZoomField = false;

    private string BeatSensitivityTextField = "65";
    private string BeatMinFrameGapTextField = "2";
    private string SelectedMarkerTargetOptionField = MarkerTargetCompLabel;
    private bool BeatOnlyWorkAreaField = false;

    #endregion

    #region Properties

    public ObservableCollection<string> ExcludedLayerNames
    {
        get
        {
            return this.ExcludedLayerNamesField;
        }
    }

    public ObservableCollection<PresetValidationItemViewModel> PresetValidationItems
    {
        get
        {
            return this.PresetValidationItemsField;
        }
    }

    public IReadOnlyList<string> MarkerTargetOptions
    {
        get
        {
            return this.MarkerTargetOptionsField;
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
                this.SaveSettingsCommand.NotifyCanExecuteChanged();
                this.ValidatePresetsCommand.NotifyCanExecuteChanged();
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

    public bool HasStatusMessage
    {
        get
        {
            return this.HasStatusMessageField;
        }
        private set
        {
            this.SetProperty(ref this.HasStatusMessageField, value);
        }
    }

    public bool IsStatusError
    {
        get
        {
            return this.IsStatusErrorField;
        }
        private set
        {
            this.SetProperty(ref this.IsStatusErrorField, value);
        }
    }

    public string StatusMessage
    {
        get
        {
            return this.StatusMessageField;
        }
        private set
        {
            this.SetProperty(ref this.StatusMessageField, value ?? string.Empty);
        }
    }

    public string AfterEffectsExecutablePath
    {
        get
        {
            return this.AfterEffectsExecutablePathField;
        }
        set
        {
            this.SetProperty(ref this.AfterEffectsExecutablePathField, value ?? string.Empty);
        }
    }

    public string UserPresetsRootPath
    {
        get
        {
            return this.UserPresetsRootPathField;
        }
        set
        {
            this.SetProperty(ref this.UserPresetsRootPathField, value ?? string.Empty);
        }
    }

    public string BatchSizeText
    {
        get
        {
            return this.BatchSizeTextField;
        }
        set
        {
            this.SetProperty(ref this.BatchSizeTextField, value ?? string.Empty);
        }
    }

    public string WatchdogTimeoutSecondsText
    {
        get
        {
            return this.WatchdogTimeoutSecondsTextField;
        }
        set
        {
            this.SetProperty(ref this.WatchdogTimeoutSecondsTextField, value ?? string.Empty);
        }
    }

    public string MaxBackupsToKeepText
    {
        get
        {
            return this.MaxBackupsToKeepTextField;
        }
        set
        {
            this.SetProperty(ref this.MaxBackupsToKeepTextField, value ?? string.Empty);
        }
    }

    public bool SaveAfterBatch
    {
        get
        {
            return this.SaveAfterBatchField;
        }
        set
        {
            this.SetProperty(ref this.SaveAfterBatchField, value);
        }
    }

    public bool PurgeAfterBatch
    {
        get
        {
            return this.PurgeAfterBatchField;
        }
        set
        {
            this.SetProperty(ref this.PurgeAfterBatchField, value);
        }
    }

    public string NewExcludedLayerName
    {
        get
        {
            return this.NewExcludedLayerNameField;
        }
        set
        {
            this.SetProperty(ref this.NewExcludedLayerNameField, value ?? string.Empty);
        }
    }

    public string FadeInOutPresetName
    {
        get
        {
            return this.FadeInOutPresetNameField;
        }
        set
        {
            this.SetProperty(ref this.FadeInOutPresetNameField, value ?? string.Empty);
        }
    }

    public string VerticalShakePresetName
    {
        get
        {
            return this.VerticalShakePresetNameField;
        }
        set
        {
            this.SetProperty(ref this.VerticalShakePresetNameField, value ?? string.Empty);
        }
    }

    public string VerticalBlurPresetName
    {
        get
        {
            return this.VerticalBlurPresetNameField;
        }
        set
        {
            this.SetProperty(ref this.VerticalBlurPresetNameField, value ?? string.Empty);
        }
    }

    public string HorizontalShakePresetName
    {
        get
        {
            return this.HorizontalShakePresetNameField;
        }
        set
        {
            this.SetProperty(ref this.HorizontalShakePresetNameField, value ?? string.Empty);
        }
    }

    public string HorizontalBlurPresetName
    {
        get
        {
            return this.HorizontalBlurPresetNameField;
        }
        set
        {
            this.SetProperty(ref this.HorizontalBlurPresetNameField, value ?? string.Empty);
        }
    }

    public string BlurZoomInPresetName
    {
        get
        {
            return this.BlurZoomInPresetNameField;
        }
        set
        {
            this.SetProperty(ref this.BlurZoomInPresetNameField, value ?? string.Empty);
        }
    }

    public string BlurZoomOutPresetName
    {
        get
        {
            return this.BlurZoomOutPresetNameField;
        }
        set
        {
            this.SetProperty(ref this.BlurZoomOutPresetNameField, value ?? string.Empty);
        }
    }

    public string MotionTileWidthText
    {
        get
        {
            return this.MotionTileWidthTextField;
        }
        set
        {
            this.SetProperty(ref this.MotionTileWidthTextField, value ?? string.Empty);
        }
    }

    public string MotionTileHeightText
    {
        get
        {
            return this.MotionTileHeightTextField;
        }
        set
        {
            this.SetProperty(ref this.MotionTileHeightTextField, value ?? string.Empty);
        }
    }

    public string StartBlurFramesText
    {
        get
        {
            return this.StartBlurFramesTextField;
        }
        set
        {
            this.SetProperty(ref this.StartBlurFramesTextField, value ?? string.Empty);
        }
    }

    public string EndBlur1LengthText
    {
        get
        {
            return this.EndBlur1LengthTextField;
        }
        set
        {
            this.SetProperty(ref this.EndBlur1LengthTextField, value ?? string.Empty);
        }
    }

    public string EndBlur2LengthText
    {
        get
        {
            return this.EndBlur2LengthTextField;
        }
        set
        {
            this.SetProperty(ref this.EndBlur2LengthTextField, value ?? string.Empty);
        }
    }

    public string VerticalBlurDirectionText
    {
        get
        {
            return this.VerticalBlurDirectionTextField;
        }
        set
        {
            this.SetProperty(ref this.VerticalBlurDirectionTextField, value ?? string.Empty);
        }
    }

    public string HorizontalBlurDirectionText
    {
        get
        {
            return this.HorizontalBlurDirectionTextField;
        }
        set
        {
            this.SetProperty(ref this.HorizontalBlurDirectionTextField, value ?? string.Empty);
        }
    }

    public bool AddMotionTileToBlurZoom
    {
        get
        {
            return this.AddMotionTileToBlurZoomField;
        }
        set
        {
            this.SetProperty(ref this.AddMotionTileToBlurZoomField, value);
        }
    }

    public string BeatSensitivityText
    {
        get
        {
            return this.BeatSensitivityTextField;
        }
        set
        {
            this.SetProperty(ref this.BeatSensitivityTextField, value ?? string.Empty);
        }
    }

    public string BeatMinFrameGapText
    {
        get
        {
            return this.BeatMinFrameGapTextField;
        }
        set
        {
            this.SetProperty(ref this.BeatMinFrameGapTextField, value ?? string.Empty);
        }
    }

    public string SelectedMarkerTargetOption
    {
        get
        {
            return this.SelectedMarkerTargetOptionField;
        }
        set
        {
            this.SetProperty(ref this.SelectedMarkerTargetOptionField, value ?? MarkerTargetCompLabel);
        }
    }

    public bool BeatOnlyWorkArea
    {
        get
        {
            return this.BeatOnlyWorkAreaField;
        }
        set
        {
            this.SetProperty(ref this.BeatOnlyWorkAreaField, value);
        }
    }

    #endregion

    #region Constructors

    public SettingsViewModel(OrchestratorAppState AppState, ISettingsStore SettingsStore)
    {
        this.AppStateField = AppState;
        this.SettingsStoreField = SettingsStore;
        this.AppStateField.SettingsUpdated += this.OnAppStateSettingsUpdated;
    }

    #endregion

    #region Public Methods

    public async Task InitializeAsync()
    {
        await this.AppStateField.EnsureInitializedAsync();

        this.RunOnMainThread(() =>
        {
            this.LoadFieldsFromSettings(this.AppStateField.CurrentSettings);
        });
    }

    #endregion

    #region Private Methods

    private void OnAppStateSettingsUpdated(object? Sender, EventArgs Args)
    {
        if (this.IsSyncingField)
        {
            return;
        }

        this.RunOnMainThread(() =>
        {
            this.LoadFieldsFromSettings(this.AppStateField.CurrentSettings);
        });
    }

    private void LoadFieldsFromSettings(PipelineSettings Settings)
    {
        this.IsSyncingField = true;
        try
        {
            this.AfterEffectsExecutablePath = Settings.AfterEffectsExecutablePath;
            this.UserPresetsRootPath = Settings.UserPresetsRootPath;
            this.BatchSizeText = Settings.BatchSize.ToString(CultureInfo.InvariantCulture);
            this.WatchdogTimeoutSecondsText = Settings.WatchdogTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
            this.MaxBackupsToKeepText = Settings.MaxBackupsToKeep.ToString(CultureInfo.InvariantCulture);
            this.SaveAfterBatch = Settings.SaveAfterBatch;
            this.PurgeAfterBatch = Settings.PurgeAfterBatch;

            this.ExcludedLayerNamesField.Clear();
            foreach (string ExcludedName in Settings.ExcludedLayerNames)
            {
                this.ExcludedLayerNamesField.Add(ExcludedName);
            }

            this.FadeInOutPresetName = Settings.Presets.FadeInOut;
            this.VerticalShakePresetName = Settings.Presets.VerticalShake;
            this.VerticalBlurPresetName = Settings.Presets.VerticalBlur;
            this.HorizontalShakePresetName = Settings.Presets.HorizontalShake;
            this.HorizontalBlurPresetName = Settings.Presets.HorizontalBlur;
            this.BlurZoomInPresetName = Settings.Presets.BlurZoomIn;
            this.BlurZoomOutPresetName = Settings.Presets.BlurZoomOut;

            this.MotionTileWidthText = Settings.MotionTileWidth.ToString(CultureInfo.InvariantCulture);
            this.MotionTileHeightText = Settings.MotionTileHeight.ToString(CultureInfo.InvariantCulture);
            this.StartBlurFramesText = Settings.StartBlurFrames.ToString(CultureInfo.InvariantCulture);
            this.EndBlur1LengthText = Settings.EndBlur1Length.ToString(CultureInfo.InvariantCulture);
            this.EndBlur2LengthText = Settings.EndBlur2Length.ToString(CultureInfo.InvariantCulture);
            this.VerticalBlurDirectionText = Settings.VerticalBlurDirection.ToString(CultureInfo.InvariantCulture);
            this.HorizontalBlurDirectionText = Settings.HorizontalBlurDirection.ToString(CultureInfo.InvariantCulture);
            this.AddMotionTileToBlurZoom = Settings.AddMotionTileToBlurZoom;

            this.BeatSensitivityText = Settings.BeatDetection.Sensitivity.ToString("0.##", CultureInfo.InvariantCulture);
            this.BeatMinFrameGapText = Settings.BeatDetection.MinFrameGap.ToString(CultureInfo.InvariantCulture);

            if (Settings.BeatDetection.MarkerTarget == BeatMarkerTarget.Layer)
            {
                this.SelectedMarkerTargetOption = MarkerTargetLayerLabel;
            }
            else
            {
                this.SelectedMarkerTargetOption = MarkerTargetCompLabel;
            }

            this.BeatOnlyWorkArea = Settings.BeatDetection.OnlyWorkArea;
        }
        finally
        {
            this.IsSyncingField = false;
        }
    }

    private bool TryBuildSettingsSnapshot(out PipelineSettings BuiltSettings, out string ValidationError)
    {
        BuiltSettings = new PipelineSettings();
        ValidationError = string.Empty;

        if (!this.TryParsePositiveInt(this.BatchSizeTextField, "Batch-Größe", 1, 500, out int ParsedBatchSize, out ValidationError))
        {
            return false;
        }

        if (!this.TryParsePositiveInt(this.WatchdogTimeoutSecondsTextField, "Watchdog-Timeout (s)", 5, 3600, out int ParsedWatchdogTimeout, out ValidationError))
        {
            return false;
        }

        if (!this.TryParsePositiveInt(this.MaxBackupsToKeepTextField, "Max. Backups", 1, 100, out int ParsedMaxBackups, out ValidationError))
        {
            return false;
        }

        if (!this.TryParsePositiveInt(this.MotionTileWidthTextField, "Motion Tile Width", 100, 2000, out int ParsedMotionTileWidth, out ValidationError))
        {
            return false;
        }

        if (!this.TryParsePositiveInt(this.MotionTileHeightTextField, "Motion Tile Height", 100, 2000, out int ParsedMotionTileHeight, out ValidationError))
        {
            return false;
        }

        if (!this.TryParsePositiveInt(this.StartBlurFramesTextField, "StartBlur Frames", 1, 240, out int ParsedStartBlurFrames, out ValidationError))
        {
            return false;
        }

        if (!this.TryParsePositiveInt(this.EndBlur1LengthTextField, "EndBlur 1 Länge", 0, 2000, out int ParsedEndBlur1Length, out ValidationError))
        {
            return false;
        }

        if (!this.TryParsePositiveInt(this.EndBlur2LengthTextField, "EndBlur 2 Länge", 0, 2000, out int ParsedEndBlur2Length, out ValidationError))
        {
            return false;
        }

        if (!this.TryParseIntRange(this.VerticalBlurDirectionTextField, "Vertical Blur Winkel", -360, 360, out int ParsedVerticalDir, out ValidationError))
        {
            return false;
        }

        if (!this.TryParseIntRange(this.HorizontalBlurDirectionTextField, "Horizontal Blur Winkel", -360, 360, out int ParsedHorizontalDir, out ValidationError))
        {
            return false;
        }

        if (!this.TryParsePositiveDouble(this.BeatSensitivityTextField, "Empfindlichkeit (10–100)", 10.0, 100.0, out double ParsedSensitivity, out ValidationError))
        {
            return false;
        }

        if (!this.TryParsePositiveInt(this.BeatMinFrameGapTextField, "MinFrameGap", 0, 300, out int ParsedMinFrameGap, out ValidationError))
        {
            return false;
        }

        PipelineSettings Existing = this.AppStateField.CurrentSettings;

        BuiltSettings.AfterEffectsExecutablePath = this.AfterEffectsExecutablePathField.Trim();
        BuiltSettings.UserPresetsRootPath = this.UserPresetsRootPathField.Trim();
        BuiltSettings.BatchSize = ParsedBatchSize;
        BuiltSettings.WatchdogTimeoutSeconds = ParsedWatchdogTimeout;
        BuiltSettings.MaxBackupsToKeep = ParsedMaxBackups;
        BuiltSettings.SaveAfterBatch = this.SaveAfterBatchField;
        BuiltSettings.PurgeAfterBatch = this.PurgeAfterBatchField;
        BuiltSettings.ExcludedLayerNames = new List<string>(this.ExcludedLayerNamesField);
        BuiltSettings.UseSelectedOnly = Existing.UseSelectedOnly;

        BuiltSettings.Presets = new PresetMappingSettings
        {
            FadeInOut = this.FadeInOutPresetNameField.Trim(),
            VerticalShake = this.VerticalShakePresetNameField.Trim(),
            VerticalBlur = this.VerticalBlurPresetNameField.Trim(),
            HorizontalShake = this.HorizontalShakePresetNameField.Trim(),
            HorizontalBlur = this.HorizontalBlurPresetNameField.Trim(),
            BlurZoomIn = this.BlurZoomInPresetNameField.Trim(),
            BlurZoomOut = this.BlurZoomOutPresetNameField.Trim()
        };

        BuiltSettings.MotionTileWidth = ParsedMotionTileWidth;
        BuiltSettings.MotionTileHeight = ParsedMotionTileHeight;
        BuiltSettings.StartBlurFrames = ParsedStartBlurFrames;
        BuiltSettings.EndBlur1Length = ParsedEndBlur1Length;
        BuiltSettings.EndBlur2Length = ParsedEndBlur2Length;
        BuiltSettings.VerticalBlurDirection = ParsedVerticalDir;
        BuiltSettings.HorizontalBlurDirection = ParsedHorizontalDir;
        BuiltSettings.StartDirection = Existing.StartDirection;
        BuiltSettings.BlurZoomStart = Existing.BlurZoomStart;
        BuiltSettings.AddMotionTileToBlurZoom = this.AddMotionTileToBlurZoomField;

        BeatMarkerTarget SelectedTarget = BeatMarkerTarget.Composition;
        if (string.Equals(this.SelectedMarkerTargetOptionField, MarkerTargetLayerLabel, StringComparison.Ordinal))
        {
            SelectedTarget = BeatMarkerTarget.Layer;
        }

        BuiltSettings.BeatDetection = new BeatDetectionSettings
        {
            Mode = Existing.BeatDetection.Mode,
            Sensitivity = ParsedSensitivity,
            MinFrameGap = ParsedMinFrameGap,
            MarkerTarget = SelectedTarget,
            OnlyWorkArea = this.BeatOnlyWorkAreaField,
            SampleXRatio = Existing.BeatDetection.SampleXRatio,
            SampleYRatio = Existing.BeatDetection.SampleYRatio,
            SampleRadius = Existing.BeatDetection.SampleRadius
        };

        BuiltSettings.ReverseMode = Existing.ReverseMode;
        BuiltSettings.SelectedStepIds = new List<string>(Existing.SelectedStepIds);

        return true;
    }

    private bool TryParsePositiveInt(
        string RawValue,
        string FieldName,
        int MinValue,
        int MaxValue,
        out int ParsedValue,
        out string ErrorMessage)
    {
        return this.TryParseIntRange(RawValue, FieldName, MinValue, MaxValue, out ParsedValue, out ErrorMessage);
    }

    private bool TryParseIntRange(
        string RawValue,
        string FieldName,
        int MinValue,
        int MaxValue,
        out int ParsedValue,
        out string ErrorMessage)
    {
        if (!int.TryParse(RawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ParsedValue))
        {
            ErrorMessage = $"'{FieldName}' muss eine gültige Ganzzahl sein.";
            return false;
        }

        if (ParsedValue < MinValue || ParsedValue > MaxValue)
        {
            ErrorMessage = $"'{FieldName}' muss zwischen {MinValue} und {MaxValue} liegen.";
            return false;
        }

        ErrorMessage = string.Empty;
        return true;
    }

    private bool TryParsePositiveDouble(
        string RawValue,
        string FieldName,
        double MinValue,
        double MaxValue,
        out double ParsedValue,
        out string ErrorMessage)
    {
        string Normalized = RawValue.Trim().Replace(',', '.');
        if (!double.TryParse(Normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out ParsedValue))
        {
            ErrorMessage = $"'{FieldName}' muss eine gültige Zahl sein.";
            return false;
        }

        if (ParsedValue < MinValue || ParsedValue > MaxValue)
        {
            ErrorMessage = $"'{FieldName}' muss zwischen {MinValue} und {MaxValue} liegen.";
            return false;
        }

        ErrorMessage = string.Empty;
        return true;
    }

    [RelayCommand]
    private void DetectAfterEffectsPath()
    {
        string? DetectedPath = this.SettingsStoreField.DetectAfterEffectsExecutablePath();
        if (!string.IsNullOrWhiteSpace(DetectedPath))
        {
            this.AfterEffectsExecutablePath = DetectedPath;
            this.SetStatus($"AfterFX.exe erkannt: {DetectedPath}", false);
        }
        else
        {
            this.SetStatus("AfterFX.exe wurde nicht automatisch gefunden. Bitte Pfad manuell eingeben.", true);
        }
    }

    [RelayCommand]
    private void DetectUserPresetsPath()
    {
        string? DetectedPath = this.SettingsStoreField.DetectUserPresetsFolderPath();
        if (!string.IsNullOrWhiteSpace(DetectedPath))
        {
            this.UserPresetsRootPath = DetectedPath;
            this.SetStatus($"User Presets erkannt: {DetectedPath}", false);
        }
        else
        {
            this.SetStatus("User Presets-Ordner wurde nicht automatisch gefunden. Bitte Pfad manuell eingeben.", true);
        }
    }

    [RelayCommand]
    private void AddExcludedLayer()
    {
        string TrimmedName = this.NewExcludedLayerNameField.Trim();
        if (string.IsNullOrWhiteSpace(TrimmedName))
        {
            return;
        }

        foreach (string ExistingName in this.ExcludedLayerNamesField)
        {
            if (string.Equals(ExistingName, TrimmedName, StringComparison.OrdinalIgnoreCase))
            {
                this.NewExcludedLayerName = string.Empty;
                return;
            }
        }

        this.ExcludedLayerNamesField.Add(TrimmedName);
        this.NewExcludedLayerName = string.Empty;
    }

    [RelayCommand]
    private void RemoveExcludedLayer(string? LayerName)
    {
        if (string.IsNullOrWhiteSpace(LayerName))
        {
            return;
        }

        this.ExcludedLayerNamesField.Remove(LayerName);
    }

    private bool CanValidatePresets()
    {
        return !this.IsBusyField;
    }

    [RelayCommand(CanExecute = nameof(CanValidatePresets))]
    private void ValidatePresets()
    {
        PipelineSettings DraftSettings = new PipelineSettings
        {
            UserPresetsRootPath = this.UserPresetsRootPathField.Trim(),
            Presets = new PresetMappingSettings
            {
                FadeInOut = this.FadeInOutPresetNameField.Trim(),
                VerticalShake = this.VerticalShakePresetNameField.Trim(),
                VerticalBlur = this.VerticalBlurPresetNameField.Trim(),
                HorizontalShake = this.HorizontalShakePresetNameField.Trim(),
                HorizontalBlur = this.HorizontalBlurPresetNameField.Trim(),
                BlurZoomIn = this.BlurZoomInPresetNameField.Trim(),
                BlurZoomOut = this.BlurZoomOutPresetNameField.Trim()
            }
        };

        IReadOnlyList<PresetValidationItem> Results = this.SettingsStoreField.ValidatePresetsOnDisk(DraftSettings);
        this.PresetValidationItemsField.Clear();

        int MissingCount = 0;
        foreach (PresetValidationItem Item in Results)
        {
            if (!Item.IsFound)
            {
                MissingCount++;
            }

            this.PresetValidationItemsField.Add(new PresetValidationItemViewModel
            {
                PresetKey = Item.PresetKey,
                PresetName = Item.PresetName,
                IsFound = Item.IsFound,
                FullPath = Item.FullPath
            });
        }

        if (MissingCount == 0)
        {
            this.SetStatus($"Alle {Results.Count} .ffx-Presets im Ordner gefunden.", false);
        }
        else
        {
            this.SetStatus($"{MissingCount} von {Results.Count} Presets nicht gefunden.", true);
        }
    }

    private bool CanSaveSettings()
    {
        return !this.IsBusyField;
    }

    [RelayCommand(CanExecute = nameof(CanSaveSettings))]
    private async Task SaveSettingsAsync()
    {
        if (!this.TryBuildSettingsSnapshot(out PipelineSettings BuiltSettings, out string ValidationError))
        {
            this.SetStatus(ValidationError, true);
            return;
        }

        this.IsBusy = true;
        try
        {
            this.IsSyncingField = true;
            this.AppStateField.CurrentSettings = BuiltSettings;
            await this.AppStateField.SaveSettingsAsync();

            this.RunOnMainThread(() =>
            {
                this.SetStatus("Einstellungen gespeichert und für Pipeline übernommen.", false);
            });
        }
        catch (Exception Ex)
        {
            this.RunOnMainThread(() =>
            {
                this.SetStatus($"Speichern fehlgeschlagen: {Ex.Message}", true);
            });
        }
        finally
        {
            this.IsSyncingField = false;
            this.RunOnMainThread(() =>
            {
                this.IsBusy = false;
            });
        }
    }

    private void SetStatus(string Message, bool IsError)
    {
        this.StatusMessage = Message;
        this.IsStatusError = IsError;
        this.HasStatusMessage = !string.IsNullOrWhiteSpace(Message);
    }

    private void RunOnMainThread(Action ActionToRun)
    {
        MainThread.BeginInvokeOnMainThread(ActionToRun);
    }

    #endregion
}
