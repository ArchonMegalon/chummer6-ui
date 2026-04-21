using Chummer.Contracts.Rulesets;
using System.IO;
using System.Text;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    private readonly object _rosterWatchRuntimeSync = new();
    private FileSystemWatcher? _rosterWatchFolderWatcher;
    private CancellationTokenSource? _rosterWatchRefreshDebounce;
    private string? _rosterWatchedFolderPath;
    private string _rosterWatchFolderFingerprint = string.Empty;

    private void SyncRosterWatchRuntime(CharacterOverviewState state)
    {
        string? rosterPath = ResolveActiveRosterWatchFolderPath(state);
        if (string.IsNullOrWhiteSpace(rosterPath) || !Directory.Exists(rosterPath))
        {
            DisposeRosterWatchRuntime();
            return;
        }

        string fingerprint = ComputeRosterWatchFolderFingerprint(rosterPath);
        lock (_rosterWatchRuntimeSync)
        {
            if (string.Equals(_rosterWatchedFolderPath, rosterPath, StringComparison.Ordinal)
                && _rosterWatchFolderWatcher is not null)
            {
                _rosterWatchFolderFingerprint = fingerprint;
                return;
            }
        }

        DisposeRosterWatchRuntime();
        FileSystemWatcher watcher = new(rosterPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
        };

        watcher.Created += OnRosterWatchFolderChanged;
        watcher.Changed += OnRosterWatchFolderChanged;
        watcher.Deleted += OnRosterWatchFolderChanged;
        watcher.Renamed += OnRosterWatchFolderRenamed;
        watcher.EnableRaisingEvents = true;

        lock (_rosterWatchRuntimeSync)
        {
            _rosterWatchFolderWatcher = watcher;
            _rosterWatchedFolderPath = rosterPath;
            _rosterWatchFolderFingerprint = fingerprint;
        }
    }

    private void DisposeRosterWatchRuntime()
    {
        FileSystemWatcher? watcher = null;
        CancellationTokenSource? debounce = null;
        lock (_rosterWatchRuntimeSync)
        {
            watcher = _rosterWatchFolderWatcher;
            debounce = _rosterWatchRefreshDebounce;
            _rosterWatchFolderWatcher = null;
            _rosterWatchRefreshDebounce = null;
            _rosterWatchedFolderPath = null;
            _rosterWatchFolderFingerprint = string.Empty;
        }

        if (watcher is not null)
        {
            watcher.Created -= OnRosterWatchFolderChanged;
            watcher.Changed -= OnRosterWatchFolderChanged;
            watcher.Deleted -= OnRosterWatchFolderChanged;
            watcher.Renamed -= OnRosterWatchFolderRenamed;
            watcher.Dispose();
        }

        if (debounce is not null)
        {
            debounce.Cancel();
            debounce.Dispose();
        }
    }

    private void OnRosterWatchFolderChanged(object sender, FileSystemEventArgs args)
    {
        if (!IsRosterWatchRelevantPath(args.FullPath))
        {
            return;
        }

        QueueRosterWatchRefresh();
    }

    private void OnRosterWatchFolderRenamed(object sender, RenamedEventArgs args)
    {
        if (!IsRosterWatchRelevantPath(args.OldFullPath) && !IsRosterWatchRelevantPath(args.FullPath))
        {
            return;
        }

        QueueRosterWatchRefresh();
    }

    private void QueueRosterWatchRefresh()
    {
        string? rosterPath;
        CancellationTokenSource debounce;
        lock (_rosterWatchRuntimeSync)
        {
            rosterPath = _rosterWatchedFolderPath;
            if (string.IsNullOrWhiteSpace(rosterPath))
            {
                return;
            }

            _rosterWatchRefreshDebounce?.Cancel();
            _rosterWatchRefreshDebounce?.Dispose();
            debounce = new CancellationTokenSource();
            _rosterWatchRefreshDebounce = debounce;
        }

        _ = DebouncedRefreshRosterDialogAsync(rosterPath, debounce.Token);
    }

    private async Task DebouncedRefreshRosterDialogAsync(string rosterPath, CancellationToken ct)
    {
        try
        {
            await Task.Delay(150, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        RefreshRosterDialogFromWatcher(rosterPath);
    }

    private void RefreshRosterDialogFromWatcher(string rosterPath)
    {
        CharacterOverviewState state = State;
        string? activeRosterPath = ResolveActiveRosterWatchFolderPath(state);
        if (!string.Equals(activeRosterPath, rosterPath, StringComparison.Ordinal))
        {
            return;
        }

        string fingerprint = ComputeRosterWatchFolderFingerprint(rosterPath);
        lock (_rosterWatchRuntimeSync)
        {
            if (!string.Equals(_rosterWatchedFolderPath, rosterPath, StringComparison.Ordinal)
                || string.Equals(_rosterWatchFolderFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return;
            }

            _rosterWatchFolderFingerprint = fingerprint;
        }

        string? rulesetId = state.WorkspaceId is not null
            ? ResolveWorkspaceRulesetId(state.WorkspaceId.Value)
            : state.OpenWorkspaces
                .Select(workspace => RulesetDefaults.NormalizeOptional(workspace.RulesetId))
                .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));

        DesktopDialogState rebuiltDialog = _dialogFactory.CreateCommandDialog(
            "character_roster",
            state.Profile,
            state.Preferences,
            state.ActiveSectionJson,
            state.WorkspaceId,
            rulesetId,
            openWorkspaces: state.OpenWorkspaces);

        Publish(state with
        {
            ActiveDialog = rebuiltDialog,
            Error = null
        });
    }

    private static string? ResolveActiveRosterWatchFolderPath(CharacterOverviewState state)
    {
        DesktopDialogState? dialog = state.ActiveDialog;
        if (!string.Equals(dialog?.Id, "dialog.character_roster", StringComparison.Ordinal))
        {
            return null;
        }

        string? dialogPath = DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderPath");
        string resolvedPath = string.IsNullOrWhiteSpace(dialogPath)
            ? state.Preferences.CharacterRosterPath
            : dialogPath.Trim();
        return string.IsNullOrWhiteSpace(resolvedPath)
            ? null
            : resolvedPath;
    }

    private static bool IsRosterWatchRelevantPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string extension = Path.GetExtension(path);
        return string.Equals(extension, ".chum5", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".chum6", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeRosterWatchFolderFingerprint(string rosterPath)
    {
        if (!Directory.Exists(rosterPath))
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (string path in Directory.EnumerateFiles(rosterPath, "*", SearchOption.AllDirectories)
                     .Where(IsRosterWatchRelevantPath)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            FileInfo info = new(path);
            builder.Append(Path.GetRelativePath(rosterPath, path));
            builder.Append('|');
            builder.Append(info.Exists ? info.Length : 0);
            builder.Append('|');
            builder.Append(info.Exists ? info.LastWriteTimeUtc.Ticks : 0);
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
