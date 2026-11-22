using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Steps;

namespace AEEditOrchestrator.Core.Engine;

public sealed class StepDependencyValidator
{
    #region Fields

    private readonly StepRegistry RegistryField;

    #endregion

    #region Constructors

    public StepDependencyValidator(StepRegistry Registry)
    {
        if (Registry == null)
        {
            throw new ArgumentNullException(nameof(Registry));
        }

        this.RegistryField = Registry;
    }

    #endregion

    #region Public Methods

    public IReadOnlyList<string> NormalizeSelectionOnEnable(
        string EnabledStepId,
        IReadOnlyCollection<string> CurrentSelectedStepIds,
        ProjectPreflightState? PreflightState)
    {
        HashSet<string> Selected = this.CreateNormalizedSelectableSet(CurrentSelectedStepIds);

        if (!string.IsNullOrWhiteSpace(EnabledStepId))
        {
            IPipelineStep? TargetStep = this.RegistryField.GetStep(EnabledStepId.Trim());
            if (TargetStep != null && !StepDependencyValidator.IsUtilityStep(TargetStep.StepId))
            {
                Selected.Add(TargetStep.StepId);
            }
        }

        bool HasStage1OrEditTags = StepDependencyValidator.HasStage1OrEditClipTags(PreflightState);
        bool HasZoomTags = StepDependencyValidator.HasZoomGroupTags(PreflightState);

        bool AddedAny = true;
        while (AddedAny)
        {
            AddedAny = false;
            List<string> Snapshot = new List<string>(Selected);

            foreach (string StepId in Snapshot)
            {
                if (StepDependencyValidator.RequiresStage1ClipOrPrecomp(StepId))
                {
                    if (!HasStage1OrEditTags && !Selected.Contains(PipelineStepIds.Precomp))
                    {
                        IPipelineStep? PrecompStep = this.RegistryField.GetStep(PipelineStepIds.Precomp);
                        if (PrecompStep != null)
                        {
                            if (Selected.Add(PrecompStep.StepId))
                            {
                                AddedAny = true;
                            }
                        }
                    }
                }
                else if (string.Equals(StepId, PipelineStepIds.BlurZoom, StringComparison.OrdinalIgnoreCase))
                {
                    if (!HasZoomTags && !Selected.Contains(PipelineStepIds.ZoomGroup))
                    {
                        IPipelineStep? ZoomGroupStep = this.RegistryField.GetStep(PipelineStepIds.ZoomGroup);
                        if (ZoomGroupStep != null)
                        {
                            if (Selected.Add(ZoomGroupStep.StepId))
                            {
                                AddedAny = true;
                            }
                        }
                    }
                }
                else
                {
                    IPipelineStep? Step = this.RegistryField.GetStep(StepId);
                    if (Step != null)
                    {
                        foreach (string RequiredId in Step.RequiredStepIds)
                        {
                            IPipelineStep? RequiredStep = this.RegistryField.GetStep(RequiredId);
                            if (RequiredStep != null && !StepDependencyValidator.IsUtilityStep(RequiredStep.StepId))
                            {
                                if (Selected.Add(RequiredStep.StepId))
                                {
                                    AddedAny = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        return this.SortSelectedStepIds(Selected);
    }

    public IReadOnlyList<string> NormalizeSelectionOnDisable(
        string DisabledStepId,
        IReadOnlyCollection<string> CurrentSelectedStepIds,
        ProjectPreflightState? PreflightState)
    {
        HashSet<string> Selected = this.CreateNormalizedSelectableSet(CurrentSelectedStepIds);

        if (!string.IsNullOrWhiteSpace(DisabledStepId))
        {
            Selected.Remove(DisabledStepId.Trim());
        }

        bool HasStage1OrEditTags = StepDependencyValidator.HasStage1OrEditClipTags(PreflightState);
        bool HasZoomTags = StepDependencyValidator.HasZoomGroupTags(PreflightState);

        bool RemovedAny = true;
        while (RemovedAny)
        {
            RemovedAny = false;
            List<string> Snapshot = new List<string>(Selected);

            foreach (string StepId in Snapshot)
            {
                if (StepDependencyValidator.RequiresStage1ClipOrPrecomp(StepId))
                {
                    if (!Selected.Contains(PipelineStepIds.Precomp) && !HasStage1OrEditTags)
                    {
                        if (Selected.Remove(StepId))
                        {
                            RemovedAny = true;
                        }
                    }
                }
                else if (string.Equals(StepId, PipelineStepIds.BlurZoom, StringComparison.OrdinalIgnoreCase))
                {
                    if (!Selected.Contains(PipelineStepIds.ZoomGroup) && !HasZoomTags)
                    {
                        if (Selected.Remove(StepId))
                        {
                            RemovedAny = true;
                        }
                    }
                }
                else
                {
                    IPipelineStep? Step = this.RegistryField.GetStep(StepId);
                    if (Step != null)
                    {
                        foreach (string RequiredId in Step.RequiredStepIds)
                        {
                            if (!Selected.Contains(RequiredId))
                            {
                                if (Selected.Remove(StepId))
                                {
                                    RemovedAny = true;
                                }

                                break;
                            }
                        }
                    }
                }
            }
        }

        return this.SortSelectedStepIds(Selected);
    }

    public string GetDependencyBadgeText(
        string StepId,
        IReadOnlyCollection<string> SelectedStepIds,
        ProjectPreflightState? PreflightState,
        PipelineSettings? Settings = null)
    {
        if (string.IsNullOrWhiteSpace(StepId))
        {
            return string.Empty;
        }

        HashSet<string> Selected = this.CreateNormalizedSelectableSet(SelectedStepIds);
        bool HasPrecompSelected = Selected.Contains(PipelineStepIds.Precomp);
        bool HasZoomGroupSelected = Selected.Contains(PipelineStepIds.ZoomGroup);
        bool HasStage1OrEditTags = StepDependencyValidator.HasStage1OrEditClipTags(PreflightState);
        bool HasZoomTags = StepDependencyValidator.HasZoomGroupTags(PreflightState);

        string TrimmedId = StepId.Trim();

        if (string.Equals(TrimmedId, PipelineStepIds.Beats, StringComparison.OrdinalIgnoreCase))
        {
            return "Standalone";
        }

        if (string.Equals(TrimmedId, PipelineStepIds.Precomp, StringComparison.OrdinalIgnoreCase))
        {
            if (HasStage1OrEditTags)
            {
                return "Basis-Schritt (Tags vorhanden)";
            }

            return "Basis-Schritt";
        }

        if (string.Equals(TrimmedId, PipelineStepIds.Fade, StringComparison.OrdinalIgnoreCase)
            || string.Equals(TrimmedId, PipelineStepIds.ZoomGroup, StringComparison.OrdinalIgnoreCase))
        {
            if (!HasPrecompSelected && HasStage1OrEditTags)
            {
                return "Nutzt bestehende Tags";
            }

            return "Benötigt Schritt 2";
        }

        if (StepDependencyValidator.UsesSharedStartDirection(TrimmedId))
        {
            string PrerequisitePart = "Benötigt Schritt 2";
            if (!HasPrecompSelected && HasStage1OrEditTags)
            {
                PrerequisitePart = "Nutzt bestehende Tags";
            }

            if (Settings != null)
            {
                string DirectionText = "Vertical";
                if (Settings.StartDirection == ShakeStartDirection.Horizontal)
                {
                    DirectionText = "Horizontal";
                }

                return $"{PrerequisitePart} · Richtung synchron ({DirectionText})";
            }

            return $"{PrerequisitePart} · Richtung synchron";
        }

        if (string.Equals(TrimmedId, PipelineStepIds.BlurZoom, StringComparison.OrdinalIgnoreCase))
        {
            if (!HasZoomGroupSelected && HasZoomTags)
            {
                return "Nutzt bestehende Tags";
            }

            return "Benötigt Schritt 7";
        }

        if (string.Equals(TrimmedId, PipelineStepIds.Preflight, StringComparison.OrdinalIgnoreCase))
        {
            return "Automatischer Vorab-Check";
        }

        if (string.Equals(TrimmedId, PipelineStepIds.Reverse, StringComparison.OrdinalIgnoreCase))
        {
            return "Recovery-Tool";
        }

        IPipelineStep? RegisteredStep = this.RegistryField.GetStep(TrimmedId);
        if (RegisteredStep == null)
        {
            return string.Empty;
        }

        if (RegisteredStep.RequiredStepIds.Count == 0)
        {
            return "Standalone";
        }

        return $"Benötigt {string.Join(", ", RegisteredStep.RequiredStepIds)}";
    }

    public IReadOnlyList<string> GetEffectiveRequiredPresets(
        IReadOnlyCollection<string> SelectedStepIds,
        PipelineSettings Settings)
    {
        if (SelectedStepIds == null || Settings == null)
        {
            return Array.Empty<string>();
        }

        HashSet<string> SelectedSet = this.CreateNormalizedSelectableSet(SelectedStepIds);
        List<string> OrderedIds = this.SortSelectedStepIds(SelectedSet);
        List<string> Presets = new List<string>();
        HashSet<string> SeenPresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string StepId in OrderedIds)
        {
            IPipelineStep? Step = this.RegistryField.GetStep(StepId);
            if (Step is BasePipelineStep BaseStep)
            {
                IReadOnlyList<string> StepPresets = BaseStep.GetRequiredPresets(Settings);
                foreach (string PresetName in StepPresets)
                {
                    if (!string.IsNullOrWhiteSpace(PresetName))
                    {
                        string TrimmedPreset = PresetName.Trim();
                        if (SeenPresets.Add(TrimmedPreset))
                        {
                            Presets.Add(TrimmedPreset);
                        }
                    }
                }
            }
        }

        return Presets.AsReadOnly();
    }

    public ValidationResult ValidateSelection(
        IReadOnlyCollection<string> SelectedStepIds,
        PipelineSettings Settings,
        ProjectPreflightState? PreflightState)
    {
        if (Settings == null)
        {
            throw new ArgumentNullException(nameof(Settings));
        }

        if (SelectedStepIds == null || SelectedStepIds.Count == 0)
        {
            return ValidationResult.Failure("Mindestens ein gültiger Pipeline-Schritt muss ausgewählt sein.");
        }

        List<string> Errors = new List<string>();
        List<string> Warnings = new List<string>();
        HashSet<string> SelectedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<IPipelineStep> SelectedSteps = new List<IPipelineStep>();

        foreach (string RawId in SelectedStepIds)
        {
            if (string.IsNullOrWhiteSpace(RawId))
            {
                continue;
            }

            string TrimmedId = RawId.Trim();
            IPipelineStep? Step = this.RegistryField.GetStep(TrimmedId);
            if (Step == null)
            {
                StepDependencyValidator.AddUniqueMessage(Errors, $"Unbekannter Pipeline-Schritt: '{TrimmedId}'.");
                continue;
            }

            if (StepDependencyValidator.IsUtilityStep(Step.StepId))
            {
                StepDependencyValidator.AddUniqueMessage(
                    Errors,
                    $"Der System-Schritt '{Step.StepId}' kann nicht direkt als Pipeline-Schritt ausgewählt werden.");
                continue;
            }

            if (SelectedSet.Add(Step.StepId))
            {
                SelectedSteps.Add(Step);
            }
        }

        if (SelectedSteps.Count == 0)
        {
            if (Errors.Count == 0)
            {
                Errors.Add("Mindestens ein gültiger Pipeline-Schritt muss ausgewählt sein.");
            }

            return ValidationResult.Failure(Errors, Warnings, Errors[0]);
        }

        SelectedSteps.Sort(StepRegistry.CompareStepsByExecutionOrder);

        if (!Enum.IsDefined(typeof(ShakeStartDirection), Settings.StartDirection))
        {
            StepDependencyValidator.AddUniqueMessage(Errors, "StartDirection hat einen ungültigen Wert.");
        }

        ProjectPreflightState EffectivePreflight = new ProjectPreflightState();
        if (PreflightState != null)
        {
            EffectivePreflight = PreflightState;
        }

        bool HasStage1OrEditTags = StepDependencyValidator.HasStage1OrEditClipTags(EffectivePreflight);
        bool HasZoomTags = StepDependencyValidator.HasZoomGroupTags(EffectivePreflight);

        PipelineSettings EffectiveSettings = PipelineSettingsCloner.Clone(Settings, SelectedSet);

        foreach (IPipelineStep Step in SelectedSteps)
        {
            bool DependencyErrorAdded = false;

            if (StepDependencyValidator.RequiresStage1ClipOrPrecomp(Step.StepId))
            {
                if (!SelectedSet.Contains(PipelineStepIds.Precomp) && !HasStage1OrEditTags)
                {
                    StepDependencyValidator.AddUniqueMessage(
                        Errors,
                        $"Schritt '{Step.DisplayName}' ({Step.StepId}) benötigt '{PipelineStepIds.Precomp}' oder bestehende [EDIT_CLIP:X]-Tags.");
                    DependencyErrorAdded = true;
                }
            }
            else if (string.Equals(Step.StepId, PipelineStepIds.BlurZoom, StringComparison.OrdinalIgnoreCase))
            {
                if (!SelectedSet.Contains(PipelineStepIds.ZoomGroup) && !HasZoomTags)
                {
                    StepDependencyValidator.AddUniqueMessage(
                        Errors,
                        $"Schritt '{Step.DisplayName}' ({Step.StepId}) benötigt '{PipelineStepIds.ZoomGroup}' oder bestehende [DONE_ZOOM_GROUP]-Tags.");
                    DependencyErrorAdded = true;
                }
            }
            else if (!StepDependencyValidator.IsKnownStandardStep(Step.StepId))
            {
                foreach (string RequiredId in Step.RequiredStepIds)
                {
                    if (!SelectedSet.Contains(RequiredId))
                    {
                        StepDependencyValidator.AddUniqueMessage(
                            Errors,
                            $"Schritt '{Step.DisplayName}' ({Step.StepId}) benötigt den Schritt '{RequiredId}'.");
                        DependencyErrorAdded = true;
                    }
                }
            }

            ValidationResult StepValidation = Step.ValidateSettings(EffectiveSettings, EffectivePreflight);
            if (!StepValidation.IsValid)
            {
                foreach (string StepError in StepValidation.Errors)
                {
                    if (DependencyErrorAdded && StepDependencyValidator.IsPrerequisiteReferenceError(StepError))
                    {
                        continue;
                    }

                    StepDependencyValidator.AddUniqueMessage(Errors, StepError);
                }
            }

            foreach (string StepWarning in StepValidation.Warnings)
            {
                StepDependencyValidator.AddUniqueMessage(Warnings, StepWarning);
            }
        }

        if (EffectivePreflight.MissingPresets != null && EffectivePreflight.MissingPresets.Count > 0)
        {
            IReadOnlyList<string> RequiredPresets = this.GetEffectiveRequiredPresets(SelectedSet, Settings);
            List<string> MissingForSelection = new List<string>();

            foreach (string RequiredPreset in RequiredPresets)
            {
                foreach (string MissingPreset in EffectivePreflight.MissingPresets)
                {
                    if (string.Equals(RequiredPreset, MissingPreset, StringComparison.OrdinalIgnoreCase))
                    {
                        MissingForSelection.Add(RequiredPreset);
                        break;
                    }
                }
            }

            if (MissingForSelection.Count > 0)
            {
                Warnings.Add($"Folgende benötigte Presets fehlen laut Preflight: {string.Join(", ", MissingForSelection)}.");
            }
        }

        if (Errors.Count > 0)
        {
            return ValidationResult.Failure(Errors, Warnings, Errors[0]);
        }

        ValidationResult Result = ValidationResult.Success($"{SelectedSteps.Count} Schritt(e) bereit");
        Result.Warnings = Warnings;
        return Result;
    }

    #endregion

    #region Private Methods

    private HashSet<string> CreateNormalizedSelectableSet(IReadOnlyCollection<string>? StepIds)
    {
        HashSet<string> Result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (StepIds == null)
        {
            return Result;
        }

        foreach (string RawId in StepIds)
        {
            if (string.IsNullOrWhiteSpace(RawId))
            {
                continue;
            }

            string TrimmedId = RawId.Trim();
            IPipelineStep? Step = this.RegistryField.GetStep(TrimmedId);
            if (Step != null && !StepDependencyValidator.IsUtilityStep(Step.StepId))
            {
                Result.Add(Step.StepId);
            }
        }

        return Result;
    }

    private List<string> SortSelectedStepIds(HashSet<string> SelectedSet)
    {
        List<string> OrderedIds = new List<string>();
        IReadOnlyList<IPipelineStep> SelectableSteps = this.RegistryField.GetSelectablePipelineSteps();

        foreach (IPipelineStep Step in SelectableSteps)
        {
            if (SelectedSet.Contains(Step.StepId))
            {
                OrderedIds.Add(Step.StepId);
            }
        }

        return OrderedIds;
    }

    private static bool IsUtilityStep(string StepId)
    {
        return string.Equals(StepId, PipelineStepIds.Preflight, StringComparison.OrdinalIgnoreCase)
            || string.Equals(StepId, PipelineStepIds.Reverse, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresStage1ClipOrPrecomp(string StepId)
    {
        return string.Equals(StepId, PipelineStepIds.Fade, StringComparison.OrdinalIgnoreCase)
            || string.Equals(StepId, PipelineStepIds.Shakes, StringComparison.OrdinalIgnoreCase)
            || string.Equals(StepId, PipelineStepIds.StartBlur, StringComparison.OrdinalIgnoreCase)
            || string.Equals(StepId, PipelineStepIds.EndBlurs, StringComparison.OrdinalIgnoreCase)
            || string.Equals(StepId, PipelineStepIds.ZoomGroup, StringComparison.OrdinalIgnoreCase);
    }

    private static bool UsesSharedStartDirection(string StepId)
    {
        return string.Equals(StepId, PipelineStepIds.Shakes, StringComparison.OrdinalIgnoreCase)
            || string.Equals(StepId, PipelineStepIds.StartBlur, StringComparison.OrdinalIgnoreCase)
            || string.Equals(StepId, PipelineStepIds.EndBlurs, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownStandardStep(string StepId)
    {
        return string.Equals(StepId, PipelineStepIds.Beats, StringComparison.OrdinalIgnoreCase)
            || string.Equals(StepId, PipelineStepIds.Precomp, StringComparison.OrdinalIgnoreCase)
            || StepDependencyValidator.RequiresStage1ClipOrPrecomp(StepId)
            || string.Equals(StepId, PipelineStepIds.BlurZoom, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasStage1OrEditClipTags(ProjectPreflightState? PreflightState)
    {
        if (PreflightState == null)
        {
            return false;
        }

        return PreflightState.HasEditClipTags || PreflightState.HasStage1Tags;
    }

    private static bool HasZoomGroupTags(ProjectPreflightState? PreflightState)
    {
        if (PreflightState == null)
        {
            return false;
        }

        return PreflightState.HasZoomGroupTags;
    }

    private static void AddUniqueMessage(List<string> TargetList, string Message)
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            return;
        }

        foreach (string Existing in TargetList)
        {
            if (string.Equals(Existing, Message, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        TargetList.Add(Message);
    }

    private static bool IsPrerequisiteReferenceError(string StepError)
    {
        return StepError.Contains(PipelineStepIds.Precomp, StringComparison.OrdinalIgnoreCase)
            || StepError.Contains(PipelineStepIds.ZoomGroup, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
