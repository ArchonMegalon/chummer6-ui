# GitHub Codex Review

PR: https://github.com/ArchonMegalon/chummer6-ui/pull/11

Findings:
- [high] scripts/ai/verify.sh [state] verify-cross-repo-build-coupling
scripts/ai/verify.sh:10-11 unconditionally builds ../chummer-hub-registry and ../chummer.run-services projects before repo-local checks.; With set -euo pipefail, missing sibling checkouts/buildability fails default verify even when this repo is otherwise valid, creating an offline/local-state hazard.
Expected fix: Make sibling-repo builds opt-in and existence-gated (or remove from default verify path) so default verify remains repo-local and offline-safe.
- [high] Chummer.Tests/Chummer.Tests.csproj [contracts] tests-sibling-binary-hintpath-coupling
Chummer.Tests/Chummer.Tests.csproj:182-184 adds <Reference Include="Chummer.Hub.Registry.Contracts"> with HintPath to ../../chummer-hub-registry/.../bin/$(Configuration)/net10.0/Chummer.Hub.Registry.Contracts.dll.; This couples test compilation to external sibling build artifacts instead of canonical package/compatibility restore, causing nondeterministic bootstrap and offline failures.
Expected fix: Replace sibling-bin HintPath reference with package-based restore (or explicit compatibility-tree fallback) so tests build without external repo binaries.
- [high] Chummer.Tests/Compliance/MigrationComplianceTests.cs [tests] missing-verify-offline-regression-guard
MigrationComplianceTests.cs:2960+ validates strict B7 flags in verify.sh, but does not assert verify avoids unconditional sibling-repo dependencies.; No compliance guard fails when verify introduces hard-coded cross-repo build prerequisites.
Expected fix: Add a compliance test that fails if default verify includes unconditional sibling-repo build dependencies.
