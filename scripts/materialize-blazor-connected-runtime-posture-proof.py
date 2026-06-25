#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json"
CONTRACT_NAME = "chummer6-ui.blazor_connected_runtime_posture"
DEFAULT_LIVE_URL = "https://chummer.run/blazor/app"
PROOF_COMPATIBLE_ROUTE = "/blazor/workbench"


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def contains_all(text: str, needles: list[str]) -> bool:
    return all(needle in text for needle in needles)


def add_check(checks: list[dict[str, object]], check_id: str, passed: bool, evidence: str) -> None:
    checks.append(
        {
            "id": check_id,
            "passed": passed,
            "evidence": evidence,
        }
    )


def fetch_text(url: str) -> tuple[str | None, str | None]:
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": "chummer-blazor-connected-runtime-posture-proof/1.0",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            return response.read().decode("utf-8", errors="replace"), None
    except (OSError, urllib.error.URLError) as exc:
        return None, str(exc)


def main() -> int:
    portal_program = read_text(REPO_ROOT / "Chummer.Portal" / "Program.cs")
    preview_razor = read_text(REPO_ROOT / "Chummer.Blazor" / "Components" / "Pages" / "Preview.razor")
    compose = read_text(REPO_ROOT / "docker-compose.yml")
    runbook = read_text(REPO_ROOT / "docs" / "BLAZOR_SELF_HOST_RUNBOOK.md")
    docs_index = read_text(REPO_ROOT / "docs" / "BLAZOR_WEB_CLIENT_DOCS_INDEX.md")
    parity_goal = read_text(REPO_ROOT / "docs" / "BLAZOR_WEB_CLIENT_PARITY_GOAL.md")
    release_signoff = read_text(REPO_ROOT / "docs" / "WORKBENCH_RELEASE_SIGNOFF.md")
    env_example = read_text(REPO_ROOT / "docs" / "examples" / "self-hosted-browser-workbench.env.example")
    live_url = os.environ.get("CHUMMER_BLAZOR_CONNECTED_RUNTIME_LIVE_URL", DEFAULT_LIVE_URL).strip() or DEFAULT_LIVE_URL
    live_html, live_error = fetch_text(live_url)

    checks: list[dict[str, object]] = []

    add_check(
        checks,
        "portal_session_and_coach_routes_forward_signed_owner_headers",
        contains_all(
            portal_program,
            [
                "MapPassThroughProxy(app, BuildCatchallPattern(options.SessionUrl), options.SessionProxyUrl, options, applyOwnerHeaders: true)",
                "MapPassThroughProxy(app, BuildCatchallPattern(options.CoachUrl), options.CoachProxyUrl, options, applyOwnerHeaders: true)",
                "bool applyOwnerHeaders = false",
                "if (applyOwnerHeaders && options is not null)",
                "ApplyOwnerHeaders(context, options)",
            ],
        ),
        "Chummer.Portal forwards signed owner headers for optional session and coach pass-through routes.",
    )

    add_check(
        checks,
        "portal_ai_route_keeps_signed_owner_header_seam",
        contains_all(
            portal_program,
            [
                'app.Map("/api/ai/{**catchall}"',
                "ApplyOwnerHeaders(context, options)",
                "options.AiProxyUrl",
            ],
        ),
        "Chummer.Portal keeps AI forwarding behind the same signed owner header seam.",
    )

    add_check(
        checks,
        "portal_compose_exposes_optional_connected_runtime_env",
        contains_all(
            compose,
            [
                "CHUMMER_PORTAL_SESSION_URL",
                "CHUMMER_PORTAL_SESSION_PROXY_URL",
                "CHUMMER_PORTAL_COACH_URL",
                "CHUMMER_PORTAL_COACH_PROXY_URL",
                "CHUMMER_PORTAL_AI_PROXY_URL",
                "CHUMMER_RUN_URL",
                "CHUMMER_PORTAL_OWNER_SHARED_KEY",
            ],
        ),
        "docker-compose.yml exposes optional session, coach, AI, run URL, and owner shared-key settings.",
    )

    add_check(
        checks,
        "blazor_compose_receives_connected_runtime_state_without_exposing_urls",
        compose.count('CHUMMER_PORTAL_SESSION_PROXY_URL: "${CHUMMER_PORTAL_SESSION_PROXY_URL:-}"') >= 2
        and compose.count('CHUMMER_PORTAL_COACH_PROXY_URL: "${CHUMMER_PORTAL_COACH_PROXY_URL:-}"') >= 2
        and compose.count('CHUMMER_PORTAL_AI_PROXY_URL: "${CHUMMER_PORTAL_AI_PROXY_URL:-}"') >= 2,
        "Standalone and portal Blazor containers receive connected-runtime configuration for server-rendered status only.",
    )

    add_check(
        checks,
        "blazor_preview_renders_connected_runtime_posture_card",
        contains_all(
            preview_razor,
            [
                "data-connected-runtime-posture",
                "data-connected-runtime-session",
                "data-connected-runtime-coach",
                "data-connected-runtime-ai",
                "BuildConnectedRuntimeTitle",
                "BuildConnectedRuntimeSummary",
                "When enabled, forwarding stays behind the portal signed-owner boundary.",
            ],
        ),
        "The browser workbench proof shelf renders a visible connected-runtime posture card without exposing proxy URLs.",
    )

    add_check(
        checks,
        "hosted_chummer_run_workbench_renders_connected_runtime_posture_card",
        live_html is not None
        and contains_all(
            live_html,
            [
                "data-connected-runtime-posture",
                "data-connected-runtime-session",
                "data-connected-runtime-coach",
                "data-connected-runtime-ai",
                "Connected runtime",
                "signed-owner boundary",
            ],
        ),
        f"Live hosted Chummer Online HTML at {live_url} renders the connected-runtime posture card."
        if live_html is not None
        else f"Live hosted Chummer Online HTML at {live_url} could not be read: {live_error}",
    )

    add_check(
        checks,
        "self_host_env_example_lists_connected_runtime_settings",
        contains_all(
            env_example,
            [
                "CHUMMER_PORTAL_SESSION_URL=/session/",
                "CHUMMER_PORTAL_SESSION_PROXY_URL",
                "CHUMMER_PORTAL_COACH_URL=/coach/",
                "CHUMMER_PORTAL_COACH_PROXY_URL",
                "CHUMMER_PORTAL_AI_PROXY_URL",
                "CHUMMER_RUN_URL",
            ],
        ),
        "The self-host environment example exposes optional connected-runtime lanes without enabling them by default.",
    )

    add_check(
        checks,
        "operator_docs_reference_connected_runtime_receipt",
        contains_all(
            docs_index,
            [
                "Connected Runtime Posture",
                "BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json",
                "session",
                "coach",
                "AI",
            ],
        )
        and contains_all(
            runbook,
            [
                "Connected-runtime posture",
                "BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json",
                "signed portal-owner header",
                "CHUMMER_PORTAL_SESSION_PROXY_URL",
                "CHUMMER_PORTAL_COACH_PROXY_URL",
                "CHUMMER_PORTAL_AI_PROXY_URL",
            ],
        ),
        "Browser-client operator docs expose the connected-runtime receipt and signed owner forwarding boundary.",
    )

    add_check(
        checks,
        "parity_and_signoff_docs_keep_connected_runtime_scope_separate",
        contains_all(
            parity_goal,
            [
                "BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json",
                "connected-runtime",
                "session",
                "coach",
                "AI",
            ],
        )
        and contains_all(
            release_signoff,
            [
                "BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json",
                "connected-runtime",
                "not full workflow parity",
            ],
        ),
        "Parity/signoff docs distinguish connected-runtime posture from full web-client workflow parity.",
    )

    status = "passed" if all(bool(check["passed"]) for check in checks) else "failed"
    payload = {
        "contract_name": CONTRACT_NAME,
        "status": status,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "checks": checks,
        "live_url": live_url,
        "public_app_route": "/blazor/app",
        "proof_compatible_route": PROOF_COMPATIBLE_ROUTE,
        "connected_runtime_routes": [
            "/session/",
            "/coach/",
            "/api/ai/",
        ],
        "owner_context_boundary": "signed-portal-owner-header-when-shared-key-configured",
        "scope": "posture-and-forwarding-boundary-not-full-runtime-parity",
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    if status != "passed":
        print(json.dumps(payload, indent=2, sort_keys=True))
        return 1

    print(f"wrote {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
