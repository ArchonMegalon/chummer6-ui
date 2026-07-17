# Desktop release pipeline

`chummer6-ui` owns desktop packaging, installer recipes, and updater integration.
It does not own promoted release-channel truth.

## What this repo owns

* building desktop publish directories
* producing Windows installer and portable `.exe` artifacts, plus portable platform bundles alongside macOS `.dmg` and Linux `.deb` desktop artifacts
* emitting a desktop release bundle (`files/` plus release metadata) that Fleet can orchestrate
* running startup smoke on each packaged desktop head before promotion evidence is considered complete
* materializing a repo-local Linux desktop exit gate that builds the Linux binary, packages the primary `.deb`, installs and purges that `.deb` inside an isolated dpkg root while booting the installed head in startup-smoke mode, and records unit-test proof
* emitting bounded release-regression packets when startup smoke fails or crashes
* keeping the desktop head honest about whether a target is still an archive, an installer, or a richer updater-ready package

## What this repo does not own

* final release-channel promotion
* canonical installer/update-feed state
* public `/downloads` truth
* public account-aware install policy

## Release flow

1. `chummer6-ui` builds Windows, macOS, and Linux artifacts from one release build.
2. `chummer6-ui` launches each packaged head in startup-smoke mode and captures receipts or a release-regression packet.
3. `chummer6-ui` materializes `.codex-studio/published/UI_LINUX_DESKTOP_EXIT_GATE.generated.json` via `scripts/materialize-linux-desktop-exit-gate.sh` before Fleet may accept the desktop lane as release-complete.
4. Fleet orchestrates the release wave.
5. `chummer6-hub-registry` materializes `RELEASE_CHANNEL.generated.json` and the compatibility `releases.json`.
6. `chummer6-hub` serves public downloads by consuming the registry projection.

When a downloads deploy target is configured, the `Desktop Downloads Matrix` workflow publishes the live `chummer.run` shelf once per day during the 08:00 Europe/Vienna release window. Pushes do not publish downloads. Manual workflow runs are build/proof runs by default; they publish only when `force_publish_downloads` is explicitly enabled.

Local proof work should build only the surface and platform needed for the change. A desktop UI fix should use targeted unit/UI tests and, when packaging is needed, the affected public platform only. A full Windows+Linux release package is reserved for the scheduled release window or an explicit operator override.

Windows installer visual capture is a host-specific gate. After a Windows installer succeeds on a real Windows host, run:

```powershell
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\capture-windows-installer-visual-proof.ps1
```

The script launches the promoted installer, captures progress and completion screenshots into `.codex-studio\published\windows-installer-visual-proof\`, hashes the images and installer bytes, and writes `.codex-studio\published\WINDOWS_INSTALLER_VISUAL_PROOF.generated.json`. The interactive reviewer must provide a specific reviewer name or accountable operator ID and separately confirm readability, contrast, and clipping. Generic labels such as `operator`, automation identities, Wine captures, and `-Auto` output do not satisfy the gate. Final gate validation also requires the reviewer identity to appear in the independently configured `CHUMMER_WINDOWS_VISUAL_AUTHORIZED_REVIEWER_IDS` comma-separated allowlist; the receipt cannot authorize itself. The desktop release matrix may treat this as the only external host proof when local payload, digest, and update-handoff gates pass; it must still fail for missing payloads, mismatched hashes, or stale local release artifacts.

When paths are omitted, the capture script now resolves the installer from the release-manifest shelf first: it prefers `.tmp\verify-release-channel\RELEASE_CHANNEL.generated.json` when present, then the current promoted downloads manifests and each manifest's sibling `files/` directory ahead of the old repo-local `Docker\Downloads` fallback. That keeps the operator capture lane aligned with the same promoted bytes the exit gates verify.

If the current checkout has the promoted installer bytes, payload sidecar, and startup-smoke receipt but is still missing `WINDOWS_INSTALLER_VISUAL_PROOF.generated.json`, the aggregate desktop executable gate must classify that state as `blockingMode=external_only`. That is an operator-host proof gap, not a local Avalonia build or packaging regression.

When a freshly rebuilt Windows shelf is blocked only by that host proof, materialize the operator handoff from the exact shelf instead of relying on the repo defaults:

```bash
python3 scripts/materialize_windows_visual_proof_handoff.py \
  --manifest <shelf>/RELEASE_CHANNEL.generated.json \
  --windows-gate <shelf>/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json \
  --startup-smoke <shelf>/startup-smoke/startup-smoke-avalonia-win-x64.receipt.json
```

That handoff records the shelf root, the release-aligned installer and payload paths it found, the Windows startup-smoke receipt and progress log it matched, and the exact `capture-windows-installer-visual-proof.ps1` command the Windows operator must run with `-ReleaseChannelPath` and `-OutputPath`.

The publish scripts now enforce that same Windows desktop exit gate before promotion. Before those platform-specific gates, `scripts/publish-latest-nightly-to-downloads.sh` also requires at least one staged `open_public` Windows or Linux installer whose platform is marked `promoted_release` and whose package matches `primary_package_kind` in `.codex-design/product/DESKTOP_PLATFORM_ACCEPTANCE_MATRIX.yaml`. The generic nightly lane therefore cannot replace the public shelf with only hidden, account-gated, guided-support, or macOS artifacts. It then refuses to publish unless the stage passes payload verification, bootstrap startup-smoke verification, and `scripts/materialize-windows-desktop-exit-gate.sh` against the stage-aligned shelf. `scripts/publish-download-bundle.sh` also reruns the Windows exit gate against the synchronized deploy shelf after startup-smoke receipts are copied into place, so the public downloads tree cannot drift past a missing visual proof, a stale bootstrap trace, or a regressed root-level payload target.

`CHUMMER_FORCE_NIGHTLY_PUBLISH=1` overrides only the daily cadence. It cannot bypass the public-installer eligibility or proof gates. When an operator needs only the staged support packet, `CHUMMER_NIGHTLY_SUPPORT_PROOF_ONLY_HANDOFF=1` is the explicit non-public lane: it refreshes the handoff, validates stage scope, emits any Windows visual-proof guidance, and exits before downloads synchronization or edge deployment. Its success is not a public-nightly publication claim.

When that Windows exit-gate failure is stage-local and blocked only by missing screenshots, both publish scripts now print the matching `WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json` path, its status and summary, and the first staged next action so the operator is pointed at the exact shelf-local capture packet instead of a generic retry message.

## Windows bootstrap rule

The public Windows row must not publish a large self-contained setup executable as `installerMode=bootstrap`.

For the main downloads shelf, a Windows bootstrap installer must:

* stay below the bootstrap size cap enforced by `scripts/verify-windows-installer-payloads.py`
* embed the payload URL, SHA-256, and byte size in the installer binary
* ship the matching `chummer-*-payload.zip` and `.json` sidecar
* prove the payload zip contains the launch executable and bundled sample character
* pass the Windows installer payload gate before promotion

`scripts/build-desktop-installer.sh` does not hardcode bootstrap readiness anymore.
It publishes the current bootstrap-capable installer, then lets the payload gate prove whether that binary is actually small enough and correctly wired for promotion.

If the available Windows installer is still a bundled or self-contained setup, keep it off the recommended public shelf or publish it only through the supplemental proof route. Do not relabel it as a bootstrap installer to make the manifest pass.

The current bootstrap lane is a native NSIS builder that ships a small installer and a matching payload sidecar. The installer downloads or reuses the payload zip, verifies its SHA-256 and byte size, extracts it with bundled 7-Zip command-line components, then registers shortcuts, uninstall, and the `chummer://` protocol. Windows auto-update now hands that same installer a staged local payload path, checksum, and byte size so the updater reuses the already-verified payload instead of downloading it twice.

For macOS, automatic installer handoff stays off for unsigned or quarantined installer images, but the updater now keeps the downloaded installer path in local state so Update Status can reopen that exact staged installer instead of sending the user back to the website.

For Linux `.deb` installs, automatic handoff still prefers `dpkg`, `pkexec`, or passwordless `sudo`. If that handoff fails, Update Status now keeps the staged package path and exposes a copyable manual `sudo dpkg -i ...` recovery command instead of leaving the user with only an error banner.

Remaining cross-platform follow-up scope after the Windows bootstrap handoff:

* add the equivalent minimal bootstrap/update shape for Linux and macOS so all three native platforms converge on one payload-first update story

For the public shelf, Windows `win-x64` and Linux `linux-x64` are the rolling-release scope. Mainline builds resolve to `preview` automatically for that scope, and `public_stable` remains an explicit promotion step. macOS remains buildable and publishable as a bounded artifact lane, but it does not get to hold back the public Windows/Linux shelf when the current public promotion policy is Windows/Linux-only.

Desktop heads may consume that canonical registry projection directly for self-update when `CHUMMER_DESKTOP_UPDATE_MANIFEST` points at `RELEASE_CHANNEL.generated.json` (or a compatible `/downloads/` base URL).

Chummer Online is a separate delivery lane from native desktop packaging. In that lane, `/app` is the clean public Chummer Online route, `/blazor/` is the stable hosted Blazor entry into Chummer Online, `/blazor/app` is the hosted app path, `/blazor/home` is the product/orientation page, `/blazor/workbench` remains the /blazor/workbench compatibility route for the same promoted Chummer Online client, and `/blazor/preview` remains a supporting proof surface rather than the primary user promise. For Docker self-hosting of `Chummer.Blazor` behind `Chummer.Portal`, use [BLAZOR_SELF_HOST_RUNBOOK.md](BLAZOR_SELF_HOST_RUNBOOK.md). For the broader Chummer Online design and parity bar, use [BLAZOR_WEB_CLIENT_PARITY_GOAL.md](BLAZOR_WEB_CLIENT_PARITY_GOAL.md).

The local shell wrappers in `scripts/generate-releases-manifest.sh` and `scripts/verify-releases-manifest.sh` are compatibility entrypoints. The canonical materializer now lives in `chummer6-hub-registry`.

For a Mac-hosted Codex/operator flow that builds, signs, notarizes, smoke-tests, and publishes a desktop bundle to `chummer.run`, use [MAC_CODEX_RELEASE_TO_CHUMMER_RUN.md](MAC_CODEX_RELEASE_TO_CHUMMER_RUN.md).
