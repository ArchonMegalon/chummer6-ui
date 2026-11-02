# win004-start-failure-diagnostics.ps1
#
# Privacy-safe startup-failure diagnostics for the WIN-004 native journey.
#
# Re-audit handoff item 1 (2026-11-02, immediate P1): startup-failure
# diagnostics must never persist the child process environment. This collector
# records values only for an explicit allowlist of non-secret configuration
# keys, records presence/absence (never the value) for machine-specific
# configuration paths, and omits everything else entirely -- credential and
# token variables included: their names are not reported either.
#
# This file is deliberately the ONLY sanctioned path from a failing
# ProcessStartInfo to a diagnostic artifact. It accepts the environment as an
# IDictionary-shaped object and never enumerates the live process environment.

param()

function Write-StartFailureEnvironmentReport {
    <#
    .SYNOPSIS
        Write a sanitized environment report for a native-start failure.

    .DESCRIPTION
        Writes <Path> as JSON. Only allowlisted configuration is recorded:
          - CHUMMER_DESKTOP_UPDATE_ENABLED / _MODE / _AUTO_APPLY: values
            (non-secret harness knobs; the safe update mode is a required
            diagnostic).
          - CHUMMER_CORE_CONTENT_ROOT / CHUMMER_DESKTOP_UPDATE_MANIFEST /
            DOTNET_ROOT: configured true/false only (machine-specific paths
            and SDK override presence, never their values).
        Every other key contributes only to a nameless count. Unknown keys --
        including credentials -- cannot appear in the output in any form.
    #>
    param(
        # The failing process's environment (e.g. $psi.EnvironmentVariables,
        # a StringDictionary, Hashtable, or generic Dictionary). Read-only;
        # never mutated, never fully enumerated into the report.
        [Parameter(Mandatory = $true)]
        [object]$Environment,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $ErrorActionPreference = 'Stop'

    # Keys whose VALUE is safe (and useful) to record verbatim.
    $valueKeys = @(
        'CHUMMER_DESKTOP_UPDATE_ENABLED',
        'CHUMMER_DESKTOP_UPDATE_MODE',
        'CHUMMER_DESKTOP_UPDATE_AUTO_APPLY'
    )
    # Keys recorded as presence/absence only (values are machine paths).
    $presenceKeys = @(
        'CHUMMER_CORE_CONTENT_ROOT',
        'CHUMMER_DESKTOP_UPDATE_MANIFEST',
        'DOTNET_ROOT'
    )

    $values = [ordered]@{}
    $presence = [ordered]@{}
    $known = @{}
    foreach ($k in $valueKeys) { $known[$k] = $true }
    foreach ($k in $presenceKeys) { $known[$k] = $true }

    $unlistedCount = 0
    foreach ($entry in $Environment.GetEnumerator()) {
        $name = [string]$entry.Key
        if ($known.ContainsKey($name)) {
            if ($valueKeys -contains $name) {
                $values[$name] = [string]$entry.Value
            } else {
                $presence[$name] = $true
            }
        } else {
            $unlistedCount++
        }
    }
    foreach ($k in $valueKeys) {
        if (-not $values.Contains($k)) { $values[$k] = $null }
    }
    foreach ($k in $presenceKeys) {
        if (-not $presence.Contains($k)) { $presence[$k] = $false }
    }

    $report = [ordered]@{
        report                       = 'win004-start-failure-environment'
        reportVersion                = 1
        generatedUtc                 = (Get-Date).ToUniversalTime().ToString('o')
        privacyPolicy                = 'allowlist-only; credential names and values are never recorded'
        environment                  = [ordered]@{
            coreContentOverrideConfigured = [bool]$presence['CHUMMER_CORE_CONTENT_ROOT']
            sdkOverrideConfigured         = [bool]$presence['DOTNET_ROOT']
            updateManifestConfigured      = [bool]$presence['CHUMMER_DESKTOP_UPDATE_MANIFEST']
            updateEnabled                 = $values['CHUMMER_DESKTOP_UPDATE_ENABLED']
            updateMode                    = $values['CHUMMER_DESKTOP_UPDATE_MODE']
            updateAutoApply               = $values['CHUMMER_DESKTOP_UPDATE_AUTO_APPLY']
        }
        unlistedEnvironmentKeyCount  = $unlistedCount
    }

    ($report | ConvertTo-Json -Depth 4) | Set-Content -Encoding UTF8 -Path $Path
}
