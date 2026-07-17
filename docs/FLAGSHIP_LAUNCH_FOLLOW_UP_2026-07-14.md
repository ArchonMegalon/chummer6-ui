# Chummer flagship launch follow-up

Date: 2026-07-14

## Decision

Hosted launch status remains **NO-GO**.

The local build, ownership, PWA, companion, persistence, and canonicalization
gates described below are green. The public release shelf is not: a read-only
check observed externally published version `run-20260714-191136` with
`publishedAt=2026-07-14T19:31:13Z`, while the served manifest still reported:

- `publicTrustMetrics.proofFreshness.status=stale`
- top-level and nested `supportabilityState=preview_supported`
- `registryBoundaryCoverage.releaseChannel.publicTrustPosture=preview`
- the optimistic pre-publication rollout reason

This is the same truth regression reported for Attempts 3 through 10. This
hardening pass did not publish that newer version and did not deploy, purge a
cache, retry a release, or mutate hosted state.

Attempt 11 local run root:

`${CHUMMER_RELEASE_WORKSPACE}/run-20260714-191136`

Its bootstrap recovered from transient HTTP/2 cancellation, connection-reset,
early-EOF, and invalid index-pack clone failures. Build, package, startup smoke,
manifest validation, promotion evidence, bundle integrity, staged publication,
and payload-summary materialization still completed. The terminal failure was
again the live canonical-manifest supportability check, not a clone, build,
package, smoke, or upload failure.

## Root cause and local containment

The post-upload shelf projection in
`Chummer.Run.Api/Services/ReleaseBundlePromotionService.cs` historically
derived rollout and supportability from proof pass/channel coverage without
applying a floor from `publicTrustMetrics.proofFreshness.status`. That explains
why version and timestamp survived publication while supportability was
rewritten optimistically.

The current dirty `chummer.run-services` checkout already contains the upstream
freshness-aware projection fix. The presentation checkout also contains a
defence-in-depth canonical-manifest guard in
`Chummer.Portal/PortalCanonicalReleaseManifest.cs`. It floors stale, missing,
blank, unknown, or absent freshness to review-required/blocked, preserves a
revoked posture, uses strict UTF-8, and fails malformed or unavailable bytes
closed with `503` and no-store headers.

Local receipts:

- Run-services stale-proof promotion theory: 5/5 passed.
- Portal canonical-manifest runtime regression: 1/1 passed.
- Portal build: 0 warnings, 0 errors.

Neither containment is deployed, so it does not change the hosted decision.

Primary Attempt 11 repro artifacts:

- `${CHUMMER_RELEASE_WORKSPACE}/run-20260714-191136/r/dist/RELEASE_CHANNEL.generated.json`
- `${CHUMMER_RELEASE_WORKSPACE}/run-20260714-191136/r/dist/releases.json`
- `${CHUMMER_RELEASE_WORKSPACE}/run-20260714-191136/r/dist/release-upload-payload-summary.json`
- `${CHUMMER_RELEASE_WORKSPACE}/run-20260714-191136/r/dist/startup-smoke/startup-smoke-avalonia-osx-arm64.receipt.json`
- `${CHUMMER_RELEASE_WORKSPACE}/run-20260714-191136/r/dist/startup-smoke/startup-smoke-blazor-desktop-osx-arm64.receipt.json`

## Hosted Build ownership and authentication

- Exact RSA-signed JWT trust path, including wrong issuer, audience, signature,
  expiry, and multiple-audience rejection: passed.
- Hosted owner boundary: 28/28 passed.
- Reduced Blazor shell after all added regressions: 127/127 passed.
- Release Blazor build: 0 warnings, 0 errors.
- Health middleware executes before authentication, owner-grant, and PWA
  middleware; root and PathBase health responses set no owner cookie.

## Build PWA

- Exact-byte Release closure: 18 assets.
- Independently generated and checked revision:
  `15920d12f982edc2654900532892c859ce556288e333c9802923e5c6a91129ed`.
- Runtime source plus live root/PathBase checks: 4/4 passed.
- Static PWA suite: 38/38 passed.
- Install/responsive focused suite: 13/13 passed.
- Immutable cache lifecycle and integrity-token Node suites: passed.
- Real Chromium responsive/offline/cache-isolation proof: passed.
- Real Chromium integrity/update/focus/QR proof: passed.

The PathBase runtime proof found and removed a duplicate explicit Blazor hub
mapping that had produced an ambiguous `/_blazor/negotiate` endpoint and HTTP
500. Interactive Razor mapping is now the single hub owner.

## Play and companion install handoff

- Rebuilt Chummer Play: 0 warnings, 0 errors.
- Mobile install-only companion runtime: 4/4 passed.
- Landing handoff Python/static/Chromium proofs: 16/16 passed.
- Local TypeScript Playwright handoff proofs: 7/7 passed.
- Run API Release build for the handoff: 0 errors.

Auto selection uses standalone mode, UA-CH `mobile`, then coarse-pointer plus
touch capability. It does not sniff a UA string. Mobile opens `/build` or
`/mobile/player` directly; desktop opens a deterministic first-party QR dialog
with copy/open fallbacks. An accessible persisted Auto/Mobile/Desktop override
can force either route.

## Startup warmup

Startup no longer resolves owner-scoped presenters or clients from a generic
service scope. It warms only the owner-neutral ruleset catalog and waits for
`ApplicationStarted` before loopback owner-aware requests.

- Focused warmup tests: 4/4 passed.
- Warmup routes `/app` and `/workbench`: 200.
- Startup log contained no owner-context, invalid-scope, pre-listen connection
  refusal, or public-edge warning.

## Persistence and readiness

Hosted Build still uses `FileWorkspaceStore`; the existing PostgreSQL code is a
separate, deliberately dormant Play-authorization boundary and is not a Build
workspace provider.

The safe single-host tier is now explicit and fail-closed:

- Both Blazor compose services use explicit `/app/state` named volumes.
- Liveness is separate from readiness.
- Readiness performs an owner-scoped durable write/read/delete sentinel.
- Probe/store construction runs behind one lazy worker task.
- Public waits are bounded to two seconds, completed decisions are cached for
  five seconds, and exactly one blocked probe is retained.
- Health paths terminate before owner/auth/PWA middleware, set no-store, and do
  not create an owner cookie.
- API readiness independently performs a write/read/delete round trip against
  its private-mode state volume and fails closed if that boundary drifts.

Receipts:

- Focused persistence/readiness: 11/11 passed.
- Full workspace-store and DI classes: 22/22 passed.
- Root and `/blazor` real-host health smoke: passed.
- Thirty-two concurrent timed-out readiness requests produced one probe,
  sanitized 503 responses, no cookie, and later recovered.
- Writable state returned live/ready/health 200; a symlink fault kept liveness
  200 and returned a path-free readiness 503; restoring the state root recovered
  readiness to 200 without restart.
- Focused API readiness: 3/3 passed. Its exact container proof passed normal
  readiness, returned 503 on mode drift, recovered after repair, and preserved
  state across restart.
- Default, test-profile, portal-profile, and portal-e2e-profile Compose
  validation: passed.

## Container and private-edge hardening

The 2026-07-15 continuation closed several production-composition gaps found
by an independent current-worktree audit:

- All six public runtime services use the .NET `app` identity at UID/GID
  1654, an init process, zero ambient Linux capabilities, and
  `no-new-privileges`.
- The unauthenticated `Chummer.Api` no longer publishes a host port. Blazor and
  Portal ports bind only to loopback for a host TLS edge.
- Production API and Portal require a non-published owner-propagation secret of
  at least 32 UTF-8 bytes from a read-only secret directory. The old known
  local default and implicit-owner default are gone.
- All six runtime services use read-only root filesystems and bounded `/tmp`
  tmpfs mounts. The API and Build writable state paths remain explicit named
  volumes; both Build instances share one Data Protection key-ring volume.
- A non-root launcher passes the key-ring as a real inherited directory
  descriptor. Text HMAC/password material uses file-backed configuration; the
  owned RSA PKCS#12 remains outside `/app`.
- Production Hub requires a private persistent Data Protection directory and
  a separately mounted, password-protected RSA-3072 certificate outside its
  content root. It pins every certificate path component, requires effective
  UID ownership and owner-only modes, loads private keys ephemerally, forces
  every retained key to materialize before new-key generation, and rejects
  plaintext key XML. New key files are mode `0600` and certificate-encrypted;
  one explicit previous certificate supports controlled rotation.
- Portal runtime services participate in both `portal` and `portal-e2e`; the
  Playwright runner participates only in `portal-e2e`. The operator-facing
  `portal` profile therefore starts no test runner, while the explicit test
  profile still has its required runtime services.
- The Playwright runner no longer uses host networking and defaults to Compose
  DNS for both the private API and Blazor.
- All six services declare their exact health paths and use healthy dependency
  gating where startup ordering depends on another service.
- All six services use a configurable restart policy defaulting to
  `unless-stopped`, a 30-second stop grace period, and Docker `local` logging
  capped with `max-size=10m` and `max-file=5`.
- The effective parent build context now excludes environment files, private
  keys, certificates, credentials, secret manifests, and Compose YAML.
- Production images normalize root-owned published content to runtime-readable
  modes before switching users. The exact-image proof caught and closed a
  root-only generated `service-worker.js` regression.
- A checked, one-shot state-volume migration rejects nested mounts, links,
  devices, permissive modes, and content drift instead of using broad
  `chown -R`.

Receipts:

- Container/security contract: 21/21 passed.
- All five production images built and passed exact-image inspection. API,
  Portal, Hub, and Avalonia Browser each passed Production startup with a
  read-only root filesystem, UID/GID 1654, `CapEff=0`, and `NoNewPrivs=1`.
- Blazor production Dockerfile: built successfully end to end. The effective
  context was initially about 1.8 GB and fell to about 271 MB after the first
  exclusion pass. A later clean Hub build transferred 1.12 GB, so current
  clean-context size has regressed and needs a dedicated size audit. The 4.77
  MB and 4.84 MB observations after operational-cache exclusions were
  incremental BuildKit transfers, not clean-context totals.
- Locked shared-path NuGet cache: the first population took about 376.5
  seconds; after a disposable included source file invalidated the source/COPY
  layer, the restore phase completed in 2.0 seconds. These are restore-only,
  not end-to-end build, timings.
- Exact Blazor Production image proof: passed with UID/GID 1654, `CapEff=0`,
  `NoNewPrivs=1`, read-only root, health, encrypted key generation, source-FD
  transfer, and state persistence across restart. The proof resolves `tini`'s
  direct `dotnet` child and verifies descriptor 3 no longer refers to the
  transferred directory while allowing legitimate descriptor-number reuse.
- Exact API Production image proof: passed normal readiness, rejected state
  mode drift with 503, recovered after repair, and preserved restart state.
- Exact Hub Production image proof: passed missing/wrong-password rejection,
  read-only/non-root/capability/NNP checks, parsed ciphertext-only key XML,
  owner-only key/certificate modes, A-to-B previous-certificate continuity,
  current-certificate selection on a fresh B ring, failed-start digest
  immutability, restart continuity, and graceful shutdown.
- API, Blazor, and Hub exact-container graceful shutdown: each stopped within
  the 30-second timeout and exited with code 0.
- Portal and Avalonia Browser exact health-path checks: passed.
- Named-volume migration proof: passed; digest-preserving migration succeeded
  and a symlinked volume was rejected.
- Production owner-secret rejection: 6/6 passed across API and Portal.
- Effective Docker-context secret-canary proof: passed; no environment,
  key/certificate, credential, or secret-manifest canary reached BuildKit.
- Blazor, API, Portal, and Hub Release builds: 0 warnings, 0 errors each.
- Focused Hub certificate encryption/rotation contract: 6/6 passed.
- Default, test-profile, portal-profile, and portal-e2e-profile Compose
  validation: passed; the portal service list contains no test runner, while
  portal-e2e includes the runner and its runtime dependencies.

The operator contract and rollback procedure are in
`docs/CONTAINER_RUNTIME_HARDENING.md`. None of these changes is deployed.

## Warning and policy gates

- Run API Release build: 0 warnings, 0 errors.
- Play access/proxy policy tests: 71/71 passed.
- Static fail-closed containment tests: 13/13 passed.
- Test-warning cleanup build: 0 warnings, 0 errors.
- Typed workspace/restart/owner focused tests: 62/62 passed.
- Previously completed workspace lifecycle slice: 176/176 passed.
- Previously completed presenter and operation slice: 136/136 passed.

## Residual risks and decisions

1. The run-services projection fix and portal guard are not committed/deployed;
   hosted canonical truth therefore remains wrong.
2. A synchronous kernel filesystem call cannot be forcibly cancelled. The
   readiness design limits a hang to one worker and keeps liveness available,
   but true execution termination needs process isolation or a cancellable
   provider.
3. The filesystem store still acknowledges same-UID path-swap/TOCTOU limits.
   Runtime processes are now non-root, but named volumes and their host remain
   trusted boundaries.
4. The two Blazor state volumes are independent; only cryptographic key-ring
   material is shared. This is durable single-host storage, not shared replica
   workspace state, failover, cross-host durability, or a proven backup/restore
   contract.
5. Provider credentials beyond the owner and Build cryptographic keys remain
   in environment configuration.
6. Hub key XML is certificate-encrypted locally, but certificate provisioning,
   backup, offline rewrap/recovery, and deployment-host ownership proof remain
   operator controls. Data Protection does not rewrap old key files during
   rotation, and losing every decrypt certificate for a retained key is
   intentionally unrecoverable.
7. Image/base tags remain mutable and lack a proven digest-refresh and
   provenance/SBOM policy.
8. CPU, memory, and PID limits remain deployment sizing and overload-policy
   decisions.
9. Compose health conditions gate startup only; later dependency degradation
   still needs deployment monitoring and recovery policy.
10. Secret ownership and volume migration need deployment-host proof for
   rootless Docker, user-namespace remapping, or Docker Desktop.
11. Full flagship multi-replica hosting requires a dedicated Build PostgreSQL
   provider and schema. Reusing `play_auth` would be incorrect. Required user
   decisions include provider/region/budget, database versus schema isolation,
   RPO/RTO/retention, preview-data migration, write-freeze allowance, workspace
   size, encryption, deletion/retention, rollback window, and secret/migration
   authority.
12. Hosted frontdoor, cache, and release proof remain unexecuted for these local
   changes.

## Authorized next sequence

1. Review and integrate the local run-services freshness fix, portal guard, and
   presentation hardening without discarding unrelated dirty work.
2. Obtain explicit deployment and cache-control authority.
3. Deploy the run-services and presentation changes.
4. Verify the live canonical manifest for the deployed version is byte/truth
   coherent and remains `review_required`/`blocked` while freshness is stale.
5. Run the hosted frontdoor, Build PWA, companion handoff, and release-shelf
   proofs.
6. Retry a preview release only if the deploy proof requires a new version.
7. Make the PostgreSQL/HA decision before claiming multi-replica flagship
   durability.

Until steps 1 through 5 pass, the release remains **NO-GO**.
