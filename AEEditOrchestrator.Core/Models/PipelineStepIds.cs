namespace AEEditOrchestrator.Core.Models;

public static class PipelineStepIds
{
    #region Constants

    public const string Preflight = "00_preflight";
    public const string Beats = "01_beats";
    public const string Precomp = "02_precomp";
    public const string Fade = "03_fade";
    public const string Shakes = "04_shakes";
    public const string StartBlur = "05_startblur";
    public const string EndBlurs = "06_endblurs";
    public const string ZoomGroup = "07_zoomgroup";
    public const string BlurZoom = "08_blurzoom";
    public const string Reverse = "99_reverse";

    #endregion
}
