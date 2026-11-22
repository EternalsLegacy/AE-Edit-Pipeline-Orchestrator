using AEEditOrchestrator.App.ViewModels;

namespace AEEditOrchestrator.App.Views;

public partial class SettingsPage : ContentPage
{
    #region Fields

    private readonly SettingsViewModel ViewModelField;

    #endregion

    #region Constructors

    public SettingsPage(SettingsViewModel ViewModel)
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
