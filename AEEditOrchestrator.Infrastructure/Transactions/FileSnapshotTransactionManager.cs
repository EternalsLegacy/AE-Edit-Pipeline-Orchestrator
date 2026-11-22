using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Models.Ipc;

namespace AEEditOrchestrator.Infrastructure.Transactions;

public sealed class FileSnapshotTransactionManager : ITransactionManager
{
    #region Constants

    private const string BackupsFolderName = "_AEP_Backups";
    private const string InitialSnapshotFileName = "00_Initial_Snapshot.aep";
    private const string ManifestFileName = "manifest.json";
    private const int CopyBufferSize = 81920;
    private const int MaxFileMoveRetries = 6;
    private const int FileMoveRetryDelayMs = 150;

    #endregion

    #region Fields

    private readonly IAfterEffectsBridge BridgeField;
    private readonly UTF8Encoding Utf8NoBomEncodingField = new UTF8Encoding(false);

    #endregion

    #region Constructors

    public FileSnapshotTransactionManager(IAfterEffectsBridge Bridge)
    {
        ArgumentNullException.ThrowIfNull(Bridge);
        this.BridgeField = Bridge;
    }

    #endregion

    #region Public Methods

    public async Task<TransactionManifest> BeginTransactionAsync(
        string ProjectFilePath,
        IReadOnlyList<string> SelectedStepIds,
        CancellationToken CancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ProjectFilePath))
        {
            throw new ArgumentException("ProjectFilePath must not be empty.", nameof(ProjectFilePath));
        }

        CancellationToken.ThrowIfCancellationRequested();

        string FullProjectPath = Path.GetFullPath(ProjectFilePath);
        if (!File.Exists(FullProjectPath))
        {
            throw new FileNotFoundException("Original After Effects project file was not found.", FullProjectPath);
        }

        string BackupRootDirectory = FileSnapshotTransactionManager.ResolveBackupRootDirectory(FullProjectPath);
        Directory.CreateDirectory(BackupRootDirectory);

        DateTimeOffset CreatedAtUtc = DateTimeOffset.UtcNow;
        string TransactionId = Guid.NewGuid().ToString("N").Substring(0, 8);
        string FolderName = string.Concat(
            CreatedAtUtc.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture),
            "_",
            TransactionId);

        string TransactionDirectory = Path.Combine(BackupRootDirectory, FolderName);
        Directory.CreateDirectory(TransactionDirectory);

        string SnapshotPath = Path.Combine(TransactionDirectory, FileSnapshotTransactionManager.InitialSnapshotFileName);
        await FileSnapshotTransactionManager.CopyFileSharedAsync(FullProjectPath, SnapshotPath, CancellationToken).ConfigureAwait(false);
        string SnapshotHash = await FileSnapshotTransactionManager.ComputeSha256HexAsync(SnapshotPath, CancellationToken).ConfigureAwait(false);

        List<string> StepIdCopy = new List<string>();
        if (SelectedStepIds != null)
        {
            foreach (string StepId in SelectedStepIds)
            {
                if (!string.IsNullOrWhiteSpace(StepId))
                {
                    StepIdCopy.Add(StepId);
                }
            }
        }

        TransactionManifest Manifest = new TransactionManifest
        {
            TransactionId = TransactionId,
            CreatedAtUtc = CreatedAtUtc,
            OriginalProjectPath = FullProjectPath,
            BackupDirectoryPath = TransactionDirectory,
            InitialSnapshotFileName = FileSnapshotTransactionManager.InitialSnapshotFileName,
            InitialSnapshotSha256 = SnapshotHash,
            SelectedStepIds = StepIdCopy,
            Status = TransactionStatus.Active
        };

        await this.SaveManifestAtomicAsync(Manifest, CancellationToken).ConfigureAwait(false);
        return Manifest;
    }

    public async Task<MilestoneSnapshot> CreateMilestoneSnapshotAsync(
        TransactionManifest Manifest,
        string MilestoneName,
        string FileName,
        CancellationToken CancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Manifest);

        if (string.IsNullOrWhiteSpace(MilestoneName))
        {
            throw new ArgumentException("MilestoneName must not be empty.", nameof(MilestoneName));
        }

        if (string.IsNullOrWhiteSpace(FileName))
        {
            throw new ArgumentException("FileName must not be empty.", nameof(FileName));
        }

        CancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(Manifest.OriginalProjectPath))
        {
            throw new FileNotFoundException("Original After Effects project file was not found.", Manifest.OriginalProjectPath);
        }

        if (string.IsNullOrWhiteSpace(Manifest.BackupDirectoryPath))
        {
            throw new InvalidOperationException("TransactionManifest.BackupDirectoryPath is empty.");
        }

        Directory.CreateDirectory(Manifest.BackupDirectoryPath);

        string SafeFileName = Path.GetFileName(FileName);
        string TargetSnapshotPath = Path.Combine(Manifest.BackupDirectoryPath, SafeFileName);

        await FileSnapshotTransactionManager.CopyFileSharedAsync(
            Manifest.OriginalProjectPath,
            TargetSnapshotPath,
            CancellationToken).ConfigureAwait(false);

        string MilestoneHash = await FileSnapshotTransactionManager.ComputeSha256HexAsync(
            TargetSnapshotPath,
            CancellationToken).ConfigureAwait(false);

        MilestoneSnapshot Milestone = new MilestoneSnapshot
        {
            Name = MilestoneName,
            FileName = SafeFileName,
            Sha256 = MilestoneHash,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        Manifest.Milestones.Add(Milestone);
        await this.SaveManifestAtomicAsync(Manifest, CancellationToken).ConfigureAwait(false);
        return Milestone;
    }

    public async Task CommitTransactionAsync(
        TransactionManifest Manifest,
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Manifest);
        ArgumentNullException.ThrowIfNull(Settings);

        CancellationToken.ThrowIfCancellationRequested();

        Manifest.Status = TransactionStatus.Committed;
        Manifest.CompletedAtUtc = DateTimeOffset.UtcNow;

        await this.SaveManifestAtomicAsync(Manifest, CancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RollbackTransactionAsync(
        TransactionManifest Manifest,
        PipelineSettings Settings,
        string Reason,
        CancellationToken CancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Manifest);
        ArgumentNullException.ThrowIfNull(Settings);

        return await this.RestoreFromManifestCoreAsync(
            Manifest,
            Settings,
            MarkAsRolledBack: true,
            RollbackReason: Reason,
            CancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RestoreSnapshotAsync(
        TransactionManifest Manifest,
        PipelineSettings Settings,
        CancellationToken CancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Manifest);
        ArgumentNullException.ThrowIfNull(Settings);

        return await this.RestoreFromManifestCoreAsync(
            Manifest,
            Settings,
            MarkAsRolledBack: false,
            RollbackReason: null,
            CancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TransactionManifest>> ListSnapshotsAsync(
        string ProjectFilePath,
        CancellationToken CancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ProjectFilePath))
        {
            return Array.Empty<TransactionManifest>();
        }

        CancellationToken.ThrowIfCancellationRequested();

        string BackupRootDirectory = FileSnapshotTransactionManager.ResolveBackupRootDirectory(ProjectFilePath);
        if (!Directory.Exists(BackupRootDirectory))
        {
            return Array.Empty<TransactionManifest>();
        }

        List<TransactionManifest> Manifests = new List<TransactionManifest>();
        string[] Subdirectories = Directory.GetDirectories(BackupRootDirectory);

        foreach (string DirectoryPath in Subdirectories)
        {
            CancellationToken.ThrowIfCancellationRequested();

            string ManifestPath = Path.Combine(DirectoryPath, FileSnapshotTransactionManager.ManifestFileName);
            if (!File.Exists(ManifestPath))
            {
                continue;
            }

            TransactionManifest? Loaded = await this.TryReadManifestAsync(ManifestPath, CancellationToken).ConfigureAwait(false);
            if (Loaded != null)
            {
                if (string.IsNullOrWhiteSpace(Loaded.BackupDirectoryPath))
                {
                    Loaded.BackupDirectoryPath = DirectoryPath;
                }

                Manifests.Add(Loaded);
            }
        }

        Manifests.Sort((TransactionManifest Left, TransactionManifest Right) =>
        {
            return Right.CreatedAtUtc.CompareTo(Left.CreatedAtUtc);
        });

        return Manifests;
    }

    public async Task<bool> VerifySnapshotIntegrityAsync(
        TransactionManifest Manifest,
        CancellationToken CancellationToken = default)
    {
        if (Manifest == null)
        {
            return false;
        }

        CancellationToken.ThrowIfCancellationRequested();

        string SnapshotPath = Manifest.InitialSnapshotFullPath;
        if (string.IsNullOrWhiteSpace(SnapshotPath) || !File.Exists(SnapshotPath))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Manifest.InitialSnapshotSha256))
        {
            return false;
        }

        string ActualHash = await FileSnapshotTransactionManager.ComputeSha256HexAsync(SnapshotPath, CancellationToken).ConfigureAwait(false);
        return string.Equals(ActualHash, Manifest.InitialSnapshotSha256, StringComparison.OrdinalIgnoreCase);
    }

    public async Task RotateBackupsAsync(
        string ProjectFilePath,
        int MaxBackupsToKeep,
        CancellationToken CancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ProjectFilePath))
        {
            return;
        }

        CancellationToken.ThrowIfCancellationRequested();

        string BackupRootDirectory = FileSnapshotTransactionManager.ResolveBackupRootDirectory(ProjectFilePath);
        if (!Directory.Exists(BackupRootDirectory))
        {
            return;
        }

        int EffectiveKeepCount = MaxBackupsToKeep;
        if (EffectiveKeepCount < 0)
        {
            EffectiveKeepCount = 0;
        }

        string[] Subdirectories = Directory.GetDirectories(BackupRootDirectory);
        if (Subdirectories.Length <= EffectiveKeepCount)
        {
            return;
        }

        List<BackupDirectoryEntry> Entries = new List<BackupDirectoryEntry>();
        foreach (string DirectoryPath in Subdirectories)
        {
            CancellationToken.ThrowIfCancellationRequested();

            DateTimeOffset SortTimestamp = await this.ResolveBackupTimestampAsync(DirectoryPath, CancellationToken).ConfigureAwait(false);
            Entries.Add(new BackupDirectoryEntry(DirectoryPath, SortTimestamp));
        }

        Entries.Sort((BackupDirectoryEntry Left, BackupDirectoryEntry Right) =>
        {
            int TimeCompare = Right.CreatedAtUtc.CompareTo(Left.CreatedAtUtc);
            if (TimeCompare != 0)
            {
                return TimeCompare;
            }

            return string.Compare(Right.DirectoryPath, Left.DirectoryPath, StringComparison.OrdinalIgnoreCase);
        });

        for (int Index = EffectiveKeepCount; Index < Entries.Count; Index++)
        {
            CancellationToken.ThrowIfCancellationRequested();
            FileSnapshotTransactionManager.TryDeleteDirectory(Entries[Index].DirectoryPath);
        }
    }

    #endregion

    #region Private Methods

    private async Task<bool> RestoreFromManifestCoreAsync(
        TransactionManifest Manifest,
        PipelineSettings Settings,
        bool MarkAsRolledBack,
        string? RollbackReason,
        CancellationToken CancellationToken)
    {
        CancellationToken.ThrowIfCancellationRequested();

        // Step A: Close project in After Effects without saving to release file lock.
        bool ClosedCleanly = await this.BridgeField.CloseProjectWithoutSavingAsync(Settings, CancellationToken).ConfigureAwait(false);
        if (!ClosedCleanly)
        {
            await this.BridgeField.TryRestartAfterEffectsAsync(Settings, CancellationToken).ConfigureAwait(false);
        }

        // Step B: Verify snapshot integrity, copy to temp file in target dir, move atomically, verify restored hash.
        bool IsSnapshotIntact = await this.VerifySnapshotIntegrityAsync(Manifest, CancellationToken).ConfigureAwait(false);
        if (!IsSnapshotIntact)
        {
            if (MarkAsRolledBack)
            {
                Manifest.Status = TransactionStatus.Failed;
                Manifest.CompletedAtUtc = DateTimeOffset.UtcNow;
                Manifest.Notes = $"Rollback failed: snapshot integrity verification failed for '{Manifest.InitialSnapshotFullPath}'.";
                await this.SaveManifestAtomicAsync(Manifest, CancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(Manifest.OriginalProjectPath))
        {
            return false;
        }

        string FullTargetPath = Path.GetFullPath(Manifest.OriginalProjectPath);
        string? TargetDirectory = Path.GetDirectoryName(FullTargetPath);
        if (string.IsNullOrWhiteSpace(TargetDirectory))
        {
            return false;
        }

        Directory.CreateDirectory(TargetDirectory);

        string TempRestorePath = Path.Combine(
            TargetDirectory,
            string.Concat(".restore_", Guid.NewGuid().ToString("N"), ".tmp"));

        try
        {
            await FileSnapshotTransactionManager.CopyFileSharedAsync(
                Manifest.InitialSnapshotFullPath,
                TempRestorePath,
                CancellationToken).ConfigureAwait(false);

            string TempHash = await FileSnapshotTransactionManager.ComputeSha256HexAsync(
                TempRestorePath,
                CancellationToken).ConfigureAwait(false);

            if (!string.Equals(TempHash, Manifest.InitialSnapshotSha256, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            await FileSnapshotTransactionManager.MoveFileWithRetryAsync(
                TempRestorePath,
                FullTargetPath,
                CancellationToken).ConfigureAwait(false);

            string RestoredHash = await FileSnapshotTransactionManager.ComputeSha256HexAsync(
                FullTargetPath,
                CancellationToken).ConfigureAwait(false);

            if (!string.Equals(RestoredHash, Manifest.InitialSnapshotSha256, StringComparison.OrdinalIgnoreCase))
            {
                if (MarkAsRolledBack)
                {
                    Manifest.Status = TransactionStatus.Failed;
                    Manifest.CompletedAtUtc = DateTimeOffset.UtcNow;
                    Manifest.Notes = "Rollback failed: restored project file SHA-256 verification failed.";
                    await this.SaveManifestAtomicAsync(Manifest, CancellationToken).ConfigureAwait(false);
                }

                return false;
            }
        }
        finally
        {
            FileSnapshotTransactionManager.TryDeleteFile(TempRestorePath);
        }

        if (MarkAsRolledBack)
        {
            Manifest.Status = TransactionStatus.RolledBack;
            Manifest.CompletedAtUtc = DateTimeOffset.UtcNow;
            Manifest.Notes = RollbackReason ?? string.Empty;
            await this.SaveManifestAtomicAsync(Manifest, CancellationToken).ConfigureAwait(false);
        }

        // Step C: Instruct After Effects to reopen the restored project.
        bool Reopened = await this.BridgeField.OpenProjectAsync(
            FullTargetPath,
            Settings,
            CancellationToken).ConfigureAwait(false);

        return Reopened;
    }

    private async Task SaveManifestAtomicAsync(TransactionManifest Manifest, CancellationToken CancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Manifest.BackupDirectoryPath))
        {
            throw new InvalidOperationException("Manifest.BackupDirectoryPath must not be empty.");
        }

        Directory.CreateDirectory(Manifest.BackupDirectoryPath);

        string ManifestPath = Path.Combine(Manifest.BackupDirectoryPath, FileSnapshotTransactionManager.ManifestFileName);
        string TempManifestPath = Path.Combine(
            Manifest.BackupDirectoryPath,
            string.Concat(".manifest_", Guid.NewGuid().ToString("N"), ".tmp"));

        string JsonContent = JsonSerializer.Serialize(Manifest, IpcJsonOptions.Default);

        try
        {
            await File.WriteAllTextAsync(TempManifestPath, JsonContent, this.Utf8NoBomEncodingField, CancellationToken).ConfigureAwait(false);
            File.Move(TempManifestPath, ManifestPath, overwrite: true);
        }
        finally
        {
            FileSnapshotTransactionManager.TryDeleteFile(TempManifestPath);
        }
    }

    private async Task<TransactionManifest?> TryReadManifestAsync(string ManifestPath, CancellationToken CancellationToken)
    {
        try
        {
            using (FileStream Stream = new FileStream(
                ManifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite))
            {
                using (StreamReader Reader = new StreamReader(Stream, this.Utf8NoBomEncodingField, detectEncodingFromByteOrderMarks: true))
                {
                    string RawJson = await Reader.ReadToEndAsync(CancellationToken).ConfigureAwait(false);
                    string CleanedJson = RawJson.Trim().TrimStart('\uFEFF');
                    if (string.IsNullOrWhiteSpace(CleanedJson))
                    {
                        return null;
                    }

                    return JsonSerializer.Deserialize<TransactionManifest>(CleanedJson, IpcJsonOptions.Default);
                }
            }
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<DateTimeOffset> ResolveBackupTimestampAsync(string DirectoryPath, CancellationToken CancellationToken)
    {
        string ManifestPath = Path.Combine(DirectoryPath, FileSnapshotTransactionManager.ManifestFileName);
        if (File.Exists(ManifestPath))
        {
            TransactionManifest? Loaded = await this.TryReadManifestAsync(ManifestPath, CancellationToken).ConfigureAwait(false);
            if (Loaded != null)
            {
                return Loaded.CreatedAtUtc;
            }
        }

        DateTime CreationUtc = Directory.GetCreationTimeUtc(DirectoryPath);
        return new DateTimeOffset(DateTime.SpecifyKind(CreationUtc, DateTimeKind.Utc));
    }

    private static string ResolveBackupRootDirectory(string ProjectFilePath)
    {
        string FullPath = Path.GetFullPath(ProjectFilePath);

        if (Directory.Exists(FullPath))
        {
            string DirName = Path.GetFileName(FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.Equals(DirName, FileSnapshotTransactionManager.BackupsFolderName, StringComparison.OrdinalIgnoreCase))
            {
                return FullPath;
            }

            return Path.Combine(FullPath, FileSnapshotTransactionManager.BackupsFolderName);
        }

        string? ParentDirectory = Path.GetDirectoryName(FullPath);
        if (string.IsNullOrWhiteSpace(ParentDirectory))
        {
            throw new ArgumentException("ProjectFilePath does not contain a valid parent directory.", nameof(ProjectFilePath));
        }

        return Path.Combine(ParentDirectory, FileSnapshotTransactionManager.BackupsFolderName);
    }

    private static async Task CopyFileSharedAsync(string SourcePath, string DestinationPath, CancellationToken CancellationToken)
    {
        using (FileStream SourceStream = new FileStream(
            SourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            FileSnapshotTransactionManager.CopyBufferSize,
            useAsync: true))
        {
            using (FileStream DestinationStream = new FileStream(
                DestinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                FileSnapshotTransactionManager.CopyBufferSize,
                useAsync: true))
            {
                await SourceStream.CopyToAsync(DestinationStream, FileSnapshotTransactionManager.CopyBufferSize, CancellationToken).ConfigureAwait(false);
                await DestinationStream.FlushAsync(CancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<string> ComputeSha256HexAsync(string FilePath, CancellationToken CancellationToken)
    {
        using (FileStream Stream = new FileStream(
            FilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            FileSnapshotTransactionManager.CopyBufferSize,
            useAsync: true))
        {
            byte[] HashBytes = await SHA256.HashDataAsync(Stream, CancellationToken).ConfigureAwait(false);
            return Convert.ToHexString(HashBytes).ToLowerInvariant();
        }
    }

    private static async Task MoveFileWithRetryAsync(string SourcePath, string DestinationPath, CancellationToken CancellationToken)
    {
        for (int Attempt = 0; Attempt < FileSnapshotTransactionManager.MaxFileMoveRetries; Attempt++)
        {
            CancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Move(SourcePath, DestinationPath, overwrite: true);
                return;
            }
            catch (IOException)
            {
                if (Attempt >= FileSnapshotTransactionManager.MaxFileMoveRetries - 1)
                {
                    throw;
                }

                await Task.Delay(FileSnapshotTransactionManager.FileMoveRetryDelayMs, CancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void TryDeleteFile(string FilePath)
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(string DirectoryPath)
    {
        try
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    #endregion

    #region Nested Types

    private sealed class BackupDirectoryEntry
    {
        #region Fields

        private readonly string DirectoryPathField;
        private readonly DateTimeOffset CreatedAtUtcField;

        #endregion

        #region Properties

        public string DirectoryPath
        {
            get
            {
                return this.DirectoryPathField;
            }
        }

        public DateTimeOffset CreatedAtUtc
        {
            get
            {
                return this.CreatedAtUtcField;
            }
        }

        #endregion

        #region Constructors

        public BackupDirectoryEntry(string DirectoryPath, DateTimeOffset CreatedAtUtc)
        {
            this.DirectoryPathField = DirectoryPath;
            this.CreatedAtUtcField = CreatedAtUtc;
        }

        #endregion
    }

    #endregion
}
