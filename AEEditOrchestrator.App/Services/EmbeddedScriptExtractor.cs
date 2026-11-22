using System.Reflection;
using System.Security.Cryptography;

namespace AEEditOrchestrator.App.Services;

public sealed class EmbeddedScriptExtractor
{
    #region Constants

    private const string AppFolderName = "AEEditOrchestrator";
    private const string ScriptsFolderName = "scripts";
    private const string ResourcePrefix = "JsxScripts.";

    #endregion

    #region Fields

    private readonly string TargetDirectoryField;

    #endregion

    #region Properties

    public string TargetDirectory
    {
        get
        {
            return this.TargetDirectoryField;
        }
    }

    #endregion

    #region Constructors

    public EmbeddedScriptExtractor(string? TargetDirectoryOverride = null)
    {
        if (string.IsNullOrWhiteSpace(TargetDirectoryOverride))
        {
            string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            this.TargetDirectoryField = Path.Combine(
                LocalAppData,
                EmbeddedScriptExtractor.AppFolderName,
                EmbeddedScriptExtractor.ScriptsFolderName);
        }
        else
        {
            this.TargetDirectoryField = Path.GetFullPath(TargetDirectoryOverride);
        }
    }

    #endregion

    #region Public Methods

    public string ExtractAllScripts()
    {
        if (!Directory.Exists(this.TargetDirectoryField))
        {
            Directory.CreateDirectory(this.TargetDirectoryField);
        }

        Assembly CurrentAssembly = typeof(EmbeddedScriptExtractor).Assembly;
        string[] ResourceNames = CurrentAssembly.GetManifestResourceNames();

        foreach (string ResourceName in ResourceNames)
        {
            if (!ResourceName.StartsWith(EmbeddedScriptExtractor.ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string ScriptFileName = ResourceName.Substring(EmbeddedScriptExtractor.ResourcePrefix.Length);
            if (string.IsNullOrWhiteSpace(ScriptFileName))
            {
                continue;
            }

            using (Stream? ResourceStream = CurrentAssembly.GetManifestResourceStream(ResourceName))
            {
                if (ResourceStream == null)
                {
                    continue;
                }

                byte[] ResourceBytes = EmbeddedScriptExtractor.ReadAllBytes(ResourceStream);
                string DestinationPath = Path.Combine(this.TargetDirectoryField, ScriptFileName);

                if (EmbeddedScriptExtractor.AreFileBytesIdentical(DestinationPath, ResourceBytes))
                {
                    continue;
                }

                EmbeddedScriptExtractor.WriteFileAtomically(this.TargetDirectoryField, DestinationPath, ResourceBytes);
            }
        }

        return this.TargetDirectoryField;
    }

    #endregion

    #region Private Methods

    private static byte[] ReadAllBytes(Stream InputStream)
    {
        using (MemoryStream BufferStream = new MemoryStream())
        {
            InputStream.CopyTo(BufferStream);
            return BufferStream.ToArray();
        }
    }

    private static bool AreFileBytesIdentical(string FilePath, byte[] ExpectedBytes)
    {
        if (!File.Exists(FilePath))
        {
            return false;
        }

        FileInfo ExistingInfo = new FileInfo(FilePath);
        if (ExistingInfo.Length != ExpectedBytes.LongLength)
        {
            return false;
        }

        byte[] ExistingBytes = File.ReadAllBytes(FilePath);
        byte[] ExistingHash = SHA256.HashData(ExistingBytes);
        byte[] ExpectedHash = SHA256.HashData(ExpectedBytes);
        return CryptographicOperations.FixedTimeEquals(ExistingHash, ExpectedHash);
    }

    private static void WriteFileAtomically(string TargetDirectory, string DestinationPath, byte[] ContentBytes)
    {
        string TempFilePath = Path.Combine(
            TargetDirectory,
            string.Concat(".script_", Guid.NewGuid().ToString("N"), ".tmp"));

        try
        {
            File.WriteAllBytes(TempFilePath, ContentBytes);
            File.Move(TempFilePath, DestinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(TempFilePath))
            {
                try
                {
                    File.Delete(TempFilePath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    #endregion
}
