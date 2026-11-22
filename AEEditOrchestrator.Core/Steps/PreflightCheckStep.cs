using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Models.Ipc;

namespace AEEditOrchestrator.Core.Steps;

public sealed class PreflightCheckStep : BasePipelineStep
{
    #region Properties

    public override string StepId
    {
        get
        {
            return PipelineStepIds.Preflight;
        }
    }

    public override string DisplayName
    {
        get
        {
            return "Preflight-Prüfung";
        }
    }

    public override string Description
    {
        get
        {
            return "Prüft After Effects, aktive Komposition, gespeichertes Projekt, gültige Clips, Tags und Presets.";
        }
    }

    public override int ExecutionOrder
    {
        get
        {
            return 0;
        }
    }

    public override string JsxScriptFileName
    {
        get
        {
            return "00_PreflightCheck.jsx";
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

    public PreflightCheckStep(IAfterEffectsBridge Bridge)
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
            Settings.Presets.FadeInOut,
            Settings.Presets.VerticalShake,
            Settings.Presets.HorizontalShake,
            Settings.Presets.VerticalBlur,
            Settings.Presets.HorizontalBlur,
            Settings.Presets.BlurZoomIn,
            Settings.Presets.BlurZoomOut);
    }

    #endregion

    #region Protected Methods

    protected override JobPayload CreateJobPayload(PipelineContext Context, int BatchStartIndex)
    {
        string JobId = Guid.NewGuid().ToString("N");
        IReadOnlyList<string> Presets = this.GetRequiredPresets(Context.Settings);

        return JobPayload.FromSettings(
            JobId,
            this.JsxScriptFileName,
            Context.Settings,
            BatchStartIndex,
            Presets,
            false,
            false);
    }

    #endregion
}
