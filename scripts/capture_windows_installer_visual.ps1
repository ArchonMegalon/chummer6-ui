[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet("avalonia", "blazor-desktop")][string]$Head,
    [Parameter(Mandatory = $true)][string]$InstallerPath,
    [Parameter(Mandatory = $true)][string]$PayloadPath,
    [Parameter(Mandatory = $true)][ValidatePattern("^[0-9a-f]{64}$")][string]$PayloadSha256,
    [Parameter(Mandatory = $true)][string]$ProgressScreenshot,
    [Parameter(Mandatory = $true)][string]$CompletionScreenshot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
    throw "Interactive installer capture requires a native Windows host."
}
if ($env:WINELOADERNOEXEC -or $env:WINEPREFIX) {
    throw "Wine cannot produce native Windows installer evidence."
}

$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$payload = (Resolve-Path -LiteralPath $PayloadPath).Path
if ((Get-FileHash -LiteralPath $PayloadPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $PayloadSha256) {
    throw "$Head payload bytes do not match the exact SHA-256 binding."
}
$payloadSize = (Get-Item -LiteralPath $payload).Length
$tracePath = Join-Path $env:TEMP "Chummer6\installer-temp\chummer-desktop-installer-progress.log"
if (Test-Path -LiteralPath $tracePath) {
    Remove-Item -LiteralPath $tracePath -Force
}

$progressParent = Split-Path -Parent $ProgressScreenshot
$completionParent = Split-Path -Parent $CompletionScreenshot
New-Item -ItemType Directory -Force -Path $progressParent, $completionParent | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ChummerNativeWindowCapture {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

function Wait-TraceMarker {
    param([string]$Marker, [int]$TimeoutSeconds = 300)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $tracePath) {
            $trace = Get-Content -LiteralPath $tracePath -Raw -ErrorAction SilentlyContinue
            if ($trace -and $trace.Contains($Marker)) { return }
        }
        if ($script:installerProcess.HasExited) {
            throw "$Head installer exited before trace marker '$Marker'."
        }
        Start-Sleep -Milliseconds 100
    }
    throw "$Head installer timed out before trace marker '$Marker'."
}

function Wait-MainWindow {
    param([int]$TimeoutSeconds = 60)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $script:installerProcess.Refresh()
        if ($script:installerProcess.MainWindowHandle -ne [IntPtr]::Zero) {
            return $script:installerProcess.MainWindowHandle
        }
        if ($script:installerProcess.HasExited) { throw "$Head installer exited without an interactive window." }
        Start-Sleep -Milliseconds 100
    }
    throw "$Head installer did not expose an interactive window."
}

function Save-WindowPng {
    param([IntPtr]$WindowHandle, [string]$OutputPath)
    $rect = New-Object ChummerNativeWindowCapture+RECT
    if (-not [ChummerNativeWindowCapture]::GetWindowRect($WindowHandle, [ref]$rect)) {
        throw "Could not resolve the native installer window bounds."
    }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -lt 320 -or $height -lt 200) { throw "Installer window is too small for accountable review." }
    [ChummerNativeWindowCapture]::SetForegroundWindow($WindowHandle) | Out-Null
    Start-Sleep -Milliseconds 200
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
        } finally {
            $graphics.Dispose()
        }
        $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $bitmap.Dispose()
    }
}

$priorPayloadPath = $env:CHUMMER_INSTALLER_PAYLOAD_PATH
$priorPayloadSha = $env:CHUMMER_INSTALLER_PAYLOAD_SHA256
$priorPayloadSize = $env:CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES
$script:installerProcess = $null
try {
    $env:CHUMMER_INSTALLER_PAYLOAD_PATH = $payload
    $env:CHUMMER_INSTALLER_PAYLOAD_SHA256 = $PayloadSha256
    $env:CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES = [string]$payloadSize
    $script:installerProcess = Start-Process -FilePath $installer -PassThru
    $window = Wait-MainWindow
    Wait-TraceMarker -Marker "Extracting application files"
    Save-WindowPng -WindowHandle $window -OutputPath $ProgressScreenshot
    Wait-TraceMarker -Marker "Install complete"
    $script:installerProcess.Refresh()
    Save-WindowPng -WindowHandle $script:installerProcess.MainWindowHandle -OutputPath $CompletionScreenshot
    if ((Get-FileHash -LiteralPath $ProgressScreenshot -Algorithm SHA256).Hash -eq
        (Get-FileHash -LiteralPath $CompletionScreenshot -Algorithm SHA256).Hash) {
        throw "$Head progress and completion screenshots are digest-identical."
    }
} finally {
    $env:CHUMMER_INSTALLER_PAYLOAD_PATH = $priorPayloadPath
    $env:CHUMMER_INSTALLER_PAYLOAD_SHA256 = $priorPayloadSha
    $env:CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES = $priorPayloadSize
    if ($script:installerProcess -and -not $script:installerProcess.HasExited) {
        $script:installerProcess.CloseMainWindow() | Out-Null
        if (-not $script:installerProcess.WaitForExit(5000)) { $script:installerProcess.Kill() }
    }
}
