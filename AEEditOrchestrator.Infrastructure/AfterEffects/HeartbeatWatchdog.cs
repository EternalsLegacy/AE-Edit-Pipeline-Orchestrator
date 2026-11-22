namespace AEEditOrchestrator.Infrastructure.AfterEffects;

public enum WatchdogTerminationReason
{
    None = 0,
    Completed = 1,
    HeartbeatTimeout = 2,
    ProcessCrashed = 3,
    UserCancelled = 4
}

public sealed class HeartbeatWatchdog : IDisposable
{
    #region Constants

    private const int DefaultTimeoutSeconds = 45;
    private const int DefaultCheckIntervalMs = 200;

    #endregion

    #region Fields

    private readonly TimeSpan TimeoutField;
    private readonly TimeSpan CheckIntervalField;
    private readonly CancellationToken UserCancellationTokenField;
    private readonly Func<bool>? IsProcessAliveCheckField;
    private readonly CancellationTokenSource LinkedCtsField;
    private readonly CancellationTokenSource MonitorCtsField;
    private readonly object SyncLockField = new object();

    private DateTimeOffset LastHeartbeatUtcField;
    private WatchdogTerminationReason TerminationReasonField = WatchdogTerminationReason.None;
    private CancellationTokenRegistration UserCancelRegistrationField;
    private Task? MonitorTaskField;
    private bool IsStartedField = false;
    private bool IsDisposedField = false;

    #endregion

    #region Properties

    public CancellationToken Token
    {
        get
        {
            return this.LinkedCtsField.Token;
        }
    }

    public WatchdogTerminationReason TerminationReason
    {
        get
        {
            lock (this.SyncLockField)
            {
                return this.TerminationReasonField;
            }
        }
    }

    public DateTimeOffset LastHeartbeatUtc
    {
        get
        {
            lock (this.SyncLockField)
            {
                return this.LastHeartbeatUtcField;
            }
        }
    }

    #endregion

    #region Constructors

    public HeartbeatWatchdog(
        int TimeoutSeconds,
        CancellationToken UserCancellationToken = default,
        Func<bool>? IsProcessAliveCheck = null,
        TimeSpan? CheckIntervalOverride = null)
        : this(
            HeartbeatWatchdog.ResolveTimeoutFromSeconds(TimeoutSeconds),
            UserCancellationToken,
            IsProcessAliveCheck,
            CheckIntervalOverride)
    {
    }

    public HeartbeatWatchdog(
        TimeSpan Timeout,
        CancellationToken UserCancellationToken = default,
        Func<bool>? IsProcessAliveCheck = null,
        TimeSpan? CheckIntervalOverride = null)
    {
        if (Timeout <= TimeSpan.Zero)
        {
            this.TimeoutField = TimeSpan.FromSeconds(HeartbeatWatchdog.DefaultTimeoutSeconds);
        }
        else
        {
            this.TimeoutField = Timeout;
        }

        if (CheckIntervalOverride.HasValue && CheckIntervalOverride.Value > TimeSpan.Zero)
        {
            this.CheckIntervalField = CheckIntervalOverride.Value;
        }
        else
        {
            this.CheckIntervalField = TimeSpan.FromMilliseconds(HeartbeatWatchdog.DefaultCheckIntervalMs);
        }

        this.UserCancellationTokenField = UserCancellationToken;
        this.IsProcessAliveCheckField = IsProcessAliveCheck;
        this.LinkedCtsField = new CancellationTokenSource();
        this.MonitorCtsField = new CancellationTokenSource();
        this.LastHeartbeatUtcField = DateTimeOffset.UtcNow;
    }

    #endregion

    #region Public Methods

    public void Start()
    {
        lock (this.SyncLockField)
        {
            if (this.IsStartedField || this.IsDisposedField)
            {
                return;
            }

            this.IsStartedField = true;
            this.LastHeartbeatUtcField = DateTimeOffset.UtcNow;
        }

        this.UserCancelRegistrationField = this.UserCancellationTokenField.Register(this.HandleUserCancellation);

        if (this.UserCancellationTokenField.IsCancellationRequested)
        {
            this.TriggerTermination(WatchdogTerminationReason.UserCancelled);
            return;
        }

        this.MonitorTaskField = Task.Run(this.MonitorLoopAsync);
    }

    public void NotifyHeartbeat()
    {
        lock (this.SyncLockField)
        {
            if (this.IsDisposedField)
            {
                return;
            }

            this.LastHeartbeatUtcField = DateTimeOffset.UtcNow;
        }
    }

    public void MarkCompleted()
    {
        lock (this.SyncLockField)
        {
            if (this.TerminationReasonField == WatchdogTerminationReason.None)
            {
                this.TerminationReasonField = WatchdogTerminationReason.Completed;
            }
        }

        this.SafeCancel(this.MonitorCtsField);
    }

    public void Dispose()
    {
        lock (this.SyncLockField)
        {
            if (this.IsDisposedField)
            {
                return;
            }

            this.IsDisposedField = true;
        }

        this.SafeCancel(this.MonitorCtsField);

        try
        {
            this.UserCancelRegistrationField.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }

        this.MonitorCtsField.Dispose();
        this.LinkedCtsField.Dispose();
    }

    #endregion

    #region Private Methods

    private static TimeSpan ResolveTimeoutFromSeconds(int TimeoutSeconds)
    {
        if (TimeoutSeconds <= 0)
        {
            return TimeSpan.FromSeconds(HeartbeatWatchdog.DefaultTimeoutSeconds);
        }

        return TimeSpan.FromSeconds(TimeoutSeconds);
    }

    private void HandleUserCancellation()
    {
        this.TriggerTermination(WatchdogTerminationReason.UserCancelled);
    }

    private async Task MonitorLoopAsync()
    {
        try
        {
            while (!this.MonitorCtsField.IsCancellationRequested)
            {
                if (this.UserCancellationTokenField.IsCancellationRequested)
                {
                    this.TriggerTermination(WatchdogTerminationReason.UserCancelled);
                    break;
                }

                if (this.IsProcessAliveCheckField != null)
                {
                    bool IsProcessAlive = true;
                    try
                    {
                        IsProcessAlive = this.IsProcessAliveCheckField();
                    }
                    catch
                    {
                        IsProcessAlive = true;
                    }

                    if (!IsProcessAlive)
                    {
                        this.TriggerTermination(WatchdogTerminationReason.ProcessCrashed);
                        break;
                    }
                }

                DateTimeOffset LastBeat;
                bool ShouldStop;
                lock (this.SyncLockField)
                {
                    LastBeat = this.LastHeartbeatUtcField;
                    ShouldStop = this.IsDisposedField || this.TerminationReasonField != WatchdogTerminationReason.None;
                }

                if (ShouldStop)
                {
                    break;
                }

                TimeSpan Elapsed = DateTimeOffset.UtcNow - LastBeat;
                if (Elapsed >= this.TimeoutField)
                {
                    this.TriggerTermination(WatchdogTerminationReason.HeartbeatTimeout);
                    break;
                }

                await Task.Delay(this.CheckIntervalField, this.MonitorCtsField.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void TriggerTermination(WatchdogTerminationReason Reason)
    {
        lock (this.SyncLockField)
        {
            if (this.IsDisposedField || this.TerminationReasonField != WatchdogTerminationReason.None)
            {
                return;
            }

            this.TerminationReasonField = Reason;
        }

        this.SafeCancel(this.LinkedCtsField);
        this.SafeCancel(this.MonitorCtsField);
    }

    private void SafeCancel(CancellationTokenSource TokenSource)
    {
        try
        {
            if (!TokenSource.IsCancellationRequested)
            {
                TokenSource.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    #endregion
}
