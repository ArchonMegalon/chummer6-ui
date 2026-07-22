[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $ArtifactPath,
    [Parameter(Mandatory = $true)][string] $ExpectedArtifactSha256,
    [Parameter(Mandatory = $true)][long] $ExpectedArtifactSizeBytes,
    [Parameter(Mandatory = $true)][string] $ExpectedSignerCertificateSha256,
    [Parameter(Mandatory = $true)][string] $ExpectedSignerSpkiSha256,
    [Parameter(Mandatory = $true)][string] $OutputPath,
    [Parameter(Mandatory = $true)][string] $SourceRepository,
    [Parameter(Mandatory = $true)][string] $SourceWorkflow,
    [Parameter(Mandatory = $true)][string] $SourceRunId,
    [Parameter(Mandatory = $true)][string] $SourceRunAttempt,
    [Parameter(Mandatory = $true)][string] $SourceRef,
    [Parameter(Mandatory = $true)][string] $SourceSha,
    [Parameter(Mandatory = $true)][string] $SourceActor
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ContractName = 'chummer6-ui.windows-authenticode-verification'
$CodeSigningEkuOid = '1.3.6.1.5.5.7.3.3'
$TimestampingEkuOid = '1.3.6.1.5.5.7.3.8'
$Rfc3161AttributeOid = '1.2.840.113549.1.9.16.2.14'
$Sha256Oid = '2.16.840.1.101.3.4.2.1'
$ImplementationPath = 'scripts/verify-windows-authenticode.ps1'

function Require-LowerSha256([string] $Value, [string] $Label) {
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -cnotmatch '^[0-9a-f]{64}$') {
        throw "$Label must be an exact lowercase SHA-256."
    }
    return $Value
}

function Require-ExactText([string] $Value, [string] $Label) {
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -cne $Value.Trim()) {
        throw "$Label must be nonempty exact text."
    }
    return $Value
}

function Get-Sha256Hex([byte[]] $Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return [Convert]::ToHexString($sha.ComputeHash($Bytes)).ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-CertificateSha256([Security.Cryptography.X509Certificates.X509Certificate2] $Certificate) {
    return Get-Sha256Hex $Certificate.RawData
}

function Get-SpkiSha256([Security.Cryptography.X509Certificates.X509Certificate2] $Certificate) {
    return Get-Sha256Hex $Certificate.ExportSubjectPublicKeyInfo()
}

function Require-Eku(
    [Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,
    [string] $RequiredOid,
    [string] $Label
) {
    $ekuExtensions = @($Certificate.Extensions | Where-Object {
        $_.Oid.Value -ceq '2.5.29.37'
    })
    if ($ekuExtensions.Count -ne 1) {
        throw "$Label must contain exactly one enhanced-key-usage extension."
    }
    $eku = [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new(
        $ekuExtensions[0], $false
    )
    $oids = @($eku.EnhancedKeyUsages | ForEach-Object { $_.Value })
    if ($RequiredOid -cnotin $oids) {
        throw "$Label lacks required EKU $RequiredOid."
    }
}

function Test-TrustedChain(
    [Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,
    [DateTimeOffset] $VerificationTime,
    [string] $Label
) {
    $chain = [Security.Cryptography.X509Certificates.X509Chain]::new()
    try {
        $chain.ChainPolicy.RevocationMode = [Security.Cryptography.X509Certificates.X509RevocationMode]::Online
        $chain.ChainPolicy.RevocationFlag = [Security.Cryptography.X509Certificates.X509RevocationFlag]::EntireChain
        $chain.ChainPolicy.VerificationFlags = [Security.Cryptography.X509Certificates.X509VerificationFlags]::NoFlag
        $chain.ChainPolicy.VerificationTime = $VerificationTime.UtcDateTime
        $chain.ChainPolicy.UrlRetrievalTimeout = [TimeSpan]::FromSeconds(30)
        $trusted = $chain.Build($Certificate)
        $statuses = @($chain.ChainStatus | ForEach-Object {
            [ordered]@{
                status = $_.Status.ToString()
                information = $_.StatusInformation.Trim()
            }
        })
        if (-not $trusted -or $statuses.Count -ne 0) {
            $summary = ($statuses | ForEach-Object { "$($_.status):$($_.information)" }) -join '; '
            throw "$Label certificate chain is not trusted with online whole-chain revocation: $summary"
        }
        return [ordered]@{
            trusted = $true
            revocationMode = 'online'
            revocationFlag = 'entire_chain'
            verificationFlags = 'no_flag'
            verificationTimeUtc = $VerificationTime.UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
            status = @()
        }
    }
    finally {
        $chain.Dispose()
    }
}

function Read-AuthenticodeCms([string] $Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 256 -or $bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a) {
        throw 'Artifact is not a structurally valid PE image.'
    }
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
    if ($peOffset -lt 0x40 -or $peOffset + 24 -gt $bytes.Length) {
        throw 'PE header offset is outside the artifact.'
    }
    if ($bytes[$peOffset] -ne 0x50 -or $bytes[$peOffset + 1] -ne 0x45 `
        -or $bytes[$peOffset + 2] -ne 0 -or $bytes[$peOffset + 3] -ne 0) {
        throw 'PE signature is invalid.'
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
        throw 'PE optional-header magic is unsupported.'
    }
    $securityEntry = $dataDirectoryOffset + (4 * 8)
    if ($securityEntry + 8 -gt $optionalOffset + $optionalSize `
        -or $securityEntry + 8 -gt $bytes.Length) {
        throw 'PE security data-directory entry is absent.'
    }
    $certificateOffset = [BitConverter]::ToUInt32($bytes, $securityEntry)
    $certificateSize = [BitConverter]::ToUInt32($bytes, $securityEntry + 4)
    if ($certificateOffset -eq 0 -or $certificateSize -lt 9 `
        -or [uint64]$certificateOffset + [uint64]$certificateSize -gt [uint64]$bytes.Length) {
        throw 'PE Authenticode WIN_CERTIFICATE is absent or truncated.'
    }
    $declaredSize = [BitConverter]::ToUInt32($bytes, [int]$certificateOffset)
    $revision = [BitConverter]::ToUInt16($bytes, [int]$certificateOffset + 4)
    $certificateType = [BitConverter]::ToUInt16($bytes, [int]$certificateOffset + 6)
    $alignedDeclaredSize = ([uint64]$declaredSize + 7) -band ([uint64]::MaxValue - 7)
    if ($declaredSize -lt 9 -or $declaredSize -gt $certificateSize `
        -or $alignedDeclaredSize -ne $certificateSize `
        -or $revision -ne 0x0200 -or $certificateType -ne 0x0002) {
        throw 'PE must contain exactly one aligned PKCS#7 Authenticode WIN_CERTIFICATE.'
    }
    $cmsBytes = [byte[]]::new($declaredSize - 8)
    [Array]::Copy($bytes, [int]$certificateOffset + 8, $cmsBytes, 0, $cmsBytes.Length)
    $cms = [Security.Cryptography.Pkcs.SignedCms]::new()
    $cms.Decode($cmsBytes)
    if ($cms.SignerInfos.Count -ne 1) {
        throw 'Authenticode PKCS#7 must contain exactly one signer.'
    }
    $cms.CheckSignature($true)
    if ($cms.SignerInfos[0].DigestAlgorithm.Value -cne $Sha256Oid) {
        throw 'Authenticode PKCS#7 signer must use SHA-256.'
    }
    return $cms
}

function Read-Rfc3161Timestamp(
    [Security.Cryptography.Pkcs.SignerInfo] $AuthenticodeSigner
) {
    $attributes = @($AuthenticodeSigner.UnsignedAttributes | Where-Object {
        $_.Oid.Value -ceq $Rfc3161AttributeOid
    })
    if ($attributes.Count -ne 1 -or $attributes[0].Values.Count -ne 1) {
        throw 'Authenticode signer must contain exactly one RFC3161 timestamp token.'
    }
    $timestampCms = [Security.Cryptography.Pkcs.SignedCms]::new()
    $timestampCms.Decode($attributes[0].Values[0].RawData)
    if ($timestampCms.SignerInfos.Count -ne 1) {
        throw 'RFC3161 timestamp token must contain exactly one signer.'
    }
    $timestampCms.CheckSignature($true)
    if ($timestampCms.ContentInfo.ContentType.Value -cne '1.2.840.113549.1.9.16.1.4' `
        -or $timestampCms.SignerInfos[0].DigestAlgorithm.Value -cne $Sha256Oid) {
        throw 'RFC3161 token must contain SHA-256-signed TSTInfo content.'
    }

    $reader = [System.Formats.Asn1.AsnReader]::new(
        $timestampCms.ContentInfo.Content,
        [System.Formats.Asn1.AsnEncodingRules]::DER
    )
    $sequence = $reader.ReadSequence()
    [void]$sequence.ReadInteger()
    [void]$sequence.ReadObjectIdentifier()
    $messageImprint = $sequence.ReadSequence()
    $algorithm = $messageImprint.ReadSequence()
    $algorithmOid = $algorithm.ReadObjectIdentifier()
    if ($algorithm.HasData) {
        $algorithm.ReadNull()
    }
    if ($algorithm.HasData) { throw 'RFC3161 message-imprint algorithm has trailing fields.' }
    $imprint = $messageImprint.ReadOctetString()
    if ($messageImprint.HasData) { throw 'RFC3161 message imprint has trailing fields.' }
    [void]$sequence.ReadInteger()
    $generatedAt = $sequence.ReadGeneralizedTime()
    if ($algorithmOid -cne $Sha256Oid) {
        throw 'RFC3161 message imprint must use SHA-256.'
    }
    $expectedImprint = Get-Sha256Hex $AuthenticodeSigner.GetSignature()
    $actualImprint = [Convert]::ToHexString($imprint).ToLowerInvariant()
    if ($actualImprint -cne $expectedImprint) {
        throw 'RFC3161 message imprint does not bind the Authenticode signer signature.'
    }
    return [ordered]@{
        cms = $timestampCms
        signer = $timestampCms.SignerInfos[0]
        generatedAt = $generatedAt
        messageImprintSha256 = $actualImprint
    }
}

$expectedSha = Require-LowerSha256 $ExpectedArtifactSha256 'Expected artifact SHA-256'
$expectedSignerCertificateSha = Require-LowerSha256 `
    $ExpectedSignerCertificateSha256 'Pinned signer certificate SHA-256'
$expectedSignerSpkiSha = Require-LowerSha256 `
    $ExpectedSignerSpkiSha256 'Pinned signer SPKI SHA-256'
if ($ExpectedArtifactSizeBytes -lt 1) { throw 'Expected artifact size must be positive.' }
$sourceFields = [ordered]@{
    'source repository' = $SourceRepository
    'source workflow' = $SourceWorkflow
    'source run ID' = $SourceRunId
    'source run attempt' = $SourceRunAttempt
    'source ref' = $SourceRef
    'source SHA' = $SourceSha
    'source actor' = $SourceActor
}
foreach ($sourceField in $sourceFields.GetEnumerator()) {
    [void](Require-ExactText $sourceField.Value $sourceField.Key)
}
if ($SourceRunId -cnotmatch '^[1-9][0-9]*$' -or $SourceRunAttempt -cnotmatch '^[1-9][0-9]*$') {
    throw 'Source run ID and attempt must be positive integer strings.'
}
if ($SourceSha -cnotmatch '^[0-9a-f]{40}$') { throw 'Source SHA must be an exact commit.' }

$resolvedArtifact = (Resolve-Path -LiteralPath $ArtifactPath -ErrorAction Stop).Path
$artifact = Get-Item -LiteralPath $resolvedArtifact -Force
if (-not ($artifact -is [IO.FileInfo]) -or $artifact.LinkType) {
    throw 'Artifact must be one regular non-link file.'
}
if ($artifact.Length -ne $ExpectedArtifactSizeBytes) {
    throw 'Artifact size differs from the held candidate authority.'
}
$artifactSha = (Get-FileHash -LiteralPath $resolvedArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
if ($artifactSha -cne $expectedSha) {
    throw 'Artifact SHA-256 differs from the held candidate authority.'
}

Add-Type -AssemblyName System.Security.Cryptography.Pkcs
$signature = Get-AuthenticodeSignature -LiteralPath $resolvedArtifact
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid `
    -or $signature.SignatureType.ToString() -cne 'Authenticode' `
    -or $null -eq $signature.SignerCertificate `
    -or $null -eq $signature.TimeStamperCertificate) {
    throw "Authenticode signature, signer, and timestamp must all be independently valid: $($signature.Status)"
}
$signer = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $signature.SignerCertificate.RawData
)
$timestampCertificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $signature.TimeStamperCertificate.RawData
)
$signerCertificateSha = Get-CertificateSha256 $signer
$signerSpkiSha = Get-SpkiSha256 $signer
if ($signerCertificateSha -cne $expectedSignerCertificateSha) {
    throw 'Validated Authenticode signer certificate differs from the pinned policy identity.'
}
if ($signerSpkiSha -cne $expectedSignerSpkiSha) {
    throw 'Validated Authenticode signer SPKI differs from the pinned policy identity.'
}
Require-Eku $signer $CodeSigningEkuOid 'Authenticode signer certificate'
Require-Eku $timestampCertificate $TimestampingEkuOid 'RFC3161 timestamp certificate'

$authenticodeCms = Read-AuthenticodeCms $resolvedArtifact
$cmsSignerCertificate = $authenticodeCms.SignerInfos[0].Certificate
if ($null -eq $cmsSignerCertificate `
    -or (Get-CertificateSha256 $cmsSignerCertificate) -cne $signerCertificateSha) {
    throw 'Parsed Authenticode CMS signer differs from Get-AuthenticodeSignature.'
}
$rfc3161 = Read-Rfc3161Timestamp $authenticodeCms.SignerInfos[0]
$timestampSignerCertificate = $rfc3161.signer.Certificate
if ($null -eq $timestampSignerCertificate) {
    throw 'RFC3161 timestamp signer certificate is absent.'
}
$timestampCertificateSha = Get-CertificateSha256 $timestampCertificate
if ((Get-CertificateSha256 $timestampSignerCertificate) -cne $timestampCertificateSha) {
    throw 'RFC3161 token signer differs from Get-AuthenticodeSignature timestamp authority.'
}
$timestampUtc = ([DateTimeOffset]$rfc3161.generatedAt).ToUniversalTime()
$now = [DateTimeOffset]::UtcNow
if ($timestampUtc -gt $now.AddMinutes(5)) { throw 'RFC3161 timestamp is in the future.' }
if ($timestampUtc -lt [DateTimeOffset]$signer.NotBefore.ToUniversalTime() `
    -or $timestampUtc -gt [DateTimeOffset]$signer.NotAfter.ToUniversalTime()) {
    throw 'RFC3161 timestamp is outside the signer certificate validity interval.'
}
if ($timestampUtc -lt [DateTimeOffset]$timestampCertificate.NotBefore.ToUniversalTime() `
    -or $timestampUtc -gt [DateTimeOffset]$timestampCertificate.NotAfter.ToUniversalTime()) {
    throw 'RFC3161 timestamp is outside the timestamp certificate validity interval.'
}
$signerChain = Test-TrustedChain $signer $timestampUtc 'Authenticode signer'
$timestampChain = Test-TrustedChain $timestampCertificate $timestampUtc 'RFC3161 timestamp signer'

$implementationSha = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$receipt = [ordered]@{
    contractName = $ContractName
    contractVersion = 1
    status = 'verified'
    generatedAt = $now.ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
    artifact = [ordered]@{
        fileName = $artifact.Name
        sha256 = $artifactSha
        sizeBytes = [long]$artifact.Length
    }
    source = [ordered]@{
        repository = $SourceRepository
        workflow = $SourceWorkflow
        runId = $SourceRunId
        runAttempt = $SourceRunAttempt
        ref = $SourceRef
        sha = $SourceSha
        actor = $SourceActor
    }
    policy = [ordered]@{
        signerCertificateSha256 = $expectedSignerCertificateSha
        signerSpkiSha256 = $expectedSignerSpkiSha
    }
    signature = [ordered]@{
        status = 'valid'
        type = 'authenticode'
        cryptographicVerification = 'passed'
        codeSigningEkuOid = $CodeSigningEkuOid
    }
    signer = [ordered]@{
        certificateSha256 = $signerCertificateSha
        spkiSha256 = $signerSpkiSha
        subject = $signer.Subject
        issuer = $signer.Issuer
        serialNumber = $signer.SerialNumber
        notBeforeUtc = ([DateTimeOffset]$signer.NotBefore.ToUniversalTime()).ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
        notAfterUtc = ([DateTimeOffset]$signer.NotAfter.ToUniversalTime()).ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
        chain = $signerChain
    }
    timestamp = [ordered]@{
        status = 'verified'
        format = 'rfc3161'
        attributeOid = $Rfc3161AttributeOid
        generatedAtUtc = $timestampUtc.ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
        messageImprintAlgorithmOid = $Sha256Oid
        messageImprintSha256 = $rfc3161.messageImprintSha256
        certificateSha256 = $timestampCertificateSha
        subject = $timestampCertificate.Subject
        issuer = $timestampCertificate.Issuer
        serialNumber = $timestampCertificate.SerialNumber
        notBeforeUtc = ([DateTimeOffset]$timestampCertificate.NotBefore.ToUniversalTime()).ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
        notAfterUtc = ([DateTimeOffset]$timestampCertificate.NotAfter.ToUniversalTime()).ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
        timestampingEkuOid = $TimestampingEkuOid
        chain = $timestampChain
    }
    verifier = [ordered]@{
        implementation = $ImplementationPath
        implementationSha256 = $implementationSha
        platform = 'windows'
        powershellVersion = $PSVersionTable.PSVersion.ToString()
    }
}

$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [IO.Path]::GetDirectoryName($resolvedOutput)
if ([string]::IsNullOrWhiteSpace($outputDirectory) -or -not [IO.Directory]::Exists($outputDirectory)) {
    throw 'Authenticode receipt output directory must already exist.'
}
$json = $receipt | ConvertTo-Json -Depth 12
$utf8NoBom = [Text.UTF8Encoding]::new($false)
$stream = [IO.File]::Open($resolvedOutput, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $bytes = $utf8NoBom.GetBytes($json + "`n")
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Flush($true)
}
finally {
    $stream.Dispose()
}
