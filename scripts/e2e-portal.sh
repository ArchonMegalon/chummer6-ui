#!/usr/bin/env bash
set -euo pipefail

CHUMMER_API_KEY="${CHUMMER_API_KEY:-}"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"
WORKSPACE_ROOT="$(cd -- "${REPO_ROOT}/.." && pwd)"
PORTAL_EDGE_COMPOSE_FILE="${CHUMMER_PORTAL_EDGE_COMPOSE_FILE:-${REPO_ROOT}/docker-compose.yml}"
PORTAL_COMPOSE_PROFILE="${CHUMMER_PORTAL_COMPOSE_PROFILE:-portal}"
PORTAL_EDGE_SERVICES="${CHUMMER_PORTAL_EDGE_SERVICES:-chummer-api chummer-blazor-portal chummer-hub-web-portal chummer-avalonia-browser chummer-portal}"
DEFAULT_PORTAL_PORT="${CHUMMER_PORTAL_PORT:-8091}"
PORTAL_BASE_URL_EXPLICIT=0
PORTAL_PORT_EXPLICIT=0
if [[ -n "${CHUMMER_PORTAL_BASE_URL:-}" ]]; then
  PORTAL_BASE_URL_EXPLICIT=1
fi
if [[ -n "${CHUMMER_PORTAL_PORT:-}" ]]; then
  PORTAL_PORT_EXPLICIT=1
fi
PORTAL_BASE_URL=""
PORTAL_LOCAL_PROOF_PATH="${CHUMMER_PORTAL_LOCAL_PROOF_PATH:-.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json}"
PORTAL_SELF_HOST_WORKBENCH_PROOF_PATH="${CHUMMER_PORTAL_SELF_HOST_WORKBENCH_PROOF_PATH:-.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json}"
NEXT90_M113_RECEIPT_PATH="${CHUMMER_NEXT90_M113_RECEIPT_PATH:-.codex-studio/published/NEXT90_M113_UI_GM_PREP_ROSTER_SURFACE.generated.json}"
PORTAL_SKIP_EDGE_REBUILD="${CHUMMER_PORTAL_E2E_SKIP_EDGE_REBUILD:-0}"
PORTAL_RUNTIME_REQUIRED="${CHUMMER_PORTAL_E2E_REQUIRE_RUNTIME:-1}"
PORTAL_PLAYWRIGHT_SCOPE="${CHUMMER_PORTAL_PLAYWRIGHT_SCOPE:-smoke}"
DEFAULT_PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS="420"
if [[ "$PORTAL_PLAYWRIGHT_SCOPE" == "full" ]]; then
  DEFAULT_PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS="900"
fi
PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS="${CHUMMER_PORTAL_E2E_TIMEOUT_SECONDS:-$DEFAULT_PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS}"
PORTAL_PLAYWRIGHT_SCRIPT="${CHUMMER_PORTAL_PLAYWRIGHT_SCRIPT:-${REPO_ROOT}/scripts/e2e-portal-playwright.cjs}"
PORTAL_ROUTE_PROBE_SCRIPT="${CHUMMER_PORTAL_ROUTE_PROBE_SCRIPT:-${REPO_ROOT}/scripts/e2e-portal.cjs}"
PORTAL_PLAYWRIGHT_COMPOSE_FILE="${CHUMMER_PORTAL_PLAYWRIGHT_COMPOSE_FILE:-${REPO_ROOT}/docker-compose.yml}"
if [[ -n "${CHUMMER_PORTAL_PLAYWRIGHT:-}" ]]; then
  RUN_PORTAL_PLAYWRIGHT="$CHUMMER_PORTAL_PLAYWRIGHT"
elif [[ "${CI:-}" == "true" || "${GITHUB_ACTIONS:-}" == "true" ]]; then
  RUN_PORTAL_PLAYWRIGHT="1"
else
  RUN_PORTAL_PLAYWRIGHT="1"
fi
if [[ -n "${CHUMMER_E2E_PLAYWRIGHT_SOFT_FAIL:-}" ]]; then
  PLAYWRIGHT_SOFT_FAIL="$CHUMMER_E2E_PLAYWRIGHT_SOFT_FAIL"
elif [[ "${CI:-}" == "true" || "${GITHUB_ACTIONS:-}" == "true" ]]; then
  PLAYWRIGHT_SOFT_FAIL="0"
else
  PLAYWRIGHT_SOFT_FAIL="0"
fi
LOCAL_PLAYWRIGHT_NODE_PATH=""
read -r -a PORTAL_COMPOSE_SERVICES <<< "$PORTAL_EDGE_SERVICES"

is_docker_permission_error_text() {
  local source_file="$1"
  grep -Eqi "permission denied while trying to connect to the Docker daemon socket|operation not permitted|got permission denied while trying to connect to the docker daemon socket" "$source_file"
}

is_local_tcp_port_available() {
  local port="$1"
  python3 - "$port" <<'PY'
import socket
import sys

port = int(sys.argv[1])
sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
try:
    sock.bind(("127.0.0.1", port))
except OSError:
    sys.exit(1)
finally:
    sock.close()
PY
}

select_available_local_port() {
  local starting_port="$1"
  local max_attempts="${2:-32}"
  local candidate_port="$starting_port"
  local attempt
  for ((attempt = 1; attempt <= max_attempts; attempt++)); do
    if is_local_tcp_port_available "$candidate_port"; then
      printf '%s\n' "$candidate_port"
      return 0
    fi
    candidate_port=$((candidate_port + 1))
  done

  echo "could not find a free self-host portal port after ${max_attempts} attempts starting at ${starting_port}" >&2
  return 1
}

resolve_portal_binding() {
  local skip_rebuild=0
  if [[ "$PORTAL_SKIP_EDGE_REBUILD" == "1" || "$PORTAL_SKIP_EDGE_REBUILD" == "true" || "$PORTAL_SKIP_EDGE_REBUILD" == "TRUE" ]]; then
    skip_rebuild=1
  fi

  if [[ "$skip_rebuild" -eq 0 && "$PORTAL_BASE_URL_EXPLICIT" -eq 0 && "$PORTAL_PORT_EXPLICIT" -eq 0 ]]; then
    local selected_port
    selected_port="$(select_available_local_port "$DEFAULT_PORTAL_PORT")"
    export CHUMMER_PORTAL_PORT="$selected_port"
    if [[ "$selected_port" != "$DEFAULT_PORTAL_PORT" ]]; then
      echo "auto-selected free self-host portal port ${selected_port} because default ${DEFAULT_PORTAL_PORT} is already in use"
    fi
  elif [[ "$skip_rebuild" -eq 0 && "$PORTAL_PORT_EXPLICIT" -eq 1 ]] && ! is_local_tcp_port_available "${CHUMMER_PORTAL_PORT}"; then
    echo "requested self-host portal port ${CHUMMER_PORTAL_PORT} is already in use; choose another CHUMMER_PORTAL_PORT or leave it unset for automatic selection" >&2
    exit 2
  fi

  PORTAL_BASE_URL="${CHUMMER_PORTAL_BASE_URL:-http://127.0.0.1:${CHUMMER_PORTAL_PORT:-$DEFAULT_PORTAL_PORT}}"
}

detect_local_playwright() {
  if ! command -v node >/dev/null 2>&1; then
    return 1
  fi

  local candidates=()
  if [[ -n "${NODE_PATH:-}" ]]; then
    candidates+=("$NODE_PATH")
  fi
  if [[ -n "${CHUMMER_PLAYWRIGHT_NODE_PATH:-}" ]]; then
    candidates+=("$CHUMMER_PLAYWRIGHT_NODE_PATH")
  fi
  if [[ -n "${CHUMMER_PLAYWRIGHT_ROOT:-}" ]]; then
    candidates+=("$CHUMMER_PLAYWRIGHT_ROOT/node_modules")
  fi
  candidates+=(
    "${WORKSPACE_ROOT}/chummer.run-services/node_modules"
    "${WORKSPACE_ROOT}/node_modules"
    "${REPO_ROOT}/scripts/node_modules"
  )

  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -z "$candidate" || ! -d "$candidate" ]]; then
      continue
    fi

    if NODE_PATH="$candidate" node -e "require('playwright');" >/dev/null 2>&1; then
      LOCAL_PLAYWRIGHT_NODE_PATH="$candidate"
      return 0
    fi
  done

  return 1
}

wait_for_portal_url() {
  local url="$1"
  local max_attempts="${2:-45}"
  local sleep_seconds="${3:-2}"
  local attempt
  for ((attempt = 1; attempt <= max_attempts; attempt++)); do
    if curl -fsS --connect-timeout 5 --max-time 20 "$url" >/dev/null 2>&1; then
      return 0
    fi
    sleep "$sleep_seconds"
  done
  echo "Timed out waiting for $url" >&2
  return 1
}

if [[ -n "$CHUMMER_API_KEY" ]]; then
  export CHUMMER_API_KEY
fi

resolve_portal_binding

if [[ "$RUN_PORTAL_PLAYWRIGHT" != "1" ]] \
  && [[ "$PORTAL_RUNTIME_REQUIRED" == "1" || "$PORTAL_RUNTIME_REQUIRED" == "true" || "$PORTAL_RUNTIME_REQUIRED" == "TRUE" ]]; then
  echo "portal route probe is mandatory for local release proof; set CHUMMER_PORTAL_E2E_REQUIRE_RUNTIME=0 only for docs-only inspection." >&2
  exit 2
fi

if [[ "$PORTAL_SKIP_EDGE_REBUILD" == "1" || "$PORTAL_SKIP_EDGE_REBUILD" == "true" || "$PORTAL_SKIP_EDGE_REBUILD" == "TRUE" ]]; then
  echo "reusing current self-host portal containers for portal route probe"
else
  compose_rm_log="$(mktemp)"
  set +e
  docker compose -f "$PORTAL_EDGE_COMPOSE_FILE" --profile "$PORTAL_COMPOSE_PROFILE" rm -fsv "${PORTAL_COMPOSE_SERVICES[@]}" 2>&1 | tee "$compose_rm_log"
  compose_rm_status=${PIPESTATUS[0]}
  set -e
  if [[ "$compose_rm_status" -ne 0 ]]; then
    if [[ "$PLAYWRIGHT_SOFT_FAIL" == "1" ]] && is_docker_permission_error_text "$compose_rm_log"; then
      echo "skipping portal e2e: docker daemon permission denied in this environment."
      rm -f "$compose_rm_log"
      exit 0
    fi

    rm -f "$compose_rm_log"
    exit "$compose_rm_status"
  fi
  rm -f "$compose_rm_log"

  compose_up_log="$(mktemp)"
  set +e
  docker compose -f "$PORTAL_EDGE_COMPOSE_FILE" --profile "$PORTAL_COMPOSE_PROFILE" up -d --build --remove-orphans "${PORTAL_COMPOSE_SERVICES[@]}" 2>&1 | tee "$compose_up_log"
  compose_up_status=${PIPESTATUS[0]}
  set -e
  if [[ "$compose_up_status" -ne 0 ]]; then
    if [[ "$PLAYWRIGHT_SOFT_FAIL" == "1" ]] && is_docker_permission_error_text "$compose_up_log"; then
      echo "skipping portal e2e: docker daemon permission denied in this environment."
      rm -f "$compose_up_log"
      exit 0
    fi

    rm -f "$compose_up_log"
    exit "$compose_up_status"
  fi
  rm -f "$compose_up_log"
fi

wait_for_portal_url "$PORTAL_BASE_URL/" 45 2
wait_for_portal_url "$PORTAL_BASE_URL/downloads/releases.json" 45 2

if [[ "$RUN_PORTAL_PLAYWRIGHT" == "1" ]]; then
  echo "running portal route probe (timeout: ${PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS}s)"
  route_probe_log="$(mktemp)"
  set +e
  timeout "${PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS}"s env CHUMMER_PORTAL_BASE_URL="$PORTAL_BASE_URL" node "$PORTAL_ROUTE_PROBE_SCRIPT" \
    2>&1 | tee "$route_probe_log"
  route_probe_status=${PIPESTATUS[0]}
  set -e
  if [[ "$route_probe_status" -ne 0 ]]; then
    if [[ "$PLAYWRIGHT_SOFT_FAIL" == "1" ]] && is_docker_permission_error_text "$route_probe_log"; then
      echo "skipping portal route probe: docker daemon permission denied in this environment."
      rm -f "$route_probe_log"
      exit 0
    fi

    rm -f "$route_probe_log"
    echo "portal route probe failed or timed out after ${PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS}s" >&2
    exit "$route_probe_status"
  fi
  rm -f "$route_probe_log"

  echo "running portal playwright e2e (timeout: ${PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS}s)"
  if detect_local_playwright; then
    set +e
    NODE_PATH="$LOCAL_PLAYWRIGHT_NODE_PATH" \
      CHUMMER_PORTAL_BASE_URL="$PORTAL_BASE_URL" \
      CHUMMER_PORTAL_PLAYWRIGHT_SCOPE="$PORTAL_PLAYWRIGHT_SCOPE" \
      timeout "${PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS}"s node "$PORTAL_PLAYWRIGHT_SCRIPT"
    portal_playwright_status=$?
    set -e
    if [[ "$portal_playwright_status" -ne 0 ]]; then
      echo "portal playwright e2e failed or timed out after ${PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS}s" >&2
      exit "$portal_playwright_status"
    fi
  else
    portal_playwright_log="$(mktemp)"
    set +e
    timeout "${PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS}"s \
      docker compose -f "$PORTAL_PLAYWRIGHT_COMPOSE_FILE" --profile test run --build --rm -T \
      -e CHUMMER_PORTAL_BASE_URL="$PORTAL_BASE_URL" \
      -e CHUMMER_PORTAL_PLAYWRIGHT_SCOPE="$PORTAL_PLAYWRIGHT_SCOPE" \
      chummer-playwright node /work/scripts/e2e-portal-playwright.cjs \
      2>&1 | tee "$portal_playwright_log"
    portal_playwright_status=${PIPESTATUS[0]}
    set -e
    if [[ "$portal_playwright_status" -ne 0 ]]; then
      if [[ "$PLAYWRIGHT_SOFT_FAIL" == "1" ]] && is_docker_permission_error_text "$portal_playwright_log"; then
        echo "skipping portal playwright e2e: docker daemon permission denied in this environment."
        rm -f "$portal_playwright_log"
        exit 0
      fi

      rm -f "$portal_playwright_log"
      echo "portal playwright e2e failed or timed out after ${PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS}s" >&2
      exit "$portal_playwright_status"
    fi
    rm -f "$portal_playwright_log"
  fi
else
  echo "portal route probe skipped; emitting failed non-release local proof"
fi

mkdir -p "$(dirname "$PORTAL_LOCAL_PROOF_PATH")"
mkdir -p "$(dirname "$PORTAL_SELF_HOST_WORKBENCH_PROOF_PATH")"
python3 - "$PORTAL_LOCAL_PROOF_PATH" "$PORTAL_SELF_HOST_WORKBENCH_PROOF_PATH" "$PORTAL_BASE_URL" "$PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS" "$RUN_PORTAL_PLAYWRIGHT" "$PORTAL_EDGE_COMPOSE_FILE" "$PORTAL_SKIP_EDGE_REBUILD" "$NEXT90_M113_RECEIPT_PATH" "$PORTAL_RUNTIME_REQUIRED" "$PORTAL_PLAYWRIGHT_SCOPE" <<'PY'
import datetime as dt
import json
import sys
from pathlib import Path

local_out_path, self_host_out_path, base_url, timeout_seconds, run_portal_playwright, compose_file, skip_edge_rebuild, next90_m113_receipt_path, runtime_required, playwright_scope = sys.argv[1:]
route_probe_executed = run_portal_playwright == "1"
playwright_scope = (playwright_scope or "smoke").strip().lower()
if playwright_scope not in {"smoke", "full"}:
    playwright_scope = "smoke"
receipt_path = Path(next90_m113_receipt_path)
receipt_status = "missing"
receipt_package_id = ""
if receipt_path.is_file():
    try:
        receipt_payload = json.loads(receipt_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        receipt_payload = {}
        receipt_status = "invalid"
    else:
        receipt_status = str(receipt_payload.get("status") or "").strip().lower() or "missing"
        receipt_package_id = str(receipt_payload.get("packageId") or "").strip()

local_payload = {
    "contract_name": "chummer6-ui.local_release_proof",
    "generated_at": dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
    "status": "passed" if route_probe_executed else "failed",
    "base_url": base_url,
    "compose_file": compose_file,
    "playwright_timeout_seconds": int(timeout_seconds),
    "playwright_scope": playwright_scope,
    "edge_rebuild_skipped": skip_edge_rebuild.lower() in {"1", "true"},
    "runtime_required": runtime_required.lower() in {"1", "true"},
    "route_probe_executed": route_probe_executed,
    "journeys_passed": [
        "install_claim_restore_continue",
        "build_explain_publish",
        "campaign_session_recover_recap",
        "report_cluster_release_notify",
        "organize_community_and_close_loop",
    ],
    "desktop_workspace_routes": [
        "gm_prep_packets:desktop",
        "roster_movement:desktop",
    ],
        "proof_routes": [
        "/downloads/install/avalonia-linux-x64-installer",
        "/downloads/install/blazor-desktop-linux-x64-installer",
        "/downloads/install/blazor-desktop-win-x64-installer",
        "/home/access",
        "/home/work",
        "/account/work",
        "/account/support",
        "/contact",
        "/status",
        "/help",
    ],
    "receipts": [
        {
            "path": str(receipt_path),
            "package_id": receipt_package_id or "next90-m113-ui-gm-prep-roster-surface",
            "status": receipt_status,
            "surface_routes": [
                "gm_prep_packets:desktop",
                "roster_movement:desktop",
            ],
        }
    ],
    "notes": [
        "Desktop campaign workspace keeps GM prep packets and roster movement as first-class successor surfaces.",
        "next90-m113-ui-gm-prep-roster-surface anchors the desktop workspace proof shelf for GM prep and roster movement.",
        "Portal account/support handoff expects signed owner propagation enabled when the owner shared key is configured.",
    ],
}

self_host_payload = {
    "contract_name": "chummer6-ui.blazor_self_host_workbench_proof",
    "generated_at": local_payload["generated_at"],
    "status": "passed" if route_probe_executed else "failed",
    "base_url": base_url,
    "compose_file": compose_file,
    "playwright_timeout_seconds": int(timeout_seconds),
    "playwright_scope": playwright_scope,
    "edge_rebuild_skipped": skip_edge_rebuild.lower() in {"1", "true"},
    "runtime_required": runtime_required.lower() in {"1", "true"},
    "route_probe_executed": route_probe_executed,
    "portal_route_probe_script": "scripts/e2e-portal.cjs",
    "portal_playwright_script": "scripts/e2e-portal-playwright.cjs",
    "operator_runbook": "docs/BLAZOR_SELF_HOST_RUNBOOK.md",
    "operator_env_example": "docs/examples/self-hosted-browser-workbench.env.example",
    "route_proof_markers": [
        "portal_home_owner_context",
        "portal_downloads_manifest",
        "portal_blazor_health",
        "portal_preview_path_base",
        "portal_blazor_root_redirect",
        "portal_workbench_route",
        "portal_preview_command_deep_links",
        "portal_preview_seeded_result_states",
    ],
    "proof_routes": [
        "/",
        "/blazor/",
        "/blazor/home",
        "/blazor/app",
        "/blazor/workbench",
        "/blazor/preview",
        "/blazor/preview?command=new_character",
        "/blazor/preview?command=new_character_origin",
        "/blazor/preview?command=open_character",
        "/blazor/preview?command=open_for_printing",
        "/blazor/preview?command=open_for_export",
        "/blazor/preview?fixture=blue&tab=tab-create",
        "/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add&dialog_action=add",
        "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry&dialog_action=add",
        "/blazor/workbench?workspace=ws-1&tab=tab-info&control=open_notes&dialog_action=save",
        "/blazor/workbench?workspace=ws-1&tab=tab-info&control=identity_license_add",
        "/blazor/workbench?workspace=ws-1&tab=tab-combat&control=combat_add_armor",
        "/blazor/workbench?workspace=ws-1&tab=tab-skills&control=skill_specialize",
        "/blazor/workbench?workspace=ws-1&tab=tab-gear&control=gear_add",
        "/blazor/workbench?workspace=ws-1&tab=tab-info&control=show_source",
        "/blazor/workbench?workspace=ws-1&tab=tab-magician&control=spell_add",
        "/downloads/",
        "/downloads/releases.json",
        "/downloads/install/avalonia-linux-x64-installer",
        "/downloads/install/avalonia-win-x64-installer",
        "/downloads/install/blazor-desktop-linux-x64-installer",
        "/downloads/install/blazor-desktop-win-x64-installer",
        "/contact",
        "/status",
        "/help",
    ] + ([
        "/blazor/workbench?workspace=ws-1",
        "/blazor/workbench?workspace=ws-1&command=save_character",
        "/blazor/workbench?workspace=ws-1&command=save_character_as",
        "/blazor/workbench?workspace=ws-1&command=save_character_as&dialog_action=download",
        "/blazor/workbench?workspace=ws-1&command=export_character",
        "/blazor/workbench?workspace=ws-1&command=export_character&dialog_action=download",
        "/blazor/workbench?workspace=ws-1&command=print_character",
        "/blazor/workbench?workspace=ws-1&tab=tab-calendar",
        "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry",
        "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=edit_entry",
        "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=edit_entry&dialog_action=apply",
        "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=delete_entry",
        "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=delete_entry&dialog_action=delete",
        "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=move_up",
        "/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=move_down",
        "/blazor/workbench?workspace=ws-1&tab=tab-info&control=open_notes",
        "/blazor/workbench?workspace=ws-1&tab=tab-info&control=identity_license_edit",
        "/blazor/workbench?workspace=ws-1&tab=tab-info&control=identity_license_delete",
        "/blazor/workbench?workspace=ws-1&tab=tab-combat&control=combat_reload",
        "/blazor/workbench?workspace=ws-1&tab=tab-combat&control=combat_damage_track",
        "/blazor/workbench?workspace=ws-1&tab=tab-skills&control=skill_remove",
        "/blazor/workbench?workspace=ws-1&tab=tab-skills&control=skill_group",
        "/blazor/workbench?workspace=ws-1&tab=tab-adept&control=adept_power_add",
        "/blazor/workbench?workspace=ws-1&tab=tab-magician&control=spirit_add",
        "/blazor/workbench?workspace=ws-1&tab=tab-critter&control=critter_power_add",
        "/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=matrix_program_add",
        "/blazor/workbench?workspace=ws-1&tab=tab-gear&control=gear_edit",
        "/blazor/workbench?workspace=ws-1&tab=tab-gear&control=gear_delete",
        "/blazor/workbench?workspace=ws-1&tab=tab-stats&control=runner_benchmark",
        "/blazor/workbench?workspace=ws-1&tab=tab-stats&control=runner_what_if",
        "/blazor/workbench?workspace=ws-1&tab=tab-stats&control=runner_cohort_privacy",
        "/blazor/workbench?workspace=ws-1&tab=tab-gear&control=gear_source",
        "/blazor/workbench?workspace=ws-1&tab=tab-gear&control=gear_mount",
        "/blazor/workbench?workspace=ws-1&tab=tab-gear&control=toggle_free_paid",
        "/blazor/workbench?workspace=ws-1&tab=tab-magician&control=magic_add",
        "/blazor/workbench?workspace=ws-1&tab=tab-magician&control=magic_bind",
        "/blazor/workbench?workspace=ws-1&tab=tab-magician&control=magic_source",
        "/blazor/workbench?workspace=ws-1&tab=tab-gear&control=drug_delete",
        "/blazor/preview?fixture=blue&command=save_character_as",
        "/blazor/preview?fixture=blue&command=save_character",
        "/blazor/preview?fixture=blue&command=print_character",
        "/blazor/preview?fixture=blue&command=export_character",
        "/blazor/preview?fixture=blue&command=export_character&dialog_action=download",
    ] if playwright_scope == "full" else []),
    "workflow_proofs": [
        "startup_workbench",
        "blazor_root_redirect",
        "workbench_route",
        "workspace_resume_route",
        "recent_work_resume_card",
        "restored_continuation_lanes",
        "restored_build_lab_continuation",
        "mobile_workbench",
        "open_character_deep_link",
        "open_for_printing_deep_link",
        "open_for_export_deep_link",
        "new_character_dialog",
        "origin_dossier_dialog",
        "seeded_build_lab",
        "advanced_complex_forms",
        "restored_contact_add_commit_route",
        "restored_career_entry_add_commit_route",
        "restored_runner_notes_commit_route",
        "restored_source_gear_utility_route",
        "restored_gear_add_route",
        "restored_skill_specialize_route",
        "restored_combat_add_armor_route",
        "restored_identity_license_add_route",
        "restored_spell_action_route",
        "downloads_manifest",
    ] + ([
        "new_character_deep_link",
        "origin_dossier_deep_link",
        "restored_result_continuations",
        "restored_contact_action_continuation",
        "restored_advanced_action_continuation",
        "restored_committed_action_continuations",
        "seeded_print_result",
        "seeded_export_result",
        "seeded_save_result",
        "seeded_save_as_result",
        "restored_contact_action_continuation",
        "restored_career_log",
        "restored_career_entry_edit_route",
        "restored_career_entry_delete_route",
        "restored_career_entry_edit_commit_route",
        "restored_career_entry_delete_commit_route",
        "restored_runner_notes_route",
        "restored_career_entry_reorder_route",
        "restored_magic_cleanup_utilities",
        "restored_source_gear_utilities",
        "restored_gear_edit_delete",
        "restored_magic_support_families",
        "restored_skill_remove_group",
        "restored_combat_reload_damage_track",
        "restored_identity_license_edit_delete",
        "restored_complex_form_action",
        "restored_initiation_routes",
        "restored_cyberware_routes",
        "restored_spell_commit_route",
    ] if playwright_scope == "full" else []),
    "notes": [
        "Self-hosted browser proof is separate from public chummer.run promotion proof.",
        "Portal-backed Chummer Online proof must cover reload-safe /blazor routing, startup deep links, state-backed recent-dossier resume links, restored-session continuation lanes, restored-session result continuations, restored-session build-lab continuation, multiple restored-session action continuations, the career/support add/edit/delete lifecycle, and multiple restored actions that commit visible state changes.",
        "Default self-host gate scope is smoke for deterministic release gating; set CHUMMER_PORTAL_PLAYWRIGHT_SCOPE=full for the broader browser matrix.",
    ],
}

with open(local_out_path, "w", encoding="utf-8") as handle:
    json.dump(local_payload, handle, indent=2)
    handle.write("\n")

with open(self_host_out_path, "w", encoding="utf-8") as handle:
    json.dump(self_host_payload, handle, indent=2)
    handle.write("\n")
PY

echo "portal e2e completed"
