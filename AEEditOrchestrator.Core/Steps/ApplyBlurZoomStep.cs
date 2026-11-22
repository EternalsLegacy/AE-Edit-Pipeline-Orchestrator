using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Core.Steps;

public sealed class ApplyBlurZoomStep : BasePipelineStep
{
    #region Fields

    private static readonly IReadOnlyList<string> RequiredSteps = new string[]
    {
        PipelineStepIds.ZoomGroup
    };

    #endregion

    #region Properties

    public override string StepId
    {
        get
        {
            return PipelineStepIds.BlurZoom;
        }
    }

    public override string DisplayName
    {
        get
        {
            return "BlurZoom anwenden";
        }
    }

    public override string Description
    {
        get
        {
            return "Erstellt pro ZoomGroup ein Adjustment Layer, wendet abwechselnd BlurZoomIn/BlurZoomOut an und streckt Keyframes.";
        }
    }

    public override int ExecutionOrder
    {
        get
        {
            return 80;
        }
    }

    public override IReadOnlyList<string> RequiredStepIds
    {
        get
        {
            return ApplyBlurZoomStep.RequiredSteps;
        }
    }

    public override string JsxScriptFileName
    {
        get
        {
            return "08_ApplyBlurZoom.jsx";
        }
    }

    public override bool SupportsBatching
    {
        get
        {
            return true;
        }
    }

    #endregion

    #region Constructors

    public ApplyBlurZoomStep(IAfterEffectsBridge Bridge)
        : base(Bridge)
    {
    }

    #endregion

    #region Public Methods

    public override IReadOnlyList<string> GetRequiredPresets(PipelineSettings Settings)
    {
        if (Settings == null)
        {
            return Array.Empty<string>();
        }

        return BasePipelineStep.CollectDistinctPresets(
            Settings.Presets.BlurZoomIn,
            Settings.Presets.BlurZoomOut);
    }

    public override ValidationResult ValidateSettings(
        PipelineSettings Settings,
        ProjectPreflightState PreflightState)
    {
        ValidationResult BaseResult = base.ValidateSettings(Settings, PreflightState);
        List<string> Errors = new List<string>(BaseResult.Errors);

        if (Settings != null)
        {
            BasePipelineStep.ValidatePresetConfigured(Settings.Presets.BlurZoomIn, "BlurZoomIn", Errors);
            BasePipelineStep.ValidatePresetConfigured(Settings.Presets.BlurZoomOut, "BlurZoomOut", Errors);

            if (!BasePipelineStep.HasZoomGroupPrerequisites(Settings, PreflightState))
            {
                Errors.Add($"Schritt '{this.DisplayName}' ({this.StepId}) benötigt '{PipelineStepIds.ZoomGroup}' oder bestehende [DONE_ZOOM_GROUP]-Tags.");
            }
        }

        if (Errors.Count > 0)
        {
            return ValidationResult.Failure(Errors);
        }

        return ValidationResult.Success();
    }

    #endregion
}
