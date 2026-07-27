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
$TraceObservationPollMilliseconds = 5
$ProgressFreezeTimeoutSeconds = 15
$ThreadSuspendResumeAccess = [uint32]0x0002
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
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenThread(uint desiredAccess, bool inheritHandle, uint threadId);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SuspendThread(IntPtr threadHandle);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint ResumeThread(IntPtr threadHandle);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);
}
"@

function Read-InstallerTrace {
    if (-not (Test-Path -LiteralPath $tracePath -PathType Leaf)) {
        return $null
    }
    return Get-Content -LiteralPath $tracePath -Raw -ErrorAction SilentlyContinue
}

function Test-TraceHasExactLine {
    param(
        [AllowNull()][string]$Trace,
        [Parameter(Mandatory = $true)][string]$Marker
    )
    if (-not $Trace) {
        return $false
    }
    return @($Trace -split "\r\n|\n|\r") -ccontains $Marker
}

function Wait-TraceMarker {
    param([string]$Marker, [int]$TimeoutSeconds = 300)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $trace = Read-InstallerTrace
        if (Test-TraceHasExactLine -Trace $trace -Marker $Marker) { return }
        if ($script:installerProcess.HasExited) {
            throw "$Head installer exited before trace marker '$Marker'."
        }
        Start-Sleep -Milliseconds $TraceObservationPollMilliseconds
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
    $windowOwnerThreadId = [uint32]0
    if ($isWindow) {
        $windowOwnerThreadId = [ChummerNativeWindowCapture]::GetWindowThreadProcessId(
            $windowHandle,
            [ref]$windowOwnerProcessId
        )
    }
    $belongsToInstallerProcess = (
        $isWindow -and
        $windowOwnerThreadId -ne [uint32]0 -and
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
        WindowOwnerThreadId = $windowOwnerThreadId
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

function Close-InstallerWindowThreadFreezeTarget {
    param([Parameter(Mandatory = $true)][object]$Target)
    if ($Target.HandleClosed) {
        return
    }
    if ($Target.OwnedSuspendCount -ne 0) {
        throw "$Head refused to close the installer window thread handle while an owned suspend count remains."
    }
    if (-not [ChummerNativeWindowCapture]::CloseHandle($Target.ThreadHandle)) {
        $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "$Head could not close the installer window thread handle; win32Error=$errorCode."
    }
    $Target.HandleClosed = $true
}

function Resume-InstallerWindowThread {
    param([Parameter(Mandatory = $true)][object]$Target)
    if ($Target.OwnedSuspendCount -eq 0) {
        return
    }
    if ($Target.OwnedSuspendCount -ne 1) {
        $Target.ResumeContractFailed = $true
        throw "$Head installer window thread has an invalid owned suspend count."
    }

    $previousSuspendCount = [ChummerNativeWindowCapture]::ResumeThread(
        $Target.ThreadHandle
    )
    if ($previousSuspendCount -eq [uint32]::MaxValue) {
        $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        $Target.ResumeContractFailed = $true
        throw "$Head could not resume the installer window thread; win32Error=$errorCode."
    }
    $Target.OwnedSuspendCount = 0
    if ($previousSuspendCount -ne [uint32]1) {
        $Target.ResumeContractFailed = $true
        Close-InstallerWindowThreadFreezeTarget -Target $Target
        throw "$Head installer window thread resume count was not exactly one; previousSuspendCount=$previousSuspendCount."
    }
    Close-InstallerWindowThreadFreezeTarget -Target $Target
}

function Suspend-InstallerWindowThread {
    param([Parameter(Mandatory = $true)][object]$Target)
    if ($Target.HandleClosed -or $Target.OwnedSuspendCount -ne 0) {
        throw "$Head installer window thread freeze target is not available for one owned suspension."
    }

    $previousSuspendCount = [ChummerNativeWindowCapture]::SuspendThread(
        $Target.ThreadHandle
    )
    if ($previousSuspendCount -eq [uint32]::MaxValue) {
        $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "$Head could not suspend the installer window thread; win32Error=$errorCode."
    }
    $Target.OwnedSuspendCount = 1
    if ($previousSuspendCount -ne [uint32]0) {
        $Target.ResumeContractFailed = $true
        $undoSuspendCount = [ChummerNativeWindowCapture]::ResumeThread(
            $Target.ThreadHandle
        )
        if ($undoSuspendCount -ne ($previousSuspendCount + [uint32]1)) {
            throw "$Head could not safely unwind a pre-suspended installer window thread; previousSuspendCount=$previousSuspendCount undoSuspendCount=$undoSuspendCount."
        }
        $Target.OwnedSuspendCount = 0
        Close-InstallerWindowThreadFreezeTarget -Target $Target
        throw "$Head refused to capture from a pre-suspended installer window thread; previousSuspendCount=$previousSuspendCount."
    }
}

function Wait-InstallerWindowThreadFreezeTarget {
    param([int]$TimeoutSeconds = 60)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $latestObservation = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($script:installerProcess.HasExited) {
            $latest = Format-WindowObservation -Observation $latestObservation
            throw "$Head installer exited before its accountable window thread could be bound; latest observation $latest."
        }
        $current = Get-MainWindowObservation
        $latestObservation = $current
        $reviewableTarget = (
            $null -ne $current -and
            $current.IsWindow -and
            $current.IsVisible -and
            -not $current.IsMinimized -and
            $current.BelongsToInstallerProcess -and
            $current.BoundsAvailable -and
            $current.Width -ge $MinimumReviewWidth -and
            $current.Height -ge $MinimumReviewHeight
        )
        if ($reviewableTarget) {
            $threadHandle = [ChummerNativeWindowCapture]::OpenThread(
                $ThreadSuspendResumeAccess,
                $false,
                [uint32]$current.WindowOwnerThreadId
            )
            if ($threadHandle -eq [IntPtr]::Zero) {
                $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
                throw "$Head could not open the accountable installer window thread; threadId=$($current.WindowOwnerThreadId) win32Error=$errorCode."
            }
            return [pscustomobject]@{
                WindowHandle = $current.WindowHandle
                WindowHandleValue = $current.HandleValue
                ProcessId = [uint32]$script:installerProcessId
                ThreadId = [uint32]$current.WindowOwnerThreadId
                ThreadHandle = $threadHandle
                OwnedSuspendCount = 0
                HandleClosed = $false
                ResumeContractFailed = $false
            }
        }
        Start-Sleep -Milliseconds $TraceObservationPollMilliseconds
    }
    $latest = Format-WindowObservation -Observation $latestObservation
    throw "$Head installer did not expose an accountable window thread within $TimeoutSeconds seconds; latest observation $latest."
}

function Assert-InstallerWindowThreadFreezeTargetBinding {
    param([Parameter(Mandatory = $true)][object]$Target)
    if (
        $Target.HandleClosed -or
        -not [ChummerNativeWindowCapture]::IsWindow(
            [IntPtr]$Target.WindowHandle
        )
    ) {
        throw "$Head installer window thread freeze target is no longer a native window."
    }
    $observedProcessId = [uint32]0
    $observedThreadId = [ChummerNativeWindowCapture]::GetWindowThreadProcessId(
        [IntPtr]$Target.WindowHandle,
        [ref]$observedProcessId
    )
    if (
        $observedProcessId -ne [uint32]$Target.ProcessId -or
        $observedProcessId -ne [uint32]$script:installerProcessId -or
        $observedThreadId -ne [uint32]$Target.ThreadId
    ) {
        throw "$Head installer window thread ownership changed before the progress freeze."
    }
    $script:installerProcess.Refresh()
    if (
        $script:installerProcess.HasExited -or
        $script:installerProcess.MainWindowHandle -ne
            [IntPtr]$Target.WindowHandle
    ) {
        throw "$Head installer main window changed before the progress freeze."
    }
}

function Assert-FrozenInstallerTracePreCompletion {
    param(
        [Parameter(Mandatory = $true)][string]$Marker,
        [Parameter(Mandatory = $true)][string]$CompletionMarker
    )
    $frozenTrace = Read-InstallerTrace
    if (-not (
        Test-TraceHasExactLine -Trace $frozenTrace -Marker $Marker
    )) {
        throw "$Head extraction marker disappeared while the progress frame was frozen."
    }
    if (
        Test-TraceHasExactLine `
            -Trace $frozenTrace `
            -Marker $CompletionMarker
    ) {
        throw "$Head installer reached completion while the progress frame was frozen."
    }
}

function Wait-TraceMarkerAndSuspendInstallerWindowThread {
    param(
        [Parameter(Mandatory = $true)][string]$Marker,
        [Parameter(Mandatory = $true)][string]$CompletionMarker,
        [Parameter(Mandatory = $true)][object]$Target,
        [int]$TimeoutSeconds = 300
    )
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        Assert-InstallerWindowThreadFreezeTargetBinding -Target $Target
        $trace = Read-InstallerTrace
        if (Test-TraceHasExactLine -Trace $trace -Marker $Marker) {
            Suspend-InstallerWindowThread -Target $Target
            try {
                Assert-FrozenInstallerTracePreCompletion `
                    -Marker $Marker `
                    -CompletionMarker $CompletionMarker
                return
            } catch {
                $freezeError = $_
                try {
                    Resume-InstallerWindowThread -Target $Target
                } catch {
                    $Target.ResumeContractFailed = $true
                    throw "$Head progress freeze failed and its owned suspension could not be released: $($_.Exception.Message)"
                }
                throw $freezeError
            }
        }
        if ($script:installerProcess.HasExited) {
            throw "$Head installer exited before trace marker '$Marker'."
        }
        Start-Sleep -Milliseconds $TraceObservationPollMilliseconds
    }
    throw "$Head installer timed out before trace marker '$Marker'."
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
$script:progressFreezeTarget = $null
$script:progressFreezeReleaseFailed = $false
try {
    $env:CHUMMER_INSTALLER_PAYLOAD_PATH = $payload
    $env:CHUMMER_INSTALLER_PAYLOAD_SHA256 = $PayloadSha256
    $env:CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES = [string]$payloadSize
    $script:installerProcess = Start-Process -FilePath $installer -PassThru
    $script:installerProcessId = $script:installerProcess.Id
    $script:progressFreezeTarget = Wait-InstallerWindowThreadFreezeTarget
    Wait-TraceMarkerAndSuspendInstallerWindowThread `
        -Marker "Extracting application files" `
        -CompletionMarker "Install complete" `
        -Target $script:progressFreezeTarget
    try {
        $progressWindow = Wait-ReviewableMainWindow `
            -Phase "progress" `
            -TimeoutSeconds $ProgressFreezeTimeoutSeconds
        Save-WindowPng `
            -WindowObservation $progressWindow `
            -OutputPath $ProgressScreenshot
        $progressScreenshotSha256 = (
            Get-FileHash -LiteralPath $ProgressScreenshot -Algorithm SHA256
        ).Hash
        Assert-FrozenInstallerTracePreCompletion `
            -Marker "Extracting application files" `
            -CompletionMarker "Install complete"
    } finally {
        try {
            Resume-InstallerWindowThread `
                -Target $script:progressFreezeTarget
        } catch {
            $script:progressFreezeReleaseFailed = $true
            throw
        }
    }
    $script:progressFreezeTarget = $null
    Wait-TraceMarker -Marker "Install complete"
    $completionWindow = Wait-ReviewableMainWindow -Phase "completion"
    Save-WindowPng -WindowObservation $completionWindow -OutputPath $CompletionScreenshot
    $completionScreenshotSha256 = (
        Get-FileHash -LiteralPath $CompletionScreenshot -Algorithm SHA256
    ).Hash
    if ($progressScreenshotSha256 -ceq $completionScreenshotSha256) {
        throw "$Head progress and completion screenshots are digest-identical."
    }
} finally {
    $env:CHUMMER_INSTALLER_PAYLOAD_PATH = $priorPayloadPath
    $env:CHUMMER_INSTALLER_PAYLOAD_SHA256 = $priorPayloadSha
    $env:CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES = $priorPayloadSize
    $threadCleanupError = $null
    if (
        $script:progressFreezeTarget -and
        $script:progressFreezeTarget.OwnedSuspendCount -ne 0
    ) {
        try {
            Resume-InstallerWindowThread `
                -Target $script:progressFreezeTarget
        } catch {
            $script:progressFreezeReleaseFailed = $true
            $threadCleanupError = $_
        }
    }
    if ($script:installerProcess -and -not $script:installerProcess.HasExited) {
        if (
            $script:progressFreezeReleaseFailed -or
            (
                $script:progressFreezeTarget -and
                $script:progressFreezeTarget.ResumeContractFailed
            )
        ) {
            $script:installerProcess.Kill()
            $script:installerProcess.WaitForExit(5000) | Out-Null
        } else {
            $script:installerProcess.CloseMainWindow() | Out-Null
            if (-not $script:installerProcess.WaitForExit(5000)) {
                $script:installerProcess.Kill()
            }
        }
    }
    if (
        $script:progressFreezeTarget -and
        -not $script:progressFreezeTarget.HandleClosed
    ) {
        if (
            $script:progressFreezeTarget.OwnedSuspendCount -ne 0 -and
            $script:installerProcess.HasExited
        ) {
            $script:progressFreezeTarget.OwnedSuspendCount = 0
        }
        try {
            Close-InstallerWindowThreadFreezeTarget `
                -Target $script:progressFreezeTarget
        } catch {
            if (-not $threadCleanupError) {
                $threadCleanupError = $_
            }
        }
    }
    if ($threadCleanupError) {
        throw "$Head could not release the installer progress freeze safely: $($threadCleanupError.Exception.Message)"
    }
}
