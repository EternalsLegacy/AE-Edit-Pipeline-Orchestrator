using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Core.Steps;

public sealed class ApplyFadeInOutStep : BasePipelineStep
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
            return PipelineStepIds.Fade;
        }
    }

    public override string DisplayName
    {
        get
        {
            return "FadeInOut anwenden";
        }
    }

    public override string Description
    {
        get
        {
            return "Wendet das Preset FadeInOut auf getaggte Clip-Precomps an und streckt Keyframes auf die Clip-Länge.";
        }
    }

    public override int ExecutionOrder
    {
        get
        {
            return 30;
        }
    }

    public override IReadOnlyList<string> RequiredStepIds
    {
        get
        {
            return ApplyFadeInOutStep.RequiredSteps;
        }
    }

    public override string JsxScriptFileName
    {
        get
        {
            return "03_ApplyFadeInOut.jsx";
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

    public ApplyFadeInOutStep(IAfterEffectsBridge Bridge)
        : base(Bridge)
    {
    }

    #endregion

    #region Public Methods

    public override IReadOnlyList<string> GetRequiredPresets(PipelineSettings Settings)
    {
        if (Settings == null || string.IsNullOrWhiteSpace(Settings.Presets.FadeInOut))
        {
            return Array.Empty<string>();
        }

        return new string[] { Settings.Presets.FadeInOut.Trim() };
    }

    public override ValidationResult ValidateSettings(
        PipelineSettings Settings,
        ProjectPreflightState PreflightState)
    {
        ValidationResult BaseResult = base.ValidateSettings(Settings, PreflightState);
        List<string> Errors = new List<string>(BaseResult.Errors);

        if (Settings != null)
        {
            BasePipelineStep.ValidatePresetConfigured(Settings.Presets.FadeInOut, "FadeInOut", Errors);

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
