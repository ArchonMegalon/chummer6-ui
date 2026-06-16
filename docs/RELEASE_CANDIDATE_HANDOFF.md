# Release Build Handoff

Purpose: turn a locally verified release bundle into a short, operator-facing handoff instead of relying on shell scrollback.

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

## Promotion rule

Do not promote the bundle to `public_stable` if the handoff still shows:

1. `missing_required_platforms`
2. `promotion_ready: false`
3. startup-smoke receipts that are only `skipped` for a required installer tuple

In that state, the bundle is a valid release-build handoff, not a promotable stable release.
