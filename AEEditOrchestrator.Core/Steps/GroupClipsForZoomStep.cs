using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Core.Steps;

public sealed class GroupClipsForZoomStep : BasePipelineStep
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
            return PipelineStepIds.ZoomGroup;
        }
    }

    public override string DisplayName
    {
        get
        {
            return "Clips für Zoom gruppieren";
        }
    }

    public override string Description
    {
        get
        {
            return "Sammelt Clip-Precomp und zugehörige Blur-Layers, precompost zu ZoomGroup_X_In/Out und setzt [DONE_ZOOM_GROUP].";
        }
    }

    public override int ExecutionOrder
    {
        get
        {
            return 70;
        }
    }

    public override IReadOnlyList<string> RequiredStepIds
    {
        get
        {
            return GroupClipsForZoomStep.RequiredSteps;
        }
    }

    public override string JsxScriptFileName
    {
        get
        {
            return "07_GroupClipsForZoom.jsx";
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

    public GroupClipsForZoomStep(IAfterEffectsBridge Bridge)
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
            if (!BasePipelineStep.HasStage1ClipPrerequisites(Settings, PreflightState))
            {
                Errors.Add($"Schritt '{this.DisplayName}' ({this.StepId}) benötigt '{PipelineStepIds.Precomp}' oder bestehende Stage-1-/EDIT_CLIP-Tags.");
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
