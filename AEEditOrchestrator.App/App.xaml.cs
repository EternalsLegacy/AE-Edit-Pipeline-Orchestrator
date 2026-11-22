namespace AEEditOrchestrator.App;

public partial class App : Application
{
    #region Constructors

    public App()
    {
        this.InitializeComponent();
        this.UserAppTheme = AppTheme.Dark;
    }

    #endregion

    #region Protected Methods

    protected override Window CreateWindow(IActivationState? ActivationState)
    {
        Window AppWindow = new Window(new AppShell());
        AppWindow.Title = "AE Edit Pipeline Orchestrator";
        AppWindow.Width = 980;
        AppWindow.Height = 680;
        AppWindow.MinimumWidth = 900;
        AppWindow.MinimumHeight = 620;
        return AppWindow;
    }

    #endregion
}
