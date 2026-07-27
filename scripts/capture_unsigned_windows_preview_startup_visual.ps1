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
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
public static class ChummerUnsignedPreviewStartupCapture {
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int count);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint command);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags
    );
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        out RECT rect,
        uint update
    );
    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(
        IntPtr hWnd,
        uint attribute,
        out RECT value,
        int valueSize
    );
    [DllImport("dwmapi.dll")] public static extern int DwmFlush();

    public static IntPtr[] EnumerateTopLevelWindows() {
        var windows = new List<IntPtr>();
        EnumWindowsProc callback = delegate(IntPtr hWnd, IntPtr lParam) {
            windows.Add(hWnd);
            return true;
        };
        if (!EnumWindows(callback, IntPtr.Zero)) {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not enumerate native top-level windows."
            );
        }
        GC.KeepAlive(callback);
        return windows.ToArray();
    }

    public static string WindowTitle(IntPtr hWnd) {
        int length = GetWindowTextLength(hWnd);
        if (length < 0 || length > 4096) {
            throw new InvalidOperationException("Native window title length is invalid.");
        }
        var value = new StringBuilder(length + 1);
        int copied = GetWindowText(hWnd, value, value.Capacity);
        if (copied < 0 || copied > length) {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not read the native window title."
            );
        }
        return value.ToString();
    }

    public static string WindowClass(IntPtr hWnd) {
        var value = new StringBuilder(257);
        int copied = GetClassName(hWnd, value, value.Capacity);
        if (copied < 1 || copied > 256) {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not read the native window class."
            );
        }
        return value.ToString();
    }

    public static bool TryGetClientBoundsInScreen(IntPtr hWnd, out RECT bounds) {
        bounds = new RECT();
        RECT client;
        if (!GetClientRect(hWnd, out client)) {
            return false;
        }
        var topLeft = new POINT { X = client.Left, Y = client.Top };
        var bottomRight = new POINT { X = client.Right, Y = client.Bottom };
        if (!ClientToScreen(hWnd, ref topLeft)
                || !ClientToScreen(hWnd, ref bottomRight)) {
            return false;
        }
        bounds.Left = topLeft.X;
        bounds.Top = topLeft.Y;
        bounds.Right = bottomRight.X;
        bounds.Bottom = bottomRight.Y;
        return true;
    }
}
"@

$ExpectedPrePromptStartupWindowTitle = 'Chummer Desktop Classic'
$ExpectedStartupWindowTitle = 'Claim your copy'
$ExpectedInstallLinkingPromptTitle = 'Claim your copy'
$RejectedConsoleWindowClass = 'ConsoleWindowClass'
$ExpectedAvaloniaWindowClassPattern = (
    '^Avalonia-[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-' +
    '[0-9a-f]{4}-[0-9a-f]{12}$'
)
$MinimumReviewWidth = 800
$MinimumReviewHeight = 500
$MinimumReviewClientWidth = 760
$MinimumReviewClientHeight = 420
$MinimumInstallLinkingPromptWidth = 760
$MinimumInstallLinkingPromptHeight = 520
$RequiredStableObservationCount = 3
$RequiredStablePromptObservationCount = 2
$RequiredPostPromptForegroundObservationCount = 20
$RequiredStableRenderedFrameCount = 2
$WindowObservationPollMilliseconds = 100
$PromptObservationPollMilliseconds = 25
$MinimumPostPromptHandoffSettleMilliseconds = 10000
$PostPromptQuiescenceTimeoutSeconds = 45
$RenderedFramePollMilliseconds = 250
$StartupWindowTimeoutSeconds = 90
$DwmExtendedFrameBounds = [uint32]9
$SpiGetWorkArea = [uint32]48
$SwRestore = 9
$SwpNoZOrder = [uint32]0x0004
$SwpNoActivate = [uint32]0x0010
$SwpShowWindow = [uint32]0x0040
$WindowPlacementMargin = 20
$GwOwner = [uint32]4
$GaRoot = [uint32]2
$GaRootOwner = [uint32]3
$ExpectedDarkClientPalette = @(
    [int]0x050B16,
    [int]0x111827,
    [int]0x162033,
    [int]0x0F172A,
    [int]0x020617,
    [int]0x172554,
    [int]0x0B1220,
    [int]0x1C4A2D
)
$MinimumExpectedPaletteFraction = 0.20
$MinimumExpectedPaletteColors = 3
$ExpectedPaletteChannelTolerance = 6

function Get-StartupWindowObservations {
    try {
        $script:startupProcess.Refresh()
        if (
            $script:startupProcess.HasExited -or
            [uint32]$script:startupProcess.Id -ne
                [uint32]$script:startupProcessId
        ) {
            return @()
        }
    } catch [System.InvalidOperationException] {
        return @()
    }

    $observations = @()
    foreach (
        $windowHandle in
            [ChummerUnsignedPreviewStartupCapture]::EnumerateTopLevelWindows()
    ) {
        if (
            $windowHandle -eq [IntPtr]::Zero -or
            -not [ChummerUnsignedPreviewStartupCapture]::IsWindow(
                $windowHandle
            )
        ) {
            continue
        }
        $ownerProcessId = [uint32]0
        $ownerThreadId = (
            [ChummerUnsignedPreviewStartupCapture]::GetWindowThreadProcessId(
                $windowHandle,
                [ref]$ownerProcessId
            )
        )
        if (
            $ownerThreadId -eq [uint32]0 -or
            $ownerProcessId -ne [uint32]$script:startupProcessId
        ) {
            continue
        }

        $title = (
            [ChummerUnsignedPreviewStartupCapture]::WindowTitle(
                $windowHandle
            )
        )
        $className = (
            [ChummerUnsignedPreviewStartupCapture]::WindowClass(
                $windowHandle
            )
        )
        $ownerHandle = (
            [ChummerUnsignedPreviewStartupCapture]::GetWindow(
                $windowHandle,
                $GwOwner
            )
        )
        $rootHandle = (
            [ChummerUnsignedPreviewStartupCapture]::GetAncestor(
                $windowHandle,
                $GaRoot
            )
        )
        $rootOwnerHandle = (
            [ChummerUnsignedPreviewStartupCapture]::GetAncestor(
                $windowHandle,
                $GaRootOwner
            )
        )
        $rect = New-Object ChummerUnsignedPreviewStartupCapture+RECT
        $boundsAvailable = (
            [ChummerUnsignedPreviewStartupCapture]::GetWindowRect(
                $windowHandle,
                [ref]$rect
            )
        )
        $clientRect = New-Object ChummerUnsignedPreviewStartupCapture+RECT
        $clientBoundsAvailable = (
            [ChummerUnsignedPreviewStartupCapture]::TryGetClientBoundsInScreen(
                $windowHandle,
                [ref]$clientRect
            )
        )
        $extendedRect = New-Object ChummerUnsignedPreviewStartupCapture+RECT
        $extendedBoundsAvailable = (
            [ChummerUnsignedPreviewStartupCapture]::DwmGetWindowAttribute(
                $windowHandle,
                $DwmExtendedFrameBounds,
                [ref]$extendedRect,
                [Runtime.InteropServices.Marshal]::SizeOf($extendedRect)
            ) -eq 0
        )
        $verifiedOwnerProcessId = [uint32]0
        $verifiedOwnerThreadId = (
            [ChummerUnsignedPreviewStartupCapture]::GetWindowThreadProcessId(
                $windowHandle,
                [ref]$verifiedOwnerProcessId
            )
        )
        if (
            -not [ChummerUnsignedPreviewStartupCapture]::IsWindow(
                $windowHandle
            ) -or
            $verifiedOwnerProcessId -ne
                [uint32]$script:startupProcessId -or
            $verifiedOwnerProcessId -ne $ownerProcessId -or
            $verifiedOwnerThreadId -ne $ownerThreadId -or
            $verifiedOwnerThreadId -eq [uint32]0 -or
            [ChummerUnsignedPreviewStartupCapture]::GetWindow(
                $windowHandle,
                $GwOwner
            ) -ne $ownerHandle -or
            [ChummerUnsignedPreviewStartupCapture]::GetAncestor(
                $windowHandle,
                $GaRoot
            ) -ne $rootHandle -or
            [ChummerUnsignedPreviewStartupCapture]::GetAncestor(
                $windowHandle,
                $GaRootOwner
            ) -ne $rootOwnerHandle
        ) {
            continue
        }
        $observations += [pscustomobject]@{
            WindowHandle = $windowHandle
            HandleValue = $windowHandle.ToInt64()
            HandleText = "0x{0:X}" -f $windowHandle.ToInt64()
            ProcessId = $verifiedOwnerProcessId
            ThreadId = $verifiedOwnerThreadId
            Title = $title
            ClassName = $className
            OwnerHandleValue = $ownerHandle.ToInt64()
            RootHandleValue = $rootHandle.ToInt64()
            RootOwnerHandleValue = $rootOwnerHandle.ToInt64()
            IsVisible = (
                [ChummerUnsignedPreviewStartupCapture]::IsWindowVisible(
                    $windowHandle
                )
            )
            IsMinimized = (
                [ChummerUnsignedPreviewStartupCapture]::IsIconic(
                    $windowHandle
                )
            )
            BoundsAvailable = $boundsAvailable
            Left = $rect.Left
            Top = $rect.Top
            Right = $rect.Right
            Bottom = $rect.Bottom
            Width = $rect.Right - $rect.Left
            Height = $rect.Bottom - $rect.Top
            ClientBoundsAvailable = $clientBoundsAvailable
            ClientLeft = $clientRect.Left
            ClientTop = $clientRect.Top
            ClientRight = $clientRect.Right
            ClientBottom = $clientRect.Bottom
            ClientWidth = $clientRect.Right - $clientRect.Left
            ClientHeight = $clientRect.Bottom - $clientRect.Top
            ExtendedBoundsAvailable = $extendedBoundsAvailable
            ExtendedLeft = $extendedRect.Left
            ExtendedTop = $extendedRect.Top
            ExtendedRight = $extendedRect.Right
            ExtendedBottom = $extendedRect.Bottom
            ExtendedWidth = $extendedRect.Right - $extendedRect.Left
            ExtendedHeight = $extendedRect.Bottom - $extendedRect.Top
        }
    }
    return @($observations)
}

function Get-VisibleStartupProcessWindows {
    param([AllowNull()][object[]]$Observations)
    return @(
        @($Observations) |
            Where-Object {
                $_.ProcessId -eq [uint32]$script:startupProcessId -and
                $_.ThreadId -ne [uint32]0 -and
                $_.IsVisible -and
                -not $_.IsMinimized -and
                $_.BoundsAvailable -and
                $_.Width -gt 0 -and
                $_.Height -gt 0
            }
    )
}

function Select-UniqueReviewableStartupWindow {
    param(
        [AllowNull()][object[]]$Observations,
        [Parameter(Mandatory = $true)][string]$Phase
    )
    $visibleProcessWindows = @(
        Get-VisibleStartupProcessWindows -Observations $Observations
    )
    if ($visibleProcessWindows.Count -gt 1) {
        throw "Installed application exposed ambiguous visible $Phase windows; count=$($visibleProcessWindows.Count)."
    }
    $matching = @(
        $visibleProcessWindows |
            Where-Object {
                $_.Title -ceq $ExpectedStartupWindowTitle -and
                -not [string]::IsNullOrWhiteSpace($_.ClassName) -and
                $_.ClassName -cne $RejectedConsoleWindowClass -and
                $_.ClassName -cmatch
                    $ExpectedAvaloniaWindowClassPattern -and
                $_.OwnerHandleValue -eq 0 -and
                $_.RootHandleValue -eq $_.HandleValue -and
                $_.RootOwnerHandleValue -eq $_.HandleValue -and
                $_.Width -ge $MinimumReviewWidth -and
                $_.Height -ge $MinimumReviewHeight -and
                $_.ClientBoundsAvailable -and
                $_.ClientWidth -ge $MinimumReviewClientWidth -and
                $_.ClientHeight -ge $MinimumReviewClientHeight -and
                $_.ClientLeft -ge $_.Left -and
                $_.ClientTop -ge $_.Top -and
                $_.ClientRight -le $_.Right -and
                $_.ClientBottom -le $_.Bottom
            }
    )
    if ($matching.Count -gt 1) {
        throw "Installed application exposed ambiguous reviewable $Phase windows; count=$($matching.Count)."
    }
    if ($matching.Count -eq 0) {
        return $null
    }
    return $matching[0]
}

function Test-SameStartupWindowHandleIdentity {
    param(
        [Parameter(Mandatory = $true)][object]$Expected,
        [Parameter(Mandatory = $true)][object]$Observed
    )
    return (
        $Observed.HandleValue -eq $Expected.HandleValue -and
        $Observed.ProcessId -eq $Expected.ProcessId -and
        $Observed.ThreadId -eq $Expected.ThreadId -and
        $Observed.ClassName -ceq $Expected.ClassName -and
        $Observed.OwnerHandleValue -eq $Expected.OwnerHandleValue -and
        $Observed.RootHandleValue -eq $Expected.RootHandleValue -and
        $Observed.RootOwnerHandleValue -eq
            $Expected.RootOwnerHandleValue
    )
}

function Test-SameStartupWindowIdentity {
    param(
        [Parameter(Mandatory = $true)][object]$Expected,
        [Parameter(Mandatory = $true)][object]$Observed
    )
    return (
        $Observed.Title -ceq $Expected.Title -and
        (Test-SameStartupWindowHandleIdentity `
            -Expected $Expected `
            -Observed $Observed)
    )
}

function Get-InstallLinkingPromptDismissAction {
    param([Parameter(Mandatory = $true)][object]$Observation)
    try {
        $root = (
            [System.Windows.Automation.AutomationElement]::FromHandle(
                $Observation.WindowHandle
            )
        )
        if (
            $null -eq $root -or
            $root.Current.ProcessId -ne [int]$script:startupProcessId -or
            $root.Current.Name -cne $ExpectedInstallLinkingPromptTitle -or
            $root.Current.FrameworkId -cne 'Avalonia' -or
            $root.Current.ControlType -ne
                [System.Windows.Automation.ControlType]::Window -or
            $root.Current.IsOffscreen -or
            -not $root.Current.IsEnabled
        ) {
            return $null
        }
        $descendants = $root.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition
        )
        if (
            $null -eq $descendants -or
            $descendants.Count -lt 3 -or
            $descendants.Count -gt 4096
        ) {
            return $null
        }
        $matchingButtons = @()
        for ($index = 0; $index -lt $descendants.Count; $index += 1) {
            try {
                $element = $descendants.Item($index)
                if (
                    $element.Current.ProcessId -ne
                        [int]$script:startupProcessId -or
                    $element.Current.FrameworkId -cne 'Avalonia' -or
                    $element.Current.ControlType -ne
                        [System.Windows.Automation.ControlType]::Button -or
                    $element.Current.Name -cne 'Continue unlinked' -or
                    $element.Current.IsOffscreen -or
                    -not $element.Current.IsEnabled
                ) {
                    continue
                }
                $bounds = $element.Current.BoundingRectangle
                if (
                    $bounds.IsEmpty -or
                    $bounds.Width -lt 80 -or
                    $bounds.Height -lt 20 -or
                    $bounds.Left -lt $Observation.ClientLeft -or
                    $bounds.Top -lt $Observation.ClientTop -or
                    $bounds.Right -gt $Observation.ClientRight -or
                    $bounds.Bottom -gt $Observation.ClientBottom
                ) {
                    continue
                }
                $patternObject = $null
                if (
                    -not $element.TryGetCurrentPattern(
                        [System.Windows.Automation.InvokePattern]::Pattern,
                        [ref]$patternObject
                    ) -or
                    $null -eq $patternObject
                ) {
                    continue
                }
                $matchingButtons += [pscustomobject]@{
                    Element = $element
                    InvokePattern = (
                        [System.Windows.Automation.InvokePattern]$patternObject
                    )
                }
            } catch [System.Windows.Automation.ElementNotAvailableException] {
                return $null
            }
        }
        if ($matchingButtons.Count -ne 1) {
            return $null
        }
        return $matchingButtons[0]
    } catch [System.Windows.Automation.ElementNotAvailableException] {
        return $null
    } catch [System.InvalidOperationException] {
        return $null
    } catch [System.Runtime.InteropServices.COMException] {
        return $null
    }
}

function Dismiss-AuthenticatedInstallLinkingPrompt {
    $deadline = [DateTime]::UtcNow.AddSeconds(
        $StartupWindowTimeoutSeconds
    )
    $stableMain = $null
    $stablePrompt = $null
    $stableCount = 0
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($script:startupProcess.HasExited) {
            throw 'Installed application exited before its install-linking prompt was authenticated.'
        }
        $observations = @(Get-StartupWindowObservations)
        $visible = @(
            Get-VisibleStartupProcessWindows -Observations $observations
        )
        if ($visible.Count -gt 2) {
            throw "Installed application exposed unexpected startup windows; count=$($visible.Count)."
        }
        $mainMatches = @(
            $visible |
                Where-Object {
                    $_.Title -ceq
                        $ExpectedPrePromptStartupWindowTitle -and
                    $_.ClassName -cmatch
                        $ExpectedAvaloniaWindowClassPattern -and
                    $_.OwnerHandleValue -eq 0 -and
                    $_.RootHandleValue -eq $_.HandleValue -and
                    $_.RootOwnerHandleValue -eq $_.HandleValue -and
                    $_.Width -ge $MinimumReviewWidth -and
                    $_.Height -ge $MinimumReviewHeight -and
                    $_.ClientBoundsAvailable
                }
        )
        if ($mainMatches.Count -gt 1) {
            throw 'Installed application exposed ambiguous main startup windows.'
        }
        $main = if ($mainMatches.Count -eq 1) {
            $mainMatches[0]
        } else {
            $null
        }
        $promptMatches = @(
            $visible |
                Where-Object {
                    $null -ne $main -and
                    $_.Title -ceq
                        $ExpectedInstallLinkingPromptTitle -and
                    $_.ClassName -cmatch
                        $ExpectedAvaloniaWindowClassPattern -and
                    $_.OwnerHandleValue -eq $main.HandleValue -and
                    $_.RootHandleValue -eq $_.HandleValue -and
                    $_.RootOwnerHandleValue -eq $main.HandleValue -and
                    $_.Width -ge $MinimumInstallLinkingPromptWidth -and
                    $_.Height -ge $MinimumInstallLinkingPromptHeight -and
                    $_.ClientBoundsAvailable -and
                    $_.ClientWidth -gt 0 -and
                    $_.ClientHeight -gt 0
                }
        )
        if ($promptMatches.Count -gt 1) {
            throw 'Installed application exposed ambiguous install-linking prompts.'
        }
        $prompt = if ($promptMatches.Count -eq 1) {
            $promptMatches[0]
        } else {
            $null
        }
        $same = (
            $visible.Count -eq 2 -and
            $null -ne $main -and
            $null -ne $prompt -and
            $null -ne $stableMain -and
            $null -ne $stablePrompt -and
            (Test-SameStartupWindowIdentity `
                -Expected $stableMain `
                -Observed $main) -and
            (Test-SameStartupWindowIdentity `
                -Expected $stablePrompt `
                -Observed $prompt)
        )
        if ($visible.Count -eq 2 -and
            $null -ne $main -and
            $null -ne $prompt) {
            if ($same) {
                $stableCount += 1
            } else {
                $stableMain = $main
                $stablePrompt = $prompt
                $stableCount = 1
            }
        } else {
            $stableMain = $null
            $stablePrompt = $null
            $stableCount = 0
        }
        if ($stableCount -ge $RequiredStablePromptObservationCount) {
            $dismissAction = (
                Get-InstallLinkingPromptDismissAction `
                    -Observation $prompt
            )
            if ($null -eq $dismissAction) {
                $stableCount = 0
                Start-Sleep -Milliseconds $PromptObservationPollMilliseconds
                continue
            }
            $dismissAction.InvokePattern.Invoke()
            $dismissDeadline = [DateTime]::UtcNow.AddSeconds(15)
            $dismissedStableCount = 0
            while ([DateTime]::UtcNow -lt $dismissDeadline) {
                $postDismissVisible = @(
                    Get-VisibleStartupProcessWindows `
                        -Observations @(Get-StartupWindowObservations)
                )
                $postDismissMain = @(
                    $postDismissVisible |
                        Where-Object {
                            $_.Title -ceq $ExpectedStartupWindowTitle -and
                            (Test-SameStartupWindowHandleIdentity `
                                -Expected $main `
                                -Observed $_)
                        }
                )
                if (
                    $postDismissVisible.Count -eq 1 -and
                    $postDismissMain.Count -eq 1
                ) {
                    $dismissedStableCount += 1
                    if (
                        $dismissedStableCount -ge
                            $RequiredStableObservationCount
                    ) {
                        return $postDismissMain[0]
                    }
                } else {
                    $dismissedStableCount = 0
                }
                Start-Sleep -Milliseconds $WindowObservationPollMilliseconds
            }
            throw 'Authenticated install-linking prompt did not close cleanly.'
        }
        Start-Sleep -Milliseconds $PromptObservationPollMilliseconds
    }
    throw 'Installed application did not expose its authenticated install-linking prompt.'
}

function Wait-AuthenticatedPostPromptQuiescence {
    param([Parameter(Mandatory = $true)][object]$Expected)
    $minimumSettleAt = [DateTime]::UtcNow.AddMilliseconds(
        $MinimumPostPromptHandoffSettleMilliseconds
    )
    $deadline = [DateTime]::UtcNow.AddSeconds(
        $PostPromptQuiescenceTimeoutSeconds
    )
    $stable = $null
    $stableCount = 0
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($script:startupProcess.HasExited) {
            throw 'Installed application exited during post-prompt quiescence.'
        }
        $visible = @(
            Get-VisibleStartupProcessWindows `
                -Observations @(Get-StartupWindowObservations)
        )
        if ($visible.Count -gt 1) {
            throw "Installed application exposed unexpected post-prompt windows; count=$($visible.Count)."
        }
        $matching = @(
            $visible |
                Where-Object {
                    $_.Title -ceq $ExpectedStartupWindowTitle -and
                    (Test-SameStartupWindowHandleIdentity `
                        -Expected $Expected `
                        -Observed $_) -and
                    $_.OwnerHandleValue -eq 0 -and
                    $_.RootHandleValue -eq $_.HandleValue -and
                    $_.RootOwnerHandleValue -eq $_.HandleValue -and
                    $_.Width -ge $MinimumReviewWidth -and
                    $_.Height -ge $MinimumReviewHeight -and
                    $_.ClientBoundsAvailable -and
                    $_.ClientWidth -ge $MinimumReviewClientWidth -and
                    $_.ClientHeight -ge $MinimumReviewClientHeight
                }
        )
        if (
            [DateTime]::UtcNow -ge $minimumSettleAt -and
            $matching.Count -eq 1
        ) {
            $current = $matching[0]
            if (
                [ChummerUnsignedPreviewStartupCapture]::GetForegroundWindow() -ne
                    $current.WindowHandle
            ) {
                [ChummerUnsignedPreviewStartupCapture]::SetForegroundWindow(
                    $current.WindowHandle
                ) | Out-Null
                $stable = $null
                $stableCount = 0
            } else {
                $same = (
                    $null -ne $stable -and
                    (Test-SameStartupWindowIdentity `
                        -Expected $stable `
                        -Observed $current) -and
                    $current.Left -eq $stable.Left -and
                    $current.Top -eq $stable.Top -and
                    $current.Right -eq $stable.Right -and
                    $current.Bottom -eq $stable.Bottom
                )
                if ($same) {
                    $stableCount += 1
                } else {
                    $stable = $current
                    $stableCount = 1
                }
                if (
                    $stableCount -ge
                        $RequiredPostPromptForegroundObservationCount
                ) {
                    return $current
                }
            }
        } else {
            $stable = $null
            $stableCount = 0
        }
        Start-Sleep -Milliseconds $WindowObservationPollMilliseconds
    }
    throw 'Installed application did not reach authenticated post-prompt quiescence.'
}

function Wait-StableStartupWindow {
    param(
        [Parameter(Mandatory = $true)][string]$Phase,
        [int]$TimeoutSeconds = $StartupWindowTimeoutSeconds
    )
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $stable = $null
    $stableCount = 0
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($script:startupProcess.HasExited) {
            throw "Installed application exited before exposing a stable $Phase window."
        }
        $current = (
            Select-UniqueReviewableStartupWindow `
                -Observations @(Get-StartupWindowObservations) `
                -Phase $Phase
        )
        if ($null -ne $current) {
            $same = (
                $null -ne $stable -and
                (Test-SameStartupWindowIdentity `
                    -Expected $stable `
                    -Observed $current) -and
                $current.Left -eq $stable.Left -and
                $current.Top -eq $stable.Top -and
                $current.Right -eq $stable.Right -and
                $current.Bottom -eq $stable.Bottom
            )
            if ($same) {
                $stableCount += 1
            } else {
                $stable = $current
                $stableCount = 1
            }
            if ($stableCount -ge $RequiredStableObservationCount) {
                return $current
            }
        } else {
            $stable = $null
            $stableCount = 0
        }
        Start-Sleep -Milliseconds $WindowObservationPollMilliseconds
    }
    throw "Installed application did not expose a stable reviewable $Phase window."
}

function Get-StartupWorkArea {
    $workArea = New-Object ChummerUnsignedPreviewStartupCapture+RECT
    if (-not [ChummerUnsignedPreviewStartupCapture]::SystemParametersInfo(
            $SpiGetWorkArea,
            [uint32]0,
            [ref]$workArea,
            [uint32]0)) {
        $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "Could not resolve the Windows startup-capture work area; win32Error=$errorCode."
    }
    if (
        $workArea.Right - $workArea.Left -lt
            ($MinimumReviewWidth + 2 * $WindowPlacementMargin) -or
        $workArea.Bottom - $workArea.Top -lt
            ($MinimumReviewHeight + 2 * $WindowPlacementMargin)
    ) {
        throw 'Windows startup-capture work area is too small for review.'
    }
    return $workArea
}

function Test-ExtendedBoundsInsideWorkArea {
    param(
        [Parameter(Mandatory = $true)][object]$Observation,
        [Parameter(Mandatory = $true)][object]$WorkArea
    )
    return (
        $Observation.ExtendedBoundsAvailable -and
        $Observation.ExtendedWidth -ge $MinimumReviewWidth -and
        $Observation.ExtendedHeight -ge $MinimumReviewHeight -and
        $Observation.ExtendedLeft -ge $WorkArea.Left -and
        $Observation.ExtendedTop -ge $WorkArea.Top -and
        $Observation.ExtendedRight -le $WorkArea.Right -and
        $Observation.ExtendedBottom -le $WorkArea.Bottom -and
        $Observation.ClientBoundsAvailable -and
        $Observation.ClientWidth -ge $MinimumReviewClientWidth -and
        $Observation.ClientHeight -ge $MinimumReviewClientHeight -and
        $Observation.ClientLeft -ge $Observation.ExtendedLeft -and
        $Observation.ClientTop -ge $Observation.ExtendedTop -and
        $Observation.ClientRight -le $Observation.ExtendedRight -and
        $Observation.ClientBottom -le $Observation.ExtendedBottom
    )
}

function Place-StartupWindowForReview {
    param([Parameter(Mandatory = $true)][object]$Observation)
    $workArea = Get-StartupWorkArea
    [ChummerUnsignedPreviewStartupCapture]::ShowWindow(
        $Observation.WindowHandle,
        $SwRestore
    ) | Out-Null
    $availableWidth = (
        $workArea.Right - $workArea.Left - 2 * $WindowPlacementMargin
    )
    $availableHeight = (
        $workArea.Bottom - $workArea.Top - 2 * $WindowPlacementMargin
    )
    $targetWidth = [Math]::Min(1000, $availableWidth)
    $targetHeight = [Math]::Min(700, $availableHeight)
    if (
        $targetWidth -lt $MinimumReviewWidth -or
        $targetHeight -lt $MinimumReviewHeight
    ) {
        throw 'Windows startup-capture target bounds are too small.'
    }
    $positioned = (
        [ChummerUnsignedPreviewStartupCapture]::SetWindowPos(
            $Observation.WindowHandle,
            [IntPtr]::Zero,
            $workArea.Left + $WindowPlacementMargin,
            $workArea.Top + $WindowPlacementMargin,
            $targetWidth,
            $targetHeight,
            ($SwpNoZOrder -bor $SwpNoActivate -bor $SwpShowWindow)
        )
    )
    if (-not $positioned) {
        $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "Could not place the application window for review; win32Error=$errorCode."
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    $stable = $null
    $stableCount = 0
    while ([DateTime]::UtcNow -lt $deadline) {
        $current = (
            Select-UniqueReviewableStartupWindow `
                -Observations @(Get-StartupWindowObservations) `
                -Phase 'positioned startup'
        )
        if (
            $null -ne $current -and
            (Test-SameStartupWindowIdentity `
                -Expected $Observation `
                -Observed $current) -and
            (Test-ExtendedBoundsInsideWorkArea `
                -Observation $current `
                -WorkArea $workArea)
        ) {
            $sameBounds = (
                $null -ne $stable -and
                $current.ExtendedLeft -eq $stable.ExtendedLeft -and
                $current.ExtendedTop -eq $stable.ExtendedTop -and
                $current.ExtendedRight -eq $stable.ExtendedRight -and
                $current.ExtendedBottom -eq $stable.ExtendedBottom -and
                $current.ClientLeft -eq $stable.ClientLeft -and
                $current.ClientTop -eq $stable.ClientTop -and
                $current.ClientRight -eq $stable.ClientRight -and
                $current.ClientBottom -eq $stable.ClientBottom
            )
            if ($sameBounds) {
                $stableCount += 1
            } else {
                $stable = $current
                $stableCount = 1
            }
            if ($stableCount -ge $RequiredStableObservationCount) {
                return [pscustomobject]@{
                    Observation = $current
                    WorkArea = $workArea
                }
            }
        } else {
            $stable = $null
            $stableCount = 0
        }
        Start-Sleep -Milliseconds $WindowObservationPollMilliseconds
    }
    throw 'Installed application window did not remain fully inside the review work area.'
}

function Get-StartupAutomationEvidence {
    param([Parameter(Mandatory = $true)][object]$Observation)
    try {
        $root = (
            [System.Windows.Automation.AutomationElement]::FromHandle(
                $Observation.WindowHandle
            )
        )
        if (
            $null -eq $root -or
            $root.Current.ProcessId -ne [int]$script:startupProcessId -or
            $root.Current.Name -cne $ExpectedStartupWindowTitle -or
            $root.Current.FrameworkId -cne 'Avalonia' -or
            $root.Current.ControlType -ne
                [System.Windows.Automation.ControlType]::Window -or
            $root.Current.IsOffscreen
        ) {
            return $null
        }
        $descendants = $root.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition
        )
        if ($null -eq $descendants -or
            $descendants.Count -lt 3 -or
            $descendants.Count -gt 4096) {
            return $null
        }
        $visibleNames = New-Object 'System.Collections.Generic.HashSet[string]' (
            [StringComparer]::Ordinal
        )
        $openButtonBounds = $null
        $saveButtonBounds = $null
        $openLabelBounds = @()
        $saveLabelBounds = @()
        for ($index = 0; $index -lt $descendants.Count; $index += 1) {
            try {
                $element = $descendants.Item($index)
                if (
                    $element.Current.ProcessId -eq
                        [int]$script:startupProcessId -and
                    $element.Current.FrameworkId -ceq 'Avalonia' -and
                    -not $element.Current.IsOffscreen
                ) {
                    $bounds = $element.Current.BoundingRectangle
                    $boundsReady = (
                        -not $bounds.IsEmpty -and
                        $bounds.Width -ge 4 -and
                        $bounds.Height -ge 4 -and
                        $bounds.Left -ge $Observation.ClientLeft -and
                        $bounds.Top -ge $Observation.ClientTop -and
                        $bounds.Right -le $Observation.ClientRight -and
                        $bounds.Bottom -le $Observation.ClientBottom
                    )
                    $name = $element.Current.Name
                    if (-not [string]::IsNullOrWhiteSpace($name)) {
                        $visibleNames.Add($name) | Out-Null
                    }
                    $controlType = $element.Current.ControlType
                    if (
                        $controlType -eq
                            [System.Windows.Automation.ControlType]::Button -and
                        $boundsReady -and
                        $bounds.Width -ge 20 -and
                        $bounds.Height -ge 10
                    ) {
                        $automationId = $element.Current.AutomationId
                        if ($automationId -ceq 'ImportFileButton') {
                            $openButtonBounds = $bounds
                        }
                        if ($automationId -ceq 'SaveButton') {
                            $saveButtonBounds = $bounds
                        }
                    }
                    if (
                        $controlType -eq
                            [System.Windows.Automation.ControlType]::Text -and
                        $boundsReady
                    ) {
                        if ($name -ceq 'Open') {
                            $openLabelBounds += $bounds
                        }
                        if ($name -ceq 'Save') {
                            $saveLabelBounds += $bounds
                        }
                    }
                }
            } catch [System.Windows.Automation.ElementNotAvailableException] {
                return $null
            }
        }
        if ($null -eq $openButtonBounds -or $null -eq $saveButtonBounds) {
            return $null
        }
        $openLabelReady = $false
        foreach ($bounds in $openLabelBounds) {
            if (
                $bounds.Left -ge $openButtonBounds.Left -and
                $bounds.Top -ge $openButtonBounds.Top -and
                $bounds.Right -le $openButtonBounds.Right -and
                $bounds.Bottom -le $openButtonBounds.Bottom
            ) {
                $openLabelReady = $true
                break
            }
        }
        $saveLabelReady = $false
        foreach ($bounds in $saveLabelBounds) {
            if (
                $bounds.Left -ge $saveButtonBounds.Left -and
                $bounds.Top -ge $saveButtonBounds.Top -and
                $bounds.Right -le $saveButtonBounds.Right -and
                $bounds.Bottom -le $saveButtonBounds.Bottom
            ) {
                $saveLabelReady = $true
                break
            }
        }
        if (-not $openLabelReady -or -not $saveLabelReady) {
            return $null
        }
        return [pscustomobject]@{
            DescendantCount = $descendants.Count
            VisibleNamedDescendantCount = $visibleNames.Count
        }
    } catch [System.Windows.Automation.ElementNotAvailableException] {
        return $null
    } catch [System.InvalidOperationException] {
        return $null
    }
}

function New-StartupWindowBitmap {
    param([Parameter(Mandatory = $true)][object]$Observation)
    $width = $Observation.ExtendedWidth
    $height = $Observation.ExtendedHeight
    if ($width -lt $MinimumReviewWidth -or $height -lt $MinimumReviewHeight) {
        throw 'Installed application extended frame is too small for review.'
    }
    $bitmap = New-Object Drawing.Bitmap $width, $height
    try {
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen(
                $Observation.ExtendedLeft,
                $Observation.ExtendedTop,
                0,
                0,
                $bitmap.Size
            )
        } finally {
            $graphics.Dispose()
        }
        return $bitmap
    } catch {
        $bitmap.Dispose()
        throw
    }
}

function Get-BitmapSha256 {
    param([Parameter(Mandatory = $true)][Drawing.Bitmap]$Bitmap)
    $stream = New-Object IO.MemoryStream
    try {
        $Bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        $stream.Position = 0
        $hasher = [Security.Cryptography.SHA256]::Create()
        try {
            return [Convert]::ToHexString(
                $hasher.ComputeHash($stream)
            ).ToLowerInvariant()
        } finally {
            $hasher.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
}

function Test-RenderedStartupBitmap {
    param(
        [Parameter(Mandatory = $true)][Drawing.Bitmap]$Bitmap,
        [Parameter(Mandatory = $true)][object]$Observation
    )
    $clientLeft = $Observation.ClientLeft - $Observation.ExtendedLeft
    $clientTop = $Observation.ClientTop - $Observation.ExtendedTop
    $clientRight = $Observation.ClientRight - $Observation.ExtendedLeft
    $clientBottom = $Observation.ClientBottom - $Observation.ExtendedTop
    if (
        -not $Observation.ClientBoundsAvailable -or
        $clientLeft -lt 0 -or
        $clientTop -lt 0 -or
        $clientRight -gt $Bitmap.Width -or
        $clientBottom -gt $Bitmap.Height -or
        $clientRight - $clientLeft -lt $MinimumReviewClientWidth -or
        $clientBottom - $clientTop -lt $MinimumReviewClientHeight
    ) {
        return $false
    }
    $sampleCount = 0
    $nearBlack = 0
    $nearWhite = 0
    $colored = 0
    $expectedPaletteMatches = 0
    $matchedPaletteIndexes = New-Object 'System.Collections.Generic.HashSet[int]'
    $quantizedColors = New-Object 'System.Collections.Generic.HashSet[int]'
    for ($y = $clientTop; $y -lt $clientBottom; $y += 8) {
        for ($x = $clientLeft; $x -lt $clientRight; $x += 8) {
            $pixel = $Bitmap.GetPixel($x, $y)
            $sampleCount += 1
            $maximum = [Math]::Max(
                $pixel.R,
                [Math]::Max($pixel.G, $pixel.B)
            )
            $minimum = [Math]::Min(
                $pixel.R,
                [Math]::Min($pixel.G, $pixel.B)
            )
            if ($maximum -le 20) { $nearBlack += 1 }
            if ($minimum -ge 235) { $nearWhite += 1 }
            if ($maximum - $minimum -ge 24) { $colored += 1 }
            $quantized = (
                (($pixel.R -shr 4) -shl 8) -bor
                (($pixel.G -shr 4) -shl 4) -bor
                ($pixel.B -shr 4)
            )
            $quantizedColors.Add($quantized) | Out-Null
            for (
                $paletteIndex = 0;
                $paletteIndex -lt $ExpectedDarkClientPalette.Count;
                $paletteIndex += 1
            ) {
                $expected = $ExpectedDarkClientPalette[$paletteIndex]
                $expectedRed = ($expected -shr 16) -band 0xFF
                $expectedGreen = ($expected -shr 8) -band 0xFF
                $expectedBlue = $expected -band 0xFF
                if (
                    [Math]::Abs($pixel.R - $expectedRed) -le
                        $ExpectedPaletteChannelTolerance -and
                    [Math]::Abs($pixel.G - $expectedGreen) -le
                        $ExpectedPaletteChannelTolerance -and
                    [Math]::Abs($pixel.B - $expectedBlue) -le
                        $ExpectedPaletteChannelTolerance
                ) {
                    $expectedPaletteMatches += 1
                    $matchedPaletteIndexes.Add($paletteIndex) | Out-Null
                    break
                }
            }
        }
    }
    if ($sampleCount -lt 1000) {
        return $false
    }
    return (
        $nearBlack -lt [Math]::Floor($sampleCount * 0.55) -and
        $nearWhite -lt [Math]::Floor($sampleCount * 0.80) -and
        $colored -ge [Math]::Ceiling($sampleCount * 0.02) -and
        $quantizedColors.Count -ge 32 -and
        $expectedPaletteMatches -ge [Math]::Ceiling(
            $sampleCount * $MinimumExpectedPaletteFraction
        ) -and
        $matchedPaletteIndexes.Count -ge $MinimumExpectedPaletteColors
    )
}

function Save-StableRenderedStartupWindow {
    param(
        [Parameter(Mandatory = $true)][object]$Expected,
        [Parameter(Mandatory = $true)][object]$WorkArea,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )
    $deadline = [DateTime]::UtcNow.AddSeconds(
        $StartupWindowTimeoutSeconds
    )
    $previousDigest = $null
    $stableFrameCount = 0
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($script:startupProcess.HasExited) {
            throw 'Installed application exited before its rendered startup frame was captured.'
        }
        $current = (
            Select-UniqueReviewableStartupWindow `
                -Observations @(Get-StartupWindowObservations) `
                -Phase 'rendered startup'
        )
        if (
            $null -eq $current -or
            -not (Test-SameStartupWindowIdentity `
                -Expected $Expected `
                -Observed $current) -or
            -not (Test-ExtendedBoundsInsideWorkArea `
                -Observation $current `
                -WorkArea $WorkArea)
        ) {
            $previousDigest = $null
            $stableFrameCount = 0
            Start-Sleep -Milliseconds $RenderedFramePollMilliseconds
            continue
        }
        $foregroundAccepted = (
            [ChummerUnsignedPreviewStartupCapture]::SetForegroundWindow(
                $current.WindowHandle
            )
        )
        if (-not $foregroundAccepted) {
            $previousDigest = $null
            $stableFrameCount = 0
            Start-Sleep -Milliseconds $RenderedFramePollMilliseconds
            continue
        }
        if (
            [ChummerUnsignedPreviewStartupCapture]::GetForegroundWindow() -ne
                $current.WindowHandle
        ) {
            $previousDigest = $null
            $stableFrameCount = 0
            Start-Sleep -Milliseconds $RenderedFramePollMilliseconds
            continue
        }
        $automation = Get-StartupAutomationEvidence -Observation $current
        if ($null -eq $automation) {
            $previousDigest = $null
            $stableFrameCount = 0
            Start-Sleep -Milliseconds $RenderedFramePollMilliseconds
            continue
        }
        if ([ChummerUnsignedPreviewStartupCapture]::DwmFlush() -ne 0) {
            throw 'Windows compositor did not confirm the startup frame.'
        }
        $postFlush = (
            Select-UniqueReviewableStartupWindow `
                -Observations @(Get-StartupWindowObservations) `
                -Phase 'post-compositor startup'
        )
        if (
            $null -eq $postFlush -or
            -not (Test-SameStartupWindowIdentity `
                -Expected $current `
                -Observed $postFlush) -or
            [ChummerUnsignedPreviewStartupCapture]::GetForegroundWindow() -ne
                $postFlush.WindowHandle -or
            $postFlush.ExtendedLeft -ne $current.ExtendedLeft -or
            $postFlush.ExtendedTop -ne $current.ExtendedTop -or
            $postFlush.ExtendedRight -ne $current.ExtendedRight -or
            $postFlush.ExtendedBottom -ne $current.ExtendedBottom -or
            $postFlush.ClientLeft -ne $current.ClientLeft -or
            $postFlush.ClientTop -ne $current.ClientTop -or
            $postFlush.ClientRight -ne $current.ClientRight -or
            $postFlush.ClientBottom -ne $current.ClientBottom
        ) {
            $previousDigest = $null
            $stableFrameCount = 0
            Start-Sleep -Milliseconds $RenderedFramePollMilliseconds
            continue
        }
        $bitmap = New-StartupWindowBitmap -Observation $postFlush
        try {
            if (-not (Test-RenderedStartupBitmap `
                    -Bitmap $bitmap `
                    -Observation $postFlush)) {
                $previousDigest = $null
                $stableFrameCount = 0
                Start-Sleep -Milliseconds $RenderedFramePollMilliseconds
                continue
            }
            $digest = Get-BitmapSha256 -Bitmap $bitmap
            if ($digest -ceq $previousDigest) {
                $stableFrameCount += 1
            } else {
                $previousDigest = $digest
                $stableFrameCount = 1
            }
            if ($stableFrameCount -ge $RequiredStableRenderedFrameCount) {
                $bitmap.Save(
                    $OutputPath,
                    [Drawing.Imaging.ImageFormat]::Png
                )
                return [pscustomobject]@{
                    Height = $bitmap.Height
                    Sha256 = $digest
                    Width = $bitmap.Width
                }
            }
        } finally {
            $bitmap.Dispose()
        }
        Start-Sleep -Milliseconds $RenderedFramePollMilliseconds
    }
    throw 'Installed application did not expose a stable rendered startup frame.'
}

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
$script:startupProcess = $null
$script:startupProcessId = $null
try {
    $env:CHUMMER_DESKTOP_UPDATE_ENABLED = '0'
    $script:startupProcess = Start-Process -FilePath $executable -PassThru
    $script:startupProcessId = $script:startupProcess.Id
    $postPromptMain = Dismiss-AuthenticatedInstallLinkingPrompt
    $null = Wait-AuthenticatedPostPromptQuiescence `
        -Expected $postPromptMain
    $startupWindow = Wait-StableStartupWindow -Phase 'initial startup'
    $placement = Place-StartupWindowForReview `
        -Observation $startupWindow
    $renderedFrame = Save-StableRenderedStartupWindow `
        -Expected $placement.Observation `
        -WorkArea $placement.WorkArea `
        -OutputPath $StartupScreenshot
    $postCaptureWindow = (
        Select-UniqueReviewableStartupWindow `
            -Observations @(Get-StartupWindowObservations) `
            -Phase 'post-capture startup'
    )
    if (
        $null -eq $postCaptureWindow -or
        -not (Test-SameStartupWindowIdentity `
            -Expected $placement.Observation `
            -Observed $postCaptureWindow) -or
        [ChummerUnsignedPreviewStartupCapture]::GetForegroundWindow() -ne
            $postCaptureWindow.WindowHandle -or
        $postCaptureWindow.ExtendedLeft -ne
            $placement.Observation.ExtendedLeft -or
        $postCaptureWindow.ExtendedTop -ne
            $placement.Observation.ExtendedTop -or
        $postCaptureWindow.ExtendedRight -ne
            $placement.Observation.ExtendedRight -or
        $postCaptureWindow.ExtendedBottom -ne
            $placement.Observation.ExtendedBottom -or
        $postCaptureWindow.ClientLeft -ne
            $placement.Observation.ClientLeft -or
        $postCaptureWindow.ClientTop -ne
            $placement.Observation.ClientTop -or
        $postCaptureWindow.ClientRight -ne
            $placement.Observation.ClientRight -or
        $postCaptureWindow.ClientBottom -ne
            $placement.Observation.ClientBottom
    ) {
        throw 'Installed application window identity changed after startup capture.'
    }
    $screenshotSha = $renderedFrame.Sha256
    $width = $renderedFrame.Width
    $height = $renderedFrame.Height
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
    if (
        $null -ne $script:startupProcess -and
        -not $script:startupProcess.HasExited
    ) {
        $script:startupProcess.CloseMainWindow() | Out-Null
        if (-not $script:startupProcess.WaitForExit(5000)) {
            $script:startupProcess.Kill()
            $script:startupProcess.WaitForExit()
        }
    }
}
