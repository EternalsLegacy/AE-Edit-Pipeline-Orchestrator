using System.Text.Json.Serialization;

namespace AEEditOrchestrator.Core.Models.Ipc;

public sealed class HeartbeatMessage
{
    #region Fields

    private string JobIdField = string.Empty;
    private string TimestampUtcField = string.Empty;
    private int CurrentIndexField = 0;
    private int TotalItemsField = 0;
    private string MessageField = string.Empty;

    #endregion

    #region Properties

    [JsonPropertyName("jobId")]
    public string JobId
    {
        get
        {
            return this.JobIdField;
        }
        set
        {
            this.JobIdField = value ?? string.Empty;
        }
    }

    [JsonPropertyName("timestampUtc")]
    public string TimestampUtc
    {
        get
        {
            return this.TimestampUtcField;
        }
        set
        {
            this.TimestampUtcField = value ?? string.Empty;
        }
    }

    [JsonPropertyName("currentIndex")]
    public int CurrentIndex
    {
        get
        {
            return this.CurrentIndexField;
        }
        set
        {
            this.CurrentIndexField = value;
        }
    }

    [JsonPropertyName("totalItems")]
    public int TotalItems
    {
        get
        {
            return this.TotalItemsField;
        }
        set
        {
            this.TotalItemsField = value;
        }
    }

    [JsonPropertyName("message")]
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

    #endregion
}
