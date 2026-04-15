#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ARTIFACT_PREFIX = "chummer-"
PASSING_STATUSES = {"pass", "passed", "ready"}
RID_TO_PLATFORM_ARCH = {
    "win-x64": ("windows", "x64"),
    "win-arm64": ("windows", "arm64"),
    "linux-x64": ("linux", "x64"),
    "linux-arm64": ("linux", "arm64"),
    "osx-arm64": ("macos", "arm64"),
    "osx-x64": ("macos", "x64"),
}


@dataclass(frozen=True)
class Candidate:
    path: Path
    file_name: str
    head: str
    rid: str
    platform: str
    arch: str


def now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def normalize(value: Any) -> str:
    return str(value or "").strip().lower()


def parse_iso_utc(value: Any) -> datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest().lower()


def expected_host_class_platform_token(platform: str) -> str:
    if platform == "windows":
        return "win"
    if platform == "macos":
        return "osx"
    if platform == "linux":
        return "linux"
    return platform


def host_class_matches_platform(host_class: str, platform: str) -> bool:
    normalized_host = normalize(host_class)
    expected_token = expected_host_class_platform_token(platform)
    if not normalized_host or not expected_token:
        return False
    host_tokens = [token for token in normalized_host.split("-") if token]
    return expected_token in host_tokens


def parse_candidate(path: Path) -> Candidate | None:
    file_name = path.name
    if not file_name.startswith(ARTIFACT_PREFIX):
        return None
    stem = file_name
    if stem.endswith(".tar.gz"):
        stem = stem[:-7]
    else:
        stem = Path(stem).stem
    if not stem.endswith("-installer"):
        return None
    stem = stem[: -len("-installer")]
    if not stem.startswith(ARTIFACT_PREFIX):
        return None
    token = stem[len(ARTIFACT_PREFIX) :]
    parts = token.split("-")
    if len(parts) < 3:
        return None
    rid = "-".join(parts[-2:]).lower()
    if rid not in RID_TO_PLATFORM_ARCH:
        return None
    head = "-".join(parts[:-2]).lower()
    if head not in {"avalonia", "blazor-desktop"}:
        return None
    platform, arch = RID_TO_PLATFORM_ARCH[rid]
    return Candidate(
        path=path.resolve(),
        file_name=file_name,
        head=head,
        rid=rid,
        platform=platform,
        arch=arch,
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Promote quarantined desktop installers into the active downloads shelf only when startup-smoke proof matches installer bytes."
    )
    parser.add_argument("--downloads-dir", type=Path, required=True)
    parser.add_argument("--startup-smoke-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--release-channel", default="", help="Optional expected channelId/channel for startup-smoke receipts.")
    parser.add_argument("--release-version", default="", help="Optional expected version/releaseVersion for startup-smoke receipts.")
    parser.add_argument(
        "--max-age-seconds",
        type=int,
        default=86400,
        help="Maximum startup-smoke receipt age in seconds.",
    )
    parser.add_argument(
        "--max-future-skew-seconds",
        type=int,
        default=300,
        help="Maximum allowed startup-smoke timestamp future skew in seconds.",
    )
    parser.add_argument(
        "--quarantine-dir",
        action="append",
        default=[],
        help="Repeatable quarantine search root. Defaults to .codex-studio/quarantine and Docker/Downloads/quarantine under repo root.",
    )
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parent.parent,
        help="Repo root used for default quarantine roots.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    downloads_dir = args.downloads_dir.resolve()
    startup_smoke_dir = args.startup_smoke_dir.resolve()
    release_channel = normalize(args.release_channel)
    release_version = str(args.release_version or "").strip()
    max_age_seconds = max(0, int(args.max_age_seconds))
    max_future_skew_seconds = max(0, int(args.max_future_skew_seconds))

    quarantine_dirs = [Path(path).resolve() for path in args.quarantine_dir]
    if not quarantine_dirs:
        quarantine_dirs = [
            (repo_root / ".codex-studio" / "quarantine").resolve(),
            (repo_root / "Docker" / "Downloads" / "quarantine").resolve(),
        ]

    downloads_dir.mkdir(parents=True, exist_ok=True)
    args.output.parent.mkdir(parents=True, exist_ok=True)

    entries: list[dict[str, Any]] = []
    promoted_count = 0
    seen_paths: set[str] = set()
    now_utc = datetime.now(timezone.utc)

    for quarantine_dir in quarantine_dirs:
        if not quarantine_dir.is_dir():
            continue
        for path in sorted(quarantine_dir.rglob("*")):
            if not path.is_file():
                continue
            resolved = str(path.resolve())
            if resolved in seen_paths:
                continue
            seen_paths.add(resolved)

            candidate = parse_candidate(path)
            if candidate is None:
                continue

            entry: dict[str, Any] = {
                "candidatePath": str(candidate.path),
                "fileName": candidate.file_name,
                "head": candidate.head,
                "rid": candidate.rid,
                "platform": candidate.platform,
                "arch": candidate.arch,
                "status": "skipped",
                "reasons": [],
            }

            receipt_path = startup_smoke_dir / f"startup-smoke-{candidate.head}-{candidate.rid}.receipt.json"
            entry["startupSmokeReceiptPath"] = str(receipt_path)
            if not receipt_path.is_file():
                entry["reasons"].append("startup smoke receipt is missing")
                entries.append(entry)
                continue

            try:
                receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
            except Exception as exc:  # pragma: no cover
                entry["reasons"].append(f"startup smoke receipt unreadable: {exc}")
                entries.append(entry)
                continue
            if not isinstance(receipt, dict):
                entry["reasons"].append("startup smoke receipt must be a JSON object")
                entries.append(entry)
                continue

            status = normalize(receipt.get("status"))
            if status not in PASSING_STATUSES:
                entry["reasons"].append(f"startup smoke status is not passing: {status or 'missing'}")

            ready_checkpoint = normalize(receipt.get("readyCheckpoint"))
            if ready_checkpoint != "pre_ui_event_loop":
                entry["reasons"].append("startup smoke readyCheckpoint is not pre_ui_event_loop")

            receipt_head = normalize(receipt.get("headId"))
            receipt_platform = normalize(receipt.get("platform"))
            receipt_arch = normalize(receipt.get("arch"))
            receipt_rid = normalize(receipt.get("rid"))
            receipt_host_class = normalize(receipt.get("hostClass"))
            receipt_operating_system = str(receipt.get("operatingSystem") or "").strip()
            receipt_channel = normalize(receipt.get("channelId") or receipt.get("channel"))
            receipt_version = str(receipt.get("version") or receipt.get("releaseVersion") or "").strip()

            if receipt_head != candidate.head:
                entry["reasons"].append(f"startup smoke headId mismatch: {receipt_head or 'missing'}")
            if receipt_platform != candidate.platform:
                entry["reasons"].append(f"startup smoke platform mismatch: {receipt_platform or 'missing'}")
            if receipt_arch != candidate.arch:
                entry["reasons"].append(f"startup smoke arch mismatch: {receipt_arch or 'missing'}")
            if receipt_rid != candidate.rid:
                entry["reasons"].append(f"startup smoke rid mismatch: {receipt_rid or 'missing'}")
            if not receipt_host_class:
                entry["reasons"].append("startup smoke hostClass is missing")
            elif not host_class_matches_platform(receipt_host_class, candidate.platform):
                entry["reasons"].append("startup smoke hostClass does not match platform")
            if not receipt_operating_system:
                entry["reasons"].append("startup smoke operatingSystem is missing")
            if release_channel and receipt_channel != release_channel:
                entry["reasons"].append(
                    f"startup smoke channel mismatch: expected {release_channel}, got {receipt_channel or 'missing'}"
                )
            if release_version and receipt_version != release_version:
                entry["reasons"].append(
                    f"startup smoke version mismatch: expected {release_version}, got {receipt_version or 'missing'}"
                )

            recorded_at = parse_iso_utc(
                receipt.get("completedAtUtc") or receipt.get("recordedAtUtc") or receipt.get("startedAtUtc")
            )
            if recorded_at is None:
                entry["reasons"].append("startup smoke timestamp is missing/invalid")
            else:
                age_delta_seconds = int((now_utc - recorded_at).total_seconds())
                if age_delta_seconds < 0:
                    future_skew_seconds = abs(age_delta_seconds)
                    if future_skew_seconds > max_future_skew_seconds:
                        entry["reasons"].append(
                            f"startup smoke timestamp future skew {future_skew_seconds}s exceeds {max_future_skew_seconds}s"
                        )
                elif age_delta_seconds > max_age_seconds:
                    entry["reasons"].append(
                        f"startup smoke receipt stale: {age_delta_seconds}s exceeds {max_age_seconds}s"
                    )

            candidate_sha = sha256_file(candidate.path)
            entry["candidateSha256"] = candidate_sha
            expected_digest = f"sha256:{candidate_sha}"
            receipt_digest = normalize(receipt.get("artifactDigest"))
            if receipt_digest != expected_digest:
                entry["reasons"].append("startup smoke artifactDigest does not match candidate bytes")

            destination_path = downloads_dir / candidate.file_name
            entry["destinationPath"] = str(destination_path)
            if destination_path.is_file():
                destination_sha = sha256_file(destination_path)
                entry["destinationSha256"] = destination_sha
                if destination_sha == candidate_sha:
                    entry["status"] = "already_promoted"
                    entries.append(entry)
                    continue
                entry["reasons"].append("destination already exists with different bytes")

            if entry["reasons"]:
                entries.append(entry)
                continue

            shutil.copy2(candidate.path, destination_path)
            promoted_count += 1
            entry["status"] = "promoted"
            entries.append(entry)

    generated_at = now_iso()
    payload = {
        "generatedAt": generated_at,
        "generated_at": generated_at,
        "contract_name": "chummer6-ui.proof_backed_quarantine_installer_promotion",
        "downloadsDir": str(downloads_dir),
        "startupSmokeDir": str(startup_smoke_dir),
        "releaseChannel": release_channel,
        "releaseVersion": release_version,
        "quarantineDirs": [str(path) for path in quarantine_dirs],
        "promotedCount": promoted_count,
        "candidatesEvaluated": len(entries),
        "entries": entries,
    }
    args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
