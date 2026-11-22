using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Core.Steps;

public sealed class BeatDetectionStep : BasePipelineStep
{
    #region Properties

    public override string StepId
    {
        get
        {
            return PipelineStepIds.Beats;
        }
    }

    public override string DisplayName
    {
        get
        {
            return "Beat-Erkennung";
        }
    }

    public override string Description
    {
        get
        {
            return "Erkennt Beats per sampleImage()-Analyse und setzt Marker auf Komposition oder Layer.";
        }
    }

    public override int ExecutionOrder
    {
        get
        {
            return 10;
        }
    }

    public override string JsxScriptFileName
    {
        get
        {
            return "01_BeatDetection.jsx";
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

    public BeatDetectionStep(IAfterEffectsBridge Bridge)
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
            if (Settings.BeatDetection.Sensitivity < 10.0 || Settings.BeatDetection.Sensitivity > 100.0)
            {
                Errors.Add("BeatDetection.Sensitivity muss zwischen 10 und 100 liegen.");
            }

            if (Settings.BeatDetection.MinFrameGap < 0)
            {
                Errors.Add("BeatDetection.MinFrameGap darf nicht negativ sein.");
            }

            if (Settings.BeatDetection.SampleRadius < 1)
            {
                Errors.Add("BeatDetection.SampleRadius muss mindestens 1 sein.");
            }

            if (Settings.BeatDetection.SampleXRatio < 0.0 || Settings.BeatDetection.SampleXRatio > 1.0)
            {
                Errors.Add("BeatDetection.SampleXRatio muss zwischen 0.0 und 1.0 liegen.");
            }

            if (Settings.BeatDetection.SampleYRatio < 0.0 || Settings.BeatDetection.SampleYRatio > 1.0)
            {
                Errors.Add("BeatDetection.SampleYRatio muss zwischen 0.0 und 1.0 liegen.");
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
