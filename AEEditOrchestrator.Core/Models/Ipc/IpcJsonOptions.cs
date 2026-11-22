using System.Text.Json;
using System.Text.Json.Serialization;

namespace AEEditOrchestrator.Core.Models.Ipc;

public static class IpcJsonOptions
{
    #region Fields

    private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    #endregion

    #region Properties

    public static JsonSerializerOptions Default
    {
        get
        {
            return IpcJsonOptions.DefaultOptions;
        }
    }

    #endregion
}
