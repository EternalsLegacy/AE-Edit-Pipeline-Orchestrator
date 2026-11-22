using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Core.Steps;

public sealed class PrecomposeClipsStep : BasePipelineStep
{
    #region Properties

    public override string StepId
    {
        get
        {
            return PipelineStepIds.Precomp;
        }
    }

    public override string DisplayName
    {
        get
        {
            return "Clips Pre-composen";
        }
    }

    public override string Description
    {
        get
        {
            return "Packt gültige Roh-Clips einzeln in Precomps, schneidet die Dauer exakt zu und setzt [EDIT_CLIP:X].";
        }
    }

    public override int ExecutionOrder
    {
        get
        {
            return 20;
        }
    }

    public override string JsxScriptFileName
    {
        get
        {
            return "02_PrecomposeClips.jsx";
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

    public PrecomposeClipsStep(IAfterEffectsBridge Bridge)
        : base(Bridge)
    {
    }

    #endregion
}
