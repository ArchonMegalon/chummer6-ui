#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Sequence
from urllib.parse import urlsplit

from blazor_public_edge_workbench_contract import (
    CONTRACT_NAME,
    MAX_RESPONSE_BYTES,
    REQUIRED_ROUTE_MODEL_NOTE,
    ROUTE_MARKER_REQUIREMENTS,
    ROUTE_SPECS,
    WORKFLOW_REQUIREMENTS,
    derived_claims,
    normalize_base_url,
    validate_probe_record,
)


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_RECEIPT_PATH = (
    REPO_ROOT
    / ".codex-studio"
    / "published"
    / "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"
)
CANONICAL_BASE_URL = "https://chummer.run"
DEFAULT_MAX_AGE_SECONDS = 900
LOOPBACK_HOSTS = {"127.0.0.1", "::1", "localhost"}


def load_json(path: Path) -> dict:
    loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(loaded, dict):
        raise ValueError("receipt root must be an object")
    return loaded


def parse_timestamp(value: object) -> datetime | None:
    text = str(value or "").strip()
    if not text:
        return None
    try:
        parsed = datetime.fromisoformat(text.replace("Z", "+00:00"))
    except ValueError:
        return None
    if parsed.tzinfo is None:
        return None
    return parsed.astimezone(timezone.utc)


def validate_base_url(base_url: str, allow_test_origin: bool) -> str | None:
    try:
        normalized = normalize_base_url(base_url)
    except ValueError as exc:
        return str(exc)
    if normalized == CANONICAL_BASE_URL:
        return None
    parsed = urlsplit(normalized)
    if allow_test_origin and parsed.scheme == "http" and parsed.hostname in LOOPBACK_HOSTS:
        return None
    return f"base_url must be exactly {CANONICAL_BASE_URL!r} outside explicit loopback tests"


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Verify a hosted Blazor route-entry receipt."
    )
    parser.add_argument(
        "--receipt-path",
        type=Path,
        default=Path(
            os.environ.get(
                "CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH",
                str(DEFAULT_RECEIPT_PATH),
            )
        ),
    )
    parser.add_argument("--allow-test-origin", action="store_true")
    parser.add_argument(
        "--max-age-seconds",
        type=int,
        default=os.environ.get(
            "CHUMMER_BLAZOR_PUBLIC_EDGE_MAX_AGE_SECONDS",
            str(DEFAULT_MAX_AGE_SECONDS),
        ),
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = ()) -> int:
    args = parse_args(argv)
    receipt_path = args.receipt_path
    reasons: list[str] = []
    if not receipt_path.is_file():
        print(f"missing receipt: {receipt_path}")
        return 1
    try:
        payload = load_json(receipt_path)
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"invalid receipt {receipt_path}: {exc}")
        return 1

    if payload.get("contract_name") != CONTRACT_NAME:
        reasons.append(
            f"contract mismatch: expected {CONTRACT_NAME!r}, got {payload.get('contract_name')!r}"
        )
    if str(payload.get("status") or "").strip().lower() != "passed":
        reasons.append("status must be exactly 'passed'")
    if payload.get("runtime_required") is not True:
        reasons.append("runtime_required must be true")
    if payload.get("route_probe_executed") is not True:
        reasons.append("route_probe_executed must be true")
    if payload.get("proof_shape") != "expanded":
        reasons.append("release receipt proof_shape must be exactly 'expanded'")

    base_url = str(payload.get("base_url") or "").strip()
    base_error = validate_base_url(base_url, args.allow_test_origin)
    if base_error:
        reasons.append(base_error)

    generated_at = parse_timestamp(payload.get("generated_at"))
    now = datetime.now(timezone.utc)
    if generated_at is None:
        reasons.append("generated_at must be an RFC 3339 timestamp with timezone")
    else:
        age_seconds = (now - generated_at).total_seconds()
        if age_seconds < -60:
            reasons.append("generated_at is more than 60 seconds in the future")
        if args.max_age_seconds <= 0 or age_seconds > args.max_age_seconds:
            reasons.append(
                f"receipt is stale: age {int(age_seconds)}s exceeds {args.max_age_seconds}s"
            )

    expected_routes = [spec.route for spec in ROUTE_SPECS]
    if payload.get("required_routes") != expected_routes:
        reasons.append("required_routes must match the canonical route order exactly")
    if payload.get("proof_routes") != expected_routes:
        reasons.append("passing proof_routes must match the canonical route order exactly")

    probes = payload.get("route_probes")
    if not isinstance(probes, list):
        reasons.append("route_probes must be a list")
        probes = []
    observed_probe_routes = [
        str(probe.get("route") or "") if isinstance(probe, dict) else "" for probe in probes
    ]
    if observed_probe_routes != expected_routes:
        reasons.append("route_probes must match the canonical route order with no extras")
    if payload.get("route_probe_count") != len(expected_routes):
        reasons.append("route_probe_count must equal the canonical route count")
    if payload.get("passed_route_probe_count") != len(expected_routes):
        reasons.append("passed_route_probe_count must equal the canonical route count")
    if payload.get("route_probe_failures") != []:
        reasons.append("passing receipt must have no route_probe_failures")

    for index, spec in enumerate(ROUTE_SPECS):
        if index >= len(probes):
            break
        probe_reasons = validate_probe_record(probes[index], spec, base_url)
        reasons.extend(f"{spec.route}: {reason}" for reason in probe_reasons)
        if isinstance(probes[index], dict):
            body_bytes = probes[index].get("response_body_bytes")
            if isinstance(body_bytes, int) and body_bytes > MAX_RESPONSE_BYTES:
                reasons.append(
                    f"{spec.route}: response body exceeds {MAX_RESPONSE_BYTES} bytes"
                )

    successful_routes = {
        str(probe.get("route"))
        for probe in probes
        if isinstance(probe, dict) and probe.get("ok") is True
    }
    expected_markers = derived_claims(successful_routes, ROUTE_MARKER_REQUIREMENTS)
    expected_workflows = derived_claims(successful_routes, WORKFLOW_REQUIREMENTS)
    if payload.get("route_proof_markers") != expected_markers:
        reasons.append("route_proof_markers do not match re-derived successful routes")
    if payload.get("workflow_proofs") != expected_workflows:
        reasons.append("workflow_proofs do not match re-derived successful routes")
    if set(expected_routes) != successful_routes:
        reasons.append("every canonical route must pass before release verification")

    notes = payload.get("notes")
    if not isinstance(notes, list) or REQUIRED_ROUTE_MODEL_NOTE not in notes:
        reasons.append("receipt is missing the canonical route-model note")

    if reasons:
        print("\n".join(dict.fromkeys(reasons)))
        return 1
    print(f"blazor_public_edge_workbench_proof:ok {receipt_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(None))
