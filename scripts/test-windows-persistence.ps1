[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CacheRoot,
    [string]$FixturePath,
    [int]$TimeoutSeconds = 110
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
$names = @('CHUMMER_DESKTOP_STATE_ROOT', 'CHUMMER_PERSISTENCE_PHASE', 'CHUMMER_PERSISTENCE_FIXTURE', 'CHUMMER_PERSISTENCE_RECEIPT', 'CHUMMER_PERSISTENCE_SAVED_RECEIPT')
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
}
finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
