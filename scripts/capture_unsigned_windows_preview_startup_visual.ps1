[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstalledExecutablePath,
    [Parameter(Mandatory = $true)][string]$InstallerPath,
    [Parameter(Mandatory = $true)][ValidatePattern("^[0-9a-f]{64}$")][string]$InstallerSha256,
    [Parameter(Mandatory = $true)][long]$InstallerSizeBytes,
    [Parameter(Mandatory = $true)][string]$PayloadPath,
    [Parameter(Mandatory = $true)][ValidatePattern("^[0-9a-f]{64}$")][string]$PayloadSha256,
    [Parameter(Mandatory = $true)][long]$PayloadSizeBytes,
    [Parameter(Mandatory = $true)][string]$CandidateSourceSha,
    [Parameter(Mandatory = $true)][string]$CandidateVersion,
    [Parameter(Mandatory = $true)][string]$StartupScreenshot,
    [Parameter(Mandatory = $true)][string]$OutputReceipt,
    [Parameter(Mandatory = $true)][string]$SourceRepository,
    [Parameter(Mandatory = $true)][string]$SourceWorkflow,
    [Parameter(Mandatory = $true)][string]$SourceRunId,
    [Parameter(Mandatory = $true)][string]$SourceRunAttempt,
    [Parameter(Mandatory = $true)][string]$SourceRef,
    [Parameter(Mandatory = $true)][string]$SourceSha,
    [Parameter(Mandatory = $true)][string]$SourceActor,
    [Parameter(Mandatory = $true)][string]$SourceTriggeringActor
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Application startup capture requires a native Windows host.'
}
if ($env:WINELOADERNOEXEC -or $env:WINEPREFIX) {
    throw 'Wine cannot produce native Windows application startup evidence.'
}
if ($SourceRepository -cne 'ArchonMegalon/chummer6-ui' -or
    $SourceWorkflow -cne '.github/workflows/unsigned-windows-preview-native-evidence-capture.yml' -or
    $SourceRef -cne 'refs/heads/main') {
    throw 'Application startup capture source authority differs.'
}
if ($SourceRunId -cnotmatch '^[1-9][0-9]*$' -or
    $SourceRunAttempt -cnotmatch '^[1-9][0-9]*$' -or
    $SourceSha -cnotmatch '^[0-9a-f]{40}$' -or
    $CandidateSourceSha -cnotmatch '^[0-9a-f]{40}$') {
    throw 'Application startup capture source identity is malformed.'
}
if ($SourceActor -cne $SourceTriggeringActor) {
    throw 'Application startup capture permits only same-actor reruns.'
}
if ($SourceActor -cne 'github-actions[bot]') {
    throw 'Application startup capture requires the hosted automation actor.'
}
if ($CandidateVersion -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$') {
    throw 'Candidate version is malformed.'
}

$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$payload = (Resolve-Path -LiteralPath $PayloadPath).Path
$executable = (Resolve-Path -LiteralPath $InstalledExecutablePath).Path
if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant() -cne
    $InstallerSha256 -or (Get-Item -LiteralPath $installer).Length -ne $InstallerSizeBytes) {
    throw 'Installed application capture installer bytes differ.'
}
if ((Get-FileHash -LiteralPath $payload -Algorithm SHA256).Hash.ToLowerInvariant() -cne
    $PayloadSha256 -or (Get-Item -LiteralPath $payload).Length -ne $PayloadSizeBytes) {
    throw 'Installed application capture payload bytes differ.'
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ChummerUnsignedPreviewStartupCapture {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

$archive = [IO.Compression.ZipFile]::OpenRead($payload)
try {
    $matches = @($archive.Entries | Where-Object {
        $_.FullName.Replace('\', '/').EndsWith('/Chummer.Avalonia.exe',
            [StringComparison]::OrdinalIgnoreCase) -or
        $_.FullName -ieq 'Chummer.Avalonia.exe'
    })
    if ($matches.Count -ne 1) {
        throw "Expected one Chummer.Avalonia.exe payload entry, found $($matches.Count)."
    }
    $payloadEntry = $matches[0]
    $payloadEntryName = $payloadEntry.FullName.Replace('\', '/')
    $payloadEntryLength = $payloadEntry.Length
    $stream = $payloadEntry.Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $payloadExecutableSha = [Convert]::ToHexString(
            $sha.ComputeHash($stream)
        ).ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
        $stream.Dispose()
    }
}
finally {
    $archive.Dispose()
}

$installedExecutableSha = (
    Get-FileHash -LiteralPath $executable -Algorithm SHA256
).Hash.ToLowerInvariant()
$installedExecutableSize = (Get-Item -LiteralPath $executable).Length
if ($installedExecutableSha -cne $payloadExecutableSha -or
    $installedExecutableSize -ne $payloadEntryLength) {
    throw 'Installed application executable differs from the exact candidate payload entry.'
}

$screenshotParent = Split-Path -Parent $StartupScreenshot
$receiptParent = Split-Path -Parent $OutputReceipt
New-Item -ItemType Directory -Force -Path $screenshotParent, $receiptParent |
    Out-Null
if ((Test-Path -LiteralPath $StartupScreenshot) -or
    (Test-Path -LiteralPath $OutputReceipt)) {
    throw 'Startup visual output paths must be absent.'
}

$priorUpdate = $env:CHUMMER_DESKTOP_UPDATE_ENABLED
$process = $null
try {
    $env:CHUMMER_DESKTOP_UPDATE_ENABLED = '0'
    $process = Start-Process -FilePath $executable -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    $window = [IntPtr]::Zero
    while ([DateTime]::UtcNow -lt $deadline) {
        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            $window = $process.MainWindowHandle
            break
        }
        if ($process.HasExited) {
            throw 'Installed application exited before exposing its startup window.'
        }
        Start-Sleep -Milliseconds 100
    }
    if ($window -eq [IntPtr]::Zero) {
        throw 'Installed application did not expose its startup window.'
    }
    $rect = New-Object ChummerUnsignedPreviewStartupCapture+RECT
    if (-not [ChummerUnsignedPreviewStartupCapture]::GetWindowRect(
            $window, [ref]$rect)) {
        throw 'Could not resolve the installed application window bounds.'
    }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -lt 320 -or $height -lt 200) {
        throw 'Installed application window is too small for accountable review.'
    }
    [ChummerUnsignedPreviewStartupCapture]::SetForegroundWindow($window) |
        Out-Null
    Start-Sleep -Milliseconds 500
    $bitmap = New-Object Drawing.Bitmap $width, $height
    try {
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen(
                $rect.Left, $rect.Top, 0, 0, $bitmap.Size
            )
        }
        finally {
            $graphics.Dispose()
        }
        $bitmap.Save(
            $StartupScreenshot,
            [Drawing.Imaging.ImageFormat]::Png
        )
    }
    finally {
        $bitmap.Dispose()
    }

    $screenshotSha = (
        Get-FileHash -LiteralPath $StartupScreenshot -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $receipt = [ordered]@{
        candidate = [ordered]@{
            installer = [ordered]@{
                fileName = [IO.Path]::GetFileName($installer)
                path = 'publication/files/chummer-avalonia-win-x64-installer.exe'
                sha256 = $InstallerSha256
                sizeBytes = $InstallerSizeBytes
            }
            payload = [ordered]@{
                fileName = [IO.Path]::GetFileName($payload)
                path = 'publication/files/chummer-avalonia-win-x64-payload.zip'
                sha256 = $PayloadSha256
                sizeBytes = $PayloadSizeBytes
            }
            release = [ordered]@{
                channel = 'preview'
                version = $CandidateVersion
            }
            signature = [ordered]@{
                policy = 'preview_policy'
                required = $false
                status = 'unsigned'
            }
            sourceSha = $CandidateSourceSha
        }
        contractName = 'chummer6-ui.unsigned-preview-windows-startup-visual'
        contractVersion = 1
        generatedAtUtc = [DateTime]::UtcNow.ToString(
            'yyyy-MM-ddTHH:mm:ssZ',
            [Globalization.CultureInfo]::InvariantCulture
        )
        installedExecutable = [ordered]@{
            fileName = [IO.Path]::GetFileName($executable)
            payloadEntry = $payloadEntryName
            sha256 = $installedExecutableSha
            sizeBytes = $installedExecutableSize
        }
        nativeHostEvidence = [ordered]@{
            contractName = 'chummer6-ui.native_windows_host_evidence'
            evidenceSource = 'GitHub-hosted windows-latest'
            hostPlatform = 'windows'
            isNativeWindows = $true
            runner = 'pwsh'
            status = 'verified'
        }
        source = [ordered]@{
            actor = $SourceActor
            artifactName = "unsigned-windows-preview-native-evidence-$SourceRunId-$SourceRunAttempt"
            ref = $SourceRef
            repository = $SourceRepository
            rerunPolicy = 'same-actor-only'
            runAttempt = $SourceRunAttempt
            runId = $SourceRunId
            sha = $SourceSha
            triggeringActor = $SourceTriggeringActor
            workflow = $SourceWorkflow
        }
        startupScreenshot = [ordered]@{
            height = $height
            path = 'screenshots/windows-application-avalonia-win-x64-startup.png'
            sha256 = $screenshotSha
            width = $width
        }
        status = 'captured'
    }
    $json = $receipt | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText(
        $OutputReceipt,
        $json + "`n",
        [Text.UTF8Encoding]::new($false)
    )
}
finally {
    $env:CHUMMER_DESKTOP_UPDATE_ENABLED = $priorUpdate
    if ($null -ne $process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(5000)) {
            $process.Kill()
            $process.WaitForExit()
        }
    }
}
