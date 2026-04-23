param(
    [Parameter(Mandatory = $true)]
    [string[]]$ArtifactPaths,
    [string]$ReceiptPath = $env:CHUMMER_WINDOWS_SIGNING_RECEIPT_PATH,
    [string]$AppKey = $env:CHUMMER_DESKTOP_APP_KEY,
    [string]$Rid = $env:CHUMMER_DESKTOP_RID,
    [string]$ReleaseChannel = $env:CHUMMER_DESKTOP_RELEASE_CHANNEL,
    [string]$ReleaseVersion = $env:CHUMMER_DESKTOP_RELEASE_VERSION
)

$ErrorActionPreference = "Stop"

function Test-Truthy([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $false
    }

    switch ($Value.Trim().ToLowerInvariant()) {
        "1" { return $true }
        "true" { return $true }
        "yes" { return $true }
        "on" { return $true }
        default { return $false }
    }
}

function Normalize-Token([string]$Value) {
    return ($Value ?? "").Trim().ToLowerInvariant()
}

function Resolve-SigntoolPath() {
    if (-not [string]::IsNullOrWhiteSpace($env:CHUMMER_WINDOWS_SIGNTOOL_PATH)) {
        return $env:CHUMMER_WINDOWS_SIGNTOOL_PATH
    }

    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidates = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending
    if ($candidates.Count -gt 0) {
        return $candidates[0].FullName
    }

    throw "signtool.exe was not found. Set CHUMMER_WINDOWS_SIGNTOOL_PATH or install Windows SDK signing tools."
}

function Resolve-PfxPath([ref]$TemporaryPath) {
    if (-not [string]::IsNullOrWhiteSpace($env:CHUMMER_WINDOWS_SIGN_PFX_PATH)) {
        return $env:CHUMMER_WINDOWS_SIGN_PFX_PATH
    }

    if ([string]::IsNullOrWhiteSpace($env:CHUMMER_WINDOWS_SIGN_PFX_BASE64)) {
        return $null
    }

    $TemporaryPath.Value = Join-Path ([System.IO.Path]::GetTempPath()) ("chummer-sign-" + [Guid]::NewGuid().ToString("N") + ".pfx")
    [System.IO.File]::WriteAllBytes(
        $TemporaryPath.Value,
        [Convert]::FromBase64String(($env:CHUMMER_WINDOWS_SIGN_PFX_BASE64 -replace "\s+", ""))
    )
    return $TemporaryPath.Value
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

function Get-ArtifactKind([string]$Path) {
    $name = [System.IO.Path]::GetFileName($Path).ToLowerInvariant()
    if ($name.EndsWith("-installer.exe") -or $name.EndsWith(".msix")) {
        return "installer"
    }

    if ($name.EndsWith(".exe")) {
        return "portable"
    }

    return "artifact"
}

function New-ArtifactRows([string[]]$Paths, [string]$SigningStatus) {
    $rows = @()
    foreach ($artifactPath in $Paths) {
        if (-not (Test-Path -LiteralPath $artifactPath)) {
            continue
        }

        $resolvedPath = (Resolve-Path -LiteralPath $artifactPath).Path
        $rows += [ordered]@{
            fileName = [System.IO.Path]::GetFileName($resolvedPath)
            sha256 = Get-Sha256 $resolvedPath
            kind = Get-ArtifactKind $resolvedPath
            signingStatus = $SigningStatus
        }
    }

    return $rows
}

function Write-Receipt(
    [string]$Path,
    [string[]]$Paths,
    [string]$SigningStatus,
    [string]$Reason
) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    $artifactRows = New-ArtifactRows -Paths $Paths -SigningStatus $SigningStatus
    $payload = [ordered]@{
        contractName = "chummer6-ui.desktop_artifact_signing"
        generatedAt = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        platform = "windows"
        app = $AppKey
        rid = $Rid
        releaseChannel = $ReleaseChannel
        releaseVersion = $ReleaseVersion
        signingStatus = $SigningStatus
        notarizationStatus = $null
        reason = $Reason
        artifacts = $artifactRows
    }

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $payload | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Path -Encoding utf8
}

$signingRequired = Test-Truthy $env:CHUMMER_WINDOWS_SIGNING_REQUIRED
$timestampUrl = if ([string]::IsNullOrWhiteSpace($env:CHUMMER_WINDOWS_TIMESTAMP_URL)) {
    "http://timestamp.digicert.com"
} else {
    $env:CHUMMER_WINDOWS_TIMESTAMP_URL
}
$pfxPassword = $env:CHUMMER_WINDOWS_SIGN_PFX_PASSWORD
if ([string]::IsNullOrWhiteSpace($pfxPassword)) {
    $pfxPassword = $env:CHUMMER_WINDOWS_SIGN_CERT_PASSWORD
}

$temporaryPfxPath = $null
$resolvedPfxPath = Resolve-PfxPath ([ref]$temporaryPfxPath)
$hasSigningConfiguration = -not [string]::IsNullOrWhiteSpace($resolvedPfxPath)

try {
    $resolvedArtifactPaths = @()
    foreach ($artifactPath in $ArtifactPaths) {
        if (-not (Test-Path -LiteralPath $artifactPath)) {
            throw "Artifact path does not exist: $artifactPath"
        }

        $resolvedArtifactPaths += (Resolve-Path -LiteralPath $artifactPath).Path
    }

    if (-not $hasSigningConfiguration) {
        if ($signingRequired) {
            $reason = "Windows signing is required for release channel '$ReleaseChannel', but no PFX certificate was configured."
            Write-Receipt -Path $ReceiptPath -Paths $resolvedArtifactPaths -SigningStatus "fail" -Reason $reason
            throw $reason
        }

        Write-Receipt -Path $ReceiptPath -Paths $resolvedArtifactPaths -SigningStatus "skipped_preview" -Reason "Preview channel does not require Authenticode signing."
        exit 0
    }

    $signtoolPath = Resolve-SigntoolPath
    foreach ($artifactPath in $resolvedArtifactPaths) {
        & $signtoolPath sign /fd SHA256 /td SHA256 /tr $timestampUrl /f $resolvedPfxPath /p $pfxPassword $artifactPath
        if ($LASTEXITCODE -ne 0) {
            throw "signtool sign failed for $artifactPath"
        }

        & $signtoolPath verify /pa /v $artifactPath
        if ($LASTEXITCODE -ne 0) {
            throw "signtool verify failed for $artifactPath"
        }
    }

    Write-Receipt -Path $ReceiptPath -Paths $resolvedArtifactPaths -SigningStatus "pass" -Reason ""
    exit 0
}
catch {
    if (-not [string]::IsNullOrWhiteSpace($ReceiptPath)) {
        $pathsForReceipt = @()
        foreach ($artifactPath in $ArtifactPaths) {
            if (Test-Path -LiteralPath $artifactPath) {
                $pathsForReceipt += (Resolve-Path -LiteralPath $artifactPath).Path
            }
        }

        Write-Receipt -Path $ReceiptPath -Paths $pathsForReceipt -SigningStatus "fail" -Reason $_.Exception.Message
    }

    throw
}
finally {
    if ($null -ne $temporaryPfxPath -and (Test-Path -LiteralPath $temporaryPfxPath)) {
        Remove-Item -LiteralPath $temporaryPfxPath -Force -ErrorAction SilentlyContinue
    }
}

