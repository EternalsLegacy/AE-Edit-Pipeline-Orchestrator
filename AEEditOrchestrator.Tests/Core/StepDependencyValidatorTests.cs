using AEEditOrchestrator.Core.Engine;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Tests.Core.TestDoubles;

namespace AEEditOrchestrator.Tests.Core;

public sealed class StepDependencyValidatorTests
{
    #region Private Methods

    private static StepDependencyValidator CreateValidator()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        StepRegistry Registry = StepRegistry.CreateDefault(Bridge);
        return new StepDependencyValidator(Registry);
    }

    #endregion

    #region Public Methods

    [Fact]
    public void ValidateSelectionEmptySelectionReturnsFailure()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        PipelineSettings Settings = new PipelineSettings();

        ValidationResult Result = Validator.ValidateSelection(
            Array.Empty<string>(),
            Settings,
            new ProjectPreflightState());

        Assert.False(Result.IsValid);
        Assert.NotEmpty(Result.Errors);
    }

    [Fact]
    public void ValidateSelectionUnknownOrUtilityStepReturnsFailure()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        PipelineSettings Settings = new PipelineSettings();

        ValidationResult UnknownResult = Validator.ValidateSelection(
            new string[] { "does_not_exist" },
            Settings,
            new ProjectPreflightState());

        ValidationResult PreflightResult = Validator.ValidateSelection(
            new string[] { PipelineStepIds.Preflight },
            Settings,
            new ProjectPreflightState());

        ValidationResult ReverseResult = Validator.ValidateSelection(
            new string[] { PipelineStepIds.Reverse },
            Settings,
            new ProjectPreflightState());

        Assert.False(UnknownResult.IsValid);
        Assert.False(PreflightResult.IsValid);
        Assert.False(ReverseResult.IsValid);
    }

    [Fact]
    public void ValidateSelectionBeatDetectionAloneIsValid()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        PipelineSettings Settings = new PipelineSettings();

        ValidationResult Result = Validator.ValidateSelection(
            new string[] { PipelineStepIds.Beats },
            Settings,
            new ProjectPreflightState());

        Assert.True(Result.IsValid);
        Assert.Empty(Result.Errors);
    }

    [Theory]
    [InlineData(PipelineStepIds.Fade)]
    [InlineData(PipelineStepIds.Shakes)]
    [InlineData(PipelineStepIds.StartBlur)]
    [InlineData(PipelineStepIds.EndBlurs)]
    [InlineData(PipelineStepIds.ZoomGroup)]
    public void ValidateSelectionStage1DependentStepsWithoutPrecompOrTagsReturnFailure(string StepId)
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        PipelineSettings Settings = new PipelineSettings();
        ProjectPreflightState Preflight = new ProjectPreflightState
        {
            HasEditClipTags = false,
            HasStage1Tags = false
        };

        ValidationResult Result = Validator.ValidateSelection(
            new string[] { StepId },
            Settings,
            Preflight);

        Assert.False(Result.IsValid);
        Assert.Single(Result.Errors);
    }

    [Theory]
    [InlineData(PipelineStepIds.Fade)]
    [InlineData(PipelineStepIds.Shakes)]
    [InlineData(PipelineStepIds.StartBlur)]
    [InlineData(PipelineStepIds.EndBlurs)]
    [InlineData(PipelineStepIds.ZoomGroup)]
    public void ValidateSelectionStage1DependentStepsWithPrecompAreValid(string StepId)
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        PipelineSettings Settings = new PipelineSettings();

        ValidationResult Result = Validator.ValidateSelection(
            new string[] { PipelineStepIds.Precomp, StepId },
            Settings,
            new ProjectPreflightState());

        Assert.True(Result.IsValid);
        Assert.Empty(Result.Errors);
    }

    [Theory]
    [InlineData(PipelineStepIds.Fade, true, false)]
    [InlineData(PipelineStepIds.Shakes, true, false)]
    [InlineData(PipelineStepIds.StartBlur, false, true)]
    [InlineData(PipelineStepIds.EndBlurs, true, false)]
    [InlineData(PipelineStepIds.ZoomGroup, false, true)]
    public void ValidateSelectionStage1DependentStepsWithPreflightTagBypassAreValidWithoutPrecomp(
        string StepId,
        bool HasEditClipTags,
        bool HasStage1Tags)
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        PipelineSettings Settings = new PipelineSettings();
        ProjectPreflightState Preflight = new ProjectPreflightState
        {
            HasEditClipTags = HasEditClipTags,
            HasStage1Tags = HasStage1Tags
        };

        ValidationResult Result = Validator.ValidateSelection(
            new string[] { StepId },
            Settings,
            Preflight);

        Assert.True(Result.IsValid);
        Assert.Empty(Result.Errors);
    }

    [Fact]
    public void ValidateSelectionBlurZoomWithoutZoomGroupOrTagsReturnsFailure()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        PipelineSettings Settings = new PipelineSettings();
        ProjectPreflightState PreflightWithStage1Only = new ProjectPreflightState
        {
            HasEditClipTags = true,
            HasStage1Tags = true,
            HasZoomGroupTags = false
        };

        ValidationResult Result = Validator.ValidateSelection(
            new string[] { PipelineStepIds.BlurZoom },
            Settings,
            PreflightWithStage1Only);

        Assert.False(Result.IsValid);
        Assert.Single(Result.Errors);
    }

    [Fact]
    public void ValidateSelectionBlurZoomWithZoomGroupTagsBypassIsValid()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        PipelineSettings Settings = new PipelineSettings();
        ProjectPreflightState Preflight = new ProjectPreflightState
        {
            HasZoomGroupTags = true
        };

        ValidationResult Result = Validator.ValidateSelection(
            new string[] { PipelineStepIds.BlurZoom },
            Settings,
            Preflight);

        Assert.True(Result.IsValid);
    }

    [Fact]
    public void ValidateSelectionMissingRequiredPresetNameReturnsFailure()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        PipelineSettings Settings = new PipelineSettings();
        Settings.Presets.VerticalShake = string.Empty;

        ValidationResult Result = Validator.ValidateSelection(
            new string[] { PipelineStepIds.Precomp, PipelineStepIds.Shakes },
            Settings,
            new ProjectPreflightState());

        Assert.False(Result.IsValid);
        Assert.Contains(Result.Errors, Error => Error.Contains("VerticalShake", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateSelectionInvalidStartDirectionReturnsFailure()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        PipelineSettings Settings = new PipelineSettings
        {
            StartDirection = (ShakeStartDirection)99
        };

        ValidationResult Result = Validator.ValidateSelection(
            new string[] { PipelineStepIds.Precomp, PipelineStepIds.Shakes },
            Settings,
            new ProjectPreflightState());

        Assert.False(Result.IsValid);
        Assert.Contains(Result.Errors, Error => Error.Contains("StartDirection", StringComparison.Ordinal));
    }

    [Fact]
    public void NormalizeSelectionOnEnableBlurZoomWithoutTagsAutoEnablesZoomGroupAndPrecomp()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();

        IReadOnlyList<string> Updated = Validator.NormalizeSelectionOnEnable(
            PipelineStepIds.BlurZoom,
            Array.Empty<string>(),
            null);

        Assert.Equal(
            new string[] { PipelineStepIds.Precomp, PipelineStepIds.ZoomGroup, PipelineStepIds.BlurZoom },
            Updated);
    }

    [Fact]
    public void NormalizeSelectionOnEnableBlurZoomWithStage1TagsAutoEnablesZoomGroupOnly()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        ProjectPreflightState Preflight = new ProjectPreflightState
        {
            HasStage1Tags = true,
            HasZoomGroupTags = false
        };

        IReadOnlyList<string> Updated = Validator.NormalizeSelectionOnEnable(
            PipelineStepIds.BlurZoom,
            Array.Empty<string>(),
            Preflight);

        Assert.Equal(
            new string[] { PipelineStepIds.ZoomGroup, PipelineStepIds.BlurZoom },
            Updated);
    }

    [Fact]
    public void NormalizeSelectionOnEnableBlurZoomWithZoomGroupTagsEnablesOnlyBlurZoom()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        ProjectPreflightState Preflight = new ProjectPreflightState
        {
            HasZoomGroupTags = true
        };

        IReadOnlyList<string> Updated = Validator.NormalizeSelectionOnEnable(
            PipelineStepIds.BlurZoom,
            Array.Empty<string>(),
            Preflight);

        Assert.Equal(new string[] { PipelineStepIds.BlurZoom }, Updated);
    }

    [Fact]
    public void NormalizeSelectionOnEnableFadeWithoutTagsAutoEnablesPrecomp()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();

        IReadOnlyList<string> Updated = Validator.NormalizeSelectionOnEnable(
            PipelineStepIds.Fade,
            new string[] { PipelineStepIds.Beats },
            new ProjectPreflightState());

        Assert.Equal(
            new string[] { PipelineStepIds.Beats, PipelineStepIds.Precomp, PipelineStepIds.Fade },
            Updated);
    }

    [Fact]
    public void NormalizeSelectionOnDisablePrecompWithoutTagsCascadesToAllDependentStepsExceptBeats()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        string[] Initial = new string[]
        {
            PipelineStepIds.Beats,
            PipelineStepIds.Precomp,
            PipelineStepIds.Fade,
            PipelineStepIds.Shakes,
            PipelineStepIds.StartBlur,
            PipelineStepIds.EndBlurs,
            PipelineStepIds.ZoomGroup,
            PipelineStepIds.BlurZoom
        };

        IReadOnlyList<string> Updated = Validator.NormalizeSelectionOnDisable(
            PipelineStepIds.Precomp,
            Initial,
            null);

        Assert.Equal(new string[] { PipelineStepIds.Beats }, Updated);
    }

    [Fact]
    public void NormalizeSelectionOnDisablePrecompWithEditClipTagsPreservesDependentSteps()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        ProjectPreflightState Preflight = new ProjectPreflightState
        {
            HasEditClipTags = true
        };
        string[] Initial = new string[]
        {
            PipelineStepIds.Precomp,
            PipelineStepIds.Fade,
            PipelineStepIds.ZoomGroup,
            PipelineStepIds.BlurZoom
        };

        IReadOnlyList<string> Updated = Validator.NormalizeSelectionOnDisable(
            PipelineStepIds.Precomp,
            Initial,
            Preflight);

        Assert.Equal(
            new string[] { PipelineStepIds.Fade, PipelineStepIds.ZoomGroup, PipelineStepIds.BlurZoom },
            Updated);
    }

    [Fact]
    public void NormalizeSelectionOnDisablePrecompWithZoomGroupTagsOnlyPreservesBeatsAndBlurZoom()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        ProjectPreflightState Preflight = new ProjectPreflightState
        {
            HasEditClipTags = false,
            HasStage1Tags = false,
            HasZoomGroupTags = true
        };
        string[] Initial = new string[]
        {
            PipelineStepIds.Beats,
            PipelineStepIds.Precomp,
            PipelineStepIds.Fade,
            PipelineStepIds.ZoomGroup,
            PipelineStepIds.BlurZoom
        };

        IReadOnlyList<string> Updated = Validator.NormalizeSelectionOnDisable(
            PipelineStepIds.Precomp,
            Initial,
            Preflight);

        Assert.Equal(new string[] { PipelineStepIds.Beats, PipelineStepIds.BlurZoom }, Updated);
    }

    [Fact]
    public void NormalizeSelectionOnDisableZoomGroupWithoutTagsDisablesBlurZoom()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        string[] Initial = new string[]
        {
            PipelineStepIds.Precomp,
            PipelineStepIds.ZoomGroup,
            PipelineStepIds.BlurZoom
        };

        IReadOnlyList<string> Updated = Validator.NormalizeSelectionOnDisable(
            PipelineStepIds.ZoomGroup,
            Initial,
            null);

        Assert.Equal(new string[] { PipelineStepIds.Precomp }, Updated);
    }

    [Fact]
    public void NormalizeSelectionOnDisableZoomGroupWithZoomGroupTagsPreservesBlurZoom()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        ProjectPreflightState Preflight = new ProjectPreflightState
        {
            HasZoomGroupTags = true
        };
        string[] Initial = new string[]
        {
            PipelineStepIds.Precomp,
            PipelineStepIds.ZoomGroup,
            PipelineStepIds.BlurZoom
        };

        IReadOnlyList<string> Updated = Validator.NormalizeSelectionOnDisable(
            PipelineStepIds.ZoomGroup,
            Initial,
            Preflight);

        Assert.Equal(new string[] { PipelineStepIds.Precomp, PipelineStepIds.BlurZoom }, Updated);
    }

    [Fact]
    public void GetDependencyBadgeTextSynchronizesStartDirectionAcrossSteps04To06()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        PipelineSettings Settings = new PipelineSettings
        {
            StartDirection = ShakeStartDirection.Vertical
        };
        string[] Selected = new string[]
        {
            PipelineStepIds.Precomp,
            PipelineStepIds.Shakes,
            PipelineStepIds.StartBlur,
            PipelineStepIds.EndBlurs
        };

        string ShakesVertical = Validator.GetDependencyBadgeText(PipelineStepIds.Shakes, Selected, null, Settings);
        string StartBlurVertical = Validator.GetDependencyBadgeText(PipelineStepIds.StartBlur, Selected, null, Settings);
        string EndBlursVertical = Validator.GetDependencyBadgeText(PipelineStepIds.EndBlurs, Selected, null, Settings);

        Assert.Contains("Richtung synchron (Vertical)", ShakesVertical, StringComparison.Ordinal);
        Assert.Contains("Richtung synchron (Vertical)", StartBlurVertical, StringComparison.Ordinal);
        Assert.Contains("Richtung synchron (Vertical)", EndBlursVertical, StringComparison.Ordinal);

        Settings.StartDirection = ShakeStartDirection.Horizontal;

        string ShakesHorizontal = Validator.GetDependencyBadgeText(PipelineStepIds.Shakes, Selected, null, Settings);
        string StartBlurHorizontal = Validator.GetDependencyBadgeText(PipelineStepIds.StartBlur, Selected, null, Settings);
        string EndBlursHorizontal = Validator.GetDependencyBadgeText(PipelineStepIds.EndBlurs, Selected, null, Settings);

        Assert.Contains("Richtung synchron (Horizontal)", ShakesHorizontal, StringComparison.Ordinal);
        Assert.Contains("Richtung synchron (Horizontal)", StartBlurHorizontal, StringComparison.Ordinal);
        Assert.Contains("Richtung synchron (Horizontal)", EndBlursHorizontal, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDependencyBadgeTextReturnsExpectedGermanBadges()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        PipelineSettings Settings = new PipelineSettings();
        ProjectPreflightState TagPreflight = new ProjectPreflightState
        {
            HasEditClipTags = true,
            HasZoomGroupTags = true
        };

        Assert.Equal(
            "Standalone",
            Validator.GetDependencyBadgeText(PipelineStepIds.Beats, Array.Empty<string>(), null, Settings));
        Assert.Equal(
            "Benötigt Schritt 2",
            Validator.GetDependencyBadgeText(PipelineStepIds.Fade, new string[] { PipelineStepIds.Precomp }, null, Settings));
        Assert.Equal(
            "Benötigt Schritt 7",
            Validator.GetDependencyBadgeText(PipelineStepIds.BlurZoom, new string[] { PipelineStepIds.ZoomGroup }, null, Settings));
        Assert.Equal(
            "Nutzt bestehende Tags",
            Validator.GetDependencyBadgeText(PipelineStepIds.Fade, Array.Empty<string>(), TagPreflight, Settings));
        Assert.Equal(
            "Nutzt bestehende Tags",
            Validator.GetDependencyBadgeText(PipelineStepIds.BlurZoom, Array.Empty<string>(), TagPreflight, Settings));
    }

    [Fact]
    public void GetEffectiveRequiredPresetsReturnsExactPresetsForSelectedSteps()
    {
        StepDependencyValidator Validator = StepDependencyValidatorTests.CreateValidator();
        PipelineSettings Settings = new PipelineSettings();

        IReadOnlyList<string> NoPresetSteps = Validator.GetEffectiveRequiredPresets(
            new string[]
            {
                PipelineStepIds.Beats,
                PipelineStepIds.Precomp,
                PipelineStepIds.EndBlurs,
                PipelineStepIds.ZoomGroup
            },
            Settings);

        IReadOnlyList<string> FadeOnly = Validator.GetEffectiveRequiredPresets(
            new string[] { PipelineStepIds.Precomp, PipelineStepIds.Fade },
            Settings);

        IReadOnlyList<string> AllSteps = Validator.GetEffectiveRequiredPresets(
            new string[]
            {
                PipelineStepIds.Beats,
                PipelineStepIds.Precomp,
                PipelineStepIds.Fade,
                PipelineStepIds.Shakes,
                PipelineStepIds.StartBlur,
                PipelineStepIds.EndBlurs,
                PipelineStepIds.ZoomGroup,
                PipelineStepIds.BlurZoom
            },
            Settings);

        Assert.Empty(NoPresetSteps);
        Assert.Equal(new string[] { "FadeInOut" }, FadeOnly);
        Assert.Equal(
            new string[]
            {
                "FadeInOut",
                "VerticalShake",
                "HorizontalShake",
                "VerticalBlur",
                "HorizontalBlur",
                "BlurZoomIn",
                "BlurZoomOut"
            },
            AllSteps);
    }

    #endregion
}
