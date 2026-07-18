#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_PATH = (
    REPO_ROOT
    / ".codex-studio"
    / "published"
    / "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"
)
CONTRACT_NAME = "chummer6-ui.blazor_public_edge_workbench_proof"
DEFAULT_BASE_URL = "https://chummer.run"
DEFAULT_TIMEOUT_SECONDS = 20.0

ROUTE_PROOF_MARKERS = [
    "public_chummer_app_route",
    "public_chummer_app_roster_route",
    "public_blazor_root_redirect",
    "public_blazor_home_roster_entry",
    "public_blazor_health",
    "public_workbench_route",
    "public_workspace_restore_route",
    "public_startup_deep_link_route",
    "public_startup_workbench_command_routes",
    "public_result_continuation_routes",
    "public_action_continuation_routes",
    "public_committed_action_route",
    "public_advanced_action_routes",
    "public_advanced_committed_action_routes",
]
WORKFLOW_PROOFS = [
    "blazor_root_redirect",
    "workbench_route",
    "workspace_resume_route_shape",
    "new_character_deep_link_route_shape",
    "startup_command_route_shapes",
    "result_continuation_route_shapes",
    "action_continuation_route_shapes",
    "committed_action_route_shape",
    "advanced_action_route_shapes",
    "advanced_committed_action_route_shapes",
]
PROOF_ROUTES = [
    "/app",
    "/app?command=character_roster",
    "/blazor/",
    "/blazor/health",
    "/blazor/home",
    "/blazor/app",
    "/blazor/workbench",
    "/blazor/workbench?workspace=ws-1",
    "/blazor/preview?command=new_character",
    "/blazor/workbench?workspace=ws-1&command=save_character_as",
    "/blazor/workbench?workspace=ws-1&command=export_character&dialog_action=download",
    "/blazor/workbench?workspace=ws-1&command=print_character",
    "/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add",
    "/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add&dialog_action=add",
    "/blazor/workbench?command=new_character",
    "/blazor/workbench?command=open_character",
    "/blazor/workbench?command=open_for_printing",
    "/blazor/workbench?command=open_for_export",
    "/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=complex_form_add",
    "/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=complex_form_add&dialog_action=add",
]
REQUIRED_ROUTE_MODEL_NOTE = (
    "Public product navigation remains /app, /blazor/ redirects into the roster-first "
    "app?command=character_roster browser workflow, /blazor/app is the hosted app path, "
    "/blazor/home carries the roster-first orientation entry, and /blazor/workbench is "
    "the canonical proof-compatible route base."
)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def normalize_base_url(value: str) -> str:
    normalized = value.strip().rstrip("/")
    parsed = urlsplit(normalized)
    if parsed.scheme not in {"http", "https"} or not parsed.netloc:
        raise ValueError("base URL must be an absolute http:// or https:// URL")
    if parsed.query or parsed.fragment:
        raise ValueError("base URL must not contain a query string or fragment")
    return normalized


def probe_route(base_url: str, route: str, timeout_seconds: float) -> dict[str, Any]:
    url = base_url + route
    request = Request(
        url,
        method="GET",
        headers={
            "Accept": "text/html,application/json;q=0.9,*/*;q=0.8",
            "User-Agent": "chummer6-ui-public-edge-receipt/1.0",
        },
    )

    try:
        with urlopen(request, timeout=timeout_seconds) as response:
            http_status = int(response.status)
            response.read(1)
            ok = 200 <= http_status < 400
            error = "" if ok else f"HTTP {http_status}"
    except HTTPError as exc:
        http_status = int(exc.code)
        ok = False
        error = f"HTTP {exc.code}: {exc.reason}"
    except URLError as exc:
        http_status = None
        ok = False
        error = f"URL error: {exc.reason}"
    except (OSError, TimeoutError) as exc:
        http_status = None
        ok = False
        error = f"request error: {exc}"
    except Exception as exc:  # Keep evidence generation fail-closed on client errors.
        http_status = None
        ok = False
        error = f"request error: {exc}"

    return {
        "checked": True,
        "url": url,
        "http_status": http_status,
        "ok": ok,
        "error": error,
        "route": route,
    }


def build_receipt(base_url: str, timeout_seconds: float) -> dict[str, Any]:
    route_probes = [
        probe_route(base_url, route, timeout_seconds) for route in PROOF_ROUTES
    ]
    route_probe_failures = [
        {
            "route": probe["route"],
            "http_status": probe["http_status"],
            "error": probe["error"],
        }
        for probe in route_probes
        if not probe["ok"]
    ]
    status = "passed" if not route_probe_failures else "failed"

    return {
        "contract_name": CONTRACT_NAME,
        "generated_at": utc_now(),
        "status": status,
        "base_url": base_url,
        "proof_shape": "expanded",
        "runtime_required": True,
        "route_probe_executed": True,
        "portal_route_probe_script": "scripts/materialize-blazor-public-edge-workbench-proof.py",
        "route_proof_markers": ROUTE_PROOF_MARKERS,
        "proof_routes": PROOF_ROUTES,
        "workflow_proofs": WORKFLOW_PROOFS,
        "route_probe_count": len(route_probes),
        "route_probe_failures": route_probe_failures,
        "route_probes": route_probes,
        "source_receipt": ".codex-studio/published/UI_EXTERNAL_HOST_PROOF_BLOCKERS.generated.json",
        "notes": [
            "Hosted public-edge browser proof is distinct from the Docker self-host workbench receipt.",
            REQUIRED_ROUTE_MODEL_NOTE,
            "This receipt proves hosted /blazor route-entry posture and route health, not full browser workflow execution.",
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
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = ()) -> int:
    args = parse_args(argv)
    try:
        base_url = normalize_base_url(args.base_url)
    except ValueError as exc:
        print(f"invalid base URL: {exc}")
        return 2
    if args.timeout_seconds <= 0:
        print("timeout seconds must be greater than zero")
        return 2

    payload = build_receipt(base_url, args.timeout_seconds)
    write_receipt(args.output, payload)
    print(f"wrote {args.output} ({payload['status']})")
    return 0 if payload["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main(None))
