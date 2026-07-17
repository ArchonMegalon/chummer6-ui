# Release Build Handoff

Purpose: turn a locally verified staged nightly bundle into a short, operator-facing handoff instead of relying on shell scrollback.
This handoff does not publish the live downloads shelf and does not change the stable channel by itself.

## When to use it

Use this when:

1. new desktop bytes have been built
2. at least one startup-smoke lane has been rerun against those exact bytes
3. the bundle verifies locally
4. the build still cannot be promoted to `public_stable` because one or more required platform tuples remain unproven

Typical examples:

1. Linux installer smoke passed locally, but Windows installer smoke requires a Windows-capable host
2. macOS packaging and notarization must still run on a Mac host
3. the upload token is intentionally not present on the build machine

## Command

```bash
python3 scripts/materialize_release_candidate_handoff.py <stageDir>
```

Default outputs:

1. `<stageDir>/RELEASE_BUILD_HANDOFF.generated.json`
2. `<stageDir>/RELEASE_BUILD_HANDOFF.generated.md`

## What it captures

The handoff records:

1. release channel and version
2. staged artifact inventory
3. startup-smoke disposition per tuple
4. missing required platforms from release-channel coverage
5. concrete remaining blockers
6. next operator actions
7. when present, the stage-local Windows visual-proof handoff packet for the exact staged installer bytes
8. a refreshed stage-local Windows exit-gate receipt, materialized against that same stage manifest, files shelf, and stage-local `WINDOWS_INSTALLER_VISUAL_PROOF.generated.json` target
9. explicit staged-nightly-only contract flags: `handoff_only`, `stable_release_unchanged`, `requires_separate_publish_lane`, and `stage_proof_complete`

## Handoff rule

Do not promote the bundle to `public_stable` if the handoff still shows:

1. `missing_required_platforms`
2. `stage_proof_complete: false`
3. startup-smoke receipts that are only `skipped` for a required installer tuple

In that state, the bundle is a valid release-build handoff, not a promotable stable release. The live downloads shelf and stable channel should remain unchanged.

For Windows bootstrap installers, a passing startup-smoke receipt must exercise bootstrap download mode. A local payload handoff is useful for diagnosis, but it is not enough for the publish preflight because it does not prove the public bootstrap path, payload download target, size verification, checksum verification, extraction, and installed app launch. On Linux/Wine hosts, keep `CHUMMER_WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE=download` and let the smoke script use its bounded Wine path, Wine prefix, and Windows binary timeouts so failed proof becomes a regression packet instead of a hanging nightly lane.

Even when `stage_proof_complete: true`, the handoff is still only a staged nightly artifact. Public/stable publication remains a separate explicit operator lane.

If the handoff includes `windows_visual_proof_handoff.status: ready_for_windows_host`, the staged Windows bytes are locally verified and blocked only by the missing Windows screenshots. Use the emitted `WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.{json,md}` packet from the same stage directory to drive the real Windows capture step. Do not substitute repo-default downloads paths at that point; the packet is already pinned to the exact staged manifest, installer, payload, and startup-smoke receipt.

If the Windows host cannot see the staged Linux path directly, copy the whole stage directory to the Windows host and keep its relative layout intact. Run the handoff packet's Windows-local template command against that copied stage, then copy back `WINDOWS_INSTALLER_VISUAL_PROOF.generated.json` plus `windows-installer-visual-proof/windows-installer-progress.png` and `windows-installer-visual-proof/windows-installer-completion.png` to the original stage before rerunning the Linux Windows-exit gate.
