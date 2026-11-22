using AEEditOrchestrator.Core.Engine;
using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Steps;
using AEEditOrchestrator.Tests.Core.TestDoubles;

namespace AEEditOrchestrator.Tests.Core;

public sealed class StepRegistryTests
{
    #region Public Methods

    [Fact]
    public void GetAllStepsReturnsAllTenStepsSortedByExecutionOrder()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        StepRegistry Registry = StepRegistry.CreateDefault(Bridge);

        IReadOnlyList<IPipelineStep> AllSteps = Registry.GetAllSteps();

        Assert.Equal(10, AllSteps.Count);
        Assert.Equal(PipelineStepIds.Preflight, AllSteps[0].StepId);
        Assert.Equal(PipelineStepIds.Beats, AllSteps[1].StepId);
        Assert.Equal(PipelineStepIds.Precomp, AllSteps[2].StepId);
        Assert.Equal(PipelineStepIds.Fade, AllSteps[3].StepId);
        Assert.Equal(PipelineStepIds.Shakes, AllSteps[4].StepId);
        Assert.Equal(PipelineStepIds.StartBlur, AllSteps[5].StepId);
        Assert.Equal(PipelineStepIds.EndBlurs, AllSteps[6].StepId);
        Assert.Equal(PipelineStepIds.ZoomGroup, AllSteps[7].StepId);
        Assert.Equal(PipelineStepIds.BlurZoom, AllSteps[8].StepId);
        Assert.Equal(PipelineStepIds.Reverse, AllSteps[9].StepId);
    }

    [Fact]
    public void GetSelectablePipelineStepsExcludesPreflightAndReverseAndSortsByOrder()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        IPipelineStep[] Unordered = new IPipelineStep[]
        {
            new ReverseRollbackStep(Bridge),
            new ApplyBlurZoomStep(Bridge),
            new BeatDetectionStep(Bridge),
            new PreflightCheckStep(Bridge),
            new PrecomposeClipsStep(Bridge),
            new GroupClipsForZoomStep(Bridge),
            new ApplyFadeInOutStep(Bridge),
            new CreateEndBlursStep(Bridge),
            new ApplyMotionTileAndShakesStep(Bridge),
            new CreateStartBlursStep(Bridge)
        };

        StepRegistry Registry = new StepRegistry(Unordered);
        IReadOnlyList<IPipelineStep> Selectable = Registry.GetSelectablePipelineSteps();

        Assert.Equal(8, Selectable.Count);
        Assert.Equal(PipelineStepIds.Beats, Selectable[0].StepId);
        Assert.Equal(PipelineStepIds.Precomp, Selectable[1].StepId);
        Assert.Equal(PipelineStepIds.Fade, Selectable[2].StepId);
        Assert.Equal(PipelineStepIds.Shakes, Selectable[3].StepId);
        Assert.Equal(PipelineStepIds.StartBlur, Selectable[4].StepId);
        Assert.Equal(PipelineStepIds.EndBlurs, Selectable[5].StepId);
        Assert.Equal(PipelineStepIds.ZoomGroup, Selectable[6].StepId);
        Assert.Equal(PipelineStepIds.BlurZoom, Selectable[7].StepId);
    }

    [Fact]
    public void GetStepIsCaseInsensitiveAndReturnsNullWhenMissing()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        StepRegistry Registry = StepRegistry.CreateDefault(Bridge);

        IPipelineStep? Found = Registry.GetStep("04_SHAKES");
        IPipelineStep? Missing = Registry.GetStep("99_nonexistent");
        IPipelineStep? Blank = Registry.GetStep("   ");

        Assert.NotNull(Found);
        Assert.Equal(PipelineStepIds.Shakes, Found.StepId);
        Assert.Null(Missing);
        Assert.Null(Blank);
    }

    [Fact]
    public void ConstructorThrowsOnDuplicateStepId()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        IPipelineStep[] DuplicateSteps = new IPipelineStep[]
        {
            new BeatDetectionStep(Bridge),
            new BeatDetectionStep(Bridge)
        };

        Assert.Throws<ArgumentException>(() =>
        {
            StepRegistry Unused = new StepRegistry(DuplicateSteps);
        });
    }

    #endregion
}
