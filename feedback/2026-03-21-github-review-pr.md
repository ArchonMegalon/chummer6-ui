# GitHub Codex Review

PR: https://github.com/ArchonMegalon/chummer6-ui/pull/11

Findings:
- [high] scripts/ai/verify.sh [state] verify-cross-repo-build-coupling
Default verify now unconditionally builds sibling repos: line 10 references `../chummer-hub-registry/...csproj` and line 11 references `../chummer.run-services/...csproj`.; With `set -euo pipefail`, missing sibling checkouts or offline-only environments fail before repo-local checks, creating a local-state/offline hazard.
Expected fix: Make cross-repo builds opt-in and existence-gated (or remove them from default verify), keeping `scripts/ai/verify.sh` repo-local and offline-safe by default.
- [high] Chummer.Tests/Chummer.Tests.csproj [contracts] tests-sibling-binary-hintpath-coupling
Lines 182-183 add an assembly reference with `HintPath` to `..\..\chummer-hub-registry\...\bin\$(Configuration)\net10.0\Chummer.Hub.Registry.Contracts.dll`.; This couples test compilation to external sibling build artifacts instead of package/compatibility-tree restore, causing nondeterministic bootstrap and offline failures.
Expected fix: Replace sibling-bin `HintPath` usage with canonical package consumption (or explicit compatibility-tree fallback) so tests restore/build without external repo binaries.
- [medium] Chummer.Tests/Compliance/MigrationComplianceTests.cs [tests] missing-offline-verify-regression-guard-test
The added compliance test `Verify_script_keeps_strict_connected_lane_defaults` checks B7 env defaults but does not assert that default `verify.sh` avoids unconditional external-repo dependencies.; Given the current verify-script coupling, there is no regression test explicitly preventing this offline/bootstrap hazard.
Expected fix: Add a compliance guard that fails if default verify introduces unconditional sibling-repo build dependencies.
