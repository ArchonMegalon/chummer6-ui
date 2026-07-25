[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CandidateInstaller,
    [Parameter(Mandatory = $true)][string]$CandidateInstallerSha256,
    [Parameter(Mandatory = $true)][long]$CandidateInstallerSizeBytes,
    [Parameter(Mandatory = $true)][string]$CandidatePayload,
    [Parameter(Mandatory = $true)][string]$CandidatePayloadSha256,
    [Parameter(Mandatory = $true)][long]$CandidatePayloadSizeBytes,
    [Parameter(Mandatory = $true)][string]$CandidateSigningReceipt,
    [Parameter(Mandatory = $true)][string]$CandidateSigningReceiptSha256,
    [Parameter(Mandatory = $true)][long]$CandidateSigningReceiptSizeBytes,
    [Parameter(Mandatory = $true)][string]$CandidateVersion,
    [Parameter(Mandatory = $true)][string]$NMinusOneBindingJson,
    [Parameter(Mandatory = $true)][string]$ExpectedSignerCertificateSha256,
    [Parameter(Mandatory = $true)][string]$ExpectedSignerSpkiSha256,
    [Parameter(Mandatory = $true)][string]$OutputRoot,
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

$contractScript = Join-Path $PSScriptRoot 'desktop_native_lifecycle_evidence.py'
$authenticodeScript = Join-Path $PSScriptRoot 'verify-windows-authenticode.ps1'
$receiptName = 'DESKTOP_NATIVE_LIFECYCLE-windows-win-x64.generated.json'
$receiptPath = Join-Path $OutputRoot $receiptName
$privateRoot = ''
$installedRoot = ''
$cachedUninstaller = ''

function Fail([string]$Message) {
    throw "windows-native-lifecycle: $Message"
}

function UtcNow {
    return [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
}

function Get-FileSha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-BoundRegularFile(
    [string]$Path,
    [string]$ExpectedSha256,
    [long]$ExpectedSizeBytes,
    [string]$Label
) {
    if ($ExpectedSha256 -cnotmatch '^[0-9a-f]{64}$' -or $ExpectedSizeBytes -lt 1) {
        Fail "$Label binding is malformed."
    }
    $item = Get-Item -LiteralPath $Path -Force
    if (-not ($item -is [System.IO.FileInfo]) -or
        (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
        Fail "$Label must be a regular non-reparse-point file."
    }
    if ($item.Length -ne $ExpectedSizeBytes) {
        Fail "$Label size differs from its binding."
    }
    if ((Get-FileSha256 $item.FullName) -cne $ExpectedSha256) {
        Fail "$Label SHA-256 differs from its binding."
    }
    return $item.FullName
}

function Parse-BindingLines([string[]]$Lines) {
    $result = @{}
    foreach ($line in $Lines) {
        if ($line -match '^([a-z0-9_]+)=(.+)$') {
            if ($result.ContainsKey($Matches[1])) {
                Fail "Contract emitted duplicate binding '$($Matches[1])'."
            }
            $result[$Matches[1]] = $Matches[2]
        }
    }
    return $result
}

function Assert-HttpsAuthority([Uri]$Uri, [string]$Label) {
    if ($Uri.Scheme -cne 'https' -or $Uri.Host -cne 'chummer.run' -or
        -not [string]::IsNullOrEmpty($Uri.UserInfo) -or
        (-not $Uri.IsDefaultPort -and $Uri.Port -ne 443) -or
        -not [string]::IsNullOrEmpty($Uri.Query) -or
        -not [string]::IsNullOrEmpty($Uri.Fragment)) {
        Fail "$Label left the pinned credential-free chummer.run HTTPS authority."
    }
}

function Invoke-PinnedDownload(
    [string]$Url,
    [string]$Target,
    [string]$ExpectedSha256,
    [long]$ExpectedSizeBytes,
    [long]$MaximumSizeBytes,
    [string]$Label
) {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromMinutes(10)
    $uri = [Uri]$Url
    try {
        for ($redirects = 0; $redirects -le 5; $redirects++) {
            Assert-HttpsAuthority $uri $Label
            $response = $client.GetAsync(
                $uri,
                [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead
            ).GetAwaiter().GetResult()
            try {
                $status = [int]$response.StatusCode
                if ($status -in @(301, 302, 303, 307, 308)) {
                    if ($redirects -eq 5 -or $null -eq $response.Headers.Location) {
                        Fail "$Label exceeded the fixed redirect policy."
                    }
                    $uri = [Uri]::new($uri, $response.Headers.Location)
                    continue
                }
                if (-not $response.IsSuccessStatusCode) {
                    Fail "$Label download returned HTTP $status."
                }
                if ($response.Content.Headers.ContentLength.HasValue -and
                    $response.Content.Headers.ContentLength.Value -gt $MaximumSizeBytes) {
                    Fail "$Label declared a response larger than its fixed bound."
                }
                $stream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                $output = [IO.File]::Open(
                    $Target,
                    [IO.FileMode]::CreateNew,
                    [IO.FileAccess]::Write,
                    [IO.FileShare]::None
                )
                try {
                    $buffer = [byte[]]::new(1024 * 1024)
                    [long]$written = 0
                    while (($count = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                        $written += $count
                        if ($written -gt $MaximumSizeBytes) {
                            Fail "$Label exceeded its fixed download bound."
                        }
                        $output.Write($buffer, 0, $count)
                    }
                    $output.Flush($true)
                }
                finally {
                    $output.Dispose()
                    $stream.Dispose()
                }
                if ($ExpectedSizeBytes -gt 0 -and $written -ne $ExpectedSizeBytes) {
                    Fail "$Label downloaded size differs from its binding."
                }
                if ((Get-FileSha256 $Target) -cne $ExpectedSha256) {
                    Fail "$Label downloaded SHA-256 differs from its binding."
                }
                return
            }
            finally {
                $response.Dispose()
            }
        }
        Fail "$Label did not reach a terminal response."
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

function Invoke-CheckedProcess(
    [string]$Executable,
    [string[]]$Arguments,
    [string]$LogPath,
    [string]$Label
) {
    $output = & $Executable @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    @($output) | Out-File -LiteralPath $LogPath -Encoding utf8
    if ($exitCode -ne 0) {
        Fail "$Label exited with code $exitCode."
    }
}

function Get-ChummerUninstallEntries {
    $roots = @(
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
    )
    $entries = @()
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        foreach ($key in Get-ChildItem -LiteralPath $root -ErrorAction Stop) {
            $row = Get-ItemProperty -LiteralPath $key.PSPath -ErrorAction Stop
            if ([string]$row.DisplayName -like 'Chummer*' -and
                -not [string]::IsNullOrWhiteSpace([string]$row.UninstallString)) {
                $entries += $row
            }
        }
    }
    return @($entries)
}

function Resolve-CachedUninstaller([object]$Entry) {
    $command = [string]$Entry.UninstallString
    if ($command -match '^"([^"]+)"(?:\s|$)') {
        return $Matches[1]
    }
    if ($command -match '^(\S+)(?:\s|$)') {
        return $Matches[1]
    }
    Fail 'Registered uninstall command cannot be parsed safely.'
}

function Resolve-InstalledLauncher([string]$Root) {
    $matches = @(
        Get-ChildItem -LiteralPath $Root -Recurse -File -Filter 'Chummer.Avalonia.exe' |
            Where-Object {
                (($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0)
            }
    )
    if ($matches.Count -ne 1) {
        Fail "Expected exactly one installed Chummer.Avalonia.exe, found $($matches.Count)."
    }
    return $matches[0].FullName
}

function Stop-InstalledProcesses([string]$Root) {
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    foreach ($process in Get-Process -ErrorAction Stop) {
        try {
            $path = $process.Path
            if (-not [string]::IsNullOrWhiteSpace($path) -and
                [IO.Path]::GetFullPath($path).StartsWith(
                    $prefix,
                    [StringComparison]::OrdinalIgnoreCase
                )) {
                Stop-Process -Id $process.Id -Force -ErrorAction Stop
                $process.WaitForExit(30000)
            }
        }
        catch [System.ComponentModel.Win32Exception] {
            continue
        }
        catch [System.InvalidOperationException] {
            continue
        }
    }
}

function Get-InstalledShortcutPaths([string]$Root) {
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $shell = New-Object -ComObject WScript.Shell
    $paths = [System.Collections.Generic.List[string]]::new()
    try {
        $shortcutRoots = @(
            [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory),
            [Environment]::GetFolderPath([Environment+SpecialFolder]::StartMenu)
        )
        foreach ($shortcutRoot in $shortcutRoots) {
            if (-not (Test-Path -LiteralPath $shortcutRoot)) { continue }
            foreach ($shortcutFile in Get-ChildItem -LiteralPath $shortcutRoot -Recurse -File -Filter '*.lnk') {
                $shortcut = $shell.CreateShortcut($shortcutFile.FullName)
                $target = [string]$shortcut.TargetPath
                if (-not [string]::IsNullOrWhiteSpace($target) -and
                    [IO.Path]::GetFullPath($target).StartsWith(
                        $prefix,
                        [StringComparison]::OrdinalIgnoreCase
                    )) {
                    $paths.Add($shortcutFile.FullName)
                }
            }
        }
    }
    finally {
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
    }
    return @($paths)
}

function Assert-PassingJson([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail "$Label receipt is missing."
    }
    $payload = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ([string]$payload.status -notin @('pass', 'passed')) {
        Fail "$Label receipt did not pass."
    }
}

function Invoke-CoreWorkflow(
    [string]$Label,
    [string]$Launcher,
    [string]$Version,
    [string]$ArtifactSha256
) {
    $safeLabel = $Label -replace '[^a-z0-9-]', '-'
    $startupReceipt = Join-Path $OutputRoot "$safeLabel-startup.receipt.json"
    $startupFailure = Join-Path $OutputRoot "$safeLabel-startup.failure.json"
    $mouseReceipt = Join-Path $OutputRoot "$safeLabel-mouse-first.receipt.json"
    $mouseFailure = Join-Path $OutputRoot "$safeLabel-mouse-first.failure.json"
    $mouseTrace = Join-Path $OutputRoot "$safeLabel-mouse-first.trace.json"
    $screenshots = Join-Path $OutputRoot "$safeLabel-mouse-first-screenshots"
    New-Item -ItemType Directory -Path $screenshots -Force | Out-Null

    $env:CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT = $startupReceipt
    $env:CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET = $startupFailure
    $env:CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST = "sha256:$ArtifactSha256"
    $env:CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS = 'github-actions-windows-x64'
    $env:CHUMMER_DESKTOP_STARTUP_SMOKE_RELEASE_VERSION = $Version
    $env:CHUMMER_DESKTOP_STARTUP_SMOKE_RID = 'win-x64'
    $env:CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT = 'pre_ui_event_loop'
    $env:CHUMMER_DESKTOP_UPDATE_ENABLED = '0'
    Invoke-CheckedProcess $Launcher @('--startup-smoke') `
        (Join-Path $OutputRoot "$safeLabel-startup.log") "$Label startup smoke"
    Assert-PassingJson $startupReceipt "$Label startup"

    $env:CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RECEIPT = $mouseReceipt
    $env:CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_FAILURE_PACKET = $mouseFailure
    $env:CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR = $screenshots
    $env:CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_TRACE = $mouseTrace
    $env:CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_ARTIFACT_DIGEST = "sha256:$ArtifactSha256"
    $env:CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_HOST_CLASS = 'github-actions-windows-x64'
    $env:CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RELEASE_VERSION = $Version
    $env:CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RID = 'win-x64'
    $env:CHUMMER_DESKTOP_RELEASE_CHANNEL = 'flagship'
    Invoke-CheckedProcess $Launcher @('--mouse-first-user-journey') `
        (Join-Path $OutputRoot "$safeLabel-mouse-first.log") "$Label mouse-first journey"
    Assert-PassingJson $mouseReceipt "$Label mouse-first"

    return @{
        startup = $startupReceipt
        mouse = $mouseReceipt
    }
}

function New-FileBinding([string]$Path, [string]$Role) {
    $relative = [IO.Path]::GetRelativePath(
        [IO.Path]::GetFullPath($OutputRoot),
        [IO.Path]::GetFullPath($Path)
    ).Replace('\', '/')
    if ($relative.StartsWith('../') -or [IO.Path]::IsPathRooted($relative)) {
        Fail "Evidence file escaped the output root: $Path"
    }
    $item = Get-Item -LiteralPath $Path
    return [ordered]@{
        path = $relative
        role = $Role
        sha256 = Get-FileSha256 $item.FullName
        sizeBytes = [long]$item.Length
    }
}

function Wait-InstallRootRemoval([string]$Path) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(90)
    while ((Test-Path -LiteralPath $Path) -and [DateTimeOffset]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 500
    }
    return -not (Test-Path -LiteralPath $Path)
}

if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or
    $env:RUNNER_OS -cne 'Windows') {
    Fail 'This evidence lane requires a native GitHub Windows runner.'
}
if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne
    [Runtime.InteropServices.Architecture]::X64) {
    Fail 'This evidence lane requires a native Windows x64 runner.'
}
if ($SourceActor -cne 'github-actions[bot]') {
    Fail 'The governed native lane must be dispatched by the producer relay.'
}
if ($SourceTriggeringActor -cne $SourceActor) {
    Fail 'The governed native lane permits only same-actor reruns.'
}
if ($SourceRepository -cne 'ArchonMegalon/chummer6-ui' -or
    $SourceWorkflow -cne '.github/workflows/windows-native-evidence-capture.yml' -or
    $SourceRef -cne 'refs/heads/main') {
    Fail 'The native source is not the governed Windows lane on main.'
}
if ($SourceSha -cnotmatch '^[0-9a-f]{40}$' -or
    $SourceRunId -cnotmatch '^[1-9][0-9]*$' -or
    $SourceRunAttempt -cnotmatch '^[1-9][0-9]*$') {
    Fail 'The native workflow run identity is malformed.'
}
if ($CandidateVersion -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._+-]{0,127}$') {
    Fail 'The candidate version is not a portable release identifier.'
}
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_SHA) -and
    $SourceSha -cne $env:GITHUB_SHA) {
    Fail 'The native source SHA differs from the checked-out workflow commit.'
}
foreach ($pin in @($ExpectedSignerCertificateSha256, $ExpectedSignerSpkiSha256)) {
    if ($pin -cnotmatch '^[0-9a-f]{64}$') {
        Fail 'Pinned Authenticode signer certificate and SPKI digests are required.'
    }
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$CandidateInstaller = Assert-BoundRegularFile $CandidateInstaller `
    $CandidateInstallerSha256 $CandidateInstallerSizeBytes 'candidate installer'
$CandidatePayload = Assert-BoundRegularFile $CandidatePayload `
    $CandidatePayloadSha256 $CandidatePayloadSizeBytes 'candidate payload'
$CandidateSigningReceipt = Assert-BoundRegularFile $CandidateSigningReceipt `
    $CandidateSigningReceiptSha256 $CandidateSigningReceiptSizeBytes 'candidate v2 signing receipt'
$candidateSigningCopy = Join-Path $OutputRoot 'candidate-v2-signing-receipt.json'
[IO.File]::Copy($CandidateSigningReceipt, $candidateSigningCopy, $false)
Assert-BoundRegularFile $candidateSigningCopy $CandidateSigningReceiptSha256 `
    $CandidateSigningReceiptSizeBytes 'copied candidate v2 signing receipt' | Out-Null

$bindingLines = & python $contractScript validate-n-minus-one `
    --binding-json $NMinusOneBindingJson --platform windows --rid win-x64
if ($LASTEXITCODE -ne 0) { Fail 'N-1 authority validation failed.' }
$previous = Parse-BindingLines @($bindingLines)
$requiredBindings = @(
    'artifact_file_name', 'artifact_sha256', 'artifact_size_bytes', 'artifact_url',
    'generation_id', 'manifest_sha256', 'manifest_url', 'payload_file_name',
    'payload_sha256', 'payload_size_bytes', 'payload_url', 'released_at', 'version'
)
foreach ($key in $requiredBindings) {
    if (-not $previous.ContainsKey($key)) { Fail "N-1 binding '$key' is missing." }
}
if ($previous.version -ceq $CandidateVersion) {
    Fail 'Candidate and N-1 versions must be distinct.'
}

$privateRoot = Join-Path $env:RUNNER_TEMP ("chummer-windows-lifecycle-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $privateRoot | Out-Null
$oldInstaller = Join-Path $privateRoot $previous.artifact_file_name
$oldPayload = Join-Path $privateRoot $previous.payload_file_name
$oldManifest = Join-Path $privateRoot 'N_MINUS_ONE_RELEASE_CHANNEL.generated.json'

$phases = [System.Collections.Generic.List[object]]::new()
$phaseStart = UtcNow
Invoke-PinnedDownload $previous.manifest_url $oldManifest $previous.manifest_sha256 `
    0 (8 * 1024 * 1024) 'N-1 release manifest'
Invoke-PinnedDownload $previous.artifact_url $oldInstaller $previous.artifact_sha256 `
    ([long]$previous.artifact_size_bytes) ([long]$previous.artifact_size_bytes) 'N-1 installer'
Invoke-PinnedDownload $previous.payload_url $oldPayload $previous.payload_sha256 `
    ([long]$previous.payload_size_bytes) ([long]$previous.payload_size_bytes) 'N-1 payload'
& python $contractScript validate-n-minus-one-manifest `
    --manifest $oldManifest `
    --binding-json $NMinusOneBindingJson `
    --platform windows `
    --rid win-x64 | Out-Null
if ($LASTEXITCODE -ne 0) { Fail 'Downloaded N-1 manifest validation failed.' }
$manifestEvidence = Join-Path $OutputRoot 'n-minus-one-release-manifest.json'
[IO.File]::Copy($oldManifest, $manifestEvidence, $false)
$oldManifestSize = [long](Get-Item -LiteralPath $oldManifest).Length
Assert-BoundRegularFile $manifestEvidence $previous.manifest_sha256 `
    $oldManifestSize 'copied N-1 release manifest' | Out-Null

$oldAuth = Join-Path $OutputRoot 'authenticode-n-minus-one.json'
$candidateAuth = Join-Path $OutputRoot 'authenticode-candidate.json'
& $authenticodeScript -ArtifactPath $oldInstaller `
    -ExpectedArtifactSha256 $previous.artifact_sha256 `
    -ExpectedArtifactSizeBytes ([long]$previous.artifact_size_bytes) `
    -ExpectedSignerCertificateSha256 $ExpectedSignerCertificateSha256 `
    -ExpectedSignerSpkiSha256 $ExpectedSignerSpkiSha256 `
    -OutputPath $oldAuth -SourceRepository $SourceRepository -SourceWorkflow $SourceWorkflow `
    -SourceRunId $SourceRunId -SourceRunAttempt $SourceRunAttempt -SourceRef $SourceRef `
    -SourceSha $SourceSha -SourceActor $SourceActor `
    -SourceTriggeringActor $SourceTriggeringActor
if ($LASTEXITCODE -ne 0) { Fail 'N-1 Authenticode verification failed.' }
& $authenticodeScript -ArtifactPath $CandidateInstaller `
    -ExpectedArtifactSha256 $CandidateInstallerSha256 `
    -ExpectedArtifactSizeBytes $CandidateInstallerSizeBytes `
    -ExpectedSignerCertificateSha256 $ExpectedSignerCertificateSha256 `
    -ExpectedSignerSpkiSha256 $ExpectedSignerSpkiSha256 `
    -OutputPath $candidateAuth -SourceRepository $SourceRepository -SourceWorkflow $SourceWorkflow `
    -SourceRunId $SourceRunId -SourceRunAttempt $SourceRunAttempt -SourceRef $SourceRef `
    -SourceSha $SourceSha -SourceActor $SourceActor `
    -SourceTriggeringActor $SourceTriggeringActor
if ($LASTEXITCODE -ne 0) { Fail 'Candidate Authenticode verification failed.' }
$phases.Add([ordered]@{
    name = 'artifact_authentication'; status = 'passed'; startedAt = $phaseStart
    completedAt = UtcNow
    details = [ordered]@{
        candidateDigestVerified = $true
        nMinusOneDigestVerified = $true
        nativePackageAuthorityVerified = $true
    }
})

$stateRoot = Join-Path $privateRoot 'user-state'
New-Item -ItemType Directory -Path $stateRoot | Out-Null
$sentinel = Join-Path $stateRoot 'lifecycle-user-state.txt'
[IO.File]::WriteAllText(
    $sentinel,
    "chummer-native-lifecycle-state-$([Guid]::NewGuid().ToString('N'))",
    [Text.UTF8Encoding]::new($false)
)
$sentinelBefore = Get-FileSha256 $sentinel
$env:CHUMMER_DESKTOP_STATE_ROOT = $stateRoot

try {
    if ((Get-ChummerUninstallEntries).Count -ne 0) {
        Fail 'Windows runner is not clean: a Chummer uninstall entry already exists.'
    }

    $phaseStart = UtcNow
    $env:CHUMMER_INSTALLER_PAYLOAD_PATH = $oldPayload
    $env:CHUMMER_INSTALLER_PAYLOAD_SHA256 = $previous.payload_sha256
    $env:CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES = $previous.payload_size_bytes
    # N-1 may predate --unattended.  --auto-update has been part of the
    # production installer contract and executes the same full registration
    # path without the completion prompt.
    Invoke-CheckedProcess $oldInstaller @('--auto-update', '--launch-head', 'avalonia') `
        (Join-Path $OutputRoot 'n-minus-one-install.log') 'N-1 full install'
    $entries = @(Get-ChummerUninstallEntries)
    if ($entries.Count -ne 1) { Fail 'N-1 install did not create exactly one uninstall entry.' }
    $cachedUninstaller = Resolve-CachedUninstaller $entries[0]
    if (-not (Test-Path -LiteralPath $cachedUninstaller -PathType Leaf)) {
        Fail 'N-1 registered cached uninstaller is missing.'
    }
    if ((Get-FileSha256 $cachedUninstaller) -cne $previous.artifact_sha256) {
        Fail 'N-1 registered cached uninstaller differs from the authenticated installer.'
    }
    $installedRoot = Split-Path -Parent $cachedUninstaller
    Stop-InstalledProcesses $installedRoot
    $oldLauncher = Resolve-InstalledLauncher $installedRoot
    $installedShortcuts = @(Get-InstalledShortcutPaths $installedRoot)
    if ($installedShortcuts.Count -lt 2) {
        Fail 'N-1 full install did not create the expected Start Menu and Desktop shortcuts.'
    }
    if (-not (Test-Path -LiteralPath 'HKCU:\Software\Classes\chummer')) {
        Fail 'N-1 full install did not register the Chummer URL protocol.'
    }
    $oldLauncherSha = Get-FileSha256 $oldLauncher
    $oldFileVersion = (Get-Item -LiteralPath $oldLauncher).VersionInfo.FileVersion
    $phases.Add([ordered]@{
        name = 'clean_install_n_minus_one'; status = 'passed'; startedAt = $phaseStart
        completedAt = UtcNow
        details = [ordered]@{ installed = $true; launcherPresent = $true }
    })

    $phaseStart = UtcNow
    $oldCore = Invoke-CoreWorkflow 'n-minus-one' $oldLauncher $previous.version `
        $previous.artifact_sha256
    $phases.Add([ordered]@{
        name = 'core_workflow_n_minus_one'; status = 'passed'; startedAt = $phaseStart
        completedAt = UtcNow
        details = [ordered]@{
            mouseFirstJourneyPassed = $true
            startupSmokePassed = $true
        }
    })

    $phaseStart = UtcNow
    $env:CHUMMER_INSTALLER_PAYLOAD_PATH = $CandidatePayload
    $env:CHUMMER_INSTALLER_PAYLOAD_SHA256 = $CandidatePayloadSha256
    $env:CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES = [string]$CandidatePayloadSizeBytes
    Invoke-CheckedProcess $CandidateInstaller @('--unattended') `
        (Join-Path $OutputRoot 'candidate-update.log') 'candidate full update'
    $entries = @(Get-ChummerUninstallEntries)
    if ($entries.Count -ne 1) { Fail 'Candidate update did not retain exactly one uninstall entry.' }
    $cachedUninstaller = Resolve-CachedUninstaller $entries[0]
    if (-not (Test-Path -LiteralPath $cachedUninstaller -PathType Leaf) -or
        (Get-FileSha256 $cachedUninstaller) -cne $CandidateInstallerSha256) {
        Fail 'Candidate registered cached uninstaller differs from the authenticated installer.'
    }
    $installedRoot = Split-Path -Parent $cachedUninstaller
    $candidateLauncher = Resolve-InstalledLauncher $installedRoot
    $candidateLauncherSha = Get-FileSha256 $candidateLauncher
    $candidateFileVersion = (Get-Item -LiteralPath $candidateLauncher).VersionInfo.FileVersion
    if ($candidateLauncherSha -ceq $oldLauncherSha) {
        Fail 'Candidate update left the N-1 launcher bytes installed.'
    }
    if ([string]::IsNullOrWhiteSpace($oldFileVersion) -or
        [string]::IsNullOrWhiteSpace($candidateFileVersion) -or
        $candidateFileVersion -ceq $oldFileVersion) {
        Fail 'Candidate update did not change the installed launcher file version.'
    }
    if (-not (Test-Path -LiteralPath 'HKCU:\Software\Classes\chummer') -or
        (@(Get-InstalledShortcutPaths $installedRoot)).Count -lt 2) {
        Fail 'Candidate update did not retain normal protocol and shortcut registration.'
    }
    $sentinelAfterUpdate = Get-FileSha256 $sentinel
    if ($sentinelAfterUpdate -cne $sentinelBefore) {
        Fail 'Candidate update changed user state.'
    }
    $phases.Add([ordered]@{
        name = 'update_to_candidate'; status = 'passed'; startedAt = $phaseStart
        completedAt = UtcNow
        details = [ordered]@{
            candidateBytesInstalled = $true
            installedVersionChanged = $true
            statePreserved = $true
        }
    })

    $phaseStart = UtcNow
    $candidateCore = Invoke-CoreWorkflow 'candidate' $candidateLauncher $CandidateVersion `
        $CandidateInstallerSha256
    $phases.Add([ordered]@{
        name = 'core_workflow_candidate'; status = 'passed'; startedAt = $phaseStart
        completedAt = UtcNow
        details = [ordered]@{
            mouseFirstJourneyPassed = $true
            startupSmokePassed = $true
        }
    })

    $phaseStart = UtcNow
    Invoke-CheckedProcess $cachedUninstaller @('--uninstall', '--unattended') `
        (Join-Path $OutputRoot 'candidate-uninstall.log') 'registered candidate uninstall'
    $rootRemoved = Wait-InstallRootRemoval $installedRoot
    $entriesAfter = @(Get-ChummerUninstallEntries)
    $protocolAbsent = -not (Test-Path -LiteralPath 'HKCU:\Software\Classes\chummer')
    $shortcutsAfter = @(Get-InstalledShortcutPaths $installedRoot)
    if (-not $rootRemoved -or $entriesAfter.Count -ne 0 -or -not $protocolAbsent -or
        $shortcutsAfter.Count -ne 0) {
        Fail 'Registered candidate uninstall left application or registry state behind.'
    }
    $sentinelAfterUninstall = Get-FileSha256 $sentinel
    if ($sentinelAfterUninstall -cne $sentinelBefore) {
        Fail 'Normal uninstall changed user state.'
    }
    $phases.Add([ordered]@{
        name = 'normal_uninstall'; status = 'passed'; startedAt = $phaseStart
        completedAt = UtcNow
        details = [ordered]@{
            launcherAbsent = $true
            packageAbsent = $true
            uninstallerInvoked = $true
        }
    })

    $oldStartup = New-FileBinding $oldCore.startup 'n-minus-one-core-startup'
    $oldMouse = New-FileBinding $oldCore.mouse 'n-minus-one-core-mouse-first'
    $candidateStartup = New-FileBinding $candidateCore.startup 'candidate-core-startup'
    $candidateMouse = New-FileBinding $candidateCore.mouse 'candidate-core-mouse-first'
    $evidenceFiles = @(
        $oldStartup,
        $oldMouse,
        $candidateStartup,
        $candidateMouse,
        (New-FileBinding $oldAuth 'n-minus-one-authenticode'),
        (New-FileBinding $candidateAuth 'candidate-authenticode'),
        (New-FileBinding $candidateSigningCopy 'candidate-v2-signing-receipt'),
        (New-FileBinding $manifestEvidence 'n-minus-one-release-manifest')
    ) | Sort-Object path
    $oldAuthBinding = New-FileBinding $oldAuth 'n-minus-one-authenticode'
    $candidateAuthBinding = New-FileBinding $candidateAuth 'candidate-authenticode'
    $candidateSigningBinding = New-FileBinding `
        $candidateSigningCopy 'candidate-v2-signing-receipt'
    $manifestBinding = New-FileBinding `
        $manifestEvidence 'n-minus-one-release-manifest'

    $receipt = [ordered]@{
        candidate = [ordered]@{
            artifactFileName = [IO.Path]::GetFileName($CandidateInstaller)
            payload = [ordered]@{
                fileName = [IO.Path]::GetFileName($CandidatePayload)
                sha256 = $CandidatePayloadSha256
                sizeBytes = $CandidatePayloadSizeBytes
            }
            sha256 = $CandidateInstallerSha256
            sizeBytes = $CandidateInstallerSizeBytes
            sourceCommit = $SourceSha
            version = $CandidateVersion
        }
        contractName = 'chummer6-ui.desktop-native-lifecycle-evidence'
        contractVersion = 1
        coreWorkflow = [ordered]@{
            candidate = [ordered]@{
                mouseFirstReceipt = $candidateMouse
                startupReceipt = $candidateStartup
            }
            nMinusOne = [ordered]@{
                mouseFirstReceipt = $oldMouse
                startupReceipt = $oldStartup
            }
        }
        evidenceFiles = @($evidenceFiles)
        generatedAt = UtcNow
        nMinusOne = [ordered]@{
            artifactFileName = $previous.artifact_file_name
            artifactUrl = $previous.artifact_url
            generationId = $previous.generation_id
            manifestSha256 = $previous.manifest_sha256
            manifestUrl = $previous.manifest_url
            releasedAt = $previous.released_at
            payload = [ordered]@{
                fileName = $previous.payload_file_name
                sha256 = $previous.payload_sha256
                sizeBytes = [long]$previous.payload_size_bytes
                url = $previous.payload_url
            }
            sha256 = $previous.artifact_sha256
            sizeBytes = [long]$previous.artifact_size_bytes
            version = $previous.version
        }
        nativeRunner = [ordered]@{
            architecture = 'x64'
            environment = 'native'
            kernel = [Environment]::OSVersion.VersionString.Replace(' ', '-')
            runnerName = 'GitHub-Actions'
            runnerOs = 'Windows'
            source = [ordered]@{
                actor = $SourceActor
                ref = $SourceRef
                repository = $SourceRepository
                rerunPolicy = 'same-actor-only'
                runAttempt = $SourceRunAttempt
                runId = $SourceRunId
                sha = $SourceSha
                triggeringActor = $SourceTriggeringActor
                workflow = $SourceWorkflow
            }
        }
        packageAuthority = [ordered]@{
            candidate = [ordered]@{
                authenticodeReceipt = $candidateAuthBinding
                signingReceipt = $candidateSigningBinding
            }
            expectedSignerCertificateSha256 = $ExpectedSignerCertificateSha256
            expectedSignerSpkiSha256 = $ExpectedSignerSpkiSha256
            manifestReceipt = $manifestBinding
            mode = 'authenticode'
            nMinusOne = [ordered]@{
                authenticodeReceipt = $oldAuthBinding
            }
        }
        phases = @($phases)
        platform = 'windows'
        rid = 'win-x64'
        statePreservation = [ordered]@{
            preservedAfterUninstall = $true
            preservedAfterUpdate = $true
            sentinelSha256AfterUninstall = $sentinelAfterUninstall
            sentinelSha256AfterUpdate = $sentinelAfterUpdate
            sentinelSha256BeforeUpdate = $sentinelBefore
        }
        status = 'passed'
        uninstall = [ordered]@{
            installRootRemoved = $true
            launchersRemoved = $true
            mode = 'registered-cached-uninstaller'
            statusAfter = 'not-installed'
        }
    }
    $receipt | ConvertTo-Json -Depth 20 | Out-File -LiteralPath $receiptPath -Encoding utf8
    & python $contractScript verify-receipt --receipt $receiptPath --evidence-root $OutputRoot
    if ($LASTEXITCODE -ne 0) { Fail 'Final lifecycle receipt revalidation failed.' }
    "lifecycle_receipt_sha256=$(Get-FileSha256 $receiptPath)"
    "lifecycle_receipt_path=$receiptPath"
}
finally {
    if ((Get-ChummerUninstallEntries).Count -gt 0 -and
        -not [string]::IsNullOrWhiteSpace($cachedUninstaller) -and
        (Test-Path -LiteralPath $cachedUninstaller -PathType Leaf)) {
        & $cachedUninstaller --uninstall --unattended *> $null
    }
    if (-not [string]::IsNullOrWhiteSpace($privateRoot) -and
        (Test-Path -LiteralPath $privateRoot)) {
        Remove-Item -LiteralPath $privateRoot -Recurse -Force
    }
}
