namespace AEEditOrchestrator.App.WinUI;

public partial class App : MauiWinUIApplication
{
    #region Constructors

    public App()
    {
        this.InitializeComponent();
    }

    #endregion

    #region Protected Methods

    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }

    #endregion
}
