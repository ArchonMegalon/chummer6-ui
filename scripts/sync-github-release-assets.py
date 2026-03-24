#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import subprocess
from pathlib import Path, PurePosixPath


DEFAULT_REPO = str(os.environ.get("GITHUB_REPOSITORY") or "").strip()
if not DEFAULT_REPO:
    raise SystemExit("GITHUB_REPOSITORY or CHUMMER_GITHUB_RELEASE_REPO is required")

REPO_SPEC = str(os.environ.get("CHUMMER_GITHUB_RELEASE_REPO") or DEFAULT_REPO).strip() or DEFAULT_REPO
OWNER, REPO = REPO_SPEC.split("/", 1)
TAG = str(os.environ.get("CHUMMER_GITHUB_RELEASE_TAG") or "desktop-latest").strip() or "desktop-latest"
TITLE = str(os.environ.get("CHUMMER_GITHUB_RELEASE_TITLE") or "Chummer6 Desktop Latest").strip() or "Chummer6 Desktop Latest"
TARGET_COMMITISH = (
    str(os.environ.get("CHUMMER_GITHUB_RELEASE_TARGET_COMMITISH") or os.environ.get("GITHUB_SHA") or "main").strip()
    or "main"
)
BUNDLE_DIR = Path(os.environ.get("CHUMMER_GITHUB_RELEASE_BUNDLE_DIR") or "dist")
MANIFEST_PATH = BUNDLE_DIR / "RELEASE_CHANNEL.generated.json"
COMPAT_MANIFEST_PATH = BUNDLE_DIR / "releases.json"
FILES_DIR = BUNDLE_DIR / "files"


def run(*args: str, input_text: str | None = None, check: bool = True) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        list(args),
        input=input_text,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=check,
    )


def load_release_payload() -> dict[str, object]:
    if not MANIFEST_PATH.exists():
        raise FileNotFoundError(f"release channel manifest not found: {MANIFEST_PATH}")
    loaded = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    if not isinstance(loaded, dict):
        raise ValueError(f"release channel manifest must be a JSON object: {MANIFEST_PATH}")
    return loaded


def release_asset_file_names(data: dict[str, object]) -> list[str]:
    names: list[str] = []
    artifacts = data.get("artifacts")
    if isinstance(artifacts, list):
        for item in artifacts:
            if not isinstance(item, dict):
                continue
            file_name = str(item.get("fileName") or "").strip()
            if file_name:
                names.append(file_name)
    downloads = data.get("downloads")
    if isinstance(downloads, list):
        for item in downloads:
            if not isinstance(item, dict):
                continue
            url = str(item.get("url") or "").strip()
            if not url:
                continue
            file_name = PurePosixPath(url).name.strip()
            if file_name:
                names.append(file_name)
    deduped: list[str] = []
    seen: set[str] = set()
    for name in names:
        if name not in seen:
            deduped.append(name)
            seen.add(name)
    return deduped


def release_asset_paths(data: dict[str, object]) -> list[Path]:
    paths: list[Path] = []
    for manifest_path in (MANIFEST_PATH, COMPAT_MANIFEST_PATH):
        if manifest_path.exists():
            paths.append(manifest_path)
    for name in release_asset_file_names(data):
        candidate = FILES_DIR / name
        if not candidate.exists():
            raise FileNotFoundError(f"release asset not found: {candidate}")
        paths.append(candidate)
    deduped: list[Path] = []
    seen: set[str] = set()
    for path in paths:
        if path.name in seen:
            continue
        deduped.append(path)
        seen.add(path.name)
    return deduped


def release_body(data: dict[str, object]) -> str:
    version = str(data.get("version") or "unknown").strip() or "unknown"
    channel = str(data.get("channelId") or data.get("channel") or "unknown").strip() or "unknown"
    published_at = str(data.get("publishedAt") or "unknown").strip() or "unknown"
    return "\n".join(
        [
            f"## {TITLE}",
            "",
            "This prerelease is kept in rolling sync with the latest successful desktop build bundle.",
            "",
            f"- manifest version: `{version}`",
            f"- release channel: `{channel}`",
            f"- published at: `{published_at}`",
            f"- target commit: `{TARGET_COMMITISH}`",
        ]
    )


def upsert_release(body: str) -> dict[str, object]:
    existing = run("gh", "api", f"repos/{OWNER}/{REPO}/releases/tags/{TAG}", check=False)
    payload = json.dumps(
        {
            "tag_name": TAG,
            "target_commitish": TARGET_COMMITISH,
            "name": TITLE,
            "body": body,
            "draft": False,
            "prerelease": True,
        }
    )
    if existing.returncode == 0:
        release = json.loads(existing.stdout)
        release_id = str(release["id"])
        run(
            "gh",
            "api",
            "--method",
            "PATCH",
            f"repos/{OWNER}/{REPO}/releases/{release_id}",
            "--input",
            "-",
            input_text=payload,
        )
        refreshed = run("gh", "api", f"repos/{OWNER}/{REPO}/releases/tags/{TAG}")
        return json.loads(refreshed.stdout)
    created = run(
        "gh",
        "api",
        "--method",
        "POST",
        f"repos/{OWNER}/{REPO}/releases",
        "--input",
        "-",
        input_text=payload,
        check=False,
    )
    if created.returncode != 0:
        retry = run("gh", "api", f"repos/{OWNER}/{REPO}/releases/tags/{TAG}", check=False)
        if retry.returncode != 0:
            raise subprocess.CalledProcessError(created.returncode, created.args, created.stdout, created.stderr)
        release = json.loads(retry.stdout)
        release_id = str(release["id"])
        run(
            "gh",
            "api",
            "--method",
            "PATCH",
            f"repos/{OWNER}/{REPO}/releases/{release_id}",
            "--input",
            "-",
            input_text=payload,
        )
        refreshed = run("gh", "api", f"repos/{OWNER}/{REPO}/releases/tags/{TAG}")
        return json.loads(refreshed.stdout)
    return json.loads(created.stdout)


def sync_release_assets(release: dict[str, object], asset_paths: list[Path]) -> None:
    expected_names = {path.name for path in asset_paths}
    for asset in release.get("assets") or []:
        if not isinstance(asset, dict):
            continue
        name = str(asset.get("name") or "").strip()
        if not name or name in expected_names:
            continue
        run("gh", "release", "delete-asset", TAG, name, "-R", f"{OWNER}/{REPO}", "--yes")
    upload_cmd = ["gh", "release", "upload", TAG, "-R", f"{OWNER}/{REPO}", "--clobber"]
    upload_cmd.extend(str(path) for path in asset_paths)
    run(*upload_cmd)


def main() -> int:
    payload = load_release_payload()
    asset_paths = release_asset_paths(payload)
    release = upsert_release(release_body(payload))
    sync_release_assets(release, asset_paths)
    print(
        json.dumps(
            {
                "repo": f"{OWNER}/{REPO}",
                "tag": TAG,
                "target_commitish": TARGET_COMMITISH,
                "asset_count": len(asset_paths),
                "status": "published",
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
