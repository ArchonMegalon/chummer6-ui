[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ArtifactPath,
    [Parameter(Mandatory = $true)][ValidatePattern("^[0-9a-f]{64}$")]
    [string]$ExpectedArtifactSha256,
    [Parameter(Mandatory = $true)][long]$ExpectedArtifactSizeBytes,
    [Parameter(Mandatory = $true)][string]$OutputPath,
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
    throw 'Unsigned Authenticode verification requires native Windows.'
}
if ($env:WINELOADERNOEXEC -or $env:WINEPREFIX) {
    throw 'Wine cannot produce native Windows Authenticode evidence.'
}
if ($SourceRepository -cne 'ArchonMegalon/chummer6-ui' -or
    $SourceWorkflow -cne
        '.github/workflows/unsigned-windows-preview-native-evidence-capture.yml' -or
    $SourceRef -cne 'refs/heads/main' -or
    $SourceActor -cne 'github-actions[bot]' -or
    $SourceTriggeringActor -cne $SourceActor) {
    throw 'Unsigned Authenticode source authority differs.'
}
if ($SourceRunId -cnotmatch '^[1-9][0-9]*$' -or
    $SourceRunAttempt -cnotmatch '^[1-9][0-9]*$' -or
    $SourceSha -cnotmatch '^[0-9a-f]{40}$') {
    throw 'Unsigned Authenticode source identity is malformed.'
}
if ($ExpectedArtifactSizeBytes -lt 1) {
    throw 'Unsigned Authenticode artifact size must be positive.'
}
if (Test-Path -LiteralPath $OutputPath) {
    throw 'Unsigned Authenticode receipt output must be absent.'
}

$resolved = (Resolve-Path -LiteralPath $ArtifactPath -ErrorAction Stop).Path
$artifact = Get-Item -LiteralPath $resolved -Force
if (-not ($artifact -is [IO.FileInfo]) -or $artifact.LinkType) {
    throw 'Unsigned Authenticode artifact must be one regular non-link file.'
}
$actualSha256 = (
    Get-FileHash -LiteralPath $resolved -Algorithm SHA256
).Hash.ToLowerInvariant()
if ($actualSha256 -cne $ExpectedArtifactSha256 -or
    $artifact.Length -ne $ExpectedArtifactSizeBytes) {
    throw 'Unsigned Authenticode artifact bytes differ from candidate authority.'
}

$bytes = [IO.File]::ReadAllBytes($resolved)
if ($bytes.Length -lt 256 -or $bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a) {
    throw 'Unsigned candidate installer is not a structurally valid PE image.'
}
$peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
if ($peOffset -lt 0x40 -or $peOffset + 24 -gt $bytes.Length -or
    $bytes[$peOffset] -ne 0x50 -or $bytes[$peOffset + 1] -ne 0x45 -or
    $bytes[$peOffset + 2] -ne 0 -or $bytes[$peOffset + 3] -ne 0) {
    throw 'Unsigned candidate installer PE header is invalid.'
}
$optionalOffset = $peOffset + 24
$optionalSize = [BitConverter]::ToUInt16($bytes, $peOffset + 20)
$magic = [BitConverter]::ToUInt16($bytes, $optionalOffset)
if ($magic -eq 0x10b) {
    $dataDirectoryOffset = $optionalOffset + 96
}
elseif ($magic -eq 0x20b) {
    $dataDirectoryOffset = $optionalOffset + 112
}
else {
    throw 'Unsigned candidate installer optional-header magic is unsupported.'
}
$securityEntry = $dataDirectoryOffset + (4 * 8)
if ($securityEntry + 8 -gt $optionalOffset + $optionalSize -or
    $securityEntry + 8 -gt $bytes.Length) {
    throw 'Unsigned candidate installer security directory is absent.'
}
if ([BitConverter]::ToUInt32($bytes, $securityEntry) -ne 0 -or
    [BitConverter]::ToUInt32($bytes, $securityEntry + 4) -ne 0) {
    throw 'Unsigned preview policy requires an empty Authenticode security directory.'
}

$signature = Get-AuthenticodeSignature -LiteralPath $resolved
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::NotSigned -or
    $null -ne $signature.SignerCertificate -or
    $null -ne $signature.TimeStamperCertificate) {
    throw "Unsigned preview candidate unexpectedly has a signature: $($signature.Status)"
}

$parent = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $parent | Out-Null
$receipt = [ordered]@{
    artifact = [ordered]@{
        fileName = [IO.Path]::GetFileName($resolved)
        path = 'publication/files/chummer-avalonia-win-x64-installer.exe'
        sha256 = $actualSha256
        sizeBytes = $artifact.Length
    }
    contractName =
        'chummer6-ui.unsigned-preview-windows-authenticode-verification'
    contractVersion = 1
    generatedAt = [DateTime]::UtcNow.ToString(
        'yyyy-MM-ddTHH:mm:ss.fffffffZ',
        [Globalization.CultureInfo]::InvariantCulture
    )
    nativeHostEvidence = [ordered]@{
        contractName = 'chummer6-ui.native_windows_host_evidence'
        evidenceSource = 'GitHub-hosted windows-latest'
        hostPlatform = 'windows'
        isNativeWindows = $true
        runner = 'pwsh'
        status = 'verified'
    }
    signatureStatus = 'unsigned'
    signingRequired = $false
    source = [ordered]@{
        actor = $SourceActor
        artifactName =
            "unsigned-windows-preview-native-evidence-$SourceRunId-$SourceRunAttempt"
        ref = $SourceRef
        repository = $SourceRepository
        rerunPolicy = 'same-actor-only'
        runAttempt = $SourceRunAttempt
        runId = $SourceRunId
        sha = $SourceSha
        triggeringActor = $SourceTriggeringActor
        workflow = $SourceWorkflow
    }
    status = 'verified'
    unsignedReason = 'preview_policy'
    verifier = [ordered]@{
        authenticodeStatus = 'NotSigned'
        implementation =
            'scripts/verify_unsigned_windows_preview_authenticode.ps1'
        platform = 'windows'
        securityDirectoryEmpty = $true
    }
}
[IO.File]::WriteAllText(
    $OutputPath,
    ($receipt | ConvertTo-Json -Depth 8) + "`n",
    [Text.UTF8Encoding]::new($false)
)
