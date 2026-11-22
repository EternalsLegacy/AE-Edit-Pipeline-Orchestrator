using System.Text;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Infrastructure.Persistence;

namespace AEEditOrchestrator.Tests.Infrastructure;

public sealed class JsonSettingsStoreTests : IDisposable
{
    #region Fields

    private readonly string TempRootDirectoryField;
    private readonly string SettingsFilePathField;

    #endregion

    #region Constructors

    public JsonSettingsStoreTests()
    {
        this.TempRootDirectoryField = Path.Combine(
            Path.GetTempPath(),
            "AEEditOrchestrator_SettingsTests_" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(this.TempRootDirectoryField);
        this.SettingsFilePathField = Path.Combine(this.TempRootDirectoryField, "Nested", "settings.json");
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
    public async Task SaveAsyncAndLoadAsyncRoundTripsAllSettingsWithoutUtf8Bom()
    {
        JsonSettingsStore Store = new JsonSettingsStore(this.SettingsFilePathField);

        PipelineSettings Original = new PipelineSettings
        {
            AfterEffectsExecutablePath = @"C:\Program Files\Adobe\Adobe After Effects 2026\Support Files\AfterFX.exe",
            UserPresetsRootPath = @"C:\Users\Editor\Documents\Adobe\After Effects 2026\User Presets",
            BatchSize = 15,
            WatchdogTimeoutSeconds = 60,
            MaxBackupsToKeep = 7,
            SaveAfterBatch = false,
            PurgeAfterBatch = true,
            ExcludedLayerNames = new List<string> { "Adjustment Layer 1", "ETERNALS", "Watermark" },
            UseSelectedOnly = true,
            MotionTileWidth = 420,
            MotionTileHeight = 390,
            StartBlurFrames = 14,
            EndBlur1Length = 250,
            EndBlur2Length = 100,
            VerticalBlurDirection = 90,
            HorizontalBlurDirection = 180,
            StartDirection = ShakeStartDirection.Horizontal,
            BlurZoomStart = BlurZoomStartPreset.BlurZoomOut,
            AddMotionTileToBlurZoom = true,
            ReverseMode = ReverseRollbackMode.Stage1Only,
            SelectedStepIds = new List<string> { PipelineStepIds.Beats, PipelineStepIds.Precomp, PipelineStepIds.Shakes },
            Presets = new PresetMappingSettings
            {
                FadeInOut = "MyFade",
                VerticalShake = "MyVShake",
                VerticalBlur = "MyVBlur",
                HorizontalShake = "MyHShake",
                HorizontalBlur = "MyHBlur",
                BlurZoomIn = "MyZoomIn",
                BlurZoomOut = "MyZoomOut"
            },
            BeatDetection = new BeatDetectionSettings
            {
                Mode = BeatDetectionMode.UniversalFourZone,
                Sensitivity = 8.25,
                MinFrameGap = 4,
                MarkerTarget = BeatMarkerTarget.Layer,
                OnlyWorkArea = true,
                SampleXRatio = 0.75,
                SampleYRatio = 0.25,
                SampleRadius = 18
            }
        };

        await Store.SaveAsync(Original);

        Assert.True(File.Exists(this.SettingsFilePathField));

        byte[] FileBytes = await File.ReadAllBytesAsync(this.SettingsFilePathField);
        bool HasUtf8Bom = FileBytes.Length >= 3
            && FileBytes[0] == 0xEF
            && FileBytes[1] == 0xBB
            && FileBytes[2] == 0xBF;
        Assert.False(HasUtf8Bom);

        PipelineSettings Loaded = await Store.LoadAsync();

        Assert.Equal(Original.AfterEffectsExecutablePath, Loaded.AfterEffectsExecutablePath);
        Assert.Equal(Original.UserPresetsRootPath, Loaded.UserPresetsRootPath);
        Assert.Equal(15, Loaded.BatchSize);
        Assert.Equal(60, Loaded.WatchdogTimeoutSeconds);
        Assert.Equal(7, Loaded.MaxBackupsToKeep);
        Assert.False(Loaded.SaveAfterBatch);
        Assert.True(Loaded.PurgeAfterBatch);
        Assert.Equal(Original.ExcludedLayerNames, Loaded.ExcludedLayerNames);
        Assert.True(Loaded.UseSelectedOnly);
        Assert.Equal(420, Loaded.MotionTileWidth);
        Assert.Equal(390, Loaded.MotionTileHeight);
        Assert.Equal(14, Loaded.StartBlurFrames);
        Assert.Equal(250, Loaded.EndBlur1Length);
        Assert.Equal(100, Loaded.EndBlur2Length);
        Assert.Equal(ShakeStartDirection.Horizontal, Loaded.StartDirection);
        Assert.Equal(BlurZoomStartPreset.BlurZoomOut, Loaded.BlurZoomStart);
        Assert.True(Loaded.AddMotionTileToBlurZoom);
        Assert.Equal(ReverseRollbackMode.Stage1Only, Loaded.ReverseMode);
        Assert.Equal(Original.SelectedStepIds, Loaded.SelectedStepIds);
        Assert.Equal("MyZoomOut", Loaded.Presets.BlurZoomOut);
        Assert.Equal(BeatDetectionMode.UniversalFourZone, Loaded.BeatDetection.Mode);
        Assert.Equal(8.25, Loaded.BeatDetection.Sensitivity);
        Assert.Equal(4, Loaded.BeatDetection.MinFrameGap);
        Assert.Equal(BeatMarkerTarget.Layer, Loaded.BeatDetection.MarkerTarget);
        Assert.True(Loaded.BeatDetection.OnlyWorkArea);
    }

    [Fact]
    public async Task LoadAsyncWhenSettingsFileDoesNotExistReturnsDefaultSettings()
    {
        JsonSettingsStore Store = new JsonSettingsStore(this.SettingsFilePathField);

        PipelineSettings Loaded = await Store.LoadAsync();

        Assert.NotNull(Loaded);
        Assert.Equal(10, Loaded.BatchSize);
        Assert.Equal(45, Loaded.WatchdogTimeoutSeconds);
        Assert.Equal(5, Loaded.MaxBackupsToKeep);
        Assert.Equal("FadeInOut", Loaded.Presets.FadeInOut);
        Assert.Equal("BlurZoomOut", Loaded.Presets.BlurZoomOut);
    }

    [Fact]
    public void ValidatePresetsOnDiskFindsRecursiveFfxFilesCaseInsensitivelyAndReportsMissing()
    {
        string PresetsRoot = Path.Combine(this.TempRootDirectoryField, "User Presets");
        string Stage1Dir = Path.Combine(PresetsRoot, "EditPack", "Stage1");
        string Stage2Dir = Path.Combine(PresetsRoot, "EditPack", "Stage2", "Deep");
        Directory.CreateDirectory(Stage1Dir);
        Directory.CreateDirectory(Stage2Dir);

        string FadePath = Path.Combine(PresetsRoot, "FadeInOut.ffx");
        string VShakePath = Path.Combine(Stage1Dir, "verticalshake.FFX");
        string VBlurPath = Path.Combine(Stage1Dir, "VerticalBlur.ffx");
        string HShakePath = Path.Combine(Stage1Dir, "HorizontalShake.ffx");
        string ZoomInPath = Path.Combine(Stage2Dir, "BLURZOOMIN.ffx");

        File.WriteAllBytes(FadePath, Encoding.ASCII.GetBytes("FFX"));
        File.WriteAllBytes(VShakePath, Encoding.ASCII.GetBytes("FFX"));
        File.WriteAllBytes(VBlurPath, Encoding.ASCII.GetBytes("FFX"));
        File.WriteAllBytes(HShakePath, Encoding.ASCII.GetBytes("FFX"));
        File.WriteAllBytes(ZoomInPath, Encoding.ASCII.GetBytes("FFX"));

        PipelineSettings Settings = new PipelineSettings
        {
            UserPresetsRootPath = PresetsRoot
        };

        JsonSettingsStore Store = new JsonSettingsStore(this.SettingsFilePathField);
        IReadOnlyList<PresetValidationItem> Items = Store.ValidatePresetsOnDisk(Settings);

        Assert.Equal(7, Items.Count);

        PresetValidationItem FadeItem = Items.Single(Item => Item.PresetKey == "FadeInOut");
        Assert.True(FadeItem.IsFound);
        Assert.Equal(FadePath, FadeItem.FullPath);

        PresetValidationItem VShakeItem = Items.Single(Item => Item.PresetKey == "VerticalShake");
        Assert.True(VShakeItem.IsFound);
        Assert.Equal(VShakePath, VShakeItem.FullPath);

        PresetValidationItem VBlurItem = Items.Single(Item => Item.PresetKey == "VerticalBlur");
        Assert.True(VBlurItem.IsFound);
        Assert.Equal(VBlurPath, VBlurItem.FullPath);

        PresetValidationItem HShakeItem = Items.Single(Item => Item.PresetKey == "HorizontalShake");
        Assert.True(HShakeItem.IsFound);
        Assert.Equal(HShakePath, HShakeItem.FullPath);

        PresetValidationItem HBlurItem = Items.Single(Item => Item.PresetKey == "HorizontalBlur");
        Assert.False(HBlurItem.IsFound);
        Assert.Equal(string.Empty, HBlurItem.FullPath);

        PresetValidationItem ZoomInItem = Items.Single(Item => Item.PresetKey == "BlurZoomIn");
        Assert.True(ZoomInItem.IsFound);
        Assert.Equal(ZoomInPath, ZoomInItem.FullPath);

        PresetValidationItem ZoomOutItem = Items.Single(Item => Item.PresetKey == "BlurZoomOut");
        Assert.False(ZoomOutItem.IsFound);
        Assert.Equal(string.Empty, ZoomOutItem.FullPath);
    }

    [Fact]
    public void ValidatePresetsOnDiskWhenRootDirectoryDoesNotExistReturnsSevenNotFoundItems()
    {
        PipelineSettings Settings = new PipelineSettings
        {
            UserPresetsRootPath = Path.Combine(this.TempRootDirectoryField, "NonExistentPresets")
        };

        JsonSettingsStore Store = new JsonSettingsStore(this.SettingsFilePathField);
        IReadOnlyList<PresetValidationItem> Items = Store.ValidatePresetsOnDisk(Settings);

        Assert.Equal(7, Items.Count);
        foreach (PresetValidationItem Item in Items)
        {
            Assert.False(Item.IsFound);
            Assert.Equal(string.Empty, Item.FullPath);
        }
    }

    #endregion
}
