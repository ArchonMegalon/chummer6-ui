using System.IO;
using System.Linq;
using Avalonia.Platform.Storage;
using Chummer.Contracts.Workspaces;

namespace Chummer.Avalonia;

internal static class MainWindowDesktopFileCoordinator
{
    private const string BundledDemoRelativePath = "Samples/Legacy/Soma-Career.chum5";
    internal static Func<IStorageProvider, string, CancellationToken, Task<DesktopImportFileResult>>? OpenImportFileOverride { get; set; }
    internal static Func<IStorageProvider, string, CancellationToken, Task<string?>>? OpenFolderPickerOverride { get; set; }

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
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, BundledDemoRelativePath),
            Path.Combine(AppContext.BaseDirectory, "..", BundledDemoRelativePath),
            Path.Combine(AppContext.BaseDirectory, "..", "..", BundledDemoRelativePath),
            Path.Combine(Directory.GetCurrentDirectory(), BundledDemoRelativePath),
            Path.Combine(Directory.GetCurrentDirectory(), "Chummer.Avalonia", BundledDemoRelativePath),
            Path.Combine("/docker/chummercomplete/chummer-presentation/Chummer.Avalonia", BundledDemoRelativePath)
        ];

        return candidates
            .Select(path => Path.GetFullPath(path))
            .FirstOrDefault(File.Exists);
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
            $"Notice: downloaded {request.Download.FileName} to {targetFile.Name}.");
    }

    public static async Task<DesktopDownloadSaveResult> SaveExportAsync(
        IStorageProvider storageProvider,
        PendingExportDispatchRequest request,
        CancellationToken ct)
    {
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
            $"Notice: {noticePrefix} {suggestedFileName} to {targetFile.Name}.");
    }
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
