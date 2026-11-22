namespace AEEditOrchestrator.Core.Models;

public sealed class PipelineSettings
{
    #region Fields

    private string AfterEffectsExecutablePathField = string.Empty;
    private string UserPresetsRootPathField = string.Empty;
    private int BatchSizeField = 10;
    private int WatchdogTimeoutSecondsField = 45;
    private int MaxBackupsToKeepField = 5;
    private bool SaveAfterBatchField = true;
    private bool PurgeAfterBatchField = true;
    private List<string> ExcludedLayerNamesField = new List<string>
    {
        "Adjustment Layer 1",
        "Black Solid 9",
        "Black Solid 10",
        "ETERNALS"
    };
    private bool UseSelectedOnlyField = false;
    private PresetMappingSettings PresetsField = new PresetMappingSettings();
    private int MotionTileWidthField = 370;
    private int MotionTileHeightField = 370;
    private int StartBlurFramesField = 10;
    private int EndBlur1LengthField = 200;
    private int EndBlur2LengthField = 80;
    private int VerticalBlurDirectionField = 90;
    private int HorizontalBlurDirectionField = 180;
    private ShakeStartDirection StartDirectionField = ShakeStartDirection.Vertical;
    private BlurZoomStartPreset BlurZoomStartField = BlurZoomStartPreset.BlurZoomIn;
    private bool AddMotionTileToBlurZoomField = false;
    private BeatDetectionSettings BeatDetectionField = new BeatDetectionSettings();
    private ReverseRollbackMode ReverseModeField = ReverseRollbackMode.Stage2Only;
    private List<string> SelectedStepIdsField = new List<string>
    {
        PipelineStepIds.Precomp,
        PipelineStepIds.Fade,
        PipelineStepIds.Shakes,
        PipelineStepIds.StartBlur,
        PipelineStepIds.EndBlurs,
        PipelineStepIds.ZoomGroup,
        PipelineStepIds.BlurZoom
    };

    #endregion

    #region Properties

    public string AfterEffectsExecutablePath
    {
        get
        {
            return this.AfterEffectsExecutablePathField;
        }
        set
        {
            this.AfterEffectsExecutablePathField = value ?? string.Empty;
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
            this.UserPresetsRootPathField = value ?? string.Empty;
        }
    }

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

    public int WatchdogTimeoutSeconds
    {
        get
        {
            return this.WatchdogTimeoutSecondsField;
        }
        set
        {
            this.WatchdogTimeoutSecondsField = value;
        }
    }

    public int MaxBackupsToKeep
    {
        get
        {
            return this.MaxBackupsToKeepField;
        }
        set
        {
            this.MaxBackupsToKeepField = value;
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
            this.SaveAfterBatchField = value;
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
            this.PurgeAfterBatchField = value;
        }
    }

    public List<string> ExcludedLayerNames
    {
        get
        {
            return this.ExcludedLayerNamesField;
        }
        set
        {
            this.ExcludedLayerNamesField = value ?? new List<string>();
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
            this.UseSelectedOnlyField = value;
        }
    }

    public PresetMappingSettings Presets
    {
        get
        {
            return this.PresetsField;
        }
        set
        {
            this.PresetsField = value ?? new PresetMappingSettings();
        }
    }

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

    public int VerticalBlurDirection
    {
        get
        {
            return this.VerticalBlurDirectionField;
        }
        set
        {
            this.VerticalBlurDirectionField = value;
        }
    }

    public int HorizontalBlurDirection
    {
        get
        {
            return this.HorizontalBlurDirectionField;
        }
        set
        {
            this.HorizontalBlurDirectionField = value;
        }
    }

    public ShakeStartDirection StartDirection
    {
        get
        {
            return this.StartDirectionField;
        }
        set
        {
            this.StartDirectionField = value;
        }
    }

    public BlurZoomStartPreset BlurZoomStart
    {
        get
        {
            return this.BlurZoomStartField;
        }
        set
        {
            this.BlurZoomStartField = value;
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
            this.AddMotionTileToBlurZoomField = value;
        }
    }

    public BeatDetectionSettings BeatDetection
    {
        get
        {
            return this.BeatDetectionField;
        }
        set
        {
            this.BeatDetectionField = value ?? new BeatDetectionSettings();
        }
    }

    public ReverseRollbackMode ReverseMode
    {
        get
        {
            return this.ReverseModeField;
        }
        set
        {
            this.ReverseModeField = value;
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

    #endregion
}

public sealed class PresetMappingSettings
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

public sealed class BeatDetectionSettings
{
    #region Fields

    private BeatDetectionMode ModeField = BeatDetectionMode.UniversalFourZone;
    private double SensitivityField = 65.0;
    private int MinFrameGapField = 2;
    private BeatMarkerTarget MarkerTargetField = BeatMarkerTarget.Composition;
    private bool OnlyWorkAreaField = false;
    private double SampleXRatioField = 0.85;
    private double SampleYRatioField = 0.15;
    private int SampleRadiusField = 15;

    #endregion

    #region Properties

    public BeatDetectionMode Mode
    {
        get
        {
            return this.ModeField;
        }
        set
        {
            this.ModeField = value;
        }
    }

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

    public BeatMarkerTarget MarkerTarget
    {
        get
        {
            return this.MarkerTargetField;
        }
        set
        {
            this.MarkerTargetField = value;
        }
    }

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

public sealed class PresetValidationItem
{
    #region Fields

    private string PresetKeyField = string.Empty;
    private string PresetNameField = string.Empty;
    private bool IsFoundField = false;
    private string FullPathField = string.Empty;

    #endregion

    #region Properties

    public string PresetKey
    {
        get
        {
            return this.PresetKeyField;
        }
        set
        {
            this.PresetKeyField = value ?? string.Empty;
        }
    }

    public string PresetName
    {
        get
        {
            return this.PresetNameField;
        }
        set
        {
            this.PresetNameField = value ?? string.Empty;
        }
    }

    public bool IsFound
    {
        get
        {
            return this.IsFoundField;
        }
        set
        {
            this.IsFoundField = value;
        }
    }

    public string FullPath
    {
        get
        {
            return this.FullPathField;
        }
        set
        {
            this.FullPathField = value ?? string.Empty;
        }
    }

    #endregion
}
