#!/usr/bin/env python3
from __future__ import annotations

"""Refresh 1min credits through codexea and append a timestamped history row."""

import csv
import json
import os
import subprocess
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


DEFAULT_HISTORY_PATH = Path("/docker/fleet/state/onemin_credit_history.csv")
DEFAULT_RUNTIME_ROOT = Path("/docker/fleet/state/browseract_bootstrap/runtime")
DEFAULT_LATEST_AGGREGATE_FILENAME = "onemin_aggregate_billing_full_refresh_latest.json"
DEFAULT_BROWSERACT_REFRESH_STATE_ROOT = Path("/docker/EA/state")
FIELDNAMES = (
    "recorded_at_local",
    "recorded_at_utc",
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
    if payload_recorded_at is not None and browseract_recorded_at <= payload_recorded_at and not bool(payload.get("used_precomputed_aggregate")):
        return payload
    merged = dict(payload)
    merged.update(browseract_payload)
    merged["payload_source"] = "browseract_refresh_summary"
    return merged


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


def load_payload() -> dict[str, Any]:
    browseract_payload = _latest_browseract_refresh_payload()
    env = os.environ.copy()
    env.setdefault("CODEXEA_STATUS_CONNECT_TIMEOUT_SECONDS", "5")
    env.setdefault("CODEXEA_ONEMIN_STATUS_TIMEOUT_SECONDS", "60")
    env.setdefault("CODEXEA_ONEMIN_BILLING_TIMEOUT_SECONDS", "7200")
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
            return browseract_payload
        raise SystemExit(result.stderr.strip() or "codexea onemin aggregate refresh failed")
    try:
        payload = json.loads(result.stdout)
    except json.JSONDecodeError as exc:
        if browseract_payload is not None:
            return browseract_payload
        raise SystemExit(f"codexea onemin aggregate refresh returned invalid JSON: {exc}") from exc
    if not isinstance(payload, dict):
        if browseract_payload is not None:
            return browseract_payload
        raise SystemExit("codexea onemin aggregate refresh returned a non-object payload")
    data = payload.get("data")
    if isinstance(data, dict):
        payload = data
    return _prefer_browseract_refresh_payload(payload, browseract_payload)


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
    if delta_credits is not None and delta_seconds and delta_seconds > 0:
        burn_rate_credits_per_hour = (0 - float(delta_credits)) * 3600.0 / float(delta_seconds)
        burn_rate_credits_per_day = burn_rate_credits_per_hour * 24.0
        burn_rate_source = "history_delta"

    row = {
        "recorded_at_local": recorded_at_local,
        "recorded_at_utc": recorded_at_utc,
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
