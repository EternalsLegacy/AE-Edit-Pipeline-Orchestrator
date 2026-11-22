using System.Text.Json;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Models.Ipc;

namespace AEEditOrchestrator.Tests.Infrastructure;

public sealed class IpcSerializationTests
{
    #region Public Methods

    [Fact]
    public void JobPayloadRoundTripPreservesAllExactCamelCasePropertyNames()
    {
        PipelineSettings Settings = new PipelineSettings
        {
            UserPresetsRootPath = @"C:\Users\Editor\Documents\Adobe\After Effects 2026\User Presets",
            ExcludedLayerNames = new List<string> { " Adjustment Layer 1 ", "Black Solid 9", "ETERNALS" },
            UseSelectedOnly = true,
            BatchSize = 12,
            SaveAfterBatch = true,
            PurgeAfterBatch = false,
            StartDirection = ShakeStartDirection.Horizontal,
            BlurZoomStart = BlurZoomStartPreset.BlurZoomOut,
            AddMotionTileToBlurZoom = true,
            MotionTileWidth = 400,
            MotionTileHeight = 380,
            StartBlurFrames = 12,
            EndBlur1Length = 220,
            EndBlur2Length = 90,
            VerticalBlurDirection = 90,
            HorizontalBlurDirection = 180,
            ReverseMode = ReverseRollbackMode.Full,
            Presets = new PresetMappingSettings
            {
                FadeInOut = "CustomFade",
                VerticalShake = "CustomVShake",
                VerticalBlur = "CustomVBlur",
                HorizontalShake = "CustomHShake",
                HorizontalBlur = "CustomHBlur",
                BlurZoomIn = "CustomZoomIn",
                BlurZoomOut = "CustomZoomOut"
            },
            BeatDetection = new BeatDetectionSettings
            {
                Mode = BeatDetectionMode.UniversalFourZone,
                Sensitivity = 7.5,
                MinFrameGap = 3,
                MarkerTarget = BeatMarkerTarget.Layer,
                OnlyWorkArea = true,
                SampleXRatio = 0.5,
                SampleYRatio = 0.5,
                SampleRadius = 20
            }
        };

        JobPayload Payload = JobPayload.FromSettings(
            "guid-1234",
            "04_ApplyMotionTileAndShakes.jsx",
            Settings,
            BatchStartIndex: 10,
            RequiredPresets: new List<string> { "CustomVShake", "CustomHShake" },
            ProjectToOpen: @"D:\Edits\JJK_Edit\JJK_Edit.aep");

        string Json = JsonSerializer.Serialize(Payload, IpcJsonOptions.Default);

        using (JsonDocument Document = JsonDocument.Parse(Json))
        {
            JsonElement Root = Document.RootElement;
            Assert.Equal("guid-1234", Root.GetProperty("jobId").GetString());
            Assert.Equal("04_ApplyMotionTileAndShakes.jsx", Root.GetProperty("scriptFile").GetString());
            Assert.True(Root.GetProperty("useSelectedOnly").GetBoolean());
            Assert.Equal(10, Root.GetProperty("batchStartIndex").GetInt32());
            Assert.Equal(12, Root.GetProperty("batchSize").GetInt32());
            Assert.True(Root.GetProperty("saveAfterBatch").GetBoolean());
            Assert.False(Root.GetProperty("purgeAfterBatch").GetBoolean());
            Assert.Equal("CustomVShake", Root.GetProperty("requiredPresets")[0].GetString());
            Assert.Equal("CustomHShake", Root.GetProperty("requiredPresets")[1].GetString());

            JsonElement SettingsNode = Root.GetProperty("settings");
            Assert.Equal(Settings.UserPresetsRootPath, SettingsNode.GetProperty("presetsRoot").GetString());
            Assert.Equal("adjustment layer 1", SettingsNode.GetProperty("excludedNames")[0].GetString());
            Assert.Equal("Horizontal", SettingsNode.GetProperty("startDirection").GetString());
            Assert.Equal("BlurZoomOut", SettingsNode.GetProperty("blurZoomStart").GetString());
            Assert.True(SettingsNode.GetProperty("addMotionTileToBlurZoom").GetBoolean());
            Assert.Equal(400, SettingsNode.GetProperty("motionTileWidth").GetInt32());
            Assert.Equal(380, SettingsNode.GetProperty("motionTileHeight").GetInt32());
            Assert.Equal(12, SettingsNode.GetProperty("startBlurFrames").GetInt32());
            Assert.Equal(220, SettingsNode.GetProperty("endBlur1Length").GetInt32());
            Assert.Equal(90, SettingsNode.GetProperty("endBlur2Length").GetInt32());
            Assert.Equal(90, SettingsNode.GetProperty("verticalBlurDir").GetInt32());
            Assert.Equal(180, SettingsNode.GetProperty("horizontalBlurDir").GetInt32());
            Assert.Equal("Full", SettingsNode.GetProperty("reverseMode").GetString());
            Assert.Equal(@"D:\Edits\JJK_Edit\JJK_Edit.aep", SettingsNode.GetProperty("projectToOpen").GetString());

            JsonElement PresetsNode = SettingsNode.GetProperty("presets");
            Assert.Equal("CustomFade", PresetsNode.GetProperty("fadeInOut").GetString());
            Assert.Equal("CustomVShake", PresetsNode.GetProperty("verticalShake").GetString());
            Assert.Equal("CustomVBlur", PresetsNode.GetProperty("verticalBlur").GetString());
            Assert.Equal("CustomHShake", PresetsNode.GetProperty("horizontalShake").GetString());
            Assert.Equal("CustomHBlur", PresetsNode.GetProperty("horizontalBlur").GetString());
            Assert.Equal("CustomZoomIn", PresetsNode.GetProperty("blurZoomIn").GetString());
            Assert.Equal("CustomZoomOut", PresetsNode.GetProperty("blurZoomOut").GetString());

            JsonElement BeatNode = SettingsNode.GetProperty("beatDetection");
            Assert.Equal("UniversalFourZone", BeatNode.GetProperty("mode").GetString());
            Assert.Equal(7.5, BeatNode.GetProperty("sensitivity").GetDouble());
            Assert.Equal(3, BeatNode.GetProperty("minFrameGap").GetInt32());
            Assert.Equal("Layer", BeatNode.GetProperty("markerTarget").GetString());
            Assert.True(BeatNode.GetProperty("onlyWorkArea").GetBoolean());
            Assert.Equal(0.5, BeatNode.GetProperty("sampleXRatio").GetDouble());
            Assert.Equal(0.5, BeatNode.GetProperty("sampleYRatio").GetDouble());
            Assert.Equal(20, BeatNode.GetProperty("sampleRadius").GetInt32());
        }

        JobPayload? RoundTripped = JsonSerializer.Deserialize<JobPayload>(Json, IpcJsonOptions.Default);
        Assert.NotNull(RoundTripped);
        Assert.Equal(Payload.JobId, RoundTripped!.JobId);
        Assert.Equal(Payload.ScriptFile, RoundTripped.ScriptFile);
        Assert.Equal(Payload.BatchStartIndex, RoundTripped.BatchStartIndex);
        Assert.Equal(Payload.Settings.Presets.BlurZoomOut, RoundTripped.Settings.Presets.BlurZoomOut);
        Assert.Equal(Payload.Settings.BeatDetection.Mode, RoundTripped.Settings.BeatDetection.Mode);
    }

    [Fact]
    public void HeartbeatMessageRoundTripUsesExactCamelCasePropertyNames()
    {
        HeartbeatMessage Heartbeat = new HeartbeatMessage
        {
            JobId = "job-hb-42",
            TimestampUtc = "2026-11-22T10:45:00.123Z",
            CurrentIndex = 7,
            TotalItems = 25,
            Message = "Clip 7 von 25 verarbeitet"
        };

        string Json = JsonSerializer.Serialize(Heartbeat, IpcJsonOptions.Default);

        using (JsonDocument Document = JsonDocument.Parse(Json))
        {
            JsonElement Root = Document.RootElement;
            Assert.Equal("job-hb-42", Root.GetProperty("jobId").GetString());
            Assert.Equal("2026-11-22T10:45:00.123Z", Root.GetProperty("timestampUtc").GetString());
            Assert.Equal(7, Root.GetProperty("currentIndex").GetInt32());
            Assert.Equal(25, Root.GetProperty("totalItems").GetInt32());
            Assert.Equal("Clip 7 von 25 verarbeitet", Root.GetProperty("message").GetString());
        }

        HeartbeatMessage? RoundTripped = JsonSerializer.Deserialize<HeartbeatMessage>(Json, IpcJsonOptions.Default);
        Assert.NotNull(RoundTripped);
        Assert.Equal(Heartbeat.JobId, RoundTripped!.JobId);
        Assert.Equal(Heartbeat.TimestampUtc, RoundTripped.TimestampUtc);
        Assert.Equal(Heartbeat.CurrentIndex, RoundTripped.CurrentIndex);
        Assert.Equal(Heartbeat.TotalItems, RoundTripped.TotalItems);
        Assert.Equal(Heartbeat.Message, RoundTripped.Message);
    }

    [Fact]
    public void StepResultRoundTripUsesExactCamelCasePropertyNames()
    {
        StepResult Result = new StepResult
        {
            JobId = "guid-1234",
            Status = "ok",
            ProjectPath = @"D:\Edits\JJK_Edit\JJK_Edit.aep",
            CompositionName = "Main_Edit",
            TotalClips = 88,
            ProcessedInBatch = 10,
            SkippedInBatch = 2,
            HasMoreBatches = true,
            NextBatchIndex = 12,
            Error = null,
            ErrorLine = null,
            Message = "Batch 1 completed",
            DetectedState = new DetectedProjectState
            {
                HasEditClipTags = true,
                HasStage1Tags = true,
                HasZoomGroupTags = false,
                MissingPresets = new List<string> { "BlurZoomOut" },
                IsProjectSaved = true,
                HasActiveComp = true
            }
        };

        string Json = JsonSerializer.Serialize(Result, IpcJsonOptions.Default);

        using (JsonDocument Document = JsonDocument.Parse(Json))
        {
            JsonElement Root = Document.RootElement;
            Assert.Equal("guid-1234", Root.GetProperty("jobId").GetString());
            Assert.Equal("ok", Root.GetProperty("status").GetString());
            Assert.Equal(@"D:\Edits\JJK_Edit\JJK_Edit.aep", Root.GetProperty("projectPath").GetString());
            Assert.Equal("Main_Edit", Root.GetProperty("compositionName").GetString());
            Assert.Equal(88, Root.GetProperty("totalClips").GetInt32());
            Assert.Equal(10, Root.GetProperty("processedInBatch").GetInt32());
            Assert.Equal(2, Root.GetProperty("skippedInBatch").GetInt32());
            Assert.True(Root.GetProperty("hasMoreBatches").GetBoolean());
            Assert.Equal(12, Root.GetProperty("nextBatchIndex").GetInt32());
            Assert.Equal(JsonValueKind.Null, Root.GetProperty("error").ValueKind);
            Assert.Equal(JsonValueKind.Null, Root.GetProperty("errorLine").ValueKind);
            Assert.Equal("Batch 1 completed", Root.GetProperty("message").GetString());

            JsonElement DetectedNode = Root.GetProperty("detectedState");
            Assert.True(DetectedNode.GetProperty("hasEditClipTags").GetBoolean());
            Assert.True(DetectedNode.GetProperty("hasStage1Tags").GetBoolean());
            Assert.False(DetectedNode.GetProperty("hasZoomGroupTags").GetBoolean());
            Assert.Equal("BlurZoomOut", DetectedNode.GetProperty("missingPresets")[0].GetString());
            Assert.True(DetectedNode.GetProperty("isProjectSaved").GetBoolean());
            Assert.True(DetectedNode.GetProperty("hasActiveComp").GetBoolean());
        }

        StepResult? RoundTripped = JsonSerializer.Deserialize<StepResult>(Json, IpcJsonOptions.Default);
        Assert.NotNull(RoundTripped);
        Assert.True(RoundTripped!.IsOk);
        Assert.Equal(Result.TotalClips, RoundTripped.TotalClips);
        Assert.Equal(Result.NextBatchIndex, RoundTripped.NextBatchIndex);
        Assert.NotNull(RoundTripped.DetectedState);
        Assert.True(RoundTripped.DetectedState!.HasStage1Tags);
        Assert.False(RoundTripped.DetectedState.HasZoomGroupTags);
        Assert.Single(RoundTripped.DetectedState.MissingPresets);
    }

    [Fact]
    public void StepResultDeserializesPdfSpecSampleCorrectly()
    {
        string PdfResultJson = """
            {
              "jobId": "guid-1234",
              "status": "ok",
              "projectPath": "D:\\Edits\\JJK_Edit\\JJK_Edit.aep",
              "totalClips": 88,
              "processedInBatch": 10,
              "skippedInBatch": 0,
              "hasMoreBatches": true,
              "nextBatchIndex": 10,
              "detectedState": {
                "hasStage1Tags": true,
                "hasZoomGroupTags": false
              },
              "error": null
            }
            """;

        StepResult? Parsed = JsonSerializer.Deserialize<StepResult>(PdfResultJson, IpcJsonOptions.Default);

        Assert.NotNull(Parsed);
        Assert.True(Parsed!.IsOk);
        Assert.Equal("guid-1234", Parsed.JobId);
        Assert.Equal(@"D:\Edits\JJK_Edit\JJK_Edit.aep", Parsed.ProjectPath);
        Assert.Equal(88, Parsed.TotalClips);
        Assert.Equal(10, Parsed.ProcessedInBatch);
        Assert.Equal(0, Parsed.SkippedInBatch);
        Assert.True(Parsed.HasMoreBatches);
        Assert.Equal(10, Parsed.NextBatchIndex);
        Assert.NotNull(Parsed.DetectedState);
        Assert.True(Parsed.DetectedState!.HasStage1Tags);
        Assert.False(Parsed.DetectedState.HasZoomGroupTags);
        Assert.Null(Parsed.Error);
    }

    [Fact]
    public void StepResultErrorPayloadSetsIsOkFalse()
    {
        string ErrorJson = """
            {
              "jobId": "err-99",
              "status": "error",
              "error": "No active composition",
              "errorLine": 47
            }
            """;

        StepResult? Parsed = JsonSerializer.Deserialize<StepResult>(ErrorJson, IpcJsonOptions.Default);

        Assert.NotNull(Parsed);
        Assert.False(Parsed!.IsOk);
        Assert.Equal("No active composition", Parsed.Error);
        Assert.Equal(47, Parsed.ErrorLine);
    }

    #endregion
}
