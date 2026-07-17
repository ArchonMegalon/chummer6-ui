# Self-Hosted Downloads Runbook

Purpose: publish desktop artifacts to a self-hosted downloads surface and verify that `/downloads/releases.json` serves non-empty artifacts.

For the full browser-client stack that serves `Chummer.Blazor` behind `Chummer.Portal`, use [BLAZOR_SELF_HOST_RUNBOOK.md](BLAZOR_SELF_HOST_RUNBOOK.md). This downloads runbook stays focused on the release shelf itself.

Registry note:
`/downloads/releases.json` is now a compatibility projection.
The canonical promoted release record is `RELEASE_CHANNEL.generated.json`, materialized by `chummer6-hub-registry`.

Release-build handoff note:
When a local latest-build bundle exists but is not yet promotable on every required desktop platform, materialize a bounded handoff first:

```bash
python3 scripts/materialize_release_candidate_handoff.py <stageDir>
```

That produces `RELEASE_BUILD_HANDOFF.generated.{json,md}` beside the staged bundle and makes the remaining promotion blockers explicit.
When the only remaining Windows blocker is host capture, that same handoff now also materializes `WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.{json,md}` for the exact staged shelf so the Windows operator can capture proof against the right bytes.
Treat that handoff as staged-nightly-only evidence: it refreshes staged receipts, but it does not publish the live downloads shelf and does not change the stable channel by itself.

## Prerequisites

1. Desktop bundle exists (`desktop-download-bundle` layout):
`RELEASE_CHANNEL.generated.json`, `releases.json`, and `files/chummer-*.zip|tar.gz|exe`.
2. Portal serves `/downloads/releases.json` from your storage topology and should carry the registry-owned `RELEASE_CHANNEL.generated.json` beside it.
3. Use preapproved runbook/script paths from repository root (`/docker/chummer5a`).
4. Optional unattended overrides:
`RUNBOOK_LOG_DIR` pins runbook log files to a known writable directory and `RUNBOOK_STATE_DIR` pins writable state (for example `DOTNET_CLI_HOME`) to a known writable directory.
5. Startup-smoke receipts copied during publish must be fresh and not future-skewed (default max age: `86400` seconds, default max future skew: `300` seconds). Override with `CHUMMER_PUBLISH_STARTUP_SMOKE_MAX_AGE_SECONDS` / `CHUMMER_PUBLISH_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS` (or shared `CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_AGE_SECONDS` / `CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS`) only when the release lane explicitly approves adjusted evidence windows.
6. Mainline `Desktop Downloads Matrix` runs on `main` resolve to `preview` automatically for the rolling Windows/Linux shelf. Set `CHUMMER_DESKTOP_RELEASE_CHANNEL` only when you intentionally want a non-mainline `docker`, `release_candidate`, or `public_stable` lane.
7. Set `CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE=true` only when you are intentionally publishing an unsigned public build. Without that override, `public_stable` and any explicit `release_candidate` lane fail closed unless the workflow can emit signing receipts:
`CHUMMER_WINDOWS_SIGN_PFX_BASE64` / `CHUMMER_WINDOWS_SIGN_PFX_PASSWORD` for Windows Authenticode, plus either a preconfigured mac keychain identity/profile, a hosted-signing P12 (`CHUMMER_MAC_CERTIFICATE_P12_BASE64` / `CHUMMER_MAC_CERTIFICATE_PASSWORD` / `CHUMMER_MAC_KEYCHAIN_PASSWORD` / `CHUMMER_MAC_APPLE_ID` / `CHUMMER_MAC_APPLE_APP_PASSWORD` / `CHUMMER_MAC_TEAM_ID`), or the persistent local-keychain fallback for Mac-hosted preview lanes (`CHUMMER_MAC_KEYCHAIN_PATH`, `CHUMMER_MAC_LOCAL_KEYCHAIN_PASSWORD`, `CHUMMER_MAC_LOCAL_CERT_COMMON_NAME`).
8. `public_stable` promotion also requires fresh root blocker truth from `RELEASE_BLOCKERS.generated.json`. The default max age is `86400` seconds via `CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS`; override it only when the release lane explicitly approves an adjusted blocker-truth window.

## Recommended Production Topology

1. Default recommendation: use `CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR` with a self-hosted runner that can write directly into the portal downloads storage mount.
2. Reason: this keeps `/downloads/` self-hosted, lets the deploy job verify both the local manifest file and the live portal manifest, and matches the canonical topology enforced in repo docs.
3. Treat object storage as the alternate topology for environments where the runner cannot write to portal storage directly; keep portal proxying and live manifest verification enabled there too.
4. Start from [`docs/examples/self-hosted-downloads.env.example`](examples/self-hosted-downloads.env.example) and adapt it to your portal base URL and storage target.

## Mode A: Filesystem Deploy (shared mount)

Repository variables:
1. `CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR`
2. `CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL`
3. Optional `CHUMMER_DESKTOP_RELEASE_CHANNEL` override for non-mainline lanes
4. `CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE` (optional; explicit unsigned public-release posture)
5. `CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS` (optional; only when the release lane explicitly approves an adjusted blocker-truth window)

Local release path:
1. Push the release-ready source to `main`, then build the release bundle on the controlled release host.
2. If `CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR` is configured, run `RUNBOOK_MODE=downloads-sync` after bundle generation.
3. `scripts/publish-download-bundle.sh` prunes superseded desktop artifacts from the target downloads root before syncing the freshly built bundle.
4. The runbook verifies the local deployed manifest and live manifest URL.

Manual path:
1. `RUNBOOK_MODE=downloads-sync DOWNLOAD_BUNDLE_DIR=<bundleDir> DOWNLOAD_DEPLOY_DIR=<deployDir> DOWNLOADS_SYNC_DEPLOY_MODE=1 DOWNLOADS_SYNC_VERIFY_TARGET=<portalBaseOrManifestUrl> bash scripts/runbook.sh`
2. `RUNBOOK_MODE=downloads-verify DOWNLOADS_VERIFY_LINKS=1 DOWNLOADS_VERIFY_TARGET=<portalBaseOrManifestUrl> bash scripts/runbook.sh`
3. `RUNBOOK_MODE=downloads-smoke bash scripts/runbook.sh`

## Mode B: Object Storage Deploy (S3/R2 compatible)

Repository variables:
1. `CHUMMER_PORTAL_DOWNLOADS_S3_URI`
2. `CHUMMER_PORTAL_DOWNLOADS_S3_LATEST_URI` (optional)
3. `CHUMMER_PORTAL_DOWNLOADS_S3_ENDPOINT_URL` (optional; required for many R2/S3-compatible endpoints)
4. `CHUMMER_PORTAL_DOWNLOADS_S3_REGION` (optional, defaults to `us-east-1`)
5. `CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL`
6. Optional `CHUMMER_DESKTOP_RELEASE_CHANNEL` override for non-mainline lanes
7. `CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE` (optional; explicit unsigned public-release posture)
8. `CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS` (optional; only when the release lane explicitly approves an adjusted blocker-truth window)

Repository secrets:
1. `CHUMMER_PORTAL_DOWNLOADS_AWS_ACCESS_KEY_ID`
2. `CHUMMER_PORTAL_DOWNLOADS_AWS_SECRET_ACCESS_KEY`
3. `CHUMMER_PORTAL_DOWNLOADS_AWS_SESSION_TOKEN` (optional)

Local release path:
1. Push the release-ready source to `main`, then build the release bundle on the controlled release host.
2. If `CHUMMER_PORTAL_DOWNLOADS_S3_URI` is configured, run `RUNBOOK_MODE=downloads-sync-s3` after bundle generation.
3. The runbook syncs the bundle using `scripts/publish-download-bundle-s3.sh`.
4. The runbook verifies the live manifest URL.

Manual path:
1. `RUNBOOK_MODE=downloads-sync-s3 DOWNLOAD_BUNDLE_DIR=<bundleDir> CHUMMER_PORTAL_DOWNLOADS_S3_URI=<s3://bucket/path> CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL=<portalBaseOrManifestUrl> [CHUMMER_PORTAL_DOWNLOADS_S3_ENDPOINT_URL=<endpoint>] bash scripts/runbook.sh`
2. `RUNBOOK_MODE=downloads-verify DOWNLOADS_VERIFY_LINKS=1 DOWNLOADS_VERIFY_TARGET=<portalBaseOrManifestUrl> bash scripts/runbook.sh`

## Mode C: Live `chummer.run` HTTP Publish

Use this mode when the release lane must promote the newest desktop bundle directly into the live `chummer.run` shelf instead of a mounted filesystem target or object store.

Repository variables and secrets:
1. `CHUMMER_RELEASE_UPLOAD_URL`
2. `CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL`
3. `CHUMMER_RELEASE_UPLOAD_TOKEN`
4. Optional `CHUMMER_DESKTOP_RELEASE_CHANNEL` override for non-mainline lanes
5. `CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE` (optional; set to `true` only when you deliberately want an unsigned public build)
6. `CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS` (optional; only when the release lane explicitly approves an adjusted blocker-truth window)
7. Windows public release secrets: `CHUMMER_WINDOWS_SIGN_PFX_BASE64`, `CHUMMER_WINDOWS_SIGN_PFX_PASSWORD`
8. macOS public release secrets/vars for hosted signing: `CHUMMER_MAC_CERTIFICATE_P12_BASE64`, `CHUMMER_MAC_CERTIFICATE_PASSWORD`, `CHUMMER_MAC_KEYCHAIN_PASSWORD`, `CHUMMER_MAC_APPLE_ID`, `CHUMMER_MAC_APPLE_APP_PASSWORD`, `CHUMMER_MAC_TEAM_ID`
9. Optional preconfigured mac runner vars: `CHUMMER_MAC_APP_SIGN_IDENTITY`, `CHUMMER_MAC_NOTARY_PROFILE`
10. Optional persistent local-preview vars on a Mac host when no P12 is configured: `CHUMMER_MAC_KEYCHAIN_PATH`, `CHUMMER_MAC_LOCAL_KEYCHAIN_PASSWORD`, `CHUMMER_MAC_LOCAL_CERT_COMMON_NAME`

Local-preview note:
1. `scripts/prepare-macos-signing-keychain.sh` now defaults the local bootstrap keychain to `~/Library/Keychains/chummer-signing.keychain-db` on macOS.
2. When no P12 and no preconfigured `CHUMMER_MAC_APP_SIGN_IDENTITY` are present, the script reuses or creates a self-signed local code-signing identity in that persistent keychain.
3. This preserves the signing identity across upgrades on a stable Mac host so app-permission prompts do not churn between preview installs.

Local release path:
1. Push the release-ready source to `main`, then run the affected local build and release scripts from the controlled release host.
2. Pushes do not publish the downloads shelf. `RUNBOOK_MODE=publish-latest-nightly` owns the scheduled nightly publication path and only publishes during the 08:00 Europe/Vienna release window.
3. Manual build/proof runs do not publish by default. Publish only through the guarded runbook mode or an explicit emergency override.
4. If `CHUMMER_RELEASE_UPLOAD_URL` is configured and the release window allows publication, `RUNBOOK_MODE=downloads-upload-http` uploads the finished desktop bundle with `scripts/publish-download-bundle-http.sh`.
5. The runbook verifies the live `RELEASE_CHANNEL.generated.json` response from `chummer.run`.

Manual path:
1. `RUNBOOK_MODE=downloads-upload-http DOWNLOAD_BUNDLE_DIR=<bundleDir> CHUMMER_RELEASE_UPLOAD_URL=<uploadUrl> CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL=<portalManifestUrl> CHUMMER_RELEASE_UPLOAD_TOKEN=<token> bash scripts/runbook.sh`
2. `RUNBOOK_MODE=downloads-verify DOWNLOADS_VERIFY_LINKS=1 DOWNLOADS_VERIFY_TARGET=<portalBaseOrManifestUrl> bash scripts/runbook.sh`
3. Local host shortcut for the newest staged nightly:
`RUNBOOK_MODE=publish-latest-nightly bash scripts/runbook.sh`
This command is guarded by the same daily cadence. It exits without publishing before 08:00 Europe/Vienna or when the downloads shelf was already published today. Use `CHUMMER_FORCE_NIGHTLY_PUBLISH=1` only for an explicit emergency/operator override of that cadence; force does not bypass installer eligibility or release proof gates.
4. The generic public-nightly lane requires at least one staged `open_public` Windows or Linux installer whose platform is `promoted_release` in `.codex-design/product/DESKTOP_PLATFORM_ACCEPTANCE_MATRIX.yaml`. A macOS-only, account-gated, hidden, quarantined, or support-only artifact set cannot replace the downloadable shelf.
5. To refresh and inspect the newest staged handoff without publishing, use `CHUMMER_NIGHTLY_SUPPORT_PROOF_ONLY_HANDOFF=1 RUNBOOK_MODE=publish-latest-nightly bash scripts/runbook.sh`. This narrowly scoped support/proof-only mode skips the public cadence check, materializes the handoff, validates stage scope, and exits before public-nightly eligibility checks, deploy synchronization, edge redeploy, or any public publication claim.

Release-build handoff expectation:
1. If a staged latest-build bundle verifies but still lists `missingRequiredPlatforms` for the public Windows/Linux promotion scope, do not promote it to `public_stable`.
2. Materialize the release-build handoff and finish the missing platform smoke/signing/upload work first.
3. A completed staged nightly handoff is still not a stable release. The live downloads shelf remains unchanged until a separate guarded publish lane runs.

Operational rule:
1. The public `chummer.run` shelf is a rolling daily shelf. It should advance once per day in the morning release window after the required proof passes, not after every local build.
2. Build only what the proof needs. Local work should use targeted tests and the affected platform; the full public Windows `win-x64` plus Linux `linux-x64` package set is for scheduled release proof or explicit override.
3. Mainline rolling release scope is Windows `win-x64` and Linux `linux-x64`. macOS may still build and publish bounded artifacts, but it must not block the public Windows/Linux shelf from advancing.
4. `preview` is the rolling release lane for mainline Windows/Linux builds. `public_stable` remains explicit promotion only.
5. Public-nightly eligibility is sourced from `.codex-design/product/DESKTOP_PLATFORM_ACCEPTANCE_MATRIX.yaml`; keep Windows/Linux `public_shelf_status` and `primary_package_kind` changes synchronized with the artifact rows emitted by the release materializer.
6. Public channels are proof-backed, not best-effort. If the resolved channel is `release_candidate` or `public_stable`, the workflow must either:
emit Windows signing and macOS signing/notarization receipts, or
run with `CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE=true` so the public-promotion evidence records `unsigned_public_release` explicitly.
7. `public_stable` publication also requires fresh root blocker truth from `RELEASE_BLOCKERS.generated.json`; the default max age is `86400` seconds via `CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS`.

## Strict Test Gate Commands (host-side)

Use these when you want hard failures instead of soft-skips.

Prerequisite probe:
1. `RUNBOOK_MODE=host-prereqs bash scripts/runbook.sh`

Single wrapper command:
1. `bash scripts/runbook-strict-host-gates.sh [optionalTestFilter] [optionalFramework]`
2. If no framework is provided, strict wrapper defaults to `net10.0` to keep host runs on the cross-platform test leg.
3. Local strict stage defaults to `FullyQualifiedName!~Chummer.Tests.ApiIntegrationTests&FullyQualifiedName!~Chummer.Tests.ChummerTest`; dual-head acceptance is now mandatory in the default local strict lane. Override with `TEST_LOCAL_FILTER` only when you are intentionally narrowing scope.
4. Wrapper fails when tracked `git` worktree state changes during the run; set `STRICT_ALLOW_WORKTREE_DRIFT=1` only when this is intentionally expected.

Local tests:
1. `RUNBOOK_MODE=local-tests TEST_NUGET_SOFT_FAIL=0 TEST_DISABLE_BUILD_SERVERS=1 TEST_MAX_CPU=1 bash scripts/runbook.sh`
2. Optional offline attempt after successful restore cache: `RUNBOOK_MODE=local-tests TEST_NO_RESTORE=1 TEST_DISABLE_BUILD_SERVERS=1 TEST_MAX_CPU=1 bash scripts/runbook.sh`

Docker tests:
1. `RUNBOOK_MODE=docker-tests DOCKER_TESTS_SOFT_FAIL=0 DOCKER_TESTS_BUILD=1 bash scripts/runbook.sh`

## Expected Verification Outcome

1. `/downloads/releases.json` has `downloads` with at least one artifact.
2. `version` is not `"unpublished"` in deployment mode.
3. When `CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS=true` (or `DOWNLOADS_VERIFY_LINKS=1`), each artifact URL/file in manifest verification is reachable.
4. Portal `/downloads/` renders artifact links that return HTTP 200.

## Portal Status Meanings

The portal manifest/page now distinguishes operator states explicitly:

1. `published`: real self-hosted artifacts are available.
2. `unpublished`: manifest is intentionally empty; no builds have been published yet.
3. `manifest-empty`: manifest exists but lists zero artifacts; treat this as a deployment/manifest generation problem.
4. `manifest-missing`: portal cannot find the self-hosted manifest or local artifacts.
5. `manifest-error`: portal found `releases.json` but could not parse it.
6. `fallback-source`: portal is using `CHUMMER_PORTAL_DOWNLOADS_FALLBACK_URL` instead of self-hosted artifacts.

Operational expectation:

1. Production/self-hosted deploys should end in `published`.
2. `unpublished` is acceptable only before the first release or in local-dev output that intentionally keeps the repo fallback snapshot.
3. `manifest-empty`, `manifest-missing`, and `manifest-error` should be treated as operator failures, not user-facing “normal empty state”.
4. Published portal builds do not ship the checked-in `Chummer.Portal/downloads/releases.json` snapshot, so a missing storage mount should surface as `manifest-missing`, not as a fake `unpublished` release feed.
