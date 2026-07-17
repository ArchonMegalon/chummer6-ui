from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
GENERATOR = REPO_ROOT / "scripts" / "generate-releases-manifest.sh"
BLOCK_START = 'python3 - "$CANONICAL_MANIFEST_PATH" "$MANIFEST_PATH" "$PORTAL_MANIFEST_PATH" <<\'PY\'\n'
BLOCK_END = '\nPY\npython3 - "$REGISTRY_ROOT/scripts/verify_public_release_channel.py"'
RECONCILE_BLOCK_START = (
    'python3 - "$REGISTRY_ROOT/scripts/verify_public_release_channel.py" '
    '"$CANONICAL_MANIFEST_PATH" "$MANIFEST_PATH" "$PORTAL_MANIFEST_PATH" <<\'PY\'\n'
)
RECONCILE_BLOCK_END = '\nPY\ncanonical_startup_smoke_dir='


def _honesty_state_program() -> str:
    source = GENERATOR.read_text(encoding="utf-8")
    start = source.index(BLOCK_START) + len(BLOCK_START)
    end = source.index(BLOCK_END, start)
    return source[start:end]


def _reconciliation_program() -> str:
    source = GENERATOR.read_text(encoding="utf-8")
    start = source.index(RECONCILE_BLOCK_START) + len(RECONCILE_BLOCK_START)
    end = source.index(RECONCILE_BLOCK_END, start)
    return source[start:end]


def _manifest(proof_status: str, *, rollout_state: str = "promoted_preview") -> dict[str, object]:
    return {
        "status": "published",
        "channelId": "preview",
        "version": "run-supportability-floor",
        "rolloutState": rollout_state,
        "supportabilityState": "preview_supported",
        "publicTrustMetrics": {
            "proofFreshness": {"status": proof_status},
            "releaseChannel": {
                "rolloutState": rollout_state,
                "supportabilityState": "preview_supported",
            },
        },
        "registryBoundaryCoverage": {
            "releaseChannel": {
                "rolloutState": rollout_state,
                "supportabilityState": "preview_supported",
            }
        },
        "artifacts": [{"artifactId": "fixture", "sha256": "a" * 64, "sizeBytes": 1}],
    }


def _run_honesty_state(tmp_path: Path, *payloads: dict[str, object]) -> list[dict[str, object]]:
    paths: list[Path] = []
    for index, payload in enumerate(payloads):
        path = tmp_path / f"manifest-{index}.json"
        path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
        paths.append(path)

    completed = subprocess.run(
        [sys.executable, "-", *(str(path) for path in paths)],
        input=_honesty_state_program(),
        text=True,
        cwd=REPO_ROOT,
        capture_output=True,
        check=False,
    )
    assert completed.returncode == 0, completed.stderr
    return [json.loads(path.read_text(encoding="utf-8")) for path in paths]


def test_generator_intermediate_honesty_state_never_rewrites_stale_or_missing_proof_to_supported(
    tmp_path: Path,
) -> None:
    stale, missing, fresh = _run_honesty_state(
        tmp_path,
        _manifest("stale"),
        _manifest("missing"),
        _manifest("fresh"),
    )

    for review_required in (stale, missing):
        assert review_required["supportabilityState"] == "review_required"
        assert review_required["rolloutState"] == "public_release_review_required"
        assert review_required["publicTrustMetrics"]["releaseChannel"]["supportabilityState"] == "review_required"
        assert review_required["registryBoundaryCoverage"]["releaseChannel"]["supportabilityState"] == "review_required"

    assert fresh["supportabilityState"] == "preview_supported"
    assert fresh["rolloutState"] == "promoted_preview"


def test_generator_intermediate_honesty_state_preserves_stronger_rollout_blockers(tmp_path: Path) -> None:
    [blocked] = _run_honesty_state(tmp_path, _manifest("stale", rollout_state="coverage_incomplete"))

    assert blocked["supportabilityState"] == "review_required"
    assert blocked["rolloutState"] == "coverage_incomplete"
    assert blocked["publicTrustMetrics"]["releaseChannel"]["rolloutState"] == "coverage_incomplete"
    assert blocked["registryBoundaryCoverage"]["releaseChannel"]["rolloutState"] == "coverage_incomplete"


def _reconciliation_manifest(proof_status: str, *, rollout_state: str = "promoted_preview") -> dict[str, object]:
    payload = _manifest(proof_status, rollout_state=rollout_state)
    payload.update(
        {
            "rolloutReason": "Current release shelf passed the local release run before publication.",
            "supportabilitySummary": "Current preview release is supported on the promoted routes.",
            "knownIssueSummary": "Preview caveats still apply, but current coverage is recent.",
            "fixAvailabilitySummary": "Only send fixed notices after the published artifact is available.",
            "desktopTupleCoverage": {
                "desktopRouteTruth": [
                    {
                        "tupleId": "avalonia:linux:linux-x64",
                        "routeRole": "primary",
                        "promotionState": "promoted",
                        "revokeState": "not_revoked",
                        "artifactId": "fixture",
                    },
                    {
                        "tupleId": "blazor-desktop:linux:linux-x64",
                        "routeRole": "fallback",
                        "promotionState": "promoted",
                        "revokeState": "not_revoked",
                        "artifactId": "fixture",
                    },
                ]
            },
        }
    )
    payload["publicTrustMetrics"]["releaseChannel"].update(
        {
            "posture": "preview",
            "recommendedRouteCount": 1,
            "fallbackRecoveryRouteCount": 1,
            "blockedRouteCount": 0,
            "summary": "One current route is recommended.",
        }
    )
    payload["publicTrustMetrics"]["adoptionHealth"] = {
        "status": "limited",
        "primaryPromotedCount": 1,
        "publicInstallCount": 0,
        "accountLinkedInstallCount": 1,
        "fallbackRecoveryCount": 1,
        "blockedRouteCount": 0,
        "summary": "One primary route is promoted.",
    }
    payload["registryBoundaryCoverage"]["releaseChannel"].update(
        {
            "publicTrustPosture": "preview",
            "summary": "The preview route is promoted.",
        }
    )
    return payload


def _run_reconciliation(tmp_path: Path, payload: dict[str, object]) -> dict[str, object]:
    verifier_path = tmp_path / "verify_public_release_channel.py"
    verifier_path.write_text(
        """from copy import deepcopy


def expected_public_trust_metrics(payload):
    metrics = deepcopy(payload.get("publicTrustMetrics") or {})
    if payload.get("supportabilityState") == "review_required":
        release_channel = metrics.setdefault("releaseChannel", {})
        release_channel.update({
            "rolloutState": payload.get("rolloutState"),
            "supportabilityState": "review_required",
            "posture": "blocked",
            "recommendedRouteCount": 0,
            "fallbackRecoveryRouteCount": 0,
            "blockedRouteCount": 2,
        })
        adoption = metrics.setdefault("adoptionHealth", {})
        adoption.update({
            "status": "blocked",
            "primaryPromotedCount": 0,
            "publicInstallCount": 0,
            "accountLinkedInstallCount": 0,
            "fallbackRecoveryCount": 0,
            "blockedRouteCount": 2,
        })
    return metrics


def expected_registry_boundary_coverage(payload):
    coverage = deepcopy(payload.get("registryBoundaryCoverage") or {})
    if payload.get("supportabilityState") == "review_required":
        release_channel = coverage.setdefault("releaseChannel", {})
        release_channel.update({
            "rolloutState": payload.get("rolloutState"),
            "supportabilityState": "review_required",
            "publicTrustPosture": "blocked",
        })
    return coverage
""",
        encoding="utf-8",
    )
    verifier_path.with_name("materialize_public_release_channel.py").write_text("\n", encoding="utf-8")
    manifest_path = tmp_path / "RELEASE_CHANNEL.generated.json"
    manifest_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    completed = subprocess.run(
        [sys.executable, "-", str(verifier_path), str(manifest_path)],
        input=_reconciliation_program(),
        text=True,
        cwd=REPO_ROOT,
        capture_output=True,
        check=False,
    )
    assert completed.returncode == 0, completed.stderr
    return json.loads(manifest_path.read_text(encoding="utf-8"))


def test_final_reconciliation_cannot_resurrect_supported_copy_from_stale_proof(tmp_path: Path) -> None:
    reconciled = _run_reconciliation(tmp_path, _reconciliation_manifest("stale"))

    assert reconciled["supportabilityState"] == "review_required"
    assert reconciled["rolloutState"] == "public_release_review_required"
    for field_name in (
        "rolloutReason",
        "supportabilitySummary",
        "knownIssueSummary",
        "fixAvailabilitySummary",
    ):
        assert "stale or incomplete proof receipts" in reconciled[field_name].lower()

    release_channel = reconciled["publicTrustMetrics"]["releaseChannel"]
    assert release_channel["supportabilityState"] == "review_required"
    assert release_channel["rolloutState"] == "public_release_review_required"
    assert release_channel["posture"] == "blocked"
    assert release_channel["recommendedRouteCount"] == 0
    assert "fallbackRecoveryRouteCount" not in release_channel
    assert release_channel["blockedRouteCount"] == 2

    adoption = reconciled["publicTrustMetrics"]["adoptionHealth"]
    assert adoption["status"] == "blocked"
    assert adoption["primaryPromotedCount"] == 0
    assert adoption["fallbackRecoveryCount"] == 0
    assert adoption["blockedRouteCount"] == 2

    boundary = reconciled["registryBoundaryCoverage"]["releaseChannel"]
    assert boundary["supportabilityState"] == "review_required"
    assert boundary["rolloutState"] == "public_release_review_required"
    assert boundary["publicTrustPosture"] == "blocked"


def test_final_reconciliation_preserves_fresh_supported_preview(tmp_path: Path) -> None:
    reconciled = _run_reconciliation(tmp_path, _reconciliation_manifest("fresh"))

    assert reconciled["supportabilityState"] == "preview_supported"
    assert reconciled["rolloutState"] == "promoted_preview"
    assert reconciled["publicTrustMetrics"]["releaseChannel"]["recommendedRouteCount"] == 1


def test_final_reconciliation_preserves_stronger_stale_rollout_blocker(tmp_path: Path) -> None:
    reconciled = _run_reconciliation(
        tmp_path,
        _reconciliation_manifest("missing", rollout_state="coverage_incomplete"),
    )

    assert reconciled["supportabilityState"] == "review_required"
    assert reconciled["rolloutState"] == "coverage_incomplete"
    assert reconciled["publicTrustMetrics"]["releaseChannel"]["rolloutState"] == "coverage_incomplete"
