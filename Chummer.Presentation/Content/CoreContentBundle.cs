using System.Security.Cryptography;
using System.Text.Json;

namespace Chummer.Presentation.Content;

/// <summary>Verify every bundled file against a manifest whose digest is embedded in the app.</summary>
public static class CoreContentBundle
{
    public const string ManifestFileName = "core-content-manifest.json";
    public const int ManifestSchemaVersion = 2;
    public const int ContentCommitHexLength = 40;

    public static bool TryReadManifestCommit(string bundleRoot, out string? contentCommit, out string? failure)
    {
        contentCommit = null;
        try
        {
            using var document = JsonDocument.Parse(ReadManifest(bundleRoot));
            if (!TryHeader(document.RootElement, out contentCommit))
                return Fail("The bundled rules manifest is invalid. Repair the installation.", out failure);
            failure = null;
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException)
        {
            return Fail("The bundled rules manifest could not be read. Repair the installation.", out failure);
        }
    }

    public static string? Validate(string candidate, IReadOnlyCollection<string> pinnedCommits,
        out string? manifestCommit, string? expectedManifestSha256 = null)
    {
        manifestCommit = null;
        if (!Hex(expectedManifestSha256, 64)) return "This application has no trusted rules-bundle identity. Use a packaged build.";
        try
        {
            if (HasLink(candidate)) return "The rules bundle cannot be a linked directory.";
            byte[] bytes = ReadManifest(candidate);
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), expectedManifestSha256, StringComparison.OrdinalIgnoreCase))
                return "The rules manifest does not match this application. Repair the installation.";
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (!TryHeader(root, out var commit) || !pinnedCommits.Contains(commit!, StringComparer.Ordinal)
                || !root.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
                return "The rules manifest is invalid or belongs to another content version. Repair the installation.";
            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in files.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("path", out var pathProperty)
                    || pathProperty.ValueKind != JsonValueKind.String || pathProperty.GetString() is not { } relative
                    || !SafeRelativePath(relative) || !expected.Add(relative)
                    || !item.TryGetProperty("sha256", out var hash) || hash.ValueKind != JsonValueKind.String || !Hex(hash.GetString(), 64)
                    || !item.TryGetProperty("bytes", out var length) || length.ValueKind != JsonValueKind.Number || !length.TryGetInt64(out var size) || size < 0)
                    return "The rules inventory is invalid. Repair the installation.";
                string path = candidate;
                foreach (string component in relative.Split('/'))
                {
                    path = Path.Combine(path, component);
                    if (HasLink(path)) return "Linked files or directories are not permitted in the rules bundle.";
                }
                using var stream = File.OpenRead(path);
                if (stream.Length != size || !string.Equals(Convert.ToHexString(SHA256.HashData(stream)), hash.GetString(), StringComparison.OrdinalIgnoreCase))
                    return "A bundled rules file is missing or changed. Repair the installation.";
            }
            var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var directories = new Stack<string>(); directories.Push(candidate);
            while (directories.TryPop(out var directory))
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (HasLink(entry)) return "Linked files or directories are not permitted in the rules bundle.";
                    if (Directory.Exists(entry)) { directories.Push(entry); continue; }
                    string relative = Path.GetRelativePath(candidate, entry).Replace(Path.DirectorySeparatorChar, '/');
                    if (relative != ManifestFileName && !actual.Add(relative)) return "Duplicate rules files were found. Repair the installation.";
                }
            }
            if (!expected.SetEquals(actual)) return "The rules bundle contains missing or unexpected files. Repair the installation.";
            string? structure = ValidateStructure(candidate);
            if (structure is not null) return structure;
            manifestCommit = commit;
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            return "The bundled rules could not be verified. Repair the installation.";
        }
    }

    private static byte[] ReadManifest(string root)
    {
        string path = Path.Combine(root, ManifestFileName);
        if (HasLink(path) || new FileInfo(path).Length > 8 * 1024 * 1024) throw new IOException("Invalid manifest file.");
        return File.ReadAllBytes(path);
    }
    private static bool TryHeader(JsonElement root, out string? commit)
    {
        commit = null;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("schemaVersion", out var schema) || schema.ValueKind != JsonValueKind.Number
            || !schema.TryGetInt32(out int version) || version != ManifestSchemaVersion
            || !root.TryGetProperty("contentCommit", out var value) || value.ValueKind != JsonValueKind.String
            || !Hex(value.GetString(), ContentCommitHexLength)) return false;
        commit = value.GetString(); return true;
    }
    private static bool Hex(string? value, int length) => value?.Length == length && value.All(Uri.IsHexDigit);
    private static bool HasLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    private static bool SafeRelativePath(string path) => path.StartsWith("Chummer/", StringComparison.Ordinal)
        && !path.Contains('\\') && !path.Contains(':') && path.Split('/').All(p => p.Length > 0 && p is not "." and not ".." && !p.EndsWith('.') && !p.EndsWith(' '));

    public static string? ValidateStructure(string candidate)
    {
        foreach (var name in new[] { "settings-all-sources.xml", "settings.xml", "lifemodules.xml" })
            if (!File.Exists(Path.Combine(candidate, "Chummer", "data", name))) return "Required bundled rules are missing. Repair the installation.";
        foreach (var name in new[] { "lang", "customdata" })
            if (!Directory.Exists(Path.Combine(candidate, "Chummer", name))) return "Required bundled content directories are missing. Repair the installation.";
        return null;
    }
    private static bool Fail(string message, out string? failure) { failure = message; return false; }
}
