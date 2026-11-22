namespace AEEditOrchestrator.Core.Models;

public enum ShakeStartDirection
{
    Vertical = 0,
    Horizontal = 1
}

public enum BlurZoomStartPreset
{
    BlurZoomIn = 0,
    BlurZoomOut = 1
}

public enum BeatDetectionMode
{
    RedOverlay = 0,
    UniversalFourZone = 1
}

public enum BeatMarkerTarget
{
    Composition = 0,
    Layer = 1
}

public enum ReverseRollbackMode
{
    Stage2Only = 0,
    Stage1Only = 1,
    Full = 2
}

public enum TransactionStatus
{
    Active = 0,
    Committed = 1,
    RolledBack = 2,
    Failed = 3
}
