namespace AEEditOrchestrator.App.ViewModels;

public sealed class PipelineLogEntry
{
    #region Fields

    private string TimestampTextField = string.Empty;
    private string MessageField = string.Empty;
    private bool IsErrorField = false;
    private bool IsSuccessField = false;

    #endregion

    #region Properties

    public string TimestampText
    {
        get
        {
            return this.TimestampTextField;
        }
        set
        {
            this.TimestampTextField = value ?? string.Empty;
        }
    }

    public string Message
    {
        get
        {
            return this.MessageField;
        }
        set
        {
            this.MessageField = value ?? string.Empty;
        }
    }

    public bool IsError
    {
        get
        {
            return this.IsErrorField;
        }
        set
        {
            this.IsErrorField = value;
        }
    }

    public bool IsSuccess
    {
        get
        {
            return this.IsSuccessField;
        }
        set
        {
            this.IsSuccessField = value;
        }
    }

    #endregion
}
