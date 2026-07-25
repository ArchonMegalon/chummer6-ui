#!/usr/bin/env python3
"""Fail-closed contracts for the governed macOS flagship evidence lane."""

from __future__ import annotations

import argparse
import base64
import binascii
import datetime as dt
import hashlib
import json
import os
import re
import sys
from pathlib import Path, PurePosixPath
from typing import Any
from urllib.parse import urlparse


AUTHORITY_CONTRACT = "chummer6-ui.macos-flagship-build-authority"
PREDECESSOR_CONTRACT = "chummer6-ui.macos-predecessor-handoff"
PREDECESSOR_VERIFICATION_CONTRACT = (
    "chummer6-ui.macos-predecessor-verification"
)
EVIDENCE_CONTRACT = "chummer6-ui.macos-flagship-evidence"
EVIDENCE_CONTRACT_VERSION = 2
HANDOFF_CONTRACT = "chummer6-ui.macos-flagship-evidence-handoff"
ESCROW_CONTRACT = "chummer6-ui.macos-flagship-candidate-escrow.v1"
ESCROW_RECEIPT_FILE = "MACOS_FLAGSHIP_CANDIDATE_ESCROW.generated.json"
ESCROW_CIPHERTEXT_FILE = (
    "chummer-avalonia-osx-arm64-installer.dmg.aes256gcm"
)
SIGNING_IDENTITY_CONTRACT = (
    "chummer6-ui.macos-signing-notarization-identity.v1"
)
WORKFLOW_PATH = ".github/workflows/macos-flagship-evidence.yml"
RERUN_POLICY = "same-actor-only"
UI_REPOSITORY = "ArchonMegalon/chummer6-ui"
UI_RELEASE_REF = "refs/heads/main"
HUB_REPOSITORY = "ArchonMegalon/chummer6-hub"
HUB_BOOTSTRAP_SCRIPT = "scripts/run-mac-release-bootstrap.sh"
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
COMMIT_PATTERN = re.compile(r"^[0-9a-f]{40}$")
UUID_PATTERN = re.compile(
    r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-"
    r"[0-9a-f]{4}-[0-9a-f]{12}$"
)
VERSION_PATTERN = re.compile(
    r"^run-(?P<date>[0-9]{8})-(?P<time>[0-9]{6})$"
)
REF_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._/-]{0,199}$")
LOGIN_PATTERN = re.compile(
    r"^(?:github-actions\[bot\]|[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?)$"
)
MAX_ARTIFACT_BYTES = 512 * 1024 * 1024

AGGREGATE_REFERENCE_KEYS = {
    "authorityReceipt",
    "cleanStartupReceipt",
    "completedUpdateState",
    "inventory",
    "manualUpdateState",
    "notaryResult",
    "pendingDeliveryReceipt",
    "postUpdateStartupReceipt",
    "predecessorVerification",
    "runtimeObservations",
    "signingIdentityReceipt",
    "signingReceipt",
    "stageManifest",
    "stageOnlyReceipt",
}

AGGREGATE_INPUT_BINDING_KEYS = {
    "authorityReceiptSha256",
    "cleanStartupReceiptSha256",
    "completedUpdateStateSha256",
    "manualUpdateStateSha256",
    "notaryResultSha256",
    "pendingDeliveryReceiptSha256",
    "postUpdateStartupReceiptSha256",
    "predecessorVerificationSha256",
    "runtimeObservationsSha256",
    "signingIdentityReceiptSha256",
    "signingReceiptSha256",
    "stageManifestSha256",
    "stageOnlyReceiptSha256",
}


AUTHORITY_KEYS = {
    "authorizedAtUtc",
    "candidateId",
    "contractName",
    "contractVersion",
    "coreCommit",
    "coreRef",
    "expiresAtUtc",
    "head",
    "generationId",
    "hubCommit",
    "hubRef",
    "launchTarget",
    "legacyCommit",
    "legacyRef",
    "mediaFactoryCommit",
    "mediaFactoryRef",
    "predecessorSelectionAuthority",
    "ref",
    "registryCommit",
    "registryRef",
    "releaseChannel",
    "releaseVersion",
    "repository",
    "rid",
    "runnerNonce",
    "scopeDecisionAuthority",
    "scopeDecisionSha256",
    "sha",
    "uiCommit",
    "uiKitCommit",
    "uiKitRef",
    "uiRef",
    "workflow",
}

PREDECESSOR_KEYS = {
    "artifactFileName",
    "artifactId",
    "artifactSha256",
    "artifactSizeBytes",
    "artifactUrl",
    "contractName",
    "contractVersion",
    "generationId",
    "head",
    "releaseManifestSha256",
    "releaseManifestUrl",
    "releaseVersion",
    "rid",
}

SCOPE_KEYS = {
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

SCOPE_PLATFORM_KEYS = {
    "artifactAccessClass",
    "fallbackHeads",
    "platform",
    "primaryHead",
    "rid",
    "signingRequirement",
}

OBSERVATION_CHECKS = {
    "candidateDmgCodesign",
    "candidateDmgGatekeeper",
    "candidateDmgStaple",
    "candidateHostArchitecture",
    "cleanInstallCopied",
    "coreStartup",
    "gatekeeperAssessmentsEnabled",
    "installedAppCodesign",
    "installedAppGatekeeper",
    "postUpdateStartup",
    "postUpdateUninstallRemoved",
    "predecessorAppGatekeeper",
    "predecessorUpdateStateObserved",
    "quarantineApplied",
    "updateCompletionStateObserved",
    "updateManualInstallCopied",
    "uninstallRemoved",
}


class ContractError(ValueError):
    pass


def fail(message: str) -> None:
    raise ContractError(message)


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


def require_regular_file(path: Path, label: str) -> None:
    if path.is_symlink() or not path.is_file():
        fail(f"{label} must be a regular non-symlink file")


def read_json_bytes(path: Path, label: str) -> tuple[dict[str, Any], bytes]:
    require_regular_file(path, label)
    raw = path.read_bytes()
    if not raw or len(raw) > 4 * 1024 * 1024 or b"\x00" in raw:
        fail(f"{label} has an invalid byte envelope")
    try:
        payload = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        fail(f"{label} is not valid UTF-8 JSON: {error}")
    if not isinstance(payload, dict):
        fail(f"{label} must be a JSON object")
    return payload, raw


def canonical_json(payload: dict[str, Any]) -> str:
    return json.dumps(
        payload, sort_keys=True, separators=(",", ":"), ensure_ascii=False
    )


def read_canonical_json(path: Path, label: str) -> tuple[dict[str, Any], bytes]:
    payload, raw = read_json_bytes(path, label)
    expected = canonical_json(payload).encode("utf-8")
    if raw != expected:
        fail(f"{label} must use exact canonical JSON serialization")
    return payload, raw


def require_exact_keys(
    payload: Any, expected: set[str], label: str
) -> None:
    if not isinstance(payload, dict):
        fail(f"{label} must be a JSON object")
    observed = set(payload)
    if observed != expected:
        missing = sorted(expected - observed)
        extra = sorted(observed - expected)
        fail(f"{label} has missing keys {missing} or extra keys {extra}")


def require_string(
    payload: dict[str, Any], key: str, label: str, *, maximum: int = 1024
) -> str:
    value = payload.get(key)
    if (
        not isinstance(value, str)
        or not value
        or len(value) > maximum
        or any(ord(character) < 32 for character in value)
    ):
        fail(f"{label} {key} must be a bounded non-empty string")
    return value


def require_sha256(payload: dict[str, Any], key: str, label: str) -> str:
    value = require_string(payload, key, label, maximum=64)
    if SHA256_PATTERN.fullmatch(value) is None:
        fail(f"{label} {key} must be an exact lowercase SHA-256")
    return value


def require_commit(payload: dict[str, Any], key: str, label: str) -> str:
    value = require_string(payload, key, label, maximum=40)
    if COMMIT_PATTERN.fullmatch(value) is None:
        fail(f"{label} {key} must be an exact lowercase 40-hex commit")
    return value


def require_ref(payload: dict[str, Any], key: str, label: str) -> str:
    value = require_string(payload, key, label, maximum=200)
    if (
        REF_PATTERN.fullmatch(value) is None
        or ".." in value
        or "//" in value
        or "@{" in value
        or value.endswith(("/", ".", ".lock"))
    ):
        fail(f"{label} {key} is not a safe reviewed Git ref")
    return value


def parse_timestamp(value: str, label: str) -> dt.datetime:
    if not value.endswith("Z"):
        fail(f"{label} must use a UTC Z timestamp")
    try:
        parsed = dt.datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError:
        fail(f"{label} is not a valid timestamp")
    if parsed.tzinfo is None:
        fail(f"{label} must include timezone authority")
    return parsed.astimezone(dt.timezone.utc)


def parse_now(raw: str | None) -> dt.datetime:
    if raw:
        return parse_timestamp(raw, "--now")
    return dt.datetime.now(dt.timezone.utc)


def version_stamp(value: str, label: str) -> str:
    match = VERSION_PATTERN.fullmatch(value)
    if match is None:
        fail(f"{label} must be a canonical run version")
    return match.group("date") + match.group("time")


def version_timestamp(value: str, label: str) -> dt.datetime:
    stamp = version_stamp(value, label)
    return dt.datetime.strptime(stamp, "%Y%m%d%H%M%S").replace(
        tzinfo=dt.timezone.utc
    )


def validate_url(raw: str, label: str, *, expect_json: bool = False) -> str:
    parsed = urlparse(raw)
    if (
        parsed.scheme != "https"
        or parsed.hostname != "chummer.run"
        or parsed.port not in (None, 443)
        or parsed.username is not None
        or parsed.password is not None
        or parsed.query
        or parsed.fragment
        or not parsed.path.startswith("/downloads/")
        or "\\" in parsed.path
        or "/../" in parsed.path
        or "/./" in parsed.path
        or "%2e" in parsed.path.lower()
    ):
        fail(f"{label} must be a credential-free immutable chummer.run URL")
    if expect_json and not parsed.path.endswith(".json"):
        fail(f"{label} must name a JSON release manifest")
    return raw


def validate_authority(
    authority: dict[str, Any],
    scope: dict[str, Any],
    scope_raw: bytes,
    *,
    expected_repository: str,
    expected_ref: str,
    expected_sha: str,
    expected_actor: str,
    expected_triggering_actor: str,
    expected_run_id: str,
    expected_run_attempt: str,
    now: dt.datetime,
) -> dict[str, str]:
    require_exact_keys(authority, AUTHORITY_KEYS, "release authority")
    if expected_repository != UI_REPOSITORY or expected_ref != UI_RELEASE_REF:
        fail("macOS flagship authority is restricted to chummer6-ui main")
    if authority.get("contractName") != AUTHORITY_CONTRACT:
        fail("release authority contractName mismatch")
    if authority.get("contractVersion") != 1:
        fail("release authority contractVersion mismatch")
    exact = {
        "repository": expected_repository,
        "workflow": WORKFLOW_PATH,
        "ref": expected_ref,
        "sha": expected_sha,
        "releaseChannel": "preview",
        "head": "avalonia",
        "rid": "osx-arm64",
        "launchTarget": "Chummer.Avalonia",
    }
    for key, value in exact.items():
        if authority.get(key) != value:
            fail(f"release authority {key} mismatch")
    if (
        LOGIN_PATTERN.fullmatch(expected_actor) is None
        or LOGIN_PATTERN.fullmatch(expected_triggering_actor) is None
    ):
        fail("GitHub actor or triggering actor is not an exact login")
    if expected_triggering_actor != expected_actor:
        fail("GitHub reruns are restricted to the original workflow actor")
    if (
        re.fullmatch(r"[1-9][0-9]*", expected_run_id) is None
        or re.fullmatch(r"[1-9][0-9]*", expected_run_attempt) is None
    ):
        fail("GitHub run ID or run attempt is invalid")
    runner_nonce = require_string(
        authority, "runnerNonce", "release authority", maximum=64
    )
    if re.fullmatch(r"[a-z0-9]{12,64}", runner_nonce) is None:
        fail("release authority runnerNonce is not canonical")
    for key in ("candidateId", "generationId"):
        value = require_string(
            authority, key, "release authority", maximum=128
        )
        if re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._+-]{0,127}", value) is None:
            fail(f"release authority {key} is not portable")

    for key in (
        "sha",
        "uiCommit",
        "coreCommit",
        "hubCommit",
        "uiKitCommit",
        "registryCommit",
        "mediaFactoryCommit",
        "legacyCommit",
    ):
        require_commit(authority, key, "release authority")
    if authority["uiCommit"] != expected_sha:
        fail("release authority uiCommit must equal the workflow source SHA")
    for key in (
        "uiRef",
        "coreRef",
        "hubRef",
        "uiKitRef",
        "registryRef",
        "mediaFactoryRef",
        "legacyRef",
    ):
        require_ref(authority, key, "release authority")

    release_version = require_string(
        authority, "releaseVersion", "release authority", maximum=80
    )
    release_timestamp = version_timestamp(
        release_version, "release authority releaseVersion"
    )
    if (
        release_timestamp < now - dt.timedelta(hours=24)
        or release_timestamp > now + dt.timedelta(minutes=5)
    ):
        fail("release authority releaseVersion must be a fresh run timestamp")
    validate_scope_decision(
        scope,
        scope_raw,
        release_version=release_version,
        expected_actor=expected_actor,
        now=now,
    )
    decision_sha = require_sha256(
        authority, "scopeDecisionSha256", "release authority"
    )
    if sha256_bytes(scope_raw) != decision_sha:
        fail("release scope decision bytes do not match the authorized SHA-256")
    decision_authority = require_string(
        authority,
        "scopeDecisionAuthority",
        "release authority",
        maximum=1024,
    )
    if (
        decision_authority.startswith(("file:", "http:", "https:"))
        or decision_sha not in decision_authority.lower()
    ):
        fail(
            "release scope authority must be immutable, non-file, and contain its SHA-256"
        )

    authorized_at = parse_timestamp(
        require_string(authority, "authorizedAtUtc", "release authority"),
        "release authority authorizedAtUtc",
    )
    expires_at = parse_timestamp(
        require_string(authority, "expiresAtUtc", "release authority"),
        "release authority expiresAtUtc",
    )
    if authorized_at > now + dt.timedelta(minutes=5):
        fail("release authority is future-dated")
    if expires_at <= now:
        fail("release authority is expired")
    if expires_at < now + dt.timedelta(hours=4):
        fail("release authority must remain valid for the full evidence window")
    if expires_at <= authorized_at or expires_at - authorized_at > dt.timedelta(
        hours=24
    ):
        fail("release authority validity window must be positive and at most 24 hours")

    return {
        "CHUMMER_RELEASE_VERSION": release_version,
        "CHUMMER_RELEASE_CHANNEL": "preview",
        "CHUMMER_RELEASE_APP": "avalonia",
        "CHUMMER_RELEASE_RID": "osx-arm64",
        "CHUMMER_RELEASE_SCOPE_DECISION_EXPECTED_SHA256": decision_sha,
        "CHUMMER_RELEASE_SCOPE_DECISION_AUTHORITY": decision_authority,
        "CHUMMER_UI_REF": authority["uiRef"],
        "CHUMMER_UI_EXPECTED_COMMIT": authority["uiCommit"],
        "CHUMMER_CORE_REF": authority["coreRef"],
        "CHUMMER_CORE_EXPECTED_COMMIT": authority["coreCommit"],
        "CHUMMER_HUB_REF": authority["hubRef"],
        "CHUMMER_HUB_EXPECTED_COMMIT": authority["hubCommit"],
        "CHUMMER_UI_KIT_REF": authority["uiKitRef"],
        "CHUMMER_UI_KIT_EXPECTED_COMMIT": authority["uiKitCommit"],
        "CHUMMER_HUB_REGISTRY_REF": authority["registryRef"],
        "CHUMMER_HUB_REGISTRY_EXPECTED_COMMIT": authority["registryCommit"],
        "CHUMMER_MEDIA_FACTORY_REF": authority["mediaFactoryRef"],
        "CHUMMER_MEDIA_FACTORY_EXPECTED_COMMIT": authority[
            "mediaFactoryCommit"
        ],
        "CHUMMER_LEGACY_REF": authority["legacyRef"],
        "CHUMMER_LEGACY_EXPECTED_COMMIT": authority["legacyCommit"],
        "CHUMMER_MAC_RELEASE_STAGE_ONLY": "1",
        "CHUMMER_MACOS_RUNNER_LABEL": (
            "chummer-macos-flagship-" + runner_nonce
        ),
        "CHUMMER_ALLOW_UNSIGNED_PREVIEW": "1",
    }


def validate_scope_decision(
    scope: dict[str, Any],
    raw: bytes,
    *,
    release_version: str,
    expected_actor: str,
    now: dt.datetime,
) -> None:
    require_exact_keys(scope, SCOPE_KEYS, "release scope decision")
    canonical = (canonical_json(scope) + "\n").encode("utf-8")
    if raw != canonical:
        fail("release scope decision must be canonical compact JSON plus LF")
    if (
        scope.get("contractName") != "chummer.release-scope-decision/v1"
        or scope.get("contractVersion") != 1
        or scope.get("status") != "approved"
        or scope.get("channel") != "preview"
        or scope.get("releaseTarget") != "preview"
        or scope.get("releaseVersion") != release_version
    ):
        fail("release scope decision does not approve this preview candidate")
    approved_by = require_string(
        scope, "approvedBy", "release scope decision", maximum=160
    )
    if approved_by.casefold() == expected_actor.casefold():
        fail("release scope decision requires an independent approver")
    approved_at = parse_timestamp(
        require_string(
            scope, "approvedAtUtc", "release scope decision", maximum=40
        ),
        "release scope decision approvedAtUtc",
    )
    if (
        approved_at > now + dt.timedelta(minutes=5)
        or approved_at < now - dt.timedelta(hours=24)
    ):
        fail("release scope approval must be fresh and not future-dated")
    for key in ("decisionId", "supportOwner"):
        value = require_string(
            scope, key, "release scope decision", maximum=160
        )
        if re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._:/ -]{0,159}", value) is None:
            fail(f"release scope decision {key} is not canonical")
    platforms = scope.get("platforms")
    if not isinstance(platforms, list) or len(platforms) != 1:
        fail("release scope decision must approve exactly one platform")
    platform = platforms[0]
    if not isinstance(platform, dict):
        fail("release scope decision platform must be an object")
    require_exact_keys(platform, SCOPE_PLATFORM_KEYS, "release scope platform")
    expected = {
        "artifactAccessClass": "open_public",
        "platform": "macos",
        "primaryHead": "avalonia",
        "rid": "osx-arm64",
        "signingRequirement": "signed",
    }
    for key, value in expected.items():
        if platform.get(key) != value:
            fail(f"release scope platform {key} mismatch")
    fallbacks = platform.get("fallbackHeads")
    if (
        not isinstance(fallbacks, list)
        or len(fallbacks) > 8
        or "avalonia" in fallbacks
        or len(fallbacks) != len(set(fallbacks))
        or any(
            not isinstance(item, str)
            or re.fullmatch(r"[a-z][a-z0-9-]{0,39}", item) is None
            for item in fallbacks
        )
    ):
        fail("release scope platform fallbackHeads are invalid")


def validate_predecessor_schema(predecessor: dict[str, Any]) -> None:
    require_exact_keys(predecessor, PREDECESSOR_KEYS, "predecessor handoff")
    if predecessor.get("contractName") != PREDECESSOR_CONTRACT:
        fail("predecessor handoff contractName mismatch")
    if predecessor.get("contractVersion") != 1:
        fail("predecessor handoff contractVersion mismatch")
    head = require_string(predecessor, "head", "predecessor handoff", maximum=40)
    rid = require_string(predecessor, "rid", "predecessor handoff", maximum=40)
    if head != "avalonia" or rid != "osx-arm64":
        fail("predecessor handoff must identify avalonia osx-arm64")
    version_stamp(
        require_string(
            predecessor, "releaseVersion", "predecessor handoff", maximum=80
        ),
        "predecessor releaseVersion",
    )
    expected_id = f"{head}-{rid}-installer"
    if predecessor.get("artifactId") != expected_id:
        fail("predecessor artifactId mismatch")
    expected_name = f"chummer-{head}-{rid}-installer.dmg"
    if predecessor.get("artifactFileName") != expected_name:
        fail("predecessor artifactFileName mismatch")
    require_sha256(predecessor, "artifactSha256", "predecessor handoff")
    require_sha256(
        predecessor, "releaseManifestSha256", "predecessor handoff"
    )
    size = predecessor.get("artifactSizeBytes")
    if (
        isinstance(size, bool)
        or not isinstance(size, int)
        or size < 1
        or size > MAX_ARTIFACT_BYTES
    ):
        fail("predecessor artifactSizeBytes is outside the fixed bound")
    generation_id = require_string(
        predecessor, "generationId", "predecessor handoff", maximum=160
    )
    if (
        not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,159}", generation_id)
        or ".." in generation_id
    ):
        fail("predecessor generationId is not canonical")
    artifact_url = validate_url(
        require_string(
            predecessor, "artifactUrl", "predecessor handoff", maximum=2048
        ),
        "predecessor artifactUrl",
    )
    manifest_url = validate_url(
        require_string(
            predecessor,
            "releaseManifestUrl",
            "predecessor handoff",
            maximum=2048,
        ),
        "predecessor releaseManifestUrl",
        expect_json=True,
    )
    generation_root = f"/downloads/g/{generation_id}"
    if (
        urlparse(artifact_url).path
        != f"{generation_root}/files/{expected_name}"
        or urlparse(manifest_url).path
        != f"{generation_root}/RELEASE_CHANNEL.generated.json"
    ):
        fail("predecessor URLs do not bind the exact generation and artifact")


def validate_predecessor(
    predecessor: dict[str, Any], candidate: dict[str, Any]
) -> None:
    validate_predecessor_schema(predecessor)
    for key in ("head", "rid"):
        if predecessor.get(key) != candidate.get(key):
            fail(f"predecessor handoff {key} does not match the candidate")
    predecessor_version = require_string(
        predecessor, "releaseVersion", "predecessor handoff", maximum=80
    )
    candidate_version = require_string(
        candidate, "releaseVersion", "release authority", maximum=80
    )
    if version_stamp(
        predecessor_version, "predecessor releaseVersion"
    ) >= version_stamp(candidate_version, "candidate releaseVersion"):
        fail("predecessor version must be older than the candidate version")


def atomic_write(path: Path, payload: dict[str, Any], *, compact: bool = False) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.tmp-{os.getpid()}")
    text = (
        canonical_json(payload)
        if compact
        else json.dumps(payload, indent=2, sort_keys=True) + "\n"
    )
    with temporary.open("x", encoding="utf-8") as handle:
        handle.write(text)
        handle.flush()
        os.fsync(handle.fileno())
    os.replace(temporary, path)


def append_environment(path: Path, values: dict[str, str]) -> None:
    with path.open("a", encoding="utf-8") as handle:
        for key, value in values.items():
            if (
                not re.fullmatch(r"[A-Z][A-Z0-9_]*", key)
                or not value
                or "\r" in value
                or "\n" in value
            ):
                fail(f"refusing unsafe GitHub environment output {key}")
            handle.write(f"{key}={value}\n")


def command_validate_authority(args: argparse.Namespace) -> int:
    authority, authority_raw = read_canonical_json(
        args.authority, "release authority"
    )
    scope, scope_raw = read_json_bytes(
        args.scope_decision, "release scope decision"
    )
    if not scope:
        fail("release scope decision must not be empty")
    predecessor, predecessor_raw = read_canonical_json(
        args.predecessor, "predecessor handoff"
    )
    values = validate_authority(
        authority,
        scope,
        scope_raw,
        expected_repository=args.expected_repository,
        expected_ref=args.expected_ref,
        expected_sha=args.expected_sha,
        expected_actor=args.expected_actor,
        expected_triggering_actor=args.expected_triggering_actor,
        expected_run_id=args.expected_run_id,
        expected_run_attempt=args.expected_run_attempt,
        now=parse_now(args.now),
    )
    validate_predecessor(predecessor, authority)
    predecessor_handoff_sha = sha256_bytes(predecessor_raw)
    selection_authority = require_string(
        authority,
        "predecessorSelectionAuthority",
        "release authority",
        maximum=1024,
    )
    if (
        selection_authority.startswith(("file:", "http:", "https:"))
        or predecessor_handoff_sha not in selection_authority.lower()
        or predecessor["releaseVersion"] not in selection_authority
        or authority["releaseVersion"] not in selection_authority
    ):
        fail(
            "predecessor selection authority must bind the reviewed N-1 handoff"
        )
    values["CHUMMER_RELEASE_SCOPE_DECISION_PATH"] = str(
        args.scope_decision.resolve()
    )
    if args.github_env:
        append_environment(args.github_env, values)

    receipt = {
        "authoritySha256": sha256_bytes(authority_raw),
        "candidateId": authority["candidateId"],
        "contractName": "chummer6-ui.macos-flagship-authority-validation",
        "contractVersion": 1,
        "generatedAtUtc": dt.datetime.now(dt.timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z"),
        "github": {
            "actor": args.expected_actor,
            "ref": args.expected_ref,
            "repository": args.expected_repository,
            "rerunPolicy": RERUN_POLICY,
            "runAttempt": args.expected_run_attempt,
            "runId": args.expected_run_id,
            "sha": args.expected_sha,
            "triggeringActor": args.expected_triggering_actor,
            "workflow": WORKFLOW_PATH,
        },
        "generationId": authority["generationId"],
        "nonPublishing": {
            "countsAsPublicationEvidence": False,
            "evidenceArtifactUploadAllowed": True,
            "publicActivationAttempted": False,
            "publicationAttempted": False,
            "releaseUploadAttempted": False,
        },
        "predecessorHandoffSha256": sha256_bytes(predecessor_raw),
        "predecessorSelectionAuthority": selection_authority,
        "releaseVersion": authority["releaseVersion"],
        "rid": authority["rid"],
        "runnerLabel": "chummer-macos-flagship-" + authority["runnerNonce"],
        "scopeDecisionAuthority": authority["scopeDecisionAuthority"],
        "scopeDecisionSha256": sha256_bytes(scope_raw),
        "bootstrapSource": {
            "commit": authority["hubCommit"],
            "ref": authority["hubRef"],
            "repository": HUB_REPOSITORY,
            "script": HUB_BOOTSTRAP_SCRIPT,
        },
        "sourcePins": {
            "core": {
                "commit": authority["coreCommit"],
                "ref": authority["coreRef"],
            },
            "hub": {
                "commit": authority["hubCommit"],
                "ref": authority["hubRef"],
            },
            "legacy": {
                "commit": authority["legacyCommit"],
                "ref": authority["legacyRef"],
            },
            "mediaFactory": {
                "commit": authority["mediaFactoryCommit"],
                "ref": authority["mediaFactoryRef"],
            },
            "registry": {
                "commit": authority["registryCommit"],
                "ref": authority["registryRef"],
            },
            "ui": {
                "commit": authority["uiCommit"],
                "ref": authority["uiRef"],
            },
            "uiKit": {
                "commit": authority["uiKitCommit"],
                "ref": authority["uiKitRef"],
            },
        },
        "status": "pass",
        "uiCommit": authority["uiCommit"],
    }
    atomic_write(args.output, receipt)
    return 0


def artifact_row(
    manifest: dict[str, Any], predecessor: dict[str, Any]
) -> dict[str, Any]:
    matches: list[dict[str, Any]] = []
    for item in manifest.get("artifacts") or []:
        if not isinstance(item, dict):
            continue
        rid = str(item.get("rid") or "").strip().lower()
        if not rid:
            arch = str(item.get("arch") or "").strip().lower()
            rid = f"osx-{arch}" if arch in {"arm64", "x64"} else ""
        if (
            str(item.get("artifactId") or item.get("id") or "")
            == predecessor["artifactId"]
            and str(item.get("head") or "").strip().lower()
            == predecessor["head"]
            and str(item.get("platform") or "").strip().lower() == "macos"
            and rid == predecessor["rid"]
            and str(item.get("fileName") or "")
            == predecessor["artifactFileName"]
        ):
            matches.append(item)
    if len(matches) != 1:
        fail("predecessor manifest must contain exactly one matching artifact")
    return matches[0]


def command_verify_predecessor(args: argparse.Namespace) -> int:
    predecessor, predecessor_raw = read_canonical_json(
        args.predecessor, "predecessor handoff"
    )
    validate_predecessor_schema(predecessor)
    manifest, manifest_raw = read_json_bytes(
        args.manifest, "predecessor release manifest"
    )
    require_regular_file(args.artifact, "predecessor artifact")
    if (
        sha256_bytes(manifest_raw) != predecessor["releaseManifestSha256"]
        or sha256_file(args.artifact) != predecessor["artifactSha256"]
        or args.artifact.stat().st_size != predecessor["artifactSizeBytes"]
    ):
        fail("downloaded predecessor bytes do not match the exact handoff")
    if str(manifest.get("version") or "") != predecessor["releaseVersion"]:
        fail("predecessor manifest version mismatch")
    observed_generation = str(
        manifest.get("generationId")
        or manifest.get("generation")
        or manifest.get("releaseGenerationId")
        or ""
    )
    if observed_generation != predecessor["generationId"]:
        fail("predecessor manifest generation mismatch")
    row = artifact_row(manifest, predecessor)
    if (
        str(row.get("sha256") or "").lower() != predecessor["artifactSha256"]
        or row.get("sizeBytes") != predecessor["artifactSizeBytes"]
    ):
        fail("predecessor manifest artifact integrity does not match the handoff")

    receipt = {
        "artifact": {
            "artifactId": predecessor["artifactId"],
            "fileName": predecessor["artifactFileName"],
            "sha256": predecessor["artifactSha256"],
            "sizeBytes": predecessor["artifactSizeBytes"],
        },
        "contractName": PREDECESSOR_VERIFICATION_CONTRACT,
        "contractVersion": 1,
        "generationId": predecessor["generationId"],
        "handoffSha256": sha256_bytes(predecessor_raw),
        "head": predecessor["head"],
        "manifestUrl": predecessor["releaseManifestUrl"],
        "manifestSha256": sha256_bytes(manifest_raw),
        "releaseVersion": predecessor["releaseVersion"],
        "rid": predecessor["rid"],
        "status": "pass",
        "artifactUrl": predecessor["artifactUrl"],
    }
    atomic_write(args.output, receipt)
    return 0


def normalize_digest(value: Any) -> str:
    raw = str(value or "").strip().lower()
    return raw.removeprefix("sha256:")


def require_receipt_contract(
    path: Path,
    label: str,
    contract_name: str,
    contract_version: int | None = None,
) -> tuple[dict[str, Any], bytes]:
    payload, raw = read_json_bytes(path, label)
    if payload.get("contractName") != contract_name:
        fail(f"{label} contractName mismatch")
    if (
        contract_version is not None
        and payload.get("contractVersion") != contract_version
    ):
        fail(f"{label} contractVersion mismatch")
    if str(payload.get("status") or "").lower() != "pass":
        fail(f"{label} is not passing")
    return payload, raw


def require_startup_receipt(
    path: Path,
    label: str,
    *,
    release_version: str,
    rid: str,
    artifact_sha: str,
) -> dict[str, Any]:
    payload, _ = read_json_bytes(path, label)
    if (
        str(payload.get("status") or "").lower() != "pass"
        or payload.get("headId") != "avalonia"
        or payload.get("platform") != "macos"
        or payload.get("rid") != rid
        or payload.get("releaseVersion") != release_version
        or payload.get("readyCheckpoint") != "pre_ui_event_loop"
        or normalize_digest(payload.get("artifactDigest")) != artifact_sha
    ):
        fail(f"{label} does not bind the exact installed candidate")
    return payload


def find_stage_artifact(
    manifest: dict[str, Any], *, rid: str, file_name: str
) -> dict[str, Any]:
    rows = [
        row
        for row in (manifest.get("artifacts") or [])
        if isinstance(row, dict)
        and str(row.get("head") or "").lower() == "avalonia"
        and str(row.get("platform") or "").lower() == "macos"
        and (
            str(row.get("rid") or "").lower() == rid
            or f"osx-{str(row.get('arch') or '').lower()}" == rid
        )
        and row.get("fileName") == file_name
    ]
    if len(rows) != 1:
        fail("stage manifest must contain exactly one source macOS artifact")
    return rows[0]


def inventory_row(role: str, path: Path) -> dict[str, Any]:
    require_regular_file(path, role)
    return {
        "fileName": path.name,
        "role": role,
        "sha256": sha256_file(path),
        "sizeBytes": path.stat().st_size,
    }


def portable_receipt_reference(path: Path) -> dict[str, Any]:
    require_regular_file(path, "native E2E evidence")
    return {
        "path": f"receipts/{path.name}",
        "sha256": sha256_file(path),
        "sizeBytes": path.stat().st_size,
    }


def _decode_json_object(raw: bytes, label: str) -> dict[str, Any]:
    try:
        value = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        fail(f"{label} is not valid UTF-8 JSON: {error}")
    if not isinstance(value, dict):
        fail(f"{label} must be a JSON object")
    return value


def _validate_reference_bytes(
    reference: Any,
    reference_files: dict[str, bytes],
    label: str,
) -> tuple[dict[str, Any], bytes]:
    require_exact_keys(reference, {"path", "sha256", "sizeBytes"}, label)
    portable_path = str(reference.get("path") or "")
    path = PurePosixPath(portable_path)
    if (
        path.as_posix() != portable_path
        or len(path.parts) != 2
        or path.parts[0] != "receipts"
        or path.parts[1] in {"", ".", ".."}
        or "/" in path.parts[1]
        or "\\" in portable_path
    ):
        fail(f"{label} path is not a portable receipts reference")
    digest = str(reference.get("sha256") or "")
    size = reference.get("sizeBytes")
    if (
        SHA256_PATTERN.fullmatch(digest) is None
        or isinstance(size, bool)
        or not isinstance(size, int)
        or size < 1
        or size > 16 * 1024 * 1024
    ):
        fail(f"{label} digest or size is invalid")
    raw = reference_files.get(portable_path)
    if (
        not isinstance(raw, bytes)
        or len(raw) != size
        or sha256_bytes(raw) != digest
    ):
        fail(f"{label} does not bind the supplied bytes")
    return _decode_json_object(raw, label), raw


def _passing(payload: dict[str, Any], label: str) -> None:
    if str(payload.get("status") or "").lower() not in {"pass", "passed"}:
        fail(f"{label} is not passing")


def validate_aggregate_receipt(
    payload: dict[str, Any],
    reference_files: dict[str, bytes],
    *,
    expected_candidate: dict[str, Any] | None = None,
    expected_global_identity: dict[str, Any] | None = None,
    expected_github: dict[str, Any] | None = None,
    expected_certificate_sha256: str | None = None,
    expected_certificate_spki_sha256: str | None = None,
    expected_developer_id_application_identity: str | None = None,
    expected_team_id: str | None = None,
) -> dict[str, Any]:
    """Purely validate aggregate authority using caller-supplied receipt bytes."""
    if not isinstance(reference_files, dict):
        fail("macOS aggregate reference files must be a path-to-bytes map")
    require_exact_keys(
        payload,
        {
            "candidate",
            "cleanInstall",
            "contractName",
            "contractVersion",
            "generatedAtUtc",
            "github",
            "globalCandidateIdentity",
            "inputBindings",
            "inventorySha256",
            "nonPublishing",
            "references",
            "releaseVersion",
            "rid",
            "signing",
            "sourceUnsignedCandidate",
            "status",
            "updateDelivery",
        },
        "macOS aggregate evidence",
    )
    if (
        payload.get("contractName") != EVIDENCE_CONTRACT
        or payload.get("contractVersion") != EVIDENCE_CONTRACT_VERSION
        or payload.get("status") != "pass"
    ):
        fail("macOS aggregate evidence contract is invalid")
    parse_timestamp(
        str(payload.get("generatedAtUtc") or ""),
        "macOS aggregate generatedAtUtc",
    )
    release_version = str(payload.get("releaseVersion") or "")
    version_stamp(release_version, "macOS aggregate releaseVersion")
    if payload.get("rid") != "osx-arm64":
        fail("macOS aggregate RID mismatch")

    candidate = payload.get("candidate")
    require_exact_keys(
        candidate,
        {"artifactId", "fileName", "sha256", "sizeBytes"},
        "macOS aggregate candidate",
    )
    if (
        candidate.get("artifactId") != "avalonia-osx-arm64-installer"
        or candidate.get("fileName")
        != "chummer-avalonia-osx-arm64-installer.dmg"
        or SHA256_PATTERN.fullmatch(str(candidate.get("sha256") or ""))
        is None
        or isinstance(candidate.get("sizeBytes"), bool)
        or not isinstance(candidate.get("sizeBytes"), int)
        or candidate["sizeBytes"] < 1
        or candidate["sizeBytes"] > MAX_ARTIFACT_BYTES
    ):
        fail("macOS aggregate candidate identity is invalid")
    if expected_candidate is not None and candidate != expected_candidate:
        fail("macOS aggregate candidate differs from caller authority")

    global_identity = payload.get("globalCandidateIdentity")
    require_exact_keys(
        global_identity,
        {
            "candidateId",
            "generationId",
            "previousReleaseVersion",
            "releaseVersion",
            "sourceCommit",
        },
        "macOS aggregate global identity",
    )
    for key in ("candidateId", "generationId"):
        if (
            re.fullmatch(
                r"[A-Za-z0-9][A-Za-z0-9._+-]{0,127}",
                str(global_identity.get(key) or ""),
            )
            is None
        ):
            fail(f"macOS aggregate global identity {key} is invalid")
    predecessor_version = str(
        global_identity.get("previousReleaseVersion") or ""
    )
    if (
        global_identity.get("releaseVersion") != release_version
        or version_stamp(predecessor_version, "aggregate predecessor")
        >= version_stamp(release_version, "aggregate candidate")
        or COMMIT_PATTERN.fullmatch(
            str(global_identity.get("sourceCommit") or "")
        )
        is None
    ):
        fail("macOS aggregate global identity is inconsistent")
    if (
        expected_global_identity is not None
        and global_identity != expected_global_identity
    ):
        fail("macOS aggregate global identity differs from caller authority")

    github = payload.get("github")
    require_exact_keys(
        github,
        {
            "actor",
            "ref",
            "repository",
            "rerunPolicy",
            "runAttempt",
            "runId",
            "sha",
            "triggeringActor",
            "workflow",
        },
        "macOS aggregate GitHub provenance",
    )
    if (
        github.get("repository") != UI_REPOSITORY
        or github.get("ref") != UI_RELEASE_REF
        or github.get("workflow") != WORKFLOW_PATH
        or github.get("sha") != global_identity["sourceCommit"]
        or COMMIT_PATTERN.fullmatch(str(github.get("sha") or "")) is None
        or LOGIN_PATTERN.fullmatch(str(github.get("actor") or "")) is None
        or LOGIN_PATTERN.fullmatch(str(github.get("triggeringActor") or ""))
        is None
        or github.get("triggeringActor") != github.get("actor")
        or github.get("rerunPolicy") != RERUN_POLICY
        or re.fullmatch(r"[1-9][0-9]*", str(github.get("runId") or ""))
        is None
        or re.fullmatch(
            r"[1-9][0-9]*", str(github.get("runAttempt") or "")
        )
        is None
    ):
        fail("macOS aggregate GitHub provenance is invalid")
    if expected_github is not None and github != expected_github:
        fail("macOS aggregate GitHub provenance differs from caller authority")

    nonpublishing = payload.get("nonPublishing")
    require_exact_keys(
        nonpublishing,
        {
            "countsAsPublicationEvidence",
            "evidenceArtifactUploadAllowed",
            "publicActivationAttempted",
            "publicationAttempted",
            "releaseUploadCredentialAccepted",
            "releaseUploadAttempted",
        },
        "macOS aggregate nonpublishing posture",
    )
    if nonpublishing != {
        "countsAsPublicationEvidence": False,
        "evidenceArtifactUploadAllowed": True,
        "publicActivationAttempted": False,
        "publicationAttempted": False,
        "releaseUploadCredentialAccepted": False,
        "releaseUploadAttempted": False,
    }:
        fail("macOS aggregate nonpublishing posture is not fail-closed")

    references = payload.get("references")
    require_exact_keys(
        references, AGGREGATE_REFERENCE_KEYS, "macOS aggregate references"
    )
    decoded: dict[str, dict[str, Any]] = {}
    raw_by_key: dict[str, bytes] = {}
    observed_paths: set[str] = set()
    for key in sorted(AGGREGATE_REFERENCE_KEYS):
        reference = references[key]
        path = str(reference.get("path") or "") if isinstance(reference, dict) else ""
        if path in observed_paths:
            fail("macOS aggregate references must use unique portable paths")
        observed_paths.add(path)
        decoded[key], raw_by_key[key] = _validate_reference_bytes(
            reference, reference_files, f"macOS aggregate references.{key}"
        )

    input_bindings = payload.get("inputBindings")
    require_exact_keys(
        input_bindings,
        AGGREGATE_INPUT_BINDING_KEYS,
        "macOS aggregate input bindings",
    )
    binding_sources = {
        "authorityReceiptSha256": "authorityReceipt",
        "cleanStartupReceiptSha256": "cleanStartupReceipt",
        "completedUpdateStateSha256": "completedUpdateState",
        "manualUpdateStateSha256": "manualUpdateState",
        "notaryResultSha256": "notaryResult",
        "pendingDeliveryReceiptSha256": "pendingDeliveryReceipt",
        "postUpdateStartupReceiptSha256": "postUpdateStartupReceipt",
        "predecessorVerificationSha256": "predecessorVerification",
        "runtimeObservationsSha256": "runtimeObservations",
        "signingIdentityReceiptSha256": "signingIdentityReceipt",
        "signingReceiptSha256": "signingReceipt",
        "stageManifestSha256": "stageManifest",
        "stageOnlyReceiptSha256": "stageOnlyReceipt",
    }
    for binding_key, reference_key in binding_sources.items():
        if input_bindings.get(binding_key) != sha256_bytes(
            raw_by_key[reference_key]
        ):
            fail(f"macOS aggregate {binding_key} does not match its reference")
    if payload.get("inventorySha256") != sha256_bytes(raw_by_key["inventory"]):
        fail("macOS aggregate inventory digest mismatch")

    authority = decoded["authorityReceipt"]
    _passing(authority, "authority receipt")
    if (
        authority.get("contractName")
        != "chummer6-ui.macos-flagship-authority-validation"
        or authority.get("contractVersion") != 1
        or authority.get("candidateId") != global_identity["candidateId"]
        or authority.get("generationId") != global_identity["generationId"]
        or authority.get("releaseVersion") != release_version
        or authority.get("rid") != "osx-arm64"
        or authority.get("github") != github
    ):
        fail("authority receipt does not bind the aggregate identity")

    signing = payload.get("signing")
    require_exact_keys(
        signing,
        {
            "candidateDmgGatekeeperStatus",
            "certificateSha256",
            "certificateSpkiSha256",
            "developerIdApplicationIdentity",
            "gatekeeperAssessmentsEnabled",
            "installedAppGatekeeperStatus",
            "notarizationStatus",
            "notarySubmissionId",
            "postUpdateAppGatekeeperStatus",
            "staplerValidationStatus",
            "signingStatus",
            "teamId",
        },
        "macOS aggregate signing authority",
    )
    team_id = str(signing.get("teamId") or "")
    identity = str(signing.get("developerIdApplicationIdentity") or "")
    certificate_sha = str(signing.get("certificateSha256") or "")
    spki_sha = str(signing.get("certificateSpkiSha256") or "")
    if (
        re.fullmatch(r"[A-Z0-9]{10}", team_id) is None
        or not identity.startswith("Developer ID Application:")
        or not identity.endswith(f"({team_id})")
        or SHA256_PATTERN.fullmatch(certificate_sha) is None
        or SHA256_PATTERN.fullmatch(spki_sha) is None
        or UUID_PATTERN.fullmatch(
            str(signing.get("notarySubmissionId") or "")
        )
        is None
        or signing.get("notarizationStatus") != "Accepted"
        or signing.get("gatekeeperAssessmentsEnabled") is not True
        or any(
            signing.get(key) != "pass"
            for key in (
                "candidateDmgGatekeeperStatus",
                "installedAppGatekeeperStatus",
                "postUpdateAppGatekeeperStatus",
                "staplerValidationStatus",
                "signingStatus",
            )
        )
    ):
        fail("macOS aggregate signing/notary/Gatekeeper authority is invalid")
    if (
        expected_certificate_sha256 is not None
        and certificate_sha != expected_certificate_sha256
    ):
        fail("macOS aggregate certificate SHA-256 differs from trusted pin")
    if (
        expected_certificate_spki_sha256 is not None
        and spki_sha != expected_certificate_spki_sha256
    ):
        fail("macOS aggregate certificate SPKI differs from trusted pin")
    if (
        expected_developer_id_application_identity is not None
        and identity != expected_developer_id_application_identity
    ):
        fail("macOS aggregate Developer ID differs from caller authority")
    if expected_team_id is not None and team_id != expected_team_id:
        fail("macOS aggregate team ID differs from caller authority")

    signing_receipt = decoded["signingReceipt"]
    _passing(signing_receipt, "v2 signing receipt")
    signing_rows = [
        row
        for row in (signing_receipt.get("artifacts") or [])
        if isinstance(row, dict)
        and row.get("fileName") == candidate["fileName"]
        and row.get("sha256") == candidate["sha256"]
        and row.get("signingStatus") == "pass"
        and row.get("notarizationStatus") == "pass"
    ]
    if (
        signing_receipt.get("contractName")
        != "chummer6-ui.desktop_artifact_signing"
        or signing_receipt.get("contractVersion") != 2
        or signing_receipt.get("app") != "avalonia"
        or signing_receipt.get("platform") != "macos"
        or signing_receipt.get("releaseVersion") != release_version
        or signing_receipt.get("rid") != "osx-arm64"
        or signing_receipt.get("signingStatus") != "pass"
        or signing_receipt.get("notarizationStatus") != "pass"
        or len(signing_rows) != 1
    ):
        fail("v2 signing receipt does not bind the aggregate candidate")

    identity_receipt = decoded["signingIdentityReceipt"]
    _passing(identity_receipt, "signing identity receipt")
    identity_certificate = identity_receipt.get("certificate")
    identity_notary = identity_receipt.get("notarization")
    if (
        identity_receipt.get("contractName") != SIGNING_IDENTITY_CONTRACT
        or identity_receipt.get("contractVersion") != 1
        or identity_receipt.get("artifact") != {
            "fileName": candidate["fileName"],
            "sha256": candidate["sha256"],
            "sizeBytes": candidate["sizeBytes"],
        }
        or identity_certificate
        != {
            "developerIdApplicationIdentity": identity,
            "sha256": certificate_sha,
            "spkiSha256": spki_sha,
            "teamId": team_id,
        }
        or identity_receipt.get("provenance") != github
        or identity_receipt.get("releaseVersion") != release_version
        or identity_receipt.get("rid") != "osx-arm64"
        or identity_receipt.get("signingReceiptSha256")
        != sha256_bytes(raw_by_key["signingReceipt"])
        or identity_receipt.get("sourceAuthorityReceiptSha256")
        != sha256_bytes(raw_by_key["authorityReceipt"])
        or not isinstance(identity_notary, dict)
    ):
        fail("signing identity receipt does not bind aggregate authority")

    notary_result = decoded["notaryResult"]
    submission_id = str(signing.get("notarySubmissionId") or "")
    if (
        notary_result.get("status") != "Accepted"
        or str(notary_result.get("id") or "").lower() != submission_id
        or identity_notary.get("status") != "Accepted"
        or identity_notary.get("submissionId") != submission_id
        or identity_notary.get("resultSha256")
        != sha256_bytes(raw_by_key["notaryResult"])
    ):
        fail("accepted notary result does not bind aggregate authority")

    for key, label in (
        ("cleanStartupReceipt", "clean startup receipt"),
        ("postUpdateStartupReceipt", "post-update startup receipt"),
    ):
        startup = decoded[key]
        _passing(startup, label)
        if (
            startup.get("headId") != "avalonia"
            or startup.get("platform") != "macos"
            or startup.get("rid") != "osx-arm64"
            or startup.get("releaseVersion") != release_version
            or startup.get("readyCheckpoint") != "pre_ui_event_loop"
            or normalize_digest(startup.get("artifactDigest"))
            != candidate["sha256"]
        ):
            fail(f"{label} does not bind the aggregate candidate")

    predecessor = decoded["predecessorVerification"]
    _passing(predecessor, "predecessor verification")
    predecessor_artifact = predecessor.get("artifact")
    require_exact_keys(
        predecessor_artifact,
        {"fileName", "sha256", "sizeBytes"},
        "predecessor verification artifact",
    )
    update = payload.get("updateDelivery")
    require_exact_keys(
        update,
        {
            "automaticApplySupported",
            "candidatePendingInstallerSha256",
            "completionStateSha256",
            "deliveryMode",
            "platformPolicyReason",
            "postUpdateStartupReceiptSha256",
            "predecessorArtifactSha256",
            "predecessorVersion",
            "targetVersion",
        },
        "macOS aggregate update delivery",
    )
    if (
        predecessor.get("contractName")
        != PREDECESSOR_VERIFICATION_CONTRACT
        or predecessor.get("contractVersion") != 1
        or predecessor.get("head") != "avalonia"
        or predecessor.get("rid") != "osx-arm64"
        or predecessor.get("releaseVersion") != predecessor_version
        or predecessor_artifact.get("fileName") != candidate["fileName"]
        or SHA256_PATTERN.fullmatch(
            str(predecessor_artifact.get("sha256") or "")
        )
        is None
        or isinstance(predecessor_artifact.get("sizeBytes"), bool)
        or not isinstance(predecessor_artifact.get("sizeBytes"), int)
        or predecessor_artifact["sizeBytes"] < 1
        or predecessor_artifact["sizeBytes"] > MAX_ARTIFACT_BYTES
        or predecessor_artifact.get("sha256")
        != update.get("predecessorArtifactSha256")
        or SHA256_PATTERN.fullmatch(
            str(authority.get("predecessorHandoffSha256") or "")
        )
        is None
        or predecessor.get("handoffSha256")
        != authority.get("predecessorHandoffSha256")
        or authority.get("predecessorSelectionAuthority")
        != (
            "governance://global-flagship/n-minus-one/"
            f"{predecessor_version}/to/{release_version}/sha256/"
            f"{authority.get('predecessorHandoffSha256')}"
        )
        or update.get("automaticApplySupported") is not False
        or update.get("deliveryMode") != "macos_manual_installer_handoff"
        or update.get("platformPolicyReason")
        != (
            "macOS DMG updates are downloaded and integrity-checked in-app, "
            "then require a Gatekeeper-visible manual install."
        )
        or update.get("candidatePendingInstallerSha256")
        != candidate["sha256"]
        or update.get("predecessorVersion") != predecessor_version
        or update.get("targetVersion") != release_version
        or update.get("postUpdateStartupReceiptSha256")
        != sha256_bytes(raw_by_key["postUpdateStartupReceipt"])
        or update.get("completionStateSha256")
        != sha256_bytes(raw_by_key["completedUpdateState"])
    ):
        fail("predecessor-to-candidate update authority is inconsistent")

    manual = decoded["manualUpdateState"]
    completed = decoded["completedUpdateState"]
    pending = decoded["pendingDeliveryReceipt"]
    _passing(pending, "pending delivery receipt")
    pending_name = str(pending.get("pendingInstallerFileName") or "")
    if (
        manual.get("InstalledVersion") != predecessor_version
        or manual.get("PendingUpdateVersion") != release_version
        or manual.get("LastFailureReason")
        != "macos_manual_install_required"
        or manual.get("PendingInstallerPath")
        != pending_name
        or manual.get("PendingInstallerPathDisclosure") != "file_name_only"
        or SHA256_PATTERN.fullmatch(
            str(manual.get("ObservedStateSha256") or "")
        )
        is None
        or not pending_name
        or PurePosixPath(pending_name).name != pending_name
        or "\\" in pending_name
        or pending.get("contractName")
        != "chummer6-ui.macos-pending-installer-delivery"
        or pending.get("contractVersion") != 1
        or pending.get("releaseVersion") != release_version
        or pending.get("stateSha256")
        != sha256_bytes(raw_by_key["manualUpdateState"])
        or pending.get("pendingInstallerSha256") != candidate["sha256"]
        or pending.get("pendingInstallerSizeBytes") != candidate["sizeBytes"]
        or completed.get("InstalledVersion") != release_version
        or completed.get("PendingUpdateVersion") not in (None, "")
        or completed.get("PendingInstallerPath") not in (None, "")
        or completed.get("LastFailureReason") not in (None, "")
        or SHA256_PATTERN.fullmatch(
            str(completed.get("ObservedStateSha256") or "")
        )
        is None
    ):
        fail("manual update state chain is inconsistent")

    runtime = decoded["runtimeObservations"]
    runtime_checks = runtime.get("checks")
    if (
        runtime.get("contractName")
        != "chummer6-ui.macos-flagship-runtime-observations"
        or runtime.get("contractVersion") != 1
        or runtime.get("releaseVersion") != release_version
        or runtime.get("rid") != "osx-arm64"
        or not isinstance(runtime_checks, dict)
        or set(runtime_checks) != OBSERVATION_CHECKS
        or any(value is not True for value in runtime_checks.values())
        or runtime.get("signingAuthority")
        != {"identity": identity, "teamId": team_id}
    ):
        fail("runtime observations do not bind aggregate authority")

    stage = decoded["stageOnlyReceipt"]
    if (
        stage.get("contractName") != "chummer.run.mac_release_stage_only"
        or stage.get("status") != "pass"
        or stage.get("releaseVersion") != release_version
        or stage.get("rid") != "osx-arm64"
        or stage.get("mode") != "stage_only"
        or stage.get("outputPathDisclosure") != "directory_name_only"
        or SHA256_PATTERN.fullmatch(
            str(stage.get("sourceReceiptSha256") or "")
        )
        is None
        or any(
            stage.get(key) is not False
            for key in (
                "uploadAttempted",
                "publicationAttempted",
                "publicActivationAttempted",
                "countsAsPublicationEvidence",
            )
        )
    ):
        fail("stage-only receipt is not nonpublishing")

    inventory = decoded["inventory"]
    _passing(inventory, "evidence inventory")
    inventory_rows = inventory.get("files")
    if (
        inventory.get("contractName")
        != "chummer6-ui.macos-flagship-evidence-inventory"
        or inventory.get("contractVersion") != 1
        or inventory.get("releaseVersion") != release_version
        or inventory.get("rid") != "osx-arm64"
        or not isinstance(inventory_rows, list)
    ):
        fail("evidence inventory identity is invalid")
    inventory_tuples = {
        (
            row.get("fileName"),
            row.get("sha256"),
            row.get("sizeBytes"),
        )
        for row in inventory_rows
        if isinstance(row, dict)
    }
    for key in AGGREGATE_REFERENCE_KEYS - {"inventory"}:
        reference = references[key]
        if (
            PurePosixPath(reference["path"]).name,
            reference["sha256"],
            reference["sizeBytes"],
        ) not in inventory_tuples:
            fail(f"evidence inventory omits aggregate reference {key}")
    if (
        (
            candidate["fileName"],
            candidate["sha256"],
            candidate["sizeBytes"],
        )
        not in inventory_tuples
    ):
        fail("evidence inventory omits the signed candidate identity")

    clean = payload.get("cleanInstall")
    require_exact_keys(
        clean,
        {
            "coreStartupReceiptSha256",
            "gatekeeperAssessment",
            "installRootClass",
            "quarantineAssessment",
            "uninstall",
        },
        "macOS aggregate clean install",
    )
    if (
        clean.get("coreStartupReceiptSha256")
        != sha256_bytes(raw_by_key["cleanStartupReceipt"])
        or clean.get("gatekeeperAssessment") != "pass"
        or clean.get("installRootClass")
        != "isolated_applications_equivalent"
        or clean.get("quarantineAssessment") != "pass"
        or clean.get("uninstall") != "pass"
    ):
        fail("clean-install authority is inconsistent")

    source = payload.get("sourceUnsignedCandidate")
    require_exact_keys(
        source,
        {"fileName", "sha256", "sizeBytes"},
        "macOS aggregate unsigned source",
    )
    if (
        source.get("fileName") != candidate["fileName"]
        or SHA256_PATTERN.fullmatch(str(source.get("sha256") or "")) is None
        or isinstance(source.get("sizeBytes"), bool)
        or not isinstance(source.get("sizeBytes"), int)
        or source["sizeBytes"] < 1
        or source["sizeBytes"] > MAX_ARTIFACT_BYTES
    ):
        fail("unsigned source identity is invalid")
    stage_manifest = decoded["stageManifest"]
    source_rows = [
        row
        for row in (stage_manifest.get("artifacts") or [])
        if isinstance(row, dict)
        and str(row.get("head") or "").lower() == "avalonia"
        and str(row.get("platform") or "").lower() == "macos"
        and (
            str(row.get("rid") or "").lower() == "osx-arm64"
            or str(row.get("arch") or "").lower() == "arm64"
        )
        and row.get("fileName") == source["fileName"]
        and row.get("sha256") == source["sha256"]
        and row.get("sizeBytes") == source["sizeBytes"]
    ]
    if len(source_rows) != 1:
        fail("stage manifest does not bind the exact unsigned source")

    return {
        "candidate": dict(candidate),
        "certificateSha256": certificate_sha,
        "certificateSpkiSha256": spki_sha,
        "developerIdApplicationIdentity": identity,
        "github": dict(github),
        "globalCandidateIdentity": dict(global_identity),
        "notarySubmissionId": submission_id,
        "references": dict(references),
        "releaseVersion": release_version,
        "rid": "osx-arm64",
        "teamId": team_id,
    }


def command_emit_signing_identity(args: argparse.Namespace) -> int:
    authority, authority_raw = require_receipt_contract(
        args.authority_receipt,
        "authority receipt",
        "chummer6-ui.macos-flagship-authority-validation",
        1,
    )
    signing, signing_raw = read_json_bytes(
        args.signing_receipt, "signing receipt"
    )
    notary, notary_raw = read_json_bytes(
        args.notary_result, "notarytool result"
    )
    require_regular_file(args.candidate_artifact, "signed candidate artifact")
    candidate_sha = sha256_file(args.candidate_artifact)
    candidate_size = args.candidate_artifact.stat().st_size
    rows = [
        row
        for row in (signing.get("artifacts") or [])
        if isinstance(row, dict)
        and row.get("fileName") == args.candidate_artifact.name
        and normalize_digest(row.get("sha256")) == candidate_sha
        and row.get("signingStatus") == "pass"
        and row.get("notarizationStatus") == "pass"
    ]
    if (
        signing.get("contractName")
        != "chummer6-ui.desktop_artifact_signing"
        or signing.get("contractVersion") != 2
        or signing.get("releaseVersion") != authority.get("releaseVersion")
        or signing.get("rid") != authority.get("rid")
        or len(rows) != 1
    ):
        fail("signing receipt does not bind the exact notarized candidate")
    identity = args.identity
    team_id = args.team_id
    if (
        not identity.startswith("Developer ID Application:")
        or not identity.endswith(f"({team_id})")
        or re.fullmatch(r"[A-Z0-9]{10}", team_id) is None
    ):
        fail("Developer ID identity and team ID do not form an exact authority")
    certificate_sha = args.certificate_sha256.lower()
    spki_sha = args.certificate_spki_sha256.lower()
    if (
        SHA256_PATTERN.fullmatch(certificate_sha) is None
        or SHA256_PATTERN.fullmatch(spki_sha) is None
        or certificate_sha != args.expected_certificate_sha256.lower()
        or spki_sha != args.expected_certificate_spki_sha256.lower()
    ):
        fail("Developer ID certificate fingerprint or SPKI pin mismatch")
    submission_id = str(notary.get("id") or "").lower()
    if (
        notary.get("status") != "Accepted"
        or UUID_PATTERN.fullmatch(submission_id) is None
    ):
        fail("notarytool result is not an accepted submission")
    github = authority.get("github")
    if not isinstance(github, dict):
        fail("authority receipt lacks GitHub workflow provenance")
    receipt = {
        "artifact": {
            "fileName": args.candidate_artifact.name,
            "sha256": candidate_sha,
            "sizeBytes": candidate_size,
        },
        "certificate": {
            "developerIdApplicationIdentity": identity,
            "sha256": certificate_sha,
            "spkiSha256": spki_sha,
            "teamId": team_id,
        },
        "contractName": SIGNING_IDENTITY_CONTRACT,
        "contractVersion": 1,
        "generatedAtUtc": dt.datetime.now(dt.timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z"),
        "notarization": {
            "resultSha256": sha256_bytes(notary_raw),
            "status": "Accepted",
            "submissionId": submission_id,
        },
        "provenance": github,
        "releaseVersion": authority["releaseVersion"],
        "rid": authority["rid"],
        "signingReceiptSha256": sha256_bytes(signing_raw),
        "sourceAuthorityReceiptSha256": sha256_bytes(authority_raw),
        "status": "pass",
    }
    atomic_write(args.output, receipt)
    return 0


def command_collect(args: argparse.Namespace) -> int:
    authority, authority_raw = require_receipt_contract(
        args.authority_receipt,
        "authority receipt",
        "chummer6-ui.macos-flagship-authority-validation",
        1,
    )
    predecessor_verification, predecessor_verification_raw = (
        require_receipt_contract(
            args.predecessor_verification,
            "predecessor verification",
            PREDECESSOR_VERIFICATION_CONTRACT,
            1,
        )
    )
    if (
        SHA256_PATTERN.fullmatch(
            str(authority.get("predecessorHandoffSha256") or "")
        )
        is None
        or predecessor_verification.get("handoffSha256")
        != authority.get("predecessorHandoffSha256")
    ):
        fail("predecessor verification is not chained to release authority")
    stage, stage_raw = require_receipt_contract(
        args.stage_receipt,
        "stage-only receipt",
        "chummer.run.mac_release_stage_only",
    )
    for key in (
        "uploadAttempted",
        "publicationAttempted",
        "publicActivationAttempted",
        "countsAsPublicationEvidence",
    ):
        if stage.get(key) is not False:
            fail(f"stage-only receipt {key} must be false")
    if stage.get("mode") != "stage_only":
        fail("stage-only receipt mode mismatch")
    if (
        stage.get("outputPathDisclosure") != "directory_name_only"
        or SHA256_PATTERN.fullmatch(
            str(stage.get("sourceReceiptSha256") or "")
        )
        is None
    ):
        fail("stage-only receipt projection is not bound to its source receipt")

    stage_manifest, stage_manifest_raw = read_json_bytes(
        args.stage_manifest, "stage manifest"
    )
    require_regular_file(args.source_artifact, "unsigned source artifact")
    require_regular_file(args.candidate_artifact, "signed candidate artifact")
    require_regular_file(
        args.predecessor_artifact, "verified predecessor artifact"
    )
    source_sha = sha256_file(args.source_artifact)
    candidate_sha = sha256_file(args.candidate_artifact)
    candidate_size = args.candidate_artifact.stat().st_size
    release_version = str(authority.get("releaseVersion") or "")
    rid = str(authority.get("rid") or "")
    if (
        stage.get("releaseVersion") != release_version
        or stage.get("rid") != rid
        or stage.get("appHeads") != ["avalonia"]
    ):
        fail("stage-only receipt release identity mismatch")
    stage_row = find_stage_artifact(
        stage_manifest, rid=rid, file_name=args.source_artifact.name
    )
    if (
        normalize_digest(stage_row.get("sha256")) != source_sha
        or stage_row.get("sizeBytes") != args.source_artifact.stat().st_size
    ):
        fail("stage manifest does not bind the unsigned source artifact")

    predecessor_artifact = predecessor_verification.get("artifact")
    predecessor_sha = sha256_file(args.predecessor_artifact)
    predecessor_size = args.predecessor_artifact.stat().st_size
    predecessor_version = str(
        predecessor_verification.get("releaseVersion") or ""
    )
    selection_authority = str(
        authority.get("predecessorSelectionAuthority") or ""
    )
    if (
        not isinstance(predecessor_artifact, dict)
        or predecessor_verification.get("head") != "avalonia"
        or predecessor_verification.get("rid") != rid
        or version_stamp(
            predecessor_version, "verified predecessor releaseVersion"
        )
        >= version_stamp(release_version, "candidate releaseVersion")
        or predecessor_artifact.get("fileName")
        != args.predecessor_artifact.name
        or predecessor_artifact.get("sha256") != predecessor_sha
        or predecessor_artifact.get("sizeBytes") != predecessor_size
        or authority["predecessorHandoffSha256"]
        not in selection_authority.lower()
        or predecessor_version not in selection_authority
        or release_version not in selection_authority
    ):
        fail("predecessor verification does not bind the installed N-1 DMG")

    signing, signing_raw = read_json_bytes(
        args.signing_receipt, "signing receipt"
    )
    if (
        signing.get("contractName")
        != "chummer6-ui.desktop_artifact_signing"
        or signing.get("contractVersion") != 2
        or signing.get("platform") != "macos"
        or signing.get("app") != "avalonia"
        or signing.get("rid") != rid
        or signing.get("releaseVersion") != release_version
        or signing.get("signingStatus") != "pass"
        or signing.get("notarizationStatus") != "pass"
    ):
        fail("signing receipt does not prove Developer ID and notarization success")
    signing_rows = [
        row
        for row in (signing.get("artifacts") or [])
        if isinstance(row, dict)
        and row.get("fileName") == args.candidate_artifact.name
        and normalize_digest(row.get("sha256")) == candidate_sha
        and row.get("signingStatus") == "pass"
        and row.get("notarizationStatus") == "pass"
    ]
    if len(signing_rows) != 1:
        fail("signing receipt does not bind the exact candidate DMG")
    signing_identity, signing_identity_raw = require_receipt_contract(
        args.signing_identity_receipt,
        "signing identity receipt",
        SIGNING_IDENTITY_CONTRACT,
        1,
    )
    notary_result, notary_result_raw = read_json_bytes(
        args.notary_result, "notarytool result"
    )
    identity_artifact = signing_identity.get("artifact")
    certificate = signing_identity.get("certificate")
    notarization = signing_identity.get("notarization")
    if (
        not isinstance(identity_artifact, dict)
        or identity_artifact.get("fileName") != args.candidate_artifact.name
        or identity_artifact.get("sha256") != candidate_sha
        or identity_artifact.get("sizeBytes") != candidate_size
        or not isinstance(certificate, dict)
        or SHA256_PATTERN.fullmatch(str(certificate.get("sha256") or ""))
        is None
        or SHA256_PATTERN.fullmatch(
            str(certificate.get("spkiSha256") or "")
        )
        is None
        or re.fullmatch(
            r"[A-Z0-9]{10}", str(certificate.get("teamId") or "")
        )
        is None
        or not str(
            certificate.get("developerIdApplicationIdentity") or ""
        ).endswith(f"({certificate.get('teamId')})")
        or not isinstance(notarization, dict)
        or notarization.get("status") != "Accepted"
        or UUID_PATTERN.fullmatch(
            str(notarization.get("submissionId") or "")
        )
        is None
        or notary_result.get("status") != "Accepted"
        or str(notary_result.get("id") or "").lower()
        != notarization.get("submissionId")
        or notarization.get("resultSha256")
        != sha256_bytes(notary_result_raw)
        or SHA256_PATTERN.fullmatch(
            str(notarization.get("resultSha256") or "")
        )
        is None
        or signing_identity.get("provenance") != authority.get("github")
        or signing_identity.get("sourceAuthorityReceiptSha256")
        != sha256_bytes(authority_raw)
        or signing_identity.get("signingReceiptSha256")
        != sha256_bytes(signing_raw)
    ):
        fail("signing identity receipt is not bound to trusted candidate authority")

    clean_startup = require_startup_receipt(
        args.clean_startup_receipt,
        "clean-install startup receipt",
        release_version=release_version,
        rid=rid,
        artifact_sha=candidate_sha,
    )
    post_update_startup = require_startup_receipt(
        args.post_update_startup_receipt,
        "post-update startup receipt",
        release_version=release_version,
        rid=rid,
        artifact_sha=candidate_sha,
    )

    manual_state, manual_state_raw = read_json_bytes(
        args.manual_update_state, "manual update state"
    )
    pending_path = Path(str(manual_state.get("PendingInstallerPath") or ""))
    if (
        manual_state.get("LastFailureReason") != "macos_manual_install_required"
        or manual_state.get("InstalledVersion") != predecessor_version
        or manual_state.get("PendingUpdateVersion") != release_version
        or not pending_path.name
        or manual_state.get("PendingInstallerPathDisclosure")
        != "file_name_only"
        or SHA256_PATTERN.fullmatch(
            str(manual_state.get("ObservedStateSha256") or "")
        )
        is None
    ):
        fail("N-1 state does not prove exact candidate manual handoff")
    pending_delivery, pending_delivery_raw = require_receipt_contract(
        args.pending_delivery_receipt,
        "pending delivery receipt",
        "chummer6-ui.macos-pending-installer-delivery",
        1,
    )
    if (
        pending_delivery.get("releaseVersion") != release_version
        or pending_delivery.get("stateSha256")
        != sha256_bytes(manual_state_raw)
        or pending_delivery.get("pendingInstallerFileName") != pending_path.name
        or pending_delivery.get("pendingInstallerSha256") != candidate_sha
        or pending_delivery.get("pendingInstallerSizeBytes") != candidate_size
    ):
        fail("pending delivery receipt does not bind the exact N-1 handoff")

    completed_state, completed_state_raw = read_json_bytes(
        args.completed_update_state, "completed update state"
    )
    if (
        completed_state.get("InstalledVersion") != release_version
        or completed_state.get("PendingUpdateVersion") not in (None, "")
        or completed_state.get("PendingInstallerPath") not in (None, "")
        or completed_state.get("LastFailureReason") not in (None, "")
        or SHA256_PATTERN.fullmatch(
            str(completed_state.get("ObservedStateSha256") or "")
        )
        is None
    ):
        fail("post-install state does not prove candidate update completion")

    observations, observations_raw = read_json_bytes(
        args.observations, "macOS runtime observations"
    )
    if (
        observations.get("contractName")
        != "chummer6-ui.macos-flagship-runtime-observations"
        or observations.get("contractVersion") != 1
        or observations.get("releaseVersion") != release_version
        or observations.get("rid") != rid
    ):
        fail("macOS runtime observations identity mismatch")
    checks = observations.get("checks")
    if not isinstance(checks, dict) or set(checks) != OBSERVATION_CHECKS:
        fail("macOS runtime observations have an incomplete check denominator")
    failed_checks = sorted(key for key, value in checks.items() if value is not True)
    if failed_checks:
        fail(f"macOS runtime checks failed: {failed_checks}")
    signing_authority = observations.get("signingAuthority")
    if (
        not isinstance(signing_authority, dict)
        or not str(signing_authority.get("identity") or "").startswith(
            "Developer ID Application:"
        )
        or not re.fullmatch(
            r"[A-Z0-9]{10}", str(signing_authority.get("teamId") or "")
        )
    ):
        fail("runtime observations do not identify Developer ID authority")
    if (
        signing_authority.get("identity")
        != certificate["developerIdApplicationIdentity"]
        or signing_authority.get("teamId") != certificate["teamId"]
    ):
        fail("runtime signing authority differs from the pinned certificate")

    inventory_paths = {
        "authority_receipt": args.authority_receipt,
        "clean_install_startup_receipt": args.clean_startup_receipt,
        "completed_update_state": args.completed_update_state,
        "manual_update_state": args.manual_update_state,
        "notarytool_result": args.notary_result,
        "pending_delivery_receipt": args.pending_delivery_receipt,
        "post_update_startup_receipt": args.post_update_startup_receipt,
        "predecessor_dmg": args.predecessor_artifact,
        "predecessor_verification": args.predecessor_verification,
        "runtime_observations": args.observations,
        "signed_candidate_dmg": args.candidate_artifact,
        "signing_identity_receipt": args.signing_identity_receipt,
        "signing_receipt": args.signing_receipt,
        "stage_manifest": args.stage_manifest,
        "stage_only_receipt": args.stage_receipt,
        "unsigned_source_dmg": args.source_artifact,
    }
    inventory = {
        "contractName": "chummer6-ui.macos-flagship-evidence-inventory",
        "contractVersion": 1,
        "files": [
            inventory_row(role, path)
            for role, path in sorted(inventory_paths.items())
        ],
        "releaseVersion": release_version,
        "rid": rid,
        "status": "pass",
    }
    atomic_write(args.inventory_output, inventory)
    inventory_sha = sha256_file(args.inventory_output)
    references = {
        "authorityReceipt": portable_receipt_reference(
            args.authority_receipt
        ),
        "cleanStartupReceipt": portable_receipt_reference(
            args.clean_startup_receipt
        ),
        "completedUpdateState": portable_receipt_reference(
            args.completed_update_state
        ),
        "inventory": portable_receipt_reference(args.inventory_output),
        "manualUpdateState": portable_receipt_reference(
            args.manual_update_state
        ),
        "notaryResult": portable_receipt_reference(args.notary_result),
        "pendingDeliveryReceipt": portable_receipt_reference(
            args.pending_delivery_receipt
        ),
        "postUpdateStartupReceipt": portable_receipt_reference(
            args.post_update_startup_receipt
        ),
        "predecessorVerification": portable_receipt_reference(
            args.predecessor_verification
        ),
        "runtimeObservations": portable_receipt_reference(
            args.observations
        ),
        "signingIdentityReceipt": portable_receipt_reference(
            args.signing_identity_receipt
        ),
        "signingReceipt": portable_receipt_reference(args.signing_receipt),
        "stageManifest": portable_receipt_reference(args.stage_manifest),
        "stageOnlyReceipt": portable_receipt_reference(args.stage_receipt),
    }
    reference_paths = {
        "authorityReceipt": args.authority_receipt,
        "cleanStartupReceipt": args.clean_startup_receipt,
        "completedUpdateState": args.completed_update_state,
        "inventory": args.inventory_output,
        "manualUpdateState": args.manual_update_state,
        "notaryResult": args.notary_result,
        "pendingDeliveryReceipt": args.pending_delivery_receipt,
        "postUpdateStartupReceipt": args.post_update_startup_receipt,
        "predecessorVerification": args.predecessor_verification,
        "runtimeObservations": args.observations,
        "signingIdentityReceipt": args.signing_identity_receipt,
        "signingReceipt": args.signing_receipt,
        "stageManifest": args.stage_manifest,
        "stageOnlyReceipt": args.stage_receipt,
    }
    reference_files = {
        references[key]["path"]: path.read_bytes()
        for key, path in reference_paths.items()
    }

    receipt = {
        "candidate": {
            "artifactId": f"avalonia-{rid}-installer",
            "fileName": args.candidate_artifact.name,
            "sha256": candidate_sha,
            "sizeBytes": candidate_size,
        },
        "cleanInstall": {
            "coreStartupReceiptSha256": sha256_file(
                args.clean_startup_receipt
            ),
            "gatekeeperAssessment": "pass",
            "installRootClass": "isolated_applications_equivalent",
            "quarantineAssessment": "pass",
            "uninstall": "pass",
        },
        "contractName": EVIDENCE_CONTRACT,
        "contractVersion": EVIDENCE_CONTRACT_VERSION,
        "generatedAtUtc": dt.datetime.now(dt.timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z"),
        "github": authority["github"],
        "globalCandidateIdentity": {
            "candidateId": authority["candidateId"],
            "generationId": authority["generationId"],
            "previousReleaseVersion": predecessor_version,
            "releaseVersion": release_version,
            "sourceCommit": authority["github"]["sha"],
        },
        "inputBindings": {
            "authorityReceiptSha256": sha256_bytes(authority_raw),
            "cleanStartupReceiptSha256": sha256_file(
                args.clean_startup_receipt
            ),
            "completedUpdateStateSha256": sha256_bytes(completed_state_raw),
            "manualUpdateStateSha256": sha256_bytes(manual_state_raw),
            "notaryResultSha256": sha256_bytes(notary_result_raw),
            "pendingDeliveryReceiptSha256": sha256_bytes(
                pending_delivery_raw
            ),
            "postUpdateStartupReceiptSha256": sha256_file(
                args.post_update_startup_receipt
            ),
            "predecessorVerificationSha256": sha256_bytes(
                predecessor_verification_raw
            ),
            "runtimeObservationsSha256": sha256_bytes(observations_raw),
            "signingReceiptSha256": sha256_bytes(signing_raw),
            "signingIdentityReceiptSha256": sha256_bytes(
                signing_identity_raw
            ),
            "stageManifestSha256": sha256_bytes(stage_manifest_raw),
            "stageOnlyReceiptSha256": sha256_bytes(stage_raw),
        },
        "inventorySha256": inventory_sha,
        "nonPublishing": {
            "countsAsPublicationEvidence": False,
            "evidenceArtifactUploadAllowed": True,
            "publicActivationAttempted": False,
            "publicationAttempted": False,
            "releaseUploadCredentialAccepted": False,
            "releaseUploadAttempted": False,
        },
        "references": references,
        "releaseVersion": release_version,
        "rid": rid,
        "signing": {
            "candidateDmgGatekeeperStatus": "pass",
            "certificateSha256": certificate["sha256"],
            "certificateSpkiSha256": certificate["spkiSha256"],
            "developerIdApplicationIdentity": certificate[
                "developerIdApplicationIdentity"
            ],
            "gatekeeperAssessmentsEnabled": True,
            "installedAppGatekeeperStatus": "pass",
            "notarizationStatus": notarization["status"],
            "notarySubmissionId": notarization["submissionId"],
            "postUpdateAppGatekeeperStatus": "pass",
            "staplerValidationStatus": "pass",
            "signingStatus": "pass",
            "teamId": certificate["teamId"],
        },
        "sourceUnsignedCandidate": {
            "fileName": args.source_artifact.name,
            "sha256": source_sha,
            "sizeBytes": args.source_artifact.stat().st_size,
        },
        "status": "pass",
        "updateDelivery": {
            "automaticApplySupported": False,
            "candidatePendingInstallerSha256": candidate_sha,
            "completionStateSha256": sha256_bytes(completed_state_raw),
            "deliveryMode": "macos_manual_installer_handoff",
            "platformPolicyReason": "macOS DMG updates are downloaded and integrity-checked in-app, then require a Gatekeeper-visible manual install.",
            "postUpdateStartupReceiptSha256": sha256_file(
                args.post_update_startup_receipt
            ),
            "predecessorArtifactSha256": predecessor_sha,
            "predecessorVersion": predecessor_version,
            "targetVersion": release_version,
        },
    }
    validate_aggregate_receipt(
        receipt,
        reference_files,
        expected_candidate=receipt["candidate"],
        expected_global_identity=receipt["globalCandidateIdentity"],
        expected_github=authority["github"],
        expected_certificate_sha256=certificate["sha256"],
        expected_certificate_spki_sha256=certificate["spkiSha256"],
        expected_developer_id_application_identity=certificate[
            "developerIdApplicationIdentity"
        ],
        expected_team_id=certificate["teamId"],
    )
    atomic_write(args.output, receipt)
    github = authority["github"]
    if (
        re.fullmatch(r"[1-9][0-9]*", args.run_id) is None
        or re.fullmatch(r"[1-9][0-9]*", args.run_attempt) is None
        or args.run_id != github.get("runId")
        or args.run_attempt != github.get("runAttempt")
        or not args.runner_os.lower().startswith("macos")
        or args.runner_arch.lower() != "arm64"
    ):
        fail("native E2E runner identity is invalid")
    adapter = {
        "artifact": {
            "artifactId": f"avalonia-{rid}-installer",
            "fileName": args.candidate_artifact.name,
            "sha256": candidate_sha,
            "sizeBytes": candidate_size,
        },
        "candidate": {
            "candidateId": authority["candidateId"],
            "generationId": authority["generationId"],
            "previousReleaseVersion": predecessor_version,
            "releaseVersion": release_version,
            "sourceCommit": github["sha"],
        },
        "checks": {
            "cleanInstall": {
                "evidence": portable_receipt_reference(args.output),
                "mode": "clean",
                "status": "pass",
            },
            "coreWorkflow": {
                "evidence": portable_receipt_reference(args.output),
                "scenario": (
                    "installed_candidate_startup_smoke_pre_ui_event_loop"
                ),
                "status": "pass",
            },
            "nMinusOneUpdate": {
                "evidence": portable_receipt_reference(args.output),
                "fromReleaseVersion": predecessor_version,
                "status": "pass",
                "toReleaseVersion": release_version,
            },
        },
        "contractName": "chummer6-ui.flagship-native-e2e.macos.v1",
        "contractVersion": 1,
        "generatedAt": dt.datetime.now(dt.timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z"),
        "platform": "macos",
        "rid": rid,
        "runner": {
            "actor": github["actor"],
            "arch": "arm64",
            "os": args.runner_os.lower(),
            "ref": github["ref"],
            "repository": github["repository"],
            "rerunPolicy": github["rerunPolicy"],
            "runAttempt": github["runAttempt"],
            "runId": github["runId"],
            "triggeringActor": github["triggeringActor"],
            "workflow": github["workflow"],
        },
        "status": "pass",
    }
    atomic_write(args.native_adapter_output, adapter)
    return 0


def _require_exact_positive_integer(
    payload: dict[str, Any],
    key: str,
    label: str,
    *,
    maximum: int,
) -> int:
    value = payload.get(key)
    if (
        isinstance(value, bool)
        or not isinstance(value, int)
        or value < 1
        or value > maximum
    ):
        fail(f"{label} {key} must be a bounded positive integer")
    return value


def _strict_base64(
    payload: dict[str, Any],
    key: str,
    label: str,
    *,
    expected_size: int,
) -> bytes:
    value = require_string(payload, key, label, maximum=16 * 1024)
    try:
        decoded = base64.b64decode(value, validate=True)
    except (ValueError, binascii.Error) as error:
        fail(f"{label} {key} must be canonical base64: {error}")
    if (
        len(decoded) != expected_size
        or base64.b64encode(decoded).decode("ascii") != value
    ):
        fail(f"{label} {key} must be canonical base64 of the exact size")
    return decoded


def _stable_file_digest(
    path: Path,
    label: str,
    *,
    maximum: int,
) -> tuple[str, int]:
    require_regular_file(path, label)
    before = path.stat(follow_symlinks=False)
    if before.st_size < 1 or before.st_size > maximum:
        fail(f"{label} size is outside the fixed bound")
    digest = sha256_file(path)
    after = path.stat(follow_symlinks=False)
    identity = lambda value: (
        value.st_dev,
        value.st_ino,
        value.st_size,
        value.st_mtime_ns,
        value.st_ctime_ns,
    )
    if identity(before) != identity(after):
        fail(f"{label} changed while it was hashed")
    return digest, after.st_size


def validate_escrow_receipt(
    receipt_path: Path,
    ciphertext_path: Path,
    *,
    evidence: dict[str, Any],
    repository: str,
    ref: str,
    sha: str,
    actor: str,
    triggering_actor: str,
    run_id: str,
    run_attempt: str,
) -> dict[str, Any]:
    expected_github = {
        "actor": actor,
        "ref": ref,
        "repository": repository,
        "rerunPolicy": RERUN_POLICY,
        "runAttempt": run_attempt,
        "runId": run_id,
        "sha": sha,
        "triggeringActor": triggering_actor,
        "workflow": WORKFLOW_PATH,
    }
    if (
        evidence.get("github") != expected_github
        or repository != UI_REPOSITORY
        or ref != UI_RELEASE_REF
        or COMMIT_PATTERN.fullmatch(sha) is None
        or LOGIN_PATTERN.fullmatch(actor) is None
        or LOGIN_PATTERN.fullmatch(triggering_actor) is None
        or triggering_actor != actor
        or re.fullmatch(r"[1-9][0-9]*", run_id) is None
        or re.fullmatch(r"[1-9][0-9]*", run_attempt) is None
    ):
        fail("macOS candidate escrow runtime does not match aggregate authority")
    if (
        receipt_path.name != ESCROW_RECEIPT_FILE
        or ciphertext_path.name != ESCROW_CIPHERTEXT_FILE
        or receipt_path.parent.resolve() != ciphertext_path.parent.resolve()
    ):
        fail("macOS escrow files do not use the fixed private custody layout")
    receipt, receipt_raw = read_canonical_json(
        receipt_path, "macOS candidate escrow receipt"
    )
    require_exact_keys(
        receipt,
        {
            "aad",
            "aadSha256",
            "candidate",
            "ciphertext",
            "contractName",
            "contractVersion",
            "encryption",
            "recipient",
            "status",
        },
        "macOS candidate escrow receipt",
    )
    if (
        receipt.get("contractName") != ESCROW_CONTRACT
        or receipt.get("contractVersion") != 1
        or receipt.get("status") != "sealed"
    ):
        fail("macOS candidate escrow receipt identity or status is invalid")

    candidate = receipt.get("candidate")
    require_exact_keys(
        candidate,
        {"artifactId", "fileName", "sha256", "sizeBytes"},
        "macOS candidate escrow identity",
    )
    expected_candidate = evidence.get("candidate")
    if candidate != expected_candidate:
        fail("macOS candidate escrow does not bind the exact native evidence DMG")
    candidate_size = _require_exact_positive_integer(
        candidate,
        "sizeBytes",
        "macOS candidate escrow identity",
        maximum=MAX_ARTIFACT_BYTES,
    )
    require_sha256(candidate, "sha256", "macOS candidate escrow identity")
    if (
        candidate.get("artifactId") != "avalonia-osx-arm64-installer"
        or candidate.get("fileName")
        != "chummer-avalonia-osx-arm64-installer.dmg"
    ):
        fail("macOS candidate escrow artifact identity is invalid")

    recipient = receipt.get("recipient")
    require_exact_keys(
        recipient,
        {"keyType", "modulusBits", "publicExponent", "spkiSha256"},
        "macOS candidate escrow recipient",
    )
    modulus_bits = _require_exact_positive_integer(
        recipient,
        "modulusBits",
        "macOS candidate escrow recipient",
        maximum=8192,
    )
    if (
        recipient.get("keyType") != "rsa"
        or recipient.get("publicExponent") != 65537
        or modulus_bits < 3072
        or modulus_bits % 256 != 0
    ):
        fail("macOS candidate escrow recipient RSA authority is invalid")
    recipient_sha = require_sha256(
        recipient, "spkiSha256", "macOS candidate escrow recipient"
    )

    ciphertext = receipt.get("ciphertext")
    require_exact_keys(
        ciphertext,
        {"fileName", "sha256", "sizeBytes"},
        "macOS candidate escrow ciphertext",
    )
    if ciphertext.get("fileName") != ESCROW_CIPHERTEXT_FILE:
        fail("macOS candidate escrow ciphertext file name is invalid")
    ciphertext_sha = require_sha256(
        ciphertext, "sha256", "macOS candidate escrow ciphertext"
    )
    ciphertext_size = _require_exact_positive_integer(
        ciphertext,
        "sizeBytes",
        "macOS candidate escrow ciphertext",
        maximum=MAX_ARTIFACT_BYTES,
    )
    if ciphertext_size != candidate_size:
        fail("macOS AES-GCM escrow ciphertext size is not exact")
    observed_ciphertext_sha, observed_ciphertext_size = _stable_file_digest(
        ciphertext_path,
        "macOS candidate escrow ciphertext",
        maximum=MAX_ARTIFACT_BYTES,
    )
    if (
        observed_ciphertext_sha != ciphertext_sha
        or observed_ciphertext_size != ciphertext_size
    ):
        fail("macOS candidate escrow ciphertext bytes do not match the receipt")

    encryption = receipt.get("encryption")
    require_exact_keys(
        encryption,
        {
            "authenticationTagBase64",
            "cipher",
            "keyWrap",
            "nonceBase64",
            "oaepLabelSha256",
            "wrappedKeyBase64",
        },
        "macOS candidate escrow encryption",
    )
    if (
        encryption.get("cipher") != "aes-256-gcm"
        or encryption.get("keyWrap") != "rsa-oaep-sha256"
    ):
        fail("macOS candidate escrow algorithms are not approved")
    _strict_base64(
        encryption,
        "authenticationTagBase64",
        "macOS candidate escrow encryption",
        expected_size=16,
    )
    _strict_base64(
        encryption,
        "nonceBase64",
        "macOS candidate escrow encryption",
        expected_size=12,
    )
    _strict_base64(
        encryption,
        "wrappedKeyBase64",
        "macOS candidate escrow encryption",
        expected_size=modulus_bits // 8,
    )

    aad = receipt.get("aad")
    require_exact_keys(
        aad,
        {
            "candidate",
            "candidateId",
            "generationId",
            "producer",
            "recipientSpkiSha256",
            "releaseVersion",
            "rid",
        },
        "macOS candidate escrow AAD",
    )
    identity = evidence.get("globalCandidateIdentity")
    if not isinstance(identity, dict):
        fail("macOS aggregate evidence global candidate identity is invalid")
    if (
        aad.get("candidate") != candidate
        or aad.get("candidateId") != identity.get("candidateId")
        or aad.get("generationId") != identity.get("generationId")
        or aad.get("releaseVersion") != identity.get("releaseVersion")
        or aad.get("rid") != evidence.get("rid")
        or aad.get("recipientSpkiSha256") != recipient_sha
    ):
        fail("macOS candidate escrow AAD does not bind the aggregate evidence")
    producer = aad.get("producer")
    require_exact_keys(
        producer,
        {
            "actor",
            "environment",
            "ref",
            "repository",
            "rerunPolicy",
            "runAttempt",
            "runId",
            "sha",
            "triggeringActor",
            "workflow",
        },
        "macOS candidate escrow producer",
    )
    expected_producer = {
        "actor": actor,
        "environment": "macos-flagship-evidence",
        "ref": ref,
        "repository": repository,
        "rerunPolicy": RERUN_POLICY,
        "runAttempt": run_attempt,
        "runId": run_id,
        "sha": sha,
        "triggeringActor": triggering_actor,
        "workflow": WORKFLOW_PATH,
    }
    if producer != expected_producer:
        fail("macOS candidate escrow producer does not match GitHub runtime")

    aad_raw = canonical_json(aad).encode("utf-8")
    aad_sha = require_sha256(
        receipt, "aadSha256", "macOS candidate escrow receipt"
    )
    if aad_sha != sha256_bytes(aad_raw):
        fail("macOS candidate escrow AAD digest is invalid")
    oaep_label = (ESCROW_CONTRACT + "\0" + aad_sha).encode("utf-8")
    if require_sha256(
        encryption,
        "oaepLabelSha256",
        "macOS candidate escrow encryption",
    ) != sha256_bytes(oaep_label):
        fail("macOS candidate escrow OAEP label digest is invalid")

    return {
        "cipher": "aes-256-gcm",
        "ciphertextFileName": ESCROW_CIPHERTEXT_FILE,
        "ciphertextSha256": ciphertext_sha,
        "ciphertextSizeBytes": ciphertext_size,
        "keyWrap": "rsa-oaep-sha256",
        "receiptFileName": ESCROW_RECEIPT_FILE,
        "receiptSha256": sha256_bytes(receipt_raw),
        "receiptSizeBytes": len(receipt_raw),
        "recipientSpkiSha256": recipient_sha,
    }


def command_validate_escrow(args: argparse.Namespace) -> int:
    evidence, _ = require_receipt_contract(
        args.evidence,
        "macOS flagship evidence",
        EVIDENCE_CONTRACT,
        EVIDENCE_CONTRACT_VERSION,
    )
    projection = validate_escrow_receipt(
        args.escrow_receipt,
        args.escrow_ciphertext,
        evidence=evidence,
        repository=args.repository,
        ref=args.ref,
        sha=args.sha,
        actor=args.actor,
        triggering_actor=args.triggering_actor,
        run_id=args.run_id,
        run_attempt=args.run_attempt,
    )
    print(canonical_json(projection))
    return 0


def command_emit_handoff(args: argparse.Namespace) -> int:
    evidence, evidence_raw = require_receipt_contract(
        args.evidence,
        "macOS flagship evidence",
        EVIDENCE_CONTRACT,
        EVIDENCE_CONTRACT_VERSION,
    )
    inventory, inventory_raw = require_receipt_contract(
        args.inventory,
        "macOS flagship inventory",
        "chummer6-ui.macos-flagship-evidence-inventory",
        1,
    )
    native_adapter, native_adapter_raw = require_receipt_contract(
        args.native_adapter,
        "macOS flagship native E2E adapter",
        "chummer6-ui.flagship-native-e2e.macos.v1",
        1,
    )
    if (
        evidence.get("inventorySha256") != sha256_bytes(inventory_raw)
        or inventory.get("releaseVersion") != evidence.get("releaseVersion")
        or inventory.get("rid") != evidence.get("rid")
        or native_adapter.get("artifact") != evidence.get("candidate")
        or native_adapter.get("candidate")
        != evidence.get("globalCandidateIdentity")
        or native_adapter.get("platform") != "macos"
        or native_adapter.get("rid") != evidence.get("rid")
    ):
        fail("evidence, inventory, and native adapter binding mismatch")
    if not re.fullmatch(r"[1-9][0-9]*", args.artifact_id):
        fail("GitHub artifact ID is invalid")
    for label, value in (
        ("run ID", args.run_id),
        ("run attempt", args.run_attempt),
    ):
        if re.fullmatch(r"[1-9][0-9]*", value) is None:
            fail(f"GitHub {label} is invalid")
    artifact_digest = args.artifact_digest.removeprefix("sha256:")
    if SHA256_PATTERN.fullmatch(artifact_digest) is None:
        fail("GitHub artifact digest is invalid")
    expected_artifact_name = (
        f"macos-flagship-encrypted-escrow-{args.run_id}-{args.run_attempt}"
    )
    if args.artifact_name != expected_artifact_name:
        fail("GitHub artifact name is not bound to run and attempt")
    if (
        LOGIN_PATTERN.fullmatch(args.actor) is None
        or LOGIN_PATTERN.fullmatch(args.triggering_actor) is None
        or args.triggering_actor != args.actor
    ):
        fail("GitHub actor, triggering actor, or rerun policy is invalid")
    if not COMMIT_PATTERN.fullmatch(args.sha):
        fail("GitHub source SHA is invalid")
    github = evidence.get("github")
    native_runner = native_adapter.get("runner")
    if (
        not isinstance(github, dict)
        or github.get("repository") != args.repository
        or github.get("ref") != args.ref
        or github.get("sha") != args.sha
        or github.get("actor") != args.actor
        or github.get("triggeringActor") != args.triggering_actor
        or github.get("rerunPolicy") != RERUN_POLICY
        or github.get("runId") != args.run_id
        or github.get("runAttempt") != args.run_attempt
        or github.get("workflow") != WORKFLOW_PATH
        or not isinstance(native_runner, dict)
        or native_runner.get("repository") != args.repository
        or native_runner.get("ref") != args.ref
        or native_runner.get("actor") != args.actor
        or native_runner.get("triggeringActor") != args.triggering_actor
        or native_runner.get("rerunPolicy") != RERUN_POLICY
        or native_runner.get("workflow") != WORKFLOW_PATH
        or str(native_runner.get("runId")) != args.run_id
        or str(native_runner.get("runAttempt")) != args.run_attempt
        or native_runner.get("arch") != "arm64"
        or not str(native_runner.get("os") or "").startswith("macos")
    ):
        fail("GitHub artifact handoff identity does not match build evidence")
    escrow = validate_escrow_receipt(
        args.escrow_receipt,
        args.escrow_ciphertext,
        evidence=evidence,
        repository=args.repository,
        ref=args.ref,
        sha=args.sha,
        actor=args.actor,
        triggering_actor=args.triggering_actor,
        run_id=args.run_id,
        run_attempt=args.run_attempt,
    )
    artifact_url = urlparse(args.artifact_url)
    if (
        artifact_url.scheme != "https"
        or artifact_url.hostname != "github.com"
        or artifact_url.port not in (None, 443)
        or artifact_url.username is not None
        or artifact_url.password is not None
        or artifact_url.query
        or artifact_url.fragment
        or artifact_url.path
        != (
            f"/{args.repository}/actions/runs/{args.run_id}"
            f"/artifacts/{args.artifact_id}"
        )
    ):
        fail("GitHub artifact URL is not a safe immutable Actions URL")
    handoff = {
        "actor": args.actor,
        "artifactDigest": artifact_digest,
        "artifactId": args.artifact_id,
        "artifactName": args.artifact_name,
        "artifactUrl": args.artifact_url,
        "artifactContents": "receipts_and_encrypted_candidate_escrow",
        "candidateBytesRetained": True,
        "candidateEscrow": escrow,
        "candidatePlaintextDistributed": False,
        "candidateArtifactSha256": evidence["candidate"]["sha256"],
        "contractName": HANDOFF_CONTRACT,
        "contractVersion": 2,
        "environment": "macos-flagship-evidence",
        "evidenceSha256": sha256_bytes(evidence_raw),
        "inventorySha256": sha256_bytes(inventory_raw),
        "nativeE2EReceiptSha256": sha256_bytes(native_adapter_raw),
        "ref": args.ref,
        "releaseVersion": evidence["releaseVersion"],
        "repository": args.repository,
        "rerunPolicy": RERUN_POLICY,
        "rid": evidence["rid"],
        "runAttempt": args.run_attempt,
        "runId": args.run_id,
        "sha": args.sha,
        "triggeringActor": args.triggering_actor,
        "workflow": WORKFLOW_PATH,
        "provenanceAuthenticated": False,
        "requiredNextAuthority": (
            "A protected downstream workflow must authenticate this run and "
            "artifact plus the macos-flagship-evidence environment approval "
            "through the GitHub API, verify artifactDigest, decrypt with the "
            "pinned recipient private key, and revalidate the candidate "
            "plaintext SHA-256 and size before assembly."
        ),
    }
    atomic_write(args.output, handoff, compact=True)
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)

    validate = subparsers.add_parser("validate-authority")
    validate.add_argument("--authority", type=Path, required=True)
    validate.add_argument("--scope-decision", type=Path, required=True)
    validate.add_argument("--predecessor", type=Path, required=True)
    validate.add_argument("--expected-repository", required=True)
    validate.add_argument("--expected-ref", required=True)
    validate.add_argument("--expected-sha", required=True)
    validate.add_argument("--expected-actor", required=True)
    validate.add_argument("--expected-triggering-actor", required=True)
    validate.add_argument("--expected-run-id", required=True)
    validate.add_argument("--expected-run-attempt", required=True)
    validate.add_argument("--now")
    validate.add_argument("--github-env", type=Path)
    validate.add_argument("--output", type=Path, required=True)
    validate.set_defaults(handler=command_validate_authority)

    predecessor = subparsers.add_parser("verify-predecessor")
    predecessor.add_argument("--predecessor", type=Path, required=True)
    predecessor.add_argument("--manifest", type=Path, required=True)
    predecessor.add_argument("--artifact", type=Path, required=True)
    predecessor.add_argument("--output", type=Path, required=True)
    predecessor.set_defaults(handler=command_verify_predecessor)

    signing_identity = subparsers.add_parser("emit-signing-identity")
    signing_identity.add_argument(
        "--authority-receipt", type=Path, required=True
    )
    signing_identity.add_argument(
        "--candidate-artifact", type=Path, required=True
    )
    signing_identity.add_argument(
        "--signing-receipt", type=Path, required=True
    )
    signing_identity.add_argument(
        "--notary-result", type=Path, required=True
    )
    signing_identity.add_argument("--identity", required=True)
    signing_identity.add_argument("--team-id", required=True)
    signing_identity.add_argument("--certificate-sha256", required=True)
    signing_identity.add_argument(
        "--certificate-spki-sha256", required=True
    )
    signing_identity.add_argument(
        "--expected-certificate-sha256", required=True
    )
    signing_identity.add_argument(
        "--expected-certificate-spki-sha256", required=True
    )
    signing_identity.add_argument("--output", type=Path, required=True)
    signing_identity.set_defaults(handler=command_emit_signing_identity)

    collect = subparsers.add_parser("collect")
    collect.add_argument("--authority-receipt", type=Path, required=True)
    collect.add_argument(
        "--predecessor-verification", type=Path, required=True
    )
    collect.add_argument("--predecessor-artifact", type=Path, required=True)
    collect.add_argument("--stage-receipt", type=Path, required=True)
    collect.add_argument("--stage-manifest", type=Path, required=True)
    collect.add_argument("--source-artifact", type=Path, required=True)
    collect.add_argument("--candidate-artifact", type=Path, required=True)
    collect.add_argument("--signing-receipt", type=Path, required=True)
    collect.add_argument(
        "--signing-identity-receipt", type=Path, required=True
    )
    collect.add_argument("--notary-result", type=Path, required=True)
    collect.add_argument(
        "--clean-startup-receipt", type=Path, required=True
    )
    collect.add_argument(
        "--post-update-startup-receipt", type=Path, required=True
    )
    collect.add_argument("--manual-update-state", type=Path, required=True)
    collect.add_argument(
        "--pending-delivery-receipt", type=Path, required=True
    )
    collect.add_argument(
        "--completed-update-state", type=Path, required=True
    )
    collect.add_argument("--observations", type=Path, required=True)
    collect.add_argument("--inventory-output", type=Path, required=True)
    collect.add_argument("--output", type=Path, required=True)
    collect.add_argument(
        "--native-adapter-output", type=Path, required=True
    )
    collect.add_argument("--run-id", required=True)
    collect.add_argument("--run-attempt", required=True)
    collect.add_argument("--runner-os", required=True)
    collect.add_argument("--runner-arch", required=True)
    collect.set_defaults(handler=command_collect)

    validate_escrow = subparsers.add_parser("validate-escrow")
    validate_escrow.add_argument("--evidence", type=Path, required=True)
    validate_escrow.add_argument(
        "--escrow-receipt", type=Path, required=True
    )
    validate_escrow.add_argument(
        "--escrow-ciphertext", type=Path, required=True
    )
    validate_escrow.add_argument("--repository", required=True)
    validate_escrow.add_argument("--ref", required=True)
    validate_escrow.add_argument("--sha", required=True)
    validate_escrow.add_argument("--actor", required=True)
    validate_escrow.add_argument("--triggering-actor", required=True)
    validate_escrow.add_argument("--run-id", required=True)
    validate_escrow.add_argument("--run-attempt", required=True)
    validate_escrow.set_defaults(handler=command_validate_escrow)

    handoff = subparsers.add_parser("emit-handoff")
    handoff.add_argument("--evidence", type=Path, required=True)
    handoff.add_argument("--inventory", type=Path, required=True)
    handoff.add_argument("--native-adapter", type=Path, required=True)
    handoff.add_argument("--escrow-receipt", type=Path, required=True)
    handoff.add_argument("--escrow-ciphertext", type=Path, required=True)
    handoff.add_argument("--artifact-id", required=True)
    handoff.add_argument("--artifact-digest", required=True)
    handoff.add_argument("--artifact-name", required=True)
    handoff.add_argument("--artifact-url", required=True)
    handoff.add_argument("--repository", required=True)
    handoff.add_argument("--ref", required=True)
    handoff.add_argument("--sha", required=True)
    handoff.add_argument("--actor", required=True)
    handoff.add_argument("--triggering-actor", required=True)
    handoff.add_argument("--run-id", required=True)
    handoff.add_argument("--run-attempt", required=True)
    handoff.add_argument("--output", type=Path, required=True)
    handoff.set_defaults(handler=command_emit_handoff)
    return parser


def main() -> int:
    try:
        args = build_parser().parse_args()
        return int(args.handler(args))
    except ContractError as error:
        print(f"macOS flagship evidence rejected: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
