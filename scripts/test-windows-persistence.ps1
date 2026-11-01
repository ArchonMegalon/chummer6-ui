[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CacheRoot,
    [string]$FixturePath,
    [int]$TimeoutSeconds = 110,
    # Negative coverage: re-inject a hostile inherited variable and require the probe head to
    # refuse to run (isolation proof). Runs after the positive save/reopen pass.
    [switch]$ConflictingEnvironmentProbe
)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Native Windows is required.' }
$repo = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repo 'Chummer.Avalonia\bin\Debug\net10.0\win-x64\Chummer.Avalonia.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the primary Avalonia win-x64 head first.' }
if (-not $FixturePath) { $FixturePath = Join-Path $repo 'Chummer.Tests\TestFiles\Apex Predator.chum5' }
$FixturePath = (Resolve-Path -LiteralPath $FixturePath).Path
$run = Join-Path ([IO.Path]::GetFullPath($CacheRoot)) ('persistence-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$names = @(
    'CHUMMER_DESKTOP_STATE_ROOT', 'CHUMMER_PERSISTENCE_PHASE', 'CHUMMER_PERSISTENCE_FIXTURE',
    'CHUMMER_PERSISTENCE_RECEIPT', 'CHUMMER_PERSISTENCE_SAVED_RECEIPT',
    # Hostile store-steering variables: when present in the invoking shell they leak into both
    # probe children (Start-Process inherits the harness environment) and would redirect the
    # workspace store or flip the head off the local in-process client. They are saved, removed
    # from the harness environment for the duration, and restored in finally.
    'CHUMMER_STATE_PATH', 'CHUMMER_CLIENT_MODE', 'CHUMMER_DESKTOP_CLIENT_MODE',
    'CHUMMER_API_BASE_URL', 'CHUMMER_API_KEY'
)
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:CHUMMER_DESKTOP_STATE_ROOT = Join-Path $run 'state'
    $env:CHUMMER_PERSISTENCE_FIXTURE = $FixturePath
    $save = $null
    $diskHash = $null
    foreach ($phase in @('save', 'reopen')) {
        $env:CHUMMER_PERSISTENCE_PHASE = $phase
        $env:CHUMMER_PERSISTENCE_RECEIPT = Join-Path $run ($phase + '.json')
        $env:CHUMMER_PERSISTENCE_SAVED_RECEIPT = Join-Path $run 'save.json'
        if ($phase -eq 'reopen') { Remove-Item Env:CHUMMER_PERSISTENCE_FIXTURE }
        $process = Start-Process -FilePath $exe -WorkingDirectory $repo -ArgumentList '--windows-persistence-probe' -RedirectStandardOutput (Join-Path $run ($phase + '.stdout.log')) -RedirectStandardError (Join-Path $run ($phase + '.stderr.log')) -PassThru
        $null = $process.Handle
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill()
            $process.WaitForExit()
            throw "Native $phase timed out; evidence retained at $run"
        }
        # Refresh exit information after the timed wait (Windows PowerShell process handle caching).
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Native $phase failed with exit $($process.ExitCode); see $run" }
        $receipt = Get-Content -LiteralPath $env:CHUMMER_PERSISTENCE_RECEIPT -Raw | ConvertFrom-Json
        if (-not $receipt.succeeded -or -not $receipt.saved -or $receipt.dirty -or $receipt.phase -ne $phase) { throw "Invalid $phase receipt." }
        if ($receipt.workspaceId -notmatch '^[a-fA-F0-9]{32}$') { throw 'Unexpected workspace ID format.' }
        $workspaceFile = Join-Path $env:CHUMMER_DESKTOP_STATE_ROOT ('Chummer6\state\workspaces\' + $receipt.workspaceId + '.json')
        $hash = (Get-FileHash -LiteralPath $workspaceFile -Algorithm SHA256).Hash
        if ($phase -eq 'save') { $save = $receipt; $diskHash = $hash }
        elseif ($receipt.processId -eq $save.processId -or $receipt.workspaceId -ne $save.workspaceId -or $receipt.profileSha256 -ne $save.profileSha256 -or $hash -ne $diskHash) { throw 'Reopen did not preserve workspace identity, profile, or saved file.' }
    }
    [pscustomobject]@{
        succeeded = $true; nativeWindows = $true; evidence = $run
        saveProcessId = $save.processId; reopenProcessId = $receipt.processId
        workspaceId = $receipt.workspaceId; profileSha256 = $receipt.profileSha256
        workspaceFileSha256 = $diskHash
    } | ConvertTo-Json

    if ($ConflictingEnvironmentProbe) {
        # Conflicting-environment negative coverage: with a hostile inherited variable present the
        # head must refuse to produce a save receipt (probe-scoped environment isolation proof).
        $negRun = Join-Path $run 'conflicting-environment'
        New-Item -ItemType Directory -Path $negRun | Out-Null
        $env:CHUMMER_PERSISTENCE_PHASE = 'save'
        $env:CHUMMER_PERSISTENCE_RECEIPT = Join-Path $negRun 'save.json'
        $env:CHUMMER_PERSISTENCE_FIXTURE = $FixturePath
        $env:CHUMMER_STATE_PATH = Join-Path $negRun 'hostile-store'   # hostile: redirects the store
        $negative = Start-Process -FilePath $exe -WorkingDirectory $repo -ArgumentList '--windows-persistence-probe' -RedirectStandardOutput (Join-Path $negRun 'save.stdout.log') -RedirectStandardError (Join-Path $negRun 'save.stderr.log') -PassThru
        $null = $negative.Handle
        if (-not $negative.WaitForExit($TimeoutSeconds * 1000)) {
            $negative.Kill(); $negative.WaitForExit()
            throw "Conflicting-environment negative probe timed out; evidence at $negRun"
        }
        $negative.WaitForExit()
        if ($negative.ExitCode -eq 0) { throw 'Conflicting-environment negative probe unexpectedly succeeded; isolation is broken.' }
        if (Test-Path -LiteralPath $env:CHUMMER_PERSISTENCE_RECEIPT) { throw 'Conflicting-environment negative probe produced a save receipt; isolation is broken.' }
        $stderrText = Get-Content -LiteralPath (Join-Path $negRun 'save.stderr.log') -Raw
        if ($stderrText -notmatch 'CHUMMER_STATE_PATH') { throw "Negative probe failed for the wrong reason; expected a CHUMMER_STATE_PATH isolation refusal. Stderr: $stderrText" }
        Remove-Item Env:CHUMMER_STATE_PATH
        [pscustomobject]@{ succeeded = $true; negativeProbe = $true; evidence = $negRun } | ConvertTo-Json
    }
}
finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
