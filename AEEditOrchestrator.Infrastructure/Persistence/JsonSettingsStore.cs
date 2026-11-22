using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Models;
using AEEditOrchestrator.Core.Models.Ipc;
using Microsoft.Win32;

namespace AEEditOrchestrator.Infrastructure.Persistence;

public sealed class JsonSettingsStore : ISettingsStore
{
    #region Constants

    private const string AppFolderName = "AEEditOrchestrator";
    private const string SettingsFileName = "settings.json";
    private const string AfterEffects2026FolderName = "Adobe After Effects 2026";
    private const string AfterEffectsFolderPrefix = "Adobe After Effects";
    private const string SupportFilesFolderName = "Support Files";
    private const string AfterEffectsExecutableName = "AfterFX.exe";
    private const string DocumentsAe2026FolderName = "After Effects 2026";
    private const string DocumentsAePrefix = "After Effects";
    private const string UserPresetsFolderName = "User Presets";
    private const string RegistryAfterEffectsKeyPath = @"SOFTWARE\Adobe\After Effects";

    #endregion

    #region Fields

    private readonly string SettingsFilePathField;
    private readonly UTF8Encoding Utf8NoBomEncodingField = new UTF8Encoding(false);

    #endregion

    #region Properties

    public string SettingsFilePath
    {
        get
        {
            return this.SettingsFilePathField;
        }
    }

    #endregion

    #region Constructors

    public JsonSettingsStore()
    {
        string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        this.SettingsFilePathField = Path.Combine(
            LocalAppData,
            JsonSettingsStore.AppFolderName,
            JsonSettingsStore.SettingsFileName);
    }

    public JsonSettingsStore(string SettingsFilePathOverride)
    {
        if (string.IsNullOrWhiteSpace(SettingsFilePathOverride))
        {
            throw new ArgumentException("SettingsFilePathOverride must not be empty.", nameof(SettingsFilePathOverride));
        }

        this.SettingsFilePathField = Path.GetFullPath(SettingsFilePathOverride);
    }

    #endregion

    #region Public Methods

    public async Task<PipelineSettings> LoadAsync(CancellationToken CancellationToken = default)
    {
        CancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(this.SettingsFilePathField))
        {
            return this.CreateDefaultSettingsWithDetection();
        }

        try
        {
            using (FileStream Stream = new FileStream(
                this.SettingsFilePathField,
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
                        return this.CreateDefaultSettingsWithDetection();
                    }

                    PipelineSettings? Loaded = JsonSerializer.Deserialize<PipelineSettings>(CleanedJson, IpcJsonOptions.Default);
                    if (Loaded == null)
                    {
                        return this.CreateDefaultSettingsWithDetection();
                    }

                    return Loaded;
                }
            }
        }
        catch (JsonException)
        {
            return this.CreateDefaultSettingsWithDetection();
        }
        catch (IOException)
        {
            return this.CreateDefaultSettingsWithDetection();
        }
    }

    public async Task SaveAsync(PipelineSettings Settings, CancellationToken CancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Settings);

        CancellationToken.ThrowIfCancellationRequested();

        string? ParentDirectory = Path.GetDirectoryName(this.SettingsFilePathField);
        if (!string.IsNullOrWhiteSpace(ParentDirectory))
        {
            Directory.CreateDirectory(ParentDirectory);
        }

        string JsonContent = JsonSerializer.Serialize(Settings, IpcJsonOptions.Default);
        string TempDirectory = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(ParentDirectory))
        {
            TempDirectory = ParentDirectory;
        }

        string TempFilePath = Path.Combine(
            TempDirectory,
            string.Concat(".settings_", Guid.NewGuid().ToString("N"), ".tmp"));

        try
        {
            await File.WriteAllTextAsync(TempFilePath, JsonContent, this.Utf8NoBomEncodingField, CancellationToken).ConfigureAwait(false);
            File.Move(TempFilePath, this.SettingsFilePathField, overwrite: true);
        }
        finally
        {
            JsonSettingsStore.TryDeleteFile(TempFilePath);
        }
    }

    public string? DetectAfterEffectsExecutablePath()
    {
        List<string> ProgramFilesRoots = new List<string>();
        string ProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string ProgramFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (!string.IsNullOrWhiteSpace(ProgramFiles))
        {
            ProgramFilesRoots.Add(ProgramFiles);
        }

        if (!string.IsNullOrWhiteSpace(ProgramFilesX86) && !string.Equals(ProgramFilesX86, ProgramFiles, StringComparison.OrdinalIgnoreCase))
        {
            ProgramFilesRoots.Add(ProgramFilesX86);
        }

        foreach (string Root in ProgramFilesRoots)
        {
            string Preferred2026Path = Path.Combine(
                Root,
                "Adobe",
                JsonSettingsStore.AfterEffects2026FolderName,
                JsonSettingsStore.SupportFilesFolderName,
                JsonSettingsStore.AfterEffectsExecutableName);

            if (File.Exists(Preferred2026Path))
            {
                return Preferred2026Path;
            }
        }

        foreach (string Root in ProgramFilesRoots)
        {
            string AdobeDirectory = Path.Combine(Root, "Adobe");
            if (!Directory.Exists(AdobeDirectory))
            {
                continue;
            }

            string[] CandidateDirectories = Directory.GetDirectories(AdobeDirectory, string.Concat(JsonSettingsStore.AfterEffectsFolderPrefix, "*"));
            Array.Sort(CandidateDirectories, JsonSettingsStore.CompareAfterEffectsFoldersDescending);

            foreach (string CandidateDir in CandidateDirectories)
            {
                string SupportFilesCandidate = Path.Combine(
                    CandidateDir,
                    JsonSettingsStore.SupportFilesFolderName,
                    JsonSettingsStore.AfterEffectsExecutableName);

                if (File.Exists(SupportFilesCandidate))
                {
                    return SupportFilesCandidate;
                }

                string DirectCandidate = Path.Combine(CandidateDir, JsonSettingsStore.AfterEffectsExecutableName);
                if (File.Exists(DirectCandidate))
                {
                    return DirectCandidate;
                }
            }
        }

        if (OperatingSystem.IsWindows())
        {
            string? RegistryPath = JsonSettingsStore.DetectAfterEffectsFromRegistry();
            if (!string.IsNullOrWhiteSpace(RegistryPath))
            {
                return RegistryPath;
            }
        }

        return null;
    }

    public string? DetectUserPresetsFolderPath()
    {
        string DocumentsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(DocumentsDirectory))
        {
            return null;
        }

        string AdobeDocumentsDirectory = Path.Combine(DocumentsDirectory, "Adobe");
        if (!Directory.Exists(AdobeDocumentsDirectory))
        {
            return null;
        }

        string Preferred2026Presets = Path.Combine(
            AdobeDocumentsDirectory,
            JsonSettingsStore.DocumentsAe2026FolderName,
            JsonSettingsStore.UserPresetsFolderName);

        if (Directory.Exists(Preferred2026Presets))
        {
            return Preferred2026Presets;
        }

        string[] CandidateDirectories = Directory.GetDirectories(
            AdobeDocumentsDirectory,
            string.Concat(JsonSettingsStore.DocumentsAePrefix, "*"));

        Array.Sort(CandidateDirectories, JsonSettingsStore.CompareAfterEffectsFoldersDescending);

        foreach (string CandidateDir in CandidateDirectories)
        {
            string PresetsCandidate = Path.Combine(CandidateDir, JsonSettingsStore.UserPresetsFolderName);
            if (Directory.Exists(PresetsCandidate))
            {
                return PresetsCandidate;
            }
        }

        return null;
    }

    public IReadOnlyList<PresetValidationItem> ValidatePresetsOnDisk(PipelineSettings Settings)
    {
        ArgumentNullException.ThrowIfNull(Settings);

        Dictionary<string, string> DiscoveredFilesByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(Settings.UserPresetsRootPath) && Directory.Exists(Settings.UserPresetsRootPath))
        {
            List<string> AllFfxFiles = new List<string>(
                Directory.EnumerateFiles(Settings.UserPresetsRootPath, "*.ffx", SearchOption.AllDirectories));

            AllFfxFiles.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (string FilePath in AllFfxFiles)
            {
                string FileName = Path.GetFileName(FilePath);
                if (!DiscoveredFilesByName.ContainsKey(FileName))
                {
                    DiscoveredFilesByName[FileName] = FilePath;
                }
            }
        }

        PresetMappingSettings Mapping = Settings.Presets;
        List<KeyValuePair<string, string>> ExpectedPresets = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("FadeInOut", Mapping.FadeInOut),
            new KeyValuePair<string, string>("VerticalShake", Mapping.VerticalShake),
            new KeyValuePair<string, string>("VerticalBlur", Mapping.VerticalBlur),
            new KeyValuePair<string, string>("HorizontalShake", Mapping.HorizontalShake),
            new KeyValuePair<string, string>("HorizontalBlur", Mapping.HorizontalBlur),
            new KeyValuePair<string, string>("BlurZoomIn", Mapping.BlurZoomIn),
            new KeyValuePair<string, string>("BlurZoomOut", Mapping.BlurZoomOut)
        };

        List<PresetValidationItem> Results = new List<PresetValidationItem>();

        foreach (KeyValuePair<string, string> Entry in ExpectedPresets)
        {
            string PresetKey = Entry.Key;
            string PresetName = Entry.Value.Trim();

            if (string.IsNullOrWhiteSpace(PresetName))
            {
                Results.Add(new PresetValidationItem
                {
                    PresetKey = PresetKey,
                    PresetName = string.Empty,
                    IsFound = false,
                    FullPath = string.Empty
                });
                continue;
            }

            string ExpectedFileName = PresetName;
            if (!ExpectedFileName.EndsWith(".ffx", StringComparison.OrdinalIgnoreCase))
            {
                ExpectedFileName = string.Concat(ExpectedFileName, ".ffx");
            }

            string? FoundPath;
            if (DiscoveredFilesByName.TryGetValue(ExpectedFileName, out FoundPath))
            {
                Results.Add(new PresetValidationItem
                {
                    PresetKey = PresetKey,
                    PresetName = PresetName,
                    IsFound = true,
                    FullPath = FoundPath
                });
            }
            else
            {
                Results.Add(new PresetValidationItem
                {
                    PresetKey = PresetKey,
                    PresetName = PresetName,
                    IsFound = false,
                    FullPath = string.Empty
                });
            }
        }

        return Results;
    }

    #endregion

    #region Private Methods

    private PipelineSettings CreateDefaultSettingsWithDetection()
    {
        PipelineSettings Defaults = new PipelineSettings();
        Defaults.AfterEffectsExecutablePath = this.DetectAfterEffectsExecutablePath() ?? string.Empty;
        Defaults.UserPresetsRootPath = this.DetectUserPresetsFolderPath() ?? string.Empty;
        return Defaults;
    }

    [SupportedOSPlatform("windows")]
    private static string? DetectAfterEffectsFromRegistry()
    {
        RegistryView[] Views = new RegistryView[]
        {
            RegistryView.Registry64,
            RegistryView.Registry32
        };

        foreach (RegistryView CurrentView in Views)
        {
            try
            {
                using (RegistryKey BaseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, CurrentView))
                {
                    using (RegistryKey? AeRootKey = BaseKey.OpenSubKey(JsonSettingsStore.RegistryAfterEffectsKeyPath))
                    {
                        if (AeRootKey == null)
                        {
                            continue;
                        }

                        string[] SubKeyNames = AeRootKey.GetSubKeyNames();
                        Array.Sort(SubKeyNames, JsonSettingsStore.CompareVersionNamesDescending);

                        foreach (string SubKeyName in SubKeyNames)
                        {
                            using (RegistryKey? VersionKey = AeRootKey.OpenSubKey(SubKeyName))
                            {
                                if (VersionKey == null)
                                {
                                    continue;
                                }

                                string? InstallPath = VersionKey.GetValue("InstallPath") as string;
                                if (string.IsNullOrWhiteSpace(InstallPath))
                                {
                                    InstallPath = VersionKey.GetValue("Path") as string;
                                }

                                if (string.IsNullOrWhiteSpace(InstallPath))
                                {
                                    InstallPath = VersionKey.GetValue(null) as string;
                                }

                                if (string.IsNullOrWhiteSpace(InstallPath))
                                {
                                    continue;
                                }

                                string DirectCandidate = Path.Combine(InstallPath, JsonSettingsStore.AfterEffectsExecutableName);
                                if (File.Exists(DirectCandidate))
                                {
                                    return DirectCandidate;
                                }

                                string SupportCandidate = Path.Combine(
                                    InstallPath,
                                    JsonSettingsStore.SupportFilesFolderName,
                                    JsonSettingsStore.AfterEffectsExecutableName);

                                if (File.Exists(SupportCandidate))
                                {
                                    return SupportCandidate;
                                }
                            }
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (System.Security.SecurityException)
            {
            }
        }

        return null;
    }

    private static int CompareAfterEffectsFoldersDescending(string Left, string Right)
    {
        string LeftName = Path.GetFileName(Left);
        string RightName = Path.GetFileName(Right);

        int LeftYear;
        int RightYear;
        bool HasLeftYear = JsonSettingsStore.TryExtractFourDigitYear(LeftName, out LeftYear);
        bool HasRightYear = JsonSettingsStore.TryExtractFourDigitYear(RightName, out RightYear);

        if (HasLeftYear && HasRightYear)
        {
            int YearCompare = RightYear.CompareTo(LeftYear);
            if (YearCompare != 0)
            {
                return YearCompare;
            }
        }
        else if (HasRightYear)
        {
            return 1;
        }
        else if (HasLeftYear)
        {
            return -1;
        }

        return string.Compare(RightName, LeftName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractFourDigitYear(string FolderName, out int Year)
    {
        Year = 0;
        if (string.IsNullOrWhiteSpace(FolderName))
        {
            return false;
        }

        string[] Tokens = FolderName.Split(new char[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        for (int Index = Tokens.Length - 1; Index >= 0; Index--)
        {
            string Token = Tokens[Index];
            int ParsedYear;
            if (Token.Length == 4 && int.TryParse(Token, out ParsedYear) && ParsedYear >= 2000 && ParsedYear <= 2099)
            {
                Year = ParsedYear;
                return true;
            }
        }

        return false;
    }

    private static int CompareVersionNamesDescending(string Left, string Right)
    {
        Version? LeftVersion;
        Version? RightVersion;
        bool LeftParsed = Version.TryParse(Left, out LeftVersion);
        bool RightParsed = Version.TryParse(Right, out RightVersion);

        if (LeftParsed && RightParsed && LeftVersion != null && RightVersion != null)
        {
            return RightVersion.CompareTo(LeftVersion);
        }

        return string.Compare(Right, Left, StringComparison.OrdinalIgnoreCase);
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

    #endregion
}
