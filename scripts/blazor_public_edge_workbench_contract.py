from __future__ import annotations

import json
import re
from dataclasses import dataclass
from typing import Any
from urllib.parse import urljoin, urlsplit


CONTRACT_NAME = "chummer6-ui.blazor_public_edge_workbench_proof"
REDIRECT_STATUSES = {301, 302, 303, 307, 308}
MAX_RESPONSE_BYTES = 4 * 1024 * 1024


@dataclass(frozen=True)
class RouteSpec:
    route: str
    expected_final_path_query: str
    tier: str
    identity_kind: str = "html"
    min_redirects: int = 0
    max_redirects: int = 0
    expected_attributes: tuple[tuple[str, str], ...] = ()
    expected_text: tuple[tuple[str, str], ...] = ()
    expected_json_fields: tuple[tuple[str, Any], ...] = ()


BASE_HREF = ("base_href", '<base href="/blazor/"')
APP_ATTRIBUTES = (
    ("data-route-family", "app"),
    ("data-route-segment", "app"),
    ("data-canonical-route", "app"),
    ("data-active-workflow", "character-roster"),
)
WORKBENCH_ATTRIBUTES = (
    ("data-chummer-classic-shell", "true"),
    ("data-route-segment", "workbench"),
    ("data-route-surface", "classic-desktop"),
    ("data-canonical-route", "workbench"),
)


def workbench_spec(
    route: str,
    *,
    tier: str = "core",
    command: str | None = None,
    tab: str | None = None,
    control: str | None = None,
    dialog_action: str | None = None,
    active_workflow: str = "workbench",
    expected_text: tuple[tuple[str, str], ...] = (),
) -> RouteSpec:
    attributes = list(WORKBENCH_ATTRIBUTES)
    attributes.append(("data-active-workflow", active_workflow))
    if command is not None:
        attributes.append(("data-command", command))
    if tab is not None:
        attributes.append(("data-tab", tab))
    if control is not None:
        attributes.append(("data-control", control))
    if dialog_action is not None:
        attributes.append(("data-dialog-action", dialog_action))
    return RouteSpec(
        route=route,
        expected_final_path_query=route,
        tier=tier,
        expected_attributes=tuple(attributes),
        expected_text=(BASE_HREF,) + expected_text,
    )


ROUTE_SPECS = (
    RouteSpec(
        route="/app",
        expected_final_path_query="/blazor/app",
        tier="core",
        min_redirects=1,
        max_redirects=2,
        expected_attributes=APP_ATTRIBUTES + (("data-command", "none"),),
        expected_text=(BASE_HREF,),
    ),
    RouteSpec(
        route="/app?command=character_roster",
        expected_final_path_query="/blazor/app?command=character_roster",
        tier="core",
        min_redirects=1,
        max_redirects=2,
        expected_attributes=APP_ATTRIBUTES
        + (
            ("data-command", "character-roster"),
            ("data-chummer-app-startup-command", "character_roster"),
        ),
        expected_text=(BASE_HREF,),
    ),
    RouteSpec(
        route="/blazor/",
        expected_final_path_query="/blazor/app?command=character_roster",
        tier="core",
        min_redirects=1,
        max_redirects=2,
        expected_attributes=APP_ATTRIBUTES
        + (
            ("data-command", "character-roster"),
            ("data-chummer-app-startup-command", "character_roster"),
        ),
        expected_text=(BASE_HREF,),
    ),
    RouteSpec(
        route="/blazor/health",
        expected_final_path_query="/blazor/health",
        tier="core",
        identity_kind="health_json",
        expected_json_fields=(
            ("ok", True),
            ("head", "blazor"),
            ("pathBase", "/blazor"),
        ),
    ),
    RouteSpec(
        route="/blazor/home",
        expected_final_path_query="/blazor/home",
        tier="core",
        expected_attributes=(("data-home-hero-action", "explore-chummer-online"),),
        expected_text=(
            BASE_HREF,
            ("home_heading", "Chummer Online for real dossier work."),
        ),
    ),
    RouteSpec(
        route="/blazor/app",
        expected_final_path_query="/blazor/app",
        tier="core",
        expected_attributes=APP_ATTRIBUTES + (("data-command", "none"),),
        expected_text=(BASE_HREF,),
    ),
    workbench_spec("/blazor/workbench"),
    workbench_spec(
        "/blazor/workbench?workspace=ws-1",
        expected_text=(("workspace_restoration", "Restored workspace: ws-1"),),
    ),
    RouteSpec(
        route="/blazor/preview?command=new_character",
        expected_final_path_query="/blazor/preview?command=new_character",
        tier="core",
        expected_attributes=(("data-command", "new_character"),),
        expected_text=(
            BASE_HREF,
            (
                "preview_route_heading",
                "Preview Chummer Online workflows without changing the public route.",
            ),
        ),
    ),
    workbench_spec(
        "/blazor/workbench?workspace=ws-1&command=save_character_as",
        command="save_character_as",
    ),
    workbench_spec(
        "/blazor/workbench?workspace=ws-1&command=export_character&dialog_action=download",
        command="export_character",
        dialog_action="download",
    ),
    workbench_spec(
        "/blazor/workbench?workspace=ws-1&command=print_character",
        command="print_character",
    ),
    workbench_spec(
        "/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add",
        tab="tab-contacts",
        control="contact_add",
        expected_text=(("contact_add_surface", "Add Contact"),),
    ),
    workbench_spec(
        "/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add&dialog_action=add",
        tab="tab-contacts",
        control="contact_add",
        dialog_action="add",
        expected_text=(
            ("contact_add_commit", "data-workbench-committed-result="),
        ),
    ),
    workbench_spec(
        "/blazor/workbench?command=new_character",
        tier="expanded",
        command="new_character",
        active_workflow="build-lab",
    ),
    workbench_spec(
        "/blazor/workbench?command=open_character",
        tier="expanded",
        command="open_character",
    ),
    workbench_spec(
        "/blazor/workbench?command=open_for_printing",
        tier="expanded",
        command="open_for_printing",
    ),
    workbench_spec(
        "/blazor/workbench?command=open_for_export",
        tier="expanded",
        command="open_for_export",
    ),
    workbench_spec(
        "/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=complex_form_add",
        tier="expanded",
        tab="tab-technomancer",
        control="complex_form_add",
    ),
    workbench_spec(
        "/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=complex_form_add&dialog_action=add",
        tier="expanded",
        tab="tab-technomancer",
        control="complex_form_add",
        dialog_action="add",
    ),
)
ROUTE_SPEC_BY_ROUTE = {spec.route: spec for spec in ROUTE_SPECS}
CORE_ROUTES = {spec.route for spec in ROUTE_SPECS if spec.tier == "core"}
EXPANDED_ROUTES = {spec.route for spec in ROUTE_SPECS if spec.tier == "expanded"}

ROUTE_MARKER_REQUIREMENTS = {
    "public_chummer_app_route": {"/app"},
    "public_chummer_app_roster_route": {"/app?command=character_roster"},
    "public_blazor_root_redirect": {"/blazor/"},
    "public_blazor_home_roster_entry": {"/blazor/home"},
    "public_blazor_health": {"/blazor/health"},
    "public_workbench_route": {"/blazor/workbench"},
    "public_workspace_restore_route": {"/blazor/workbench?workspace=ws-1"},
    "public_startup_deep_link_route": {"/blazor/preview?command=new_character"},
    "public_result_continuation_routes": {
        "/blazor/workbench?workspace=ws-1&command=save_character_as",
        "/blazor/workbench?workspace=ws-1&command=export_character&dialog_action=download",
        "/blazor/workbench?workspace=ws-1&command=print_character",
    },
    "public_action_continuation_routes": {
        "/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add"
    },
    "public_committed_action_route": {
        "/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add&dialog_action=add"
    },
    "public_startup_workbench_command_routes": {
        "/blazor/workbench?command=new_character",
        "/blazor/workbench?command=open_character",
        "/blazor/workbench?command=open_for_printing",
        "/blazor/workbench?command=open_for_export",
    },
    "public_advanced_action_routes": {
        "/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=complex_form_add"
    },
    "public_advanced_committed_action_routes": {
        "/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=complex_form_add&dialog_action=add"
    },
}
WORKFLOW_REQUIREMENTS = {
    "blazor_root_redirect": {"/blazor/"},
    "workbench_route": {"/blazor/workbench"},
    "workspace_resume_route_shape": {"/blazor/workbench?workspace=ws-1"},
    "new_character_deep_link_route_shape": {"/blazor/preview?command=new_character"},
    "result_continuation_route_shapes": ROUTE_MARKER_REQUIREMENTS["public_result_continuation_routes"],
    "action_continuation_route_shapes": ROUTE_MARKER_REQUIREMENTS["public_action_continuation_routes"],
    "committed_action_route_shape": ROUTE_MARKER_REQUIREMENTS["public_committed_action_route"],
    "startup_command_route_shapes": ROUTE_MARKER_REQUIREMENTS["public_startup_workbench_command_routes"],
    "advanced_action_route_shapes": ROUTE_MARKER_REQUIREMENTS["public_advanced_action_routes"],
    "advanced_committed_action_route_shapes": ROUTE_MARKER_REQUIREMENTS["public_advanced_committed_action_routes"],
}
CORE_ROUTE_PROOF_MARKERS = {
    marker
    for marker, routes in ROUTE_MARKER_REQUIREMENTS.items()
    if routes <= CORE_ROUTES
}
EXPANDED_ROUTE_PROOF_MARKERS = set(ROUTE_MARKER_REQUIREMENTS) - CORE_ROUTE_PROOF_MARKERS
CORE_WORKFLOW_PROOFS = {
    workflow
    for workflow, routes in WORKFLOW_REQUIREMENTS.items()
    if routes <= CORE_ROUTES
}
EXPANDED_WORKFLOW_PROOFS = set(WORKFLOW_REQUIREMENTS) - CORE_WORKFLOW_PROOFS

REQUIRED_ROUTE_MODEL_NOTE = (
    "Public product navigation remains /app, /blazor/ redirects into the roster-first "
    "app?command=character_roster browser workflow, /blazor/app is the hosted app path, "
    "/blazor/home carries the roster-first orientation entry, and /blazor/workbench is "
    "the canonical proof-compatible route base."
)


def path_query(url: str) -> str:
    parsed = urlsplit(url)
    return parsed.path + (f"?{parsed.query}" if parsed.query else "")


def normalize_base_url(value: str) -> str:
    normalized = value.strip().rstrip("/")
    parsed = urlsplit(normalized)
    if parsed.scheme not in {"http", "https"} or not parsed.netloc:
        raise ValueError("base URL must be an absolute http:// or https:// URL")
    if parsed.username is not None or parsed.password is not None:
        raise ValueError("base URL must not contain userinfo")
    if parsed.query or parsed.fragment:
        raise ValueError("base URL must not contain a query string or fragment")
    return normalized


def validate_same_origin_url(candidate: str, base_url: str) -> str | None:
    parsed = urlsplit(candidate)
    base = urlsplit(base_url)
    if parsed.scheme not in {"http", "https"} or not parsed.netloc:
        return "redirect target must be an absolute HTTP(S) URL"
    if parsed.username is not None or parsed.password is not None:
        return "redirect target must not contain userinfo"
    if parsed.scheme != base.scheme:
        if base.scheme == "https" and parsed.scheme == "http":
            return "redirect target must not downgrade HTTPS to HTTP"
        return "redirect target must keep the configured origin scheme"
    try:
        parsed_port = parsed.port
        base_port = base.port
    except ValueError:
        return "redirect target contains an invalid port"
    if parsed.hostname != base.hostname or parsed_port != base_port:
        return "redirect target must remain on the configured origin"
    if parsed.fragment:
        return "redirect target must not contain a fragment"
    return None


def expected_identity_checks(spec: RouteSpec) -> list[dict[str, Any]]:
    content_type = "application/json" if spec.identity_kind == "health_json" else "text/html"
    checks: list[dict[str, Any]] = [
        {
            "id": "content_type",
            "operator": "prefix",
            "expected": content_type,
        },
        {
            "id": "body_nonempty",
            "operator": "equals",
            "expected": True,
        },
    ]
    checks.extend(
        {
            "id": f"attribute:{name}",
            "operator": "equals",
            "expected": value,
        }
        for name, value in spec.expected_attributes
    )
    checks.extend(
        {
            "id": f"text:{check_id}",
            "operator": "equals",
            "expected": True,
        }
        for check_id, _marker in spec.expected_text
    )
    checks.extend(
        {
            "id": f"json:{field}",
            "operator": "equals",
            "expected": value,
        }
        for field, value in spec.expected_json_fields
    )
    return checks


def evaluate_response_identity(
    spec: RouteSpec,
    content_type: str,
    body: bytes,
) -> dict[str, Any]:
    text = body.decode("utf-8-sig", errors="replace")
    actual_by_id: dict[str, Any] = {
        "content_type": content_type,
        "body_nonempty": bool(body),
    }
    for name, _expected in spec.expected_attributes:
        match = re.search(rf'\b{re.escape(name)}="([^"]*)"', text)
        actual_by_id[f"attribute:{name}"] = match.group(1) if match else None
    for check_id, marker in spec.expected_text:
        actual_by_id[f"text:{check_id}"] = marker in text

    parsed_json: dict[str, Any] = {}
    if spec.expected_json_fields:
        try:
            loaded = json.loads(text)
            if isinstance(loaded, dict):
                parsed_json = loaded
        except json.JSONDecodeError:
            pass
    for field, _expected in spec.expected_json_fields:
        actual_by_id[f"json:{field}"] = parsed_json.get(field)

    checks: list[dict[str, Any]] = []
    for expected_check in expected_identity_checks(spec):
        actual = actual_by_id.get(expected_check["id"])
        if expected_check["operator"] == "prefix":
            passed = isinstance(actual, str) and actual.lower().startswith(
                str(expected_check["expected"]).lower()
            )
        else:
            passed = actual == expected_check["expected"]
        checks.append({**expected_check, "actual": actual, "passed": passed})

    return {
        "kind": spec.identity_kind,
        "passed": all(check["passed"] for check in checks),
        "checks": checks,
    }


def derived_claims(
    successful_routes: set[str], requirements: dict[str, set[str]]
) -> list[str]:
    return sorted(
        claim for claim, required_routes in requirements.items() if required_routes <= successful_routes
    )


def validate_identity_record(identity: Any, spec: RouteSpec) -> list[str]:
    reasons: list[str] = []
    if not isinstance(identity, dict):
        return ["response_identity must be an object"]
    if identity.get("kind") != spec.identity_kind:
        reasons.append(
            f"response_identity kind mismatch: expected {spec.identity_kind!r}, got {identity.get('kind')!r}"
        )
    checks = identity.get("checks")
    if not isinstance(checks, list):
        return reasons + ["response_identity checks must be a list"]
    expected_checks = {check["id"]: check for check in expected_identity_checks(spec)}
    actual_checks = {
        str(check.get("id") or ""): check for check in checks if isinstance(check, dict)
    }
    if set(actual_checks) != set(expected_checks):
        reasons.append(
            "response_identity check IDs mismatch: expected "
            f"{sorted(expected_checks)!r}, got {sorted(actual_checks)!r}"
        )
    for check_id, expected in expected_checks.items():
        actual_check = actual_checks.get(check_id)
        if actual_check is None:
            continue
        if actual_check.get("operator") != expected["operator"]:
            reasons.append(f"identity check {check_id!r} operator mismatch")
        if actual_check.get("expected") != expected["expected"]:
            reasons.append(f"identity check {check_id!r} expected value mismatch")
        actual = actual_check.get("actual")
        recalculated = (
            isinstance(actual, str)
            and actual.lower().startswith(str(expected["expected"]).lower())
            if expected["operator"] == "prefix"
            else actual == expected["expected"]
        )
        if actual_check.get("passed") is not recalculated:
            reasons.append(f"identity check {check_id!r} pass result is inconsistent")
    recalculated_identity = not reasons and all(
        bool(check.get("passed")) for check in actual_checks.values()
    )
    if identity.get("passed") is not recalculated_identity:
        reasons.append("response_identity passed result is inconsistent")
    return reasons


def validate_probe_record(probe: Any, spec: RouteSpec, base_url: str) -> list[str]:
    if not isinstance(probe, dict):
        return ["probe must be an object"]
    reasons: list[str] = []
    expected_url = base_url.rstrip("/") + spec.route
    if probe.get("route") != spec.route:
        reasons.append(f"route mismatch: expected {spec.route!r}, got {probe.get('route')!r}")
    if probe.get("url") != expected_url:
        reasons.append(f"requested URL mismatch: expected {expected_url!r}, got {probe.get('url')!r}")
    if probe.get("checked") is not True:
        reasons.append("checked must be true")

    chain = probe.get("redirect_chain")
    if not isinstance(chain, list):
        chain = []
        reasons.append("redirect_chain must be a list")
    if probe.get("redirect_count") != len(chain):
        reasons.append("redirect_count does not match redirect_chain length")
    if not spec.min_redirects <= len(chain) <= spec.max_redirects:
        reasons.append(
            f"redirect count must be between {spec.min_redirects} and {spec.max_redirects}, got {len(chain)}"
        )

    current_url = expected_url
    for index, hop in enumerate(chain):
        if not isinstance(hop, dict):
            reasons.append(f"redirect_chain[{index}] must be an object")
            continue
        if hop.get("from_url") != current_url:
            reasons.append(f"redirect_chain[{index}] from_url breaks chain continuity")
        if hop.get("http_status") not in REDIRECT_STATUSES:
            reasons.append(f"redirect_chain[{index}] has a non-redirect HTTP status")
        location = str(hop.get("location") or "")
        expected_target = urljoin(current_url, location)
        target = str(hop.get("to_url") or "")
        if target != expected_target:
            reasons.append(f"redirect_chain[{index}] target does not match Location")
        target_error = validate_same_origin_url(target, base_url)
        if target_error:
            reasons.append(f"redirect_chain[{index}] {target_error}")
        current_url = target

    final_url = str(probe.get("final_url") or "")
    if final_url != current_url:
        reasons.append("final_url does not match the end of the redirect chain")
    final_url_error = validate_same_origin_url(final_url, base_url)
    if final_url_error:
        reasons.append(final_url_error)
    observed_path_query = path_query(final_url) if final_url else ""
    if probe.get("final_path_query") != observed_path_query:
        reasons.append("final_path_query does not match final_url")
    if probe.get("expected_final_path_query") != spec.expected_final_path_query:
        reasons.append("expected_final_path_query does not match the route contract")
    if observed_path_query != spec.expected_final_path_query:
        reasons.append(
            f"unexpected final route identity: expected {spec.expected_final_path_query!r}, got {observed_path_query!r}"
        )

    initial_status = probe.get("http_status")
    expected_initial_status = chain[0].get("http_status") if chain and isinstance(chain[0], dict) else probe.get("final_http_status")
    if initial_status != expected_initial_status:
        reasons.append("http_status does not match the initial response")
    final_status = probe.get("final_http_status")
    if not isinstance(final_status, int) or isinstance(final_status, bool) or final_status != 200:
        reasons.append(f"final HTTP status must be 200, got {final_status!r}")

    content_type = probe.get("response_content_type")
    if not isinstance(content_type, str) or not content_type:
        reasons.append("response_content_type is missing")
    body_bytes = probe.get("response_body_bytes")
    if not isinstance(body_bytes, int) or isinstance(body_bytes, bool) or body_bytes <= 0:
        reasons.append("response_body_bytes must be a positive integer")
    body_sha256 = str(probe.get("response_body_sha256") or "")
    if re.fullmatch(r"[0-9a-f]{64}", body_sha256) is None:
        reasons.append("response_body_sha256 must be a lowercase SHA-256 digest")
    identity = probe.get("response_identity")
    reasons.extend(validate_identity_record(identity, spec))
    if not isinstance(identity, dict) or identity.get("passed") is not True:
        reasons.append("response_identity must pass every route-specific check")

    recorded_errors = probe.get("errors")
    if not isinstance(recorded_errors, list):
        reasons.append("errors must be a list")
        recorded_errors = []
    semantic_pass = not reasons and not recorded_errors
    if probe.get("ok") is not semantic_pass:
        reasons.append("ok does not match the revalidated route result")
    if str(probe.get("error") or "") != "; ".join(str(item) for item in recorded_errors):
        reasons.append("error does not match errors")
    return reasons
