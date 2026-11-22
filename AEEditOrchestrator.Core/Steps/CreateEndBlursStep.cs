using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Core.Steps;

public sealed class CreateEndBlursStep : BasePipelineStep
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
            return PipelineStepIds.EndBlurs;
        }
    }

    public override string DisplayName
    {
        get
        {
            return "End-Blurs erstellen";
        }
    }

    public override string Description
    {
        get
        {
            return "Erstellt zwei 1-Frame Adjustment Layers (200 -> 80) am Clip-Ende synchron zur StartDirection und setzt [DONE_PIPELINE_1].";
        }
    }

    public override int ExecutionOrder
    {
        get
        {
            return 60;
        }
    }

    public override IReadOnlyList<string> RequiredStepIds
    {
        get
        {
            return CreateEndBlursStep.RequiredSteps;
        }
    }

    public override string JsxScriptFileName
    {
        get
        {
            return "06_CreateEndBlurs.jsx";
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

    public CreateEndBlursStep(IAfterEffectsBridge Bridge)
        : base(Bridge)
    {
    }

    #endregion

    #region Public Methods

    public override ValidationResult ValidateSettings(
        PipelineSettings Settings,
        ProjectPreflightState PreflightState)
    {
        ValidationResult BaseResult = base.ValidateSettings(Settings, PreflightState);
        List<string> Errors = new List<string>(BaseResult.Errors);

        if (Settings != null)
        {
            if (Settings.EndBlur1Length < 0 || Settings.EndBlur2Length < 0)
            {
                Errors.Add("EndBlur1Length und EndBlur2Length dürfen nicht negativ sein.");
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
