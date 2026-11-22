using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Steps;

namespace AEEditOrchestrator.Core.Engine;

public sealed class StepRegistry
{
    #region Fields

    private readonly List<IPipelineStep> AllStepsField;
    private readonly List<IPipelineStep> SelectableStepsField;
    private readonly Dictionary<string, IPipelineStep> StepsByIdField;

    #endregion

    #region Constructors

    public StepRegistry(IEnumerable<IPipelineStep> Steps)
    {
        if (Steps == null)
        {
            throw new ArgumentNullException(nameof(Steps));
        }

        this.StepsByIdField = new Dictionary<string, IPipelineStep>(StringComparer.OrdinalIgnoreCase);
        List<IPipelineStep> CollectedSteps = new List<IPipelineStep>();

        foreach (IPipelineStep Step in Steps)
        {
            if (Step == null)
            {
                throw new ArgumentException("Die Step-Liste darf keine null-Einträge enthalten.", nameof(Steps));
            }

            if (string.IsNullOrWhiteSpace(Step.StepId))
            {
                throw new ArgumentException("Jeder Pipeline-Schritt benötigt eine gültige StepId.", nameof(Steps));
            }

            string NormalizedId = Step.StepId.Trim();
            if (this.StepsByIdField.ContainsKey(NormalizedId))
            {
                throw new ArgumentException($"Doppelte StepId '{NormalizedId}' in StepRegistry.", nameof(Steps));
            }

            this.StepsByIdField.Add(NormalizedId, Step);
            CollectedSteps.Add(Step);
        }

        CollectedSteps.Sort(StepRegistry.CompareStepsByExecutionOrder);
        this.AllStepsField = CollectedSteps;

        this.SelectableStepsField = new List<IPipelineStep>();
        foreach (IPipelineStep Step in this.AllStepsField)
        {
            if (!StepRegistry.IsUtilityStep(Step.StepId))
            {
                this.SelectableStepsField.Add(Step);
            }
        }
    }

    #endregion

    #region Public Methods

    public static StepRegistry CreateDefault(IAfterEffectsBridge Bridge)
    {
        if (Bridge == null)
        {
            throw new ArgumentNullException(nameof(Bridge));
        }

        IPipelineStep[] DefaultSteps = new IPipelineStep[]
        {
            new PreflightCheckStep(Bridge),
            new BeatDetectionStep(Bridge),
            new PrecomposeClipsStep(Bridge),
            new ApplyFadeInOutStep(Bridge),
            new ApplyMotionTileAndShakesStep(Bridge),
            new CreateStartBlursStep(Bridge),
            new CreateEndBlursStep(Bridge),
            new GroupClipsForZoomStep(Bridge),
            new ApplyBlurZoomStep(Bridge),
            new ReverseRollbackStep(Bridge)
        };

        return new StepRegistry(DefaultSteps);
    }

    public IReadOnlyList<IPipelineStep> GetAllSteps()
    {
        return this.AllStepsField.AsReadOnly();
    }

    public IReadOnlyList<IPipelineStep> GetSelectablePipelineSteps()
    {
        return this.SelectableStepsField.AsReadOnly();
    }

    public IPipelineStep? GetStep(string StepId)
    {
        if (string.IsNullOrWhiteSpace(StepId))
        {
            return null;
        }

        IPipelineStep? FoundStep;
        if (this.StepsByIdField.TryGetValue(StepId.Trim(), out FoundStep))
        {
            return FoundStep;
        }

        return null;
    }

    #endregion

    #region Internal Methods

    internal static int CompareStepsByExecutionOrder(IPipelineStep Left, IPipelineStep Right)
    {
        int OrderComparison = Left.ExecutionOrder.CompareTo(Right.ExecutionOrder);
        if (OrderComparison != 0)
        {
            return OrderComparison;
        }

        return string.Compare(Left.StepId, Right.StepId, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Private Methods

    private static bool IsUtilityStep(string StepId)
    {
        if (string.Equals(StepId, PipelineStepIds.Preflight, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(StepId, PipelineStepIds.Reverse, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    #endregion
}
