from __future__ import annotations

import json
import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "materialize-verified-release-channel-mirror.py"


def test_verified_release_channel_mirror_syncs_local_and_published_portal_mirrors_from_resolved_registry_root(
    tmp_path: Path,
) -> None:
    workspace = tmp_path / "workspace"
    repo_root = workspace / "chummer-presentation-sr6-origin-dialog-clean"
    scripts_dir = repo_root / "scripts"
    scripts_dir.mkdir(parents=True)
    script_copy = scripts_dir / SCRIPT.name
    script_copy.write_text(SCRIPT.read_text(encoding="utf-8"), encoding="utf-8")

    registry_root = workspace / "custom-registry"
    (registry_root / ".codex-studio" / "published").mkdir(parents=True)

    canonical_manifest = registry_root / ".codex-studio" / "published" / "RELEASE_CHANNEL.generated.json"
    canonical_manifest.write_text(
        json.dumps(
            {
                "channelId": "public_stable",
                "channel": "public_stable",
                "version": "run-20260704-170602",
                "publishedAt": "2026-07-04T17:48:20Z",
                "generatedAt": "2026-07-08T23:43:01Z",
                "generated_at": "2026-07-08T23:43:01Z",
                "artifacts": [
                    {
                        "artifactId": "avalonia-win-x64-installer",
                        "head": "avalonia",
                        "platform": "windows",
                        "rid": "win-x64",
                        "kind": "installer",
                        "fileName": "chummer-avalonia-win-x64-installer.exe",
                        "downloadUrl": "https://example.invalid/downloads/files/chummer-avalonia-win-x64-installer.exe",
                        "sha256": "80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a",
                    }
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    canonical_compat = registry_root / ".codex-studio" / "published" / "releases.json"
    canonical_compat.write_text(
        json.dumps(
            {
                "channelId": "public_stable",
                "channel": "public_stable",
                "version": "run-20260704-170602",
                "publishedAt": "2026-07-04T17:48:20Z",
                "rolloutState": "public_stable",
                "supportabilityState": "gold_supported",
                "artifactIdentityRegistry": [
                    {
                        "tupleId": "avalonia:windows:win-x64",
                        "publicationState": "published",
                        "retentionState": "current",
                    }
                ],
                "downloads": [
                    {
                        "artifactId": "avalonia-win-x64-installer",
                        "fileName": "chummer-avalonia-win-x64-installer.exe",
                        "sha256": "80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a",
                    }
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    (scripts_dir / "resolve-hub-registry-root.sh").write_text(
        "\n".join(
            [
                "#!/usr/bin/env bash",
                "set -euo pipefail",
                f"printf '%s\\n' {str(registry_root)!r}",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    (scripts_dir / "verify-releases-manifest.sh").write_text(
        "\n".join(
            [
                "#!/usr/bin/env bash",
                "set -euo pipefail",
                'target="${1:?target-required}"',
                "python3 - <<'PY' \"$target\"",
                "from pathlib import Path",
                "import sys",
                "target = Path(sys.argv[1])",
                "if not target.is_file():",
                "    raise SystemExit(f'missing manifest: {target}')",
                "PY",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    for rel_root in (
        repo_root / "Docker" / "Downloads",
        repo_root / "Chummer.Portal" / "downloads",
    ):
        (rel_root / "files").mkdir(parents=True)
        startup_smoke_dir = rel_root / "startup-smoke"
        startup_smoke_dir.mkdir(parents=True)
        (rel_root / "files" / "chummer-avalonia-win-x64-installer.exe").write_bytes(b"installer")
        (rel_root / "RELEASE_CHANNEL.generated.json").write_text(
            json.dumps({"channelId": "preview", "channel": "preview", "version": "stale-preview"}, indent=2) + "\n",
            encoding="utf-8",
        )
        (rel_root / "releases.json").write_text(
            json.dumps({"channelId": "preview", "channel": "preview", "version": "stale-preview"}, indent=2) + "\n",
            encoding="utf-8",
        )
        (startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json").write_text(
            json.dumps({"status": "pass", "releaseVersion": "stale-preview"}, indent=2) + "\n",
            encoding="utf-8",
        )

    source_portal_root = workspace / "chummer.run-services" / "Chummer.Portal" / "downloads"
    (source_portal_root / "files").mkdir(parents=True)
    source_startup_smoke_dir = source_portal_root / "startup-smoke"
    source_startup_smoke_dir.mkdir(parents=True)
    (source_portal_root / "files" / "chummer-avalonia-win-x64-installer.exe").write_bytes(b"installer")
    (source_startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json").write_text(
        json.dumps({"status": "pass", "releaseVersion": "run-20260704-170602"}, indent=2) + "\n",
        encoding="utf-8",
    )
    canonical_manifest_source_text = canonical_manifest.read_text(encoding="utf-8")
    canonical_compat_source_text = canonical_compat.read_text(encoding="utf-8")
    (source_portal_root / "RELEASE_CHANNEL.generated.json").write_text(canonical_manifest_source_text, encoding="utf-8")
    (source_portal_root / "releases.json").write_text(canonical_compat_source_text, encoding="utf-8")

    published_portal = repo_root / ".codex-studio" / "published" / "portal"
    published_portal.mkdir(parents=True)
    (published_portal / "RELEASE_CHANNEL.generated.json").write_text(
        json.dumps({"channelId": "preview", "channel": "preview", "version": "stale-preview"}, indent=2) + "\n",
        encoding="utf-8",
    )
    (published_portal / "releases.json").write_text(
        json.dumps({"channelId": "preview", "channel": "preview", "version": "stale-preview"}, indent=2) + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        ["python3", str(script_copy)],
        cwd=repo_root,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr

    verified_path = repo_root / ".tmp" / "verify-release-channel" / "RELEASE_CHANNEL.generated.json"
    verified_payload = json.loads(verified_path.read_text(encoding="utf-8"))
    assert verified_payload["channelId"] == "public_stable"
    assert verified_payload["version"] == "run-20260704-170602"
    assert verified_payload["verifiedFromPath"] == str(canonical_manifest)

    local_portal_payload = json.loads((repo_root / "Chummer.Portal" / "downloads" / "RELEASE_CHANNEL.generated.json").read_text(encoding="utf-8"))
    published_portal_payload = json.loads((repo_root / ".codex-studio" / "published" / "portal" / "RELEASE_CHANNEL.generated.json").read_text(encoding="utf-8"))
    local_portal_compat = json.loads((repo_root / "Chummer.Portal" / "downloads" / "releases.json").read_text(encoding="utf-8"))
    assert local_portal_payload["channelId"] == "public_stable"
    assert local_portal_payload["version"] == "run-20260704-170602"
    assert published_portal_payload["channelId"] == "public_stable"
    assert published_portal_payload["version"] == "run-20260704-170602"
    assert local_portal_compat["rolloutState"] == "public_stable"
    assert local_portal_compat["supportabilityState"] == "gold_supported"
    synced_receipt = json.loads(
        (repo_root / "Chummer.Portal" / "downloads" / "startup-smoke" / "startup-smoke-avalonia-win-x64.receipt.json").read_text(
            encoding="utf-8"
        )
    )
    assert synced_receipt["releaseVersion"] == "run-20260704-170602"
