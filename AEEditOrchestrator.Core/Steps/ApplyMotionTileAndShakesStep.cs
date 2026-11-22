using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Core.Steps;

public sealed class ApplyMotionTileAndShakesStep : BasePipelineStep
{
    #region Fields

    private static readonly IReadOnlyList<string> RequiredSteps = new string[]
    {
        PipelineStepIds.Precomp
    };

    #endregion

    #region Properties

    public override string StepId
    {
        get
        {
            return PipelineStepIds.Shakes;
        }
    }

    public override string DisplayName
    {
        get
        {
            return "Motion Tile & Shakes";
        }
    }

    public override string Description
    {
        get
        {
            return "Fügt Motion Tile hinzu und wendet abwechselnd VerticalShake / HorizontalShake gemäß StartDirection an.";
        }
    }

    public override int ExecutionOrder
    {
        get
        {
            return 40;
        }
    }

    public override IReadOnlyList<string> RequiredStepIds
    {
        get
        {
            return ApplyMotionTileAndShakesStep.RequiredSteps;
        }
    }

    public override string JsxScriptFileName
    {
        get
        {
            return "04_ApplyMotionTileAndShakes.jsx";
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

    public ApplyMotionTileAndShakesStep(IAfterEffectsBridge Bridge)
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
            Settings.Presets.VerticalShake,
            Settings.Presets.HorizontalShake);
    }

    public override ValidationResult ValidateSettings(
        PipelineSettings Settings,
        ProjectPreflightState PreflightState)
    {
        ValidationResult BaseResult = base.ValidateSettings(Settings, PreflightState);
        List<string> Errors = new List<string>(BaseResult.Errors);

        if (Settings != null)
        {
            BasePipelineStep.ValidatePresetConfigured(Settings.Presets.VerticalShake, "VerticalShake", Errors);
            BasePipelineStep.ValidatePresetConfigured(Settings.Presets.HorizontalShake, "HorizontalShake", Errors);

            if (Settings.MotionTileWidth <= 0 || Settings.MotionTileHeight <= 0)
            {
                Errors.Add("MotionTileWidth und MotionTileHeight müssen größer als 0 sein.");
            }

            if (!BasePipelineStep.HasStage1ClipPrerequisites(Settings, PreflightState))
            {
                Errors.Add($"Schritt '{this.DisplayName}' ({this.StepId}) benötigt '{PipelineStepIds.Precomp}' oder bestehende [EDIT_CLIP:X]-Tags.");
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
