using AEEditOrchestrator.App.ViewModels;

namespace AEEditOrchestrator.App.Views;

public partial class RecoveryPage : ContentPage
{
    #region Fields

    private readonly RecoveryViewModel ViewModelField;

    #endregion

    #region Constructors

    public RecoveryPage(RecoveryViewModel ViewModel)
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
