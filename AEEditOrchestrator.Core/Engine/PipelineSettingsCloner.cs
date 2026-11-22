using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.Core.Engine;

internal static class PipelineSettingsCloner
{
    #region Public Methods

    public static PipelineSettings Clone(
        PipelineSettings Source,
        IReadOnlyCollection<string>? SelectedStepIdsOverride = null,
        ReverseRollbackMode? ReverseModeOverride = null)
    {
        if (Source == null)
        {
            throw new ArgumentNullException(nameof(Source));
        }

        List<string> ClonedStepIds = new List<string>();
        if (SelectedStepIdsOverride != null)
        {
            foreach (string StepId in SelectedStepIdsOverride)
            {
                if (!string.IsNullOrWhiteSpace(StepId))
                {
                    ClonedStepIds.Add(StepId.Trim());
                }
            }
        }
        else if (Source.SelectedStepIds != null)
        {
            ClonedStepIds.AddRange(Source.SelectedStepIds);
        }

        ReverseRollbackMode EffectiveReverseMode = Source.ReverseMode;
        if (ReverseModeOverride.HasValue)
        {
            EffectiveReverseMode = ReverseModeOverride.Value;
        }

        List<string> ClonedExcludedNames = new List<string>();
        if (Source.ExcludedLayerNames != null)
        {
            ClonedExcludedNames.AddRange(Source.ExcludedLayerNames);
        }

        PipelineSettings Copy = new PipelineSettings
        {
            AfterEffectsExecutablePath = Source.AfterEffectsExecutablePath,
            UserPresetsRootPath = Source.UserPresetsRootPath,
            BatchSize = Source.BatchSize,
            WatchdogTimeoutSeconds = Source.WatchdogTimeoutSeconds,
            MaxBackupsToKeep = Source.MaxBackupsToKeep,
            SaveAfterBatch = Source.SaveAfterBatch,
            PurgeAfterBatch = Source.PurgeAfterBatch,
            ExcludedLayerNames = ClonedExcludedNames,
            UseSelectedOnly = Source.UseSelectedOnly,
            Presets = Source.Presets,
            MotionTileWidth = Source.MotionTileWidth,
            MotionTileHeight = Source.MotionTileHeight,
            StartBlurFrames = Source.StartBlurFrames,
            EndBlur1Length = Source.EndBlur1Length,
            EndBlur2Length = Source.EndBlur2Length,
            VerticalBlurDirection = Source.VerticalBlurDirection,
            HorizontalBlurDirection = Source.HorizontalBlurDirection,
            StartDirection = Source.StartDirection,
            BlurZoomStart = Source.BlurZoomStart,
            AddMotionTileToBlurZoom = Source.AddMotionTileToBlurZoom,
            BeatDetection = Source.BeatDetection,
            ReverseMode = EffectiveReverseMode,
            SelectedStepIds = ClonedStepIds
        };

        return Copy;
    }

    #endregion
}
