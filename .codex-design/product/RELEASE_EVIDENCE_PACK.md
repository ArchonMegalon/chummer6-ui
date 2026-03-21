# Release Evidence Pack

Last reviewed: 2026-03-19

Purpose: close `WL-D037` by keeping the final release argument in one canonical location.

## Program exit summary

- All phase exits from `A` through `F` are materially met in `PROGRAM_MILESTONES.yaml`.
- `GROUP_BLOCKERS.md` reports no red blockers.
- The product vision, horizon canon, public-guide policy, and Fleet participation/support posture are all canonical and downstream-synced from this repo.

## Owner-repo evidence

- `chummer6-core`: contract canon, explain/runtime canon, restore/runbook proof, legacy migration certification, and explicit legacy-root quarantine are recorded in `docs/CONTRACT_BOUNDARY_MAP.md`, `docs/EXPLAIN_AND_RUNTIME_CANON.md`, `docs/CORE_RUNTIME_RESTORE_RUNBOOK.md`, `docs/LEGACY_MIGRATION_CERTIFICATION.md`, and `docs/LEGACY_ROOT_SURFACE_INVENTORY.md`.
- `chummer6-ui`: workbench completion and cross-head signoff are explicit in `docs/WORKBENCH_RELEASE_SIGNOFF.md`.
- `chummer6-mobile`: replay, reconnect, installable-PWA, and release hardening are explicit in `docs/PLAY_RELEASE_SIGNOFF.md`.
- `chummer6-hub`: hosted boundary, adapter authority, assistant governance, docs/help, feedback, and operator-consumer posture are explicit in `docs/HOSTED_BOUNDARY.md`, `docs/HOSTED_ADAPTER_AUTHORITY.md`, `docs/ASSISTANT_PLANE_AUTHORITY.md`, `docs/HOSTED_DOCS_HELP_CONSUMERS.md`, and `docs/HOSTED_FEEDBACK_AND_OPERATOR_CONSUMERS.md`.
- `chummer6-ui-kit`: shared package release posture is explicit in `docs/SHARED_SURFACE_SIGNOFF.md`.
- `chummer6-hub-registry`: owner-read-model and restore proof are explicit in `docs/REGISTRY_PRODUCT_READMODELS.md` and `docs/REGISTRY_RESTORE_RUNBOOK.md`.
- `chummer6-media-factory`: adapter authority, stable media capability, and restore proof are explicit in `docs/MEDIA_ADAPTER_MATRIX.md`, `docs/MEDIA_CAPABILITY_SIGNOFF.md`, and `docs/MEDIA_FACTORY_RESTORE_RUNBOOK.md`.
- `fleet`: design remains mirrored into runtime/operator truth, and premium-burst participation is design-first canon before downstream execution.

## Mirror and truth freshness

- review-template parity evidence: `products/chummer/sync/REVIEW_TEMPLATE_MIRROR_PUBLISH_EVIDENCE.md`
- local mirror parity evidence: `products/chummer/sync/LOCAL_MIRROR_PUBLISH_EVIDENCE.md`
- truth-maintenance evidence: `products/chummer/maintenance/TRUTH_MAINTENANCE_LOG.md`

## Promotion posture

Chummer is release-complete at the canonical product/design level. Public-guide and participation surfaces remain downstream-only and may stay on a protected-preview deployment posture until operators choose broader promotion, but that deployment choice no longer reflects missing design or repo-boundary truth.
