using System.IO;
using System.Linq;
using Avalonia.Platform.Storage;
using Chummer.Desktop.Runtime;
using Chummer.Contracts.Workspaces;

namespace Chummer.Avalonia;

internal static class MainWindowDesktopFileCoordinator
{
    private const string BundledDemoRelativePath = "Samples/Legacy/Soma-Career.chum5";
    private const string BundledDemoOverrideEnvironmentVariable = "CHUMMER_BUNDLED_DEMO_RUNNER_PATH";
    internal static Func<IStorageProvider, string, CancellationToken, Task<DesktopImportFileResult>>? OpenImportFileOverride { get; set; }
    internal static Func<IStorageProvider, string, CancellationToken, Task<string?>>? OpenFolderPickerOverride { get; set; }
    internal static Func<IStorageProvider, PendingDownloadDispatchRequest, CancellationToken, Task<DesktopDownloadSaveResult>>? SaveDownloadOverride { get; set; }
    internal static Func<IStorageProvider, PendingExportDispatchRequest, CancellationToken, Task<DesktopDownloadSaveResult>>? SaveExportOverride { get; set; }
    internal static Func<IStorageProvider, PendingPrintDispatchRequest, CancellationToken, Task<DesktopDownloadSaveResult>>? SavePrintOverride { get; set; }

    public static async Task<DesktopImportFileResult> OpenImportFileAsync(
        IStorageProvider storageProvider,
        string title,
        CancellationToken ct)
    {
        if (OpenImportFileOverride is not null)
        {
            return await OpenImportFileOverride(storageProvider, title, ct);
        }

        if (!storageProvider.CanOpen)
        {
            return new DesktopImportFileResult(DesktopFileOperationOutcome.Unavailable, Payload: null, SourceLabel: null);
        }

        IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Chummer Character Files")
                {
                    Patterns = ["*.chum5", "*.xml"]
                }
            ]
        });

        IStorageFile? file = files.FirstOrDefault();
        if (file is null)
        {
            return new DesktopImportFileResult(DesktopFileOperationOutcome.Cancelled, Payload: null, SourceLabel: null);
        }

        await using Stream stream = await file.OpenReadAsync();
        using MemoryStream memory = new();
        await stream.CopyToAsync(memory, ct);
        return new DesktopImportFileResult(DesktopFileOperationOutcome.Completed, memory.ToArray(), file.Name);
    }

    public static async Task<DesktopImportFileResult> OpenBundledDemoRunnerAsync(CancellationToken ct)
    {
        string? samplePath = ResolveBundledDemoRunnerPath();
        if (samplePath is null)
        {
            return new DesktopImportFileResult(DesktopFileOperationOutcome.Unavailable, Payload: null, SourceLabel: BundledDemoRelativePath);
        }

        byte[] payload = await File.ReadAllBytesAsync(samplePath, ct);
        return new DesktopImportFileResult(
            DesktopFileOperationOutcome.Completed,
            payload,
            "Samples/Legacy/Soma-Career.chum5");
    }

    private static string? ResolveBundledDemoRunnerPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable(BundledDemoOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            string normalizedOverridePath = Path.GetFullPath(overridePath);
            if (File.Exists(normalizedOverridePath))
            {
                return normalizedOverridePath;
            }
        }

        IEnumerable<string> workingDirectories = EnumerateWorkingDirectories();
        string? repoRoot = DesktopRepoRootLocator.TryResolveChummerPresentationRepoRoot(
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory());
        IEnumerable<string> repoRootCandidates = string.IsNullOrWhiteSpace(repoRoot)
            ? Array.Empty<string>()
            : [repoRoot];
        IEnumerable<string> candidates =
        [
            .. workingDirectories.Select(path => Path.Combine(path, BundledDemoRelativePath)),
            .. workingDirectories.Select(path => Path.Combine(path, "Chummer.Avalonia", BundledDemoRelativePath)),
            .. repoRootCandidates.Select(path => Path.Combine(path, "Chummer.Avalonia", BundledDemoRelativePath))
        ];

        return candidates
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> EnumerateWorkingDirectories()
    {
        yield return AppContext.BaseDirectory;
        yield return Path.Combine(AppContext.BaseDirectory, "..");
        yield return Path.Combine(AppContext.BaseDirectory, "..", "..");
        yield return Directory.GetCurrentDirectory();
    }

    public static async Task<string?> OpenFolderAsync(
        IStorageProvider storageProvider,
        string title,
        CancellationToken ct)
    {
        if (OpenFolderPickerOverride is not null)
        {
            return await OpenFolderPickerOverride(storageProvider, title, ct);
        }

        if (!storageProvider.CanPickFolder)
        {
            return null;
        }

        IReadOnlyList<IStorageFolder> folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public static async Task<DesktopDownloadSaveResult> SaveDownloadAsync(
        IStorageProvider storageProvider,
        PendingDownloadDispatchRequest request,
        CancellationToken ct)
    {
        if (SaveDownloadOverride is not null)
        {
            return await SaveDownloadOverride(storageProvider, request, ct);
        }

        if (!storageProvider.CanSave)
        {
            return new DesktopDownloadSaveResult(DesktopFileOperationOutcome.Unavailable, Notice: null);
        }

        IReadOnlyList<FilePickerFileType> fileTypes =
            request.Download.Format == WorkspaceDocumentFormat.Json
                ? [
                    new FilePickerFileType("JSON Files")
                    {
                        Patterns = ["*.json"],
                        MimeTypes = ["application/json"]
                    }
                ]
                : [
                    new FilePickerFileType("Chummer Character Files")
                    {
                        Patterns = ["*.chum5", "*.xml"],
                        MimeTypes = ["application/xml"]
                    }
                ];

        string pickerTitle = request.Download.Format == WorkspaceDocumentFormat.Json
            ? "Download Export Bundle"
            : "Save Character As";

        IStorageFile? targetFile = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = pickerTitle,
            SuggestedFileName = request.Download.FileName,
            FileTypeChoices = fileTypes,
            ShowOverwritePrompt = true
        });

        if (targetFile is null)
        {
            return new DesktopDownloadSaveResult(DesktopFileOperationOutcome.Cancelled, Notice: null);
        }

        byte[] payloadBytes = Convert.FromBase64String(request.Download.ContentBase64);
        await using Stream output = await targetFile.OpenWriteAsync();
        if (output.CanSeek)
        {
            output.SetLength(0);
        }

        await output.WriteAsync(payloadBytes, ct);
        await output.FlushAsync(ct);
        return new DesktopDownloadSaveResult(
            DesktopFileOperationOutcome.Completed,
            BuildSavedFileNotice("Downloaded", request.Download.FileName, targetFile.Name));
    }

    public static async Task<DesktopDownloadSaveResult> SaveExportAsync(
        IStorageProvider storageProvider,
        PendingExportDispatchRequest request,
        CancellationToken ct)
    {
        if (SaveExportOverride is not null)
        {
            return await SaveExportOverride(storageProvider, request, ct);
        }

        return await SaveBase64PayloadAsync(
            storageProvider,
            pickerTitle: "Save Export Bundle",
            suggestedFileName: request.Export.FileName,
            contentBase64: request.Export.ContentBase64,
            noticePrefix: "exported",
            fileTypes:
            [
                new FilePickerFileType("JSON Files")
                {
                    Patterns = ["*.json"],
                    MimeTypes = ["application/json"]
                }
            ],
            ct);
    }

    public static async Task<DesktopDownloadSaveResult> SavePrintAsync(
        IStorageProvider storageProvider,
        PendingPrintDispatchRequest request,
        CancellationToken ct)
    {
        if (SavePrintOverride is not null)
        {
            return await SavePrintOverride(storageProvider, request, ct);
        }

        return await SaveBase64PayloadAsync(
            storageProvider,
            pickerTitle: "Save Print Preview",
            suggestedFileName: request.Print.FileName,
            contentBase64: request.Print.ContentBase64,
            noticePrefix: "saved print preview",
            fileTypes:
            [
                new FilePickerFileType("HTML Files")
                {
                    Patterns = ["*.html", "*.htm"],
                    MimeTypes = ["text/html"]
                }
            ],
            ct);
    }

    private static async Task<DesktopDownloadSaveResult> SaveBase64PayloadAsync(
        IStorageProvider storageProvider,
        string pickerTitle,
        string suggestedFileName,
        string contentBase64,
        string noticePrefix,
        IReadOnlyList<FilePickerFileType> fileTypes,
        CancellationToken ct)
    {
        if (!storageProvider.CanSave)
        {
            return new DesktopDownloadSaveResult(DesktopFileOperationOutcome.Unavailable, Notice: null);
        }

        IStorageFile? targetFile = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = pickerTitle,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = fileTypes,
            ShowOverwritePrompt = true
        });

        if (targetFile is null)
        {
            return new DesktopDownloadSaveResult(DesktopFileOperationOutcome.Cancelled, Notice: null);
        }

        byte[] payloadBytes = Convert.FromBase64String(contentBase64);
        await using Stream output = await targetFile.OpenWriteAsync();
        if (output.CanSeek)
        {
            output.SetLength(0);
        }

        await output.WriteAsync(payloadBytes, ct);
        await output.FlushAsync(ct);
        return new DesktopDownloadSaveResult(
            DesktopFileOperationOutcome.Completed,
            BuildSavedFileNotice(CapitalizeNoticePrefix(noticePrefix), suggestedFileName, targetFile.Name));
    }

    private static string BuildSavedFileNotice(string action, string sourceName, string targetName)
        => $"{action} {sourceName} to {targetName}.";

    private static string CapitalizeNoticePrefix(string noticePrefix)
        => string.IsNullOrWhiteSpace(noticePrefix)
            ? "Saved"
            : char.ToUpperInvariant(noticePrefix[0]) + noticePrefix[1..];
}

internal enum DesktopFileOperationOutcome
{
    Unavailable,
    Cancelled,
    Completed
}

internal sealed record DesktopImportFileResult(
    DesktopFileOperationOutcome Outcome,
    byte[]? Payload,
    string? SourceLabel);

internal sealed record DesktopDownloadSaveResult(
    DesktopFileOperationOutcome Outcome,
    string? Notice);
