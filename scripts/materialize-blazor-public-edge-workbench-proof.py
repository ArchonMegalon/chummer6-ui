#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import os
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence
from urllib.error import HTTPError, URLError
from urllib.parse import urljoin, urlsplit
from urllib.request import HTTPRedirectHandler, Request, build_opener

from blazor_public_edge_workbench_contract import (
    CONTRACT_NAME,
    CORE_ROUTES,
    EXPANDED_ROUTES,
    MAX_RESPONSE_BYTES,
    REDIRECT_STATUSES,
    REQUIRED_ROUTE_MODEL_NOTE,
    ROUTE_MARKER_REQUIREMENTS,
    ROUTE_SPECS,
    WORKFLOW_REQUIREMENTS,
    derived_claims,
    evaluate_response_identity,
    normalize_base_url,
    path_query,
    validate_probe_record,
    validate_same_origin_url,
)


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_PATH = (
    REPO_ROOT
    / ".codex-studio"
    / "published"
    / "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"
)
DEFAULT_BASE_URL = "https://chummer.run"
DEFAULT_TIMEOUT_SECONDS = 20.0
DEFAULT_MAX_REDIRECTS = 5
CANONICAL_BASE_URL = "https://chummer.run"
LOOPBACK_HOSTS = {"127.0.0.1", "::1", "localhost"}


class NoRedirectHandler(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):  # type: ignore[override]
        return None

    def http_error_301(self, req, fp, code, msg, headers):  # type: ignore[override]
        return fp

    http_error_302 = http_error_303 = http_error_307 = http_error_308 = http_error_301


NO_REDIRECT_OPENER = build_opener(NoRedirectHandler())


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def fetch_once(url: str, timeout_seconds: float) -> tuple[int, dict[str, str], bytes]:
    request = Request(
        url,
        method="GET",
        headers={
            "Accept": "text/html,application/json;q=0.9,*/*;q=0.8",
            "User-Agent": "chummer6-ui-public-edge-receipt/2.0",
        },
    )
    try:
        response = NO_REDIRECT_OPENER.open(request, timeout=timeout_seconds)
    except HTTPError as exc:
        response = exc
    with response:
        status = int(response.status)
        headers = {key.lower(): value for key, value in response.headers.items()}
        body = response.read(MAX_RESPONSE_BYTES + 1)
    if len(body) > MAX_RESPONSE_BYTES:
        raise ValueError(f"response exceeded {MAX_RESPONSE_BYTES} bytes")
    return status, headers, body


def probe_route(
    base_url: str,
    spec,
    timeout_seconds: float,
    max_redirects: int,
) -> dict[str, Any]:
    requested_url = base_url + spec.route
    current_url = requested_url
    redirect_chain: list[dict[str, Any]] = []
    errors: list[str] = []
    initial_status: int | None = None
    final_status: int | None = None
    final_headers: dict[str, str] = {}
    final_body = b""

    while True:
        try:
            status, headers, body = fetch_once(current_url, timeout_seconds)
        except (OSError, TimeoutError, URLError, ValueError) as exc:
            errors.append(f"request failed for {current_url}: {exc}")
            break
        except Exception as exc:  # Keep evidence materialization fail-closed.
            errors.append(f"request failed for {current_url}: {exc}")
            break

        if initial_status is None:
            initial_status = status
        if status not in REDIRECT_STATUSES:
            final_status = status
            final_headers = headers
            final_body = body
            break

        location = headers.get("location", "").strip()
        if not location:
            errors.append(f"redirect response {status} did not include Location")
            final_status = status
            final_headers = headers
            final_body = body
            break
        if len(redirect_chain) >= max_redirects:
            errors.append(f"redirect limit exceeded ({max_redirects})")
            final_status = status
            final_headers = headers
            final_body = body
            break

        target_url = urljoin(current_url, location)
        redirect_chain.append(
            {
                "from_url": current_url,
                "http_status": status,
                "location": location,
                "to_url": target_url,
            }
        )
        target_error = validate_same_origin_url(target_url, base_url)
        if target_error:
            errors.append(target_error)
            current_url = target_url
            break
        current_url = target_url

    content_type = final_headers.get("content-type", "")
    identity = evaluate_response_identity(spec, content_type, final_body)
    preliminary = {
        "checked": True,
        "url": requested_url,
        "http_status": initial_status,
        "final_url": current_url,
        "final_http_status": final_status,
        "final_path_query": path_query(current_url),
        "expected_final_path_query": spec.expected_final_path_query,
        "redirect_count": len(redirect_chain),
        "redirect_chain": redirect_chain,
        "response_content_type": content_type,
        "response_body_bytes": len(final_body),
        "response_body_sha256": hashlib.sha256(final_body).hexdigest() if final_body else "",
        "response_identity": identity,
        "route": spec.route,
    }
    structural_reasons = validate_probe_record(
        {**preliminary, "ok": True, "error": "", "errors": []},
        spec,
        base_url,
    )
    errors.extend(
        reason
        for reason in structural_reasons
        if reason != "ok does not match the revalidated route result"
    )
    errors = list(dict.fromkeys(errors))
    return {
        **preliminary,
        "ok": not errors,
        "error": "; ".join(errors),
        "errors": errors,
    }


def build_receipt(
    base_url: str,
    timeout_seconds: float,
    max_redirects: int,
) -> dict[str, Any]:
    route_probes = [
        probe_route(base_url, spec, timeout_seconds, max_redirects) for spec in ROUTE_SPECS
    ]
    successful_routes = {
        str(probe["route"]) for probe in route_probes if probe.get("ok") is True
    }
    route_probe_failures = [
        {
            "route": probe["route"],
            "http_status": probe["http_status"],
            "final_url": probe["final_url"],
            "final_http_status": probe["final_http_status"],
            "errors": probe["errors"],
        }
        for probe in route_probes
        if not probe["ok"]
    ]
    core_passed = CORE_ROUTES <= successful_routes
    expanded_passed = core_passed and EXPANDED_ROUTES <= successful_routes
    proof_shape = "expanded" if expanded_passed else "core" if core_passed else "incomplete"
    status = "passed" if expanded_passed else "failed"

    return {
        "contract_name": CONTRACT_NAME,
        "generated_at": utc_now(),
        "status": status,
        "base_url": base_url,
        "proof_shape": proof_shape,
        "runtime_required": True,
        "route_probe_executed": True,
        "portal_route_probe_script": "scripts/materialize-blazor-public-edge-workbench-proof.py",
        "route_proof_markers": derived_claims(
            successful_routes, ROUTE_MARKER_REQUIREMENTS
        ),
        "proof_routes": [spec.route for spec in ROUTE_SPECS if spec.route in successful_routes],
        "required_routes": [spec.route for spec in ROUTE_SPECS],
        "workflow_proofs": derived_claims(successful_routes, WORKFLOW_REQUIREMENTS),
        "route_probe_count": len(route_probes),
        "passed_route_probe_count": len(successful_routes),
        "route_probe_failures": route_probe_failures,
        "route_probes": route_probes,
        "source_receipt": ".codex-studio/published/UI_EXTERNAL_HOST_PROOF_BLOCKERS.generated.json",
        "notes": [
            "Hosted public-edge route-entry evidence uses bounded explicit redirects and response-identity checks; HTTP 200 alone is insufficient.",
            REQUIRED_ROUTE_MODEL_NOTE,
            "Claims and proof_shape are derived only from routes whose final identity passed.",
        ],
    }


def write_receipt(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    serialized = json.dumps(payload, indent=2, sort_keys=True) + "\n"
    with tempfile.NamedTemporaryFile(
        mode="w",
        encoding="utf-8",
        dir=path.parent,
        prefix=f".{path.name}.",
        suffix=".tmp",
        delete=False,
    ) as handle:
        temporary_path = Path(handle.name)
        handle.write(serialized)
        handle.flush()
        os.fsync(handle.fileno())
    temporary_path.replace(path)


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Probe the hosted Blazor route-entry surface and emit its receipt."
    )
    parser.add_argument(
        "--base-url",
        default=os.environ.get("CHUMMER_BLAZOR_PUBLIC_EDGE_BASE_URL", DEFAULT_BASE_URL),
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path(
            os.environ.get(
                "CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH",
                str(DEFAULT_OUTPUT_PATH),
            )
        ),
    )
    parser.add_argument(
        "--timeout-seconds",
        type=float,
        default=os.environ.get(
            "CHUMMER_BLAZOR_PUBLIC_EDGE_TIMEOUT_SECONDS",
            str(DEFAULT_TIMEOUT_SECONDS),
        ),
    )
    parser.add_argument(
        "--max-redirects",
        type=int,
        default=os.environ.get(
            "CHUMMER_BLAZOR_PUBLIC_EDGE_MAX_REDIRECTS",
            str(DEFAULT_MAX_REDIRECTS),
        ),
    )
    parser.add_argument("--allow-test-origin", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = ()) -> int:
    args = parse_args(argv)
    try:
        base_url = normalize_base_url(args.base_url)
    except ValueError as exc:
        print(f"invalid base URL: {exc}")
        return 2
    parsed_base_url = urlsplit(base_url)
    if base_url != CANONICAL_BASE_URL and not (
        args.allow_test_origin
        and parsed_base_url.scheme == "http"
        and parsed_base_url.hostname in LOOPBACK_HOSTS
    ):
        print(
            f"base URL must be exactly {CANONICAL_BASE_URL!r}; "
            "only explicit loopback tests may override it"
        )
        return 2
    if args.timeout_seconds <= 0:
        print("timeout seconds must be greater than zero")
        return 2
    if not 0 <= args.max_redirects <= 10:
        print("max redirects must be between zero and ten")
        return 2

    payload = build_receipt(base_url, args.timeout_seconds, args.max_redirects)
    write_receipt(args.output, payload)
    print(
        f"wrote {args.output} ({payload['status']}; "
        f"{payload['passed_route_probe_count']}/{payload['route_probe_count']} routes)"
    )
    return 0 if payload["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main(None))
