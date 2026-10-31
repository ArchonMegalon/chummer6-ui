# Friday Code is started by the user and keeps its existing provider setup.
[CmdletBinding()]
param(
    [ValidateSet('prepare', 'check', 'next', 'setup', 'build')]
    [string]$Mode = 'next',
    [string]$WorkerId = $env:CHUMMER_WORKER_ID,
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$PromptPath,
    [string]$PackageFeed,
    [string]$CacheRoot,
    [string]$InputDir,
    [switch]$Download,
    [ValidateSet('primary', 'compatibility')]
    [string]$Project = 'primary'
)

$ErrorActionPreference = 'Stop'
try {
    # Use argument arrays throughout. Teable rows and environment values are data.
    $python = Get-Command python -ErrorAction SilentlyContinue
    $pythonPrefix = @()
    if (-not $python) {
        $python = Get-Command py -ErrorAction SilentlyContinue
        $pythonPrefix = @('-3')
    }
    if (-not $python) {
        throw 'Install Python 3.10 or later and enable python or py on PATH.'
    }
    $helper = Join-Path $RepoRoot 'scripts/prepare-agent-workspace.py'
    $arguments = $pythonPrefix + @($helper, '--mode', $Mode, '--repo-root', $RepoRoot)
    if ($WorkerId) { $arguments += @('--worker-id', $WorkerId) }
    if ($PromptPath) { $arguments += @('--prompt-path', $PromptPath) }
    if ($PackageFeed) { $arguments += @('--package-feed', $PackageFeed) }
    if ($CacheRoot) { $arguments += @('--cache-root', $CacheRoot) }
    if ($InputDir) { $arguments += @('--input-dir', $InputDir) }
    if ($Download) { $arguments += '--download' }
    if ($Mode -eq 'build') { $arguments += @('--project', $Project) }
    & $python.Source @arguments
    exit $LASTEXITCODE
}
catch {
    # Do not echo arbitrary exception text, command lines or environment values.
    Write-Error 'Windows onboarding could not start. Check Git, Python 3.10+, the repository path and docs/agents/WINDOWS_REMOTE.md.' -ErrorAction Continue
    exit 2
}
