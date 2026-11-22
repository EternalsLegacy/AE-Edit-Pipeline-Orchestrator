namespace AEEditOrchestrator.App.ViewModels;

public sealed class PresetValidationItemViewModel
{
    #region Fields

    private string PresetKeyField = string.Empty;
    private string PresetNameField = string.Empty;
    private bool IsFoundField = false;
    private string FullPathField = string.Empty;

    #endregion

    #region Properties

    public string PresetKey
    {
        get
        {
            return this.PresetKeyField;
        }
        set
        {
            this.PresetKeyField = value ?? string.Empty;
        }
    }

    public string PresetName
    {
        get
        {
            return this.PresetNameField;
        }
        set
        {
            this.PresetNameField = value ?? string.Empty;
        }
    }

    public bool IsFound
    {
        get
        {
            return this.IsFoundField;
        }
        set
        {
            this.IsFoundField = value;
        }
    }

    public string FullPath
    {
        get
        {
            return this.FullPathField;
        }
        set
        {
            this.FullPathField = value ?? string.Empty;
        }
    }

    public string StatusText
    {
        get
        {
            if (this.IsFoundField)
            {
                return "Gefunden";
            }

            return "Fehlt";
        }
    }

    public string PathDisplayText
    {
        get
        {
            if (this.IsFoundField && !string.IsNullOrWhiteSpace(this.FullPathField))
            {
                return this.FullPathField;
            }

            return "Nicht im User-Presets-Ordner gefunden";
        }
    }

    #endregion
}
