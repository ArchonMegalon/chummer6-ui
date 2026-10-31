# Explicit native Windows restore/build; the primary Avalonia head is the default.
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$CacheRoot,
    [string]$PackageFeed,
    [ValidateSet('primary', 'compatibility')]
    [string]$Project = 'primary'
)

$ErrorActionPreference = 'Stop'
# Use the same interpreter discovery and argument-array handoff as onboarding.
$parameters = @{ Mode = 'build'; RepoRoot = $RepoRoot; Project = $Project }
if ($CacheRoot) { $parameters.CacheRoot = $CacheRoot }
if ($PackageFeed) { $parameters.PackageFeed = $PackageFeed }
& (Join-Path $PSScriptRoot 'start-windows-worker.ps1') @parameters
exit $LASTEXITCODE
