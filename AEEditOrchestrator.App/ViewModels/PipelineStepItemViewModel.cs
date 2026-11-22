using CommunityToolkit.Mvvm.ComponentModel;

namespace AEEditOrchestrator.App.ViewModels;

public sealed class PipelineStepItemViewModel : ObservableObject
{
    #region Fields

    private readonly Action<PipelineStepItemViewModel> SelectionToggledCallback;
    private string StepIdField = string.Empty;
    private string DisplayNameField = string.Empty;
    private string DescriptionField = string.Empty;
    private int ExecutionOrderField = 0;
    private bool IsSelectedField = false;
    private string DependencyBadgeTextField = string.Empty;
    private string BadgeKindField = "Default";
    private bool IsSelectionEnabledField = true;

    #endregion

    #region Properties

    public string StepId
    {
        get
        {
            return this.StepIdField;
        }
        set
        {
            this.SetProperty(ref this.StepIdField, value ?? string.Empty);
        }
    }

    public string DisplayName
    {
        get
        {
            return this.DisplayNameField;
        }
        set
        {
            this.SetProperty(ref this.DisplayNameField, value ?? string.Empty);
        }
    }

    public string Description
    {
        get
        {
            return this.DescriptionField;
        }
        set
        {
            this.SetProperty(ref this.DescriptionField, value ?? string.Empty);
        }
    }

    public int ExecutionOrder
    {
        get
        {
            return this.ExecutionOrderField;
        }
        set
        {
            this.SetProperty(ref this.ExecutionOrderField, value);
        }
    }

    public bool IsSelected
    {
        get
        {
            return this.IsSelectedField;
        }
        set
        {
            bool Changed = this.SetProperty(ref this.IsSelectedField, value);
            if (Changed)
            {
                this.SelectionToggledCallback(this);
            }
        }
    }

    public string DependencyBadgeText
    {
        get
        {
            return this.DependencyBadgeTextField;
        }
        private set
        {
            this.SetProperty(ref this.DependencyBadgeTextField, value ?? string.Empty);
        }
    }

    public string BadgeKind
    {
        get
        {
            return this.BadgeKindField;
        }
        private set
        {
            this.SetProperty(ref this.BadgeKindField, value ?? "Default");
        }
    }

    public bool IsSelectionEnabled
    {
        get
        {
            return this.IsSelectionEnabledField;
        }
        set
        {
            this.SetProperty(ref this.IsSelectionEnabledField, value);
        }
    }

    #endregion

    #region Constructors

    public PipelineStepItemViewModel(
        string StepId,
        string DisplayName,
        string Description,
        int ExecutionOrder,
        bool IsSelected,
        Action<PipelineStepItemViewModel> SelectionToggledCallback)
    {
        this.StepIdField = StepId;
        this.DisplayNameField = DisplayName;
        this.DescriptionField = Description;
        this.ExecutionOrderField = ExecutionOrder;
        this.IsSelectedField = IsSelected;
        this.SelectionToggledCallback = SelectionToggledCallback;
    }

    #endregion

    #region Public Methods

    public void SetSelectedWithoutCallback(bool Selected)
    {
        this.SetProperty(ref this.IsSelectedField, Selected, nameof(this.IsSelected));
    }

    public void UpdateBadge(string BadgeText)
    {
        string NormalizedText = BadgeText ?? string.Empty;
        this.DependencyBadgeText = NormalizedText;

        if (NormalizedText.Contains("Tags", StringComparison.OrdinalIgnoreCase))
        {
            this.BadgeKind = "TagBypass";
        }
        else if (NormalizedText.Contains("synchron", StringComparison.OrdinalIgnoreCase))
        {
            this.BadgeKind = "Sync";
        }
        else if (NormalizedText.Contains("Benötigt", StringComparison.OrdinalIgnoreCase)
            || NormalizedText.Contains("Fehlt", StringComparison.OrdinalIgnoreCase)
            || NormalizedText.Contains("erforderlich", StringComparison.OrdinalIgnoreCase))
        {
            this.BadgeKind = "Warning";
        }
        else if (NormalizedText.Contains("Standalone", StringComparison.OrdinalIgnoreCase))
        {
            this.BadgeKind = "Standalone";
        }
        else
        {
            this.BadgeKind = "Default";
        }
    }

    #endregion
}
