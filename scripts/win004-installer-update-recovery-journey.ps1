# win004-installer-update-recovery-journey.ps1
#
# WIN-004 native verification journey: produce the existing Windows installer
# artifact locally and verify install -> start -> update -> relaunch -> recovery
# against the real user-scope install (%LOCALAPPDATA%\Programs\Chummer6, HKCU).
#
# Claim discipline: this run exercises LOCAL, UNSIGNED developer artifacts built
# from this working tree. It makes no claim about signed or published channels.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\win004-installer-update-recovery-journey.ps1
#     [-WorkRoot <dir>] [-SkipV1Build] [-SkipV2Build] [-KeepArtifacts]
#
# Evidence: <WorkRoot>\journey-receipt.json + installer progress trace
# (%TEMP%\chummer-desktop-installer-progress.log snapshots per leg).

param(
    [string]$WorkRoot = "",
    [switch]$SkipV1Build,
    [switch]$SkipV2Build,
    [switch]$KeepArtifacts
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not $WorkRoot) { $WorkRoot = Join-Path $RepoRoot 'artifacts\win004-journey' }
$WorkRoot = [IO.Path]::GetFullPath($WorkRoot)
. (Join-Path $PSScriptRoot 'win004-acceptance.ps1')
# Fresh per-run state survives through save, update, recovery and uninstall.
$JourneyStateRoot = Join-Path $WorkRoot ('app-state-' + [guid]::NewGuid().ToString('N'))
$CharacterDataRoot = Join-Path $JourneyStateRoot 'Chummer6\state'
$UpdateStateRoot = Join-Path $JourneyStateRoot 'Chummer6\desktop-update\avalonia\windows\x64'
$PersistenceRoot = Join-Path $JourneyStateRoot 'probe-receipts'
$DistDir = Join-Path $WorkRoot 'dist'
$PubV1 = Join-Path $WorkRoot 'publish\v1'
$PubV2 = Join-Path $WorkRoot 'publish\v2'
$ReceiptPath = Join-Path $WorkRoot 'journey-receipt.json'
$ProgressLog = Join-Path $env:TEMP 'chummer-desktop-installer-progress.log'

$HeadId = 'avalonia'
$Rid = 'win-x64'
$LaunchTarget = 'Chummer.Avalonia.exe'
$AppId = "$HeadId-$Rid"
$InstallerName = "chummer-$AppId-installer.exe"
$PayloadResourceName = 'ChummerInstaller.Payload.zip'
$UninstallKeyName = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Chummer6.avalonia-win-x64'
$TaskDir = 'C:\Users\tibor\AppData\Local\Chummer\friday\tasks\WIN-001'
$DotNetExe = Join-Path $TaskDir 'sdk\dotnet.exe'
# Core content checkout (WIN-001 contract): validated by the app's resolver and
# pinned to the authoritative Core commit; fail fast before any build work.
$CoreContentRoot = 'D:\sources\chummer\chummer6-core'
if (-not (Test-Path (Join-Path $CoreContentRoot 'Chummer\data\settings-all-sources.xml'))) {
    throw "Core content checkout missing at $CoreContentRoot (Chummer/data/settings-all-sources.xml not found)."
}
$NuGetConfigPath = Join-Path $RepoRoot 'NuGet.Config'
$NeedNuGetConfig = -not (Test-Path $NuGetConfigPath)
$InstallerArgsProps = @(
    '-p:PublishSingleFile=true', '-p:GenerateRuntimeConfigurationFiles=true',
    '-p:PublishTrimmed=false', '-p:EnableCompressionInSingleFile=false',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:ChummerInstallerPayloadRequired=false', '-p:ChummerInstallerEmbedPayload=false',
    '-p:ChummerInstallerIncludeSidecarPayload=false'
)

$script:Receipt = [ordered]@{
    task            = 'WIN-004'
    claimLevel      = 'local-unsigned-developer-artifacts'
    startedUtc      = (Get-Date).ToUniversalTime().ToString('o')
    repoRoot        = $RepoRoot
    gitHead         = ''
    legs            = [ordered]@{}
}

function Write-Leg([string]$Name, [hashtable]$Data) {
    $Data.endedUtc = (Get-Date).ToUniversalTime().ToString('o')
    $script:Receipt.legs[$Name] = $Data
    $status = if ($Data.status) { $Data.status } else { 'unknown' }
    Write-Host "[win004] leg $Name -> $status"
    ($Receipt | ConvertTo-Json -Depth 8) | Set-Content -Encoding UTF8 $ReceiptPath
}

function Fail-Leg([string]$Name, [string]$Why, [string]$Detail = '') {
    Write-Host "[win004] FAILED $Name : $Why $Detail" -ForegroundColor Red
    Write-Leg $Name @{ status = 'fail'; why = $Why; detail = $Detail }
    ($Receipt | ConvertTo-Json -Depth 8) | Set-Content -Encoding UTF8 $ReceiptPath
    if ($NeedNuGetConfig -and (Test-Path $NuGetConfigPath)) { Remove-Item $NuGetConfigPath -Force }
    exit 1
}

function Get-FileSha256([string]$Path) {
    (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Invoke-Step {
    # NOTE: do not name the list param '$Args' - that collides with PowerShell's
    # automatic $args variable and the array arrives nested (dotnet got one
    # giant argument and printed the app-path usage banner).
    param([string]$Exe, [string[]]$ArgList, [int]$TimeoutSec = 600, [string]$LegName)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Exe
    # Windows PowerShell 5.1 = .NET Framework: no ProcessStartInfo.ArgumentList.
    $quoted = foreach ($a in $ArgList) { if ($a -match '\s') { '"' + $a + '"' } else { $a } }
    $psi.Arguments = ($quoted -join ' ')
    $psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.WorkingDirectory = $RepoRoot
    # Child must inherit this process's environment (sandbox SDK on PATH).
    $p = [System.Diagnostics.Process]::Start($psi)
    $outTask = $p.StandardOutput.ReadToEndAsync()
    $errTask = $p.StandardError.ReadToEndAsync()
    if (-not (Wait-ForExit $p $TimeoutSec)) {
        try { $p.Kill($true) } catch {}
        throw "$LegName timed out after ${TimeoutSec}s: $Exe $($ArgList -join ' ')"
    }
    $stdout = $outTask.GetAwaiter().GetResult()
    $stderr = $errTask.GetAwaiter().GetResult()
    return [pscustomobject]@{
        ExitCode = $p.ExitCode
        StdOut   = $stdout
        StdErr   = $stderr
    }
}

function Wait-ForExit([System.Diagnostics.Process]$p, [int]$TimeoutSec) {
    return $p.WaitForExit($TimeoutSec * 1000)
}

function Get-InstallInfo {
    $info = @{ ExePath = ''; InstallDir = ''; DisplayVersion = '' }
    if (Test-Path $UninstallKeyName) {
        $props = Get-ItemProperty -Path $UninstallKeyName
        $info.DisplayVersion = [string]$props.DisplayVersion
        $info.InstallDir = [string]$props.InstallLocation
        if ($info.InstallDir) {
            $candidate = Join-Path $info.InstallDir $LaunchTarget
            if (Test-Path $candidate) { $info.ExePath = $candidate }
        }
    }
    return $info
}


function Invoke-InstalledCharacterProbe {
    param([string]$Exe, [string]$Phase, [string]$Label)
    New-Item -ItemType Directory -Force -Path $PersistenceRoot | Out-Null
    $receiptPath = Join-Path $PersistenceRoot ($Label + '.json')
    if (Test-Path -LiteralPath $receiptPath) { throw 'Persistence receipt already exists; do not reuse a previous result.' }
    $probeInfo = New-Object System.Diagnostics.ProcessStartInfo
    $probeInfo.FileName = $Exe
    $probeInfo.Arguments = '--windows-persistence-probe'
    $probeInfo.WorkingDirectory = Split-Path -Parent $Exe
    $probeInfo.UseShellExecute = $false
    Set-JourneyAppEnvironment -StartInfo $probeInfo -StateRoot $JourneyStateRoot
    $probeInfo.EnvironmentVariables.Remove('CHUMMER_CORE_CONTENT_ROOT')
    $probeInfo.EnvironmentVariables['CHUMMER_PERSISTENCE_PHASE'] = $Phase
    $probeInfo.EnvironmentVariables['CHUMMER_PERSISTENCE_RECEIPT'] = $receiptPath
    $probeInfo.EnvironmentVariables['CHUMMER_PERSISTENCE_SAVED_RECEIPT'] = Join-Path $PersistenceRoot 'save.json'
    if ($Phase -eq 'save') {
        # Repository fixture only: no operator character or personal store is used.
        $probeInfo.EnvironmentVariables['CHUMMER_PERSISTENCE_FIXTURE'] = Join-Path $RepoRoot 'Chummer.Tests\TestFiles\Apex Predator.chum5'
    }
    $probeInfo.RedirectStandardOutput = $true
    $probeInfo.RedirectStandardError = $true
    $probe = [System.Diagnostics.Process]::Start($probeInfo)
    $outTask = $probe.StandardOutput.ReadToEndAsync()
    $errTask = $probe.StandardError.ReadToEndAsync()
    try {
        if (-not $probe.WaitForExit(110000)) { throw "Installed character $Label probe timed out." }
        $probe.WaitForExit()
        $outTask.GetAwaiter().GetResult() | Set-Content -Encoding UTF8 (Join-Path $PersistenceRoot ($Label + '.stdout.log'))
        $errTask.GetAwaiter().GetResult() | Set-Content -Encoding UTF8 (Join-Path $PersistenceRoot ($Label + '.stderr.log'))
        if ($probe.ExitCode -ne 0) { throw "Installed character $Label probe failed; see its private logs." }
        $result = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        if ($result.workspaceId -notmatch '^[a-fA-F0-9]{32}$') { throw 'Unexpected workspace ID in persistence receipt.' }
        $workspaceFile = Join-Path $CharacterDataRoot ('workspaces\' + $result.workspaceId + '.json')
        $fileSha = Get-FileSha256 $workspaceFile
        Assert-JourneyPersistenceReceipt -Receipt $result -Phase $Phase -ProcessId $probe.Id `
            -StateRoot $JourneyStateRoot -Saved $script:SavedRunner `
            -WorkspaceFileSha256 $fileSha -SavedFileSha256 $script:SavedRunnerFileSha
        if ($Phase -eq 'save') {
            $script:SavedRunner = $result
            $script:SavedRunnerFileSha = $fileSha
        }
        Write-Leg ('character-' + $Label) @{
            status = 'pass'; executable = $Exe; processId = $result.processId
            workspaceId = $result.workspaceId; profileSha256 = $result.profileSha256
            workspaceFileSha256 = $fileSha; isolatedStateRoot = $JourneyStateRoot
        }
    } finally {
        if (-not $probe.HasExited) { $probe.Kill(); $probe.WaitForExit(10000) | Out-Null }
        $probe.Dispose()
    }
}

function Stop-HeadProcesses {
    Get-Process -Name 'Chummer.Avalonia' -ErrorAction SilentlyContinue | ForEach-Object {
        try { $_.Kill(); $_.WaitForExit(10000) } catch {}
    }
}

function Publish-App([string]$Stamp, [string]$OutDir) {
    if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }
    $r = Invoke-Step $DotNetExe @(
        'publish', (Join-Path $RepoRoot 'Chummer.Avalonia\Chummer.Avalonia.csproj'),
        '-c', 'Release', '-f', 'net10.0', '-r', $Rid,
        '--self-contained', 'true',
        '-p:PublishSingleFile=true', '-p:PublishTrimmed=false', '-p:IncludeNativeLibrariesForSelfExtract=true',
        "-p:ChummerDesktopReleaseVersion=$Stamp",
        "-p:ChummerCoreContentSource=$CoreContentRoot",
        '-p:ChummerDesktopReleaseChannel=preview',
        '-o', $OutDir
    ) 900 'app-publish'
    if ($r.ExitCode -ne 0) {
        $tailOut = ($r.StdOut -split "`n" | Select-Object -Last 12) -join "`n"
        throw "app publish failed ($Stamp) exit $($r.ExitCode): $tailOut $($r.StdErr)"
    }
    if (-not (Test-Path (Join-Path $OutDir $LaunchTarget))) { throw "launch target missing after publish: $OutDir" }
}

function Build-InstallerBundled([string]$Stamp, [string]$PublishDir, [string]$OutDir, [string]$PayloadZip) {
    if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }
    $props = @(
        (Join-Path $RepoRoot 'Chummer.Desktop.Installer\Chummer.Desktop.Installer.csproj'),
        '-c', 'Release', '-r', $Rid, '--self-contained', 'true'
    ) + $InstallerArgsProps + @(
        "-p:ChummerInstallerAssemblyName=Chummer6Installer-$AppId",
        "-p:InstallerPayloadZip=$PayloadZip",
        "-p:ChummerInstallerPayloadResourceName=$PayloadResourceName",
        '-p:ChummerInstallerPayloadUrl=', '-p:ChummerInstallerPayloadSha256=', '-p:ChummerInstallerPayloadSizeBytes=',
        "-p:ChummerInstallerAppId=$AppId",
        "-p:ChummerInstallerHeadId=$HeadId",
        '-p:ChummerInstallerDisplayName=Chummer6 (Avalonia preview)',
        "-p:ChummerInstallerInstallDirName=$AppId",
        "-p:ChummerInstallerLaunchExecutable=$LaunchTarget",
        "-p:ChummerInstallerVersion=$Stamp",
        '-p:ChummerInstallerShortcutName=Chummer6',
        '-p:ChummerInstallerHeadsJsonBase64=',
        "-p:ChummerInstallerOutputName=Chummer6Installer-$AppId",
        '-o', $OutDir
    )
    $r = Invoke-Step $DotNetExe (@('publish') + $props) 900 'installer-publish'
    if ($r.ExitCode -ne 0) {
        $tailOut = ($r.StdOut -split "`n" | Select-Object -Last 12) -join "`n"
        throw "installer publish failed ($Stamp) exit $($r.ExitCode): $tailOut $($r.StdErr)"
    }
    $exe = Get-ChildItem -Path $OutDir -Filter '*.exe' | Select-Object -First 1
    if (-not $exe) { throw "installer publish produced no exe in $OutDir" }

    # Append payload (bundled mode): payload + int64 length + magic.
    $appendPy = Join-Path $env:TEMP "win004-append-$([guid]::NewGuid().ToString('N')).py"
    @"
import struct, sys
from pathlib import Path
installer = Path(sys.argv[1]); payload = Path(sys.argv[2])
magic = b"CHUMMER6PAYLOAD1"
pb = payload.read_bytes()
with installer.open("ab") as h:
    h.write(pb); h.write(struct.pack("<q", len(pb))); h.write(magic)
print(installer)
"@ | Set-Content -Encoding ASCII $appendPy
    $r2 = Invoke-Step 'python' @($appendPy, $exe.FullName, $PayloadZip) 120 'payload-append'
    Remove-Item -Force $appendPy -ErrorAction SilentlyContinue
    if ($r2.ExitCode -ne 0) { throw "payload append failed: $($r2.StdErr) $($r2.StdOut)" }
    return $exe.FullName
}

function Assert-PayloadGate([string]$InstallerExe, [string]$PayloadZip, [string]$LegName) {
    $gate = Join-Path $RepoRoot 'scripts\verify-windows-installer-payloads.py'
    $r = Invoke-Step 'python' @(
        $gate,
        '--installer', $InstallerExe,
        '--expected-launch', $LaunchTarget,
        '--expected-entry', $LaunchTarget,
        '--payload', $PayloadZip,
        '--files-dir', (Split-Path -Parent $PayloadZip)
    ) 300 $LegName
    if ($r.ExitCode -ne 0) { throw "payload gate failed for ${InstallerExe}: $($r.StdOut) $($r.StdErr)" }
}

# ---------------------------------------------------------------------------
# Leg 0: identity + preflight
# ---------------------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
# Build environment: sandbox SDK + private feed + caches (WIN-001 task-folder
# contract). C:\Program Files\dotnet hosts only the apphost, no SDK. Child
# processes launched by Invoke-Step inherit this process environment.
$env:PATH = "$TaskDir\sdk;$env:PATH"
$env:DOTNET_ROOT = "$TaskDir\sdk"
$env:NUGET_PACKAGES = "$TaskDir\nuget-packages"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
if ($NeedNuGetConfig) { Copy-Item "$TaskDir\NuGet.Config" $NuGetConfigPath -Force }
$gitHead = (& git -C $RepoRoot rev-parse HEAD) 2>$null
$script:Receipt.gitHead = $gitHead
$stampBase = (Get-Date).ToUniversalTime()
$V1Stamp = 'run-' + $stampBase.ToString('yyyyMMdd-HHmmss')
Start-Sleep -Seconds 2
$V2Stamp = 'run-' + (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
$leg0 = @{
    status = 'pass'; gitHead = $gitHead; v1Stamp = $V1Stamp; v2Stamp = $V2Stamp
    dotnet = (Invoke-Step $DotNetExe @('--version') 60 'preflight').StdOut.Trim()
}
Write-Leg 'preflight' $leg0

# Canonical artifact file names: the update manifest parser requires
# payloadDownloadUrl's file name to equal payloadFileName (and the channel
# sidecar pattern keys on canonical installer names), so v1/v2 live in
# per-version subdirectories instead of name-prefixed files.
$V1Dir = Join-Path $DistDir 'v1'
$V2Dir = Join-Path $DistDir 'v2'
New-Item -ItemType Directory -Force -Path $V1Dir, $V2Dir | Out-Null
$V1Payload = Join-Path $V1Dir "chummer-$AppId-payload.zip"
$V2Payload = Join-Path $V2Dir "chummer-$AppId-payload.zip"
$V1Installer = Join-Path $V1Dir $InstallerName
$V2Installer = Join-Path $V2Dir $InstallerName

# ---------------------------------------------------------------------------
# Leg 1: build v1 artifact locally
# ---------------------------------------------------------------------------
if ($SkipV1Build -and (Test-Path $V1Installer)) {
    Write-Leg 'build-v1' @{ status = 'skip (cached)'; installer = $V1Installer }
} else {
    try {
        Publish-App $V1Stamp $PubV1
        $zipper = Join-Path $RepoRoot 'scripts\build-reproducible-zip.py'
        $r = Invoke-Step 'python' @($zipper, $PubV1, $V1Payload) 600 'v1-zip'
        if ($r.ExitCode -ne 0) { throw "payload zip failed: $($r.StdErr)" }
        $exe = Build-InstallerBundled $V1Stamp $PubV1 (Join-Path $DistDir 'installer-v1') $V1Payload
        Move-Item -Force $exe $V1Installer
        Assert-PayloadGate $V1Installer $V1Payload 'build-v1'
        Write-Leg 'build-v1' @{
            status = 'pass'; stamp = $V1Stamp
            installer = $V1Installer; installerSha256 = (Get-FileSha256 $V1Installer)
            payload = $V1Payload; payloadSha256 = (Get-FileSha256 $V1Payload)
            payloadSizeBytes = (Get-Item $V1Payload).Length
        }
    } catch {
        Fail-Leg 'build-v1' $_.Exception.Message
    }
}

# ---------------------------------------------------------------------------
# Leg 2: build v2 artifact locally (update channel target)
# ---------------------------------------------------------------------------
if ($SkipV2Build -and (Test-Path $V2Installer)) {
    Write-Leg 'build-v2' @{ status = 'skip (cached)'; installer = $V2Installer }
} else {
    try {
        Publish-App $V2Stamp $PubV2
        $zipper = Join-Path $RepoRoot 'scripts\build-reproducible-zip.py'
        $r = Invoke-Step 'python' @($zipper, $PubV2, $V2Payload) 600 'v2-zip'
        if ($r.ExitCode -ne 0) { throw "payload zip failed: $($r.StdErr)" }
        $exe = Build-InstallerBundled $V2Stamp $PubV2 (Join-Path $DistDir 'installer-v2') $V2Payload
        Move-Item -Force $exe $V2Installer
        Assert-PayloadGate $V2Installer $V2Payload 'build-v2'
        Write-Leg 'build-v2' @{
            status = 'pass'; stamp = $V2Stamp
            installer = $V2Installer; installerSha256 = (Get-FileSha256 $V2Installer)
            payload = $V2Payload; payloadSha256 = (Get-FileSha256 $V2Payload)
            payloadSizeBytes = (Get-Item $V2Payload).Length
        }
    } catch {
        Fail-Leg 'build-v2' $_.Exception.Message
    }
}

# ---------------------------------------------------------------------------
# Leg 3: channel manifests (v2 = good update; bad = sha-mismatched artifact)
# ---------------------------------------------------------------------------
$v2InstallerSha = Get-FileSha256 $V2Installer
$v2InstallerSize = (Get-Item $V2Installer).Length
$v2PayloadSha = Get-FileSha256 $V2Payload
$v2PayloadSize = (Get-Item $V2Payload).Length
$v2Url = ([System.Uri]$V2Installer).AbsoluteUri
$v2PayloadUrl = ([System.Uri]$V2Payload).AbsoluteUri

$ManifestV2 = Join-Path $DistDir 'channel-v2.json'
$artifactV2 = [ordered]@{
    artifactId        = "$AppId-installer"
    head              = $HeadId
    platform          = 'windows'
    arch              = 'x64'
    kind              = 'installer'
    fileName          = $InstallerName
    downloadUrl       = $v2Url
    sha256            = $v2InstallerSha
    sizeBytes         = $v2InstallerSize
    installerMode     = 'bootstrap'
    payloadFileName   = "chummer-$AppId-payload.zip"
    payloadDownloadUrl = $v2PayloadUrl
    payloadSha256     = $v2PayloadSha
    payloadSizeBytes  = $v2PayloadSize
}
$manifestV2Obj = [ordered]@{
    channelId   = 'preview'
    version     = $V2Stamp
    status      = 'published'
    publishedAt = (Get-Date).ToUniversalTime().ToString('o')
    artifacts   = @($artifactV2)
}
$manifestV2Obj | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 $ManifestV2

# Bad channel: claims a sha256 the artifact cannot produce -> download/verify fails.
$BadManifest = Join-Path $DistDir 'channel-bad.json'
$artifactBad = [ordered]@{
    artifactId        = "$AppId-installer"
    head              = $HeadId
    platform          = 'windows'
    arch              = 'x64'
    kind              = 'installer'
    fileName          = $InstallerName
    downloadUrl       = $v2Url
    sha256            = ('0' * 64)
    sizeBytes         = $v2InstallerSize
    installerMode     = 'bootstrap'
    payloadFileName   = "chummer-$AppId-payload.zip"
    payloadDownloadUrl = $v2PayloadUrl
    payloadSha256     = $v2PayloadSha
    payloadSizeBytes  = $v2PayloadSize
}
$manifestBadObj = [ordered]@{
    channelId   = 'preview'
    version     = 'run-' + (Get-Date).ToUniversalTime().AddMinutes(5).ToString('yyyyMMdd-HHmmss')
    status      = 'published'
    publishedAt = (Get-Date).ToUniversalTime().ToString('o')
    artifacts   = @($artifactBad)
}
$manifestBadObj | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 $BadManifest
Write-Leg 'manifests' @{ status = 'pass'; v2 = $ManifestV2; bad = $BadManifest }

# ---------------------------------------------------------------------------
# Leg 4: native install (unattended, user scope)
# ---------------------------------------------------------------------------
Stop-HeadProcesses
try {
    if (Test-Path $UninstallKeyName) {
        # Clean any pre-existing install so this leg starts from nothing.
        $prev = Get-InstallInfo
        $r = Invoke-Step $V1Installer @('--uninstall', '--unattended') 300 'pre-clean'
        if ($r.ExitCode -ne 0) { throw "pre-clean uninstall failed: $($r.StdErr)" }
        # Directory removal is scheduled (delayed cmd script) - wait it out.
        for ($i = 0; $i -lt 45 -and $prev.InstallDir -and (Test-Path $prev.InstallDir); $i++) { Start-Sleep -Seconds 2 }
    }
    if (Test-Path $ProgressLog) { Remove-Item -Force $ProgressLog }
    $r = Invoke-Step $V1Installer @('--unattended') 600 'install'
    if ($r.ExitCode -ne 0) { throw "unattended install exit $($r.ExitCode): $($r.StdErr)" }
    $info = Get-InstallInfo
    if (-not $info.ExePath) { throw "install did not produce a usable install (registry: $UninstallKeyName)" }
    # With -SkipV1Build the cached installer bakes an older stamp; the contract is
    # that the registry carries the installer's baked run-stamp version.
    if ($info.DisplayVersion -notmatch '^run-\d{8}-\d{6}$') { throw "registry DisplayVersion '$($info.DisplayVersion)' is not a run-stamp" }
    $progSnap = ''
    if (Test-Path $ProgressLog) { $progSnap = (Get-Content $ProgressLog | Select-Object -Last 5) -join ' | ' }
    Write-Leg 'install' @{
        status = 'pass'; installer = $V1Installer
        installDir = $info.InstallDir; exe = $info.ExePath
        registryVersion = $info.DisplayVersion
        progressTraceTail = $progSnap
    }
} catch {
    Fail-Leg 'install' $_.Exception.Message
}
$InstalledExe = (Get-InstallInfo).ExePath

# ---------------------------------------------------------------------------
# Leg 5: native start (update channel off)
# ---------------------------------------------------------------------------
try {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $InstalledExe
    $psi.UseShellExecute = $false
    Set-JourneyAppEnvironment -StartInfo $psi -StateRoot $JourneyStateRoot
    # The installed executable must use its delivered, verified rules bundle.
    $psi.EnvironmentVariables.Remove('CHUMMER_CORE_CONTENT_ROOT')
    $psi.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_ENABLED'] = '0'
    $psi.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_MODE'] = 'off'
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardOutput = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    Start-Sleep -Seconds 15
    if ($p.HasExited) {
        $errOut = ''; $stdOut = ''
        try { $errOut = $p.StandardError.ReadToEnd(); $stdOut = $p.StandardOutput.ReadToEnd() } catch {}
        try {
            # Privacy-safe diagnostics (re-audit item 1, 2026-11-02): never persist
            # the child environment. The collector records allowlisted values and
            # presence-only machine paths; credential names/values cannot appear.
            . (Join-Path $PSScriptRoot 'win004-start-failure-diagnostics.ps1')
            Write-StartFailureEnvironmentReport -Environment $psi.EnvironmentVariables `
                -Path (Join-Path $WorkRoot 'start-failure-environment.json')
        } catch {}
        throw "app exited during start leg (code $($p.ExitCode)) stderr: $errOut stdout: $stdOut"
    }
    $alive = $true
    try { $p.Kill(); $p.WaitForExit(10000) | Out-Null } catch {}
    Write-Leg 'start' @{ status = 'pass'; exe = $InstalledExe; stayedAliveSeconds = 15 }

    # Character-data preservation: snapshot the persistent store before the update legs.
    Invoke-InstalledCharacterProbe -Exe $InstalledExe -Phase save -Label save
    $script:CharacterDataBefore = Get-CharacterDataSnapshot -Root $CharacterDataRoot
    Write-Leg 'character-data-snapshot' @{ status = 'pass'; root = $CharacterDataRoot; files = @($script:CharacterDataBefore.Keys).Count }
} catch {
    Stop-HeadProcesses
    Fail-Leg 'start' $_.Exception.Message
}

# ---------------------------------------------------------------------------
# Leg 6: native update apply + relaunch (v1 -> v2 through the real runtime)
# ---------------------------------------------------------------------------
try {
    # Reset update state so the runtime evaluates the channel fresh.
    $stateRoot = $UpdateStateRoot
    if (Test-Path $stateRoot) { Remove-Item -Recurse -Force $stateRoot }
    Stop-HeadProcesses

    # Existing shortcuts are RETAINED through the update (WINDOWS_SCOPE re-audit
    # item 5.2): the acceptance case must exercise shortcut overwrite, and the
    # overwrite hang is resolved in the installer (delete-before-create in
    # CreateShortcutCore), not by pre-cleaning here. Cleanup belongs after the
    # tested behavior, not before it. Snapshot the pre-update shortcuts so the
    # update-relaunch leg can assert retention with real evidence.
    $ShortcutPaths = @(
        (Join-Path (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs') 'Chummer6.lnk'),
        (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Chummer6.lnk')
    )
    function Get-LnkSnapshot {
        $snap = @{}
        $shell = New-Object -ComObject WScript.Shell
        foreach ($lnk in $ShortcutPaths) {
            if (Test-Path $lnk) {
                $target = ''
                try { $target = [string]$shell.CreateShortcut($lnk).TargetPath } catch { $target = '' }
                $snap[$lnk] = (Get-FileSha256 $lnk) + '|' + (Get-Item $lnk).LastWriteTimeUtc.Ticks + '|' + $target
            }
        }
        return $snap
    }
    # Retention comparator (PR #513 followup P2): EVERY pre-update shortcut
    # must survive AND resolve to the expected installed executable - a nonzero
    # post-update count alone can hide the loss of one of two.
    function Test-LnkRetention {
        param($Before, $After, $ExpectedExe)
        foreach ($lnk in $Before.Keys) {
            if (-not $After.ContainsKey($lnk)) { return "shortcut lost by update: $lnk" }
        }
        foreach ($lnk in $After.Keys) {
            $parts = @($After[$lnk] -split '\|')
            $target = if ($parts.Count -ge 3) { $parts[2] } else { '' }
            if (-not $target) { return "shortcut does not resolve to a target: $lnk" }
            if ($target -ine $ExpectedExe) { return "shortcut does not resolve to the expected executable: $lnk -> $target" }
        }
        return $null
    }
    $ShortcutsBefore = Get-LnkSnapshot
    if ($ShortcutsBefore.Count -eq 0) { throw 'pre-update shortcut snapshot found no Chummer6 shortcuts (the acceptance case requires existing shortcuts to retain through the update)' }

    $PreUpdateVersion = [string]((Get-InstallInfo).DisplayVersion)
    $ApplyStartedUtc = (Get-Date).ToUniversalTime()
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $InstalledExe
    $psi.UseShellExecute = $false
    Set-JourneyAppEnvironment -StartInfo $psi -StateRoot $JourneyStateRoot
    $psi.EnvironmentVariables.Remove('CHUMMER_CORE_CONTENT_ROOT')
    $psi.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_ENABLED'] = '1'
    $psi.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_MODE'] = 'full'
    $psi.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_AUTO_APPLY'] = 'true'
    $psi.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_MANIFEST'] = $ManifestV2
    $p = [System.Diagnostics.Process]::Start($psi)

    $deadline = (Get-Date).AddMinutes(8)
    # Poll for the registry version CHANGING from the pre-update value — the
    # installed stamp after an update is whatever the v2 payload baked at its own
    # build time, which cached-build runs cannot know exactly (run-stamp vs binary
    # mtime skew). A change from the v1 value is the update landing, however named.
    $PreviousProcessId = $p.Id
    $PreviousStartUtc = $p.StartTime.ToUniversalTime()
    $updated = $false
    $updateObservedAtUtc = Get-Date
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 10
        $info = Get-InstallInfo
        if ($info.DisplayVersion -and $info.DisplayVersion -ne $PreUpdateVersion) { $updated = $true; $updateObservedAtUtc = (Get-Date); break }
        if ($p.HasExited -and -not $updated) {
            # Expected mid-leg: the app exits so the apply helper can run. Keep polling
            # for the installer to finish; only fail if the registry never moves.
        }
    }
    if (-not $updated) { throw "update apply did not land within 8 minutes (registry still '$((Get-InstallInfo).DisplayVersion)')" }

    # Relaunch: the apply chain brings the head back up under v2.
    # History (WINDOWS_HANDOFF item 7, source-corrected on audited main
    # 0d224e7a7): this path used to crash ~250ms after completing with
    # ObjectDisposedException in InstallSplashForm (Show-after-dispose), so the
    # automatic relaunch never happened and the harness relaunched natively
    # itself (relaunchMode='harness-fallback').
    # WINDOWS_SCOPE re-audit item 5.1: diagnostic recovery and acceptance are
    # now separated. An installer-auto relaunch is REQUIRED for this leg to
    # pass; a harness relaunch survives only as diagnostic recovery (recorded
    # in a separate diagnostic leg) and update-relaunch FAILS when it is used.
    $infoUpd = Get-InstallInfo
    if (-not $infoUpd.ExePath) { throw 'post-update install info unusable (registry missing ExePath)' }
    $relaunched = $false
    $relaunchMode = 'installer-auto'
    $defectDetected = $false
    $relaunchDeadline = (Get-Date).AddMinutes(3)
    $newProc = $null
    $newProcPath = ''
    $newProcVersion = ''
    $newProcStart = $null
    # Bound detection (WINDOWS_SCOPE re-audit item 5.3): the relaunched head
    # must be the EXPECTED executable at the EXPECTED install location - not
    # merely any process named 'Chummer.Avalonia'.
    while ((Get-Date) -lt $relaunchDeadline) {
        $candidate = Get-Process -Name 'Chummer.Avalonia' -ErrorAction SilentlyContinue | Where-Object {
            # v1 and v2 share one install path: the pre-update head ($p) would
            # match a pure path filter, so exclude it by Id and require birth
            # AFTER it started (immune to the poll skew between the registry
            # flip and $updateObservedAtUtc). A version-stamp cascade (manifest newer
            # than the relaunched binary) produces a head that immediately
            # chains ANOTHER update and exits - the post-relaunch liveness
            # window below rejects it; the acceptance requirement is a STABLE
            # relaunched head, and full builds (no -Skip) prevent the cascade.
            try {
                return ((Test-UpdatedProcessBirth -ProcessId $_.Id -StartTimeUtc $_.StartTime.ToUniversalTime() `
                            -PreviousProcessId $PreviousProcessId -PreviousStartUtc $PreviousStartUtc -ApplyStartedUtc $ApplyStartedUtc) -and
                        $null -ne $_.MainModule -and
                        $null -ne $_.MainModule.FileName -and
                        ($_.MainModule.FileName -ieq $infoUpd.ExePath))
            } catch { return $false }
        } | Select-Object -First 1
        if ($candidate) {
            $newProc = $candidate
            $newProcStart = $null
            try {
                $newProcPath = $candidate.MainModule.FileName
                $newProcVersion = $candidate.MainModule.FileVersionInfo.ProductVersion
                $newProcStart = $candidate.StartTime.ToUniversalTime()
            } catch { $newProcPath = ''; $newProcVersion = ''; $newProcStart = $null }
            if ($newProcPath) { $relaunched = $true; break }
            $newProc = $null
        }
        if (Test-Path $ProgressLog) {
            # Match only THIS run's crash signature: the trace is appended across
            # runs, so a stale tail from an earlier run would false-positive here.
            $tail = @(Get-Content $ProgressLog -Tail 30 -ErrorAction SilentlyContinue |
                Where-Object { $_ -ge $ApplyStartedUtc.ToString('yyyy-MM-ddTHH:mm') })
            if (($tail -match 'ObjectDisposedException').Count -gt 0) {
                $defectDetected = $true
                Get-Process | Where-Object { $_.ProcessName -like 'chummer-avalonia-win-x64-installer*' } |
                    ForEach-Object { try { $_.Kill(); $_.WaitForExit(5000) | Out-Null } catch {} }
                Start-Sleep -Seconds 2
                break
            }
        }
        Start-Sleep -Seconds 5
    }
    if (-not $relaunched -and -not $defectDetected) { throw 'updated install did not relaunch the head within 3 minutes' }
    if (-not $relaunched) {
        # Diagnostic-only recovery relaunch (WINDOWS_SCOPE re-audit item 5.1):
        # the harness may still bring the updated install up to observe it, but
        # a harness-launched app can never pass the autonomous-update leg. The
        # diagnostic evidence goes to its own leg and update-relaunch FAILS.
        $relaunchMode = 'harness-fallback'
        $psiR = New-Object System.Diagnostics.ProcessStartInfo
        $psiR.FileName = $infoUpd.ExePath
        $psiR.WorkingDirectory = $infoUpd.InstallDir
        $psiR.UseShellExecute = $false
        Set-JourneyAppEnvironment -StartInfo $psiR -StateRoot $JourneyStateRoot
        $psiR.EnvironmentVariables.Remove('CHUMMER_CORE_CONTENT_ROOT')
        $newProc = $null
        try { $newProc = [System.Diagnostics.Process]::Start($psiR) }
        catch {
            # A still-locked staged installer or AV scan can transiently deny the
            # launch; reap every chummer process and retry once.
            Get-Process | Where-Object { $_.ProcessName -like 'chummer*' } |
                ForEach-Object { try { $_.Kill(); $_.WaitForExit(5000) | Out-Null } catch {} }
            Start-Sleep -Seconds 3
            $newProc = [System.Diagnostics.Process]::Start($psiR)
        }
        Start-Sleep -Seconds 15
        $diagAlive = -not $newProc.HasExited
        $diagPath = ''; $diagVersion = ''
        try {
            $diagPath = $newProc.MainModule.FileName
            $diagVersion = $newProc.MainModule.FileVersionInfo.ProductVersion
        } catch {}
        if ($diagAlive) { try { $newProc.Kill(); $newProc.WaitForExit(10000) | Out-Null } catch {} }
        Write-Leg 'update-relaunch-diagnostic' @{
            status = 'diagnostic (harness fallback used; installer-auto relaunch missing)'
            defectSignature = 'ObjectDisposedException in installer splash'
            harnessLaunchedExe = $diagPath
            harnessLaunchedProductVersion = $diagVersion
            harnessLaunchedStayedAlive = $diagAlive
        }
        Fail-Leg 'update-relaunch' 'installer crash prevented autonomous relaunch; harness fallback is diagnostic-only and cannot pass the autonomous-update leg (defectSignature=ObjectDisposedException)'
    }
    Start-Sleep -Seconds 15
    $stillAlive = -not $newProc.HasExited
    if (-not $stillAlive) {
        $diagId = '?'
        $diagStart = '?'
        try { $diagId = $newProc.Id; $diagStart = $newProc.StartTime.ToString('o') } catch {}
        $diagState = ''
        try {
            $sfDiag = Join-Path $UpdateStateRoot 'state.json'
            if (Test-Path $sfDiag) {
                $jDiag = (Get-Content $sfDiag -Raw) | ConvertFrom-Json
                $diagState = " state.lastError='$([string]$jDiag.LastError)' state.failureReason='$([string]$jDiag.LastFailureReason)'"
            }
        } catch {}
        throw "relaunched head (pid $diagId, started $diagStart) exited within 15s (relaunchMode: $relaunchMode)$diagState"
    }
    # Bind to the pre-apply boundary and old process, not the later registry
    # observation. The expected v2 payload hash below supplies version identity.
    if (-not $newProcPath -or ($newProcPath -ine $infoUpd.ExePath)) { throw "relaunched head is not the expected executable: $newProcPath" }
    if (-not $newProcStart -or -not (Test-UpdatedProcessBirth -ProcessId $newProc.Id -StartTimeUtc $newProcStart `
        -PreviousProcessId $PreviousProcessId -PreviousStartUtc $PreviousStartUtc -ApplyStartedUtc $ApplyStartedUtc)) {
        throw 'Relaunched head is not a new process from this apply attempt.'
    }
    if (-not $p.HasExited) { throw 'Pre-update application is still running.' }
    # Payload-byte binding (PR #513 followup P2): the accepted head must BE the
    # v2 build bytes. Registry transition + path + birth order alone would
    # accept an old payload whose registry metadata was rewritten; the running
    # executable must hash identically to the published v2 payload exe.
    $ExpectedV2ExeSha = Get-FileSha256 (Join-Path $PubV2 $LaunchTarget)
    $newProcExeSha = ''
    try { $newProcExeSha = Get-FileSha256 $newProcPath } catch { $newProcExeSha = '' }
    if (-not $newProcExeSha -or ($newProcExeSha -ine $ExpectedV2ExeSha)) { throw "relaunched head bytes are not the expected v2 payload exe (expected $ExpectedV2ExeSha, observed $newProcExeSha)" }
    $newProc.Kill(); $newProc.WaitForExit(10000) | Out-Null
    Invoke-InstalledCharacterProbe -Exe $infoUpd.ExePath -Phase reopen -Label reopen-after-update
    # Shortcut retention evidence (WINDOWS_SCOPE re-audit item 5.2): the
    # shortcuts present before the update must survive it.
    $ShortcutsAfter = Get-LnkSnapshot
    $retentionError = Test-LnkRetention $ShortcutsBefore $ShortcutsAfter $infoUpd.ExePath
    if ($retentionError) { throw $retentionError }
    # Negative controls (PR #513 followup P2): the comparator must reject the
    # loss of exactly one shortcut and a retargeted shortcut, otherwise the
    # positive assertion above is vacuous.
    $allKeys = @($ShortcutsAfter.Keys)
    $probeKey = $allKeys[0]
    $lostOne = @{}
    foreach ($k in $allKeys) { $lostOne[$k] = $ShortcutsAfter[$k] }
    $lostOne.Remove($probeKey)
    if (-not (Test-LnkRetention $ShortcutsBefore $lostOne $infoUpd.ExePath)) { throw 'retention comparator failed to reject a lost shortcut (negative control)' }
    $retargeted = @{}
    foreach ($k in $allKeys) { $retargeted[$k] = $ShortcutsAfter[$k] }
    $retargeted[$probeKey] = ($ShortcutsAfter[$probeKey] -split '\|')[0] + '|' + ($ShortcutsAfter[$probeKey] -split '\|')[1] + '|C:\definitely-not-chummer.exe'
    if (-not (Test-LnkRetention $ShortcutsBefore $retargeted $infoUpd.ExePath)) { throw 'retention comparator failed to reject a retargeted shortcut (negative control)' }

    $stateFile = Join-Path $stateRoot 'state.json'
    $stateSnap = ''
    if (Test-Path $stateFile) { $stateSnap = Get-Content $stateFile -Raw }
    Write-Leg 'update-relaunch' @{
        status = 'pass'
        fromVersion = $V1Stamp; toVersion = $infoUpd.DisplayVersion
        registryVersionAfter = $infoUpd.DisplayVersion
        headRelaunched = $true; relaunchedStayedAlive = $stillAlive
        relaunchMode = $relaunchMode
        relaunchedProcessPath = $newProcPath
        relaunchedProcessProductVersion = $newProcVersion
        processBoundToExpectedExecutable = ($newProcPath -ieq $infoUpd.ExePath)
        processBoundToUpdatedVersion = ($newProcExeSha -ieq $ExpectedV2ExeSha)
        applyStartedUtc = $ApplyStartedUtc.ToString('o')
        updateObservedAtUtc = $updateObservedAtUtc.ToUniversalTime().ToString('o')
        relaunchedProcessStartedUtc = $newProcStart.ToString('o')
        expectedV2ExeSha256 = $ExpectedV2ExeSha
        relaunchedProcessExeSha256 = $newProcExeSha
        processBoundToV2PayloadBytes = ($newProcExeSha -ieq $ExpectedV2ExeSha)
        shortcutsRetainedAll = ($null -eq $retentionError)
        installerAutoRelaunchDefectDetected = $defectDetected
        retainedShortcuts = $ShortcutsAfter.Count
        shortcutsBefore = ($ShortcutsBefore | ConvertTo-Json -Depth 3)
        shortcutsAfter = ($ShortcutsAfter | ConvertTo-Json -Depth 3)
        updateStateSnapshot = $stateSnap
        installerUsed = $V2Installer
    }
} catch {
    Stop-HeadProcesses
    # Attach the installer's own progress trace tail and the exact failure
    # position so failures carry their cause (harness diagnosability contract).
    $instTrace = Join-Path $env:TEMP 'chummer-desktop-installer-progress.log'
    $why = $_.Exception.Message
    $pos = $_.InvocationInfo.PositionMessage
    $stack = $_.ScriptStackTrace
    if (Test-Path $instTrace) {
        $tail = (Get-Content $instTrace -Tail 3) -join ' | '
        $why = "$why [installer-trace] $tail"
    }
    $why = "$why [at] $pos [stack] $stack"
    Fail-Leg 'update-relaunch' $why
}

# ---------------------------------------------------------------------------
# Leg 7: recovery (sha-mismatched artifact must fail safely and journal)
# ---------------------------------------------------------------------------
try {
    $stateRoot = $UpdateStateRoot
    $stateFile = Join-Path $stateRoot 'state.json'
    if (Test-Path $stateFile) { Remove-Item -Force $stateFile }
    Stop-HeadProcesses

    # Identity/content snapshot BEFORE the attempt (WINDOWS_SCOPE re-audit item
    # 5.3): a checksum-mismatch attempt must leave the installed identity and
    # content byte-identical. Compare this AFTER the rejection.
    function Get-InstallFingerprint {
        $info = Get-InstallInfo
        $exe = Join-Path $info.InstallDir $LaunchTarget
        # PR #513 followup P1: hash every file's CONTENTS, not paths+lengths -
        # a same-length DLL swap left the old digest unchanged. Enumeration and
        # read failures abort the leg (fail-closed inventory).
        $files = @(Get-ChildItem -Path $info.InstallDir -Recurse -File -Force -ErrorAction Stop | Sort-Object FullName)
        $h = [System.Security.Cryptography.SHA256]::Create()
        try {
            $ms = New-Object System.IO.MemoryStream
            $sw = New-Object System.IO.StreamWriter($ms, (New-Object System.Text.UTF8Encoding($false)))
            foreach ($f in $files) {
                $rel = $f.FullName.Substring($info.InstallDir.Length).TrimStart('\').ToLowerInvariant()
                $fs = [System.IO.File]::OpenRead($f.FullName)
                try { $fsha = [BitConverter]::ToString($h.ComputeHash($fs)).Replace('-', '').ToLowerInvariant() } finally { $fs.Dispose() }
                $sw.Write("$rel`0$fsha")
            }
            $sw.Flush()
            $digest = [BitConverter]::ToString($h.ComputeHash($ms.ToArray())).Replace('-', '').ToLowerInvariant()
        } finally { $h.Dispose() }
        return @{
            exeSha256 = if (Test-Path $exe) { Get-FileSha256 $exe } else { '' }
            exeBytes = if (Test-Path $exe) { (Get-Item $exe).Length } else { -1 }
            fileCount = $files.Count
            contentDigest = $digest
            registryVersion = $info.DisplayVersion
        }
    }
    $before = Get-InstallFingerprint
    if (-not $before.exeSha256) { throw 'pre-check snapshot: installed exe missing' }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $InstalledExe
    $psi.UseShellExecute = $false
    Set-JourneyAppEnvironment -StartInfo $psi -StateRoot $JourneyStateRoot
    $psi.EnvironmentVariables.Remove('CHUMMER_CORE_CONTENT_ROOT')
    $psi.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_ENABLED'] = '1'
    $psi.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_MODE'] = 'full'
    $psi.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_AUTO_APPLY'] = 'true'
    $psi.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_MANIFEST'] = $BadManifest
    $p = [System.Diagnostics.Process]::Start($psi)

    # Positive evidence loop (WINDOWS_SCOPE re-audit item 5.3): liveness alone
    # proves nothing. The runtime must JOURNAL the attempted checksum check and
    # its rejection in desktop-update state.json: LastFailureReason =
    # 'update_apply_failed' and LastError naming the failed checksum validation
    # with the manifest's CLAIMED sha256 and the artifact's OBSERVED sha256.
    # A check that never journals fails this leg (no-op checks are not passes).
    $claimedSha = '0' * 64
    $observedSha = $v2InstallerSha
    $rejectionSeen = $false
    $stateSnap = ''
    $installerObservedByPolling = $false
    $traceLinesBefore = if (Test-Path $ProgressLog) { @(Get-Content $ProgressLog -ErrorAction SilentlyContinue).Count } else { 0 }
    $deadline = (Get-Date).AddMinutes(4)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 10
        if (Test-Path $stateFile) {
            try { $stateSnap = Get-Content $stateFile -Raw } catch {}
            if ($stateSnap) {
                $failReason = ''
                $lastError = ''
                try {
                    $j = $stateSnap | ConvertFrom-Json
                    $failReason = [string]$j.LastFailureReason
                    $lastError = [string]$j.LastError
                } catch {}
                if ($failReason -eq 'update_apply_failed' -and
                    $lastError -like '*failed checksum validation*' -and
                    $lastError -like "*Expected '$claimedSha'*" -and
                    $lastError -like "*observed '$observedSha'*") {
                    $rejectionSeen = $true
                    break
                }
            }
        }
        if (Get-Process | Where-Object { $_.ProcessName -like 'chummer-avalonia-win-x64-installer*' }) { $installerObservedByPolling = $true }
        if ($p.HasExited) { break }
    }
    # Bind the surviving app process to the expected executable and version.
    $observedAppPath = ''; $observedAppVersion = ''
    try {
        $observedAppPath = $p.MainModule.FileName
        $observedAppVersion = $p.MainModule.FileVersionInfo.ProductVersion
    } catch {}
    $stillUp = -not $p.HasExited
    try { $p.Kill(); $p.WaitForExit(10000) | Out-Null } catch {}

    if (-not $rejectionSeen) {
        $snapHead = if ($stateSnap) { $stateSnap.Substring(0, [Math]::Min(400, $stateSnap.Length)) } else { 'no state.json' }
        throw "checksum-mismatch rejection not journaled within 4 minutes (attempted-check evidence absent; state: $snapHead)"
    }
    if (-not $stillUp) { throw 'app exited during failed-update leg (expected backoff, not exit)' }
    # No-installer-invocation evidence (PR #513 followup P2, honesty): the old
    # check polled process names every 10s AFTER the initial sleep, so a
    # short-lived installer could exit unobserved and the rejection branch
    # typically broke before the first poll. Polling observations are recorded
    # but never asserted. The check is grounded in apply evidence: the runtime
    # journaled the rejection at checksum VALIDATION (a stage that precedes any
    # installer handoff) and the installer's own progress trace appended
    # nothing during this leg.
    $traceLinesAfter = if (Test-Path $ProgressLog) { @(Get-Content $ProgressLog -ErrorAction SilentlyContinue).Count } else { 0 }
    $traceLinesDuringLeg = [Math]::Max(0, $traceLinesAfter - $traceLinesBefore)
    if ($traceLinesDuringLeg -gt 0) { throw "installer progress trace gained $traceLinesDuringLeg lines during the rejected update (installer activity during a rejection leg)" }

    # Unchanged installed identity/content after the rejected update.
    $after = Get-InstallFingerprint
    if ($after.exeSha256 -ne $before.exeSha256) { throw "installed exe changed after rejected update (before=$($before.exeSha256) after=$($after.exeSha256))" }
    if ($after.registryVersion -ne $before.registryVersion) { throw "registry version changed after rejected update ($($before.registryVersion) -> $($after.registryVersion))" }
    if ($after.contentDigest -ne $before.contentDigest) { throw 'install directory content changed after rejected update' }
    # Fingerprint negative control (PR #513 followup P1): flipping a non-exe
    # byte WITHOUT changing the filename or the length must change the content
    # digest - the old path+length scheme was blind to exactly this mutation -
    # and restoring the bytes must return the digest to the pre-leg value.
    $victim = @(Get-ChildItem -Path ((Get-InstallInfo).InstallDir) -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ine $LaunchTarget -and $_.Length -gt 0 } |
        Sort-Object Length | Select-Object -First 1)
    if (-not $victim) { throw 'fingerprint negative control: no mutable non-exe file found in install dir' }
    $victimPath = $victim[0].FullName
    $victimOriginal = [System.IO.File]::ReadAllBytes($victimPath)
    $victimMutated = [byte[]]::new($victimOriginal.Length)
    [Array]::Copy($victimOriginal, $victimMutated, $victimOriginal.Length)
    try {
        $victimMutated[$victimMutated.Length - 1] = $victimMutated[$victimMutated.Length - 1] -bxor 0xFF
        [System.IO.File]::WriteAllBytes($victimPath, $victimMutated)
        $mutatedFp = Get-InstallFingerprint
        if ($mutatedFp.contentDigest -eq $before.contentDigest) { throw 'fingerprint did not detect a same-length non-exe mutation (negative control failed)' }
    } finally {
        [System.IO.File]::WriteAllBytes($victimPath, $victimOriginal)
    }
    $restoredFp = Get-InstallFingerprint
    if ($restoredFp.contentDigest -ne $before.contentDigest) { throw 'fingerprint did not return to baseline after restoring the mutated file' }
    if ($observedAppPath -and ($observedAppPath -ine $InstalledExe)) { throw "surviving app is not the installed executable: $observedAppPath" }

    # Post-failure recovery: the same install starts cleanly with the channel
    # off. The recovery process is bound to the expected executable AND the
    # installed version - not a process name alone (re-audit item 5.3).
    $psi2 = New-Object System.Diagnostics.ProcessStartInfo
    $psi2.FileName = $InstalledExe
    $psi2.UseShellExecute = $false
    Set-JourneyAppEnvironment -StartInfo $psi2 -StateRoot $JourneyStateRoot
    $psi2.EnvironmentVariables.Remove('CHUMMER_CORE_CONTENT_ROOT')
    $psi2.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_ENABLED'] = '0'
    $psi2.EnvironmentVariables['CHUMMER_DESKTOP_UPDATE_MODE'] = 'off'
    $p2 = [System.Diagnostics.Process]::Start($psi2)
    Start-Sleep -Seconds 12
    $recovered = -not $p2.HasExited
    $recoveredPath = ''; $recoveredVersion = ''
    try {
        $recoveredPath = $p2.MainModule.FileName
        $recoveredVersion = $p2.MainModule.FileVersionInfo.ProductVersion
    } catch {}
    if ($recovered) { try { $p2.Kill(); $p2.WaitForExit(10000) | Out-Null } catch {} }
    if (-not $recovered) { throw 'app failed to start after failed-update leg' }
    if (-not $recoveredPath -or ($recoveredPath -ine $InstalledExe)) { throw "recovery process is not the expected executable: $recoveredPath" }
    # Version identity is bound via the unchanged exe bytes + unchanged registry
    # DisplayVersion above (the run stamp is AssemblyMetadata, not Win32
    # ProductVersion); the recovered process runs exactly that bound executable.

    Invoke-InstalledCharacterProbe -Exe $InstalledExe -Phase reopen -Label reopen-after-recovery
    $finalStateSnap = ''
    try {
        $finalStateSnap = [System.IO.File]::ReadAllText($stateFile)
    } catch {}
    Write-Leg 'recovery' @{
        status = 'pass'
        badManifest = $BadManifest
        attemptedCheckEvidence = 'journaled checksum-mismatch rejection in desktop-update state.json'
        claimedSha256 = $claimedSha
        observedSha256 = $observedSha
        rejectionJournaled = $rejectionSeen
        installerObservedByPolling = $installerObservedByPolling
        installerTraceLinesDuringLeg = $traceLinesDuringLeg
        installerInvocationEvidence = 'rejection journaled at checksum validation (pre-handoff stage); installer progress trace appended nothing during this leg; name-polling recorded as observation, not absence proof'
        appSurvivedFailedUpdate = $stillUp
        survivingAppPath = $observedAppPath
        survivingAppProductVersion = $observedAppVersion
        identityBefore = $before.registryVersion
        identityAfter = $after.registryVersion
        exeSha256Unchanged = ($after.exeSha256 -eq $before.exeSha256)
        contentDigestUnchanged = ($after.contentDigest -eq $before.contentDigest)
        fingerprintNegativeControl = 'same-length non-exe byte flip detected; restore returned baseline'
        failureStateSnapshot = $finalStateSnap
        restartedAfterFailure = $recovered
        recoveryProcessPath = $recoveredPath
        recoveryProcessProductVersion = $recoveredVersion
    }
} catch {
    Stop-HeadProcesses
    $why = "$($_.Exception.Message) [at] $($_.InvocationInfo.PositionMessage) [stack] $($_.ScriptStackTrace)"
    Fail-Leg 'recovery' $why
}

# ---------------------------------------------------------------------------
# Leg 8: native uninstall
# ---------------------------------------------------------------------------
try {
    Stop-HeadProcesses
    $installDir = (Get-ItemProperty $UninstallKeyName -ErrorAction SilentlyContinue).InstallLocation
    $r = Invoke-Step $V2Installer @('--uninstall', '--unattended') 300 'uninstall'
    if ($r.ExitCode -ne 0) { throw "uninstall exit $($r.ExitCode): $($r.StdErr)" }
    if (Test-Path $UninstallKeyName) { throw 'registry uninstall key still present' }
    # Directory removal is scheduled via a delayed cmd script - poll before asserting.
    $dirGone = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 2
        if (-not $installDir -or -not (Test-Path $installDir)) { $dirGone = $true; break }
    }
    if (-not $dirGone) { throw "install dir still present after uninstall grace window: $installDir" }
    Write-Leg 'uninstall' @{ status = 'pass'; installer = $V2Installer; registryCleared = $true; installDirRemoved = $true }
} catch {
    Fail-Leg 'uninstall' $_.Exception.Message
}

# ---------------------------------------------------------------------------
# Receipt
# ---------------------------------------------------------------------------
$script:Receipt.completedUtc = (Get-Date).ToUniversalTime().ToString('o')
# ---------------------------------------------------------------------------
# Character-data preservation (spans update + recovery legs)
# ---------------------------------------------------------------------------
$CharacterDataRoot2 = $CharacterDataRoot
$CharacterDataAfter = Get-CharacterDataSnapshot -Root $CharacterDataRoot2
$characterDataReport = Test-CharacterDataPreservation -Before $script:CharacterDataBefore -After $CharacterDataAfter -Root $CharacterDataRoot2
Write-Leg 'character-data' @{ status = $characterDataReport.status; before = $characterDataReport.beforeCount; after = $characterDataReport.afterCount; lost = @($characterDataReport.lost).Count; modified = @($characterDataReport.modified).Count; added = @($characterDataReport.added).Count }
if ($characterDataReport.status -eq 'fail') { Fail-Leg 'character-data' "character data modified or lost across the update; lost=$(@($characterDataReport.lost).Count) modified=$(@($characterDataReport.modified).Count)" (ConvertTo-Json $characterDataReport -Depth 4) }
if ($characterDataReport.status -ne 'pass') { Fail-Leg 'character-data' "character-data snapshot inconclusive: before-count was $($characterDataReport.beforeCount)" (ConvertTo-Json $characterDataReport -Depth 4) }

$script:Receipt.status = 'pass'
$script:Receipt.claimBoundary = 'Local unsigned artifacts built from this working tree; no signed-release or published-channel claims.'
($Receipt | ConvertTo-Json -Depth 8) | Set-Content -Encoding UTF8 $ReceiptPath
if ($NeedNuGetConfig -and (Test-Path $NuGetConfigPath)) { Remove-Item $NuGetConfigPath -Force }
Write-Host "[win004] JOURNEY PASS -> $ReceiptPath" -ForegroundColor Green
if (-not $KeepArtifacts) {
    # Keep dist (installers/payloads/manifests) for evidence; drop multi-GB publish trees.
    foreach ($dir in @($PubV1, $PubV2)) {
        if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
    }
}
exit 0
