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
$MinimumReviewWidth = 320
$MinimumReviewHeight = 200
$RequiredStableObservationCount = 3
$WindowObservationPollMilliseconds = 100
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
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
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

function Get-MainWindowObservation {
    try {
        $script:installerProcess.Refresh()
        $windowHandle = $script:installerProcess.MainWindowHandle
    } catch [System.InvalidOperationException] {
        return $null
    }
    if ($windowHandle -eq [IntPtr]::Zero) {
        return $null
    }

    $isWindow = [ChummerNativeWindowCapture]::IsWindow($windowHandle)
    $isVisible = $isWindow -and [ChummerNativeWindowCapture]::IsWindowVisible($windowHandle)
    $isMinimized = $isWindow -and [ChummerNativeWindowCapture]::IsIconic($windowHandle)
    $windowOwnerProcessId = [uint32]0
    if ($isWindow) {
        [ChummerNativeWindowCapture]::GetWindowThreadProcessId(
            $windowHandle,
            [ref]$windowOwnerProcessId
        ) | Out-Null
    }
    $belongsToInstallerProcess = (
        $isWindow -and
        $windowOwnerProcessId -eq [uint32]$script:installerProcessId
    )
    $rect = New-Object ChummerNativeWindowCapture+RECT
    $boundsAvailable = $isWindow -and [ChummerNativeWindowCapture]::GetWindowRect($windowHandle, [ref]$rect)
    $width = 0
    $height = 0
    if ($boundsAvailable) {
        $width = $rect.Right - $rect.Left
        $height = $rect.Bottom - $rect.Top
    }

    return [pscustomobject]@{
        WindowHandle = $windowHandle
        HandleValue = $windowHandle.ToInt64()
        HandleText = "0x{0:X}" -f $windowHandle.ToInt64()
        IsWindow = $isWindow
        IsVisible = $isVisible
        IsMinimized = $isMinimized
        WindowOwnerProcessId = $windowOwnerProcessId
        BelongsToInstallerProcess = $belongsToInstallerProcess
        BoundsAvailable = $boundsAvailable
        Left = $rect.Left
        Top = $rect.Top
        Right = $rect.Right
        Bottom = $rect.Bottom
        Width = $width
        Height = $height
    }
}

function Format-WindowObservation {
    param([object]$Observation)
    if ($null -eq $Observation) {
        return "handle=0x0 ownerProcessId=0 width=0 height=0 visible=false minimized=false bounds=false"
    }
    $visible = ([string]$Observation.IsVisible).ToLowerInvariant()
    $minimized = ([string]$Observation.IsMinimized).ToLowerInvariant()
    $bounds = ([string]$Observation.BoundsAvailable).ToLowerInvariant()
    return "handle=$($Observation.HandleText) ownerProcessId=$($Observation.WindowOwnerProcessId) width=$($Observation.Width) height=$($Observation.Height) visible=$visible minimized=$minimized bounds=$bounds"
}

function Wait-ReviewableMainWindow {
    param(
        [Parameter(Mandatory = $true)][string]$Phase,
        [int]$TimeoutSeconds = 60
    )
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $latestObservation = $null
    $lastNonZeroObservation = $null
    $stableObservation = $null
    $stableObservationCount = 0
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($script:installerProcess.HasExited) {
            $latest = Format-WindowObservation -Observation $latestObservation
            $lastNonZero = Format-WindowObservation -Observation $lastNonZeroObservation
            throw "$Head installer exited before a stable reviewable $Phase window was available; latest observation $latest; last nonzero observation $lastNonZero."
        }

        $current = Get-MainWindowObservation
        $latestObservation = $current
        if ($null -ne $current) {
            $lastNonZeroObservation = $current
        }

        $reviewable = (
            $null -ne $current -and
            $current.IsWindow -and
            $current.IsVisible -and
            -not $current.IsMinimized -and
            $current.BelongsToInstallerProcess -and
            $current.BoundsAvailable -and
            $current.Width -ge $MinimumReviewWidth -and
            $current.Height -ge $MinimumReviewHeight
        )
        if ($reviewable) {
            $sameWindowAndBounds = (
                $null -ne $stableObservation -and
                $current.HandleValue -eq $stableObservation.HandleValue -and
                $current.Left -eq $stableObservation.Left -and
                $current.Top -eq $stableObservation.Top -and
                $current.Right -eq $stableObservation.Right -and
                $current.Bottom -eq $stableObservation.Bottom
            )
            if ($sameWindowAndBounds) {
                $stableObservationCount += 1
            } else {
                $stableObservation = $current
                $stableObservationCount = 1
            }
            if ($stableObservationCount -ge $RequiredStableObservationCount) {
                return $current
            }
        } else {
            $stableObservation = $null
            $stableObservationCount = 0
        }

        Start-Sleep -Milliseconds $WindowObservationPollMilliseconds
    }

    $latest = Format-WindowObservation -Observation $latestObservation
    $lastNonZero = Format-WindowObservation -Observation $lastNonZeroObservation
    throw "$Head installer did not expose a stable reviewable $Phase window within $TimeoutSeconds seconds; latest observation $latest; last nonzero observation $lastNonZero; required minimum width=$MinimumReviewWidth height=$MinimumReviewHeight stableObservations=$RequiredStableObservationCount."
}

function Save-WindowPng {
    param([object]$WindowObservation, [string]$OutputPath)
    $WindowHandle = [IntPtr]($WindowObservation.WindowHandle)
    if (
        $WindowHandle -eq [IntPtr]::Zero -or
        -not [ChummerNativeWindowCapture]::IsWindow($WindowHandle) -or
        -not [ChummerNativeWindowCapture]::IsWindowVisible($WindowHandle) -or
        [ChummerNativeWindowCapture]::IsIconic($WindowHandle)
    ) {
        $handleText = "0x{0:X}" -f $WindowHandle.ToInt64()
        throw "Installer window is not visible for accountable review; handle=$handleText."
    }
    $foregroundRequestAccepted = [ChummerNativeWindowCapture]::SetForegroundWindow($WindowHandle)
    Start-Sleep -Milliseconds 200
    try {
        $script:installerProcess.Refresh()
        if ($script:installerProcess.HasExited) {
            throw [System.InvalidOperationException]::new("installer process exited")
        }
        $currentMainWindowHandle = $script:installerProcess.MainWindowHandle
    } catch [System.InvalidOperationException] {
        $handleText = "0x{0:X}" -f $WindowHandle.ToInt64()
        throw "Installer exited before accountable capture; expectedHandle=$handleText."
    }
    $foregroundWindowHandle = [ChummerNativeWindowCapture]::GetForegroundWindow()
    $captureWindowStillVisible = (
        $currentMainWindowHandle -eq $WindowHandle -and
        $foregroundWindowHandle -eq $WindowHandle -and
        [ChummerNativeWindowCapture]::IsWindow($WindowHandle) -and
        [ChummerNativeWindowCapture]::IsWindowVisible($WindowHandle) -and
        -not [ChummerNativeWindowCapture]::IsIconic($WindowHandle)
    )
    if (-not $captureWindowStillVisible) {
        $expectedHandleText = "0x{0:X}" -f $WindowHandle.ToInt64()
        $observedHandleText = "0x{0:X}" -f $currentMainWindowHandle.ToInt64()
        $foregroundHandleText = "0x{0:X}" -f $foregroundWindowHandle.ToInt64()
        throw "Installer main window changed before accountable capture; expectedHandle=$expectedHandleText observedHandle=$observedHandleText foregroundHandle=$foregroundHandleText foregroundRequestAccepted=$foregroundRequestAccepted."
    }
    $windowOwnerProcessId = [uint32]0
    [ChummerNativeWindowCapture]::GetWindowThreadProcessId(
        $WindowHandle,
        [ref]$windowOwnerProcessId
    ) | Out-Null
    if ($windowOwnerProcessId -ne [uint32]$script:installerProcessId) {
        $handleText = "0x{0:X}" -f $WindowHandle.ToInt64()
        throw "Installer window ownership changed before accountable capture; handle=$handleText expectedProcessId=$script:installerProcessId observedProcessId=$windowOwnerProcessId."
    }
    $rect = New-Object ChummerNativeWindowCapture+RECT
    if (-not [ChummerNativeWindowCapture]::GetWindowRect($WindowHandle, [ref]$rect)) {
        $handleText = "0x{0:X}" -f $WindowHandle.ToInt64()
        throw "Could not resolve the native installer window bounds; handle=$handleText."
    }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $matchesStableBounds = (
        $rect.Left -eq $WindowObservation.Left -and
        $rect.Top -eq $WindowObservation.Top -and
        $rect.Right -eq $WindowObservation.Right -and
        $rect.Bottom -eq $WindowObservation.Bottom
    )
    if (-not $matchesStableBounds) {
        $handleText = "0x{0:X}" -f $WindowHandle.ToInt64()
        $expectedBounds = "$($WindowObservation.Left),$($WindowObservation.Top),$($WindowObservation.Right),$($WindowObservation.Bottom)"
        $observedBounds = "$($rect.Left),$($rect.Top),$($rect.Right),$($rect.Bottom)"
        throw "Installer window bounds changed after stable observation; handle=$handleText expectedBounds=$expectedBounds observedBounds=$observedBounds."
    }
    if ($width -lt $MinimumReviewWidth -or $height -lt $MinimumReviewHeight) {
        $handleText = "0x{0:X}" -f $WindowHandle.ToInt64()
        throw "Installer window is too small for accountable review; handle=$handleText width=$width height=$height requiredMinimumWidth=$MinimumReviewWidth requiredMinimumHeight=$MinimumReviewHeight."
    }
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
$script:installerProcessId = $null
try {
    $env:CHUMMER_INSTALLER_PAYLOAD_PATH = $payload
    $env:CHUMMER_INSTALLER_PAYLOAD_SHA256 = $PayloadSha256
    $env:CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES = [string]$payloadSize
    $script:installerProcess = Start-Process -FilePath $installer -PassThru
    $script:installerProcessId = $script:installerProcess.Id
    Wait-TraceMarker -Marker "Extracting application files"
    $progressWindow = Wait-ReviewableMainWindow -Phase "progress"
    Save-WindowPng -WindowObservation $progressWindow -OutputPath $ProgressScreenshot
    Wait-TraceMarker -Marker "Install complete"
    $completionWindow = Wait-ReviewableMainWindow -Phase "completion"
    Save-WindowPng -WindowObservation $completionWindow -OutputPath $CompletionScreenshot
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
