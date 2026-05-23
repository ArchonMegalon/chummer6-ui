#!/usr/bin/env python3
from __future__ import annotations

"""Refresh 1min credits through codexea and append a timestamped history row."""

import csv
import json
import os
import shutil
import subprocess
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


DEFAULT_HISTORY_PATH = Path("/docker/fleet/state/onemin_credit_history.csv")
DEFAULT_RUNTIME_ROOT = Path("/docker/fleet/state/browseract_bootstrap/runtime")
DEFAULT_LATEST_AGGREGATE_FILENAME = "onemin_aggregate_billing_full_refresh_latest.json"
DEFAULT_BROWSERACT_REFRESH_STATE_ROOT = Path("/docker/EA/state")
DEFAULT_BROWSERACT_MAX_AGE_SECONDS = 6 * 60 * 60
FIELDNAMES = (
    "recorded_at_local",
    "recorded_at_utc",
    "measurement_trust",
    "payload_source",
    "source_recorded_at_utc",
    "source_age_seconds",
    "free_credits",
    "max_credits",
    "percent_remaining",
    "slot_count",
    "owner_mapped_slot_count",
    "ready_ok_count",
    "depleted_count",
    "basis_summary",
    "last_probe_at_utc",
    "actual_billing_account_count",
    "billing_note",
    "reported_free_credits",
    "sum_probe_estimated_credits",
    "sum_probe_available_credits",
    "slot_sum_free_credits",
    "slot_sum_max_credits",
    "free_credits_source",
    "raw_last_error",
    "current_pace_burn_credits_per_hour",
    "avg_daily_burn_credits_7d",
    "used_precomputed_aggregate",
    "delta_credits",
    "delta_seconds",
    "burn_rate_credits_per_hour",
    "burn_rate_credits_per_day",
    "burn_rate_source",
    "refresh_error",
)


def _history_path() -> Path:
    raw = str(os.environ.get("ONEMIN_CREDIT_HISTORY_PATH", "") or "").strip()
    return Path(raw) if raw else DEFAULT_HISTORY_PATH


def _runtime_root() -> Path:
    raw = str(os.environ.get("ONEMIN_AGGREGATE_RUNTIME_ROOT", "") or "").strip()
    return Path(raw) if raw else DEFAULT_RUNTIME_ROOT


def _latest_aggregate_filename() -> str:
    raw = str(os.environ.get("ONEMIN_AGGREGATE_LATEST_FILENAME", "") or "").strip()
    return raw or DEFAULT_LATEST_AGGREGATE_FILENAME


def _browseract_refresh_state_root() -> Path:
    raw = str(os.environ.get("ONEMIN_BROWSERACT_REFRESH_STATE_ROOT", "") or "").strip()
    return Path(raw) if raw else DEFAULT_BROWSERACT_REFRESH_STATE_ROOT


def _browseract_max_age_seconds() -> int:
    raw = str(os.environ.get("ONEMIN_BROWSERACT_MAX_AGE_SECONDS", "") or "").strip()
    if not raw:
        return DEFAULT_BROWSERACT_MAX_AGE_SECONDS
    try:
        return max(0, int(float(raw)))
    except ValueError:
        return DEFAULT_BROWSERACT_MAX_AGE_SECONDS


def _coerce_int(value: Any) -> int | None:
    if value is None or value == "":
        return None
    if isinstance(value, bool):
        return int(value)
    try:
        return int(float(str(value).strip()))
    except (TypeError, ValueError):
        return None


def _coerce_float(value: Any) -> float | None:
    if value is None or value == "":
        return None
    if isinstance(value, bool):
        return float(int(value))
    try:
        return float(str(value).strip())
    except (TypeError, ValueError):
        return None


def _safe_sum(values: list[int | None]) -> int | None:
    numbers = [value for value in values if value is not None]
    if not numbers:
        return None
    return sum(numbers)


def _atomic_write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", dir=path.parent, delete=False) as handle:
        handle.write(text)
        temp_path = Path(handle.name)
    temp_path.replace(path)


def _parse_iso(value: Any) -> datetime | None:
    text = str(value or "").strip()
    if not text:
        return None
    try:
        parsed = datetime.fromisoformat(text.replace("Z", "+00:00"))
    except ValueError:
        return None
    return parsed.astimezone(timezone.utc) if parsed.tzinfo else parsed.replace(tzinfo=timezone.utc)


def _latest_browseract_refresh_payload() -> dict[str, Any] | None:
    state_root = _browseract_refresh_state_root()
    if not state_root.exists():
        return None

    latest_success_by_account: dict[str, tuple[datetime, dict[str, Any]]] = {}
    latest_fallback_by_account: dict[str, tuple[datetime, dict[str, Any]]] = {}
    for path in sorted(state_root.glob("onemin_browseract_refresh*.json")):
        try:
            body = json.loads(path.read_text(encoding="utf-8"))
        except Exception:
            continue
        rows = body.get("results") or body.get("accounts") or []
        if not isinstance(rows, list):
            continue
        file_observed_at = _parse_iso(body.get("finished_at_utc")) or datetime.fromtimestamp(
            path.stat().st_mtime, tz=timezone.utc
        )
        for row in rows:
            if not isinstance(row, dict):
                continue
            account_label = str(row.get("account_label") or "").strip()
            if not account_label:
                continue
            observed_at = _parse_iso(((row.get("persisted_snapshot") or {}).get("observed_at")))
            status = str(row.get("status") or "").strip().lower()
            if observed_at is not None and status == "ok":
                previous_success = latest_success_by_account.get(account_label)
                if previous_success is None or observed_at >= previous_success[0]:
                    latest_success_by_account[account_label] = (observed_at, row)
                continue
            fallback_at = observed_at or file_observed_at
            previous_fallback = latest_fallback_by_account.get(account_label)
            if previous_fallback is None or fallback_at >= previous_fallback[0]:
                latest_fallback_by_account[account_label] = (fallback_at, row)

    latest_by_account: dict[str, tuple[datetime, dict[str, Any]]] = dict(latest_fallback_by_account)
    latest_by_account.update(latest_success_by_account)

    if not latest_by_account:
        return None

    ordered_rows = [item[1] for item in sorted(latest_by_account.values(), key=lambda item: item[0])]
    successes = [row for row in ordered_rows if str(row.get("status") or "").strip().lower() == "ok"]
    if not successes:
        return None
    failures = [row for row in ordered_rows if str(row.get("status") or "").strip().lower() != "ok"]

    sum_remaining = sum(int(_coerce_int(row.get("remaining_credits")) or 0) for row in successes)
    sum_max = _safe_sum([_coerce_int(row.get("max_credits")) for row in successes])
    daily_bonus_known = [_coerce_int(row.get("daily_bonus_credits")) for row in successes if _coerce_int(row.get("daily_bonus_credits")) is not None]
    daily_bonus_claimable = sum(daily_bonus_known) if daily_bonus_known else None
    latest_observed_at = max(item[0] for item in latest_by_account.values())
    failure_suffix = f", ui_lane_failure x{len(failures)}" if failures else ""

    return {
        "recorded_at_utc": latest_observed_at.replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        "sum_free_credits": sum_remaining,
        "free_credits": sum_remaining,
        "remaining_credits": sum_remaining,
        "total_remaining_credits": sum_remaining,
        "sum_max_credits": sum_max,
        "slot_count": len(ordered_rows),
        "slot_count_with_billing_snapshot": len(successes),
        "slot_count_with_positive_balance": sum(
            1 for row in successes if int(_coerce_int(row.get("remaining_credits")) or 0) > 0
        ),
        "basis_summary": f"actual_billing_usage_page x{len(successes)}{failure_suffix}",
        "sum_claimable_daily_bonus_credits": daily_bonus_claimable,
        "sum_free_credits_plus_claimable_daily_bonus": (
            sum_remaining + daily_bonus_claimable if daily_bonus_claimable is not None else None
        ),
        "browseract_refresh_success_count": len(successes),
        "browseract_refresh_failure_count": len(failures),
        "browseract_refresh_state_root": str(state_root),
        "used_browseract_refresh_summary": True,
        "used_precomputed_aggregate": False,
        "payload_source": "browseract_refresh_summary",
        "slots": [
            {
                "free_credits": _coerce_int(row.get("remaining_credits")),
                "max_credits": _coerce_int(row.get("max_credits")),
                "basis": row.get("basis") or "actual_billing_usage_page",
                "daily_bonus_available": row.get("daily_bonus_available"),
                "daily_bonus_credits": _coerce_int(row.get("daily_bonus_credits")),
                "account_label": row.get("account_label"),
            }
            for row in ordered_rows
        ],
    }


def _prefer_browseract_refresh_payload(payload: dict[str, Any], browseract_payload: dict[str, Any] | None) -> dict[str, Any]:
    if not browseract_payload:
        return payload
    payload_recorded_at = _parse_iso(payload.get("recorded_at_utc"))
    browseract_recorded_at = _parse_iso(browseract_payload.get("recorded_at_utc"))
    if browseract_recorded_at is None:
        return payload
    browseract_age_seconds = int((datetime.now(timezone.utc) - browseract_recorded_at).total_seconds())
    if browseract_age_seconds > _browseract_max_age_seconds():
        merged = dict(payload)
        merged.setdefault("ignored_browseract_payload_source", "browseract_refresh_summary")
        merged.setdefault("ignored_browseract_recorded_at_utc", browseract_payload.get("recorded_at_utc"))
        merged.setdefault("ignored_browseract_age_seconds", browseract_age_seconds)
        return merged
    if payload_recorded_at is not None and browseract_recorded_at <= payload_recorded_at and not bool(payload.get("used_precomputed_aggregate")):
        return payload
    merged = dict(payload)
    merged.update(browseract_payload)
    merged["payload_source"] = "browseract_refresh_summary"
    return merged


def _source_recorded_at(payload: dict[str, Any]) -> datetime | None:
    for key in ("recorded_at_utc", "payload_fetched_at", "last_probe_at_utc"):
        parsed = _parse_iso(payload.get(key))
        if parsed is not None:
            return parsed
    return None


def _measurement_trust(payload: dict[str, Any], *, source_age_seconds: int | None) -> str:
    payload_source = str(payload.get("payload_source") or "").strip()
    if source_age_seconds is None:
        return "unknown_source_time"
    if payload_source == "actual_provider_api_snapshot_rollup":
        return "fresh"
    if payload_source.endswith("_cache") or payload_source == "browseract_refresh_summary":
        if source_age_seconds is not None and source_age_seconds > _browseract_max_age_seconds():
            return "stale"
    if bool(payload.get("used_precomputed_aggregate")) and source_age_seconds is not None and source_age_seconds > _browseract_max_age_seconds():
        return "stale"
    if source_age_seconds is not None and source_age_seconds > _browseract_max_age_seconds():
        return "stale"
    return "fresh"


def _normalize_payload(payload: dict[str, Any]) -> dict[str, Any]:
    slots = [slot for slot in (payload.get("slots") or []) if isinstance(slot, dict)]
    probe = payload.get("probe") or {}
    probe_slots = [slot for slot in (probe.get("slots") or []) if isinstance(slot, dict)]

    reported_free_credits = _coerce_int(payload.get("sum_free_credits"))
    sum_max_credits = _coerce_int(payload.get("sum_max_credits"))
    slot_sum_free_credits = _safe_sum([_coerce_int(slot.get("free_credits")) for slot in slots])
    slot_sum_max_credits = _safe_sum([_coerce_int(slot.get("max_credits")) for slot in slots])
    sum_probe_estimated_credits = _coerce_int(payload.get("sum_probe_estimated_credits"))
    if sum_probe_estimated_credits is None:
        sum_probe_estimated_credits = _safe_sum(
            [_coerce_int(slot.get("estimated_remaining_credits")) for slot in probe_slots]
        )
    sum_probe_available_credits = _coerce_int(payload.get("sum_probe_available_credits"))
    if sum_probe_available_credits is None:
        sum_probe_available_credits = _safe_sum(
            [_coerce_int(slot.get("available_credits")) for slot in probe_slots]
        )

    free_credits = reported_free_credits
    free_credits_source = "reported_sum_free_credits"
    if free_credits in (None, 0):
        if slot_sum_free_credits not in (None, 0):
            free_credits = slot_sum_free_credits
            free_credits_source = "slot_sum_free_credits"
        elif sum_probe_estimated_credits is not None:
            free_credits = sum_probe_estimated_credits
            free_credits_source = "sum_probe_estimated_credits"
        elif sum_probe_available_credits is not None:
            free_credits = sum_probe_available_credits
            free_credits_source = "sum_probe_available_credits"
        else:
            free_credits = 0
            free_credits_source = "empty"

    max_credits = sum_max_credits
    if max_credits in (None, 0):
        max_credits = slot_sum_max_credits or 0

    percent_remaining = _coerce_float(payload.get("percent_remaining"))
    if percent_remaining is None and max_credits:
        percent_remaining = max(0.0, min(100.0, (float(free_credits) / float(max_credits)) * 100.0))

    return {
        "slots": slots,
        "probe_slots": probe_slots,
        "free_credits": free_credits,
        "free_credits_source": free_credits_source,
        "max_credits": max_credits,
        "percent_remaining": percent_remaining,
        "reported_free_credits": reported_free_credits,
        "slot_sum_free_credits": slot_sum_free_credits,
        "slot_sum_max_credits": slot_sum_max_credits,
        "sum_probe_estimated_credits": sum_probe_estimated_credits,
        "sum_probe_available_credits": sum_probe_available_credits,
    }


def _container_global_refresh_payload() -> dict[str, Any] | None:
    if not shutil.which("docker"):
        return None
    now_utc = datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    script = r"""
import json
import os
import requests

base_url = "http://127.0.0.1:8090"
principal = ""
for env_name in ("EA_OPERATOR_PRINCIPAL_IDS", "EA_OPERATOR_PRINCIPALS"):
    raw = str(os.environ.get(env_name) or "").strip()
    if not raw:
        continue
    for item in raw.split(","):
        item = str(item or "").strip()
        if item:
            principal = item
            break
    if principal:
        break
if not principal:
    principal = str(os.environ.get("EA_DEFAULT_PRINCIPAL_ID") or "").strip() or "codex-fleet"
headers = {"X-EA-Principal-ID": principal}
token = str(os.environ.get("EA_API_TOKEN") or "").strip()
if token:
    headers["Authorization"] = f"Bearer {token}"
refresh_payload = {
    "include_members": True,
    "capture_raw_text": True,
    "provider_api_all_accounts": True,
    "provider_api_continue_on_rate_limit": True,
}
refresh = requests.post(
    f"{base_url}/v1/providers/onemin/billing-refresh",
    headers=headers,
    json=refresh_payload,
    timeout=180,
)
refresh.raise_for_status()
refresh_json = refresh.json()
print(json.dumps({
    "principal_id": principal,
    "fetched_at_utc": "__NOW_UTC__",
    "billing_lookup": refresh_json,
    "global_aggregate_snapshot": refresh_json.get("global_aggregate_snapshot") or {},
}))
""".replace("__NOW_UTC__", now_utc)
    result = subprocess.run(
        ["docker", "exec", "-i", "ea-api", "python", "-c", script],
        check=False,
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        return None
    try:
        payload = json.loads(result.stdout)
    except json.JSONDecodeError:
        return None
    if not isinstance(payload, dict):
        return None
    aggregate = payload.get("global_aggregate_snapshot")
    if not isinstance(aggregate, dict) or not aggregate:
        return None
    merged = dict(aggregate)
    merged["billing_lookup"] = payload.get("billing_lookup") if isinstance(payload.get("billing_lookup"), dict) else {}
    merged["payload_source"] = "actual_provider_api_snapshot_rollup"
    merged["payload_fetched_at"] = str(payload.get("fetched_at_utc") or now_utc)
    merged["used_precomputed_aggregate"] = False
    return merged


def _should_use_container_global_refresh(payload: dict[str, Any]) -> bool:
    payload_source = str(payload.get("payload_source") or "").strip()
    if payload_source.endswith("_cache"):
        return True
    billing_lookup = payload.get("billing_lookup")
    if isinstance(billing_lookup, dict):
        global_snapshot = billing_lookup.get("global_aggregate_snapshot")
        if isinstance(global_snapshot, dict) and global_snapshot:
            return False
    return bool(payload.get("refresh_error"))


def load_payload() -> dict[str, Any]:
    browseract_payload = _latest_browseract_refresh_payload()
    env = os.environ.copy()
    env.setdefault("CODEXEA_STATUS_CONNECT_TIMEOUT_SECONDS", "5")
    env.setdefault("CODEXEA_ONEMIN_STATUS_TIMEOUT_SECONDS", "60")
    env.setdefault("CODEXEA_ONEMIN_BILLING_TIMEOUT_SECONDS", "180")
    result = subprocess.run(
        [
            "codexea",
            "--onemin-aggregate",
            "--refresh",
            "--billing",
            "--billing-full-refresh",
            "--json",
        ],
        check=False,
        capture_output=True,
        text=True,
        env=env,
    )
    if result.returncode != 0:
        if browseract_payload is not None:
            payload = dict(browseract_payload)
            payload["refresh_error"] = result.stderr.strip() or "codexea onemin aggregate refresh failed"
            return payload
        raise SystemExit(result.stderr.strip() or "codexea onemin aggregate refresh failed")
    try:
        payload = json.loads(result.stdout)
    except json.JSONDecodeError as exc:
        if browseract_payload is not None:
            payload = dict(browseract_payload)
            payload["refresh_error"] = f"codexea onemin aggregate refresh returned invalid JSON: {exc}"
            return payload
        raise SystemExit(f"codexea onemin aggregate refresh returned invalid JSON: {exc}") from exc
    if not isinstance(payload, dict):
        if browseract_payload is not None:
            payload = dict(browseract_payload)
            payload["refresh_error"] = "codexea onemin aggregate refresh returned a non-object payload"
            return payload
        raise SystemExit("codexea onemin aggregate refresh returned a non-object payload")
    data = payload.get("data")
    if isinstance(data, dict):
        envelope = {
            key: payload.get(key)
            for key in (
                "message",
                "payload_source",
                "payload_fetched_at",
                "status_error",
                "profiles_error",
                "source_notice",
                "exit_code",
                "ok",
            )
            if payload.get(key) not in (None, "")
        }
        envelope.update(data)
        payload = envelope
    payload = _prefer_browseract_refresh_payload(payload, browseract_payload)
    if _should_use_container_global_refresh(payload):
        container_payload = _container_global_refresh_payload()
        if container_payload is not None:
            return container_payload
    return payload


def _read_previous_history_row(path: Path) -> dict[str, str] | None:
    if not path.exists():
        return None
    try:
        with path.open("r", encoding="utf-8", newline="") as handle:
            rows = list(csv.DictReader(handle))
    except OSError:
        return None
    if not rows:
        return None
    return rows[-1]


def append_history(*, history_path: Path, row: dict[str, Any]) -> None:
    history_path.parent.mkdir(parents=True, exist_ok=True)
    existing_rows: list[dict[str, str]] = []
    if history_path.exists():
        with history_path.open("r", encoding="utf-8", newline="") as handle:
            existing_rows = list(csv.DictReader(handle))
    with history_path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=FIELDNAMES)
        writer.writeheader()
        for existing in existing_rows:
            writer.writerow({field: existing.get(field, "") for field in FIELDNAMES})
        writer.writerow({field: row.get(field, "") for field in FIELDNAMES})


def write_runtime_aggregate(
    *,
    runtime_root: Path,
    latest_filename: str,
    recorded_at_utc: str,
    payload: dict[str, Any],
    normalized: dict[str, Any],
    history_path: Path,
) -> tuple[Path, Path]:
    runtime_root.mkdir(parents=True, exist_ok=True)
    archive_name = (
        "onemin_aggregate_billing_full_refresh_"
        + recorded_at_utc.replace("-", "").replace(":", "").replace("T", "T").replace("Z", "Z")
        + ".json"
    )
    latest_path = runtime_root / latest_filename
    archive_path = runtime_root / archive_name
    aggregate_payload = dict(payload)
    aggregate_payload.update(
        {
            "recorded_at_utc": recorded_at_utc,
            "refresh_mode": "billing_full_refresh",
            "history_path": str(history_path),
            "free_credits": normalized["free_credits"],
            "remaining_credits": normalized["free_credits"],
            "total_remaining_credits": normalized["free_credits"],
            "sum_free_credits": normalized["free_credits"],
            "sum_max_credits": normalized["max_credits"],
            "percent_remaining": normalized["percent_remaining"],
            "slot_sum_free_credits": normalized["slot_sum_free_credits"],
            "slot_sum_max_credits": normalized["slot_sum_max_credits"],
            "sum_probe_estimated_credits": normalized["sum_probe_estimated_credits"],
            "sum_probe_available_credits": normalized["sum_probe_available_credits"],
            "free_credits_source": normalized["free_credits_source"],
        }
    )
    text = json.dumps(aggregate_payload, indent=2, sort_keys=True) + "\n"
    _atomic_write_text(latest_path, text)
    _atomic_write_text(archive_path, text)
    return latest_path, archive_path


def main() -> int:
    payload = load_payload()
    normalized = _normalize_payload(payload)
    now = datetime.now(timezone.utc)
    recorded_at_utc = now.replace(microsecond=0).isoformat().replace("+00:00", "Z")
    recorded_at_local = datetime.now().astimezone().replace(microsecond=0).isoformat()
    source_recorded_at_dt = _source_recorded_at(payload)
    source_recorded_at_utc = (
        source_recorded_at_dt.replace(microsecond=0).isoformat().replace("+00:00", "Z")
        if source_recorded_at_dt is not None
        else ""
    )
    source_age_seconds = (
        int((now - source_recorded_at_dt.astimezone(timezone.utc)).total_seconds())
        if source_recorded_at_dt is not None
        else None
    )
    measurement_trust = _measurement_trust(payload, source_age_seconds=source_age_seconds)
    payload = {
        **payload,
        "measurement_trust": measurement_trust,
        "source_recorded_at_utc": source_recorded_at_utc,
        "source_age_seconds": source_age_seconds,
    }

    history_path = _history_path()
    runtime_root = _runtime_root()
    latest_filename = _latest_aggregate_filename()
    previous = _read_previous_history_row(history_path)

    previous_free_credits = _coerce_int((previous or {}).get("free_credits"))
    previous_recorded_at = str((previous or {}).get("recorded_at_utc") or "").strip()
    previous_recorded_at_dt = None
    if previous_recorded_at:
        try:
            previous_recorded_at_dt = datetime.fromisoformat(previous_recorded_at.replace("Z", "+00:00"))
        except ValueError:
            previous_recorded_at_dt = None

    delta_credits = None
    if previous_free_credits is not None:
        delta_credits = normalized["free_credits"] - previous_free_credits
    delta_seconds = None
    if previous_recorded_at_dt is not None:
        delta_seconds = int((now - previous_recorded_at_dt.astimezone(timezone.utc)).total_seconds())
    burn_rate_credits_per_hour = None
    burn_rate_credits_per_day = None
    burn_rate_source = ""
    if measurement_trust != "fresh":
        delta_credits = None
        delta_seconds = None
        burn_rate_source = "stale_source_no_burn"
    elif delta_credits is not None and delta_seconds and delta_seconds > 0:
        burn_rate_credits_per_hour = (0 - float(delta_credits)) * 3600.0 / float(delta_seconds)
        burn_rate_credits_per_day = burn_rate_credits_per_hour * 24.0
        burn_rate_source = "history_delta"

    row = {
        "recorded_at_local": recorded_at_local,
        "recorded_at_utc": recorded_at_utc,
        "measurement_trust": measurement_trust,
        "payload_source": str(payload.get("payload_source") or ""),
        "source_recorded_at_utc": source_recorded_at_utc,
        "source_age_seconds": source_age_seconds if source_age_seconds is not None else "",
        "free_credits": normalized["free_credits"],
        "max_credits": normalized["max_credits"],
        "percent_remaining": normalized["percent_remaining"],
        "slot_count": _coerce_int(payload.get("slot_count")) or len(normalized["slots"]),
        "owner_mapped_slot_count": _coerce_int(payload.get("owner_mapped_slot_count")) or "",
        "ready_ok_count": _coerce_int(payload.get("ready_ok_count")) or "",
        "depleted_count": _coerce_int(payload.get("depleted_count")) or "",
        "basis_summary": str(payload.get("basis_summary") or ""),
        "last_probe_at_utc": str(payload.get("last_probe_at_utc") or ""),
        "actual_billing_account_count": _coerce_int(payload.get("actual_billing_account_count")) or "",
        "billing_note": str(payload.get("billing_note") or ""),
        "reported_free_credits": normalized["reported_free_credits"],
        "sum_probe_estimated_credits": normalized["sum_probe_estimated_credits"],
        "sum_probe_available_credits": normalized["sum_probe_available_credits"],
        "slot_sum_free_credits": normalized["slot_sum_free_credits"],
        "slot_sum_max_credits": normalized["slot_sum_max_credits"],
        "free_credits_source": normalized["free_credits_source"],
        "raw_last_error": str(payload.get("raw_last_error") or ""),
        "current_pace_burn_credits_per_hour": _coerce_float(payload.get("current_pace_burn_credits_per_hour")) or "",
        "avg_daily_burn_credits_7d": _coerce_float(payload.get("avg_daily_burn_credits_7d")) or "",
        "used_precomputed_aggregate": bool(payload.get("used_precomputed_aggregate")),
        "delta_credits": delta_credits if delta_credits is not None else "",
        "delta_seconds": delta_seconds if delta_seconds is not None else "",
        "burn_rate_credits_per_hour": burn_rate_credits_per_hour if burn_rate_credits_per_hour is not None else "",
        "burn_rate_credits_per_day": burn_rate_credits_per_day if burn_rate_credits_per_day is not None else "",
        "burn_rate_source": burn_rate_source,
        "refresh_error": str(payload.get("refresh_error") or payload.get("status_error") or ""),
    }
    append_history(history_path=history_path, row=row)
    latest_path, archive_path = write_runtime_aggregate(
        runtime_root=runtime_root,
        latest_filename=latest_filename,
        recorded_at_utc=recorded_at_utc,
        payload=payload,
        normalized=normalized,
        history_path=history_path,
    )

    result = {
        "recorded_at_utc": recorded_at_utc,
        "measurement_trust": measurement_trust,
        "payload_source": str(payload.get("payload_source") or ""),
        "source_recorded_at_utc": source_recorded_at_utc,
        "source_age_seconds": source_age_seconds,
        "free_credits": normalized["free_credits"],
        "max_credits": normalized["max_credits"],
        "percent_remaining": normalized["percent_remaining"],
        "refresh_mode": "billing_full_refresh",
        "history_path": str(history_path),
        "aggregate_latest_path": str(latest_path),
        "aggregate_archive_path": str(archive_path),
        "free_credits_source": normalized["free_credits_source"],
    }
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
