# Container runtime hardening

Date: 2026-07-15

## Contract

All public Chummer Compose services run with the .NET runtime image's fixed
`app` identity, UID/GID `1654:1654`. The application files remain owned by
root and are readable, not writable, by the runtime process. The API and Build
images create only `/app/state` as a private `0700` directory owned by the app
identity. Each final image normalizes published content to root-owned,
runtime-readable modes before dropping privilege; generated PWA files are
therefore never accidentally left root-only by a restrictive build umask.

Compose also starts each public service with an init process, drops all Linux
capabilities, and sets `no-new-privileges`. No service binds a privileged port,
so it needs no capability exception. `chummer-api` has no host-published port;
only services on the private Compose network can reach it. The Blazor and
Portal host ports bind to loopback for a host TLS edge.

All six runtime services additionally use a read-only root filesystem and a
bounded `/tmp` tmpfs. The API state volume, the two independent Build state
volumes, the shared Build Data Protection key ring, and the Hub Data Protection
key ring are the declared persistent writable mounts; secret mounts remain
read-only. A non-root Build launcher opens the key-ring directory as an
inherited descriptor before `exec`-ing .NET; the existing application boundary
then validates, duplicates, and closes that source descriptor.

Production Hub requires its persistent Data Protection directory outside the
content root with private `0700` permissions and a password-protected RSA-3072
PKCS#12 certificate from a separate read-only secret mount. Certificate path
components are pinned with Linux `openat`/`O_NOFOLLOW`, the file must be owned
by the effective UID with mode `0400` or `0600`, PKCS#12 bytes are stable
double-read and zeroed, and private keys remain ephemeral. Startup materializes
every retained key before allowing a new key, performs a protect/unprotect
round trip, and parses every key XML file to reject plaintext `masterKey`
material. New key files are mode `0600` and certificate encrypted at rest.

Every runtime service declares its real health path, and Compose dependency
gating waits for required services to become healthy. The Playwright runner
uses Compose networking and defaults to Compose DNS for both the private API
and Blazor; it does not use host networking.

All six runtime services also use a configurable restart policy that defaults
to `unless-stopped`, a 30-second stop grace period, and Docker's `local` log
driver with `max-size=10m` and `max-file=5`.

Portal runtime services participate in both the `portal` and `portal-e2e`
profiles. The Playwright runner participates only in `portal-e2e`, so the
operator-facing `portal` profile remains free of test infrastructure while the
explicit end-to-end profile still starts its runtime dependencies.

This contract covers:

- `chummer-api`
- `chummer-blazor`
- `chummer-blazor-portal`
- `chummer-hub-web-portal`
- `chummer-avalonia-browser`
- `chummer-portal`

## Production secret material

Compose mounts text secrets read-only from directories that are excluded from
the effective parent Docker build context:

- `${CHUMMER_PORTAL_OWNER_SECRETS_DIRECTORY:-./Docker/Secrets/portal-owner}`
  is mounted only into API and Portal. It must contain
  `CHUMMER_PORTAL_OWNER_SHARED_KEY` with at least 32 UTF-8 bytes of externally
  generated secret material.
- `${CHUMMER_BUILD_SECRETS_DIRECTORY:-./Docker/Secrets/build}` is mounted only
  into both Build instances. It must contain
  `CHUMMER_BUILD_OWNER_CHANNEL_HMAC_KEY_BASE64`, whose value is exactly 32
  CSPRNG bytes encoded as Base64. During rotation it may also contain
  `CHUMMER_BUILD_OWNER_CHANNEL_PREVIOUS_HMAC_KEY_BASE64`.
- The Build directory must contain the owned PKCS#12 certificate at
  `certificates/chummer-build-data-protection.p12`. If protected, its password
  is the content of
  `CHUMMER_BLAZOR_DATA_PROTECTION_CERTIFICATE_PASSWORD` in the Build secret
  directory.
- `${CHUMMER_HUB_SECRETS_DIRECTORY:-./Docker/Secrets/hub}` is mounted only
  into Hub. It must contain
  `certificates/chummer-hub-data-protection.p12` and the file
  `CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PASSWORD`. Hub requires a currently
  valid RSA certificate with a private key of at least 3072 bits and key
  encipherment usage when that extension is present.

Secret directories must be owned by UID/GID 1654 with mode `0700`; text secret
files and the certificate must be owned by 1654 with mode `0400` or `0600`.
The Build certificate must contain an RSA private key of at least 2048 bits;
the Hub certificate has the stronger 3072-bit minimum above. Generate and
transfer these values through the deployment secret provider; never add them
to Git, an image layer, Compose environment values, or a release bundle. Both
Build replicas must receive the same current HMAC key, certificate, and
key-ring volume. Every Hub replica must receive the same Hub certificate set,
application name, and coherent key-ring repository.

Production API and Portal fail startup if the owner-propagation key is missing,
short, or the formerly published local default. Production Build fails startup
if its real inherited directory descriptor, certificate, or current HMAC key
is invalid. Production Hub fails startup for missing/partial certificate
settings, unsafe paths or modes, wrong passwords, weak/non-RSA material,
plaintext key XML, or any retained key that its current/previous certificate
set cannot decrypt. Setting the environment to Development is not an
acceptable deployment workaround.

## Hub certificate migration and rotation

Do not deploy the encrypted-key contract over an existing plaintext Hub ring.
First back it up and choose one explicit migration: offline rewrap under the
new certificate, or archive/reset the ring while accepting invalidation of
existing Hub cookies, antiforgery tokens, and protected payloads. Startup now
rejects plaintext or permissively-mode key files rather than silently
grandfathering them.

Rotate from certificate A to B in two phases:

1. Stage B and its password on every replica without removing A. Verify that
   every replica sees the same shared ring and material.
2. Roll B as current and retain A as
   `CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PATH` with password file
   `CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PASSWORD`.
3. Keep A for as long as any retained key XML is A-wrapped. Data Protection
   does not automatically rewrap old keys. Remove A only after those files are
   explicitly rewrapped or retired with all dependent payload lifetimes ended.

A rollback from B to A must keep B as the previous certificate if B has
already written a key. The current contract supports one previous certificate;
repeated overlapping rotations require an offline rewrap/retirement tool or a
reviewed decrypt-certificate list extension. Compose's named volume proves
single-host behavior only; multi-host replicas need a coherent shared
repository and cross-replica protect/unprotect evidence.

## Existing-volume migration

New named volumes inherit the ownership of the image's `/app/state` mount
point. A volume previously written by a root-running Chummer image may instead
contain root-owned data. Do not deploy the non-root image over such a volume
without this migration.

1. Schedule a write freeze and stop the API and both Build services.
2. Back up all three named volumes: `chummer-state`,
   `chummer-blazor-state`, and `chummer-blazor-portal-state`. Use the actual
   Compose project prefix shown by `docker volume ls`.
3. Normalize legacy modes with the reviewed one-shot normalizer. It requires
   an actual `/app/state` mount, rejects nested mounts, symlinks, special
   files, and special mode bits, changes only directories to `0700` and
   regular files to `0600`, and verifies the content digest before and after
   the mode change. The temporary capability additions apply only to this
   stopped migration container.

```sh
docker compose run --rm --no-deps --user 0 \
  --cap-add DAC_OVERRIDE --cap-add FOWNER \
  --entrypoint /usr/local/bin/chummer-state-mode-normalization chummer-api

docker compose run --rm --no-deps --user 0 \
  --cap-add DAC_OVERRIDE --cap-add FOWNER \
  --entrypoint /usr/local/bin/chummer-state-mode-normalization chummer-blazor

docker compose --profile portal run --rm --no-deps --user 0 \
  --cap-add DAC_OVERRIDE --cap-add FOWNER \
  --entrypoint /usr/local/bin/chummer-state-mode-normalization \
  chummer-blazor-portal
```

4. Run the reviewed one-shot ownership migration from each new image. It
   requires an actual `/app/state` mount, rejects nested mounts, symlinks,
   special files, permissive modes, and special mode bits, does not follow
   links or change modes, and verifies the content digest before and after
   ownership changes. `CAP_CHOWN` and `CAP_DAC_OVERRIDE` are added only to the
   stopped migration container because it must finish a mixed-ownership tree
   after changing a private ancestor, while every runtime service otherwise
   drops all capabilities. Do not adapt either tool to an unreviewed host bind
   mount.

```sh
docker compose run --rm --no-deps --user 0 \
  --cap-add DAC_OVERRIDE --cap-add CHOWN \
  --entrypoint /usr/local/bin/chummer-state-ownership-migration chummer-api

docker compose run --rm --no-deps --user 0 \
  --cap-add DAC_OVERRIDE --cap-add CHOWN \
  --entrypoint /usr/local/bin/chummer-state-ownership-migration chummer-blazor

docker compose --profile portal run --rm --no-deps --user 0 \
  --cap-add DAC_OVERRIDE --cap-add CHOWN \
  --entrypoint /usr/local/bin/chummer-state-ownership-migration \
  chummer-blazor-portal
```

5. Preserve the JSON receipts from both one-shot tools with the backup/change
   record. Both must report `status=passed` and the same content SHA-256; the
   ownership receipt must also report UID/GID `1654`.

6. Start the services and verify the effective user, state access, and health:

```sh
docker compose up -d chummer-api chummer-blazor
docker compose --profile portal up -d chummer-blazor-portal

docker compose exec -T chummer-api id
docker compose exec -T chummer-blazor id
docker compose --profile portal exec -T chummer-blazor-portal id

docker compose exec -T chummer-api /bin/sh -c 'test -w /app/state'
docker compose exec -T chummer-blazor /bin/sh -c 'test -w /app/state'
docker compose --profile portal exec -T chummer-blazor-portal \
  /bin/sh -c 'test -w /app/state'

curl --fail http://127.0.0.1:${CHUMMER_BLAZOR_PORT:-8089}/health/ready
```

Each `id` result must report UID and GID 1654. The readiness check must not be
accepted as a substitute for checking all three state volumes.

Local reproducible gates:

```sh
/bin/sh tests/run_state_volume_migration_proof.sh
/bin/sh tests/run_api_container_security_proof.sh
/bin/sh tests/run_blazor_container_security_proof.sh
/bin/sh tests/run_hub_container_security_proof.sh
/bin/sh tests/run_production_owner_secret_gate.sh
/bin/sh tests/run_docker_context_secret_exclusion_proof.sh
```

The migration proof covers digest-preserving named-volume migration and
symlink rejection. The API proof exercises state-backed readiness, mode-drift
failure and recovery, restart persistence, and graceful shutdown. The Build
proof runs a real Production container with generated
throwaway RSA material, UID/GID 1654, zero effective capabilities,
`NoNewPrivs=1`, read-only root, writable state/key mounts, health, source-FD
transfer, encrypted key generation, and restart persistence. It resolves
`tini`'s direct `dotnet` child and proves descriptor 3 no longer points to the
transferred directory; it does not incorrectly assume that the descriptor
number can never be reused. The Hub proof covers missing/wrong-password
rejection, pinned RSA-3072 loading from file-backed secrets, parsed encrypted
XML with no plaintext master key, private generated-file modes, two-ring A/B
rotation in both directions, failed-start digest immutability, restart
persistence, and graceful shutdown. The
owner gate rejects missing, short, and formerly published material in both
Production API and Portal. The context gate sends disposable secret canaries
through the effective parent context and proves that only its non-secret marker
reaches BuildKit.

All five production images built and passed exact-image inspection. API,
Portal, Hub, and Avalonia Browser each started in Production with a read-only
root filesystem, UID/GID 1654, `CapEff=0`, and `NoNewPrivs=1`. The actual
Blazor image passed the stronger security, state-persistence, and Data
Protection proof described above. API readiness passed its private-mode
write/read/delete volume round trip, rejected mode drift with `503`, recovered,
and preserved state across restart. Hub failed closed without complete
certificate material, then proved encrypted key creation, A-to-B
previous-certificate continuity, current-certificate selection on a fresh B
ring, and unchanged key digests across restarts and failed starts.
Portal and Avalonia Browser passed their exact health paths. The static
container contract is 21/21, focused API readiness is 3/3, API and Hub builds
reported zero warnings and errors, and Compose validation passes for the
default, `test`, `portal`, and `portal-e2e` configurations. These are local
receipts; they are not deployment or hosted front-door proof.

The API, Blazor, and Hub exact-container proofs additionally stop their
containers with a 30-second timeout and assert exit code 0; all three graceful
shutdown checks pass.

Build-context and restore-cache measurements are deliberately reported with
their measurement conditions intact:

- The effective parent context was initially about 1.8 GB and fell to about
  271 MB after the first exclusion pass.
- A later clean Hub image build transferred 1.12 GB, so the current clean
  context has regressed and needs a dedicated size audit before it can be
  called optimized. The security exclusions still passed their canary proof.
- Later operational-cache exclusions were followed by 4.77 MB and 4.84 MB
  **incremental**
  BuildKit transfer observation. That number is not a clean-context total and
  must not be presented as the final context size.
- Populating the locked, shared-path NuGet cache for the first time took about
  376.5 seconds. After a disposable included source file invalidated the
  source/COPY layer, the restore phase completed in 2.0 seconds from that
  cache. These are restore-phase measurements, not end-to-end image build
  durations.
- The root ignore contract also excludes Compose YAML, so orchestration-only
  edits do not invalidate the application-source `COPY` layer.

## Rollback

If migration or startup verification fails:

1. Re-establish the write freeze and stop the affected services.
2. Capture their logs and current volume metadata for diagnosis.
3. Restore the backed-up volumes.
4. Restore the previously verified image references and Compose definition.
5. Start the old composition and repeat its health and workspace read/write
   checks before lifting the write freeze.

Changing ownership to 1654 does not prevent the former root image from reading
the data, but restore from the backup rather than relying on that fact.

## Deliberate residuals

- Image tags are not yet digest-pinned. Pinning needs an update/rollback policy
  so security fixes do not silently stop flowing.
- CPU, memory, and PID limits are not yet set and remain deployment sizing and
  overload-policy decisions.
- Some upstream/provider API keys remain Compose environment variables. The
  owner-propagation and Build cryptographic keys now use file-backed
  configuration, but the remaining providers need the same treatment.
- Hub key XML is certificate-encrypted, but the key-ring volume, secret
  provider, certificate backup, and offline recovery/rewrap procedure remain
  deployment controls. Losing all certificates for retained keys is
  intentionally unrecoverable.
- Certificate ownership through rootless Docker, user-namespace remapping, and
  Docker Desktop bind mounts needs proof on the deployment host. Perform
  migration and secret preparation from inside that Docker user namespace.
- These named volumes provide single-host durability only; they do not provide
  replica sharing, backup proof, or regional failover.
- Compose `service_healthy` conditions gate startup ordering only. They do not
  automatically stop or restart dependents when a dependency becomes unhealthy;
  deployment monitoring and recovery policy must cover later degradation.
- A directly exposed public API remains unsupported. It needs endpoint
  authentication and owner-authorization tests before any host port is added.
