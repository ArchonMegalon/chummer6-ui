using System.Security.Cryptography;
using System.Text;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed record AvaloniaCreationWizardCheckpointLoad(
    CharacterCreationWizardDesktopCheckpoint? Checkpoint,
    string? RecoveryReason);

/// <summary>
/// Cross-platform Windows/macOS checkpoint persistence for UI navigation only. Writes are
/// flushed to a sibling temporary file before atomic replacement; corrupt files fail closed and
/// can be replaced by the next authoritative snapshot.
/// </summary>
internal sealed class AvaloniaCreationWizardCheckpointStore
{
    internal const int MaximumCheckpointBytes = 64 * 1024;
    private readonly string _root;

    internal AvaloniaCreationWizardCheckpointStore(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
    }

    internal static AvaloniaCreationWizardCheckpointStore CreateDefault()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            throw new InvalidOperationException("Local application data is unavailable for wizard checkpoint recovery.");

        return new AvaloniaCreationWizardCheckpointStore(
            Path.Combine(appData, "Chummer", "creation-wizard-checkpoints"));
    }

    internal AvaloniaCreationWizardCheckpointLoad Load(string workspaceId)
    {
        string path = ResolvePath(workspaceId);
        if (!File.Exists(path))
            return new AvaloniaCreationWizardCheckpointLoad(null, null);

        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                return new AvaloniaCreationWizardCheckpointLoad(
                    null,
                    CharacterCreationWizardCheckpointInvalidationReasons.InvalidCheckpoint);
            }

            byte[]? payload = ReadBounded(path);
            if (payload is null)
            {
                return new AvaloniaCreationWizardCheckpointLoad(
                    null,
                    CharacterCreationWizardCheckpointInvalidationReasons.InvalidCheckpoint);
            }

            return CharacterCreationWizardDesktopSession.TryDeserializeCheckpoint(payload, out CharacterCreationWizardDesktopCheckpoint? checkpoint)
                ? new AvaloniaCreationWizardCheckpointLoad(checkpoint, null)
                : new AvaloniaCreationWizardCheckpointLoad(
                    null,
                    CharacterCreationWizardCheckpointInvalidationReasons.InvalidCheckpoint);
        }
        catch (IOException)
        {
            return new AvaloniaCreationWizardCheckpointLoad(
                null,
                CharacterCreationWizardCheckpointInvalidationReasons.InvalidCheckpoint);
        }
        catch (UnauthorizedAccessException)
        {
            return new AvaloniaCreationWizardCheckpointLoad(
                null,
                CharacterCreationWizardCheckpointInvalidationReasons.InvalidCheckpoint);
        }
    }

    internal void Save(CharacterCreationWizardDesktopCheckpoint checkpoint)
    {
        byte[] payload = CharacterCreationWizardDesktopSession.SerializeCheckpoint(checkpoint);
        Directory.CreateDirectory(_root);
        string path = ResolvePath(checkpoint.WorkspaceId);
        string temporary = Path.Combine(
            _root,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream output = new(
                       temporary,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                output.Write(payload);
                output.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    internal void Delete(string workspaceId)
    {
        string path = ResolvePath(workspaceId);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string ResolvePath(string workspaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(workspaceId)))
            .ToLowerInvariant();
        return Path.Combine(_root, $"{digest}.json");
    }

    private static byte[]? ReadBounded(string path)
    {
        using FileStream input = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        if (input.Length > MaximumCheckpointBytes)
            return null;

        byte[] payload = new byte[(int)input.Length];
        int count = 0;
        while (count < payload.Length)
        {
            int read = input.Read(payload, count, payload.Length - count);
            if (read == 0)
                return null;
            count += read;
        }

        return payload;
    }
}
