using System.Collections.ObjectModel;
using AEEditOrchestrator.Core.Engine;
using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace AEEditOrchestrator.App.ViewModels;

public sealed partial class RecoveryViewModel : ObservableObject
{
    #region Constants

    private const string ReverseStage2Label = "Stufe 2 (BlurZoom)";
    private const string ReverseStage1Label = "Stufe 1 (Precomp & Blurs)";
    private const string ReverseFullLabel = "Komplett (Stufe 2 + 1)";

    #endregion

    #region Fields

    private readonly OrchestratorAppState AppStateField;
    private readonly ITransactionManager TransactionManagerField;
    private readonly PipelineOrchestrator OrchestratorField;

    private readonly ObservableCollection<SnapshotItemViewModel> SnapshotsField = new ObservableCollection<SnapshotItemViewModel>();
    private readonly IReadOnlyList<string> ReverseModeOptionsField = new List<string>
    {
        ReverseStage2Label,
        ReverseStage1Label,
        ReverseFullLabel
    };

    private string ProjectFilePathField = string.Empty;
    private string LastPreflightProjectPathField = string.Empty;
    private string LastLoadedProjectPathField = string.Empty;
    private bool NeedsSnapshotReloadField = false;
    private string SelectedReverseModeOptionField = ReverseStage2Label;
    private bool IsBusyField = false;
    private bool HasStatusMessageField = false;
    private bool IsStatusErrorField = false;
    private string StatusMessageField = string.Empty;

    #endregion

    #region Properties

    public ObservableCollection<SnapshotItemViewModel> Snapshots
    {
        get
        {
            return this.SnapshotsField;
        }
    }

    public IReadOnlyList<string> ReverseModeOptions
    {
        get
        {
            return this.ReverseModeOptionsField;
        }
    }

    public string ProjectFilePath
    {
        get
        {
            return this.ProjectFilePathField;
        }
        set
        {
            if (this.SetProperty(ref this.ProjectFilePathField, value ?? string.Empty))
            {
                this.LoadSnapshotsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SelectedReverseModeOption
    {
        get
        {
            return this.SelectedReverseModeOptionField;
        }
        set
        {
            this.SetProperty(ref this.SelectedReverseModeOptionField, value ?? ReverseStage2Label);
        }
    }

    public bool IsBusy
    {
        get
        {
            return this.IsBusyField;
        }
        private set
        {
            if (this.SetProperty(ref this.IsBusyField, value))
            {
                this.OnPropertyChanged(nameof(this.IsNotBusy));
                this.LoadSnapshotsCommand.NotifyCanExecuteChanged();
                this.BrowseProjectFileCommand.NotifyCanExecuteChanged();
                this.RestoreSnapshotCommand.NotifyCanExecuteChanged();
                this.ExecuteReverseRollbackCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy
    {
        get
        {
            return !this.IsBusyField;
        }
    }

    public bool HasNoSnapshots
    {
        get
        {
            return this.SnapshotsField.Count == 0;
        }
    }

    public bool HasStatusMessage
    {
        get
        {
            return this.HasStatusMessageField;
        }
        private set
        {
            this.SetProperty(ref this.HasStatusMessageField, value);
        }
    }

    public bool IsStatusError
    {
        get
        {
            return this.IsStatusErrorField;
        }
        private set
        {
            this.SetProperty(ref this.IsStatusErrorField, value);
        }
    }

    public string StatusMessage
    {
        get
        {
            return this.StatusMessageField;
        }
        private set
        {
            this.SetProperty(ref this.StatusMessageField, value ?? string.Empty);
        }
    }

    #endregion

    #region Constructors

    public RecoveryViewModel(
        OrchestratorAppState AppState,
        ITransactionManager TransactionManager,
        PipelineOrchestrator Orchestrator)
    {
        this.AppStateField = AppState;
        this.TransactionManagerField = TransactionManager;
        this.OrchestratorField = Orchestrator;
        this.AppStateField.PreflightStateUpdated += this.OnPreflightStateUpdated;
    }

    #endregion

    #region Public Methods

    public async Task InitializeAsync()
    {
        await this.AppStateField.EnsureInitializedAsync();

        string PreflightPath = this.AppStateField.LastPreflightState.ProjectPath;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!string.IsNullOrWhiteSpace(PreflightPath)
                && (string.IsNullOrWhiteSpace(this.ProjectFilePathField)
                    || !string.Equals(PreflightPath, this.LastPreflightProjectPathField, StringComparison.OrdinalIgnoreCase)))
            {
                this.LastPreflightProjectPathField = PreflightPath;
                this.ProjectFilePath = PreflightPath;
                this.NeedsSnapshotReloadField = true;
            }
        });

        bool ShouldLoad = !string.IsNullOrWhiteSpace(this.ProjectFilePathField)
            && (this.NeedsSnapshotReloadField
                || !string.Equals(this.ProjectFilePathField.Trim(), this.LastLoadedProjectPathField, StringComparison.OrdinalIgnoreCase));

        if (ShouldLoad && !this.IsBusyField)
        {
            await this.LoadSnapshotsAsync();
        }
    }

    #endregion

    #region Private Methods

    private void OnPreflightStateUpdated(object? Sender, EventArgs Args)
    {
        string PreflightPath = this.AppStateField.LastPreflightState.ProjectPath;
        if (string.IsNullOrWhiteSpace(PreflightPath))
        {
            return;
        }

        this.RunOnMainThread(() =>
        {
            this.LastPreflightProjectPathField = PreflightPath;
            this.ProjectFilePath = PreflightPath;
            this.NeedsSnapshotReloadField = true;
        });
    }

    private bool CanBrowseProjectFile()
    {
        return !this.IsBusyField;
    }

    [RelayCommand(CanExecute = nameof(CanBrowseProjectFile))]
    private async Task BrowseProjectFileAsync()
    {
        try
        {
            Dictionary<DevicePlatform, IEnumerable<string>> FileTypesByPlatform = new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new List<string> { ".aep" } }
            };
            FilePickerFileType AepFileType = new FilePickerFileType(FileTypesByPlatform);
            PickOptions Options = new PickOptions
            {
                PickerTitle = "After Effects Projekt (.aep) wählen",
                FileTypes = AepFileType
            };

            FileResult? PickedFile = await FilePicker.Default.PickAsync(Options);
            if (PickedFile != null && !string.IsNullOrWhiteSpace(PickedFile.FullPath))
            {
                this.ProjectFilePath = PickedFile.FullPath;
                await this.LoadSnapshotsAsync();
            }
        }
        catch (Exception Ex)
        {
            this.SetStatus($"Dateiauswahl fehlgeschlagen: {Ex.Message}", true);
        }
    }

    private bool CanLoadSnapshots()
    {
        return !this.IsBusyField && !string.IsNullOrWhiteSpace(this.ProjectFilePathField);
    }

    [RelayCommand(CanExecute = nameof(CanLoadSnapshots))]
    private async Task LoadSnapshotsAsync()
    {
        string TargetPath = this.ProjectFilePathField.Trim();
        if (string.IsNullOrWhiteSpace(TargetPath))
        {
            this.SetStatus("Bitte zuerst einen .aep-Projektpfad angeben oder Preflight prüfen.", true);
            return;
        }

        this.IsBusy = true;
        try
        {
            IReadOnlyList<TransactionManifest> Manifests = await this.TransactionManagerField.ListSnapshotsAsync(TargetPath);
            List<SnapshotItemViewModel> LoadedItems = new List<SnapshotItemViewModel>();

            foreach (TransactionManifest Manifest in Manifests)
            {
                bool IsValid = await this.TransactionManagerField.VerifySnapshotIntegrityAsync(Manifest);
                LoadedItems.Add(new SnapshotItemViewModel(Manifest, IsValid));
            }

            this.RunOnMainThread(() =>
            {
                this.SnapshotsField.Clear();
                foreach (SnapshotItemViewModel Item in LoadedItems)
                {
                    this.SnapshotsField.Add(Item);
                }

                this.LastLoadedProjectPathField = TargetPath;
                this.NeedsSnapshotReloadField = false;
                this.OnPropertyChanged(nameof(this.HasNoSnapshots));

                if (LoadedItems.Count == 0)
                {
                    this.SetStatus("Keine Snapshots im Ordner _AEP_Backups gefunden.", false);
                }
                else
                {
                    this.SetStatus($"{LoadedItems.Count} Snapshot(s) aus _AEP_Backups geladen.", false);
                }
            });
        }
        catch (Exception Ex)
        {
            this.RunOnMainThread(() =>
            {
                this.SetStatus($"Laden der Snapshots fehlgeschlagen: {Ex.Message}", true);
            });
        }
        finally
        {
            this.RunOnMainThread(() =>
            {
                this.IsBusy = false;
            });
        }
    }

    private bool CanRestoreSnapshot(SnapshotItemViewModel? Item)
    {
        return !this.IsBusyField && Item != null;
    }

    [RelayCommand(CanExecute = nameof(CanRestoreSnapshot))]
    private async Task RestoreSnapshotAsync(SnapshotItemViewModel? Item)
    {
        if (Item == null)
        {
            return;
        }

        if (!Item.IsIntegrityValid)
        {
            this.SetStatus($"Snapshot {Item.TransactionId} hat eine ungültige SHA-256-Prüfsumme und kann nicht wiederhergestellt werden.", true);
            return;
        }

        await this.AppStateField.EnsureInitializedAsync();

        this.IsBusy = true;
        this.SetStatus($"Stelle Snapshot {Item.TransactionId} wieder her...", false);

        try
        {
            bool Restored = await this.TransactionManagerField.RestoreSnapshotAsync(
                Item.Manifest,
                this.AppStateField.CurrentSettings);

            this.RunOnMainThread(() =>
            {
                if (Restored)
                {
                    this.SetStatus($"Snapshot {Item.TransactionId} erfolgreich wiederhergestellt und in AE geöffnet.", false);
                }
                else
                {
                    this.SetStatus($"Wiederherstellung von Snapshot {Item.TransactionId} fehlgeschlagen.", true);
                }
            });
        }
        catch (Exception Ex)
        {
            this.RunOnMainThread(() =>
            {
                this.SetStatus($"Fehler bei Wiederherstellung: {Ex.Message}", true);
            });
        }
        finally
        {
            this.RunOnMainThread(() =>
            {
                this.IsBusy = false;
            });
        }
    }

    private bool CanExecuteReverseRollback()
    {
        return !this.IsBusyField;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteReverseRollback))]
    private async Task ExecuteReverseRollbackAsync()
    {
        await this.AppStateField.EnsureInitializedAsync();

        ReverseRollbackMode Mode = this.ResolveReverseRollbackMode();
        this.AppStateField.CurrentSettings.ReverseMode = Mode;

        this.IsBusy = true;
        this.SetStatus($"Führe 99_ReverseRollback ({this.SelectedReverseModeOptionField}) in After Effects aus...", false);

        IProgress<PipelineProgressUpdate> ProgressReporter = new Progress<PipelineProgressUpdate>(
            (PipelineProgressUpdate Update) =>
            {
                this.RunOnMainThread(() =>
                {
                    if (!string.IsNullOrWhiteSpace(Update.Message))
                    {
                        this.SetStatus(Update.Message, Update.IsError);
                    }
                });
            });

        try
        {
            StepExecutionResult Result = await this.OrchestratorField.ExecuteReverseRollbackAsync(
                this.AppStateField.CurrentSettings,
                Mode,
                ProgressReporter,
                CancellationToken.None);

            this.RunOnMainThread(() =>
            {
                if (Result.Succeeded)
                {
                    string Detail = string.IsNullOrWhiteSpace(Result.Message)
                        ? $"99_ReverseRollback abgeschlossen ({Result.ProcessedClips} Clips entpackt)."
                        : Result.Message;
                    this.SetStatus(Detail, false);
                }
                else
                {
                    string ErrorText = string.IsNullOrWhiteSpace(Result.ErrorMessage)
                        ? "Reverse-Rollback fehlgeschlagen."
                        : Result.ErrorMessage;
                    this.SetStatus(ErrorText, true);
                }
            });
        }
        catch (Exception Ex)
        {
            this.RunOnMainThread(() =>
            {
                this.SetStatus($"Reverse-Rollback Fehler: {Ex.Message}", true);
            });
        }
        finally
        {
            this.RunOnMainThread(() =>
            {
                this.IsBusy = false;
            });
        }
    }

    private ReverseRollbackMode ResolveReverseRollbackMode()
    {
        if (string.Equals(this.SelectedReverseModeOptionField, ReverseStage1Label, StringComparison.Ordinal))
        {
            return ReverseRollbackMode.Stage1Only;
        }

        if (string.Equals(this.SelectedReverseModeOptionField, ReverseFullLabel, StringComparison.Ordinal))
        {
            return ReverseRollbackMode.Full;
        }

        return ReverseRollbackMode.Stage2Only;
    }

    private void SetStatus(string Message, bool IsError)
    {
        this.StatusMessage = Message;
        this.IsStatusError = IsError;
        this.HasStatusMessage = !string.IsNullOrWhiteSpace(Message);
    }

    private void RunOnMainThread(Action ActionToRun)
    {
        MainThread.BeginInvokeOnMainThread(ActionToRun);
    }

    #endregion
}
