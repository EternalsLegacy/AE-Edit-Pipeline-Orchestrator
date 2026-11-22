using AEEditOrchestrator.App.ViewModels;

namespace AEEditOrchestrator.App.Views;

public partial class PipelinePage : ContentPage
{
    #region Fields

    private readonly PipelineViewModel ViewModelField;

    #endregion

    #region Constructors

    public PipelinePage(PipelineViewModel ViewModel)
    {
        this.InitializeComponent();
        this.ViewModelField = ViewModel;
        this.BindingContext = ViewModel;
    }

    #endregion

    #region Protected Methods

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await this.ViewModelField.InitializeAsync();
    }

    #endregion
}
