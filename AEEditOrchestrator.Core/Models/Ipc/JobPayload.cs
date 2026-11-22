using System.Text.Json.Serialization;

namespace AEEditOrchestrator.Core.Models.Ipc;

public sealed class JobPayload
{
    #region Fields

    private string JobIdField = string.Empty;
    private string ScriptFileField = string.Empty;
    private bool UseSelectedOnlyField = false;
    private int BatchStartIndexField = 0;
    private int BatchSizeField = 10;
    private bool SaveAfterBatchField = true;
    private bool PurgeAfterBatchField = true;
    private List<string> RequiredPresetsField = new List<string>();
    private JobSettingsPayload SettingsField = new JobSettingsPayload();

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

    [JsonPropertyName("scriptFile")]
    public string ScriptFile
    {
        get
        {
            return this.ScriptFileField;
        }
        set
        {
            this.ScriptFileField = value ?? string.Empty;
        }
    }

    [JsonPropertyName("useSelectedOnly")]
    public bool UseSelectedOnly
    {
        get
        {
            return this.UseSelectedOnlyField;
        }
        set
        {
            this.UseSelectedOnlyField = value;
        }
    }

    [JsonPropertyName("batchStartIndex")]
    public int BatchStartIndex
    {
        get
        {
            return this.BatchStartIndexField;
        }
        set
        {
            this.BatchStartIndexField = value;
        }
    }

    [JsonPropertyName("batchSize")]
    public int BatchSize
    {
        get
        {
            return this.BatchSizeField;
        }
        set
        {
            this.BatchSizeField = value;
        }
    }

    [JsonPropertyName("saveAfterBatch")]
    public bool SaveAfterBatch
    {
        get
        {
            return this.SaveAfterBatchField;
        }
        set
        {
            this.SaveAfterBatchField = value;
        }
    }

    [JsonPropertyName("purgeAfterBatch")]
    public bool PurgeAfterBatch
    {
        get
        {
            return this.PurgeAfterBatchField;
        }
        set
        {
            this.PurgeAfterBatchField = value;
        }
    }

    [JsonPropertyName("requiredPresets")]
    public List<string> RequiredPresets
    {
        get
        {
            return this.RequiredPresetsField;
        }
        set
        {
            this.RequiredPresetsField = value ?? new List<string>();
        }
    }

    [JsonPropertyName("settings")]
    public JobSettingsPayload Settings
    {
        get
        {
            return this.SettingsField;
        }
        set
        {
            this.SettingsField = value ?? new JobSettingsPayload();
        }
    }

    #endregion

    #region Public Methods

    public static JobPayload FromSettings(
        string JobId,
        string ScriptFile,
        PipelineSettings Settings,
        int BatchStartIndex,
        IReadOnlyList<string>? RequiredPresets = null,
        bool? SaveAfterBatchOverride = null,
        bool? PurgeAfterBatchOverride = null,
        string? ProjectToOpen = null,
        ReverseRollbackMode? ReverseModeOverride = null)
    {
        List<string> LowerExcluded = new List<string>();
        foreach (string Name in Settings.ExcludedLayerNames)
        {
            if (!string.IsNullOrWhiteSpace(Name))
            {
                LowerExcluded.Add(Name.Trim().ToLowerInvariant());
            }
        }

        string DirectionString = "Vertical";
        if (Settings.StartDirection == ShakeStartDirection.Horizontal)
        {
            DirectionString = "Horizontal";
        }

        string BlurZoomStartString = "BlurZoomIn";
        if (Settings.BlurZoomStart == BlurZoomStartPreset.BlurZoomOut)
        {
            BlurZoomStartString = "BlurZoomOut";
        }

        string BeatModeString = "UniversalFourZone";
        if (Settings.BeatDetection.Mode == BeatDetectionMode.RedOverlay)
        {
            BeatModeString = "RedOverlay";
        }

        string MarkerTargetString = "Composition";
        if (Settings.BeatDetection.MarkerTarget == BeatMarkerTarget.Layer)
        {
            MarkerTargetString = "Layer";
        }

        ReverseRollbackMode EffectiveReverseMode = Settings.ReverseMode;
        if (ReverseModeOverride.HasValue)
        {
            EffectiveReverseMode = ReverseModeOverride.Value;
        }

        string ReverseModeString = "Stage2Only";
        if (EffectiveReverseMode == ReverseRollbackMode.Stage1Only)
        {
            ReverseModeString = "Stage1Only";
        }
        else if (EffectiveReverseMode == ReverseRollbackMode.Full)
        {
            ReverseModeString = "Full";
        }

        bool EffectiveSave = Settings.SaveAfterBatch;
        if (SaveAfterBatchOverride.HasValue)
        {
            EffectiveSave = SaveAfterBatchOverride.Value;
        }

        bool EffectivePurge = Settings.PurgeAfterBatch;
        if (PurgeAfterBatchOverride.HasValue)
        {
            EffectivePurge = PurgeAfterBatchOverride.Value;
        }

        List<string> PresetList = new List<string>();
        if (RequiredPresets != null)
        {
            foreach (string PresetName in RequiredPresets)
            {
                if (!string.IsNullOrWhiteSpace(PresetName))
                {
                    PresetList.Add(PresetName);
                }
            }
        }

        JobPayload Payload = new JobPayload
        {
            JobId = JobId,
            ScriptFile = ScriptFile,
            UseSelectedOnly = Settings.UseSelectedOnly,
            BatchStartIndex = BatchStartIndex,
            BatchSize = Settings.BatchSize,
            SaveAfterBatch = EffectiveSave,
            PurgeAfterBatch = EffectivePurge,
            RequiredPresets = PresetList,
            Settings = new JobSettingsPayload
            {
                PresetsRoot = Settings.UserPresetsRootPath,
                ExcludedNames = LowerExcluded,
                StartDirection = DirectionString,
                BlurZoomStart = BlurZoomStartString,
                AddMotionTileToBlurZoom = Settings.AddMotionTileToBlurZoom,
                MotionTileWidth = Settings.MotionTileWidth,
                MotionTileHeight = Settings.MotionTileHeight,
                StartBlurFrames = Settings.StartBlurFrames,
                EndBlur1Length = Settings.EndBlur1Length,
                EndBlur2Length = Settings.EndBlur2Length,
                VerticalBlurDir = Settings.VerticalBlurDirection,
                HorizontalBlurDir = Settings.HorizontalBlurDirection,
                ReverseMode = ReverseModeString,
                ProjectToOpen = ProjectToOpen ?? string.Empty,
                Presets = new JobPresetNamesPayload
                {
                    FadeInOut = Settings.Presets.FadeInOut,
                    VerticalShake = Settings.Presets.VerticalShake,
                    VerticalBlur = Settings.Presets.VerticalBlur,
                    HorizontalShake = Settings.Presets.HorizontalShake,
                    HorizontalBlur = Settings.Presets.HorizontalBlur,
                    BlurZoomIn = Settings.Presets.BlurZoomIn,
                    BlurZoomOut = Settings.Presets.BlurZoomOut
                },
                BeatDetection = new JobBeatDetectionPayload
                {
                    Mode = BeatModeString,
                    Sensitivity = Settings.BeatDetection.Sensitivity,
                    MinFrameGap = Settings.BeatDetection.MinFrameGap,
                    MarkerTarget = MarkerTargetString,
                    OnlyWorkArea = Settings.BeatDetection.OnlyWorkArea,
                    SampleXRatio = Settings.BeatDetection.SampleXRatio,
                    SampleYRatio = Settings.BeatDetection.SampleYRatio,
                    SampleRadius = Settings.BeatDetection.SampleRadius
                }
            }
        };

        return Payload;
    }

    #endregion
}

public sealed class JobSettingsPayload
{
    #region Fields

    private string PresetsRootField = string.Empty;
    private List<string> ExcludedNamesField = new List<string>();
    private string StartDirectionField = "Vertical";
    private string BlurZoomStartField = "BlurZoomIn";
    private bool AddMotionTileToBlurZoomField = false;
    private int MotionTileWidthField = 370;
    private int MotionTileHeightField = 370;
    private int StartBlurFramesField = 10;
    private int EndBlur1LengthField = 200;
    private int EndBlur2LengthField = 80;
    private int VerticalBlurDirField = 90;
    private int HorizontalBlurDirField = 180;
    private JobPresetNamesPayload PresetsField = new JobPresetNamesPayload();
    private JobBeatDetectionPayload BeatDetectionField = new JobBeatDetectionPayload();
    private string ReverseModeField = "Stage2Only";
    private string ProjectToOpenField = string.Empty;

    #endregion

    #region Properties

    [JsonPropertyName("presetsRoot")]
    public string PresetsRoot
    {
        get
        {
            return this.PresetsRootField;
        }
        set
        {
            this.PresetsRootField = value ?? string.Empty;
        }
    }

    [JsonPropertyName("excludedNames")]
    public List<string> ExcludedNames
    {
        get
        {
            return this.ExcludedNamesField;
        }
        set
        {
            this.ExcludedNamesField = value ?? new List<string>();
        }
    }

    [JsonPropertyName("startDirection")]
    public string StartDirection
    {
        get
        {
            return this.StartDirectionField;
        }
        set
        {
            this.StartDirectionField = value ?? "Vertical";
        }
    }

    [JsonPropertyName("blurZoomStart")]
    public string BlurZoomStart
    {
        get
        {
            return this.BlurZoomStartField;
        }
        set
        {
            this.BlurZoomStartField = value ?? "BlurZoomIn";
        }
    }

    [JsonPropertyName("addMotionTileToBlurZoom")]
    public bool AddMotionTileToBlurZoom
    {
        get
        {
            return this.AddMotionTileToBlurZoomField;
        }
        set
        {
            this.AddMotionTileToBlurZoomField = value;
        }
    }

    [JsonPropertyName("motionTileWidth")]
    public int MotionTileWidth
    {
        get
        {
            return this.MotionTileWidthField;
        }
        set
        {
            this.MotionTileWidthField = value;
        }
    }

    [JsonPropertyName("motionTileHeight")]
    public int MotionTileHeight
    {
        get
        {
            return this.MotionTileHeightField;
        }
        set
        {
            this.MotionTileHeightField = value;
        }
    }

    [JsonPropertyName("startBlurFrames")]
    public int StartBlurFrames
    {
        get
        {
            return this.StartBlurFramesField;
        }
        set
        {
            this.StartBlurFramesField = value;
        }
    }

    [JsonPropertyName("endBlur1Length")]
    public int EndBlur1Length
    {
        get
        {
            return this.EndBlur1LengthField;
        }
        set
        {
            this.EndBlur1LengthField = value;
        }
    }

    [JsonPropertyName("endBlur2Length")]
    public int EndBlur2Length
    {
        get
        {
            return this.EndBlur2LengthField;
        }
        set
        {
            this.EndBlur2LengthField = value;
        }
    }

    [JsonPropertyName("verticalBlurDir")]
    public int VerticalBlurDir
    {
        get
        {
            return this.VerticalBlurDirField;
        }
        set
        {
            this.VerticalBlurDirField = value;
        }
    }

    [JsonPropertyName("horizontalBlurDir")]
    public int HorizontalBlurDir
    {
        get
        {
            return this.HorizontalBlurDirField;
        }
        set
        {
            this.HorizontalBlurDirField = value;
        }
    }

    [JsonPropertyName("presets")]
    public JobPresetNamesPayload Presets
    {
        get
        {
            return this.PresetsField;
        }
        set
        {
            this.PresetsField = value ?? new JobPresetNamesPayload();
        }
    }

    [JsonPropertyName("beatDetection")]
    public JobBeatDetectionPayload BeatDetection
    {
        get
        {
            return this.BeatDetectionField;
        }
        set
        {
            this.BeatDetectionField = value ?? new JobBeatDetectionPayload();
        }
    }

    [JsonPropertyName("reverseMode")]
    public string ReverseMode
    {
        get
        {
            return this.ReverseModeField;
        }
        set
        {
            this.ReverseModeField = value ?? "Stage2Only";
        }
    }

    [JsonPropertyName("projectToOpen")]
    public string ProjectToOpen
    {
        get
        {
            return this.ProjectToOpenField;
        }
        set
        {
            this.ProjectToOpenField = value ?? string.Empty;
        }
    }

    #endregion
}

public sealed class JobPresetNamesPayload
{
    #region Fields

    private string FadeInOutField = "FadeInOut";
    private string VerticalShakeField = "VerticalShake";
    private string VerticalBlurField = "VerticalBlur";
    private string HorizontalShakeField = "HorizontalShake";
    private string HorizontalBlurField = "HorizontalBlur";
    private string BlurZoomInField = "BlurZoomIn";
    private string BlurZoomOutField = "BlurZoomOut";

    #endregion

    #region Properties

    [JsonPropertyName("fadeInOut")]
    public string FadeInOut
    {
        get
        {
            return this.FadeInOutField;
        }
        set
        {
            this.FadeInOutField = value ?? string.Empty;
        }
    }

    [JsonPropertyName("verticalShake")]
    public string VerticalShake
    {
        get
        {
            return this.VerticalShakeField;
        }
        set
        {
            this.VerticalShakeField = value ?? string.Empty;
        }
    }

    [JsonPropertyName("verticalBlur")]
    public string VerticalBlur
    {
        get
        {
            return this.VerticalBlurField;
        }
        set
        {
            this.VerticalBlurField = value ?? string.Empty;
        }
    }

    [JsonPropertyName("horizontalShake")]
    public string HorizontalShake
    {
        get
        {
            return this.HorizontalShakeField;
        }
        set
        {
            this.HorizontalShakeField = value ?? string.Empty;
        }
    }

    [JsonPropertyName("horizontalBlur")]
    public string HorizontalBlur
    {
        get
        {
            return this.HorizontalBlurField;
        }
        set
        {
            this.HorizontalBlurField = value ?? string.Empty;
        }
    }

    [JsonPropertyName("blurZoomIn")]
    public string BlurZoomIn
    {
        get
        {
            return this.BlurZoomInField;
        }
        set
        {
            this.BlurZoomInField = value ?? string.Empty;
        }
    }

    [JsonPropertyName("blurZoomOut")]
    public string BlurZoomOut
    {
        get
        {
            return this.BlurZoomOutField;
        }
        set
        {
            this.BlurZoomOutField = value ?? string.Empty;
        }
    }

    #endregion
}

public sealed class JobBeatDetectionPayload
{
    #region Fields

    private string ModeField = "UniversalFourZone";
    private double SensitivityField = 65.0;
    private int MinFrameGapField = 2;
    private string MarkerTargetField = "Composition";
    private bool OnlyWorkAreaField = false;
    private double SampleXRatioField = 0.85;
    private double SampleYRatioField = 0.15;
    private int SampleRadiusField = 15;

    #endregion

    #region Properties

    [JsonPropertyName("mode")]
    public string Mode
    {
        get
        {
            return this.ModeField;
        }
        set
        {
            this.ModeField = value ?? "UniversalFourZone";
        }
    }

    [JsonPropertyName("sensitivity")]
    public double Sensitivity
    {
        get
        {
            return this.SensitivityField;
        }
        set
        {
            this.SensitivityField = value;
        }
    }

    [JsonPropertyName("minFrameGap")]
    public int MinFrameGap
    {
        get
        {
            return this.MinFrameGapField;
        }
        set
        {
            this.MinFrameGapField = value;
        }
    }

    [JsonPropertyName("markerTarget")]
    public string MarkerTarget
    {
        get
        {
            return this.MarkerTargetField;
        }
        set
        {
            this.MarkerTargetField = value ?? "Composition";
        }
    }

    [JsonPropertyName("onlyWorkArea")]
    public bool OnlyWorkArea
    {
        get
        {
            return this.OnlyWorkAreaField;
        }
        set
        {
            this.OnlyWorkAreaField = value;
        }
    }

    [JsonPropertyName("sampleXRatio")]
    public double SampleXRatio
    {
        get
        {
            return this.SampleXRatioField;
        }
        set
        {
            this.SampleXRatioField = value;
        }
    }

    [JsonPropertyName("sampleYRatio")]
    public double SampleYRatio
    {
        get
        {
            return this.SampleYRatioField;
        }
        set
        {
            this.SampleYRatioField = value;
        }
    }

    [JsonPropertyName("sampleRadius")]
    public int SampleRadius
    {
        get
        {
            return this.SampleRadiusField;
        }
        set
        {
            this.SampleRadiusField = value;
        }
    }

    #endregion
}
