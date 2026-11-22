namespace AEEditOrchestrator.Infrastructure.AfterEffects;

public sealed class IpcPaths
{
    #region Constants

    private const string AppFolderName = "AEEditOrchestrator";
    private const string IpcFolderName = "ipc";
    private const string JobFileName = "job.json";
    private const string HeartbeatFileName = "heartbeat.json";
    private const string ResultFileName = "result.json";
    private const string AbortFlagFileName = "abort.flag";
    private const string RunnerScriptFileName = "runner.jsx";
    private const string CloseNoSaveScriptFileName = "close_no_save.jsx";

    #endregion

    #region Fields

    private readonly string BaseDirectoryField;
    private readonly string JsxScriptsDirectoryField;

    #endregion

    #region Properties

    public string BaseDirectory
    {
        get
        {
            return this.BaseDirectoryField;
        }
    }

    public string JobFilePath
    {
        get
        {
            return Path.Combine(this.BaseDirectoryField, IpcPaths.JobFileName);
        }
    }

    public string HeartbeatFilePath
    {
        get
        {
            return Path.Combine(this.BaseDirectoryField, IpcPaths.HeartbeatFileName);
        }
    }

    public string ResultFilePath
    {
        get
        {
            return Path.Combine(this.BaseDirectoryField, IpcPaths.ResultFileName);
        }
    }

    public string AbortFlagFilePath
    {
        get
        {
            return Path.Combine(this.BaseDirectoryField, IpcPaths.AbortFlagFileName);
        }
    }

    public string JsxScriptsDirectory
    {
        get
        {
            return this.JsxScriptsDirectoryField;
        }
    }

    public string RunnerScriptPath
    {
        get
        {
            return Path.Combine(this.JsxScriptsDirectoryField, IpcPaths.RunnerScriptFileName);
        }
    }

    public string CloseNoSaveScriptPath
    {
        get
        {
            return Path.Combine(this.JsxScriptsDirectoryField, IpcPaths.CloseNoSaveScriptFileName);
        }
    }

    #endregion

    #region Constructors

    public IpcPaths(string? BaseDirectoryOverride = null, string? JsxScriptsDirectoryOverride = null)
    {
        if (string.IsNullOrWhiteSpace(BaseDirectoryOverride))
        {
            string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            this.BaseDirectoryField = Path.Combine(LocalAppData, IpcPaths.AppFolderName, IpcPaths.IpcFolderName);
        }
        else
        {
            this.BaseDirectoryField = Path.GetFullPath(BaseDirectoryOverride);
        }

        if (string.IsNullOrWhiteSpace(JsxScriptsDirectoryOverride))
        {
            this.JsxScriptsDirectoryField = Path.Combine(AppContext.BaseDirectory, "Resources", "Scripts", "jsx");
        }
        else
        {
            this.JsxScriptsDirectoryField = Path.GetFullPath(JsxScriptsDirectoryOverride);
        }
    }

    #endregion

    #region Public Methods

    public void EnsureBaseDirectoryExists()
    {
        if (!Directory.Exists(this.BaseDirectoryField))
        {
            Directory.CreateDirectory(this.BaseDirectoryField);
        }
    }

    #endregion
}
