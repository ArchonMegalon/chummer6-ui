#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLIC_EDGE_COMPOSE = Path(
    os.environ.get(
        "CHUMMER_PUBLIC_EDGE_COMPOSE_PATH",
        REPO_ROOT.parent / "chummer.run-services" / "docker-compose.public-edge.yml",
    )
)
OUTPUT_PATH = REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_ANALYTICS_POSTURE.generated.json"
CONTRACT_NAME = "chummer6-ui.blazor_analytics_posture"
DEFAULT_LIVE_URL = "https://chummer.run/blazor/"
DEFAULT_HEALTH_URL = "https://chummer.run/blazor/health"


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def add_check(checks: list[dict[str, object]], check_id: str, passed: bool, evidence: str) -> None:
    checks.append(
        {
            "id": check_id,
            "passed": passed,
            "evidence": evidence,
        }
    )


def contains_all(text: str, needles: list[str]) -> bool:
    return all(needle in text for needle in needles)


def fetch_text(url: str) -> tuple[str | None, str | None]:
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": "chummer-blazor-analytics-posture-proof/1.0",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            return response.read().decode("utf-8", errors="replace"), None
    except (OSError, urllib.error.URLError) as exc:
        return None, str(exc)


def main() -> int:
    app_razor = read_text(REPO_ROOT / "Chummer.Blazor" / "Components" / "App.razor")
    compose = read_text(REPO_ROOT / "docker-compose.yml")
    public_edge_compose = read_text(PUBLIC_EDGE_COMPOSE)
    runbook = read_text(REPO_ROOT / "docs" / "BLAZOR_SELF_HOST_RUNBOOK.md")
    parity_goal = read_text(REPO_ROOT / "docs" / "BLAZOR_WEB_CLIENT_PARITY_GOAL.md")
    docs_index = read_text(REPO_ROOT / "docs" / "BLAZOR_WEB_CLIENT_DOCS_INDEX.md")
    release_signoff = read_text(REPO_ROOT / "docs" / "WORKBENCH_RELEASE_SIGNOFF.md")
    env_example = read_text(REPO_ROOT / "docs" / "examples" / "self-hosted-browser-workbench.env.example")
    receipt_example = read_text(REPO_ROOT / "docs" / "examples" / "blazor-analytics-posture.receipt.example.json")
    live_url = os.environ.get("CHUMMER_BLAZOR_ANALYTICS_LIVE_URL", DEFAULT_LIVE_URL).strip() or DEFAULT_LIVE_URL
    health_url = os.environ.get("CHUMMER_BLAZOR_ANALYTICS_HEALTH_URL", DEFAULT_HEALTH_URL).strip() or DEFAULT_HEALTH_URL
    live_html, live_error = fetch_text(live_url)
    health_text, health_error = fetch_text(health_url)
    health_payload: dict[str, object] = {}
    if health_text is not None:
        try:
            loaded_health = json.loads(health_text)
            if isinstance(loaded_health, dict):
                health_payload = loaded_health
        except json.JSONDecodeError:
            health_error = "health response was not valid JSON"

    checks: list[dict[str, object]] = []

    add_check(
        checks,
        "blazor_shell_loads_rybbit_only_from_config",
        contains_all(
            app_razor,
            [
                "@inject IConfiguration Configuration",
                "BuildRybbitAnalyticsOptions()",
                "CHUMMER_ANALYTICS_PROVIDER",
                "CHUMMER_RYBBIT_SITE_ID",
                "CHUMMER_RYBBIT_SCRIPT_URL",
                "CHUMMER_RYBBIT_BASE_URL",
                "data-site-id",
                "data-chummer-analytics-provider=\"rybbit\"",
                "data-chummer-analytics-scope=\"route-workflow-metadata-only\"",
                "data-chummer-session-replay=\"disabled\"",
                "data-chummer-autocapture=\"disabled\"",
            ],
        ),
        "Chummer.Blazor/Components/App.razor conditionally renders the Rybbit script from configuration with explicit no-replay/no-autocapture metadata.",
    )

    add_check(
        checks,
        "analytics_event_bridge_uses_sanitized_route_metadata",
        contains_all(
            app_razor,
            [
                "window.chummerAnalytics",
                "browser_route",
                "analytics_scope",
                "session_replay",
                "autocapture",
                "route-workflow-metadata-only",
                "route_family",
                "chummer_app",
                "command_id",
                "tab_id",
                "control_id",
                "dialog_action_id",
                "has_workspace",
                "has_fixture",
            ],
        ),
        "The Blazor shell event bridge emits route/workflow metadata and explicit no-replay/no-autocapture posture instead of character payloads.",
    )

    add_check(
        checks,
        "analytics_sensitive_property_denylist_present",
        contains_all(
            app_razor,
            [
                "forbiddenPropertyPattern",
                "alias",
                "character",
                "content",
                "document",
                "email",
                "file",
                "hash",
                "name",
                "owner",
                "payload",
                "workspace",
                "xml",
            ],
        ),
        "The client-side analytics bridge rejects sensitive property keys before dispatch.",
    )

    add_check(
        checks,
        "self_host_compose_defaults_analytics_off",
        compose.count('CHUMMER_ANALYTICS_PROVIDER: "${CHUMMER_ANALYTICS_PROVIDER:-none}"') >= 2
        and contains_all(
            compose,
            [
                'CHUMMER_RYBBIT_SCRIPT_URL: "${CHUMMER_RYBBIT_SCRIPT_URL:-}"',
                'CHUMMER_RYBBIT_BASE_URL: "${CHUMMER_RYBBIT_BASE_URL:-}"',
                'CHUMMER_RYBBIT_SITE_ID: "${CHUMMER_RYBBIT_SITE_ID:-}"',
            ],
        ),
        "docker-compose.yml keeps standalone and portal Blazor analytics default-off for self-hosters.",
    )

    add_check(
        checks,
        "blazor_health_reports_non_secret_analytics_policy",
        contains_all(
            read_text(REPO_ROOT / "Chummer.Blazor" / "Program.cs"),
            [
                "SelfHostDefault: \"analytics-disabled\"",
                "HostedPublicEdge: \"rybbit-enabled-when-site-id-configured\"",
                "SensitiveDataPolicy: \"route-and-workflow-metadata-only\"",
                "SessionReplayPolicy: \"disabled\"",
                "AutocapturePolicy: \"disabled\"",
                "sealed record AnalyticsHealth",
            ],
        ),
        "Chummer.Blazor /health reports non-secret analytics policy posture, including no-replay/no-autocapture policy, for operators.",
    )

    add_check(
        checks,
        "hosted_chummer_run_public_blazor_enables_rybbit",
        contains_all(
            public_edge_compose,
            [
                "chummer-public-blazor:",
                "CHUMMER_ANALYTICS_PROVIDER: rybbit",
                "CHUMMER_RYBBIT_SITE_ID: ${RYBBIT_CHUMMER_RUN_SITE_ID:-}",
                "CHUMMER_RYBBIT_SCRIPT_URL: ${RYBBIT_CHUMMER_RUN_SCRIPT_URL:-}",
                "CHUMMER_RYBBIT_BASE_URL: ${RYBBIT_CHUMMER_RUN_SCRIPT_ORIGIN:-https://app.rybbit.io}",
            ],
        ),
        "docker-compose.public-edge.yml maps hosted Rybbit configuration into the public Blazor container.",
    )

    add_check(
        checks,
        "self_host_env_example_documents_default_off",
        contains_all(
            env_example,
            [
                "CHUMMER_ANALYTICS_PROVIDER=none",
                "CHUMMER_RYBBIT_SITE_ID",
                "CHUMMER_RYBBIT_SCRIPT_URL",
                "CHUMMER_RYBBIT_BASE_URL",
            ],
        ),
        "The self-host environment example exposes optional Rybbit variables while keeping analytics disabled.",
    )

    add_check(
        checks,
        "runbook_documents_privacy_boundary",
        contains_all(
            runbook,
            [
                "Optional analytics inputs",
                "CHUMMER_ANALYTICS_PROVIDER=none",
                "CHUMMER_ANALYTICS_PROVIDER=rybbit",
                "does not emit character names",
                "workspace ids",
                "owner ids",
                "XML",
                "payloads",
                "hashes",
                "session replay disabled",
            ],
        ),
        "The self-host runbook documents the opt-in analytics posture and sensitive-data boundary.",
    )

    add_check(
        checks,
        "browser_parity_docs_reference_analytics_receipt",
        contains_all(
            parity_goal,
            [
                "BLAZOR_ANALYTICS_POSTURE.generated.json",
                "Rybbit",
                "self-host default-off",
                "route/workflow metadata only",
            ],
        )
        and contains_all(
            docs_index,
            [
                "Analytics and Privacy Posture",
                "BLAZOR_ANALYTICS_POSTURE.generated.json",
                "docs/examples/blazor-analytics-posture.receipt.example.json",
                "CHUMMER_ANALYTICS_PROVIDER=none",
                "Rybbit",
                "route/workflow metadata",
                "must not emit character names",
                "session replay",
                "autocapture",
                "selfHostDefault",
                "hostedPublicEdge",
                "sensitiveDataPolicy",
                "sessionReplayPolicy",
                "autocapturePolicy",
            ],
        )
        and contains_all(
            release_signoff,
            [
                "BLAZOR_ANALYTICS_POSTURE.generated.json",
                "optional Rybbit wiring",
                "privacy boundaries",
                "not workflow parity",
            ],
        ),
        "Browser parity and release signoff docs distinguish analytics posture proof from workflow parity proof.",
    )

    add_check(
        checks,
        "analytics_example_receipt_documents_policy_fields",
        contains_all(
            receipt_example,
            [
                '"contract_name": "chummer6-ui.blazor_analytics_posture"',
                '"public_app_route_family": "chummer_app"',
                '"sensitive_data_policy": "route-and-workflow-metadata-only"',
                '"session_replay_policy": "disabled"',
                '"autocapture_policy": "disabled"',
                "blazor_shell_loads_rybbit_only_from_config",
                "analytics_event_bridge_uses_sanitized_route_metadata",
                "blazor_health_reports_non_secret_analytics_policy",
            ],
        ),
        "The analytics example receipt documents the route metadata, sensitive-data, no-replay, and no-autocapture fields.",
    )

    add_check(
        checks,
        "hosted_chummer_run_blazor_renders_rybbit_adapter",
        live_html is not None
        and contains_all(
            live_html,
            [
                "rybbit",
                "data-site-id",
                "window.chummerAnalytics",
            ],
        ),
        f"Live hosted Blazor HTML at {live_url} contains the Rybbit script tag and sanitized analytics bridge."
        if live_html is not None
        else f"Live hosted Blazor HTML at {live_url} could not be read: {live_error}",
    )

    health_analytics = health_payload.get("analytics") if isinstance(health_payload, dict) else None
    add_check(
        checks,
        "hosted_chummer_run_blazor_health_reports_rybbit_enabled",
        isinstance(health_analytics, dict)
        and health_analytics.get("provider") == "rybbit"
        and health_analytics.get("enabled") is True
        and health_analytics.get("siteIdConfigured") is True
        and health_analytics.get("selfHostDefault") == "analytics-disabled"
        and health_analytics.get("hostedPublicEdge") == "rybbit-enabled-when-site-id-configured"
        and health_analytics.get("sensitiveDataPolicy") == "route-and-workflow-metadata-only"
        and health_analytics.get("sessionReplayPolicy") == "disabled"
        and health_analytics.get("autocapturePolicy") == "disabled",
        f"Live hosted Blazor health at {health_url} reports Rybbit enabled without exposing secret values."
        if health_payload
        else f"Live hosted Blazor health at {health_url} could not be read: {health_error}",
    )

    status = "passed" if all(bool(check["passed"]) for check in checks) else "failed"
    payload = {
        "contract_name": CONTRACT_NAME,
        "status": status,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "checks": checks,
        "live_url": live_url,
        "health_url": health_url,
        "public_app_route": "/blazor/app",
        "public_app_route_family": "chummer_app",
        "proof_compatible_route": "/blazor/workbench",
        "self_host_default": "analytics-disabled",
        "hosted_public_edge": "rybbit-enabled-when-site-id-configured",
        "sensitive_data_policy": "route-and-workflow-metadata-only",
        "session_replay_policy": "disabled",
        "autocapture_policy": "disabled",
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
