# Preview-nightly evidence coordinator

`scripts/release/run_preview_nightly_pipeline.py` coordinates the existing
protected release operations without collapsing their authorities. It can run
stage preparation, the governed JIT candidate export, native Windows capture,
protected human finalization, original-artifact preservation, and stage seal.
It cannot upload release bytes, deploy, publish, or advance `CURRENT`.

All paths are absolute and integrity-bound into the resumable state:

```bash
python3 scripts/release/run_preview_nightly_pipeline.py \
  --state-file /secure/run/pipeline-state.json \
  --evidence-directory /secure/run/evidence \
  --prepared-stage-root /secure/nightly/.nightly-run-V.candidate \
  --stage-dir /secure/nightly/nightly-run-V \
  --release-version V \
  --published-at 2026-07-19T12:00:00Z \
  --stage-authority-input /secure/run/STAGE_AUTHORITY_INPUT.json \
  --provenance-output /secure/run/DURABLE_PROVENANCE.json \
  --review-request-output /secure/run/HUMAN_REVIEW_REQUEST.json \
  --handoff-output /secure/run/IMMUTABLE_PUBLICATION_HANDOFF.json \
  --finalized-archive /secure/run/finalized-original.zip \
  --run-prepare
```

`STAGE_AUTHORITY_INPUT.json` uses contract
`chummer6-ui.preview-nightly-stage-authority-input` version 1 and contains an
exact `environment` object with every source root/commit, retained-shelf
path/digest, and proof path/digest listed in `PREVIEW_NIGHTLY_STAGE.md`. Missing
or extra keys fail closed. The coordinator strips ambient `CHUMMER_*` values and
exports only that authority plus the CLI-bound candidate path, stage path,
version, and publication timestamp to both stage preparation and seal.

The first invocation authenticates the exact source/ref, source commits,
candidate run, artifact ID/API digest, candidate inventory, relay-returned
capture run ID, and capture artifact. The relay run ID is preserved in a second
artifact bound to the exact candidate run, artifact ID, and inventory digest;
the coordinator polls only that run ID, so another same-SHA capture cannot be
substituted. It preserves the original candidate, dispatch, and capture ZIP
bytes and exits with code `3` at `action_required_human_review`.

The reviewer must inspect the two digest-bound Avalonia screenshots from the exact
request and create a separate input with contract
`chummer6-ui.preview-nightly-human-review-input` version 1. It must bind the
request SHA-256, exact capture object, authenticated reviewer, the promoted
Avalonia head, and
explicit `readability`, `contrast`, and `clipping` confirmations. Resume with
the same arguments plus:

```bash
  --review-input /secure/run/HUMAN_REVIEW_INPUT.json
```

The coordinator then dispatches the protected `windows-visual-review`
finalization workflow. The workflow remains responsible for its environment
approval and reviewer allowlist. On success the coordinator downloads the
original finalized Actions ZIP by exact artifact ID, checks the REST digest,
records durable provenance (workflow/run/source/artifact identities, reviewer,
candidate inventory, and archive digests), parses the seal, proves its release,
manifest, candidate inventory, and seven source authorities match coordinator
state, then emits an
exclusive handoff marked `sealed_for_dry_run_only` and
`uploadAuthorized=false`.

Actions expiry timestamps are recorded only as acquisition-time facts. The
provenance never claims long-term online availability; the exact original ZIPs
are the durable local evidence.
