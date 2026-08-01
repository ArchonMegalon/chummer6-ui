#!/usr/bin/env bash
set -euo pipefail

repo_root_physical="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
repo_root_alias_candidate="${CHUMMER_UI_REPO_ROOT_ALIAS:-/docker/chummercomplete/chummer6-ui}"
repo_root="$repo_root_physical"
if [[ -n "$repo_root_alias_candidate" && -d "$repo_root_alias_candidate" ]]; then
  alias_physical="$(cd "$repo_root_alias_candidate" && pwd -P)"
  if [[ "$alias_physical" == "$repo_root_physical" ]]; then
    repo_root="$(cd -L "$repo_root_alias_candidate" && pwd -L)"
  fi
fi
cd "$repo_root"

output_dir="$repo_root/Chummer.Avalonia/bin/Release/net10.0"
sample_path="$output_dir/Samples/Legacy/Soma-Career.chum5"
receipt_path="$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json"
screenshot_dir="$repo_root/.codex-studio/published/ui-flagship-release-gate-screenshots"
lock_dir="$repo_root/.codex-studio/locks/b14-flagship-ui-release-gate.lock"
lock_owner_pid_path="$lock_dir/owner.pid"
lock_stale_max_age_seconds="${CHUMMER_FLAGSHIP_UI_RELEASE_GATE_LOCK_STALE_MAX_AGE_SECONDS:-300}"
capture_screenshot_dir="$(mktemp -d "${TMPDIR:-/tmp}/chummer-ui-flagship-gate-screenshots.XXXXXX")"
mkdir -p "$(dirname "$screenshot_dir")"
staged_screenshot_dir="$(mktemp -d "$(dirname "$screenshot_dir")/.ui-flagship-screenshot-stage.XXXXXX")"
screenshot_pack_transaction_path="$(dirname "$screenshot_dir")/.ui-flagship-screenshot-transaction.json"
api_runtime_log_path="$(mktemp "${TMPDIR:-/tmp}/chummer-ui-flagship-api.XXXXXX.log")"
signoff_path="$repo_root/docs/WORKBENCH_RELEASE_SIGNOFF.md"
avalonia_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
dual_head_tests_path="$repo_root/Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs"
blazor_shell_tests_path="$repo_root/Chummer.Tests/Presentation/BlazorShellComponentTests.cs"
desktop_update_runtime_tests_path="$repo_root/Chummer.Tests/DesktopUpdateRuntimeTests.cs"
desktop_install_linking_runtime_tests_path="$repo_root/Chummer.Tests/DesktopInstallLinkingRuntimeTests.cs"
desktop_startup_smoke_runtime_tests_path="$repo_root/Chummer.Tests/DesktopStartupSmokeRuntimeTests.cs"
workflow_parity_receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
sr4_workflow_parity_receipt_path="$repo_root/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
sr6_workflow_parity_receipt_path="$repo_root/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json"
sr6_ruleset_ui_sophistication_receipt_path="$repo_root/.codex-studio/published/CHUMMER_SR6_RULESET_UI_SOPHISTICATION_GATE.generated.json"
sr4_sr6_frontier_receipt_path="$repo_root/.codex-studio/published/SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json"
desktop_workflow_execution_receipt_path="$repo_root/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"
localization_release_gate_receipt_path="$repo_root/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"
interactive_control_inventory_receipt_path="$repo_root/.codex-studio/published/INTERACTIVE_CONTROL_INVENTORY.generated.json"
section_host_ruleset_parity_receipt_path="$repo_root/.codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json"
recursive_ui_event_exit_gate_receipt_path="$repo_root/.codex-studio/published/RECURSIVE_UI_EVENT_EXIT_GATE.generated.json"
startup_workbench_survival_receipt_path="$repo_root/.codex-studio/published/STARTUP_WORKBENCH_SURVIVAL.generated.json"
design_mirror_completeness_receipt_path="$repo_root/.codex-studio/published/DESIGN_MIRROR_COMPLETENESS.generated.json"
design_authorized_parity_softening_receipt_path="$repo_root/.codex-studio/published/DESIGN_AUTHORIZED_PARITY_SOFTENING.generated.json"
veteran_task_time_receipt_path="$repo_root/.codex-studio/published/VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json"
chummer5a_screenshot_review_receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
classic_dense_workbench_receipt_path="$repo_root/.codex-studio/published/CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json"
chummer5a_legacy_ui_element_parity_receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json"
chummer4_legacy_ui_element_parity_receipt_path="$repo_root/.codex-studio/published/CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json"
sr5_sr6_ui_parity_audit_receipt_path="$repo_root/.codex-studio/published/SR5_SR6_UI_PARITY_AUDIT.generated.json"
browser_lane_proof_set_receipt_path="$repo_root/.codex-studio/published/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json"
play_surface_horizon_receipt_path="$repo_root/.codex-studio/published/BLAZOR_PLAY_SURFACE_HORIZON.generated.json"
ui_element_parity_audit_receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"
desktop_visual_familiarity_receipt_path="$repo_root/.codex-studio/published/DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
direct_import_route_proof_receipt_path="$repo_root/.codex-studio/published/NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json"
direct_output_route_proof_receipt_path="$repo_root/.codex-studio/published/NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json"
desktop_executable_exit_gate_receipt_path="$repo_root/.codex-studio/published/DESKTOP_EXECUTABLE_EXIT_GATE.generated.json"
default_chummer5a_oracle_root="/docker/fleet/docs/chummer5a-oracle"
local_chummer5a_oracle_root="$repo_root/docs/chummer5a-oracle"
if [[ ! -d "$default_chummer5a_oracle_root" ]]; then
  default_chummer5a_oracle_root="$local_chummer5a_oracle_root"
fi
chummer5a_oracle_root="${CHUMMER5A_ORACLE_ROOT:-$default_chummer5a_oracle_root}"
# family:dense_builder_and_career_workflows proof is anchored by
# SECTION_HOST_RULESET_PARITY.generated.json,
# CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json,
# CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json,
# and UI_LOCAL_RELEASE_PROOF.generated.json.
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
run_services_release_channel_path="${CHUMMER_RUN_SERVICES_RELEASE_CHANNEL_PATH:-/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
verified_release_channel_path="$repo_root/.tmp/verify-release-channel/RELEASE_CHANNEL.generated.json"
explicit_release_channel_path="${CHUMMER_FLAGSHIP_UI_RELEASE_CHANNEL_PATH:-${CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH:-}}"
if [[ -n "$explicit_release_channel_path" ]]; then
  release_channel_path_default="$explicit_release_channel_path"
elif [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path_default="$canonical_release_channel_path"
elif [[ -f "$verified_release_channel_path" ]]; then
  release_channel_path_default="$verified_release_channel_path"
elif [[ -f "$run_services_release_channel_path" ]]; then
  release_channel_path_default="$run_services_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi
release_channel_path="$release_channel_path_default"
refresh_supporting_receipts="${CHUMMER_FLAGSHIP_UI_RELEASE_GATE_REFRESH_SUPPORTING_RECEIPTS:-1}"
skip_downstream_receipt_materialization="${CHUMMER_FLAGSHIP_UI_RELEASE_GATE_SKIP_DOWNSTREAM_RECEIPTS:-0}"
refresh_flagship_readiness="${CHUMMER_FLAGSHIP_UI_RELEASE_GATE_REFRESH_FLAGSHIP_READINESS:-0}"
skip_flagship_readiness_refresh="${CHUMMER_FLAGSHIP_UI_RELEASE_GATE_SKIP_FLAGSHIP_READINESS_REFRESH:-0}"
desktop_workflow_execution_gate_script_path="${CHUMMER_DESKTOP_WORKFLOW_EXECUTION_GATE_SCRIPT_PATH:-$repo_root/scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh}"
desktop_executable_exit_gate_script_path="${CHUMMER_DESKTOP_EXECUTABLE_EXIT_GATE_SCRIPT_PATH:-$repo_root/scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh}"
flagship_product_readiness_materializer_path="${CHUMMER_FLAGSHIP_PRODUCT_READINESS_MATERIALIZER_PATH:-/docker/fleet/scripts/materialize_flagship_product_readiness.py}"
flagship_product_readiness_receipt_path="${CHUMMER_FLAGSHIP_PRODUCT_READINESS_RECEIPT_PATH:-/docker/fleet/.codex-studio/published/FLAGSHIP_PRODUCT_READINESS.generated.json}"
human_side_rule_authority_approval_path="${CHUMMER_HUMAN_SIDE_RULE_AUTHORITY_GOLD_APPROVAL_PATH:-/docker/chummercomplete/chummer-core-engine/.codex-studio/published/HUMAN_SIDE_RULE_AUTHORITY_GOLD_APPROVAL.generated.json}"
ui_parity_audit_probe_path="${CHUMMER_UI_PARITY_AUDIT_PROBE_PATH:-/docker/fleet/scripts/codex-shims/codexea_ui_parity_audit_probe.py}"
nuget_packages="${CHUMMER_NUGET_PACKAGES:-$repo_root/.codex-studio/.nuget/packages}"
api_base_url="${CHUMMER_API_BASE_URL:-${CHUMMER_WEB_BASE_URL:-http://127.0.0.1:8088}}"
api_project_path="${CHUMMER_API_AUTOSTART_PROJECT:-$repo_root/Chummer.Api/Chummer.Api.csproj}"
api_build_output_path="${CHUMMER_API_AUTOSTART_BUILD_OUTPUT:-$repo_root/Chummer.Api/bin/Debug/net10.0/Chummer.Api.dll}"
api_autostart_timeout_seconds="${CHUMMER_API_AUTOSTART_TIMEOUT_SECONDS:-90}"
api_server_pid=""
release_channel_max_age_seconds="${CHUMMER_FLAGSHIP_UI_RELEASE_CHANNEL_MAX_AGE_SECONDS:-86400}"
release_channel_max_future_skew_seconds="${CHUMMER_FLAGSHIP_UI_RELEASE_CHANNEL_MAX_FUTURE_SKEW_SECONDS:-300}"

# Route-local proof markers for milestone 142:
# "family:dense_builder_and_career_workflows"
# "SECTION_HOST_RULESET_PARITY.generated.json"
# "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
# "CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json"
# "UI_LOCAL_RELEASE_PROOF.generated.json"
# Route-local screenshot anchors for milestone 143:
# "18-import-dialog-light.png"
# "19-workflow-file-menu-loaded-light.png"
# "34-workflow-validate-section-light.png"
# "35-workflow-rules-section-light.png"

mkdir -p "$(dirname "$lock_dir")"
prune_release_gate_lock_if_stale() {
  if [[ ! -d "$lock_dir" ]]; then
    return 0
  fi
  if [[ -f "$lock_owner_pid_path" ]]; then
    owner_pid="$(tr -dc '0-9' <"$lock_owner_pid_path")"
    if [[ -n "$owner_pid" ]] && kill -0 "$owner_pid" 2>/dev/null; then
      return 0
    fi
  fi

  lock_stale_probe="$(
    python3 - <<'PY' "$lock_dir" "$lock_owner_pid_path" "$lock_stale_max_age_seconds"
from __future__ import annotations

import sys
import time
from pathlib import Path

lock_dir = Path(sys.argv[1])
owner_pid_path = Path(sys.argv[2])
max_age = int(sys.argv[3])
if not lock_dir.is_dir():
    print("absent")
    raise SystemExit(0)

entries = list(lock_dir.iterdir())
entries_without_owner = [entry for entry in entries if entry != owner_pid_path]
if entries_without_owner:
    print("nonempty")
    raise SystemExit(0)

age_seconds = max(0, int(time.time() - lock_dir.stat().st_mtime))
if owner_pid_path.exists():
    print(f"dead_owner_only:{age_seconds}")
    raise SystemExit(0)

if age_seconds < max_age:
    print(f"young:{age_seconds}")
    raise SystemExit(0)

print(f"stale_empty:{age_seconds}")
PY
  )"
  if [[ "$lock_stale_probe" == stale_empty:* || "$lock_stale_probe" == stale_owner_only:* || "$lock_stale_probe" == dead_owner_only:* ]]; then
    rm -rf "$lock_dir"
  fi
}

acquired_lock=0
for _ in $(seq 1 150); do
  if mkdir "$lock_dir" 2>/dev/null; then
    acquired_lock=1
    break
  fi
  prune_release_gate_lock_if_stale
  sleep 2
done
if [[ "$acquired_lock" != "1" ]]; then
  echo "[b14] FAIL: could not acquire release gate lock: $lock_dir" >&2
  exit 44
fi
printf '%s\n' "$$" >"$lock_owner_pid_path"

manage_screenshot_pack_transaction() {
  local action="$1"
  shift
  python3 - <<'PY' "$action" "$screenshot_pack_transaction_path" "$screenshot_dir" "$receipt_path" "$@"
from __future__ import annotations

import ctypes
import hashlib
import json
import os
import shutil
import stat
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path

CONTROL_NAME = "SCREENSHOT_CONTROL_EVIDENCE.generated.json"
CONTROL_CONTRACT = "chummer6-ui.screenshot_control_evidence"
PACK_DIGEST_ALGORITHM = "sha256-canonical-inventory-v1"
JOURNAL_CONTRACT = "chummer6-ui.screenshot_pack_transaction"
STAGE_PREFIX = ".ui-flagship-screenshot-stage."
RENAME_EXCHANGE = 2
LOWER_SHA256 = set("0123456789abcdef")
PRECOMMIT_STATES = {"prepared", "swapped", "fanout_prepared", "fanout_sealed"}
ALL_STATES = PRECOMMIT_STATES | {"committing"}

action = sys.argv[1]
journal_path = Path(sys.argv[2])
expected_published_dir = Path(sys.argv[3])
expected_receipt_path = Path(sys.argv[4])
action_paths = [Path(value) for value in sys.argv[5:]]
receipt_backup_path = Path(str(journal_path) + ".receipt-backup")
fanout_backup_dir = Path(str(journal_path) + ".fanout-backups")
test_failpoint = os.environ.get("CHUMMER_B14_TRANSACTION_TEST_FAILPOINT", "").strip()


def fail(message: str) -> None:
    raise SystemExit(f"[b14] FAIL: screenshot pack transaction {message}")


def fsync_directory(path: Path) -> None:
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0))
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def is_lower_sha256(value: object) -> bool:
    return (
        isinstance(value, str)
        and len(value) == 64
        and all(character in LOWER_SHA256 for character in value)
    )


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def atomic_write_bytes(path: Path, data: bytes, mode: int = 0o644) -> None:
    if path.is_symlink() or (path.exists() and not path.is_file()):
        fail(f"refusing to replace a non-regular target: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.", suffix=".tmp", dir=path.parent
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(data)
            handle.flush()
            os.fchmod(handle.fileno(), mode)
            os.fsync(handle.fileno())
        os.replace(temporary_path, path)
        fsync_directory(path.parent)
    except BaseException:
        temporary_path.unlink(missing_ok=True)
        raise


def atomic_write_journal(value: dict) -> None:
    atomic_write_bytes(
        journal_path,
        (json.dumps(value, indent=2) + "\n").encode("utf-8"),
        0o600,
    )


def directory_tree_sha256(path: Path) -> str:
    if not path.is_dir() or path.is_symlink():
        return ""
    hasher = hashlib.sha256()
    try:
        entries = sorted(path.iterdir(), key=lambda item: item.name)
        for entry in entries:
            if not entry.is_file() or entry.is_symlink():
                return ""
            data = entry.read_bytes()
            mode = stat.S_IMODE(entry.stat(follow_symlinks=False).st_mode)
            hasher.update(
                f"{entry.name}\0{mode:o}\0{len(data)}\0{hashlib.sha256(data).hexdigest()}\n".encode(
                    "utf-8"
                )
            )
    except OSError:
        return ""
    return hasher.hexdigest()


def rename_exchange(left: Path, right: Path) -> None:
    libc = ctypes.CDLL(None, use_errno=True)
    renameat2 = getattr(libc, "renameat2", None)
    if renameat2 is None:
        fail("requires Linux renameat2(RENAME_EXCHANGE)")
    renameat2.argtypes = [
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_uint,
    ]
    renameat2.restype = ctypes.c_int
    result = renameat2(
        -100,
        os.fsencode(left),
        -100,
        os.fsencode(right),
        RENAME_EXCHANGE,
    )
    if result != 0:
        error_number = ctypes.get_errno()
        raise OSError(error_number, os.strerror(error_number), f"{left} <-> {right}")


def remove_backup_artifacts() -> None:
    if receipt_backup_path.exists() or receipt_backup_path.is_symlink():
        if not receipt_backup_path.is_file() or receipt_backup_path.is_symlink():
            fail(f"receipt backup is not a regular file: {receipt_backup_path}")
        receipt_backup_path.unlink()
        fsync_directory(receipt_backup_path.parent)
    if fanout_backup_dir.exists() or fanout_backup_dir.is_symlink():
        if not fanout_backup_dir.is_dir() or fanout_backup_dir.is_symlink():
            fail(f"fanout backup path is invalid: {fanout_backup_dir}")
        shutil.rmtree(fanout_backup_dir)
        fsync_directory(fanout_backup_dir.parent)


if not journal_path.exists():
    if action == "recover":
        if receipt_backup_path.exists() or receipt_backup_path.is_symlink():
            fail(f"orphan receipt backup requires operator review: {receipt_backup_path}")
        if fanout_backup_dir.exists() or fanout_backup_dir.is_symlink():
            fail(f"orphan fanout backup requires operator review: {fanout_backup_dir}")
        raise SystemExit(0)
    fail(f"journal is missing for {action}: {journal_path}")
if not journal_path.is_file() or journal_path.is_symlink():
    fail(f"journal is not a regular file: {journal_path}")
try:
    payload = json.loads(journal_path.read_text(encoding="utf-8-sig"))
except (OSError, UnicodeError, json.JSONDecodeError) as exc:
    fail(f"journal is unreadable: {exc}")
if not isinstance(payload, dict) or payload.get("contract_name") != JOURNAL_CONTRACT:
    fail("journal contract is invalid")
if payload.get("state") not in ALL_STATES:
    fail("journal state is invalid")
if type(payload.get("hadPreviousPack")) is not bool:
    fail("journal hadPreviousPack must be a boolean")
if type(payload.get("hadPreviousReceipt")) is not bool:
    fail("journal hadPreviousReceipt must be a boolean")

published_dir = Path(str(payload.get("publishedDir") or ""))
stage_dir = Path(str(payload.get("stageDir") or ""))
if published_dir != expected_published_dir:
    fail("journal publishedDir does not match the canonical pack path")
if Path(str(payload.get("receiptPath") or "")) != expected_receipt_path:
    fail("journal receiptPath does not match the canonical flagship receipt path")
if Path(str(payload.get("receiptBackupPath") or "")) != receipt_backup_path:
    fail("journal receiptBackupPath is invalid")
published_parent = expected_published_dir.parent.resolve(strict=True)
if expected_receipt_path.parent.resolve(strict=True) != published_parent:
    fail("canonical flagship receipt must share the screenshot pack parent")
if stage_dir.parent.resolve(strict=True) != published_parent:
    fail("journal stageDir is outside the canonical pack parent")
if not stage_dir.name.startswith(STAGE_PREFIX):
    fail("journal stageDir does not have the governed staging prefix")
new_control_sha256 = str(payload.get("newControlSha256") or "")
if not is_lower_sha256(new_control_sha256):
    fail("journal newControlSha256 is invalid")
new_pack_tree_sha256 = str(payload.get("newPackTreeSha256") or "")
if not is_lower_sha256(new_pack_tree_sha256):
    fail("journal newPackTreeSha256 is invalid")
previous_pack_tree_sha256 = str(payload.get("previousPackTreeSha256") or "")
if payload["hadPreviousPack"] and not is_lower_sha256(previous_pack_tree_sha256):
    fail("journal previousPackTreeSha256 is invalid")
previous_receipt_sha256 = str(payload.get("previousReceiptSha256") or "")
if payload["hadPreviousReceipt"] and not is_lower_sha256(previous_receipt_sha256):
    fail("journal previousReceiptSha256 is invalid")


def directory_marks_new_pack(path: Path) -> bool:
    control_path = path / CONTROL_NAME
    return (
        path.is_dir()
        and not path.is_symlink()
        and control_path.is_file()
        and not control_path.is_symlink()
        and hashlib.sha256(control_path.read_bytes()).hexdigest() == new_control_sha256
    )


def validate_authority(authority: object) -> None:
    if not isinstance(authority, dict):
        fail("canonical new pack authority is missing")
    exact_values = {
        "visualBaseline": "Chummer5a",
        "designAuthorityPlatform": "windows",
        "captureHead": "avalonia",
        "captureMode": "avalonia_headless_test_harness",
    }
    for key, expected in exact_values.items():
        if authority.get(key) != expected:
            fail(f"canonical new pack authority {key} is invalid")
    for key in ("actualCaptureOperatingSystem", "actualCaptureArchitecture"):
        value = authority.get(key)
        if not isinstance(value, str) or not value.strip() or value != value.strip():
            fail(f"canonical new pack authority {key} is invalid")
    if authority.get("releaseCandidateBound") is not False:
        fail("canonical new pack authority releaseCandidateBound must be false")


def validate_full_new_pack(path: Path) -> dict:
    if not path.is_dir() or path.is_symlink():
        fail("canonical new screenshot pack is absent or invalid")
    control_path = path / CONTROL_NAME
    if not control_path.is_file() or control_path.is_symlink():
        fail("canonical new screenshot control is absent or invalid")
    control_bytes = control_path.read_bytes()
    if hashlib.sha256(control_bytes).hexdigest() != new_control_sha256:
        fail("canonical new screenshot control hash changed before commit")
    try:
        control = json.loads(control_bytes.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        fail(f"canonical new screenshot control is unreadable: {exc}")
    if not isinstance(control, dict) or control.get("contract_name") != CONTROL_CONTRACT:
        fail("canonical new screenshot control contract is invalid")
    if type(control.get("schemaVersion")) is not int or control["schemaVersion"] != 1:
        fail("canonical new screenshot control schemaVersion is invalid")
    validate_authority(control.get("authority"))
    entries = control.get("entries")
    if not isinstance(entries, list) or not entries:
        fail("canonical new screenshot control entries are invalid")
    if type(control.get("screenshotCount")) is not int or control["screenshotCount"] != len(entries):
        fail("canonical new screenshot control count is invalid")
    declared: dict[str, tuple[str, int]] = {}
    for index, entry in enumerate(entries):
        if not isinstance(entry, dict):
            fail(f"canonical new screenshot entry {index} is invalid")
        name = entry.get("screenshot")
        digest = entry.get("sha256")
        size = entry.get("sizeBytes")
        if (
            not isinstance(name, str)
            or not name
            or name != name.strip()
            or not name.endswith(".png")
            or Path(name).name != name
            or "/" in name
            or "\\" in name
            or name in declared
        ):
            fail(f"canonical new screenshot entry {index} has an invalid basename")
        if not is_lower_sha256(digest) or type(size) is not int or size <= 0:
            fail(f"canonical new screenshot entry {name} has invalid identity")
        declared[name] = (digest, size)
    observed_names = set()
    for candidate in path.iterdir():
        if not candidate.is_file() or candidate.is_symlink():
            fail(f"canonical new screenshot pack contains a non-regular entry: {candidate.name}")
        observed_names.add(candidate.name)
    if observed_names != set(declared) | {CONTROL_NAME}:
        fail("canonical new screenshot pack inventory is not exact")
    pack_hasher = hashlib.sha256()
    for name in sorted(declared):
        data = (path / name).read_bytes()
        digest, size = declared[name]
        if len(data) != size or hashlib.sha256(data).hexdigest() != digest:
            fail(f"canonical new screenshot bytes changed before commit: {name}")
        pack_hasher.update(f"{name}\0{digest}\0{size}\n".encode("utf-8"))
    pack_digest = pack_hasher.hexdigest()
    if control.get("screenshotPackDigestAlgorithm") != PACK_DIGEST_ALGORITHM:
        fail("canonical new screenshot pack digest algorithm is invalid")
    if control.get("screenshotPackSha256") != pack_digest:
        fail("canonical new screenshot pack digest is invalid")
    if directory_tree_sha256(path) != new_pack_tree_sha256:
        fail("canonical new screenshot tree changed before commit")
    return {
        "controlSha256": new_control_sha256,
        "controlSizeBytes": len(control_bytes),
        "screenshotCount": len(entries),
        "packSha256": pack_digest,
        "packDigestAlgorithm": PACK_DIGEST_ALGORITHM,
    }


def validate_receipt_binding(pack: dict) -> None:
    if not expected_receipt_path.is_file() or expected_receipt_path.is_symlink():
        fail("passing flagship receipt is absent or invalid")
    try:
        receipt = json.loads(expected_receipt_path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        fail(f"passing flagship receipt is unreadable: {exc}")
    visual = receipt.get("visualReviewEvidence") if isinstance(receipt, dict) else None
    if (
        not isinstance(receipt, dict)
        or receipt.get("contract_name") != "chummer6-ui.flagship_ui_release_gate"
        or str(receipt.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}
        or not isinstance(visual, dict)
    ):
        fail("published flagship receipt is not a passing governed receipt")
    expected_visual = {
        "screenshotControlSha256": pack["controlSha256"],
        "screenshotControlSizeBytes": pack["controlSizeBytes"],
        "screenshotCount": pack["screenshotCount"],
        "screenshotPackSha256": pack["packSha256"],
        "screenshotPackDigestAlgorithm": pack["packDigestAlgorithm"],
        "screenshotDirectory": str(expected_published_dir),
    }
    for key, expected in expected_visual.items():
        if visual.get(key) != expected:
            fail(f"published flagship receipt does not bind {key}")
    channel_id = receipt.get("channelId")
    channel_alias = receipt.get("channel")
    version = receipt.get("releaseVersion")
    version_alias = receipt.get("version")
    if (
        not isinstance(channel_id, str)
        or not channel_id.strip()
        or channel_id != channel_alias
        or not isinstance(version, str)
        or not version.strip()
        or version != version_alias
    ):
        fail("published flagship receipt release aliases are missing or conflicting")
    release = receipt.get("releaseChannelEvidence")
    if not isinstance(release, dict):
        fail("published flagship receipt releaseChannelEvidence is missing")
    release_path = Path(str(release.get("path") or ""))
    try:
        resolved_release_path = release_path.resolve(strict=True)
    except OSError as exc:
        fail(f"bound release channel path is invalid: {exc}")
    if str(resolved_release_path) != str(release_path):
        fail("bound release channel path is not canonical")
    if not release_path.is_file() or release_path.is_symlink():
        fail("bound release channel is not a regular file")
    release_bytes = release_path.read_bytes()
    if release.get("sha256") != hashlib.sha256(release_bytes).hexdigest():
        fail("bound release channel hash changed before commit")
    if release.get("sizeBytes") != len(release_bytes):
        fail("bound release channel size changed before commit")
    try:
        release_payload = json.loads(release_bytes.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        fail(f"bound release channel is unreadable: {exc}")
    if (
        not isinstance(release_payload, dict)
        or release_payload.get("contract_name") != "Chummer.Hub.Registry.Contracts"
        or str(release_payload.get("status") or "").strip().lower() != "published"
        or release.get("contract_name") != release_payload.get("contract_name")
        or release.get("status") != release_payload.get("status")
        or release.get("channelId") != channel_id
        or release.get("releaseVersion") != version
        or release_payload.get("channelId") != channel_id
        or release_payload.get("channel") != channel_alias
        or release_payload.get("releaseVersion") != version
        or release_payload.get("version") != version_alias
        or release.get("generatedAt")
        != (release_payload.get("generatedAt") or release_payload.get("generated_at"))
    ):
        fail("published flagship receipt release-channel identity is inconsistent")


def validate_fanout_records() -> list[dict]:
    records = payload.get("fanoutTargets") or []
    if not isinstance(records, list):
        fail("journal fanoutTargets is invalid")
    seen: set[str] = set()
    for index, record in enumerate(records):
        if not isinstance(record, dict):
            fail(f"journal fanout target {index} is invalid")
        path_value = record.get("path")
        if not isinstance(path_value, str) or not path_value or path_value in seen:
            fail(f"journal fanout target {index} path is invalid")
        seen.add(path_value)
        if type(record.get("existed")) is not bool:
            fail(f"journal fanout target {index} existed is invalid")
        if type(record.get("parentExisted")) is not bool:
            fail(f"journal fanout target {index} parentExisted is invalid")
        if record["existed"]:
            if not is_lower_sha256(record.get("previousSha256")):
                fail(f"journal fanout target {index} previousSha256 is invalid")
            if type(record.get("previousMode")) is not int:
                fail(f"journal fanout target {index} previousMode is invalid")
            backup_name = record.get("backupName")
            if not isinstance(backup_name, str) or Path(backup_name).name != backup_name:
                fail(f"journal fanout target {index} backupName is invalid")
    return records


def prepare_fanout() -> None:
    if payload["state"] != "swapped":
        fail("prepare-fanout requires a swapped transaction")
    normalized_paths: list[Path] = []
    seen: set[str] = set()
    for target in action_paths:
        if not target.is_absolute():
            fail(f"fanout target must be absolute: {target}")
        target_string = str(target)
        if target_string in seen:
            fail(f"fanout target is duplicated: {target}")
        seen.add(target_string)
        if target.parent.exists() and (
            not target.parent.is_dir() or target.parent.is_symlink()
        ):
            fail(f"fanout target parent is invalid: {target.parent}")
        if target.exists() and not target.parent.is_dir():
            fail(f"fanout target has no valid parent: {target}")
        normalized_paths.append(target)
    if not normalized_paths:
        fail("prepare-fanout requires at least one target")
    if fanout_backup_dir.exists() or fanout_backup_dir.is_symlink():
        fail(f"fanout backup directory already exists: {fanout_backup_dir}")
    fanout_backup_dir.mkdir(mode=0o700)
    fsync_directory(fanout_backup_dir.parent)
    records = []
    for index, target in enumerate(normalized_paths):
        if target.is_symlink() or (target.exists() and not target.is_file()):
            fail(f"fanout target is not a regular file or absent: {target}")
        record = {
            "path": str(target),
            "existed": target.is_file(),
            "parentExisted": target.parent.is_dir(),
        }
        if target.is_file():
            data = target.read_bytes()
            backup_name = f"{index:03d}.backup"
            atomic_write_bytes(fanout_backup_dir / backup_name, data, 0o600)
            record.update(
                {
                    "backupName": backup_name,
                    "previousSha256": hashlib.sha256(data).hexdigest(),
                    "previousSizeBytes": len(data),
                    "previousMode": stat.S_IMODE(target.stat(follow_symlinks=False).st_mode),
                }
            )
        records.append(record)
    payload["fanoutTargets"] = records
    payload["state"] = "fanout_prepared"
    payload["fanoutPreparedAt"] = utc_now()
    atomic_write_journal(payload)


def seal_fanout() -> None:
    if payload["state"] != "fanout_prepared":
        fail("seal-fanout requires a prepared fanout")
    records = validate_fanout_records()
    for record in records:
        target = Path(record["path"])
        if target.is_symlink() or (target.exists() and not target.is_file()):
            fail(f"fanout target is invalid at seal: {target}")
        record["finalExists"] = target.is_file()
        if target.is_file():
            data = target.read_bytes()
            record["finalSha256"] = hashlib.sha256(data).hexdigest()
            record["finalSizeBytes"] = len(data)
    payload["state"] = "fanout_sealed"
    payload["fanoutSealedAt"] = utc_now()
    atomic_write_journal(payload)


def verify_sealed_fanout() -> None:
    for record in validate_fanout_records():
        if type(record.get("finalExists")) is not bool:
            fail(f"sealed fanout target lacks finalExists: {record['path']}")
        target = Path(record["path"])
        if record["finalExists"]:
            if not target.is_file() or target.is_symlink():
                fail(f"sealed fanout target disappeared: {target}")
            data = target.read_bytes()
            if (
                not is_lower_sha256(record.get("finalSha256"))
                or record["finalSha256"] != hashlib.sha256(data).hexdigest()
                or record.get("finalSizeBytes") != len(data)
            ):
                fail(f"sealed fanout target changed before commit: {target}")
        elif target.exists() or target.is_symlink():
            fail(f"sealed absent fanout target appeared before commit: {target}")


def restore_fanout() -> None:
    records = validate_fanout_records()
    for record in records:
        target = Path(record["path"])
        if record["existed"]:
            backup = fanout_backup_dir / record["backupName"]
            if backup.is_file() and not backup.is_symlink():
                data = backup.read_bytes()
                if (
                    hashlib.sha256(data).hexdigest() != record["previousSha256"]
                    or len(data) != record["previousSizeBytes"]
                ):
                    fail(f"fanout backup identity is invalid: {backup}")
                atomic_write_bytes(target, data, record["previousMode"])
            elif (
                target.is_file()
                and not target.is_symlink()
                and hashlib.sha256(target.read_bytes()).hexdigest()
                == record["previousSha256"]
                and target.stat(follow_symlinks=False).st_size
                == record["previousSizeBytes"]
            ):
                pass
            else:
                fail(f"fanout backup is absent or invalid: {backup}")
        elif target.exists() or target.is_symlink():
            if not target.is_file() or target.is_symlink():
                fail(f"refusing to remove an invalid new fanout target: {target}")
            target.unlink()
            fsync_directory(target.parent)
        if not record.get("parentExisted") and target.parent.is_dir():
            try:
                target.parent.rmdir()
                fsync_directory(target.parent.parent)
            except OSError:
                # A materializer may share this directory with other governed
                # outputs. Never recursively remove an originally absent parent.
                pass


def finalize_commit() -> None:
    if stage_dir.exists() or stage_dir.is_symlink():
        if not stage_dir.is_dir() or stage_dir.is_symlink():
            fail("retained stageDir is invalid during commit finalization")
        if test_failpoint == "commit_during_stage_delete":
            first = next(iter(stage_dir.iterdir()), None)
            if first is not None:
                if first.is_dir() and not first.is_symlink():
                    shutil.rmtree(first)
                else:
                    first.unlink()
                fsync_directory(stage_dir)
            raise SystemExit("[b14] TEST FAILPOINT: commit_during_stage_delete")
        shutil.rmtree(stage_dir)
        fsync_directory(published_parent)
    remove_backup_artifacts()
    if journal_path.exists():
        journal_path.unlink()
        fsync_directory(published_parent)


def rollback() -> None:
    if payload["state"] in {"fanout_prepared", "fanout_sealed"}:
        restore_fanout()
    had_previous_pack = payload["hadPreviousPack"]
    canonical_tree = directory_tree_sha256(published_dir)
    stage_tree = directory_tree_sha256(stage_dir)
    if had_previous_pack:
        if canonical_tree == previous_pack_tree_sha256:
            pass
        elif stage_tree == previous_pack_tree_sha256:
            rename_exchange(stage_dir, published_dir)
            fsync_directory(published_parent)
            canonical_tree = directory_tree_sha256(published_dir)
            if canonical_tree != previous_pack_tree_sha256:
                fail("restored previous screenshot pack identity is invalid")
        else:
            fail("cannot locate the proven previous screenshot pack; retaining both directories")
    else:
        if published_dir.exists() or published_dir.is_symlink():
            if not published_dir.is_dir() or published_dir.is_symlink():
                fail("first-publication canonical pack is invalid during rollback")
            if stage_dir.exists() or stage_dir.is_symlink():
                fail("first-publication rollback found both canonical and staged packs")
            os.replace(published_dir, stage_dir)
            fsync_directory(published_parent)
        elif not stage_dir.is_dir() or stage_dir.is_symlink():
            fail("first-publication rollback cannot locate the uncommitted pack")
    if payload["hadPreviousReceipt"]:
        if receipt_backup_path.is_file() and not receipt_backup_path.is_symlink():
            previous_bytes = receipt_backup_path.read_bytes()
            if hashlib.sha256(previous_bytes).hexdigest() != previous_receipt_sha256:
                fail("previous flagship receipt backup hash is invalid")
            atomic_write_bytes(expected_receipt_path, previous_bytes, 0o644)
        elif (
            expected_receipt_path.is_file()
            and not expected_receipt_path.is_symlink()
            and hashlib.sha256(expected_receipt_path.read_bytes()).hexdigest()
            == previous_receipt_sha256
        ):
            pass
        else:
            fail("cannot restore the proven previous flagship receipt")
    elif expected_receipt_path.exists() or expected_receipt_path.is_symlink():
        if not expected_receipt_path.is_file() or expected_receipt_path.is_symlink():
            fail("refusing to remove an invalid uncommitted flagship receipt")
        expected_receipt_path.unlink()
        fsync_directory(published_parent)
    if stage_dir.exists() or stage_dir.is_symlink():
        if not stage_dir.is_dir() or stage_dir.is_symlink():
            fail("refusing to remove an invalid retained stageDir")
        shutil.rmtree(stage_dir)
        fsync_directory(published_parent)
    remove_backup_artifacts()
    journal_path.unlink()
    fsync_directory(published_parent)


published_parent = expected_published_dir.parent.resolve(strict=True)

if action == "recover":
    if payload["state"] == "committing":
        finalize_commit()
    else:
        rollback()
elif action == "prepare-fanout":
    prepare_fanout()
elif action == "seal-fanout":
    seal_fanout()
elif action == "commit":
    if payload["state"] not in {"swapped", "fanout_sealed"}:
        fail("commit requires a swapped transaction with any fanout sealed")
    pack = validate_full_new_pack(published_dir)
    validate_receipt_binding(pack)
    if payload["state"] == "fanout_sealed":
        verify_sealed_fanout()
    if payload["hadPreviousPack"]:
        if directory_tree_sha256(stage_dir) != previous_pack_tree_sha256:
            fail("retained previous screenshot pack identity changed before commit")
    elif stage_dir.exists() or stage_dir.is_symlink():
        fail("first-publication commit found an unexpected retained stageDir")
    if payload["hadPreviousReceipt"]:
        if not receipt_backup_path.is_file() or receipt_backup_path.is_symlink():
            fail("previous flagship receipt backup is missing before commit")
        if hashlib.sha256(receipt_backup_path.read_bytes()).hexdigest() != previous_receipt_sha256:
            fail("previous flagship receipt backup hash changed before commit")
    payload["state"] = "committing"
    payload["commitIntentAt"] = utc_now()
    atomic_write_journal(payload)
    if test_failpoint == "commit_after_state":
        raise SystemExit("[b14] TEST FAILPOINT: commit_after_state")
    finalize_commit()
else:
    fail(f"action is unsupported: {action}")
PY
}

stop_local_api_runtime() {
  if [[ -n "$api_server_pid" ]] && kill -0 "$api_server_pid" 2>/dev/null; then
    kill "$api_server_pid" 2>/dev/null || true
    wait "$api_server_pid" 2>/dev/null || true
  fi
  api_server_pid=""
}

cleanup() {
  cleanup_status=$?
  if [[ -e "$screenshot_pack_transaction_path" \
    || -L "$screenshot_pack_transaction_path" \
    || -e "${screenshot_pack_transaction_path}.receipt-backup" \
    || -L "${screenshot_pack_transaction_path}.receipt-backup" \
    || -e "${screenshot_pack_transaction_path}.fanout-backups" \
    || -L "${screenshot_pack_transaction_path}.fanout-backups" ]]; then
    manage_screenshot_pack_transaction recover || {
      echo "[b14] FAIL: could not recover the uncommitted screenshot pack transaction." >&2
      cleanup_status=46
    }
  fi
  rm -rf "$capture_screenshot_dir" "$staged_screenshot_dir"
  stop_local_api_runtime
  rm -f "$api_runtime_log_path"
  rm -f "$lock_owner_pid_path"
  rmdir "$lock_dir" 2>/dev/null || rm -rf "$lock_dir" 2>/dev/null || true
  return "$cleanup_status"
}
trap cleanup EXIT

# A killed prior run can leave the new pack and retained previous pack on disk.
# Recover it under the same release-gate lock before validating any new input.
manage_screenshot_pack_transaction recover

run_with_retry() {
  local max_attempts="$1"
  local step_label="$2"
  shift 2

  local attempt=1
  while true; do
    if "$@"; then
      return 0
    fi

    if (( attempt >= max_attempts )); then
      echo "[b14] FAIL: ${step_label} failed after ${attempt} attempts." >&2
      return 1
    fi

    echo "[b14] WARN: ${step_label} failed on attempt ${attempt}/${max_attempts}; retrying..." >&2
    attempt=$((attempt + 1))
    sleep 1
  done
}

run_dual_head_acceptance_tests() {
  local test_log
  local rc=0
  test_log="$(mktemp "${TMPDIR:-/tmp}/chummer-dual-head.XXXXXX.log")"
  set +e
  CHUMMER_API_BASE_URL="$api_base_url" \
  CHUMMER_WEB_BASE_URL="$api_base_url" \
  dotnet test --project Chummer.Tests/Chummer.Tests.csproj --no-restore --no-build -v minimal \
    --filter "FullyQualifiedName~Chummer.Tests.Presentation.DualHeadAcceptanceTests" >"$test_log" 2>&1
  rc=$?
  set -e
  if [[ $rc -eq 0 ]]; then
    rm -f "$test_log"
    return 0
  fi
  cat "$test_log" >&2
  rm -f "$test_log"
  return $rc
}

probe_api_surface() {
  local probe_path="$1"
  local status
  status="$(
    curl -sS -o /dev/null -m 2 -w '%{http_code}' \
      "${api_base_url%/}${probe_path}" 2>/dev/null || true
  )"
  case "$status" in
    200|401|403|404|405)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

api_surface_ready() {
  probe_api_surface "/api/workspaces?maxCount=1" && probe_api_surface "/api/shell/bootstrap"
}

ensure_local_api_runtime() {
  export CHUMMER_API_BASE_URL="$api_base_url"
  export CHUMMER_WEB_BASE_URL="$api_base_url"

  if api_surface_ready; then
    return 0
  fi

  if [[ "$api_base_url" != http://127.0.0.1:* && "$api_base_url" != http://localhost:* ]]; then
    echo "[b14] FAIL: cross-head runtime is unavailable at non-local base URL: $api_base_url" >&2
    return 1
  fi

  if [[ ! -f "$api_project_path" ]]; then
    echo "[b14] FAIL: missing API autostart project: $api_project_path" >&2
    return 1
  fi

  local run_cmd=(dotnet run --project "$api_project_path" --no-restore)
  if [[ -f "$api_build_output_path" ]]; then
    run_cmd+=(--no-build)
  fi
  run_cmd+=(--urls "$api_base_url")

  local portal_owner_shared_key="${CHUMMER_API_AUTOSTART_PORTAL_OWNER_SHARED_KEY:-}"
  if [[ -z "$portal_owner_shared_key" ]]; then
    portal_owner_shared_key="$(
      python3 - <<'PY'
import secrets

print(secrets.token_urlsafe(48))
PY
    )"
  fi
  if (( ${#portal_owner_shared_key} < 32 )); then
    echo "[b14] FAIL: CHUMMER_API_AUTOSTART_PORTAL_OWNER_SHARED_KEY must contain at least 32 UTF-8 bytes when supplied." >&2
    return 1
  fi

  CHUMMER_PORTAL_OWNER_SHARED_KEY="$portal_owner_shared_key" \
    "${run_cmd[@]}" >"$api_runtime_log_path" 2>&1 &
  api_server_pid="$!"
  unset portal_owner_shared_key

  local deadline=$((SECONDS + api_autostart_timeout_seconds))
  while (( SECONDS < deadline )); do
    if api_surface_ready; then
      return 0
    fi
    if ! kill -0 "$api_server_pid" 2>/dev/null; then
      echo "[b14] FAIL: local API autostart exited early. Log: $api_runtime_log_path" >&2
      cat "$api_runtime_log_path" >&2 || true
      return 1
    fi
    sleep 1
  done

  echo "[b14] FAIL: local API autostart timed out at $api_base_url. Log: $api_runtime_log_path" >&2
  cat "$api_runtime_log_path" >&2 || true
  return 1
}

receipt_passes_recently() {
  local receipt_path="$1"
  local max_age_seconds="${2:-86400}"
  python3 - <<'PY' "$receipt_path" "$max_age_seconds"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

receipt_path = Path(sys.argv[1])
max_age_seconds = int(sys.argv[2])
if not receipt_path.is_file():
    raise SystemExit(1)

try:
    payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(1)

status = str(payload.get("status") or "").strip().lower()
if status not in {"pass", "passed", "ready"}:
    raise SystemExit(1)

raw_generated_at = str(payload.get("generatedAt") or payload.get("generated_at") or "").strip()
if not raw_generated_at:
    raise SystemExit(1)
if raw_generated_at.endswith("Z"):
    raw_generated_at = raw_generated_at[:-1] + "+00:00"
generated_at = datetime.fromisoformat(raw_generated_at)
if generated_at.tzinfo is None:
    generated_at = generated_at.replace(tzinfo=timezone.utc)
age_seconds = (datetime.now(timezone.utc) - generated_at.astimezone(timezone.utc)).total_seconds()
if age_seconds < 0 or age_seconds > max_age_seconds:
    raise SystemExit(1)
PY
}

human_side_rule_authority_approval_present() {
  python3 - <<'PY' "$human_side_rule_authority_approval_path"
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
if not path.is_file():
    raise SystemExit(1)
payload = json.loads(path.read_text(encoding="utf-8-sig"))
rulesets = {str(item or "").strip().lower() for item in payload.get("rulesets", [])}
if str(payload.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
    raise SystemExit(1)
if not {"sr4", "sr6"}.issubset(rulesets):
    raise SystemExit(1)
PY
}

validate_release_channel_receipt() {
  python3 - <<'PY' "$release_channel_path" "$release_channel_max_age_seconds" "$release_channel_max_future_skew_seconds"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

path = Path(sys.argv[1])
max_age_seconds = int(sys.argv[2])
max_future_skew_seconds = int(sys.argv[3])
if not path.is_file() or path.is_symlink():
    raise SystemExit(f"[b14] FAIL: release channel is absent or not a regular file: {path}")
try:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
except (OSError, UnicodeError, json.JSONDecodeError) as exc:
    raise SystemExit(f"[b14] FAIL: release channel is unreadable: {exc}") from exc
if not isinstance(payload, dict):
    raise SystemExit("[b14] FAIL: release channel root must be an object")
if payload.get("contract_name") != "Chummer.Hub.Registry.Contracts":
    raise SystemExit("[b14] FAIL: release channel contract_name is not recognized")
if str(payload.get("status") or "").strip().lower() != "published":
    raise SystemExit("[b14] FAIL: release channel status is not published")
channel_id = str(payload.get("channelId") or "").strip()
channel_alias = str(payload.get("channel") or "").strip()
release_version = str(payload.get("releaseVersion") or "").strip()
version_alias = str(payload.get("version") or "").strip()
if not channel_id or not channel_alias or channel_id.lower() != channel_alias.lower():
    raise SystemExit("[b14] FAIL: release channel channelId/channel aliases are missing or conflicting")
if not release_version or not version_alias or release_version != version_alias:
    raise SystemExit("[b14] FAIL: release channel releaseVersion/version aliases are missing or conflicting")
raw_generated_at = str(payload.get("generatedAt") or payload.get("generated_at") or "").strip()
try:
    generated_at = datetime.fromisoformat(raw_generated_at.replace("Z", "+00:00"))
except ValueError as exc:
    raise SystemExit("[b14] FAIL: release channel generatedAt is invalid") from exc
if generated_at.tzinfo is None or generated_at.utcoffset() is None:
    raise SystemExit("[b14] FAIL: release channel generatedAt must include a UTC offset")
age_seconds = (datetime.now(timezone.utc) - generated_at.astimezone(timezone.utc)).total_seconds()
if age_seconds < -max_future_skew_seconds:
    raise SystemExit("[b14] FAIL: release channel generatedAt is too far in the future")
if age_seconds > max_age_seconds:
    raise SystemExit("[b14] FAIL: release channel is stale")
PY
}

mkdir -p "$(dirname "$receipt_path")"
mkdir -p "$nuget_packages"
export NUGET_PACKAGES="$nuget_packages"

validate_release_channel_receipt

ruleset_ui_adaptation_receipt_path="$repo_root/.codex-studio/published/RULESET_UI_ADAPTATION.generated.json"
chummer5a_layout_hard_receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_LAYOUT_HARD_GATE.generated.json"

# Every receipt that a post-swap supporting or downstream materializer can
# replace is snapshotted under the screenshot-pack journal. A failure therefore
# restores one coherent receipt generation with the prior pack and flagship
# receipt instead of leaving a partially refreshed proof fanout behind.
fanout_target_paths=()
if [[ "$refresh_supporting_receipts" == "1" ]]; then
  fanout_target_paths+=(
    "$chummer5a_legacy_ui_element_parity_receipt_path"
    "$chummer4_legacy_ui_element_parity_receipt_path"
    "$sr5_sr6_ui_parity_audit_receipt_path"
    "$browser_lane_proof_set_receipt_path"
    "$play_surface_horizon_receipt_path"
    "$workflow_parity_receipt_path"
    "$sr4_sr6_frontier_receipt_path"
    "$ruleset_ui_adaptation_receipt_path"
    "$sr6_ruleset_ui_sophistication_receipt_path"
    "$chummer5a_layout_hard_receipt_path"
    "$design_authorized_parity_softening_receipt_path"
    "$design_mirror_completeness_receipt_path"
    "$startup_workbench_survival_receipt_path"
    "$localization_release_gate_receipt_path"
    "$interactive_control_inventory_receipt_path"
    "$ui_element_parity_audit_receipt_path"
    "$section_host_ruleset_parity_receipt_path"
    "$recursive_ui_event_exit_gate_receipt_path"
    "$repo_root/.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json"
    "$repo_root/.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json"
    "$repo_root/.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"
  )
fi
if [[ "$skip_downstream_receipt_materialization" == "0" ]]; then
  fanout_target_paths+=(
    "$desktop_visual_familiarity_receipt_path"
    "$chummer5a_screenshot_review_receipt_path"
    "$direct_import_route_proof_receipt_path"
    "$desktop_workflow_execution_receipt_path"
    "$classic_dense_workbench_receipt_path"
    "$veteran_task_time_receipt_path"
    "$desktop_executable_exit_gate_receipt_path"
    "$direct_output_route_proof_receipt_path"
    "$verified_release_channel_path"
  )
  if [[ "$refresh_flagship_readiness" == "1" \
    && "$skip_flagship_readiness_refresh" == "0" ]]; then
    fanout_target_paths+=("$flagship_product_readiness_receipt_path")
  fi
fi

echo "[b14] building the current Avalonia desktop head without restore..."
bash scripts/ai/build.sh Chummer.Avalonia/Chummer.Avalonia.csproj \
  -c Release --no-restore -v minimal

if [[ ! -f "$sample_path" ]]; then
  echo "[b14] FAIL: bundled sample-character fixture missing from Release output: $sample_path" >&2
  exit 41
fi

if ! rg -q "b14-flagship-ui-release-gate\\.sh" "$signoff_path"; then
  echo "[b14] FAIL: workbench release signoff does not cite the flagship UI release gate: $signoff_path" >&2
  exit 42
fi

python3 - <<'PY' "$avalonia_gate_tests_path" "$dual_head_tests_path" "$blazor_shell_tests_path" "$desktop_update_runtime_tests_path" "$desktop_install_linking_runtime_tests_path" "$desktop_startup_smoke_runtime_tests_path"
import sys
from pathlib import Path

avalonia_gate_tests_path = Path(sys.argv[1])
dual_head_tests_path = Path(sys.argv[2])
blazor_shell_tests_path = Path(sys.argv[3])
desktop_update_runtime_tests_path = Path(sys.argv[4])
desktop_install_linking_runtime_tests_path = Path(sys.argv[5])
desktop_startup_smoke_runtime_tests_path = Path(sys.argv[6])
avalonia_text = avalonia_gate_tests_path.read_text(encoding="utf-8")
required_avalonia_tests = [
    "File_menu_new_character_creates_runtime_workspace",
    "Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters",
    "Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus",
    "Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions",
    "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture",
    "Runtime_backed_roster_tree_preserves_legacy_left_rail_navigation_posture",
    "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
    "Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture",
    "Runtime_backed_shell_avoids_modern_dashboard_copy_that_breaks_chummer5a_orientation",
    "Runtime_backed_shell_chrome_stays_enabled_after_runner_load",
    "Fresh_launch_main_window_survives_first_paint_without_self_termination",
    "Fresh_launch_workbench_does_not_render_a_fake_empty_section_expander",
    "Standalone_toolstrip_buttons_raise_expected_events",
    "Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events",
    "Standalone_workspace_strip_quick_start_button_raises_expected_event",
    "Standalone_summary_header_keeps_navigation_tabs_visible_without_restore_handoff",
    "Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events",
    "Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions",
    "Standalone_coach_sidecar_copy_button_raises_event_when_launch_uri_is_available",
    "Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end",
    "Loaded_runner_header_stays_tab_panel_only_without_metric_cards",
    "Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters",
    "Workspace_strip_quick_start_hides_after_runtime_backed_runner_load",
    "Loaded_runner_workbench_preserves_legacy_frmcareer_landmarks",
    "Character_creation_preserves_familiar_dense_builder_rhythm",
    "Advancement_and_karma_journal_workflows_preserve_familiar_progression_rhythm",
    "Gear_builder_preserves_familiar_browse_detail_confirm_rhythm",
    "Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm",
    "Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues",
    "Contacts_diary_and_support_routes_execute_with_public_path_visibility",
    "Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions",
    "Matrix_workflows_execute_with_specific_dialog_fields_and_confirm_actions",
]
missing_avalonia = [name for name in required_avalonia_tests if name not in avalonia_text]
if missing_avalonia:
    raise SystemExit(
        "[b14] FAIL: missing required runtime-backed Avalonia gate tests: " + ", ".join(missing_avalonia)
    )

text = dual_head_tests_path.read_text(encoding="utf-8")
required_tests = [
    "Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections",
    "Avalonia_and_Blazor_representative_legacy_workflow_fixtures_render_populated_matching_sections",
    "Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts",
    "Avalonia_and_Blazor_download_export_and_print_commands_prepare_matching_receipts",
    "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
    "Avalonia_and_Blazor_skill_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_support_family_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_gear_vehicle_and_combat_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_cyberware_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_magic_matrix_and_spirit_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_cyberware_workspace_preserves_modular_legacy_fixture_details",
    "Avalonia_and_Blazor_character_settings_save_updates_shared_state",
    "Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture",
]
missing = [name for name in required_tests if name not in text]
if missing:
    raise SystemExit(
        "[b14] FAIL: missing required full-workflow equivalence tests: " + ", ".join(missing)
    )

blazor_text = blazor_shell_tests_path.read_text(encoding="utf-8")
required_blazor_tests = [
    "MenuBar_invokes_toggle_and_execute_callbacks",
    "WorkspaceLeftPane_renders_shell_controls_and_invokes_callbacks",
    "DialogHost_renders_dialog_and_emits_events",
    "StatusStrip_announces_status_via_shared_live_region_semantics",
    "CampaignJournalPanel_renders_explicit_downtime_planner_calendar_and_schedule_views",
]
missing_blazor = [name for name in required_blazor_tests if name not in blazor_text]
if missing_blazor:
    raise SystemExit(
        "[b14] FAIL: missing required Blazor desktop shell tests: " + ", ".join(missing_blazor)
    )

desktop_update_runtime_text = desktop_update_runtime_tests_path.read_text(encoding="utf-8")
desktop_install_linking_runtime_text = desktop_install_linking_runtime_tests_path.read_text(encoding="utf-8")
desktop_startup_smoke_runtime_text = desktop_startup_smoke_runtime_tests_path.read_text(encoding="utf-8")
required_lifecycle_runtime_tests = [
    "CheckAndScheduleStartupUpdateAsync_rollout_blocked_manifests_reason_and_stops_scheduling",
    "BuildSupportPortalRelativePathForUpdate_includes_manifest_and_error_context",
    "TryHandleAsync_writes_receipt_when_requested",
]
missing_lifecycle_runtime_tests = [
    test_name
    for test_name in required_lifecycle_runtime_tests
    if test_name not in desktop_update_runtime_text
    and test_name not in desktop_install_linking_runtime_text
    and test_name not in desktop_startup_smoke_runtime_text
]
if missing_lifecycle_runtime_tests:
    raise SystemExit(
        "[b14] FAIL: missing required desktop lifecycle runtime tests: "
        + ", ".join(missing_lifecycle_runtime_tests)
    )
PY

echo "[b14] building the current flagship test assembly without restore..."
dotnet build Chummer.Tests/Chummer.Tests.csproj \
  --no-restore -m:1 --disable-build-servers -v minimal >/dev/null

echo "[b14] running flagship Avalonia headless UI gate tests..."
run_with_retry 2 "flagship Avalonia headless UI gate tests" \
  env CHUMMER_UI_GATE_SCREENSHOT_DIR="$capture_screenshot_dir" \
  dotnet test --project Chummer.Tests/Chummer.Tests.csproj --no-restore --no-build -v minimal \
  --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests" >/dev/null

echo "[b14] running flagship Blazor desktop shell gate tests..."
run_with_retry 2 "flagship Blazor desktop shell gate tests" \
  dotnet test --project Chummer.Tests/Chummer.Tests.csproj --no-restore --no-build -v minimal \
  --filter "FullyQualifiedName~BlazorShellComponentTests" >/dev/null

echo "[b14] running desktop install/update/recovery runtime tests..."
run_with_retry 2 "desktop install/update/recovery runtime tests" \
  dotnet test --project Chummer.Tests/Chummer.Tests.csproj --no-restore --no-build -v minimal -p:RunDesktopUpdateTestsOnly=true \
  --filter "CheckAndScheduleStartupUpdateAsync_rollout_blocked_manifests_reason_and_stops_scheduling|BuildSupportPortalRelativePathForUpdate_includes_manifest_and_error_context|TryHandleAsync_writes_receipt_when_requested" >/dev/null

echo "[b14] validating, normalizing, and atomically publishing the current-run screenshot pack..."
python3 - <<'PY' "$capture_screenshot_dir" "$staged_screenshot_dir" "$screenshot_dir" "$avalonia_gate_tests_path" "$screenshot_pack_transaction_path" "$receipt_path"
from __future__ import annotations

import binascii
import ctypes
import hashlib
import json
import os
import re
import stat
import struct
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path

CONTROL_NAME = "SCREENSHOT_CONTROL_EVIDENCE.generated.json"
CONTROL_CONTRACT = "chummer6-ui.screenshot_control_evidence"
PACK_DIGEST_ALGORITHM = "sha256-canonical-inventory-v1"
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
LOWER_SHA256 = re.compile(r"^[0-9a-f]{64}$")

capture_dir = Path(sys.argv[1])
stage_dir = Path(sys.argv[2])
published_dir = Path(sys.argv[3])
producer_source_path = Path(sys.argv[4])
journal_path = Path(sys.argv[5])
receipt_path = Path(sys.argv[6])
receipt_backup_path = Path(str(journal_path) + ".receipt-backup")
JOURNAL_CONTRACT = "chummer6-ui.screenshot_pack_transaction"
RENAME_EXCHANGE = 2


def fail(message: str) -> None:
    raise SystemExit(f"[b14] FAIL: {message}")


def is_regular_file(path: Path) -> bool:
    return path.is_file() and not path.is_symlink()


def require_offset_timestamp(value: object, label: str) -> str:
    if not isinstance(value, str) or not value.strip():
        fail(f"screenshot control evidence is missing {label}")
    normalized = value.strip()
    try:
        parsed = datetime.fromisoformat(normalized.replace("Z", "+00:00"))
    except ValueError:
        fail(f"screenshot control evidence {label} is not an ISO-8601 timestamp: {normalized}")
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        fail(f"screenshot control evidence {label} must include a UTC offset: {normalized}")
    return normalized


def normalize_png_bytes(data: bytes, name: str) -> bytes:
    if not data.startswith(PNG_SIGNATURE):
        fail(f"screenshot is not a PNG file: {name}")

    offset = len(PNG_SIGNATURE)
    output = bytearray(PNG_SIGNATURE)
    saw_iend = False
    saw_ihdr = False
    saw_idat = False
    while offset + 12 <= len(data):
        length = int.from_bytes(data[offset : offset + 4], "big")
        chunk_type = data[offset + 4 : offset + 8]
        chunk_start = offset + 8
        chunk_end = chunk_start + length
        crc_end = chunk_end + 4
        if crc_end > len(data):
            fail(
                "screenshot PNG chunk is truncated "
                f"({chunk_type.decode('ascii', 'replace')}): {name}"
            )
        chunk_data = data[chunk_start:chunk_end]
        if chunk_type == b"IHDR":
            if saw_ihdr or offset != len(PNG_SIGNATURE) or length != 13:
                fail(f"screenshot PNG has an invalid IHDR chunk: {name}")
            width = int.from_bytes(chunk_data[0:4], "big")
            height = int.from_bytes(chunk_data[4:8], "big")
            if width <= 0 or height <= 0:
                fail(f"screenshot PNG has invalid dimensions: {name}")
            saw_ihdr = True
        elif chunk_type == b"IDAT":
            if not saw_ihdr or length <= 0:
                fail(f"screenshot PNG has an invalid IDAT chunk: {name}")
            saw_idat = True
        crc = binascii.crc32(chunk_type)
        crc = binascii.crc32(chunk_data, crc) & 0xFFFFFFFF
        output.extend(struct.pack(">I", length))
        output.extend(chunk_type)
        output.extend(chunk_data)
        output.extend(struct.pack(">I", crc))
        offset = crc_end
        if chunk_type == b"IEND":
            if length != 0:
                fail(f"screenshot PNG IEND chunk is not empty: {name}")
            saw_iend = True
            break

    if not saw_iend:
        fail(f"screenshot PNG is missing IEND chunk: {name}")
    if not saw_ihdr:
        fail(f"screenshot PNG is missing IHDR chunk: {name}")
    if not saw_idat:
        fail(f"screenshot PNG is missing IDAT chunk: {name}")
    if offset != len(data):
        fail(f"screenshot PNG contains trailing bytes after IEND: {name}")
    return bytes(output)


def write_fsynced(path: Path, data: bytes) -> None:
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(fd, "wb") as handle:
            handle.write(data)
            handle.flush()
            os.fsync(handle.fileno())
    except BaseException:
        try:
            os.close(fd)
        except OSError:
            pass
        raise


def fsync_directory(path: Path) -> None:
    directory_fd = os.open(path, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0))
    try:
        os.fsync(directory_fd)
    finally:
        os.close(directory_fd)


def directory_tree_sha256(path: Path) -> str:
    if not path.is_dir() or path.is_symlink():
        fail(f"screenshot pack is absent or invalid: {path}")
    hasher = hashlib.sha256()
    for entry in sorted(path.iterdir(), key=lambda item: item.name):
        if not entry.is_file() or entry.is_symlink():
            fail(f"screenshot pack contains a non-regular entry: {entry}")
        data = entry.read_bytes()
        mode = stat.S_IMODE(entry.stat(follow_symlinks=False).st_mode)
        hasher.update(
            f"{entry.name}\0{mode:o}\0{len(data)}\0{hashlib.sha256(data).hexdigest()}\n".encode(
                "utf-8"
            )
        )
    return hasher.hexdigest()


def atomic_write_journal(payload: dict) -> None:
    if journal_path.is_symlink():
        fail(f"screenshot pack transaction journal must not be a symlink: {journal_path}")
    temporary_path = journal_path.parent / (
        f".{journal_path.name}.{os.getpid()}.{uuid.uuid4().hex}.tmp"
    )
    encoded = (json.dumps(payload, indent=2) + "\n").encode("utf-8")
    try:
        write_fsynced(temporary_path, encoded)
        os.replace(temporary_path, journal_path)
        fsync_directory(journal_path.parent)
    except BaseException:
        temporary_path.unlink(missing_ok=True)
        raise


def rename_exchange(left: Path, right: Path) -> None:
    libc = ctypes.CDLL(None, use_errno=True)
    renameat2 = getattr(libc, "renameat2", None)
    if renameat2 is None:
        fail("atomic pack replacement requires Linux renameat2(RENAME_EXCHANGE)")
    renameat2.argtypes = [
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_uint,
    ]
    renameat2.restype = ctypes.c_int
    result = renameat2(
        -100,
        os.fsencode(left),
        -100,
        os.fsencode(right),
        RENAME_EXCHANGE,
    )
    if result != 0:
        error_number = ctypes.get_errno()
        raise OSError(error_number, os.strerror(error_number), f"{left} <-> {right}")


def extract_producer_contract(path: Path) -> tuple[list[str], dict[str, list[str]]]:
    try:
        source = path.read_text(encoding="utf-8")
    except (OSError, UnicodeError) as exc:
        fail(f"capture producer source is unreadable: {path}: {exc}")
    inventory_match = re.search(
        r"VeteranCertificationScreenshotFiles\s*=\s*\[(.*?)\];",
        source,
        re.DOTALL,
    )
    coverage_match = re.search(
        r"WorkflowScreenshotCoverage\s*=\s*\[(.*?)\];",
        source,
        re.DOTALL,
    )
    if inventory_match is None or coverage_match is None:
        fail("capture producer source is missing its canonical screenshot contract")
    inventory = re.findall(r'"([^"]+\.png)"', inventory_match.group(1))
    coverage = {
        family_id: re.findall(r'"([^"]+\.png)"', screenshot_list)
        for family_id, screenshot_list in re.findall(
            r'new\("([^"]+)",\s*"[^"]*",\s*\[(.*?)\]\)',
            coverage_match.group(1),
            re.DOTALL,
        )
    }
    if not inventory or len(inventory) != len(set(inventory)):
        fail("capture producer source has an empty or duplicate canonical screenshot inventory")
    if not coverage:
        fail("capture producer source has no canonical workflow coverage")
    return inventory, coverage


if not capture_dir.is_dir() or capture_dir.is_symlink():
    fail(f"capture directory is absent, invalid, or a symlink: {capture_dir}")
if not stage_dir.is_dir() or stage_dir.is_symlink():
    fail(f"staging directory is absent, invalid, or a symlink: {stage_dir}")
if any(stage_dir.iterdir()):
    fail(f"staging directory is not empty: {stage_dir}")

expected_screenshot_names, expected_workflow_coverage = extract_producer_contract(
    producer_source_path
)

control_path = capture_dir / CONTROL_NAME
if not is_regular_file(control_path):
    fail(f"current-run screenshot control evidence was not produced: {control_path}")

try:
    control_evidence = json.loads(control_path.read_text(encoding="utf-8-sig"))
except (OSError, UnicodeError, json.JSONDecodeError) as exc:
    fail(f"current-run screenshot control evidence is unreadable: {exc}")
if not isinstance(control_evidence, dict):
    fail("current-run screenshot control evidence root must be an object")
if control_evidence.get("contract_name") != CONTROL_CONTRACT:
    fail("current-run screenshot control evidence contract_name is not recognized")
if type(control_evidence.get("schemaVersion")) is not int or control_evidence["schemaVersion"] != 1:
    fail("current-run screenshot control evidence schemaVersion must be integer 1")
authority = control_evidence.get("authority")
if not isinstance(authority, dict):
    fail("current-run screenshot control evidence authority must be an object")
for key, expected in {
    "visualBaseline": "Chummer5a",
    "designAuthorityPlatform": "windows",
    "captureHead": "avalonia",
    "captureMode": "avalonia_headless_test_harness",
}.items():
    if authority.get(key) != expected:
        fail(f"current-run screenshot control evidence authority {key} is invalid")
for key in ("actualCaptureOperatingSystem", "actualCaptureArchitecture"):
    value = authority.get(key)
    if not isinstance(value, str) or not value.strip() or value != value.strip():
        fail(f"current-run screenshot control evidence authority {key} is invalid")
if authority.get("releaseCandidateBound") is not False:
    fail("current-run screenshot control evidence authority releaseCandidateBound must be false")

entries = control_evidence.get("entries")
if not isinstance(entries, list) or not entries:
    fail("current-run screenshot control evidence entries must be a non-empty array")
declared_count = control_evidence.get("screenshotCount")
if type(declared_count) is not int or declared_count <= 0:
    fail("current-run screenshot control evidence screenshotCount must be a positive integer")

capture_pngs: dict[str, Path] = {}
capture_png_bytes: dict[str, bytes] = {}
for candidate in sorted(capture_dir.iterdir(), key=lambda item: item.name):
    if candidate.suffix.lower() != ".png":
        continue
    if not is_regular_file(candidate):
        fail(f"capture screenshot is not a regular non-symlink file: {candidate}")
    capture_pngs[candidate.name] = candidate
if not capture_pngs:
    fail(f"no screenshot PNG files were produced in capture directory: {capture_dir}")

declared_names: list[str] = []
for index, entry in enumerate(entries):
    if not isinstance(entry, dict):
        fail(f"screenshot control entry {index} must be an object")
    name = entry.get("screenshot")
    if (
        not isinstance(name, str)
        or not name
        or name != name.strip()
        or not name.endswith(".png")
        or "/" in name
        or "\\" in name
        or Path(name).name != name
    ):
        fail(f"screenshot control entry {index} has an invalid screenshot basename")
    if name in declared_names:
        fail(f"screenshot control evidence contains duplicate entry: {name}")
    declared_names.append(name)

    declared_sha256 = entry.get("sha256")
    declared_size = entry.get("sizeBytes")
    if not isinstance(declared_sha256, str) or LOWER_SHA256.fullmatch(declared_sha256) is None:
        fail(f"screenshot control entry has an invalid lowercase sha256: {name}")
    if type(declared_size) is not int or declared_size <= 0:
        fail(f"screenshot control entry has an invalid positive sizeBytes: {name}")
    source_path = capture_pngs.get(name)
    if source_path is None:
        fail(f"screenshot control entry has no current-run PNG: {name}")
    source_bytes = source_path.read_bytes()
    if len(source_bytes) != declared_size:
        fail(f"current-run screenshot size does not match producer evidence: {name}")
    if hashlib.sha256(source_bytes).hexdigest() != declared_sha256:
        fail(f"current-run screenshot sha256 does not match producer evidence: {name}")
    capture_png_bytes[name] = source_bytes

if declared_count != len(entries):
    fail("screenshotCount does not equal the screenshot control entry count")
if set(declared_names) != set(capture_pngs):
    undeclared = sorted(set(capture_pngs) - set(declared_names))
    missing = sorted(set(declared_names) - set(capture_pngs))
    fail(
        "current-run screenshot entry/PNG inventory differs "
        f"(undeclared={undeclared}, missing={missing})"
    )
if declared_names != sorted(expected_screenshot_names):
    fail("current-run screenshot inventory does not exactly match the capture producer contract")

workflow_coverage = control_evidence.get("workflowCoverage")
if not isinstance(workflow_coverage, list) or not workflow_coverage:
    fail("current-run screenshot control evidence workflowCoverage must be a non-empty array")
workflow_family_ids: set[str] = set()
observed_workflow_coverage: dict[str, list[str]] = {}
for index, row in enumerate(workflow_coverage):
    if not isinstance(row, dict):
        fail(f"workflowCoverage row {index} must be an object")
    family_id = row.get("workflowFamilyId")
    screenshot_files = row.get("screenshotFiles")
    if not isinstance(family_id, str) or not family_id.strip() or family_id != family_id.strip():
        fail(f"workflowCoverage row {index} has an invalid workflowFamilyId")
    if family_id in workflow_family_ids:
        fail(f"workflowCoverage contains duplicate workflowFamilyId: {family_id}")
    workflow_family_ids.add(family_id)
    if not isinstance(screenshot_files, list) or not screenshot_files:
        fail(f"workflowCoverage row {family_id} must declare screenshotFiles")
    if any(not isinstance(name, str) or name not in capture_pngs for name in screenshot_files):
        fail(f"workflowCoverage row {family_id} references an undeclared screenshot")
    if len(screenshot_files) != len(set(screenshot_files)):
        fail(f"workflowCoverage row {family_id} contains duplicate screenshotFiles")
    row_count = row.get("screenshotCount")
    if type(row_count) is not int or row_count != len(screenshot_files):
        fail(f"workflowCoverage row {family_id} has an invalid screenshotCount")
    observed_workflow_coverage[family_id] = screenshot_files

if set(observed_workflow_coverage) != set(expected_workflow_coverage):
    fail("current-run workflowCoverage family inventory does not match the capture producer contract")
for family_id, expected_files in expected_workflow_coverage.items():
    if observed_workflow_coverage[family_id] != expected_files:
        fail(
            "current-run workflowCoverage screenshot bindings do not match the capture "
            f"producer contract: {family_id}"
        )

capture_generated_at = require_offset_timestamp(
    control_evidence.get("captureGeneratedAt") or control_evidence.get("generatedAt"),
    "captureGeneratedAt/generatedAt",
)
normalized_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")

for entry in entries:
    name = entry["screenshot"]
    normalized_bytes = normalize_png_bytes(capture_png_bytes[name], name)
    final_sha256 = hashlib.sha256(normalized_bytes).hexdigest()
    final_size = len(normalized_bytes)
    entry["sha256"] = final_sha256
    entry["sizeBytes"] = final_size
    write_fsynced(stage_dir / name, normalized_bytes)

control_evidence["captureGeneratedAt"] = capture_generated_at
control_evidence["normalizedAt"] = normalized_at
control_evidence["generatedAt"] = normalized_at
control_evidence["screenshotCount"] = len(entries)
pack_hasher = hashlib.sha256()
for entry in sorted(entries, key=lambda item: item["screenshot"]):
    pack_hasher.update(
        f"{entry['screenshot']}\0{entry['sha256']}\0{entry['sizeBytes']}\n".encode("utf-8")
    )
control_evidence["screenshotPackDigestAlgorithm"] = PACK_DIGEST_ALGORITHM
control_evidence["screenshotPackSha256"] = pack_hasher.hexdigest()

control_bytes = (json.dumps(control_evidence, indent=2) + "\n").encode("utf-8")
write_fsynced(stage_dir / CONTROL_NAME, control_bytes)
stage_inventory = {item.name for item in stage_dir.iterdir()}
expected_stage_inventory = set(declared_names) | {CONTROL_NAME}
if stage_inventory != expected_stage_inventory:
    fail("staged screenshot entry/PNG inventory is not exact")
for staged_file in stage_dir.iterdir():
    os.chmod(staged_file, 0o644)
os.chmod(stage_dir, 0o755)
fsync_directory(stage_dir)

published_parent = published_dir.parent
if stage_dir.parent.resolve() != published_parent.resolve():
    fail("staged and published screenshot packs must share a parent filesystem")
if published_dir.is_symlink():
    fail(f"published screenshot directory must not be a symlink: {published_dir}")
if published_dir.exists() and not published_dir.is_dir():
    fail(f"published screenshot path is not a directory: {published_dir}")
if journal_path.parent.resolve() != published_parent.resolve():
    fail("screenshot pack transaction journal must share the published pack parent")
if journal_path.exists() or journal_path.is_symlink():
    fail(f"unresolved screenshot pack transaction journal already exists: {journal_path}")
if receipt_path.parent.resolve() != published_parent.resolve():
    fail("flagship receipt must share the published screenshot pack parent")
if receipt_path.is_symlink() or (receipt_path.exists() and not receipt_path.is_file()):
    fail(f"flagship receipt path is not a regular file: {receipt_path}")
if receipt_backup_path.exists() or receipt_backup_path.is_symlink():
    fail(f"unresolved flagship receipt transaction backup already exists: {receipt_backup_path}")

had_previous_pack = published_dir.exists()
had_previous_receipt = receipt_path.is_file()
previous_pack_tree_sha256 = directory_tree_sha256(published_dir) if had_previous_pack else ""
new_pack_tree_sha256 = directory_tree_sha256(stage_dir)
previous_receipt_sha256 = ""
if had_previous_receipt:
    previous_receipt_bytes = receipt_path.read_bytes()
    previous_receipt_sha256 = hashlib.sha256(previous_receipt_bytes).hexdigest()
transaction = {
    "contract_name": JOURNAL_CONTRACT,
    "schemaVersion": 2,
    "state": "prepared",
    "publishedDir": str(published_dir),
    "stageDir": str(stage_dir),
    "hadPreviousPack": had_previous_pack,
    "receiptPath": str(receipt_path),
    "receiptBackupPath": str(receipt_backup_path),
    "hadPreviousReceipt": had_previous_receipt,
    "previousReceiptSha256": previous_receipt_sha256,
    "newControlSha256": hashlib.sha256(control_bytes).hexdigest(),
    "newPackTreeSha256": new_pack_tree_sha256,
    "previousPackTreeSha256": previous_pack_tree_sha256,
    "preparedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
}
atomic_write_journal(transaction)
try:
    if had_previous_receipt:
        write_fsynced(receipt_backup_path, previous_receipt_bytes)
        fsync_directory(published_parent)
    if had_previous_pack:
        rename_exchange(stage_dir, published_dir)
    else:
        os.replace(stage_dir, published_dir)
    fsync_directory(published_parent)
    transaction["state"] = "swapped"
    transaction["swappedAt"] = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    atomic_write_journal(transaction)
except BaseException as exc:
    fail(f"could not atomically publish screenshot pack: {exc}")
PY

if (( ${#fanout_target_paths[@]} > 0 )); then
  manage_screenshot_pack_transaction prepare-fanout "${fanout_target_paths[@]}"
fi

echo "[b14] running cross-head workflow parity tests..."
ensure_local_api_runtime
run_with_retry 3 "cross-head workflow parity tests" run_dual_head_acceptance_tests
# The workflow-family producer must start and attest its own canonical API
# runtime. Release the B14-owned runtime before those receipts are refreshed so
# it cannot be misclassified as an untrusted pre-existing service.
stop_local_api_runtime
unset CHUMMER_API_BASE_URL CHUMMER_WEB_BASE_URL

if [[ "$refresh_supporting_receipts" == "1" ]]; then
echo "[b14] running explicit Chummer5a legacy UI element parity gate..."
bash scripts/ai/milestones/chummer5a-legacy-ui-element-parity-check.sh >/dev/null

echo "[b14] running explicit Chummer4 legacy UI element parity gate..."
bash scripts/ai/milestones/chummer4-legacy-ui-element-parity-check.sh >/dev/null

echo "[b14] running explicit direct SR5/SR6 UI parity audit..."
bash scripts/ai/milestones/sr5-sr6-ui-parity-audit-check.sh >/dev/null

echo "[b14] refreshing direct public-edge Blazor workbench proof..."
CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_REFRESH=1 \
  bash scripts/ai/milestones/blazor-public-edge-workbench-proof-check.sh >/dev/null

echo "[b14] running aggregate Blazor browser-lane proof-set gate..."
bash scripts/ai/milestones/blazor-browser-lane-proof-set-check.sh >/dev/null

echo "[b14] running public browser/PWA play-surface horizon gate..."
bash scripts/ai/milestones/blazor-play-surface-horizon-check.sh >/dev/null

echo "[b14] running explicit Chummer5a desktop workflow parity gate..."
CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH="$release_channel_path" \
  bash scripts/ai/milestones/chummer5a-desktop-workflow-parity-check.sh >/dev/null

echo "[b14] running explicit SR4/SR6 desktop parity frontier gate..."
CHUMMER_SR4_SR6_FRONTIER_SKIP_SUBGATE_REFRESH=0 \
  CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH="$release_channel_path" \
  CHUMMER_SR4_WORKFLOW_PARITY_SKIP_DEPENDENCY_MATERIALIZE=1 \
  CHUMMER_SR6_WORKFLOW_PARITY_SKIP_DEPENDENCY_MATERIALIZE=1 \
  CHUMMER_CHUMMER5A_WORKFLOW_PARITY_SKIP_DEPENDENCY_MATERIALIZE=1 \
  bash scripts/ai/milestones/sr4-sr6-desktop-parity-frontier-receipt.sh >/dev/null

echo "[b14] refreshing explicit ruleset UI adaptation gate..."
bash scripts/ai/milestones/ruleset-ui-adaptation-check.sh >/dev/null

echo "[b14] refreshing section-host ruleset parity gate..."
bash scripts/ai/milestones/section-host-ruleset-parity-check.sh >/dev/null

echo "[b14] running explicit SR6 ruleset UI sophistication gate..."
bash scripts/ai/milestones/sr6-ruleset-ui-sophistication-gate.sh >/dev/null

echo "[b14] running explicit Chummer5a layout hard gate..."
bash scripts/ai/milestones/chummer5a-layout-hard-gate.sh >/dev/null

echo "[b14] running explicit design-authorized parity softening gate..."
bash scripts/ai/milestones/design-authorized-parity-softening-check.sh >/dev/null

echo "[b14] running explicit flagship design mirror completeness gate..."
bash scripts/ai/milestones/design-mirror-completeness-check.sh >/dev/null

echo "[b14] running explicit startup workbench survival gate..."
bash scripts/ai/milestones/startup-workbench-survival-check.sh >/dev/null

echo "[b14] refreshing standalone interactive control inventory..."
bash scripts/ai/milestones/interactive-control-inventory-check.sh >/dev/null

echo "[b14] materializing localization release gate..."
bash scripts/ai/milestones/b15-localization-release-gate.sh >/dev/null

echo "[b14] refreshing Chummer5a UI element parity audit..."
CHUMMER_UI_PARITY_REPO_ROOT="$(realpath "$repo_root")" python3 "$ui_parity_audit_probe_path" >/dev/null

echo "[b14] refreshing recursive UI event exit gate..."
bash scripts/ai/milestones/recursive-ui-event-exit-gate.sh >/dev/null
else
  echo "[b14] supporting receipt refreshes disabled; consuming existing receipts only."
fi

CHUMMER5A_ORACLE_ROOT="$chummer5a_oracle_root" python3 - <<'PY' "$sample_path" "$receipt_path" "$screenshot_dir" "$signoff_path" "$avalonia_gate_tests_path" "$dual_head_tests_path" "$blazor_shell_tests_path" "$desktop_update_runtime_tests_path" "$desktop_install_linking_runtime_tests_path" "$desktop_startup_smoke_runtime_tests_path" "$workflow_parity_receipt_path" "$sr4_workflow_parity_receipt_path" "$sr6_workflow_parity_receipt_path" "$sr6_ruleset_ui_sophistication_receipt_path" "$sr4_sr6_frontier_receipt_path" "$desktop_workflow_execution_receipt_path" "$localization_release_gate_receipt_path" "$interactive_control_inventory_receipt_path" "$startup_workbench_survival_receipt_path" "$design_mirror_completeness_receipt_path" "$design_authorized_parity_softening_receipt_path" "$release_channel_path" "$human_side_rule_authority_approval_path"
import hashlib
import json
import os
import re
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path

(
    sample_path,
    receipt_path,
    screenshot_dir,
    signoff_path,
    avalonia_gate_tests_path,
    dual_head_tests_path,
    blazor_shell_tests_path,
    desktop_update_runtime_tests_path,
    desktop_install_linking_runtime_tests_path,
    desktop_startup_smoke_runtime_tests_path,
    workflow_parity_receipt_path,
    sr4_workflow_parity_receipt_path,
    sr6_workflow_parity_receipt_path,
    sr6_ruleset_ui_sophistication_receipt_path,
    sr4_sr6_frontier_receipt_path,
    desktop_workflow_execution_receipt_path,
    localization_release_gate_receipt_path,
    interactive_control_inventory_receipt_path,
    startup_workbench_survival_receipt_path,
    design_mirror_completeness_receipt_path,
    design_authorized_parity_softening_receipt_path,
    release_channel_path,
    human_side_rule_authority_approval_path,
) = sys.argv[1:24]
expected_screenshots = [
    "01-initial-shell-light.png",
    "02-menu-open-light.png",
    "03-settings-open-light.png",
    "04-loaded-runner-light.png",
    "05-dense-section-light.png",
    "06-dense-section-dark.png",
    "07-loaded-runner-tabs-light.png",
    "08-cyberware-dialog-light.png",
    "09-vehicles-section-light.png",
    "10-contacts-section-light.png",
    "11-diary-dialog-light.png",
    "12-magic-dialog-light.png",
    "13-matrix-dialog-light.png",
    "14-advancement-dialog-light.png",
    "15-creation-section-light.png",
    "16-master-index-dialog-light.png",
    "17-character-roster-dialog-light.png",
    "18-import-dialog-light.png",
    "19-workflow-file-menu-loaded-light.png",
    "20-workflow-skills-section-light.png",
    "21-workflow-skill-add-dialog-light.png",
    "22-workflow-qualities-section-light.png",
    "23-workflow-quality-add-dialog-light.png",
    "24-workflow-gear-section-light.png",
    "25-workflow-gear-add-dialog-light.png",
    "26-workflow-weapons-section-light.png",
    "27-workflow-weapon-add-dialog-light.png",
    "28-workflow-armor-section-light.png",
    "29-workflow-armor-add-dialog-light.png",
    "30-workflow-cyberware-section-light.png",
    "31-workflow-powers-section-light.png",
    "32-workflow-adept-power-dialog-light.png",
    "33-workflow-complex-form-dialog-light.png",
    "34-workflow-validate-section-light.png",
    "35-workflow-rules-section-light.png",
    "36-workflow-new-character-dialog-light.png",
    "37-workflow-calendar-section-light.png",
    "38-translator-dialog-light.png",
    "39-xml-editor-dialog-light.png",
    "40-hero-lab-importer-dialog-light.png",
    "41-horizons-hub-light.png",
    "42-horizon-karma-forge-light.png",
    "43-horizon-alice-light.png",
    "44-horizon-black-ledger-light.png",
    "45-horizon-run-control-light.png",
    "46-horizon-runsite-light.png",
    "47-horizon-jackpoint-light.png",
    "48-horizon-table-pulse-light.png",
    "49-horizon-community-hub-light.png",
    "50-horizon-nexus-pan-light.png",
    "51-horizon-quicksilver-light.png",
    "52-horizon-runner-passport-light.png",
    "53-horizon-runbook-press-light.png",
    "54-horizon-creator-os-light.png",
    "55-horizon-local-co-processor-light.png",
    "56-horizon-anarchy-light.png",
    "57-horizon-ghostwire-light.png",
    "58-horizon-ready-for-tonight-light.png",
    "60-horizon-knowledge-fabric-light.png",
]
required_full_workflow_tests = [
    "Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections",
    "Avalonia_and_Blazor_representative_legacy_workflow_fixtures_render_populated_matching_sections",
    "Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts",
    "Avalonia_and_Blazor_download_export_and_print_commands_prepare_matching_receipts",
    "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
    "Avalonia_and_Blazor_skill_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_support_family_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_gear_vehicle_and_combat_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_cyberware_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_magic_matrix_and_spirit_dialog_actions_execute_matching_notices",
    "Avalonia_and_Blazor_cyberware_workspace_preserves_modular_legacy_fixture_details",
    "Avalonia_and_Blazor_character_settings_save_updates_shared_state",
]
required_blazor_shell_tests = [
    "MenuBar_invokes_toggle_and_execute_callbacks",
    "WorkspaceLeftPane_renders_shell_controls_and_invokes_callbacks",
    "DialogHost_renders_dialog_and_emits_events",
    "StatusStrip_announces_status_via_shared_live_region_semantics",
    "CampaignJournalPanel_renders_explicit_downtime_planner_calendar_and_schedule_views",
]
required_lifecycle_runtime_tests = [
    "CheckAndScheduleStartupUpdateAsync_rollout_blocked_manifests_reason_and_stops_scheduling",
    "BuildSupportPortalRelativePathForUpdate_includes_manifest_and_error_context",
    "TryHandleAsync_writes_receipt_when_requested",
]
release_channel_payload = {}
release_channel_channel_id = ""
release_channel_version = ""
repo_root = str(Path(receipt_path).resolve().parents[2])
published_root = os.path.join(repo_root, ".codex-studio", "published")
ui_element_parity_audit_receipt_path = os.path.join(published_root, "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json")
chummer5a_legacy_ui_element_parity_receipt_path = os.path.join(published_root, "CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json")
chummer4_legacy_ui_element_parity_receipt_path = os.path.join(published_root, "CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json")
sr5_sr6_ui_parity_audit_receipt_path = os.path.join(published_root, "SR5_SR6_UI_PARITY_AUDIT.generated.json")
desktop_executable_exit_gate_receipt_path = os.path.join(published_root, "DESKTOP_EXECUTABLE_EXIT_GATE.generated.json")
direct_import_route_proof_receipt_path = os.path.join(published_root, "NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json")
direct_workflow_route_proof_receipt_path = os.path.join(published_root, "NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json")
direct_output_route_proof_receipt_path = os.path.join(published_root, "NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json")
browser_lane_proof_set_receipt_path = os.path.join(published_root, "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json")
play_surface_horizon_receipt_path = os.path.join(published_root, "BLAZOR_PLAY_SURFACE_HORIZON.generated.json")
flagship_product_readiness_receipt_path = os.environ.get(
    "CHUMMER_FLAGSHIP_PRODUCT_READINESS_RECEIPT_PATH",
    "/docker/fleet/.codex-studio/published/FLAGSHIP_PRODUCT_READINESS.generated.json",
).strip()


def load_json_if_present(path: str) -> dict:
    candidate = Path(path)
    if not candidate.is_file() or candidate.is_symlink():
        return {}
    try:
        loaded = json.loads(candidate.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError):
        return {}
    return loaded if isinstance(loaded, dict) else {}


def require_current_passing_receipt(
    path: str,
    label: str,
    expected_contract: str | None,
    *,
    require_channel: bool = False,
    require_version: bool = False,
    expected_schema_version: int | None = None,
    require_nonempty_coverage: bool = False,
) -> dict:
    receipt_path = Path(path)
    if not receipt_path.is_file() or receipt_path.is_symlink():
        raise SystemExit(
            f"[b14] FAIL: {label} receipt is missing or not a regular file: {path}"
        )
    receipt_bytes = receipt_path.read_bytes()
    try:
        payload = json.loads(receipt_bytes.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise SystemExit(f"[b14] FAIL: {label} receipt is unreadable: {exc}") from exc
    if not isinstance(payload, dict):
        raise SystemExit(f"[b14] FAIL: {label} receipt root must be an object")
    contract_values = [
        payload[key]
        for key in ("contract_name", "contractName")
        if key in payload
    ]
    if expected_contract is not None and (
        not contract_values or any(value != expected_contract for value in contract_values)
    ):
        raise SystemExit(
            f"[b14] FAIL: {label} receipt contract identity is missing or invalid"
        )
    if len(contract_values) == 2 and contract_values[0] != contract_values[1]:
        raise SystemExit(f"[b14] FAIL: {label} receipt contract aliases conflict")
    if expected_schema_version is not None:
        schema_values = [
            payload[key]
            for key in ("schemaVersion", "schema_version")
            if key in payload
        ]
        if not schema_values or any(
            type(value) is not int or value != expected_schema_version
            for value in schema_values
        ):
            raise SystemExit(f"[b14] FAIL: {label} receipt schema identity is invalid")
    if require_channel:
        channel_values = [
            str(payload[key]).strip()
            for key in ("channelId", "channel")
            if key in payload
        ]
        if (
            not channel_values
            or any(not value for value in channel_values)
            or any(value.lower() != release_channel_channel_id.lower() for value in channel_values)
        ):
            raise SystemExit(f"[b14] FAIL: {label} receipt release channel is invalid")
    if require_version:
        version_values = [
            str(payload[key]).strip()
            for key in ("releaseVersion", "version")
            if key in payload
        ]
        if (
            not version_values
            or any(not value for value in version_values)
            or any(value != release_channel_version for value in version_values)
        ):
            raise SystemExit(f"[b14] FAIL: {label} receipt release version is invalid")
    if require_nonempty_coverage and (
        not isinstance(payload.get("coverage"), dict) or not payload["coverage"]
    ):
        raise SystemExit(f"[b14] FAIL: {label} receipt coverage is missing or empty")
    if str(payload.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
        reasons = (
            payload.get("reasons")
            or payload.get("blockingFindings")
            or payload.get("blocking_findings")
            or payload.get("findings")
            or ["missing reason"]
        )
        if not isinstance(reasons, list):
            reasons = [str(reasons)]
        raise SystemExit(
            f"[b14] FAIL: {label} receipt is not passed: "
            + ", ".join(str(reason) for reason in reasons)
        )
    generated_at_raw = str(
        payload.get("generatedAt") or payload.get("generated_at") or ""
    ).strip()
    if not generated_at_raw:
        raise SystemExit(
            f"[b14] FAIL: {label} receipt is missing generatedAt/generated_at"
        )
    try:
        generated_at = datetime.fromisoformat(
            generated_at_raw.replace("Z", "+00:00")
        )
    except ValueError as exc:
        raise SystemExit(
            f"[b14] FAIL: {label} receipt generatedAt/generated_at is invalid"
        ) from exc
    if generated_at.tzinfo is None or generated_at.utcoffset() is None:
        raise SystemExit(
            f"[b14] FAIL: {label} receipt generatedAt/generated_at must include a UTC offset"
        )
    age_seconds = (
        datetime.now(timezone.utc) - generated_at.astimezone(timezone.utc)
    ).total_seconds()
    max_age_seconds = int(
        os.environ.get("CHUMMER_FLAGSHIP_UI_SUPPORTING_PROOF_MAX_AGE_SECONDS")
        or "86400"
    )
    max_future_skew_seconds = int(
        os.environ.get("CHUMMER_FLAGSHIP_UI_SUPPORTING_PROOF_MAX_FUTURE_SKEW_SECONDS")
        or "300"
    )
    if age_seconds < -max_future_skew_seconds:
        raise SystemExit(
            f"[b14] FAIL: {label} receipt generatedAt is too far in the future"
        )
    if age_seconds > max_age_seconds:
        raise SystemExit(f"[b14] FAIL: {label} receipt is stale")
    return payload


def atomic_write_json(path: str | Path, payload: dict) -> None:
    target = Path(path)
    if target.is_symlink():
        raise SystemExit(f"[b14] FAIL: refusing to replace symlink receipt path: {target}")
    encoded = (json.dumps(payload, indent=2) + "\n").encode("utf-8")
    fd, temporary_name = tempfile.mkstemp(
        prefix=f".{target.name}.",
        suffix=".tmp",
        dir=target.parent,
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(fd, "wb") as handle:
            handle.write(encoded)
            handle.flush()
            os.fchmod(handle.fileno(), 0o644)
            os.fsync(handle.fileno())
        os.replace(temporary_path, target)
        directory_fd = os.open(target.parent, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0))
        try:
            os.fsync(directory_fd)
        finally:
            os.close(directory_fd)
    except BaseException:
        temporary_path.unlink(missing_ok=True)
        raise


def require_offset_timestamp(value: object, label: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise SystemExit(f"[b14] FAIL: screenshot control evidence is missing {label}")
    normalized = value.strip()
    try:
        parsed = datetime.fromisoformat(normalized.replace("Z", "+00:00"))
    except ValueError as exc:
        raise SystemExit(
            f"[b14] FAIL: screenshot control evidence {label} is not an ISO-8601 timestamp"
        ) from exc
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise SystemExit(
            f"[b14] FAIL: screenshot control evidence {label} must include a UTC offset"
        )
    return normalized


def human_side_rule_authority_approved(path: str) -> tuple[bool, dict]:
    if not os.path.isfile(path):
        return False, {"status": "missing", "path": path}
    with open(path, "r", encoding="utf-8") as handle:
        receipt = json.load(handle)
    rulesets = {
        str(item or "").strip().lower()
        for item in receipt.get("rulesets", [])
        if str(item or "").strip()
    }
    approved = (
        str(receipt.get("status") or "").strip().lower() in {"pass", "passed", "ready"}
        and {"sr4", "sr6"}.issubset(rulesets)
    )
    return approved, {
        "status": str(receipt.get("status") or "").strip(),
        "path": path,
        "reviewer": str(receipt.get("reviewer") or "").strip(),
        "rulesets": sorted(rulesets),
        "generatedAt": str(receipt.get("generated_at_utc") or receipt.get("generatedAt") or "").strip(),
    }


release_channel_file = Path(release_channel_path)
if not release_channel_file.is_file() or release_channel_file.is_symlink():
    raise SystemExit("[b14] FAIL: release channel is absent or not a regular file")
release_channel_bytes = release_channel_file.read_bytes()
try:
    loaded_release_channel = json.loads(release_channel_bytes.decode("utf-8-sig"))
except (UnicodeError, json.JSONDecodeError) as exc:
    raise SystemExit(f"[b14] FAIL: release channel is unreadable: {exc}") from exc
if not isinstance(loaded_release_channel, dict):
    raise SystemExit("[b14] FAIL: release channel root must be an object")
release_channel_payload = loaded_release_channel
if release_channel_payload.get("contract_name") != "Chummer.Hub.Registry.Contracts":
    raise SystemExit("[b14] FAIL: release channel contract_name is not recognized")
if str(release_channel_payload.get("status") or "").strip().lower() != "published":
    raise SystemExit("[b14] FAIL: release channel status is not published")
release_channel_channel_id = str(release_channel_payload.get("channelId") or "").strip()
release_channel_channel_alias = str(release_channel_payload.get("channel") or "").strip()
release_channel_version = str(release_channel_payload.get("releaseVersion") or "").strip()
release_channel_version_alias = str(release_channel_payload.get("version") or "").strip()
if (
    not release_channel_channel_id
    or not release_channel_channel_alias
    or release_channel_channel_id.lower() != release_channel_channel_alias.lower()
):
    raise SystemExit("[b14] FAIL: release channel channelId/channel aliases are missing or conflicting")
if (
    not release_channel_version
    or not release_channel_version_alias
    or release_channel_version != release_channel_version_alias
):
    raise SystemExit("[b14] FAIL: release channel releaseVersion/version aliases are missing or conflicting")
release_channel_sha256 = hashlib.sha256(release_channel_bytes).hexdigest()
release_channel_size_bytes = len(release_channel_bytes)
release_channel_generated_at_raw = str(
    release_channel_payload.get("generatedAt")
    or release_channel_payload.get("generated_at")
    or ""
).strip()
try:
    release_channel_generated_at = datetime.fromisoformat(
        release_channel_generated_at_raw.replace("Z", "+00:00")
    )
except ValueError as exc:
    raise SystemExit("[b14] FAIL: release channel generatedAt is invalid") from exc
if (
    release_channel_generated_at.tzinfo is None
    or release_channel_generated_at.utcoffset() is None
):
    raise SystemExit("[b14] FAIL: release channel generatedAt must include a UTC offset")
release_channel_age_seconds = (
    datetime.now(timezone.utc)
    - release_channel_generated_at.astimezone(timezone.utc)
).total_seconds()
release_channel_max_age_seconds = int(
    os.environ.get("CHUMMER_FLAGSHIP_UI_RELEASE_CHANNEL_MAX_AGE_SECONDS") or "86400"
)
release_channel_max_future_skew_seconds = int(
    os.environ.get("CHUMMER_FLAGSHIP_UI_RELEASE_CHANNEL_MAX_FUTURE_SKEW_SECONDS") or "300"
)
if release_channel_age_seconds < -release_channel_max_future_skew_seconds:
    raise SystemExit("[b14] FAIL: release channel generatedAt is too far in the future")
if release_channel_age_seconds > release_channel_max_age_seconds:
    raise SystemExit("[b14] FAIL: release channel is stale")
workflow_parity_receipt = require_current_passing_receipt(
    workflow_parity_receipt_path,
    "explicit Chummer5a desktop workflow parity proof",
    "chummer6-ui.chummer5a_desktop_workflow_parity",
    require_channel=True,
)
sr4_workflow_parity_receipt = require_current_passing_receipt(
    sr4_workflow_parity_receipt_path,
    "explicit SR4 desktop workflow parity proof",
    "chummer6-ui.sr4_desktop_workflow_parity",
    require_channel=True,
)
human_side_rule_authority_is_approved, human_side_rule_authority_receipt = human_side_rule_authority_approved(
    human_side_rule_authority_approval_path
)
sr6_workflow_parity_receipt = require_current_passing_receipt(
    sr6_workflow_parity_receipt_path,
    "explicit SR6 desktop workflow parity proof",
    "chummer6-ui.sr6_desktop_workflow_parity",
    require_channel=True,
)
sr6_ruleset_ui_sophistication_receipt = require_current_passing_receipt(
    sr6_ruleset_ui_sophistication_receipt_path,
    "explicit SR6 ruleset UI sophistication proof",
    "chummer6-ui.chummer_sr6_ruleset_ui_sophistication_gate",
)
chummer5a_legacy_ui_element_parity_receipt = require_current_passing_receipt(
    chummer5a_legacy_ui_element_parity_receipt_path,
    "explicit Chummer5a legacy UI element parity proof",
    "chummer6-ui.chummer5a_legacy_ui_element_parity",
)
chummer4_legacy_ui_element_parity_receipt = require_current_passing_receipt(
    chummer4_legacy_ui_element_parity_receipt_path,
    "explicit Chummer4 legacy UI element parity proof",
    "chummer6-ui.chummer4_legacy_ui_element_parity",
)
sr5_sr6_ui_parity_audit_receipt = require_current_passing_receipt(
    sr5_sr6_ui_parity_audit_receipt_path,
    "explicit direct SR5/SR6 UI parity audit",
    "chummer6-ui.sr5_sr6_ui_parity_audit",
)
sr4_sr6_frontier_receipt = require_current_passing_receipt(
    sr4_sr6_frontier_receipt_path,
    "explicit SR4/SR6 desktop parity frontier proof",
    "chummer6-ui.sr4_sr6_desktop_parity_frontier",
    require_channel=True,
)
localization_release_gate_receipt = require_current_passing_receipt(
    localization_release_gate_receipt_path,
    "explicit localization release gate proof",
    "chummer6-ui.localization_release_gate",
)
design_authorized_parity_softening_receipt = require_current_passing_receipt(
    design_authorized_parity_softening_receipt_path,
    "explicit design-authorized parity softening proof",
    "chummer6-ui.design_authorized_parity_softening",
)
design_mirror_completeness_receipt = require_current_passing_receipt(
    design_mirror_completeness_receipt_path,
    "explicit flagship design mirror completeness proof",
    "chummer6-ui.design_mirror_completeness",
)
startup_workbench_survival_receipt = require_current_passing_receipt(
    startup_workbench_survival_receipt_path,
    "explicit startup workbench survival proof",
    "chummer6-ui.startup_workbench_survival",
)
interactive_control_inventory_receipt = require_current_passing_receipt(
    interactive_control_inventory_receipt_path,
    "standalone interactive control inventory proof",
    "chummer6-ui.interactive_control_inventory",
)
full_interactive_control_inventory_status = str(interactive_control_inventory_receipt.get("evidence", {}).get("fullInteractiveControlInventory") or "").strip().lower()
main_window_interaction_inventory_status = str(interactive_control_inventory_receipt.get("evidence", {}).get("mainWindowInteractionInventory") or "").strip().lower()
if full_interactive_control_inventory_status not in {"pass", "passed", "ready"}:
    raise SystemExit("[b14] FAIL: standalone interactive control inventory proof is not passed.")
if main_window_interaction_inventory_status not in {"pass", "passed", "ready"}:
    raise SystemExit("[b14] FAIL: main-window interaction inventory proof is not passed.")

def receipt_status(payload: dict) -> str:
    value = str(payload.get("status") or "").strip().lower()
    return "pass" if value in {"pass", "passed", "ready"} else "fail"


def proof_status(*values: object) -> str:
    normalized = [str(value or "").strip().lower() for value in values]
    return "pass" if all(value in {"pass", "passed", "ready"} for value in normalized) else "fail"


def bool_status(value: bool) -> str:
    return "pass" if value else "fail"


def normalize(value: object) -> str:
    return str(value or "").strip().lower()


ui_element_parity_audit_receipt = require_current_passing_receipt(
    ui_element_parity_audit_receipt_path,
    "Chummer5a UI element parity audit",
    None,
)
direct_import_route_proof_receipt = load_json_if_present(direct_import_route_proof_receipt_path)
direct_workflow_route_proof_receipt = load_json_if_present(direct_workflow_route_proof_receipt_path)
direct_output_route_proof_receipt = load_json_if_present(direct_output_route_proof_receipt_path)
ui_element_summary = ui_element_parity_audit_receipt.get("summary") or {}
ui_element_visual_no_count_raw = ui_element_parity_audit_receipt.get("visualNoCount")
if ui_element_visual_no_count_raw is None:
    ui_element_visual_no_count_raw = ui_element_summary.get("visual_no_count")
ui_element_behavioral_no_count_raw = ui_element_parity_audit_receipt.get("behavioralNoCount")
if ui_element_behavioral_no_count_raw is None:
    ui_element_behavioral_no_count_raw = ui_element_summary.get("behavioral_no_count")
if type(ui_element_visual_no_count_raw) is not int or ui_element_visual_no_count_raw < 0:
    raise SystemExit("[b14] FAIL: UI element parity audit visual no-count is missing or invalid")
if type(ui_element_behavioral_no_count_raw) is not int or ui_element_behavioral_no_count_raw < 0:
    raise SystemExit("[b14] FAIL: UI element parity audit behavioral no-count is missing or invalid")
ui_element_visual_no_count = ui_element_visual_no_count_raw
ui_element_behavioral_no_count = ui_element_behavioral_no_count_raw
ui_element_coverage_gap_keys_raw = ui_element_parity_audit_receipt.get("coverageGapKeys")
if ui_element_coverage_gap_keys_raw is None:
    ui_element_coverage_gap_keys_raw = ui_element_summary.get("coverage_gap_keys")
if not isinstance(ui_element_coverage_gap_keys_raw, list):
    raise SystemExit("[b14] FAIL: UI element parity audit coverage-gap keys are missing or invalid")
ui_element_coverage_gap_keys = list(ui_element_coverage_gap_keys_raw)
ui_element_rows = ui_element_parity_audit_receipt.get("rows")
if not isinstance(ui_element_rows, list) or not ui_element_rows:
    raise SystemExit("[b14] FAIL: UI element parity audit rows are missing")
ui_element_parity_rows = {
    str(row.get("id") or "").strip(): row
    for row in ui_element_rows
    if isinstance(row, dict) and str(row.get("id") or "").strip()
}
chummer5a_oracle_root = os.environ.get("CHUMMER5A_ORACLE_ROOT", "/docker/fleet/docs/chummer5a-oracle")

dense_builder_route_local_evidence = [
    os.path.join(chummer5a_oracle_root, "veteran_workflow_packs.yaml"),
    os.path.join(published_root, "SECTION_HOST_RULESET_PARITY.generated.json"),
    os.path.join(published_root, "RECURSIVE_UI_EVENT_EXIT_GATE.generated.json"),
    os.path.join(published_root, "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"),
    os.path.join(published_root, "CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json"),
    os.path.join(published_root, "CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json"),
    os.path.join(published_root, "NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json"),
    os.path.join(published_root, "CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json"),
    receipt_path,
    os.path.join(published_root, "UI_LOCAL_RELEASE_PROOF.generated.json"),
    os.path.join(published_root, "BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json"),
    os.path.join(published_root, "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"),
    os.path.join(published_root, "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json"),
    os.path.join(published_root, "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json"),
]
required_dense_builder_route_local_evidence_suffixes = [
    "SECTION_HOST_RULESET_PARITY.generated.json",
    "RECURSIVE_UI_EVENT_EXIT_GATE.generated.json",
    "CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json",
    "CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json",
    "UI_FLAGSHIP_RELEASE_GATE.generated.json",
    "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json",
    "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json",
]
downstream_dense_builder_route_local_evidence_suffixes = [
    "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
    "CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json",
    "UI_LOCAL_RELEASE_PROOF.generated.json",
    "BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json",
    "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json",
    "NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json",
]
dense_builder_contracts = {
    "SECTION_HOST_RULESET_PARITY.generated.json": "chummer6-ui.section_host_ruleset_parity",
    "RECURSIVE_UI_EVENT_EXIT_GATE.generated.json": "chummer6-ui.recursive_ui_event_exit_gate",
    "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json": "chummer6-ui.chummer5a_screenshot_review_gate",
    "CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json": "chummer6-ui.chummer5a_legacy_ui_element_parity",
    "CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json": "chummer6-ui.chummer4_legacy_ui_element_parity",
    "CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json": "chummer6-ui.classic_dense_workbench_posture_gate",
    "UI_LOCAL_RELEASE_PROOF.generated.json": "chummer6-ui.local_release_proof",
    "BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json": "chummer6-ui.blazor_self_host_workbench_proof",
    "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json": "chummer6-ui.blazor_public_edge_workbench_proof",
    "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json": "chummer6-ui.blazor_browser_lane_proof_set",
    "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json": "chummer6-ui.veteran_task_time_evidence_gate",
}
missing_dense_builder_route_local_evidence_suffixes = []
for suffix in required_dense_builder_route_local_evidence_suffixes:
    matching_paths = [
        entry for entry in dense_builder_route_local_evidence if entry.endswith(suffix)
    ]
    if len(matching_paths) != 1:
        missing_dense_builder_route_local_evidence_suffixes.append(suffix)
        continue
    # The current flagship receipt is the output being composed below. Every
    # other precommit receipt must already be fresh and passing. Receipts that
    # consume this flagship receipt are recorded separately as downstream
    # evidence and cannot be prerequisites without creating a release cycle.
    if suffix == "UI_FLAGSHIP_RELEASE_GATE.generated.json":
        continue
    require_current_passing_receipt(
        matching_paths[0],
        f"dense-builder route-local evidence {suffix}",
        dense_builder_contracts[suffix],
        require_channel=suffix == "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
        require_version=suffix == "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
    )

def all_true_checks(payload: object, required_keys: list[str] | None = None) -> bool:
    if not isinstance(payload, dict) or not payload:
        return False
    if required_keys is None:
        return all(bool(value) for value in payload.values())
    return all(bool(payload.get(key)) for key in required_keys)


direct_import_route_receipt_checks = (
    direct_import_route_proof_receipt.get("evidence", {}).get("routeReceiptChecks")
    if isinstance(direct_import_route_proof_receipt.get("evidence"), dict)
    else {}
)
direct_import_receipt_checks = (
    direct_import_route_proof_receipt.get("evidence", {}).get("receiptChecks")
    if isinstance(direct_import_route_proof_receipt.get("evidence"), dict)
    else {}
)
direct_workflow_family_checks = (
    direct_workflow_route_proof_receipt.get("evidence", {}).get("familyChecks")
    if isinstance(direct_workflow_route_proof_receipt.get("evidence"), dict)
    else {}
)
direct_workflow_receipt_checks = (
    direct_workflow_route_proof_receipt.get("evidence", {}).get("receiptChecks")
    if isinstance(direct_workflow_route_proof_receipt.get("evidence"), dict)
    else {}
)
direct_output_route_receipt_checks = (
    direct_output_route_proof_receipt.get("evidence", {}).get("routeReceiptChecks")
    if isinstance(direct_output_route_proof_receipt.get("evidence"), dict)
    else {}
)
direct_output_receipt_checks = (
    direct_output_route_proof_receipt.get("evidence", {}).get("receiptChecks")
    if isinstance(direct_output_route_proof_receipt.get("evidence"), dict)
    else {}
)

ui_element_route_local_row_proofs = {
    "source:hero_lab_importer_route": (
        all_true_checks(
            (direct_import_route_receipt_checks or {}).get("hero_lab_import_oracle"),
            ["exists", "status_pass", "route_ids_exact", "workflow_family_matches", "screenshots_exact"],
        )
        and all_true_checks(
            direct_import_receipt_checks,
            [
                "visual_familiarity_gate_pass",
                "screenshot_review_gate_pass",
                "veteran_task_gate_pass",
                "ui_flagship_gate_tokens_present",
            ],
        )
    ),
    "family:legacy_and_adjacent_import_oracles": (
        all_true_checks(
            (direct_import_route_receipt_checks or {}).get("hero_lab_import_oracle"),
            ["exists", "status_pass", "route_ids_exact", "workflow_family_matches", "screenshots_exact"],
        )
        and all_true_checks(
            direct_import_receipt_checks,
            [
                "visual_familiarity_gate_pass",
                "screenshot_review_gate_pass",
                "veteran_task_gate_pass",
                "ui_flagship_gate_tokens_present",
            ],
        )
    ),
    "family:dice_initiative_and_table_utilities": (
        all_true_checks(
            (direct_workflow_family_checks or {}).get("family:dice_initiative_and_table_utilities")
        )
        and all_true_checks(
            direct_workflow_receipt_checks,
            [
                "audit_receipt_pass",
                "screenshot_review_receipt_pass",
                "workflow_execution_receipt_pass",
                "route_local_dense_initiative_pass",
                "route_local_dense_initiative_route_ids_match",
                "route_local_dense_initiative_screenshots_match",
                "workflow_initiative_utility_pass",
            ],
        )
    ),
    "family:sheet_export_print_viewer_and_exchange": (
        all_true_checks(
            (direct_output_route_receipt_checks or {}).get("print_export_exchange"),
            ["exists", "status_pass", "route_ids_exact", "workflow_family_matches", "screenshots_exact"],
        )
        and all_true_checks(
            direct_output_receipt_checks,
            [
                "screenshot_review_status_pass",
                "section_host_status_pass",
                "generated_dialog_status_pass",
                "section_host_open_for_printing_present",
                "section_host_open_for_export_present",
                "section_host_print_multiple_present",
                "generated_dialog_open_for_printing_present",
                "generated_dialog_open_for_export_present",
                "generated_dialog_print_multiple_present",
                "ui_flagship_18-import-dialog-light.png_present",
                "ui_flagship_19-workflow-file-menu-loaded-light.png_present",
                "route_local_receipts_present",
            ],
        )
    ),
}
ui_element_route_local_expected_row_ids = set(ui_element_route_local_row_proofs)
ui_element_nonpassing_row_ids = sorted(
    row_id
    for row_id, row in ui_element_parity_rows.items()
    if normalize(row.get("visual_parity")) != "yes" or normalize(row.get("behavioral_parity")) != "yes"
)
ui_element_parity_audit_source_status = proof_status(
    bool_status(ui_element_visual_no_count == 0),
    bool_status(ui_element_behavioral_no_count == 0),
    bool_status(not missing_dense_builder_route_local_evidence_suffixes),
)
ui_element_parity_route_local_only = (
    ui_element_parity_audit_source_status == "fail"
    and bool(ui_element_nonpassing_row_ids)
    and set(ui_element_nonpassing_row_ids).issubset(ui_element_route_local_expected_row_ids)
    and all(ui_element_route_local_row_proofs.get(row_id, False) for row_id in ui_element_nonpassing_row_ids)
)
ui_element_parity_audit_effective_status = (
    ui_element_parity_audit_source_status
)

desktop_executable_exit_gate_receipt = load_json_if_present(
    desktop_executable_exit_gate_receipt_path
)
desktop_executable_exit_gate_status = receipt_status(desktop_executable_exit_gate_receipt)
desktop_executable_exit_gate_local_blocking_findings = [
    str(item).strip()
    for item in (
        desktop_executable_exit_gate_receipt.get("localBlockingFindings")
        or desktop_executable_exit_gate_receipt.get("local_blocking_findings")
        or []
    )
    if str(item).strip()
]
desktop_executable_exit_gate_route_local_allowed_fragments = (
    "Desktop visual familiarity exit gate is missing or not passing.",
    "Desktop workflow execution gate is missing or not passing.",
    "linux desktop exit gate proof for ",
    "Linux desktop exit gate receipt head channelId/channel does not match release channel",
    "Linux desktop exit gate receipt checks.release_channel_id does not match release channel",
    "Linux desktop exit gate receipt checks.release_channel_version does not match release channel",
    "Linux desktop exit gate receipt releaseVersion/version does not match release channel",
    "Linux installer startup smoke receipt channelId does not match release channel",
    "Linux installer startup smoke receipt version does not match release channel",
    "Linux installer startup smoke receipt carries conflicting version/releaseVersion alias values",
    "Linux gate embedded release_channel_linux_artifact channelId/channel does not match promoted release channel.",
    "Linux gate embedded release_channel_linux_artifact version/releaseVersion does not match promoted release channel version.",
    "Linux gate embedded release_channel_linux_artifact sha256 does not match promoted release channel.",
    "Linux gate embedded release_channel_linux_artifact sizeBytes does not match promoted release channel.",
    "Linux installer startup smoke receipt artifactDigest does not match promoted release-channel artifact bytes",
    "flagship UI release gate proof is stale",
    "Windows desktop exit gate requires a Windows-capable host; current host cannot run promoted Windows installer smoke.",
    "Windows gate reason: Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host.",
)
desktop_executable_exit_gate_route_local_only = (
    desktop_executable_exit_gate_status == "fail"
    and bool(desktop_executable_exit_gate_local_blocking_findings)
    and all(
        any(fragment in finding for fragment in desktop_executable_exit_gate_route_local_allowed_fragments)
        for finding in desktop_executable_exit_gate_local_blocking_findings
    )
)
desktop_executable_exit_gate_effective_status = (
    desktop_executable_exit_gate_status
)

flagship_product_readiness_receipt = load_json_if_present(
    flagship_product_readiness_receipt_path
)
flagship_readiness_status = receipt_status(flagship_product_readiness_receipt)
flagship_readiness_coverage = dict(flagship_product_readiness_receipt.get("coverage") or {})
flagship_readiness_open_coverage_keys = [
    key for key, value in flagship_readiness_coverage.items()
    if str(value or "").strip().lower() not in {"ready", "pass", "passed"}
]
desktop_client_coverage_status = str(flagship_readiness_coverage.get("desktop_client") or "").strip().lower()
flagship_readiness_allowed_external_open_keys = {
    "desktop_client",
    "fleet_and_operator_loop",
    "horizons_and_public_surface",
}
flagship_readiness_route_local_only = (
    flagship_readiness_status == "fail"
    and bool(flagship_readiness_coverage)
    and bool(flagship_readiness_open_coverage_keys)
    and set(flagship_readiness_open_coverage_keys).issubset(flagship_readiness_allowed_external_open_keys)
)
flagship_readiness_effective_status = (
    flagship_readiness_status
)

public_edge_workbench_receipt = require_current_passing_receipt(
    os.path.join(published_root, "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"),
    "public-edge workbench proof",
    "chummer6-ui.blazor_public_edge_workbench_proof",
)
public_edge_workbench_receipt_status = receipt_status(public_edge_workbench_receipt)
browser_lane_proof_set_receipt = require_current_passing_receipt(
    browser_lane_proof_set_receipt_path,
    "Blazor browser-lane proof set",
    "chummer6-ui.blazor_browser_lane_proof_set",
)
browser_lane_proof_set_status = receipt_status(browser_lane_proof_set_receipt)
browser_lane_proof_set_checks = {
    "status_pass": browser_lane_proof_set_status == "pass",
    "contract_matches": str(browser_lane_proof_set_receipt.get("contract_name") or "").strip()
    == "chummer6-ui.blazor_browser_lane_proof_set",
    "all_required_receipts_passed": int(browser_lane_proof_set_receipt.get("required_receipt_count") or 0)
    == int(browser_lane_proof_set_receipt.get("passed_receipt_count") or -1),
}
play_surface_horizon_receipt = require_current_passing_receipt(
    play_surface_horizon_receipt_path,
    "Blazor play-surface horizon proof",
    "chummer6-ui.blazor_play_surface_horizon",
)
play_surface_horizon_status = receipt_status(play_surface_horizon_receipt)
play_surface_horizon_ids = {
    str(item.get("id") or "").strip()
    for item in play_surface_horizon_receipt.get("horizons") or []
    if isinstance(item, dict)
}
play_surface_horizon_checks = {
    "status_pass": play_surface_horizon_status == "pass",
    "contract_matches": str(play_surface_horizon_receipt.get("contract_name") or "").strip()
    == "chummer6-ui.blazor_play_surface_horizon",
    "required_horizon_ids_present": {
        "near_term_stabilization",
        "mid_term_pwa_session_utility",
        "long_term_living_world_expansion",
    }.issubset(play_surface_horizon_ids),
    "pwa_public_edge_status_pass": normalize(
        (play_surface_horizon_receipt.get("current_release_truth") or {}).get("pwa_public_edge_status")
    ) in {"pass", "passed", "ready"},
    "promoted_route_base_present": str(
        (play_surface_horizon_receipt.get("current_release_truth") or {}).get("promoted_route_base") or ""
    ).strip() == "/blazor/workbench",
}
public_edge_workbench_required_route_markers = [
    "public_chummer_app_route",
    "public_chummer_app_roster_route",
    "public_blazor_root_redirect",
    "public_blazor_health",
    "public_workbench_route",
    "public_workspace_restore_route",
    "public_startup_deep_link_route",
    "public_result_continuation_routes",
    "public_action_continuation_routes",
    "public_committed_action_route",
]
public_edge_workbench_extended_route_markers = [
    "public_startup_workbench_command_routes",
    "public_advanced_action_routes",
    "public_advanced_committed_action_routes",
]
public_edge_workbench_required_workflow_markers = [
    "blazor_root_redirect",
    "workbench_route",
    "workspace_resume_route_shape",
    "new_character_deep_link_route_shape",
    "result_continuation_route_shapes",
    "action_continuation_route_shapes",
    "committed_action_route_shape",
]
public_edge_workbench_extended_workflow_markers = [
    "startup_command_route_shapes",
    "advanced_action_route_shapes",
    "advanced_committed_action_route_shapes",
]
public_edge_workbench_receipt_checks = {
    "status_pass": public_edge_workbench_receipt_status == "pass",
    "contract_matches": str(public_edge_workbench_receipt.get("contract_name") or "").strip()
    == "chummer6-ui.blazor_public_edge_workbench_proof",
    "proof_shape_known": str(public_edge_workbench_receipt.get("proof_shape") or "").strip() in {"core", "expanded"}
    or all(
        marker in [str(item).strip() for item in public_edge_workbench_receipt.get("route_proof_markers") or []]
        for marker in public_edge_workbench_extended_route_markers
    )
    or all(
        marker in [str(item).strip() for item in public_edge_workbench_receipt.get("workflow_proofs") or []]
        for marker in public_edge_workbench_required_workflow_markers
    ),
    "new_character_deep_link_present": "/blazor/preview?command=new_character" in json.dumps(public_edge_workbench_receipt),
    "chummer_app_roster_route_present": "/app?command=character_roster" in json.dumps(public_edge_workbench_receipt),
    "workbench_route_present": "/blazor/workbench" in json.dumps(public_edge_workbench_receipt),
    "route_markers_present": all(
        marker in [str(item).strip() for item in public_edge_workbench_receipt.get("route_proof_markers") or []]
        for marker in public_edge_workbench_required_route_markers
    ),
    "workflow_markers_present": all(
        marker in [str(item).strip() for item in public_edge_workbench_receipt.get("workflow_proofs") or []]
        for marker in public_edge_workbench_required_workflow_markers
    ),
    "extended_route_markers_present": all(
        marker in [str(item).strip() for item in public_edge_workbench_receipt.get("route_proof_markers") or []]
        for marker in public_edge_workbench_extended_route_markers
    ),
    "extended_workflow_markers_present": all(
        marker in [str(item).strip() for item in public_edge_workbench_receipt.get("workflow_proofs") or []]
        for marker in public_edge_workbench_extended_workflow_markers
    ),
}

required_workflow_family_ids = [
    "create-open-import-save-save-as-print-export",
    "metatype-priorities-karma-entry",
    "attributes-skills-skill-groups-specializations-knowledge-languages",
    "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources",
    "armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers",
    "cyberware-bioware-modular-hierarchies-nested-plugins",
    "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms",
    "improvements-explain-result-parity",
    "recovery-reload-migration-roundtrips",
    "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
    "native-horizons-surface-catalog",
]
control_contract = "chummer6-ui.screenshot_control_evidence"
pack_digest_algorithm = "sha256-canonical-inventory-v1"
lower_sha256_pattern = re.compile(r"^[0-9a-f]{64}$")
screenshot_directory = Path(screenshot_dir)
if not screenshot_directory.is_dir() or screenshot_directory.is_symlink():
    raise SystemExit(
        f"[b14] FAIL: published screenshot directory is absent, invalid, or a symlink: {screenshot_directory}"
    )

screenshot_control_path = screenshot_directory / "SCREENSHOT_CONTROL_EVIDENCE.generated.json"
if not screenshot_control_path.is_file() or screenshot_control_path.is_symlink():
    raise SystemExit(
        f"[b14] FAIL: final screenshot control evidence is absent or not a regular file: {screenshot_control_path}"
    )
screenshot_control_canonical_path = str(screenshot_control_path.resolve(strict=True))
screenshot_control_bytes = screenshot_control_path.read_bytes()
screenshot_control_sha256 = hashlib.sha256(screenshot_control_bytes).hexdigest()
screenshot_control_size_bytes = len(screenshot_control_bytes)
if screenshot_control_size_bytes <= 0:
    raise SystemExit("[b14] FAIL: final screenshot control evidence is empty")
try:
    control_evidence = json.loads(screenshot_control_bytes.decode("utf-8-sig"))
except (UnicodeError, json.JSONDecodeError) as exc:
    raise SystemExit(f"[b14] FAIL: final screenshot control evidence is unreadable: {exc}") from exc
if not isinstance(control_evidence, dict):
    raise SystemExit("[b14] FAIL: final screenshot control evidence root must be an object")
if control_evidence.get("contract_name") != control_contract:
    raise SystemExit("[b14] FAIL: final screenshot control contract_name is not recognized")
screenshot_control_schema_version = control_evidence.get("schemaVersion")
if type(screenshot_control_schema_version) is not int or screenshot_control_schema_version != 1:
    raise SystemExit("[b14] FAIL: final screenshot control schemaVersion must be integer 1")
final_authority = control_evidence.get("authority")
if not isinstance(final_authority, dict):
    raise SystemExit("[b14] FAIL: final screenshot control authority must be an object")
for key, expected in {
    "visualBaseline": "Chummer5a",
    "designAuthorityPlatform": "windows",
    "captureHead": "avalonia",
    "captureMode": "avalonia_headless_test_harness",
}.items():
    if final_authority.get(key) != expected:
        raise SystemExit(f"[b14] FAIL: final screenshot control authority {key} is invalid")
for key in ("actualCaptureOperatingSystem", "actualCaptureArchitecture"):
    value = final_authority.get(key)
    if not isinstance(value, str) or not value.strip() or value != value.strip():
        raise SystemExit(f"[b14] FAIL: final screenshot control authority {key} is invalid")
if final_authority.get("releaseCandidateBound") is not False:
    raise SystemExit(
        "[b14] FAIL: final screenshot control authority releaseCandidateBound must be false"
    )
screenshot_control_generated_at = require_offset_timestamp(
    control_evidence.get("generatedAt"),
    "generatedAt",
)
require_offset_timestamp(control_evidence.get("captureGeneratedAt"), "captureGeneratedAt")
screenshot_control_normalized_at = require_offset_timestamp(
    control_evidence.get("normalizedAt"),
    "normalizedAt",
)
if screenshot_control_generated_at != screenshot_control_normalized_at:
    raise SystemExit(
        "[b14] FAIL: final screenshot control generatedAt must equal normalizedAt"
    )

control_entries = control_evidence.get("entries")
screenshot_count = control_evidence.get("screenshotCount")
if not isinstance(control_entries, list) or not control_entries:
    raise SystemExit("[b14] FAIL: final screenshot control entries must be a non-empty array")
if type(screenshot_count) is not int or screenshot_count != len(control_entries) or screenshot_count <= 0:
    raise SystemExit("[b14] FAIL: final screenshot control screenshotCount is invalid")

published_pngs = {}
for candidate in sorted(screenshot_directory.iterdir(), key=lambda item: item.name):
    if candidate.suffix.lower() != ".png":
        continue
    if not candidate.is_file() or candidate.is_symlink():
        raise SystemExit(
            f"[b14] FAIL: final screenshot is not a regular non-symlink file: {candidate}"
        )
    published_pngs[candidate.name] = candidate

entry_by_name = {}
pack_hasher = hashlib.sha256()
for index, entry in enumerate(control_entries):
    if not isinstance(entry, dict):
        raise SystemExit(f"[b14] FAIL: final screenshot control entry {index} must be an object")
    name = entry.get("screenshot")
    sha256 = entry.get("sha256")
    size_bytes = entry.get("sizeBytes")
    if (
        not isinstance(name, str)
        or not name
        or name != name.strip()
        or not name.endswith(".png")
        or "/" in name
        or "\\" in name
        or Path(name).name != name
    ):
        raise SystemExit(f"[b14] FAIL: final screenshot control entry {index} has an invalid basename")
    if name in entry_by_name:
        raise SystemExit(f"[b14] FAIL: final screenshot control contains duplicate entry: {name}")
    if not isinstance(sha256, str) or lower_sha256_pattern.fullmatch(sha256) is None:
        raise SystemExit(f"[b14] FAIL: final screenshot control has invalid lowercase sha256: {name}")
    if type(size_bytes) is not int or size_bytes <= 0:
        raise SystemExit(f"[b14] FAIL: final screenshot control has invalid sizeBytes: {name}")
    screenshot_path = published_pngs.get(name)
    if screenshot_path is None:
        raise SystemExit(f"[b14] FAIL: final screenshot control entry has no PNG: {name}")
    screenshot_bytes = screenshot_path.read_bytes()
    if len(screenshot_bytes) != size_bytes:
        raise SystemExit(f"[b14] FAIL: final screenshot size does not match control: {name}")
    if hashlib.sha256(screenshot_bytes).hexdigest() != sha256:
        raise SystemExit(f"[b14] FAIL: final screenshot sha256 does not match control: {name}")
    entry_by_name[name] = entry

if set(entry_by_name) != set(published_pngs):
    raise SystemExit("[b14] FAIL: final screenshot entry/PNG inventory is not exact")
for name in sorted(entry_by_name):
    entry = entry_by_name[name]
    pack_hasher.update(
        f"{name}\0{entry['sha256']}\0{entry['sizeBytes']}\n".encode("utf-8")
    )
screenshot_pack_sha256 = pack_hasher.hexdigest()
if control_evidence.get("screenshotPackDigestAlgorithm") != pack_digest_algorithm:
    raise SystemExit("[b14] FAIL: final screenshot pack digest algorithm is invalid")
if control_evidence.get("screenshotPackSha256") != screenshot_pack_sha256:
    raise SystemExit("[b14] FAIL: final screenshot pack digest does not match its inventory")

captured = []
missing = []
for name in expected_screenshots:
    entry = entry_by_name.get(name)
    if entry is None:
        missing.append(str(screenshot_directory / name))
        continue
    captured.append(
        {
            "name": name,
            "path": str(published_pngs[name]),
            "sha256": entry["sha256"],
            "sizeBytes": entry["sizeBytes"],
        }
    )
if missing:
    raise SystemExit("[b14] FAIL: missing screenshot evidence: " + ", ".join(missing))

workflow_screenshot_coverage = control_evidence.get("workflowCoverage")
if not isinstance(workflow_screenshot_coverage, list) or not workflow_screenshot_coverage:
    raise SystemExit("[b14] FAIL: final screenshot control workflowCoverage must be a non-empty array")
workflow_coverage_by_id = {}
for index, row in enumerate(workflow_screenshot_coverage):
    if not isinstance(row, dict):
        raise SystemExit(f"[b14] FAIL: workflowCoverage row {index} must be an object")
    family_id = row.get("workflowFamilyId")
    screenshot_files = row.get("screenshotFiles")
    if not isinstance(family_id, str) or not family_id.strip() or family_id != family_id.strip():
        raise SystemExit(f"[b14] FAIL: workflowCoverage row {index} has an invalid family ID")
    if family_id in workflow_coverage_by_id:
        raise SystemExit(f"[b14] FAIL: workflowCoverage has a duplicate family ID: {family_id}")
    if not isinstance(screenshot_files, list) or not screenshot_files:
        raise SystemExit(f"[b14] FAIL: workflowCoverage row {family_id} has no screenshotFiles")
    if len(screenshot_files) != len(set(screenshot_files)):
        raise SystemExit(f"[b14] FAIL: workflowCoverage row {family_id} has duplicate screenshots")
    if any(not isinstance(name, str) or name not in entry_by_name for name in screenshot_files):
        raise SystemExit(f"[b14] FAIL: workflowCoverage row {family_id} references an unknown screenshot")
    workflow_coverage_by_id[family_id] = row
if set(workflow_coverage_by_id) != set(required_workflow_family_ids):
    raise SystemExit("[b14] FAIL: final screenshot workflowCoverage inventory is not exact")
workflow_screenshot_coverage_status = "pass"

blocking_findings = []
aggregate_readiness_observations = []
if ui_element_parity_audit_effective_status != "pass":
    blocking_findings.append(
        "Top-level release gate cannot pass while parity matrix still has no-parity rows."
    )
if missing_dense_builder_route_local_evidence_suffixes:
    blocking_findings.append(
        "Dense builder parity audit row is missing route-local proof evidence: "
        + ", ".join(missing_dense_builder_route_local_evidence_suffixes)
    )
if not all(public_edge_workbench_receipt_checks.values()):
    blocking_findings.append(
        "Hosted public-edge browser-client proof is missing required route-entry markers."
    )
if not all(browser_lane_proof_set_checks.values()):
    blocking_findings.append(
        "Aggregate Blazor browser-lane proof set is missing or not passing."
    )
if not all(play_surface_horizon_checks.values()):
    blocking_findings.append(
        "Public browser/PWA play-surface horizon proof is missing required horizons or release-truth posture."
    )
if desktop_executable_exit_gate_status != "pass":
    aggregate_readiness_observations.append(
        "Desktop executable exit gate is not passed; this remains release-blocking "
        "in the desktop executable and flagship product readiness gates."
    )
if flagship_readiness_status != "pass":
    aggregate_readiness_observations.append(
        "Flagship product readiness is not passed; this remains release-blocking "
        "in the Fleet aggregate readiness gate."
    )
if (
    desktop_client_coverage_status not in {"", "ready", "pass", "passed"}
):
    aggregate_readiness_observations.append(
        "Flagship readiness coverage.desktop_client is not ready."
    )
if flagship_readiness_open_coverage_keys:
    aggregate_readiness_observations.append(
        "Flagship readiness still has open coverage keys: "
        + ", ".join(flagship_readiness_open_coverage_keys)
        + "."
    )

top_level_status = proof_status(
    "pass",
    receipt_status(workflow_parity_receipt),
    receipt_status(localization_release_gate_receipt),
    ui_element_parity_audit_effective_status,
)
if blocking_findings:
    top_level_status = "fail"

payload = {
    "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    "contract_name": "chummer6-ui.flagship_ui_release_gate",
    "channelId": release_channel_channel_id,
    "channel": release_channel_channel_id,
    "releaseVersion": release_channel_version,
        "version": release_channel_version,
    "status": top_level_status,
    "blockingFindings": blocking_findings,
    "aggregateReadinessObservations": aggregate_readiness_observations,
    "releaseGate": "b14-flagship-ui-release-gate",
    "desktopHead": "avalonia",
    "desktopHeads": ["avalonia", "blazor-desktop"],
    "artifactPresence": {
        "bundledDemoRunnerPath": sample_path,
        "bundledDemoRunnerPresent": os.path.isfile(sample_path),
    },
    "releaseChannelEvidence": {
        "path": str(release_channel_file.resolve(strict=True)),
        "contract_name": "Chummer.Hub.Registry.Contracts",
        "status": "published",
        "channelId": release_channel_channel_id,
        "releaseVersion": release_channel_version,
        "sha256": release_channel_sha256,
        "sizeBytes": release_channel_size_bytes,
        "generatedAt": release_channel_generated_at_raw,
    },
    "interactionProof": {
        "testSuites": [
            "AvaloniaFlagshipUiGateTests",
            "BlazorShellComponentTests",
            "DualHeadAcceptanceTests",
        ],
        "menuSurface": "pass",
        "settingsInlineDialog": "pass",
        "demoRunnerDispatch": "pass",
        "keyboardShortcutParity": "pass",
        "legacyFamiliarityBridge": "pass",
        "crossHeadWorkflowParity": "pass",
        "installUpdateRecoveryLifecycle": "pass",
        "themeReadabilityContrast": "pass",
        "blazorDesktopShellChrome": "pass",
        "runtimeBackedShellMenu": "pass",
        "runtimeBackedMenuBarLabels": "pass",
        "runtimeBackedClickablePrimaryMenus": "pass",
        "runtimeBackedToolstripActions": "pass",
        "runtimeBackedCodexTree": "pass",
        "runtimeBackedFileMenuRoutes": "pass",
        "runtimeBackedNewCharacterFileWorkflow": "pass",
        "runtimeBackedMasterIndex": "pass",
        "runtimeBackedCharacterRoster": "pass",
        "runtimeBackedSr4CodexOrientationModel": "pass",
        "runtimeBackedSr5CodexOrientationModel": "pass",
        "runtimeBackedSr6CodexOrientationModel": "pass",
        "runtimeBackedClassicChromeCopy": "pass",
        "runtimeBackedTabPanelOnlyHeader": "pass",
        "runtimeBackedChromeEnabledAfterRunnerLoad": "pass",
        "runtimeBackedDemoRunnerImport": "pass",
        "translator_xml_custom_data": "pass",
        "hero_lab_import_oracle": "pass",
        "fullInteractiveControlInventory": full_interactive_control_inventory_status,
        "mainWindowInteractionInventory": main_window_interaction_inventory_status,
        "runtimeBackedLegacyWorkbench": "pass",
        "defaultSingleRunnerKeepsWorkspaceChromeCollapsed": "pass",
        "legacyDenseBuilderRhythm": "pass",
        "legacyCreationWorkflowRhythm": "pass",
        "legacyAdvancementWorkflowRhythm": "pass",
        "legacyBrowseDetailConfirmRhythm": "pass",
        "legacyMainframeVisualSimilarity": "pass",
        "legacyGearWorkflowRhythm": "pass",
        "legacyVehiclesBuilderRhythm": "pass",
        "legacyCyberwareDialogRhythm": "pass",
        "legacyContactsDiaryRhythm": "pass",
        "legacyContactsWorkflowRhythm": "pass",
        "legacyDiaryWorkflowRhythm": "pass",
        "legacyMagicWorkflowRhythm": "pass",
        "legacyMatrixWorkflowRhythm": "pass",
        "lifecycleRuntimeTestSuites": [
            "DesktopUpdateRuntimeTests",
            "DesktopInstallLinkingRuntimeTests",
            "DesktopStartupSmokeRuntimeTests",
        ],
    },
    "headProofs": {
        "avalonia": {
            "status": "pass",
            "testSuites": [
                "AvaloniaFlagshipUiGateTests",
                "DualHeadAcceptanceTests"
            ],
            "sourceTestFile": avalonia_gate_tests_path,
            "visualReview": "pass",
            "themeReadabilityContrast": "pass",
            "bundledDemoRunner": "pass",
            "releaseLifecycle": "pass",
            "requiredRuntimeBackedTests": [
                "File_menu_new_character_creates_runtime_workspace",
                "Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters",
                "Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus",
                "Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions",
                "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture",
                "Runtime_backed_roster_tree_preserves_legacy_left_rail_navigation_posture",
                "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
                "Runtime_backed_shell_avoids_modern_dashboard_copy_that_breaks_chummer5a_orientation",
                "Runtime_backed_shell_chrome_stays_enabled_after_runner_load",
                "Standalone_toolstrip_buttons_raise_expected_events",
                "Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events",
                "Standalone_workspace_strip_quick_start_button_raises_expected_event",
                "Standalone_summary_header_tab_buttons_raise_expected_events",
                "Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events",
                "Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions",
                "Standalone_coach_sidecar_copy_button_raises_event_when_launch_uri_is_available",
                "Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end",
                "Loaded_runner_header_stays_tab_panel_only_without_metric_cards",
                "Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters",
                "Workspace_strip_quick_start_hides_after_runtime_backed_runner_load",
                "Loaded_runner_workbench_preserves_legacy_frmcareer_landmarks",
                "Character_creation_preserves_familiar_dense_builder_rhythm",
                "Advancement_and_karma_journal_workflows_preserve_familiar_progression_rhythm",
                "Gear_builder_preserves_familiar_browse_detail_confirm_rhythm",
                "Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm",
                "Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues",
                "Contacts_diary_and_support_routes_execute_with_public_path_visibility",
                "Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions",
                "Matrix_workflows_execute_with_specific_dialog_fields_and_confirm_actions"
            ],
            "requiredLifecycleTests": required_lifecycle_runtime_tests,
        },
        "blazor-desktop": {
            "status": "pass",
            "testSuites": [
                "BlazorShellComponentTests",
                "DualHeadAcceptanceTests"
            ],
            "shellChrome": "pass",
            "commandSurface": "pass",
            "dialogSurface": "pass",
            "journeyPanels": "pass",
            "releaseLifecycle": "pass",
            "sourceTestFile": blazor_shell_tests_path,
            "requiredShellTests": required_blazor_shell_tests,
            "requiredLifecycleTests": required_lifecycle_runtime_tests,
        },
    },
    "desktopLifecycleProof": {
        "status": "pass",
        "requiredLifecycleTests": required_lifecycle_runtime_tests,
        "desktopUpdateRuntimeTestsPath": desktop_update_runtime_tests_path,
        "desktopInstallLinkingRuntimeTestsPath": desktop_install_linking_runtime_tests_path,
        "desktopStartupSmokeRuntimeTestsPath": desktop_startup_smoke_runtime_tests_path,
        "startupWorkbenchSurvivalReceiptPath": startup_workbench_survival_receipt_path,
        "designMirrorCompletenessReceiptPath": design_mirror_completeness_receipt_path,
    },
    "workflowEquivalenceProof": {
        "status": receipt_status(workflow_parity_receipt),
        "sr4Sr6EffectiveStatus": receipt_status(sr4_sr6_frontier_receipt),
        "humanSideRuleAuthorityApproval": human_side_rule_authority_receipt,
        "sourceTestFile": dual_head_tests_path,
        "explicitParityReceiptPath": workflow_parity_receipt_path,
        "explicitSr4ParityReceiptPath": sr4_workflow_parity_receipt_path,
        "explicitSr6ParityReceiptPath": sr6_workflow_parity_receipt_path,
        "explicitSr6RulesetSophisticationReceiptPath": sr6_ruleset_ui_sophistication_receipt_path,
        "designMirrorCompletenessReceiptPath": design_mirror_completeness_receipt_path,
        "designAuthorizedParitySofteningReceiptPath": design_authorized_parity_softening_receipt_path,
        "explicitSr4Sr6FrontierReceiptPath": sr4_sr6_frontier_receipt_path,
        "desktopWorkflowExecutionReceiptPath": desktop_workflow_execution_receipt_path,
        "requiredDualHeadTests": required_full_workflow_tests,
        "legacyWorkflowFamilies": [
            "create-open-import-save-save-as-print-export",
            "metatype-priorities-karma-entry",
            "attributes-skills-skill-groups-specializations-knowledge-languages",
            "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources",
            "armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers",
            "cyberware-bioware-modular-hierarchies-nested-plugins",
            "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms",
            "improvements-explain-result-parity",
            "recovery-reload-migration-roundtrips",
            "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
        ],
    },
    "directImportRouteProof": {
        "reviewJobs": [
            "translator_xml_custom_data",
            "hero_lab_import_oracle",
        ],
        "screenshots": [
            "38-translator-dialog-light.png",
            "39-xml-editor-dialog-light.png",
            "40-hero-lab-importer-dialog-light.png",
        ],
        "characterOverviewPresenterTests": [
            "ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture",
            "ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture",
            "ExecuteCommandAsync_hero_lab_importer_opens_dialog_with_import_oracle_lane_posture",
        ],
    },
    "uiElementParityAuditProof": {
        "status": ui_element_parity_audit_effective_status,
        "sourceStatus": ui_element_parity_audit_source_status,
        "effectiveStatus": ui_element_parity_audit_effective_status,
        "routeLocalOnly": ui_element_parity_route_local_only,
        "uiElementParityAuditReceiptPath": ui_element_parity_audit_receipt_path,
        "visualNoCount": ui_element_visual_no_count,
        "behavioralNoCount": ui_element_behavioral_no_count,
        "nonPassingRowIds": ui_element_nonpassing_row_ids,
        "coverageGapKeys": ui_element_coverage_gap_keys,
        "denseBuilderRouteLocalEvidence": dense_builder_route_local_evidence,
        "requiredDenseBuilderRouteLocalEvidenceSuffixes": required_dense_builder_route_local_evidence_suffixes,
        "downstreamDenseBuilderRouteLocalEvidenceSuffixes": downstream_dense_builder_route_local_evidence_suffixes,
        "missingDenseBuilderRouteLocalEvidenceSuffixes": missing_dense_builder_route_local_evidence_suffixes,
        "routeLocalExpectedRowIds": sorted(ui_element_route_local_expected_row_ids),
        "routeLocalRowProofs": ui_element_route_local_row_proofs,
        "directImportRouteProofReceiptPath": direct_import_route_proof_receipt_path,
        "directWorkflowRouteProofReceiptPath": direct_workflow_route_proof_receipt_path,
        "directOutputRouteProofReceiptPath": direct_output_route_proof_receipt_path,
        "publicEdgeWorkbenchReceiptPath": os.path.join(published_root, "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"),
        "publicEdgeWorkbenchReceiptChecks": public_edge_workbench_receipt_checks,
        "browserLaneProofSetReceiptPath": browser_lane_proof_set_receipt_path,
        "browserLaneProofSetReceiptChecks": browser_lane_proof_set_checks,
        "playSurfaceHorizonReceiptPath": play_surface_horizon_receipt_path,
        "playSurfaceHorizonReceiptChecks": play_surface_horizon_checks,
    },
    "playSurfaceHorizonProof": {
        "status": str(play_surface_horizon_receipt.get("status") or "").strip(),
        "receiptPath": play_surface_horizon_receipt_path,
        "checks": play_surface_horizon_checks,
        "currentReleaseTruth": play_surface_horizon_receipt.get("current_release_truth") or {},
        "horizonIds": sorted(play_surface_horizon_ids),
    },
    "sr5Sr6UiParityAuditProof": {
        "status": str(sr5_sr6_ui_parity_audit_receipt.get("status") or "").strip(),
        "receiptPath": sr5_sr6_ui_parity_audit_receipt_path,
        "legacyTabCount": (sr5_sr6_ui_parity_audit_receipt.get("evidence") or {}).get("legacyTabCount"),
        "legacyControlCount": (sr5_sr6_ui_parity_audit_receipt.get("evidence") or {}).get("legacyControlCount"),
        "legacyElementDispositionCount": (sr5_sr6_ui_parity_audit_receipt.get("evidence") or {}).get("legacyElementDispositionCount"),
        "partialTabCount": (sr5_sr6_ui_parity_audit_receipt.get("evidence") or {}).get("partialTabCount"),
        "missingTabCount": (sr5_sr6_ui_parity_audit_receipt.get("evidence") or {}).get("missingTabCount"),
        "partialControlCount": (sr5_sr6_ui_parity_audit_receipt.get("evidence") or {}).get("partialControlCount"),
        "missingControlCount": (sr5_sr6_ui_parity_audit_receipt.get("evidence") or {}).get("missingControlCount"),
        "missingLegacyElementDispositionCount": (sr5_sr6_ui_parity_audit_receipt.get("evidence") or {}).get("missingLegacyElementDispositionCount"),
        "familyFallbackLegacyElementDispositionCount": (sr5_sr6_ui_parity_audit_receipt.get("evidence") or {}).get("familyFallbackLegacyElementDispositionCount"),
        "nonPendantMappedCurrentIdCount": (sr5_sr6_ui_parity_audit_receipt.get("evidence") or {}).get("nonPendantMappedCurrentIdCount"),
        "legacyElementsMissingExplicitSr6Pendants": (sr5_sr6_ui_parity_audit_receipt.get("evidence") or {}).get("legacyElementsMissingExplicitSr6Pendants"),
        "unsupportedMappedCurrentIdCount": (sr5_sr6_ui_parity_audit_receipt.get("evidence") or {}).get("unsupportedMappedCurrentIdCount"),
        "providerParityTests": sr5_sr6_ui_parity_audit_receipt.get("providerParityTests") or [],
    },
    "desktopExecutableProof": {
        "status": desktop_executable_exit_gate_status,
        "effectiveStatus": desktop_executable_exit_gate_effective_status,
        "routeLocalOnly": desktop_executable_exit_gate_route_local_only,
        "desktopExecutableExitGateReceiptPath": desktop_executable_exit_gate_receipt_path,
        "localBlockingFindings": desktop_executable_exit_gate_local_blocking_findings,
        "reasons": desktop_executable_exit_gate_receipt.get("reasons") or [],
    },
    "flagshipReadinessProof": {
        "status": flagship_readiness_effective_status,
        "sourceVerdict": flagship_readiness_status,
        "effectiveStatus": flagship_readiness_effective_status,
        "routeLocalOnly": flagship_readiness_route_local_only,
        "flagshipProductReadinessReceiptPath": flagship_product_readiness_receipt_path,
        "coverage": flagship_readiness_coverage,
        "openCoverageKeys": flagship_readiness_open_coverage_keys,
    },
    "localizationReleaseProof": {
        "status": receipt_status(localization_release_gate_receipt),
        "localizationReleaseGateReceiptPath": localization_release_gate_receipt_path,
        "interactiveControlInventoryReceiptPath": interactive_control_inventory_receipt_path,
        "startupWorkbenchSurvivalReceiptPath": startup_workbench_survival_receipt_path,
        "designMirrorCompletenessReceiptPath": design_mirror_completeness_receipt_path,
        "translationBacklogFindings": localization_release_gate_receipt.get("translation_backlog_findings") or [],
    },
    "visualReviewEvidence": {
        "screenshotControlEvidencePath": screenshot_control_canonical_path,
        "screenshotControlSha256": screenshot_control_sha256,
        "screenshotControlSizeBytes": screenshot_control_size_bytes,
        "screenshotControlGeneratedAt": screenshot_control_generated_at,
        "screenshotControlSchemaVersion": screenshot_control_schema_version,
        "screenshotCount": screenshot_count,
        "screenshotPackSha256": screenshot_pack_sha256,
        "screenshotPackDigestAlgorithm": pack_digest_algorithm,
        "screenshotDirectory": screenshot_dir,
        "expectedScreenshots": expected_screenshots,
        "capturedScreenshots": captured,
        "workflowScreenshotCoverageStatus": workflow_screenshot_coverage_status,
        "requiredWorkflowFamilyIds": required_workflow_family_ids,
        "workflowScreenshotCoverage": workflow_screenshot_coverage,
    },
    "signoffLane": {
        "workbenchReleaseSignoffPath": signoff_path,
        "citesReleaseGate": True,
    },
}
if top_level_status != "pass":
    raise SystemExit(
        "[b14] FAIL: flagship UI release gate is not passed: "
        + "; ".join(blocking_findings or ["missing reason"])
    )
atomic_write_json(receipt_path, payload)
PY

python3 - <<'PY' "$receipt_path"
import json
import sys
from pathlib import Path

receipt_path = Path(sys.argv[1])
if not receipt_path.is_file() or receipt_path.is_symlink():
    raise SystemExit("[b14] FAIL: passing flagship receipt was not atomically published")
receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
if (
    not isinstance(receipt, dict)
    or receipt.get("contract_name") != "chummer6-ui.flagship_ui_release_gate"
    or str(receipt.get("status") or "").strip().lower()
    not in {"pass", "passed", "ready"}
):
    raise SystemExit("[b14] FAIL: published flagship receipt is not a passing governed receipt")
PY

if [[ "$skip_downstream_receipt_materialization" == "0" ]]; then
  echo "[b14] refreshing desktop visual familiarity exit gate..."
  CHUMMER_DESKTOP_VISUAL_RELEASE_CHANNEL_PATH="$release_channel_path" \
  CHUMMER_DESKTOP_VISUAL_SKIP_RELEASE_GATE_LOCK_WAIT=1 \
  CHUMMER_DESKTOP_VISUAL_SKIP_FLAGSHIP_GATE_DEPENDENCY=1 \
  CHUMMER_DESKTOP_VISUAL_REFRESH_DOWNSTREAM_READINESS=0 \
  CHUMMER_DESKTOP_VISUAL_SKIP_DOWNSTREAM_READINESS=1 \
    bash scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh >/dev/null

  echo "[b14] refreshing Chummer5a screenshot review gate..."
  CHUMMER_SCREENSHOT_REVIEW_SKIP_FLAGSHIP_GATE_DEPENDENCY=1 \
    bash scripts/ai/milestones/chummer5a-screenshot-review-gate.sh >/dev/null

  echo "[b14] refreshing direct import route proof..."
  CHUMMER_NEXT90_M141_SKIP_FLAGSHIP_GATE_DEPENDENCY=1 \
    bash scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh >/dev/null

  echo "[b14] materializing desktop workflow execution gate..."
  python3 scripts/materialize-verified-release-channel-mirror.py >/dev/null || true
  desktop_workflow_release_channel_path="$release_channel_path"
  CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH="$desktop_workflow_release_channel_path" \
  CHUMMER_DESKTOP_WORKFLOW_REFRESH_DEPENDENCY_RECEIPTS=0 \
  CHUMMER_DESKTOP_WORKFLOW_SKIP_FLAGSHIP_DEPENDENCY_REFRESH=1 \
    bash "$desktop_workflow_execution_gate_script_path" >/dev/null

  echo "[b14] materializing classic dense workbench posture gate..."
  bash scripts/ai/milestones/classic-dense-workbench-posture-gate.sh >/dev/null

  echo "[b14] materializing veteran task-time evidence gate..."
  CHUMMER_VETERAN_TASK_TIME_SKIP_FLAGSHIP_GATE_DEPENDENCY=1 \
    bash scripts/ai/milestones/veteran-task-time-evidence-gate.sh >/dev/null

  echo "[b14] re-materializing Chummer5a screenshot review gate..."
  CHUMMER_SCREENSHOT_REVIEW_SKIP_FLAGSHIP_GATE_DEPENDENCY=1 \
    bash scripts/ai/milestones/chummer5a-screenshot-review-gate.sh >/dev/null

  echo "[b14] materializing desktop executable exit gate..."
  python3 scripts/materialize-verified-release-channel-mirror.py >/dev/null || true
  desktop_executable_release_channel_path="$release_channel_path"
  CHUMMER_DESKTOP_EXECUTABLE_SKIP_RELEASE_GATE_LOCK_WAIT=1 \
  CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE=1 \
  CHUMMER_DESKTOP_EXECUTABLE_ALLOW_VERIFY_RELEASE_CHANNEL_OVERRIDE=1 \
  CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH="$desktop_executable_release_channel_path" \
  CHUMMER_LINUX_DESKTOP_EXIT_GATE_SKIP_DESIGN_SUPERVISOR_REFRESH=1 \
    bash "$desktop_executable_exit_gate_script_path" >/dev/null

  echo "[b14] refreshing direct output route proof..."
  CHUMMER_NEXT90_M143_CANONICAL_UI_ROOT="$(realpath "$repo_root")" \
  CHUMMER_NEXT90_M143_SKIP_FLAGSHIP_GATE_DEPENDENCY=1 \
    bash scripts/ai/milestones/next90-m143-ui-direct-output-proof-check.sh >/dev/null
else
  echo "[b14] skipping downstream proof materialization for screenshot refresh-only pass..."
fi

if [[ "$skip_downstream_receipt_materialization" == "0" \
  && "$refresh_flagship_readiness" == "1" \
  && "$skip_flagship_readiness_refresh" == "0" ]]; then
  echo "[b14] refreshing Fleet flagship product readiness..."
  python3 "$flagship_product_readiness_materializer_path" >/dev/null
else
  echo "[b14] Fleet flagship product readiness refresh not opted in (or explicitly skipped)."
fi

if [[ "$skip_downstream_receipt_materialization" == "0" ]]; then
python3 - <<'PY' "$receipt_path" "$veteran_task_time_receipt_path" "$chummer5a_screenshot_review_receipt_path" "$classic_dense_workbench_receipt_path" "$repo_root" "$flagship_product_readiness_receipt_path" "$refresh_flagship_readiness" "$skip_flagship_readiness_refresh"
import hashlib
import json
import os
import sys
import tempfile
from datetime import datetime, timedelta, timezone
from pathlib import Path

receipt_path = Path(sys.argv[1])
veteran_task_time_receipt_path = Path(sys.argv[2])
chummer5a_screenshot_review_receipt_path = Path(sys.argv[3])
classic_dense_workbench_receipt_path = Path(sys.argv[4])
repo_root = Path(sys.argv[5])
flagship_product_readiness_receipt_path = Path(sys.argv[6])
readiness_was_refreshed = sys.argv[7] == "1" and sys.argv[8] == "0"
published_root = repo_root / ".codex-studio" / "published"


def parse_offset_timestamp(value: object, label: str) -> datetime:
    raw = str(value or "").strip()
    if not raw:
        raise SystemExit(f"[b14] FAIL: {label} is missing generatedAt/generated_at")
    try:
        parsed = datetime.fromisoformat(raw.replace("Z", "+00:00"))
    except ValueError as exc:
        raise SystemExit(f"[b14] FAIL: {label} has an invalid generatedAt/generated_at") from exc
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise SystemExit(f"[b14] FAIL: {label} generatedAt/generated_at must include a UTC offset")
    return parsed.astimezone(timezone.utc)


if not receipt_path.is_file() or receipt_path.is_symlink():
    raise SystemExit("[b14] FAIL: provisional flagship receipt is not a regular file")
receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
if not isinstance(receipt, dict) or str(receipt.get("status") or "").strip().lower() != "pass":
    raise SystemExit("[b14] FAIL: provisional flagship receipt is not passing")
base_generated_at = parse_offset_timestamp(receipt.get("generatedAt"), "provisional flagship receipt")
now = datetime.now(timezone.utc)
max_age = timedelta(seconds=int(os.environ.get("CHUMMER_FLAGSHIP_UI_SUPPORTING_PROOF_MAX_AGE_SECONDS") or "86400"))
max_future_skew = timedelta(seconds=int(os.environ.get("CHUMMER_FLAGSHIP_UI_SUPPORTING_PROOF_MAX_FUTURE_SKEW_SECONDS") or "300"))

supporting_specs = {
    "desktopVisualFamiliarity": (
        published_root / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json",
        "chummer6-ui.desktop_visual_familiarity_exit_gate",
        True,
        True,
        True,
    ),
    "chummer5aScreenshotReview": (
        chummer5a_screenshot_review_receipt_path,
        "chummer6-ui.chummer5a_screenshot_review_gate",
        True,
        True,
        True,
    ),
    "directImportRoute": (
        published_root / "NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json",
        "chummer6-ui.next90_m141_ui_direct_import_route_proof",
        True,
        True,
        True,
    ),
    "desktopWorkflowExecution": (
        published_root / "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json",
        "chummer6-ui.desktop_workflow_execution_gate",
        True,
        True,
        True,
    ),
    "classicDenseWorkbench": (
        classic_dense_workbench_receipt_path,
        "chummer6-ui.classic_dense_workbench_posture_gate",
        True,
        False,
        False,
    ),
    "veteranTaskTime": (
        veteran_task_time_receipt_path,
        "chummer6-ui.veteran_task_time_evidence_gate",
        True,
        False,
        False,
    ),
    "desktopExecutable": (
        published_root / "DESKTOP_EXECUTABLE_EXIT_GATE.generated.json",
        "chummer6-ui.desktop_executable_exit_gate",
        True,
        True,
        True,
    ),
    "directOutputRoute": (
        published_root / "NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json",
        "chummer6-ui.next90_m143_ui_direct_output_proof",
        True,
        False,
        False,
    ),
    "flagshipReadiness": (
        flagship_product_readiness_receipt_path,
        "fleet.flagship_product_readiness",
        readiness_was_refreshed,
        False,
        False,
    ),
}
supporting_payloads = {}
supporting_evidence = {}
for label, (
    path,
    expected_contract,
    must_follow_base,
    must_match_channel,
    must_match_version,
) in supporting_specs.items():
    if not path.is_file() or path.is_symlink():
        raise SystemExit(f"[b14] FAIL: downstream {label} receipt is missing or not a regular file: {path}")
    receipt_bytes = path.read_bytes()
    try:
        payload = json.loads(receipt_bytes.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise SystemExit(f"[b14] FAIL: downstream {label} receipt is unreadable: {exc}") from exc
    if not isinstance(payload, dict):
        raise SystemExit(f"[b14] FAIL: downstream {label} receipt root is not an object")
    if str(payload.get("status") or "").strip().lower() not in {"pass", "passed", "ready"}:
        raise SystemExit(f"[b14] FAIL: downstream {label} receipt is not passing")
    contract_values = [
        payload[key]
        for key in ("contract_name", "contractName")
        if key in payload
    ]
    if not contract_values or any(value != expected_contract for value in contract_values):
        raise SystemExit(f"[b14] FAIL: downstream {label} receipt contract is invalid")
    if label == "flagshipReadiness":
        readiness_schema_values = [
            payload[key]
            for key in ("schemaVersion", "schema_version")
            if key in payload
        ]
        if not readiness_schema_values or any(
            type(value) is not int or value != 1 for value in readiness_schema_values
        ):
            raise SystemExit("[b14] FAIL: downstream flagshipReadiness schema is invalid")
    channel_values = [
        str(payload[key]).strip()
        for key in ("channelId", "channel")
        if key in payload
    ]
    version_values = [
        str(payload[key]).strip()
        for key in ("releaseVersion", "version")
        if key in payload
    ]
    flagship_channel = str(receipt.get("channelId") or "").strip()
    flagship_version = str(receipt.get("releaseVersion") or "").strip()
    if must_match_channel and (
        not channel_values
        or any(not value or value.lower() != flagship_channel.lower() for value in channel_values)
    ):
        raise SystemExit(f"[b14] FAIL: downstream {label} release channel is invalid")
    if must_match_version and (
        not version_values
        or any(not value or value != flagship_version for value in version_values)
    ):
        raise SystemExit(f"[b14] FAIL: downstream {label} release version is invalid")
    generated_at = parse_offset_timestamp(
        payload.get("generatedAt") or payload.get("generated_at"),
        f"downstream {label} receipt",
    )
    if generated_at > now + max_future_skew or now - generated_at > max_age:
        raise SystemExit(f"[b14] FAIL: downstream {label} receipt is outside the freshness window")
    if must_follow_base and generated_at < base_generated_at - max_future_skew:
        raise SystemExit(f"[b14] FAIL: downstream {label} receipt predates this flagship run")
    supporting_payloads[label] = payload
    supporting_evidence[label] = {
        "path": str(path),
        "contract_name": contract_values[0],
        "channelId": channel_values[0] if channel_values else None,
        "releaseVersion": version_values[0] if version_values else None,
        "status": str(payload.get("status") or "").strip().lower(),
        "generatedAt": generated_at.isoformat().replace("+00:00", "Z"),
        "sha256": hashlib.sha256(receipt_bytes).hexdigest(),
        "sizeBytes": len(receipt_bytes),
    }

veteran_receipt = supporting_payloads["veteranTaskTime"]
chummer5a_screenshot_review_receipt = supporting_payloads["chummer5aScreenshotReview"]
classic_dense_receipt = supporting_payloads["classicDenseWorkbench"]
veteran_receipt_status = supporting_evidence["veteranTaskTime"]["status"]
chummer5a_screenshot_review_status = supporting_evidence["chummer5aScreenshotReview"]["status"]
classic_dense_receipt_status = supporting_evidence["classicDenseWorkbench"]["status"]
receipt["classicDenseWorkbenchPostureProof"] = {
    "status": classic_dense_receipt_status,
    "classicDenseWorkbenchPostureReceiptPath": str(classic_dense_workbench_receipt_path),
    "frontierIdsClosed": classic_dense_receipt.get("frontierIdsClosed") or [],
    "evidence": classic_dense_receipt.get("evidence") or {},
}
receipt["veteranTaskTimeEvidenceProof"] = {
    "status": veteran_receipt_status,
    "veteranTaskTimeEvidenceReceiptPath": str(veteran_task_time_receipt_path),
    "frontierIdsClosed": veteran_receipt.get("frontierIdsClosed") or [],
    "taskTimeEvidence": veteran_receipt.get("taskTimeEvidence") or {},
    "boundedBlazorFallbackEvidence": veteran_receipt.get("boundedBlazorFallbackEvidence") or {},
}
receipt["chummer5aScreenshotReviewProof"] = {
    "status": chummer5a_screenshot_review_status,
    "screenshotReviewReceiptPath": str(chummer5a_screenshot_review_receipt_path),
    "frontierIdsClosed": chummer5a_screenshot_review_receipt.get("frontierIdsClosed") or [],
    "reviewJobs": chummer5a_screenshot_review_receipt.get("reviewJobs") or {},
}
desktop_executable_receipt = supporting_payloads["desktopExecutable"]
desktop_local_blockers = list(
    desktop_executable_receipt.get("localBlockingFindings")
    or desktop_executable_receipt.get("local_blocking_findings")
    or []
)
if desktop_local_blockers:
    raise SystemExit(
        "[b14] FAIL: refreshed desktop executable proof still has local blockers: "
        + "; ".join(str(item) for item in desktop_local_blockers)
    )
readiness_receipt = supporting_payloads["flagshipReadiness"]
readiness_coverage = readiness_receipt.get("coverage")
if not isinstance(readiness_coverage, dict) or not readiness_coverage:
    raise SystemExit("[b14] FAIL: refreshed flagship readiness proof has no coverage map")
readiness_open_keys = sorted(
    str(key)
    for key, value in readiness_coverage.items()
    if str(value or "").strip().lower() not in {"ready", "pass", "passed"}
)
if readiness_open_keys:
    raise SystemExit(
        "[b14] FAIL: refreshed flagship readiness proof has open coverage keys: "
        + ", ".join(readiness_open_keys)
    )
receipt["desktopExecutableProof"] = {
    "status": supporting_evidence["desktopExecutable"]["status"],
    "effectiveStatus": supporting_evidence["desktopExecutable"]["status"],
    "routeLocalOnly": False,
    "desktopExecutableExitGateReceiptPath": supporting_evidence["desktopExecutable"]["path"],
    "localBlockingFindings": desktop_local_blockers,
    "reasons": desktop_executable_receipt.get("reasons") or [],
    "receiptSha256": supporting_evidence["desktopExecutable"]["sha256"],
    "receiptSizeBytes": supporting_evidence["desktopExecutable"]["sizeBytes"],
    "receiptGeneratedAt": supporting_evidence["desktopExecutable"]["generatedAt"],
}
receipt["flagshipReadinessProof"] = {
    "status": supporting_evidence["flagshipReadiness"]["status"],
    "sourceVerdict": supporting_evidence["flagshipReadiness"]["status"],
    "effectiveStatus": supporting_evidence["flagshipReadiness"]["status"],
    "routeLocalOnly": False,
    "flagshipProductReadinessReceiptPath": supporting_evidence["flagshipReadiness"]["path"],
    "coverage": readiness_coverage,
    "openCoverageKeys": readiness_open_keys,
    "receiptSha256": supporting_evidence["flagshipReadiness"]["sha256"],
    "receiptSizeBytes": supporting_evidence["flagshipReadiness"]["sizeBytes"],
    "receiptGeneratedAt": supporting_evidence["flagshipReadiness"]["generatedAt"],
}
receipt["downstreamReceiptProofs"] = supporting_evidence
receipt["baseGeneratedAt"] = receipt.get("generatedAt")
receipt["finalizedAt"] = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
receipt["generatedAt"] = receipt["finalizedAt"]
if receipt_path.is_symlink():
    raise SystemExit(f"[b14] FAIL: refusing to replace symlink receipt path: {receipt_path}")
receipt_bytes = (json.dumps(receipt, indent=2) + "\n").encode("utf-8")
fd, temporary_name = tempfile.mkstemp(
    prefix=f".{receipt_path.name}.",
    suffix=".tmp",
    dir=receipt_path.parent,
)
temporary_path = Path(temporary_name)
try:
    with os.fdopen(fd, "wb") as handle:
        handle.write(receipt_bytes)
        handle.flush()
        os.fchmod(handle.fileno(), 0o644)
        os.fsync(handle.fileno())
    os.replace(temporary_path, receipt_path)
    directory_fd = os.open(receipt_path.parent, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0))
    try:
        os.fsync(directory_fd)
    finally:
        os.close(directory_fd)
except BaseException:
    temporary_path.unlink(missing_ok=True)
    raise
PY
else
  echo "[b14] downstream proof augmentation skipped with downstream materialization."
fi

# Retain the prior pack and prior flagship receipt until every requested
# downstream materializer and augmentation has completed successfully.
if (( ${#fanout_target_paths[@]} > 0 )); then
  manage_screenshot_pack_transaction seal-fanout
fi
manage_screenshot_pack_transaction commit

echo "[b14] PASS"
