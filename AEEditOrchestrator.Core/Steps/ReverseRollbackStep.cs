using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Core.Steps;

public sealed class ReverseRollbackStep : BasePipelineStep
{
    #region Properties

    public override string StepId
    {
        get
        {
            return PipelineStepIds.Reverse;
        }
    }

    public override string DisplayName
    {
        get
        {
            return "Granulares Reverse";
        }
    }

    public override string Description
    {
        get
        {
            return "Entpackt gezielt Stufe 2 (BlurZoom) und/oder Stufe 1 (Precomps) zurück in die Hauptkomposition.";
        }
    }

    public override int ExecutionOrder
    {
        get
        {
            return 99;
        }
    }

    public override string JsxScriptFileName
    {
        get
        {
            return "99_ReverseRollback.jsx";
        }
    }

    public override bool SupportsBatching
    {
        get
        {
            return false;
        }
    }

    #endregion

    #region Constructors

    public ReverseRollbackStep(IAfterEffectsBridge Bridge)
        : base(Bridge)
    {
    }

    #endregion
}
