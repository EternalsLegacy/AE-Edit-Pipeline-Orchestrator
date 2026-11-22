using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Models.Ipc;
using AEEditOrchestrator.Infrastructure.Transactions;

namespace AEEditOrchestrator.Tests.Infrastructure;

public sealed class FileSnapshotTransactionManagerTests : IDisposable
{
    #region Fields

    private readonly string TempRootDirectoryField;
    private readonly string ProjectFilePathField;
    private readonly byte[] OriginalProjectBytesField;

    #endregion

    #region Constructors

    public FileSnapshotTransactionManagerTests()
    {
        this.TempRootDirectoryField = Path.Combine(
            Path.GetTempPath(),
            "AEEditOrchestrator_TxTests_" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(this.TempRootDirectoryField);

        this.ProjectFilePathField = Path.Combine(this.TempRootDirectoryField, "TestEditProject.aep");
        this.OriginalProjectBytesField = Encoding.UTF8.GetBytes("RIFX_FAKE_AEP_PROJECT_CONTENT_V1_0123456789ABCDEF");
        File.WriteAllBytes(this.ProjectFilePathField, this.OriginalProjectBytesField);
    }

    #endregion

    #region Public Methods

    public void Dispose()
    {
        if (Directory.Exists(this.TempRootDirectoryField))
        {
            Directory.Delete(this.TempRootDirectoryField, recursive: true);
        }
    }

    [Fact]
    public async Task BeginTransactionAsyncCreatesSnapshotAndManifestWithValidSha256()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FileSnapshotTransactionManager Manager = new FileSnapshotTransactionManager(Bridge);
        List<string> Steps = new List<string> { PipelineStepIds.Precomp, PipelineStepIds.Fade };

        TransactionManifest Manifest = await Manager.BeginTransactionAsync(this.ProjectFilePathField, Steps);

        Assert.False(string.IsNullOrWhiteSpace(Manifest.TransactionId));
        Assert.Equal(TransactionStatus.Active, Manifest.Status);
        Assert.Equal(Path.GetFullPath(this.ProjectFilePathField), Manifest.OriginalProjectPath);
        Assert.True(Directory.Exists(Manifest.BackupDirectoryPath));
        Assert.True(File.Exists(Manifest.InitialSnapshotFullPath));

        string ManifestFilePath = Path.Combine(Manifest.BackupDirectoryPath, "manifest.json");
        Assert.True(File.Exists(ManifestFilePath));

        byte[] SnapshotBytes = await File.ReadAllBytesAsync(Manifest.InitialSnapshotFullPath);
        Assert.Equal(this.OriginalProjectBytesField, SnapshotBytes);

        string ExpectedHash = FileSnapshotTransactionManagerTests.ComputeSha256Hex(this.OriginalProjectBytesField);
        Assert.Equal(ExpectedHash, Manifest.InitialSnapshotSha256);

        bool IntegrityOk = await Manager.VerifySnapshotIntegrityAsync(Manifest);
        Assert.True(IntegrityOk);
    }

    [Fact]
    public async Task VerifySnapshotIntegrityAsyncReturnsFalseWhenSnapshotFileIsTampered()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FileSnapshotTransactionManager Manager = new FileSnapshotTransactionManager(Bridge);
        TransactionManifest Manifest = await Manager.BeginTransactionAsync(
            this.ProjectFilePathField,
            new List<string> { PipelineStepIds.Precomp });

        byte[] TamperedBytes = Encoding.UTF8.GetBytes("TAMPERED_SNAPSHOT_BYTES");
        await File.WriteAllBytesAsync(Manifest.InitialSnapshotFullPath, TamperedBytes);

        bool IntegrityOk = await Manager.VerifySnapshotIntegrityAsync(Manifest);
        Assert.False(IntegrityOk);
    }

    [Fact]
    public async Task VerifySnapshotIntegrityAsyncReturnsFalseWhenSnapshotFileIsMissing()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FileSnapshotTransactionManager Manager = new FileSnapshotTransactionManager(Bridge);
        TransactionManifest Manifest = await Manager.BeginTransactionAsync(
            this.ProjectFilePathField,
            new List<string> { PipelineStepIds.Precomp });

        File.Delete(Manifest.InitialSnapshotFullPath);

        bool IntegrityOk = await Manager.VerifySnapshotIntegrityAsync(Manifest);
        Assert.False(IntegrityOk);
    }

    [Fact]
    public async Task CreateMilestoneSnapshotAsyncCopiesProjectComputesHashAndPersistsManifest()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FileSnapshotTransactionManager Manager = new FileSnapshotTransactionManager(Bridge);
        TransactionManifest Manifest = await Manager.BeginTransactionAsync(
            this.ProjectFilePathField,
            new List<string> { PipelineStepIds.Precomp, PipelineStepIds.ZoomGroup });

        byte[] Stage1Bytes = Encoding.UTF8.GetBytes("RIFX_FAKE_AEP_AFTER_STAGE_1_MODIFIED");
        await File.WriteAllBytesAsync(this.ProjectFilePathField, Stage1Bytes);

        MilestoneSnapshot Milestone = await Manager.CreateMilestoneSnapshotAsync(
            Manifest,
            "After Stage 1",
            "01_After_Stage1.aep");

        string MilestonePath = Path.Combine(Manifest.BackupDirectoryPath, "01_After_Stage1.aep");
        Assert.True(File.Exists(MilestonePath));
        Assert.Equal("After Stage 1", Milestone.Name);
        Assert.Equal("01_After_Stage1.aep", Milestone.FileName);
        Assert.Equal(FileSnapshotTransactionManagerTests.ComputeSha256Hex(Stage1Bytes), Milestone.Sha256);
        Assert.Single(Manifest.Milestones);

        string ManifestJson = await File.ReadAllTextAsync(Path.Combine(Manifest.BackupDirectoryPath, "manifest.json"));
        TransactionManifest? Reloaded = JsonSerializer.Deserialize<TransactionManifest>(ManifestJson, IpcJsonOptions.Default);
        Assert.NotNull(Reloaded);
        Assert.Single(Reloaded!.Milestones);
        Assert.Equal(Milestone.Sha256, Reloaded.Milestones[0].Sha256);
    }

    [Fact]
    public async Task RollbackTransactionAsyncRestoresCorruptedProjectAtomicallyAndReopens()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FileSnapshotTransactionManager Manager = new FileSnapshotTransactionManager(Bridge);
        PipelineSettings Settings = new PipelineSettings();

        TransactionManifest Manifest = await Manager.BeginTransactionAsync(
            this.ProjectFilePathField,
            new List<string> { PipelineStepIds.Precomp, PipelineStepIds.Fade });

        byte[] CorruptedBytes = Encoding.UTF8.GetBytes("CORRUPTED_PARTIAL_TIMELINE_DATA");
        await File.WriteAllBytesAsync(this.ProjectFilePathField, CorruptedBytes);

        bool RolledBack = await Manager.RollbackTransactionAsync(Manifest, Settings, "Step 03 failed");

        Assert.True(RolledBack);
        Assert.Equal(TransactionStatus.RolledBack, Manifest.Status);
        Assert.Equal("Step 03 failed", Manifest.Notes);
        Assert.NotNull(Manifest.CompletedAtUtc);

        byte[] RestoredBytes = await File.ReadAllBytesAsync(this.ProjectFilePathField);
        Assert.Equal(this.OriginalProjectBytesField, RestoredBytes);

        Assert.Equal(
            new List<string> { nameof(Bridge.CloseProjectWithoutSavingAsync), nameof(Bridge.OpenProjectAsync) },
            Bridge.RecordedCalls);
        Assert.Single(Bridge.OpenedProjectPaths);
        Assert.Equal(Path.GetFullPath(this.ProjectFilePathField), Bridge.OpenedProjectPaths[0]);
    }

    [Fact]
    public async Task RollbackTransactionAsyncRestartsAfterEffectsWhenCloseProjectFails()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge
        {
            CloseProjectResult = false,
            RestartResult = true,
            OpenProjectResult = true
        };
        FileSnapshotTransactionManager Manager = new FileSnapshotTransactionManager(Bridge);
        PipelineSettings Settings = new PipelineSettings();

        TransactionManifest Manifest = await Manager.BeginTransactionAsync(
            this.ProjectFilePathField,
            new List<string> { PipelineStepIds.Shakes });

        await File.WriteAllBytesAsync(this.ProjectFilePathField, Encoding.UTF8.GetBytes("BROKEN_DATA"));

        bool RolledBack = await Manager.RollbackTransactionAsync(Manifest, Settings, "Watchdog timeout");

        Assert.True(RolledBack);
        Assert.Equal(
            new List<string>
            {
                nameof(Bridge.CloseProjectWithoutSavingAsync),
                nameof(Bridge.TryRestartAfterEffectsAsync),
                nameof(Bridge.OpenProjectAsync)
            },
            Bridge.RecordedCalls);
        Assert.Equal(this.OriginalProjectBytesField, await File.ReadAllBytesAsync(this.ProjectFilePathField));
    }

    [Fact]
    public async Task RollbackTransactionAsyncReturnsFalseWhenSnapshotIsCorrupted()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FileSnapshotTransactionManager Manager = new FileSnapshotTransactionManager(Bridge);
        PipelineSettings Settings = new PipelineSettings();

        TransactionManifest Manifest = await Manager.BeginTransactionAsync(
            this.ProjectFilePathField,
            new List<string> { PipelineStepIds.Fade });

        byte[] ModifiedProjectBytes = Encoding.UTF8.GetBytes("CURRENT_MODIFIED_AEP");
        await File.WriteAllBytesAsync(this.ProjectFilePathField, ModifiedProjectBytes);
        await File.WriteAllBytesAsync(Manifest.InitialSnapshotFullPath, Encoding.UTF8.GetBytes("CORRUPT_SNAPSHOT"));

        bool RolledBack = await Manager.RollbackTransactionAsync(Manifest, Settings, "Error");

        Assert.False(RolledBack);
        Assert.Equal(TransactionStatus.Failed, Manifest.Status);
        Assert.Equal(ModifiedProjectBytes, await File.ReadAllBytesAsync(this.ProjectFilePathField));
    }

    [Fact]
    public async Task CommitAndRestoreSnapshotAsyncWorksAsExpected()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FileSnapshotTransactionManager Manager = new FileSnapshotTransactionManager(Bridge);
        PipelineSettings Settings = new PipelineSettings();

        TransactionManifest Manifest = await Manager.BeginTransactionAsync(
            this.ProjectFilePathField,
            new List<string> { PipelineStepIds.Precomp });

        await Manager.CommitTransactionAsync(Manifest, Settings);
        Assert.Equal(TransactionStatus.Committed, Manifest.Status);
        Assert.NotNull(Manifest.CompletedAtUtc);

        await File.WriteAllBytesAsync(this.ProjectFilePathField, Encoding.UTF8.GetBytes("LATER_EDITS"));

        bool Restored = await Manager.RestoreSnapshotAsync(Manifest, Settings);
        Assert.True(Restored);
        Assert.Equal(TransactionStatus.Committed, Manifest.Status);
        Assert.Equal(this.OriginalProjectBytesField, await File.ReadAllBytesAsync(this.ProjectFilePathField));
    }

    [Fact]
    public async Task ListSnapshotsAsyncReturnsManifestsSortedByCreatedAtDescending()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FileSnapshotTransactionManager Manager = new FileSnapshotTransactionManager(Bridge);

        TransactionManifest First = await Manager.BeginTransactionAsync(this.ProjectFilePathField, new List<string> { PipelineStepIds.Beats });
        await Task.Delay(15);
        TransactionManifest Second = await Manager.BeginTransactionAsync(this.ProjectFilePathField, new List<string> { PipelineStepIds.Precomp });
        await Task.Delay(15);
        TransactionManifest Third = await Manager.BeginTransactionAsync(this.ProjectFilePathField, new List<string> { PipelineStepIds.Fade });

        IReadOnlyList<TransactionManifest> Listed = await Manager.ListSnapshotsAsync(this.ProjectFilePathField);

        Assert.Equal(3, Listed.Count);
        Assert.Equal(Third.TransactionId, Listed[0].TransactionId);
        Assert.Equal(Second.TransactionId, Listed[1].TransactionId);
        Assert.Equal(First.TransactionId, Listed[2].TransactionId);
    }

    [Fact]
    public async Task RotateBackupsAsyncKeepsExactlyNewestConfiguredBackups()
    {
        FakeAfterEffectsBridge Bridge = new FakeAfterEffectsBridge();
        FileSnapshotTransactionManager Manager = new FileSnapshotTransactionManager(Bridge);

        List<TransactionManifest> CreatedManifests = new List<TransactionManifest>();
        for (int Index = 0; Index < 5; Index++)
        {
            TransactionManifest Manifest = await Manager.BeginTransactionAsync(
                this.ProjectFilePathField,
                new List<string> { PipelineStepIds.Precomp });
            CreatedManifests.Add(Manifest);
            await Task.Delay(15);
        }

        await Manager.RotateBackupsAsync(this.ProjectFilePathField, MaxBackupsToKeep: 3);

        IReadOnlyList<TransactionManifest> Remaining = await Manager.ListSnapshotsAsync(this.ProjectFilePathField);

        Assert.Equal(3, Remaining.Count);
        Assert.Equal(CreatedManifests[4].TransactionId, Remaining[0].TransactionId);
        Assert.Equal(CreatedManifests[3].TransactionId, Remaining[1].TransactionId);
        Assert.Equal(CreatedManifests[2].TransactionId, Remaining[2].TransactionId);
        Assert.False(Directory.Exists(CreatedManifests[0].BackupDirectoryPath));
        Assert.False(Directory.Exists(CreatedManifests[1].BackupDirectoryPath));
    }

    #endregion

    #region Private Methods

    private static string ComputeSha256Hex(byte[] Data)
    {
        byte[] HashBytes = SHA256.HashData(Data);
        return Convert.ToHexString(HashBytes).ToLowerInvariant();
    }

    #endregion
}
