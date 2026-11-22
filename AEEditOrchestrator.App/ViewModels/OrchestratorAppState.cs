using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;

namespace AEEditOrchestrator.App.ViewModels;

public sealed class OrchestratorAppState
{
    #region Fields

    private readonly ISettingsStore SettingsStoreField;
    private readonly SemaphoreSlim InitSemaphoreField = new SemaphoreSlim(1, 1);
    private PipelineSettings CurrentSettingsField = new PipelineSettings();
    private ProjectPreflightState LastPreflightStateField = new ProjectPreflightState();
    private bool IsInitializedField = false;

    #endregion

    #region Events

    public event EventHandler? SettingsUpdated;
    public event EventHandler? PreflightStateUpdated;

    #endregion

    #region Properties

    public PipelineSettings CurrentSettings
    {
        get
        {
            return this.CurrentSettingsField;
        }
        set
        {
            this.CurrentSettingsField = value ?? new PipelineSettings();
        }
    }

    public ProjectPreflightState LastPreflightState
    {
        get
        {
            return this.LastPreflightStateField;
        }
        set
        {
            this.LastPreflightStateField = value ?? new ProjectPreflightState();
        }
    }

    public bool IsInitialized
    {
        get
        {
            return this.IsInitializedField;
        }
    }

    #endregion

    #region Constructors

    public OrchestratorAppState(ISettingsStore SettingsStore)
    {
        this.SettingsStoreField = SettingsStore;
    }

    #endregion

    #region Public Methods

    public async Task EnsureInitializedAsync(CancellationToken CancellationToken = default)
    {
        if (this.IsInitializedField)
        {
            return;
        }

        await this.InitSemaphoreField.WaitAsync(CancellationToken);
        try
        {
            if (this.IsInitializedField)
            {
                return;
            }

            PipelineSettings LoadedSettings = await this.SettingsStoreField.LoadAsync(CancellationToken);
            this.CurrentSettingsField = LoadedSettings ?? new PipelineSettings();
            this.IsInitializedField = true;
        }
        finally
        {
            this.InitSemaphoreField.Release();
        }
    }

    public async Task SaveSettingsAsync(CancellationToken CancellationToken = default)
    {
        await this.SettingsStoreField.SaveAsync(this.CurrentSettingsField, CancellationToken);
        this.NotifySettingsUpdated();
    }

    public void NotifySettingsUpdated()
    {
        EventHandler? Handler = this.SettingsUpdated;
        if (Handler != null)
        {
            Handler(this, EventArgs.Empty);
        }
    }

    public void UpdatePreflightState(ProjectPreflightState State)
    {
        this.LastPreflightStateField = State ?? new ProjectPreflightState();
        EventHandler? Handler = this.PreflightStateUpdated;
        if (Handler != null)
        {
            Handler(this, EventArgs.Empty);
        }
    }

    #endregion
}
