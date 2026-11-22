namespace AEEditOrchestrator.Tests.Core.TestDoubles;

public sealed class RecordingProgress<T> : IProgress<T>
{
    #region Fields

    private readonly List<T> ItemsField = new List<T>();

    #endregion

    #region Properties

    public IReadOnlyList<T> Items
    {
        get
        {
            return this.ItemsField.AsReadOnly();
        }
    }

    #endregion

    #region Public Methods

    public void Report(T Value)
    {
        this.ItemsField.Add(Value);
    }

    #endregion
}
