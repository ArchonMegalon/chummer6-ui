# Shared only by the WIN-004 journey and its focused PowerShell tests.
# Dot-sourcing this file performs no launch, environment mutation or filesystem write.

function Set-JourneyAppEnvironment {
    param([System.Diagnostics.ProcessStartInfo]$StartInfo, [string]$StateRoot)
    # Explicitly select the local store for every app launch. A child updater
    # inherits the resolved store from its parent. Never read the operator's store.
    foreach ($name in @(
        'CHUMMER_STATE_PATH', 'CHUMMER_CLIENT_MODE', 'CHUMMER_DESKTOP_CLIENT_MODE',
        'CHUMMER_API_BASE_URL', 'CHUMMER_API_KEY',
        'CHUMMER_PERSISTENCE_PHASE', 'CHUMMER_PERSISTENCE_FIXTURE',
        'CHUMMER_PERSISTENCE_RECEIPT', 'CHUMMER_PERSISTENCE_SAVED_RECEIPT',
        'CHUMMER_DESKTOP_UPDATE_MANIFEST', 'CHUMMER_DESKTOP_UPDATE_AUTO_APPLY'
    )) { $StartInfo.EnvironmentVariables.Remove($name) }
    $StartInfo.EnvironmentVariables['CHUMMER_DESKTOP_STATE_ROOT'] = [IO.Path]::GetFullPath($StateRoot)
    $StartInfo.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_ENABLED'] = '0'
    $StartInfo.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_MODE'] = 'off'
}

function Test-UpdatedProcessBirth {
    param([int]$ProcessId, [datetime]$StartTimeUtc, [int]$PreviousProcessId,
          [datetime]$PreviousStartUtc, [datetime]$ApplyStartedUtc)
    # The registry poll observes completion later; its timestamp is not the
    # completion event. Use boundaries recorded BEFORE auto-apply can start.
    return ($ProcessId -ne $PreviousProcessId -and
            $StartTimeUtc -gt $PreviousStartUtc -and
            $StartTimeUtc -ge $ApplyStartedUtc)
}

function Get-CharacterDataSnapshot {
    param([string]$Root)
    $map = @{}
    if (-not (Test-Path -LiteralPath $Root)) { return $map }
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force -ErrorAction Stop)) {
        $map[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256 -ErrorAction Stop).Hash
    }
    return $map
}

function Test-CharacterDataPreservation {
    param($Before, $After, [string]$Root)
    $report = @{ status = ''; root = $Root; beforeCount = @($Before.Keys).Count;
                 afterCount = @($After.Keys).Count; lost = @(); modified = @(); added = @() }
    foreach ($key in @($Before.Keys)) {
        if (-not $After.ContainsKey($key)) { $report.lost += $key; continue }
        if ($After[$key] -ne $Before[$key]) { $report.modified += $key }
    }
    foreach ($key in @($After.Keys)) {
        if (-not $Before.ContainsKey($key)) { $report.added += $key }
    }
    if ($report.beforeCount -eq 0) { $report.status = 'inconclusive-empty-before' }
    elseif ($report.lost.Count -gt 0 -or $report.modified.Count -gt 0) { $report.status = 'fail' }
    else { $report.status = 'pass' }
    return $report
}

function Assert-JourneyPersistenceReceipt {
    param($Receipt, [string]$Phase, [int]$ProcessId, [string]$StateRoot, $Saved,
          [string]$WorkspaceFileSha256, [string]$SavedFileSha256)
    if (-not $Receipt.succeeded -or -not $Receipt.saved -or $Receipt.dirty -or
        $Receipt.phase -ne $Phase -or $Receipt.processId -ne $ProcessId -or
        $Receipt.workspaceId -notmatch '^[a-fA-F0-9]{32}$' -or
        $Receipt.profileSha256 -notmatch '^[a-fA-F0-9]{64}$' -or
        [string]::IsNullOrWhiteSpace($Receipt.stateRoot) -or
        [IO.Path]::GetFullPath($Receipt.stateRoot) -ine [IO.Path]::GetFullPath($StateRoot)) {
        throw 'Persistence receipt does not match the launched process and isolated store.'
    }
    if ($Phase -eq 'reopen' -and (
        $null -eq $Saved -or $Receipt.processId -eq $Saved.processId -or
        $Receipt.workspaceId -ne $Saved.workspaceId -or
        $Receipt.profileSha256 -ne $Saved.profileSha256 -or
        $Receipt.name -cne $Saved.name -or
        [string]::IsNullOrWhiteSpace($WorkspaceFileSha256) -or
        $WorkspaceFileSha256 -ne $SavedFileSha256)) {
        throw 'Reopen did not preserve the saved runner identity, profile and file bytes.'
    }
}
