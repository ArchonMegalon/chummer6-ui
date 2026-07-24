#!/usr/bin/env python3
"""Shared fail-closed routing for candidate-bound proof producers.

The shell producers keep their historical in-repository defaults.  When their
external plane is requested, this module makes the plane all-or-nothing,
validates every canonical receipt before expensive work starts, prevents output
aliases from clobbering inputs, and provides atomic external writes.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import stat
import tempfile
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence
from urllib.parse import unquote, urlsplit


PASS_STATUSES = {"pass", "passed", "ready"}
OUTPUT_STATUSES = PASS_STATUSES | {"fail", "failed", "blocked"}
PUBLISHED_PREFIX = Path(".codex-studio/published")
RELEASE_CHANNEL_CONTRACT = "Chummer.Hub.Registry.Contracts"
RELEASE_CHANNEL_STATUS = "published"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
GIT_SHA_RE = re.compile(r"^[0-9a-f]{40}$")
CANONICAL_TOKEN_RE = re.compile(r"^[a-z0-9][a-z0-9._-]{0,127}$")
UNRESOLVED_VALUES = {"", "none", "null", "tbd", "todo", "unknown", "unassigned"}
CANDIDATE_DESKTOP_TARGETS = {
    ("macos", "osx-arm64"): {
        "arch": "arm64",
        "signingRequirement": "signed",
    },
    ("windows", "win-x64"): {
        "arch": "x64",
        "signingRequirement": "preview_unsigned_allowed",
    },
}
APPROVED_SCOPE_FIELDS = {
    "approvedAtUtc",
    "approvedBy",
    "channel",
    "contractName",
    "contractVersion",
    "decisionId",
    "platforms",
    "releaseTarget",
    "releaseVersion",
    "status",
    "supportOwner",
}
APPROVED_SCOPE_PLATFORM_FIELDS = {
    "artifactAccessClass",
    "fallbackHeads",
    "platform",
    "primaryHead",
    "rid",
    "signingRequirement",
}
REGISTRY_REVIEW_SEED_FIELDS = {
    "authorityContract",
    "releaseVersion",
    "channel",
    "status",
    "rolloutState",
    "supportabilityState",
    "availablePlatforms",
    "primaryHeadByPlatform",
    "artifactCount",
    "downloadAccessPosture",
    "knownIssueSummary",
    "manifestSha256",
    "registryRepository",
    "registryCommit",
    "releaseDecisionStatus",
    "releaseDecisionSha256",
    "releaseDecisionPath",
    "supportOwner",
    "nextActions",
    "artifacts",
    "manifestPath",
}
REGISTRY_ARTIFACT_FIELDS = {
    "artifactId",
    "head",
    "platform",
    "rid",
    "arch",
    "kind",
    "downloadUrl",
    "sha256",
    "sizeBytes",
    "compatibilityState",
    "promotionState",
    "publicationScope",
    "revokeState",
    "publicInstallRoute",
    "installAccessClass",
}
REGISTRY_GENERATION_FILE_ROUTE = re.compile(
    r"^/downloads/g/([^/]+)/files/([^/]+)$"
)
REGISTRY_GENERATION_INSTALL_ROUTE = re.compile(
    r"^/downloads/g/([^/]+)/install/([^/]+)$"
)
REGISTRY_PUBLIC_INSTALL_ROUTE = re.compile(
    r"^/downloads/(?:install/|g/([^/]+)/install/)([^/]+)$"
)
CAMPAIGN_OPERABILITY_PRODUCER_CONTRACTS = {
    "desktop-visual": "chummer6-ui.desktop_visual_familiarity_exit_gate",
    "desktop-workflow": "chummer6-ui.desktop_workflow_execution_gate",
    "desktop-executable": "chummer6-ui.desktop_executable_exit_gate",
}
CAMPAIGN_OPERABILITY_CANDIDATE_BINDING_CONTRACT = (
    "chummer6-ui.campaign_operability_candidate_binding"
)
CAMPAIGN_OPERABILITY_CANDIDATE_BINDING_FIELDS = {
    "contract_name",
    "contract_version",
    "release_version",
    "release_scope_decision_sha256",
    "manifest_sha256",
    "authority_snapshot_sha256",
    "release_decision_sha256",
    "registry_commit",
    "platform",
    "rid",
    "primary_head",
    "required_heads",
}
CAMPAIGN_OPERABILITY_ENV = {
    "mode": "CHUMMER_CAMPAIGN_OPERABILITY_PREVIEW_MODE",
    "scope_path": "CHUMMER_CAMPAIGN_OPERABILITY_APPROVED_SCOPE_PATH",
    "scope_sha256": "CHUMMER_CAMPAIGN_OPERABILITY_EXPECTED_SCOPE_SHA256",
    "release_version": "CHUMMER_CAMPAIGN_OPERABILITY_EXPECTED_RELEASE_VERSION",
    "review_seed_path": "CHUMMER_CAMPAIGN_OPERABILITY_REGISTRY_REVIEW_SEED_PATH",
    "review_seed_sha256": "CHUMMER_CAMPAIGN_OPERABILITY_EXPECTED_REGISTRY_REVIEW_SEED_SHA256",
    "bounded_owner": "CHUMMER_CAMPAIGN_OPERABILITY_BOUNDED_OWNER",
    "next_actions": "CHUMMER_CAMPAIGN_OPERABILITY_NEXT_ACTIONS_JSON",
    "allow_raw_fail": "CHUMMER_CAMPAIGN_OPERABILITY_ALLOW_RAW_FAIL_DECLARATION",
}


class RoutingError(RuntimeError):
    """Raised when an external proof plane is incomplete or unsafe."""


@dataclass(frozen=True)
class ReceiptSpec:
    relative_path: str
    contract_name: str | None
    optional: bool = False


@dataclass(frozen=True)
class CampaignOperabilityCandidateContext:
    release_version: str
    release_scope_decision_sha256: str
    authority_snapshot_sha256: str
    registry_commit: str
    manifest_sha256: str
    release_decision_sha256: str
    bounded_owner: str
    next_actions: tuple[str, ...]
    platform: str
    rid: str
    primary_head: str
    required_heads: tuple[str, ...]
    allow_raw_fail_declaration: bool


B14_INPUTS: tuple[ReceiptSpec, ...] = (
    ReceiptSpec("CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.chummer5a_desktop_workflow_parity"),
    ReceiptSpec("SR4_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.sr4_desktop_workflow_parity"),
    ReceiptSpec("SR6_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.sr6_desktop_workflow_parity"),
    ReceiptSpec(
        "CHUMMER_SR6_RULESET_UI_SOPHISTICATION_GATE.generated.json",
        "chummer6-ui.chummer_sr6_ruleset_ui_sophistication_gate",
    ),
    ReceiptSpec("SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json", "chummer6-ui.sr4_sr6_desktop_parity_frontier"),
    ReceiptSpec("RULESET_UI_ADAPTATION.generated.json", "chummer6-ui.ruleset_ui_adaptation_frontier"),
    ReceiptSpec("CHUMMER5A_LAYOUT_HARD_GATE.generated.json", "chummer6-ui.chummer5a_layout_hard_gate"),
    ReceiptSpec("DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json", "chummer6-ui.desktop_workflow_execution_gate"),
    ReceiptSpec("UI_LOCALIZATION_RELEASE_GATE.generated.json", "chummer6-ui.localization_release_gate"),
    ReceiptSpec("INTERACTIVE_CONTROL_INVENTORY.generated.json", "chummer6-ui.interactive_control_inventory"),
    ReceiptSpec("RECURSIVE_UI_EVENT_EXIT_GATE.generated.json", "chummer6-ui.recursive_ui_event_exit_gate"),
    ReceiptSpec("STARTUP_WORKBENCH_SURVIVAL.generated.json", "chummer6-ui.startup_workbench_survival"),
    ReceiptSpec("DESIGN_MIRROR_COMPLETENESS.generated.json", "chummer6-ui.design_mirror_completeness"),
    ReceiptSpec("DESIGN_AUTHORIZED_PARITY_SOFTENING.generated.json", "chummer6-ui.design_authorized_parity_softening"),
    ReceiptSpec("VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json", "chummer6-ui.veteran_task_time_evidence_gate"),
    ReceiptSpec("CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json", "chummer6-ui.chummer5a_screenshot_review_gate"),
    ReceiptSpec("CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json", "chummer6-ui.classic_dense_workbench_posture_gate"),
    ReceiptSpec("CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json", "chummer6-ui.chummer5a_legacy_ui_element_parity"),
    ReceiptSpec("CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json", "chummer6-ui.chummer4_legacy_ui_element_parity"),
    ReceiptSpec("SR5_SR6_UI_PARITY_AUDIT.generated.json", "chummer6-ui.sr5_sr6_ui_parity_audit"),
    ReceiptSpec("BLAZOR_BROWSER_LANE_PROOF_SET.generated.json", "chummer6-ui.blazor_browser_lane_proof_set"),
    ReceiptSpec("BLAZOR_PLAY_SURFACE_HORIZON.generated.json", "chummer6-ui.blazor_play_surface_horizon"),
    ReceiptSpec("FLAGSHIP_PRODUCT_READINESS.generated.json", "fleet.flagship_product_readiness"),
    ReceiptSpec("CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json", None),
    ReceiptSpec("DESKTOP_EXECUTABLE_EXIT_GATE.generated.json", "chummer6-ui.desktop_executable_exit_gate"),
    ReceiptSpec("NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json", "chummer6-ui.next90_m141_ui_direct_import_route_proof"),
    ReceiptSpec("NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json", "chummer6-ui.next90_m142_ui_direct_workflow_proof"),
    ReceiptSpec("NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json", "chummer6-ui.next90_m143_ui_direct_output_proof"),
    ReceiptSpec("SECTION_HOST_RULESET_PARITY.generated.json", "chummer6-ui.section_host_ruleset_parity"),
    ReceiptSpec("UI_LOCAL_RELEASE_PROOF.generated.json", "chummer6-ui.local_release_proof"),
    ReceiptSpec("BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json", "chummer6-ui.blazor_self_host_workbench_proof"),
    ReceiptSpec("BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json", "chummer6-ui.blazor_public_edge_workbench_proof"),
    ReceiptSpec("HUMAN_SIDE_RULE_AUTHORITY_GOLD_APPROVAL.generated.json", None, optional=True),
)

DESKTOP_WORKFLOW_INPUTS: tuple[ReceiptSpec, ...] = (
    ReceiptSpec("CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.chummer5a_desktop_workflow_parity"),
    ReceiptSpec("SR4_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.sr4_desktop_workflow_parity"),
    ReceiptSpec("SR6_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.sr6_desktop_workflow_parity"),
    ReceiptSpec("SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json", "chummer6-ui.sr4_sr6_desktop_parity_frontier"),
    ReceiptSpec("RULESET_UI_ADAPTATION.generated.json", "chummer6-ui.ruleset_ui_adaptation_frontier"),
    ReceiptSpec("UI_FLAGSHIP_RELEASE_GATE.generated.json", "chummer6-ui.flagship_ui_release_gate"),
    ReceiptSpec("DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json", "chummer6-ui.desktop_visual_familiarity_exit_gate"),
    ReceiptSpec("CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json", "chummer6-ui.chummer5a_screenshot_review_gate"),
    ReceiptSpec("NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json", "chummer6-ui.next90_m141_ui_direct_import_route_proof"),
    ReceiptSpec("NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json", "chummer6-ui.next90_m142_ui_direct_workflow_proof"),
    ReceiptSpec("HUMAN_SIDE_RULE_AUTHORITY_GOLD_APPROVAL.generated.json", None, optional=True),
)

STATIC_INPUTS: dict[str, tuple[ReceiptSpec, ...]] = {
    "b14": B14_INPUTS,
    "desktop-executable": (),
    "desktop-visual": (),
    "desktop-workflow": DESKTOP_WORKFLOW_INPUTS,
    "chummer5a": (),
    "sr4": (),
    "sr6": (
        ReceiptSpec("SR4_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.sr4_desktop_workflow_parity"),
    ),
}

OUTPUT_CONTRACTS = {
    "b14": "chummer6-ui.flagship_ui_release_gate",
    "desktop-executable": "chummer6-ui.desktop_executable_exit_gate",
    "desktop-visual": "chummer6-ui.desktop_visual_familiarity_exit_gate",
    "desktop-workflow": "chummer6-ui.desktop_workflow_execution_gate",
    "chummer5a": "chummer6-ui.chummer5a_desktop_workflow_parity",
    "sr4": "chummer6-ui.sr4_desktop_workflow_parity",
    "sr6": "chummer6-ui.sr6_desktop_workflow_parity",
}

LEDGER_CONFIG = {
    "sr4": ("docs/SR4_WORKFLOW_PARITY_LEDGER.json", "sr4"),
    "sr6": ("docs/SR6_WORKFLOW_PARITY_LEDGER.json", "sr6"),
}

FAMILY_CONTRACTS = {
    "sr4": {
        "parityReceipts": "chummer6-ui.sr4_workflow_family_parity_receipt",
        "verificationReceipts": "chummer6-ui.sr4_workflow_family_verification_receipt",
        "executionReceipts": "chummer6-ui.sr4_workflow_family_execution_receipt",
    },
    "sr6": {
        "parityReceipts": "chummer6-ui.sr6_workflow_family_parity_receipt",
        "verificationReceipts": "chummer6-ui.sr6_workflow_family_verification_receipt",
        "executionReceipts": "chummer6-ui.sr6_workflow_family_execution_receipt",
    },
}


def _normalize(value: object) -> str:
    return str(value or "").strip()


def _open_directory_no_symlink_components(
    path: Path,
    *,
    create: bool = False,
) -> tuple[int, Path]:
    absolute = Path(os.path.abspath(os.path.expanduser(str(path))))
    flags = os.O_RDONLY | getattr(os, "O_DIRECTORY", 0)
    flags |= getattr(os, "O_NOFOLLOW", 0)
    anchor = Path(absolute.anchor or os.sep)
    try:
        directory_fd = os.open(anchor, flags)
    except OSError as exc:
        raise RoutingError(f"directory anchor could not be opened: {anchor}: {exc}") from exc
    try:
        for component in absolute.parts[1:]:
            if component in {"", ".", ".."}:
                raise RoutingError("directory path contains an invalid component")
            try:
                before = os.stat(
                    component,
                    dir_fd=directory_fd,
                    follow_symlinks=False,
                )
            except FileNotFoundError:
                if not create:
                    raise RoutingError(
                        f"directory path does not exist: {absolute}"
                    )
                os.mkdir(component, 0o700, dir_fd=directory_fd)
                before = os.stat(
                    component,
                    dir_fd=directory_fd,
                    follow_symlinks=False,
                )
            if not stat.S_ISDIR(before.st_mode):
                raise RoutingError(
                    f"directory path contains a symlink or non-directory component: {component}"
                )
            try:
                next_fd = os.open(component, flags, dir_fd=directory_fd)
            except OSError as exc:
                raise RoutingError(
                    f"directory component could not be opened safely: {component}: {exc}"
                ) from exc
            opened = os.fstat(next_fd)
            if (before.st_dev, before.st_ino) != (opened.st_dev, opened.st_ino):
                os.close(next_fd)
                raise RoutingError("directory identity changed while opening")
            os.close(directory_fd)
            directory_fd = next_fd
        return directory_fd, absolute
    except Exception:
        os.close(directory_fd)
        raise


def _stable_read_regular_file(path: Path, label: str) -> bytes:
    if path.name in {"", ".", ".."}:
        raise RoutingError(f"{label} path is invalid: {path}")
    directory_fd, absolute_parent = _open_directory_no_symlink_components(path.parent)
    file_fd: int | None = None
    try:
        try:
            before = os.stat(
                path.name,
                dir_fd=directory_fd,
                follow_symlinks=False,
            )
        except OSError as exc:
            raise RoutingError(f"{label} is unavailable: {path}: {exc}") from exc
        if not stat.S_ISREG(before.st_mode):
            raise RoutingError(
                f"{label} must be an existing regular non-symlink file: {path}"
            )
        flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
        try:
            file_fd = os.open(path.name, flags, dir_fd=directory_fd)
        except OSError as exc:
            raise RoutingError(f"{label} could not be opened safely: {path}: {exc}") from exc
        opened = os.fstat(file_fd)
        if (before.st_dev, before.st_ino) != (opened.st_dev, opened.st_ino):
            raise RoutingError(f"{label} identity changed before read: {path}")
        chunks: list[bytes] = []
        while True:
            chunk = os.read(file_fd, 1024 * 1024)
            if not chunk:
                break
            chunks.append(chunk)
        after_read = os.fstat(file_fd)
        after_path = os.stat(
            path.name,
            dir_fd=directory_fd,
            follow_symlinks=False,
        )
        stable_fields = (
            "st_dev",
            "st_ino",
            "st_mode",
            "st_size",
            "st_mtime_ns",
            "st_ctime_ns",
        )
        if any(
            getattr(before, field) != getattr(after_read, field)
            or getattr(before, field) != getattr(after_path, field)
            for field in stable_fields
        ):
            raise RoutingError(f"{label} changed while it was read: {path}")
        raw = b"".join(chunks)
        if len(raw) != before.st_size:
            raise RoutingError(f"{label} size changed while it was read: {path}")
        return raw
    finally:
        if file_fd is not None:
            os.close(file_fd)
        os.close(directory_fd)


def _load_object(path: Path, label: str) -> dict[str, Any]:
    try:
        loaded = json.loads(_stable_read_regular_file(path, label).decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise RoutingError(f"{label} is not valid JSON: {path}: {exc}") from exc
    if not isinstance(loaded, dict):
        raise RoutingError(f"{label} must be a JSON object: {path}")
    return loaded


def _strict_object_with_digest(path: Path, label: str) -> tuple[dict[str, Any], str, bytes]:
    raw = _stable_read_regular_file(path, label)

    def reject_duplicate_or_case_shadowed_keys(
        pairs: list[tuple[str, Any]],
    ) -> dict[str, Any]:
        result: dict[str, Any] = {}
        folded: set[str] = set()
        for key, value in pairs:
            normalized = key.casefold()
            if normalized in folded:
                raise RoutingError(
                    f"{label} contains duplicate or case-shadowed JSON field: {key}"
                )
            folded.add(normalized)
            result[key] = value
        return result

    try:
        loaded = json.loads(
            raw.decode("utf-8"),
            object_pairs_hook=reject_duplicate_or_case_shadowed_keys,
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise RoutingError(f"{label} is not strict UTF-8 JSON: {path}") from exc
    if not isinstance(loaded, dict):
        raise RoutingError(f"{label} must be a JSON object: {path}")
    return loaded, hashlib.sha256(raw).hexdigest(), raw


def _canonical_candidate_token(value: Any, label: str) -> str:
    if (
        not isinstance(value, str)
        or value != value.strip()
        or CANONICAL_TOKEN_RE.fullmatch(value) is None
        or value.casefold() in UNRESOLVED_VALUES
    ):
        raise RoutingError(f"{label} must be an exact canonical token")
    return value


def _concrete_candidate_actions(value: Any) -> tuple[str, ...]:
    if not isinstance(value, (list, tuple)) or not value or len(value) > 16:
        raise RoutingError("campaign-operability candidate requires 1-16 concrete next actions")
    actions: list[str] = []
    for item in value:
        if (
            not isinstance(item, str)
            or item != item.strip()
            or not item
            or len(item) > 512
            or item.casefold() in UNRESOLVED_VALUES
        ):
            raise RoutingError(
                "campaign-operability next actions must be concrete bounded strings"
            )
        actions.append(item)
    if len(actions) != len(set(actions)):
        raise RoutingError("campaign-operability next actions must be unique")
    return tuple(actions)


def _load_approved_scope(
    path: Path,
    *,
    expected_sha256: str,
    expected_release_version: str,
    expected_owner: str,
) -> tuple[dict[str, Any], dict[str, Any]]:
    if SHA256_RE.fullmatch(expected_sha256) is None:
        raise RoutingError("expected approved release-scope SHA-256 is invalid")
    scope, actual_sha256, raw = _strict_object_with_digest(
        path, "approved release-scope decision"
    )
    if actual_sha256 != expected_sha256:
        raise RoutingError(
            "approved release-scope decision bytes do not match the expected SHA-256"
        )
    if set(scope) != APPROVED_SCOPE_FIELDS:
        raise RoutingError("approved release-scope decision field set is not exact")
    canonical = (
        json.dumps(
            scope,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=False,
        )
        + "\n"
    ).encode("utf-8")
    if raw != canonical:
        raise RoutingError(
            "approved release-scope decision bytes are not canonical compact JSON plus LF"
        )
    if (
        scope.get("contractName") != "chummer.release-scope-decision/v1"
        or scope.get("contractVersion") != 1
        or scope.get("status") != "approved"
        or scope.get("channel") != "preview"
        or scope.get("releaseTarget") != "preview"
    ):
        raise RoutingError("approved release-scope decision v1 posture is invalid")
    release_version = _canonical_candidate_token(
        scope.get("releaseVersion"), "approved release version"
    )
    owner = _canonical_candidate_token(
        scope.get("supportOwner"), "approved support owner"
    )
    _canonical_candidate_token(scope.get("decisionId"), "approved decision ID")
    if release_version != expected_release_version:
        raise RoutingError(
            "approved release-scope release version differs from the expected candidate"
        )
    if owner != expected_owner:
        raise RoutingError(
            "candidate bounded owner differs from the approved release-scope support owner"
        )
    platforms = scope.get("platforms")
    if not isinstance(platforms, list) or len(platforms) != 1:
        raise RoutingError(
            "candidate-native desktop proof requires exactly one approved platform"
        )
    platform = platforms[0]
    if not isinstance(platform, dict) or set(platform) != APPROVED_SCOPE_PLATFORM_FIELDS:
        raise RoutingError("approved scope platform field set is not exact")
    for field in (
        "artifactAccessClass",
        "platform",
        "primaryHead",
        "rid",
        "signingRequirement",
    ):
        _canonical_candidate_token(platform.get(field), f"approved platform {field}")
    fallback_heads = platform.get("fallbackHeads")
    if not isinstance(fallback_heads, list):
        raise RoutingError("approved platform fallback heads must be an array")
    normalized_fallbacks = [
        _canonical_candidate_token(item, "approved platform fallback head")
        for item in fallback_heads
    ]
    if len(normalized_fallbacks) != len(set(normalized_fallbacks)):
        raise RoutingError("approved platform fallback heads are duplicated")
    platform_name = str(platform["platform"])
    rid = str(platform["rid"])
    target = CANDIDATE_DESKTOP_TARGETS.get((platform_name, rid))
    if target is None:
        raise RoutingError(
            "candidate-native desktop proof platform/RID is not approved"
        )
    if platform.get("signingRequirement") != target["signingRequirement"]:
        raise RoutingError(
            "approved platform signing requirement does not match its platform/RID"
        )
    if platform.get("primaryHead") in normalized_fallbacks:
        raise RoutingError("approved primary head must not also be a fallback head")
    return scope, platform


def _safe_registry_route_match(
    value: Any,
    *,
    pattern: re.Pattern[str],
    label: str,
) -> re.Match[str]:
    if not isinstance(value, str) or value != value.strip() or not value:
        raise RoutingError(f"{label} must be an exact nonempty route")
    parsed = urlsplit(value)
    if (
        parsed.scheme
        or parsed.netloc
        or parsed.query
        or parsed.fragment
        or not parsed.path.startswith("/")
        or parsed.path.startswith("//")
        or "\\" in parsed.path
        or any(character.isspace() or ord(character) < 32 for character in parsed.path)
    ):
        raise RoutingError(f"{label} must be a safe root-relative route")
    match = pattern.fullmatch(parsed.path)
    if match is None:
        raise RoutingError(f"{label} does not match the Registry route schema")
    for segment in match.groups():
        if segment is None:
            continue
        decoded = unquote(segment)
        if (
            decoded in {".", ".."}
            or "/" in decoded
            or "\\" in decoded
            or any(character.isspace() or ord(character) < 32 for character in decoded)
        ):
            raise RoutingError(f"{label} contains traversal or unsafe bytes")
    return match


def _load_registry_review_seed(
    path: Path,
    *,
    expected_sha256: str,
    expected_release_version: str,
    expected_owner: str,
    scope_platform: dict[str, Any],
) -> tuple[dict[str, Any], str, str, str]:
    if SHA256_RE.fullmatch(expected_sha256) is None:
        raise RoutingError("expected Registry review-seed SHA-256 is invalid")
    seed, actual_sha256, _ = _strict_object_with_digest(
        path, "Registry review seed"
    )
    if actual_sha256 != expected_sha256:
        raise RoutingError(
            "Registry review-seed bytes do not match the expected SHA-256"
        )
    if set(seed) != REGISTRY_REVIEW_SEED_FIELDS:
        raise RoutingError("Registry review seed field set is not exact v2")
    if (
        seed.get("authorityContract") != "chummer.release-authority-snapshot/v2"
        or seed.get("releaseVersion") != expected_release_version
        or seed.get("channel") != "preview"
        or seed.get("status") != "published"
        or seed.get("rolloutState") != "promoted_preview"
        or seed.get("supportabilityState") != "preview_supported"
        or seed.get("releaseDecisionStatus") != "review_required"
        or seed.get("registryRepository")
        != "ArchonMegalon/chummer6-hub-registry"
        or seed.get("manifestPath") != "RELEASE_CHANNEL.json"
        or seed.get("releaseDecisionPath") != "RELEASE_DECISION.json"
    ):
        raise RoutingError(
            "Registry review seed does not have the exact pre-scorecard preview posture"
        )
    if seed.get("supportOwner") != expected_owner:
        raise RoutingError(
            "Registry review-seed support owner differs from the approved candidate"
        )
    registry_commit = _normalize(seed.get("registryCommit"))
    manifest_sha256 = _normalize(seed.get("manifestSha256"))
    if GIT_SHA_RE.fullmatch(registry_commit) is None:
        raise RoutingError("Registry review seed registryCommit is invalid")
    if SHA256_RE.fullmatch(manifest_sha256) is None:
        raise RoutingError("Registry review seed manifestSha256 is invalid")
    release_decision_sha256 = _normalize(seed.get("releaseDecisionSha256"))
    if SHA256_RE.fullmatch(release_decision_sha256) is None:
        raise RoutingError("Registry review seed releaseDecisionSha256 is invalid")
    _concrete_candidate_actions(seed.get("nextActions"))
    platform_name = str(scope_platform["platform"])
    rid = str(scope_platform["rid"])
    target = CANDIDATE_DESKTOP_TARGETS.get((platform_name, rid))
    if target is None:
        raise RoutingError(
            "approved scope platform/RID is not supported by candidate routing"
        )
    if (
        seed.get("availablePlatforms") != [platform_name]
        or seed.get("primaryHeadByPlatform")
        != {platform_name: scope_platform["primaryHead"]}
    ):
        raise RoutingError(
            "Registry review seed platform projection differs from the approved scope"
        )

    artifacts = seed.get("artifacts")
    if not isinstance(artifacts, list) or not artifacts:
        raise RoutingError("Registry review seed has no promoted candidate artifacts")
    if (
        not isinstance(seed.get("artifactCount"), int)
        or isinstance(seed.get("artifactCount"), bool)
        or seed.get("artifactCount") != len(artifacts)
    ):
        raise RoutingError("Registry review seed artifactCount is inconsistent")
    expected_heads = {
        scope_platform["primaryHead"],
        *scope_platform["fallbackHeads"],
    }
    observed_heads: set[str] = set()
    artifact_ids: set[str] = set()
    access_classes: set[str] = set()
    for index, artifact in enumerate(artifacts):
        if not isinstance(artifact, dict) or set(artifact) != REGISTRY_ARTIFACT_FIELDS:
            raise RoutingError(
                f"Registry review seed artifact {index} field set is not exact v2"
            )
        artifact_id = _normalize(artifact.get("artifactId"))
        head = _normalize(artifact.get("head"))
        if not artifact_id or artifact_id in artifact_ids:
            raise RoutingError("Registry review seed artifact IDs are missing or duplicated")
        artifact_ids.add(artifact_id)
        observed_heads.add(head)
        if (
            artifact.get("platform") != platform_name
            or artifact.get("rid") != rid
            or artifact.get("arch") != target["arch"]
            or artifact.get("kind") != "installer"
            or artifact.get("compatibilityState") != "compatible"
            or artifact.get("promotionState") != "promoted"
            or artifact.get("publicationScope") != "signed-in-and-public"
            or artifact.get("revokeState") != "not_revoked"
            or head not in expected_heads
        ):
            raise RoutingError(
                "Registry review seed contains an artifact outside the approved candidate scope"
            )
        if SHA256_RE.fullmatch(_normalize(artifact.get("sha256"))) is None:
            raise RoutingError("Registry review seed artifact SHA-256 is invalid")
        size_bytes = artifact.get("sizeBytes")
        if not isinstance(size_bytes, int) or isinstance(size_bytes, bool) or size_bytes <= 0:
            raise RoutingError("Registry review seed artifact size is invalid")
        access_class = _normalize(artifact.get("installAccessClass"))
        download_route = _normalize(artifact.get("downloadUrl"))
        download_match = _safe_registry_route_match(
            download_route,
            pattern=(
                REGISTRY_GENERATION_FILE_ROUTE
                if access_class == "open_public"
                else REGISTRY_GENERATION_INSTALL_ROUTE
            ),
            label=f"Registry review seed artifact {index} downloadUrl",
        )
        public_route = _normalize(artifact.get("publicInstallRoute"))
        _safe_registry_route_match(
            public_route,
            pattern=REGISTRY_PUBLIC_INSTALL_ROUTE,
            label=f"Registry review seed artifact {index} publicInstallRoute",
        )
        if access_class == "open_public" and public_route == download_route:
            raise RoutingError("Registry review seed open-public routes must be distinct")
        if access_class != "open_public" and (
            public_route != download_route
            or unquote(download_match.group(2)) != artifact_id
        ):
            raise RoutingError(
                "Registry review seed protected routes must equal and end with artifactId"
            )
        access_classes.add(access_class)
    if observed_heads != expected_heads:
        raise RoutingError(
            "Registry review seed does not cover every approved desktop head"
        )
    expected_access_class = _normalize(scope_platform.get("artifactAccessClass"))
    if access_classes != {expected_access_class}:
        raise RoutingError(
            "Registry review seed artifact access class differs from the approved scope"
        )
    if seed.get("downloadAccessPosture") != expected_access_class:
        raise RoutingError(
            "Registry review seed download access posture differs from its artifacts"
        )
    return seed, registry_commit, manifest_sha256, release_decision_sha256


def load_campaign_operability_candidate_context(
    *,
    approved_scope_path: Path,
    expected_scope_sha256: str,
    expected_release_version: str,
    registry_review_seed_path: Path,
    expected_registry_review_seed_sha256: str,
    bounded_owner: str,
    next_actions: Sequence[str],
    allow_raw_fail_declaration: bool,
) -> CampaignOperabilityCandidateContext:
    release_version = _canonical_candidate_token(
        expected_release_version, "expected candidate release version"
    )
    owner = _canonical_candidate_token(bounded_owner, "candidate bounded owner")
    actions = _concrete_candidate_actions(next_actions)
    _, scope_platform = _load_approved_scope(
        approved_scope_path,
        expected_sha256=expected_scope_sha256,
        expected_release_version=release_version,
        expected_owner=owner,
    )
    (
        _,
        registry_commit,
        manifest_sha256,
        release_decision_sha256,
    ) = _load_registry_review_seed(
        registry_review_seed_path,
        expected_sha256=expected_registry_review_seed_sha256,
        expected_release_version=release_version,
        expected_owner=owner,
        scope_platform=scope_platform,
    )
    return CampaignOperabilityCandidateContext(
        release_version=release_version,
        release_scope_decision_sha256=expected_scope_sha256,
        authority_snapshot_sha256=expected_registry_review_seed_sha256,
        registry_commit=registry_commit,
        manifest_sha256=manifest_sha256,
        release_decision_sha256=release_decision_sha256,
        bounded_owner=owner,
        next_actions=actions,
        platform=scope_platform["platform"],
        rid=scope_platform["rid"],
        primary_head=scope_platform["primaryHead"],
        required_heads=tuple(
            [scope_platform["primaryHead"], *scope_platform["fallbackHeads"]]
        ),
        allow_raw_fail_declaration=allow_raw_fail_declaration,
    )


def campaign_operability_candidate_context_from_environment(
    environ: dict[str, str] | None = None,
) -> CampaignOperabilityCandidateContext | None:
    source = os.environ if environ is None else environ
    values = {
        key: str(source.get(variable) or "")
        for key, variable in CAMPAIGN_OPERABILITY_ENV.items()
    }
    mode = values["mode"]
    if mode not in {"", "0", "1"}:
        raise RoutingError("campaign-operability preview mode must be exactly 0 or 1")
    configured_values = [values[key] for key in values if key != "mode"]
    if mode in {"", "0"}:
        if any(value.strip() for value in configured_values):
            raise RoutingError(
                "campaign-operability candidate inputs require explicit preview mode"
            )
        return None
    missing = [
        CAMPAIGN_OPERABILITY_ENV[key]
        for key, value in values.items()
        if key != "mode" and not value.strip()
    ]
    if missing:
        raise RoutingError(
            "campaign-operability preview mode requires the complete candidate plane: "
            + ", ".join(sorted(missing))
        )
    if values["allow_raw_fail"] not in {"0", "1"}:
        raise RoutingError(
            "campaign-operability raw-fail declaration switch must be exactly 0 or 1"
        )
    try:
        next_actions = json.loads(values["next_actions"])
    except json.JSONDecodeError as exc:
        raise RoutingError(
            "campaign-operability next-actions input must be a JSON array"
        ) from exc
    return load_campaign_operability_candidate_context(
        approved_scope_path=Path(values["scope_path"]),
        expected_scope_sha256=values["scope_sha256"].strip(),
        expected_release_version=values["release_version"].strip(),
        registry_review_seed_path=Path(values["review_seed_path"]),
        expected_registry_review_seed_sha256=values["review_seed_sha256"].strip(),
        bounded_owner=values["bounded_owner"].strip(),
        next_actions=next_actions,
        allow_raw_fail_declaration=values["allow_raw_fail"] == "1",
    )


CAMPAIGN_OPERABILITY_PRODUCER_ENV = {
    "desktop-visual": {
        "output": "CHUMMER_DESKTOP_VISUAL_OUTPUT_PATH",
        "release_channel": "CHUMMER_DESKTOP_VISUAL_RELEASE_CHANNEL_PATH",
    },
    "desktop-executable": {
        "output": "CHUMMER_DESKTOP_EXECUTABLE_GATE_PATH",
        "release_channel": "CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH",
    },
    "desktop-workflow": {
        "output": "CHUMMER_DESKTOP_WORKFLOW_OUTPUT_PATH",
        "release_channel": "CHUMMER_DESKTOP_WORKFLOW_EXTERNAL_RELEASE_CHANNEL_PATH",
        "input_root": "CHUMMER_DESKTOP_WORKFLOW_PROOF_INPUT_ROOT",
    },
}


def _lexical_absolute(path: Path) -> Path:
    return Path(os.path.abspath(os.path.expanduser(str(path))))


def preflight_campaign_operability_candidate(
    *,
    producer: str,
    output_path: Path,
    repo_root: Path,
    release_channel_path: Path,
    input_root: Path | None = None,
    environ: dict[str, str] | None = None,
) -> CampaignOperabilityCandidateContext | None:
    source = os.environ if environ is None else environ
    context = campaign_operability_candidate_context_from_environment(source)
    if context is None:
        return None
    required = CAMPAIGN_OPERABILITY_PRODUCER_ENV.get(producer)
    if required is None:
        raise RoutingError(f"unsupported campaign-operability producer: {producer}")
    missing = [
        variable
        for variable in required.values()
        if not str(source.get(variable) or "").strip()
    ]
    if missing:
        raise RoutingError(
            "candidate mode requires an explicit external producer plane: "
            + ", ".join(sorted(missing))
        )

    supplied_paths: dict[str, Path | None] = {
        "output": output_path,
        "release_channel": release_channel_path,
        "input_root": input_root,
    }
    for key, variable in required.items():
        supplied = supplied_paths[key]
        expected = _lexical_absolute(Path(str(source[variable])))
        if supplied is None or _lexical_absolute(supplied) != expected:
            raise RoutingError(
                f"candidate {key} path does not match explicit {variable}"
            )

    output_absolute = _lexical_absolute(output_path)
    published_root = (repo_root / ".codex-studio" / "published").resolve()
    try:
        output_absolute.relative_to(published_root)
    except ValueError:
        pass
    else:
        raise RoutingError(
            "candidate-native desktop proof output must stay outside the tracked public evidence root"
        )
    parent_fd, _ = _open_directory_no_symlink_components(output_absolute.parent)
    try:
        parent_stat = os.fstat(parent_fd)
        if parent_stat.st_uid != os.getuid() or stat.S_IMODE(parent_stat.st_mode) & 0o077:
            raise RoutingError(
                "candidate output parent must be caller-owned with no group/other permissions"
            )
        try:
            existing = os.stat(
                output_absolute.name,
                dir_fd=parent_fd,
                follow_symlinks=False,
            )
        except FileNotFoundError:
            existing = None
        if existing is not None and not stat.S_ISREG(existing.st_mode):
            raise RoutingError(
                "candidate output must not be a symlink or non-regular file"
            )
    finally:
        os.close(parent_fd)

    release_channel, manifest_sha256, _ = _strict_object_with_digest(
        release_channel_path,
        "candidate release channel manifest",
    )
    if manifest_sha256 != context.manifest_sha256:
        raise RoutingError(
            "candidate release-channel bytes do not match Registry manifestSha256"
        )
    release_aliases = [
        release_channel[field]
        for field in ("releaseVersion", "version")
        if field in release_channel
    ]
    if (
        not release_aliases
        or any(
            not isinstance(value, str) or value != value.strip()
            for value in release_aliases
        )
        or len(set(release_aliases)) != 1
        or release_aliases[0] != context.release_version
        or release_channel.get("status") != "published"
    ):
        raise RoutingError(
            "candidate release-channel manifest does not match the approved release"
        )
    _validate_release_channel(release_channel_path)

    if input_root is not None:
        input_fd, _ = _open_directory_no_symlink_components(input_root)
        os.close(input_fd)
    return context


def _candidate_release_aliases(payload: dict[str, Any]) -> list[Any]:
    aliases = [
        payload[field]
        for field in ("releaseVersion", "release_version")
        if field in payload
    ]
    contract_version_fields = {
        "contract_version",
        "contractVersion",
        "schemaVersion",
        "schema_version",
    }
    if "version" in payload and not contract_version_fields.intersection(payload):
        aliases.append(payload["version"])
    return aliases


def decorate_campaign_operability_candidate_payload(
    *,
    producer: str,
    payload: dict[str, Any],
    context: CampaignOperabilityCandidateContext,
) -> dict[str, Any]:
    expected_contract = CAMPAIGN_OPERABILITY_PRODUCER_CONTRACTS.get(producer)
    if expected_contract is None:
        raise RoutingError(f"unsupported campaign-operability producer: {producer}")
    if _contract_name(payload, Path("<generated-payload>")) != expected_contract:
        raise RoutingError(
            f"campaign-operability producer payload contract must be {expected_contract}"
        )
    aliases = _candidate_release_aliases(payload)
    if (
        not aliases
        or any(
            not isinstance(value, str)
            or value != value.strip()
            or CANONICAL_TOKEN_RE.fullmatch(value) is None
            for value in aliases
        )
        or len(set(aliases)) != 1
        or aliases[0] != context.release_version
    ):
        raise RoutingError(
            "generated desktop proof is not bound to the exact candidate release version"
        )
    if (
        "campaign_operability_candidate_binding" in payload
        or "campaign_operability_preview" in payload
    ):
        raise RoutingError("generated desktop proof collides with reserved candidate fields")
    raw_status = payload.get("status")
    raw_verdict_present = "verdict" in payload
    raw_verdict = payload.get("verdict")
    decorated = dict(payload)
    candidate_binding = {
        "contract_name": CAMPAIGN_OPERABILITY_CANDIDATE_BINDING_CONTRACT,
        "contract_version": 1,
        "release_version": context.release_version,
        "release_scope_decision_sha256": context.release_scope_decision_sha256,
        "manifest_sha256": context.manifest_sha256,
        "authority_snapshot_sha256": context.authority_snapshot_sha256,
        "release_decision_sha256": context.release_decision_sha256,
        "registry_commit": context.registry_commit,
        "platform": context.platform,
        "rid": context.rid,
        "primary_head": context.primary_head,
        "required_heads": list(context.required_heads),
    }
    if set(candidate_binding) != CAMPAIGN_OPERABILITY_CANDIDATE_BINDING_FIELDS:
        raise RoutingError("internal campaign-operability candidate binding schema drift")
    decorated["campaign_operability_candidate_binding"] = candidate_binding
    normalized_status = _normalize(raw_status).lower()
    if context.allow_raw_fail_declaration and normalized_status in {"fail", "failed"}:
        reasons = payload.get("reasons")
        if not isinstance(reasons, list) or not any(
            isinstance(item, str) and item.strip() for item in reasons
        ):
            raise RoutingError(
                "raw-fail campaign-operability preview evidence requires explicit failure reasons"
            )
        decorated["campaign_operability_preview"] = {
            "contract_name": "chummer.campaign_operability_preview_evidence",
            "contract_version": 2,
            "status": "pass",
            "release_version": context.release_version,
            "release_scope_decision_sha256": context.release_scope_decision_sha256,
            "bounded_owner": context.bounded_owner,
            "next_actions": list(context.next_actions),
        }
    if decorated.get("status") != raw_status or (
        raw_verdict_present and decorated.get("verdict") != raw_verdict
    ):
        raise RoutingError(
            "campaign-operability candidate decoration changed raw status or verdict"
        )
    return decorated


def decorate_campaign_operability_from_environment(
    *,
    producer: str,
    payload: dict[str, Any],
    output_path: Path,
    repo_root: Path,
    release_channel_path: Path,
    input_root: Path | None = None,
) -> dict[str, Any]:
    context = preflight_campaign_operability_candidate(
        producer=producer,
        output_path=output_path,
        repo_root=repo_root,
        release_channel_path=release_channel_path,
        input_root=input_root,
    )
    if context is None:
        return payload
    return decorate_campaign_operability_candidate_payload(
        producer=producer,
        payload=payload,
        context=context,
    )


def _require_regular_non_symlink(path: Path, label: str) -> None:
    if path.is_symlink():
        raise RoutingError(f"{label} must not be a symbolic link: {path}")
    if not path.is_file():
        raise RoutingError(f"{label} must be an existing regular file: {path}")


def _contract_name(payload: dict[str, Any], path: Path) -> str:
    snake = _normalize(payload.get("contract_name"))
    camel = _normalize(payload.get("contractName"))
    if snake and camel and snake != camel:
        raise RoutingError(f"receipt has conflicting contract aliases: {path}")
    return snake or camel


def _validate_receipt(path: Path, spec: ReceiptSpec) -> None:
    if spec.optional and not path.exists() and not path.is_symlink():
        return
    _require_regular_non_symlink(path, "proof input")
    payload = _load_object(path, "proof input")
    actual_contract = _contract_name(payload, path)
    if spec.contract_name and actual_contract != spec.contract_name:
        raise RoutingError(
            f"proof input contract must be {spec.contract_name}, got "
            f"{actual_contract or '<missing>'}: {path}"
        )
    status = _normalize(payload.get("status")).lower()
    if status not in PASS_STATUSES:
        raise RoutingError(
            f"proof input must be pass/passed/ready, got {status or '<missing>'}: {path}"
        )


def _validate_input_containment(path: Path, input_root: Path) -> None:
    try:
        relative_path = path.relative_to(input_root)
    except ValueError as exc:
        raise RoutingError(f"proof input must stay under explicit input root: {path}") from exc
    current = input_root
    for component in relative_path.parts[:-1]:
        current /= component
        if current.is_symlink():
            raise RoutingError(f"proof input directory must not be a symbolic link: {current}")
    try:
        resolved_root = input_root.resolve(strict=True)
        resolved_path = path.resolve(strict=True)
        resolved_path.relative_to(resolved_root)
    except (OSError, ValueError) as exc:
        raise RoutingError(
            f"proof input resolves outside explicit input root {input_root}: {path}"
        ) from exc


def _validate_release_channel(path: Path) -> None:
    _require_regular_non_symlink(path, "release channel input")
    payload = _load_object(path, "release channel input")
    contract_name = _contract_name(payload, path)
    if contract_name != RELEASE_CHANNEL_CONTRACT:
        raise RoutingError(
            f"release channel input contract must be {RELEASE_CHANNEL_CONTRACT}, got "
            f"{contract_name or '<missing>'}: {path}"
        )
    status = _normalize(payload.get("status")).lower()
    if status != RELEASE_CHANNEL_STATUS:
        raise RoutingError(
            f"release channel input status must be {RELEASE_CHANNEL_STATUS}, got "
            f"{status or '<missing>'}: {path}"
        )
    channel_id_camel = _normalize(payload.get("channelId"))
    channel_id_legacy = _normalize(payload.get("channel"))
    version_plain = _normalize(payload.get("version"))
    version_release = _normalize(payload.get("releaseVersion"))
    published_at_camel = _normalize(payload.get("publishedAt"))
    published_at_snake = _normalize(payload.get("published_at"))
    for label, left, right in (
        ("channelId/channel", channel_id_camel, channel_id_legacy),
        ("version/releaseVersion", version_plain, version_release),
        ("publishedAt/published_at", published_at_camel, published_at_snake),
    ):
        if left and right and left != right:
            raise RoutingError(f"release channel input has conflicting {label} aliases: {path}")
    channel_id = channel_id_camel or channel_id_legacy
    version = version_plain or version_release
    published_at = published_at_camel or published_at_snake
    missing = [
        label
        for label, value in (
            ("channelId/channel", channel_id),
            ("version/releaseVersion", version),
            ("publishedAt/published_at", published_at),
        )
        if not value
    ]
    if missing:
        raise RoutingError(
            f"release channel input is missing required field(s) {', '.join(missing)}: {path}"
        )


def _ledger_specs(repo_root: Path, input_root: Path, edition: str) -> list[ReceiptSpec]:
    ledger_relative, _ = LEDGER_CONFIG[edition]
    ledger_path = repo_root / ledger_relative
    payload = _load_object(ledger_path, f"{edition.upper()} workflow parity ledger")
    specs: dict[str, ReceiptSpec] = {}
    for family in payload.get("requiredFamilies") or []:
        if not isinstance(family, dict):
            continue
        family_id = _normalize(family.get("id"))
        if not family_id:
            continue
        for field, contract_name in FAMILY_CONTRACTS[edition].items():
            for raw in family.get(field) or []:
                value = _normalize(raw).replace("{familyId}", family_id)
                if not value:
                    continue
                relative = Path(value)
                try:
                    mapped = relative.relative_to(PUBLISHED_PREFIX)
                except ValueError as exc:
                    raise RoutingError(
                        f"{edition.upper()} ledger receipt must stay under {PUBLISHED_PREFIX}: {value}"
                    ) from exc
                spec = ReceiptSpec(str(mapped), contract_name)
                specs[spec.relative_path] = spec
    return [specs[key] for key in sorted(specs)]


def required_inputs(
    producer: str,
    repo_root: Path,
    input_root: Path | None,
) -> list[tuple[ReceiptSpec, Path]]:
    if producer not in STATIC_INPUTS:
        raise RoutingError(f"unknown producer: {producer}")
    specs = list(STATIC_INPUTS[producer])
    if producer in {"sr4", "sr6"}:
        if input_root is None:
            raise RoutingError(f"{producer} external plane requires a proof input root")
        specs.extend(_ledger_specs(repo_root, input_root, producer))
    elif producer == "desktop-workflow":
        if input_root is None:
            raise RoutingError("desktop-workflow external plane requires a proof input root")
        specs.extend(_ledger_specs(repo_root, input_root, "sr4"))
        specs.extend(_ledger_specs(repo_root, input_root, "sr6"))

    if specs and input_root is None:
        raise RoutingError(f"{producer} external plane requires a proof input root")
    return [(spec, input_root / spec.relative_path) for spec in specs] if input_root else []


def _paths_alias(left: Path, right: Path) -> bool:
    try:
        if left.resolve(strict=False) == right.resolve(strict=False):
            return True
        return left.exists() and right.exists() and left.samefile(right)
    except OSError as exc:
        raise RoutingError(f"could not compare proof paths {left} and {right}: {exc}") from exc


def _path_contains(container: Path, child: Path) -> bool:
    try:
        container_resolved = container.resolve(strict=False)
        child_resolved = child.resolve(strict=False)
    except OSError as exc:
        raise RoutingError(f"could not resolve proof paths {container} and {child}: {exc}") from exc
    return container_resolved == child_resolved or container_resolved in child_resolved.parents


def _validate_output(path: Path, protected_paths: Iterable[Path], label: str) -> None:
    if path.is_symlink():
        raise RoutingError(f"{label} must not be a symbolic link: {path}")
    if path.exists() and not path.is_file():
        raise RoutingError(f"existing {label} must be a regular file: {path}")
    for protected in protected_paths:
        if _paths_alias(path, protected):
            raise RoutingError(f"{label} must not alias proof input: {protected}")


def preflight_external_plane(
    *,
    producer: str,
    output_path: Path,
    repo_root: Path,
    release_channel_path: Path,
    input_root: Path | None = None,
    sidecar_output: Path | None = None,
) -> list[Path]:
    if producer not in OUTPUT_CONTRACTS:
        raise RoutingError(f"unknown producer: {producer}")
    if input_root is not None:
        if input_root.is_symlink() or not input_root.is_dir():
            raise RoutingError(
                f"proof input root must be an existing non-symlink directory: {input_root}"
            )
    _validate_release_channel(release_channel_path)
    resolved_inputs = required_inputs(producer, repo_root, input_root)
    for spec, path in resolved_inputs:
        if spec.optional and not path.exists() and not path.is_symlink():
            continue
        _validate_receipt(path, spec)
        if input_root is not None:
            _validate_input_containment(path, input_root)
    protected_paths = [release_channel_path, *(path for _, path in resolved_inputs)]
    if input_root is not None:
        protected_paths.append(input_root)
    _validate_output(output_path, protected_paths, "proof output")
    if input_root is not None and (
        _path_contains(input_root, output_path) or _path_contains(output_path, input_root)
    ):
        raise RoutingError("proof output and explicit input root must not overlap")
    if sidecar_output is not None:
        if sidecar_output.is_symlink():
            raise RoutingError(f"sidecar output must not be a symbolic link: {sidecar_output}")
        if sidecar_output.exists() and not sidecar_output.is_dir():
            raise RoutingError(f"existing sidecar output must be a directory: {sidecar_output}")
        for protected in protected_paths:
            if _paths_alias(sidecar_output, protected):
                raise RoutingError(f"sidecar output must not alias proof input: {protected}")
            if _path_contains(sidecar_output, protected):
                raise RoutingError(f"sidecar output must not contain proof input: {protected}")
            if protected == input_root and _path_contains(protected, sidecar_output):
                raise RoutingError("sidecar output and explicit input root must not overlap")
        if _path_contains(output_path, sidecar_output) or _path_contains(sidecar_output, output_path):
            raise RoutingError("proof output and sidecar output must not overlap")
    return protected_paths


def _validate_output_payload(producer: str, payload: dict[str, Any]) -> None:
    contract_name = _contract_name(payload, Path("<generated-payload>"))
    expected = OUTPUT_CONTRACTS[producer]
    if contract_name != expected:
        raise RoutingError(
            f"generated output contract must be {expected}, got {contract_name or '<missing>'}"
        )
    status = _normalize(payload.get("status")).lower()
    if status not in OUTPUT_STATUSES:
        raise RoutingError(f"generated output has unsupported status: {status or '<missing>'}")


def atomic_write_json(
    *,
    producer: str,
    output_path: Path,
    payload: dict[str, Any],
    repo_root: Path,
    release_channel_path: Path,
    input_root: Path | None = None,
) -> None:
    _validate_output_payload(producer, payload)
    preflight_external_plane(
        producer=producer,
        output_path=output_path,
        repo_root=repo_root,
        release_channel_path=release_channel_path,
        input_root=input_root,
    )
    directory_fd, absolute_parent = _open_directory_no_symlink_components(
        output_path.parent,
        create=True,
    )
    temporary_name = f".{output_path.name}.{uuid.uuid4().hex}.tmp"
    try:
        opened = os.fstat(directory_fd)
        observed = os.lstat(absolute_parent)
        if (opened.st_dev, opened.st_ino) != (observed.st_dev, observed.st_ino):
            raise RoutingError("proof output parent identity changed before write")
        temporary_flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
        temporary_flags |= getattr(os, "O_NOFOLLOW", 0)
        temporary_fd = os.open(
            temporary_name,
            temporary_flags,
            0o600,
            dir_fd=directory_fd,
        )
        with os.fdopen(temporary_fd, mode="w", encoding="utf-8") as handle:
            json.dump(payload, handle, indent=2)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        # Revalidate immediately before replacement. Directory-relative replace
        # holds the opened parent identity and replaces a raced output symlink or
        # hardlink entry rather than following it.
        preflight_external_plane(
            producer=producer,
            output_path=output_path,
            repo_root=repo_root,
            release_channel_path=release_channel_path,
            input_root=input_root,
        )
        observed = os.lstat(absolute_parent)
        if (opened.st_dev, opened.st_ino) != (observed.st_dev, observed.st_ino):
            raise RoutingError("proof output parent identity changed before replace")
        os.replace(
            temporary_name,
            output_path.name,
            src_dir_fd=directory_fd,
            dst_dir_fd=directory_fd,
        )
        temporary_name = ""
        os.fsync(directory_fd)
    finally:
        if temporary_name:
            try:
                os.unlink(temporary_name, dir_fd=directory_fd)
            except FileNotFoundError:
                pass
        os.close(directory_fd)


def atomic_replace_directory(
    *,
    producer: str,
    source: Path,
    output_path: Path,
    repo_root: Path,
    release_channel_path: Path,
    input_root: Path,
) -> None:
    if not source.is_dir() or source.is_symlink():
        raise RoutingError(f"sidecar source must be an existing non-symlink directory: {source}")
    preflight_external_plane(
        producer=producer,
        output_path=output_path.parent / f".{output_path.name}.receipt-probe",
        repo_root=repo_root,
        release_channel_path=release_channel_path,
        input_root=input_root,
        sidecar_output=output_path,
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    staged = Path(tempfile.mkdtemp(prefix=f".{output_path.name}.", dir=output_path.parent))
    backup = output_path.parent / f".{output_path.name}.backup.{uuid.uuid4().hex}"
    moved_existing = False
    try:
        shutil.copytree(source, staged, dirs_exist_ok=True)
        preflight_external_plane(
            producer=producer,
            output_path=output_path.parent / f".{output_path.name}.receipt-probe",
            repo_root=repo_root,
            release_channel_path=release_channel_path,
            input_root=input_root,
            sidecar_output=output_path,
        )
        if output_path.exists():
            os.replace(output_path, backup)
            moved_existing = True
        try:
            os.replace(staged, output_path)
        except BaseException:
            if moved_existing and backup.exists() and not output_path.exists():
                os.replace(backup, output_path)
                moved_existing = False
            raise
        if moved_existing:
            if backup.is_symlink() or backup.is_file():
                backup.unlink()
            else:
                shutil.rmtree(backup)
            moved_existing = False
    finally:
        if staged.exists():
            shutil.rmtree(staged)
        if moved_existing and backup.exists() and not output_path.exists():
            os.replace(backup, output_path)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    for command in ("preflight", "replace-directory", "campaign-preflight"):
        child = subparsers.add_parser(command)
        child.add_argument("--producer", required=True, choices=sorted(OUTPUT_CONTRACTS))
        child.add_argument("--output", required=True, type=Path)
        child.add_argument("--repo-root", required=True, type=Path)
        child.add_argument("--release-channel", required=True, type=Path)
        child.add_argument("--input-root", type=Path)
        child.add_argument("--sidecar-output", type=Path)
        if command == "replace-directory":
            child.add_argument("--source", required=True, type=Path)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        if args.command == "preflight":
            preflight_external_plane(
                producer=args.producer,
                output_path=args.output,
                repo_root=args.repo_root,
                release_channel_path=args.release_channel,
                input_root=args.input_root,
                sidecar_output=args.sidecar_output,
            )
        elif args.command == "replace-directory":
            if args.input_root is None:
                raise RoutingError("replace-directory requires --input-root")
            atomic_replace_directory(
                producer=args.producer,
                source=args.source,
                output_path=args.output,
                repo_root=args.repo_root,
                release_channel_path=args.release_channel,
                input_root=args.input_root,
            )
        else:
            preflight_campaign_operability_candidate(
                producer=args.producer,
                output_path=args.output,
                repo_root=args.repo_root,
                release_channel_path=args.release_channel,
                input_root=args.input_root,
            )
    except RoutingError as exc:
        print(f"[candidate-proof-routing] FAIL: {exc}", file=os.sys.stderr)
        return 65
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
