using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Core.Steps;

public sealed class CreateStartBlursStep : BasePipelineStep
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
            return PipelineStepIds.StartBlur;
        }
    }

    public override string DisplayName
    {
        get
        {
            return "Start-Blurs erstellen";
        }
    }

    public override string Description
    {
        get
        {
            return "Erstellt pro Clip ein 10-Frame Adjustment Layer mit Motion Tile und VerticalBlur / HorizontalBlur synchron zur StartDirection.";
        }
    }

    public override int ExecutionOrder
    {
        get
        {
            return 50;
        }
    }

    public override IReadOnlyList<string> RequiredStepIds
    {
        get
        {
            return CreateStartBlursStep.RequiredSteps;
        }
    }

    public override string JsxScriptFileName
    {
        get
        {
            return "05_CreateStartBlurs.jsx";
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

    public CreateStartBlursStep(IAfterEffectsBridge Bridge)
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
            Settings.Presets.VerticalBlur,
            Settings.Presets.HorizontalBlur);
    }

    public override ValidationResult ValidateSettings(
        PipelineSettings Settings,
        ProjectPreflightState PreflightState)
    {
        ValidationResult BaseResult = base.ValidateSettings(Settings, PreflightState);
        List<string> Errors = new List<string>(BaseResult.Errors);

        if (Settings != null)
        {
            BasePipelineStep.ValidatePresetConfigured(Settings.Presets.VerticalBlur, "VerticalBlur", Errors);
            BasePipelineStep.ValidatePresetConfigured(Settings.Presets.HorizontalBlur, "HorizontalBlur", Errors);

            if (Settings.StartBlurFrames <= 0)
            {
                Errors.Add("StartBlurFrames muss größer als 0 sein.");
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
