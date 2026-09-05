#!/usr/bin/env python3
"""Validate the non-authoritative split-preseal publication transaction.

The preseal lane exists only to publish package-recipe and verification code
through protected, linear-history pull requests.  It never validates package
consumers and never grants release or publication authority.

Topology:

    base -> recipe -> marker

The marker commit changes only this contract file.  A later seal commit is a
direct child of the marker commit and changes exactly the two canonical lock
files.  The marker binds the recipe tree rather than the recipe commit so that
GitHub's protected rebase merge may rewrite commit identities without changing
the reviewed bytes.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import secrets
import stat
import subprocess
import sys
from datetime import UTC, datetime
from pathlib import Path
from typing import Any


CONTRACT_NAME = "chummer6-ui.split-preseal-publication/v2"
RECEIPT_CONTRACT = "chummer6-ui.split-preseal-validation/v2"
MARKER_PATH = "config/ui-preseal-publication.json"
ORACLE_FIXTURE_PATH = "config/ui-next-authority-oracle-v10.json"
CANONICAL_LOCK_PATHS = (
    "config/package-plane.lock.json",
    "config/ui-owner-package-plane.lock.json",
)
ALLOWED_RECIPE_PATHS = frozenset(
    {
        ".github/workflows/current-main-package-plane.yml",
        ".github/workflows/pull-request-ci.yml",
        ".github/workflows/unsigned-macos-native-build.yml",
        "Chummer.Desktop.Runtime/GrantBoundDesktopWorkspaceRoamingSync.cs",
        "Chummer.Desktop.Runtime/IDesktopWorkspaceRoamingSync.cs",
        "Chummer.Desktop.Runtime/InProcessChummerClient.cs",
        "Chummer.Desktop.Runtime.Tests/Chummer.Desktop.Runtime.Tests.csproj",
        "Chummer.Presentation/Overview/CharacterOverviewPresenter.CreationBootstrap.cs",
        "Chummer.Presentation/Overview/CharacterCreationResourcesInteractionPresenter.cs",
        "Chummer.Tests/InProcessChummerClientRulesetPluginTests.cs",
        "Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs",
        "Chummer.Tests/Presentation/CharacterCreationResourcesInteractionPresenterTests.cs",
        "Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj",
        ORACLE_FIXTURE_PATH,
        "Directory.Build.props",
        "README.md",
        "scripts/ai/verify_fresh_checkout_package_plane.py",
        "scripts/ai/verify_split_preseal_publication.py",
        "scripts/ai/with-package-plane.sh",
        "scripts/build-unsigned-macos-native.sh",
        "tests/test_current_owner_contract_feed.py",
        "tests/test_desktop_downloads_local_release_policy.py",
        "tests/test_fresh_package_plane_controls.py",
        "tests/test_keylocker_fixture_intake.py",
        "tests/test_split_preseal_publication.py",
        "tests/fixtures/keylocker-signer-v1/MANIFEST.json",
        "tests/test_unsigned_macos_native_build.py",
    }
)
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
TREE_RE = COMMIT_RE
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
TRUSTED_GIT = Path("/usr/bin/git")
NEXT_AUTHORITY_ORACLE = {
    "canonicalLock": {
        "blob": "4b1d26ad990d6d942f8e518bb8d2b61d872907b0",
        "commit": "c12811fda570cd56c70e52c44e38b1d32ff831a1",
        "path": "config/package-plane.lock.json",
        "fixturePath": ORACLE_FIXTURE_PATH,
        "rawSha256": "adb54a232ba6020d970d343d219f0c7539c7556aef3ea6e757ab306daafb2c38",
        "rawSizeBytes": 51528,
        "semanticCanonicalSha256": "02a97aac792b175281655d29e8f353301147bb3926e11b9124ed818b58110a05",
        "semanticCanonicalSizeBytes": 51528,
        "tree": "faec09b431f3f6fd94736655e4e1850bbdf5d3f2",
    },
    "producerLock": {
        "absentAtCommit": True,
        "path": "config/ui-owner-package-plane.lock.json",
    },
}


class PresealError(RuntimeError):
    """Raised when the preseal transaction is not exact."""


def canonical_json_bytes(value: Any) -> bytes:
    return (json.dumps(value, indent=2, sort_keys=True) + "\n").encode("utf-8")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def git(
    repo_root: Path,
    *arguments: str,
    text: bool = True,
    allow_exit_one: bool = False,
) -> str | bytes:
    completed = subprocess.run(
        [str(TRUSTED_GIT), "--no-replace-objects", *arguments],
        cwd=repo_root,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=text,
        check=False,
    )
    allowed = {0, 1} if allow_exit_one else {0}
    if completed.returncode not in allowed:
        raise PresealError(
            f"git {' '.join(arguments)} failed with exit {completed.returncode}"
        )
    if text:
        return completed.stdout.strip()
    return completed.stdout


def require_commit(value: str, label: str) -> str:
    if not COMMIT_RE.fullmatch(value):
        raise PresealError(f"{label} is not an exact commit")
    return value


def parents(repo_root: Path, commit: str) -> list[str]:
    exact = require_commit(commit, "commit")
    row = str(git(repo_root, "rev-list", "--parents", "-n", "1", exact)).split()
    if not row or row[0] != exact or any(not COMMIT_RE.fullmatch(item) for item in row):
        raise PresealError("commit topology is malformed")
    return row[1:]


def tree(repo_root: Path, commit: str) -> str:
    value = str(git(repo_root, "rev-parse", f"{require_commit(commit, 'commit')}^{{tree}}"))
    if not TREE_RE.fullmatch(value):
        raise PresealError("commit tree is malformed")
    return value


def commit_path_exists(repo_root: Path, commit: str, relative: str) -> bool:
    completed = subprocess.run(
        [
            str(TRUSTED_GIT),
            "--no-replace-objects",
            "cat-file",
            "-e",
            f"{require_commit(commit, 'commit')}:{relative}",
        ],
        cwd=repo_root,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    if completed.returncode not in (0, 1, 128):
        raise PresealError("marker existence could not be determined")
    return completed.returncode == 0


def commit_bytes(repo_root: Path, commit: str, relative: str) -> bytes:
    return bytes(git(repo_root, "show", f"{require_commit(commit, 'commit')}:{relative}", text=False))


def commit_blob(repo_root: Path, commit: str, relative: str) -> str:
    value = str(git(repo_root, "rev-parse", f"{require_commit(commit, 'commit')}:{relative}"))
    if not COMMIT_RE.fullmatch(value):
        raise PresealError(f"{relative} is not an exact blob")
    return value


def secure_worktree_bytes(path: Path) -> bytes:
    metadata = path.lstat()
    if not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
        raise PresealError(f"{path} is not a single-link regular file")
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    try:
        opened = os.fstat(descriptor)
        if (metadata.st_dev, metadata.st_ino) != (opened.st_dev, opened.st_ino):
            raise PresealError(f"{path} changed before read")
        chunks: list[bytes] = []
        while chunk := os.read(descriptor, 1024 * 1024):
            chunks.append(chunk)
        after = os.fstat(descriptor)
        if (
            opened.st_dev,
            opened.st_ino,
            opened.st_size,
            opened.st_mtime_ns,
        ) != (
            after.st_dev,
            after.st_ino,
            after.st_size,
            after.st_mtime_ns,
        ):
            raise PresealError(f"{path} changed during read")
        return b"".join(chunks)
    finally:
        os.close(descriptor)


def worktree_blob(repo_root: Path, relative: str) -> str:
    payload = secure_worktree_bytes(repo_root / relative)
    completed = subprocess.run(
        [
            str(TRUSTED_GIT),
            "--no-replace-objects",
            "hash-object",
            f"--path={relative}",
            "--stdin",
        ],
        cwd=repo_root,
        input=payload,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    value = completed.stdout.decode("ascii", errors="strict").strip()
    if completed.returncode != 0 or not COMMIT_RE.fullmatch(value):
        raise PresealError(f"worktree blob could not be derived: {relative}")
    return value


def require_clean_checkout(repo_root: Path, expected_head: str) -> None:
    if str(git(repo_root, "rev-parse", "HEAD")) != require_commit(
        expected_head, "checkout head"
    ):
        raise PresealError("checkout HEAD differs from validated topology")
    if str(git(repo_root, "status", "--porcelain")):
        raise PresealError("preseal checkout is dirty")
    tracked = bytes(git(repo_root, "ls-files", "-v", "-z", text=False))
    flagged = [
        row.decode("utf-8", errors="replace")
        for row in tracked.split(b"\0")
        if row and not row.startswith(b"H ")
    ]
    if flagged:
        raise PresealError(
            "preseal checkout uses hidden index flags: " + ", ".join(flagged)
        )


def diff_rows(repo_root: Path, base: str, target: str) -> list[dict[str, Any]]:
    output = str(
        git(
            repo_root,
            "diff-tree",
            "--no-commit-id",
            "--name-status",
            "-r",
            require_commit(base, "diff base"),
            require_commit(target, "diff target"),
        )
    )
    rows: list[dict[str, Any]] = []
    for line in output.splitlines() if output else []:
        fields = line.split("\t")
        if len(fields) != 2 or fields[0] not in {"A", "M"}:
            raise PresealError("preseal recipe contains a rename, deletion, or malformed row")
        status_value, relative = fields
        if relative not in ALLOWED_RECIPE_PATHS:
            raise PresealError(f"preseal recipe path is not allowed: {relative}")
        payload = commit_bytes(repo_root, target, relative)
        rows.append(
            {
                "blob": commit_blob(repo_root, target, relative),
                "path": relative,
                "sha256": sha256_bytes(payload),
                "sizeBytes": len(payload),
                "status": status_value,
            }
        )
    rows.sort(key=lambda row: row["path"])
    if not rows:
        raise PresealError("preseal recipe change inventory is empty")
    return rows


def lock_rows(repo_root: Path, base: str) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for relative in CANONICAL_LOCK_PATHS:
        payload = commit_bytes(repo_root, base, relative)
        rows.append(
            {
                "blob": commit_blob(repo_root, base, relative),
                "path": relative,
                "sha256": sha256_bytes(payload),
                "sizeBytes": len(payload),
            }
        )
    return rows


def load_commit_json(repo_root: Path, commit: str, relative: str) -> dict[str, Any]:
    try:
        value = json.loads(commit_bytes(repo_root, commit, relative))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise PresealError(f"{relative} is not valid JSON") from exc
    if not isinstance(value, dict):
        raise PresealError(f"{relative} is not a JSON object")
    return value


def validate_oracle_at_recipe(
    repo_root: Path, recipe_commit: str, oracle: object
) -> dict[str, Any]:
    """Validate an oracle against the exact recipe that published it.

    Retained markers must remain verifiable after a later recipe rotates the
    next-authority fixture.  The marker carries its historical oracle, while
    the new recipe carries NEXT_AUTHORITY_ORACLE.  Both are accepted only when
    their fixture bytes, blob identity, canonical semantics, and closed shape
    agree at their respective recipe commits.
    """

    if not isinstance(oracle, dict) or set(oracle) != {
        "canonicalLock",
        "producerLock",
    }:
        raise PresealError("preseal authority oracle shape is invalid")
    canonical = oracle.get("canonicalLock")
    producer = oracle.get("producerLock")
    if not isinstance(canonical, dict) or set(canonical) != {
        "blob",
        "commit",
        "fixturePath",
        "path",
        "rawSha256",
        "rawSizeBytes",
        "semanticCanonicalSha256",
        "semanticCanonicalSizeBytes",
        "tree",
    }:
        raise PresealError("preseal canonical-lock oracle shape is invalid")
    if producer != {
        "absentAtCommit": True,
        "path": "config/ui-owner-package-plane.lock.json",
    }:
        raise PresealError("preseal producer-lock oracle shape is invalid")
    if (
        canonical.get("fixturePath") != ORACLE_FIXTURE_PATH
        or canonical.get("path") != "config/package-plane.lock.json"
        or not COMMIT_RE.fullmatch(str(canonical.get("blob", "")))
        or not COMMIT_RE.fullmatch(str(canonical.get("commit", "")))
        or not TREE_RE.fullmatch(str(canonical.get("tree", "")))
        or not re.fullmatch(r"^[0-9a-f]{64}$", str(canonical.get("rawSha256", "")))
        or not re.fullmatch(
            r"^[0-9a-f]{64}$", str(canonical.get("semanticCanonicalSha256", ""))
        )
        or not isinstance(canonical.get("rawSizeBytes"), int)
        or not isinstance(canonical.get("semanticCanonicalSizeBytes"), int)
    ):
        raise PresealError("preseal canonical-lock oracle metadata is invalid")

    recipe = require_commit(recipe_commit, "oracle recipe")
    payload = commit_bytes(repo_root, recipe, ORACLE_FIXTURE_PATH)
    if (
        commit_blob(repo_root, recipe, ORACLE_FIXTURE_PATH) != canonical["blob"]
        or len(payload) != canonical["rawSizeBytes"]
        or sha256_bytes(payload) != canonical["rawSha256"]
    ):
        raise PresealError("preseal recipe oracle fixture differs from fixed bytes")
    try:
        value = json.loads(payload)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise PresealError("preseal recipe oracle fixture is not JSON") from exc
    canonical_payload = canonical_json_bytes(value)
    if (
        not isinstance(value, dict)
        or len(canonical_payload) != canonical["semanticCanonicalSizeBytes"]
        or sha256_bytes(canonical_payload) != canonical["semanticCanonicalSha256"]
        or value.get("contractVersion") != 10
        or "uiOwnerFeed" in value
        or len(value.get("consumer", {}).get("sourceFiles", {})) != 33
        or producer["path"] in value.get("consumer", {}).get("sourceFiles", {})
    ):
        raise PresealError("preseal recipe oracle fixture semantics differ")
    return json.loads(json.dumps(oracle))


def unwrap_seal_commit(repo_root: Path, sealed_commit: str) -> str:
    sealed = require_commit(sealed_commit, "sealed commit")
    sealed_parents = parents(repo_root, sealed)
    if len(sealed_parents) == 2:
        base_parent, candidate = sealed_parents
        if tree(repo_root, sealed) != tree(repo_root, candidate):
            raise PresealError("sealed pull-request merge tree differs from seal")
        if parents(repo_root, candidate) != [base_parent]:
            raise PresealError("sealed pull-request merge parents are not exact")
        return candidate
    if len(sealed_parents) != 1:
        raise PresealError("sealed preseal topology has unexpected parents")
    return sealed


def validate_existing_sealed_marker(repo_root: Path, sealed_commit: str) -> str:
    """Validate one retained marker transaction and return its published Q commit."""

    sealed = require_commit(sealed_commit, "sealed base")
    if not commit_path_exists(repo_root, sealed, MARKER_PATH):
        raise PresealError("sealed base does not retain a preseal marker")
    seal = unwrap_seal_commit(repo_root, sealed)
    marker_parents = parents(repo_root, seal)
    if len(marker_parents) != 1:
        raise PresealError("seal is not the sole child of its marker commit")
    marker_commit = marker_parents[0]
    recipe_parents = parents(repo_root, marker_commit)
    if len(recipe_parents) != 1:
        raise PresealError("retained marker commit has unexpected parents")
    recipe = recipe_parents[0]
    marker = load_marker_bytes(commit_bytes(repo_root, marker_commit, MARKER_PATH))
    marker_base = require_commit(str(marker.get("baseCommit", "")), "retained marker base")
    historical_oracle = validate_oracle_at_recipe(
        repo_root, recipe, marker.get("nextAuthorityOracle")
    )
    expected = expected_marker(
        repo_root,
        marker_base,
        recipe,
        next_authority_oracle=historical_oracle,
    )
    if marker != expected:
        raise PresealError("retained marker is not its exact prior transaction")
    if commit_blob(repo_root, seal, MARKER_PATH) != commit_blob(
        repo_root, marker_commit, MARKER_PATH
    ):
        raise PresealError("seal changed the retained preseal marker")
    output = str(
        git(
            repo_root,
            "diff-tree",
            "--no-commit-id",
            "--name-only",
            "-r",
            marker_commit,
            seal,
        )
    )
    if sorted(output.splitlines()) != sorted(CANONICAL_LOCK_PATHS):
        raise PresealError("seal did not change exactly the two canonical locks")
    consumer_lock = load_commit_json(
        repo_root, seal, "config/package-plane.lock.json"
    )
    owner_lock = load_commit_json(
        repo_root, seal, "config/ui-owner-package-plane.lock.json"
    )
    ui_owner_feed = consumer_lock.get("uiOwnerFeed")
    if (
        not isinstance(ui_owner_feed, dict)
        or ui_owner_feed.get("packageRecipeCommit") != marker_commit
        or owner_lock.get("packageRecipeCommit") != marker_commit
    ):
        raise PresealError("sealed locks do not bind the retained recipe commit")
    return marker_commit


def validate_existing_unsealed_marker(repo_root: Path, marker_commit: str) -> str:
    """Validate one exact first-cycle Q that has not yet received its seal."""

    published = require_commit(marker_commit, "unsealed preseal base")
    if not commit_path_exists(repo_root, published, MARKER_PATH):
        raise PresealError("unsealed preseal base does not retain a marker")
    marker_parents = parents(repo_root, published)
    if len(marker_parents) != 1:
        raise PresealError("unsealed preseal marker has unexpected parents")
    recipe = marker_parents[0]
    recipe_parents = parents(repo_root, recipe)
    if len(recipe_parents) != 1:
        raise PresealError("unsealed preseal recipe has unexpected parents")
    original_base = recipe_parents[0]
    if commit_path_exists(repo_root, original_base, MARKER_PATH):
        raise PresealError("unsealed preseal marker cannot be superseded twice")
    if commit_path_exists(repo_root, recipe, MARKER_PATH):
        raise PresealError("unsealed preseal recipe unexpectedly contains a marker")
    marker_diff = str(
        git(
            repo_root,
            "diff-tree",
            "--no-commit-id",
            "--name-status",
            "-r",
            recipe,
            published,
        )
    )
    if marker_diff != f"A\t{MARKER_PATH}":
        raise PresealError("unsealed preseal base is not an exact marker-only commit")
    marker = load_marker_bytes(commit_bytes(repo_root, published, MARKER_PATH))
    historical_oracle = validate_oracle_at_recipe(
        repo_root, recipe, marker.get("nextAuthorityOracle")
    )
    expected = expected_marker(
        repo_root,
        original_base,
        recipe,
        next_authority_oracle=historical_oracle,
    )
    if marker != expected:
        raise PresealError("unsealed preseal base marker differs from its exact transaction")
    for relative in CANONICAL_LOCK_PATHS:
        if commit_blob(repo_root, published, relative) != commit_blob(
            repo_root, recipe, relative
        ):
            raise PresealError("unsealed preseal base changed a canonical sealed lock")
    return published


def expected_marker(
    repo_root: Path,
    base: str,
    recipe: str,
    *,
    next_authority_oracle: object | None = None,
) -> dict[str, Any]:
    base_exact = require_commit(base, "preseal base")
    recipe_exact = require_commit(recipe, "preseal recipe")
    if parents(repo_root, recipe_exact) != [base_exact]:
        raise PresealError("recipe commit is not the sole direct child of base")
    base_has_marker = commit_path_exists(repo_root, base_exact, MARKER_PATH)
    if base_has_marker:
        try:
            validate_existing_sealed_marker(repo_root, base_exact)
        except PresealError:
            try:
                validate_existing_unsealed_marker(repo_root, base_exact)
            except PresealError as unsealed_error:
                raise PresealError(
                    "preseal base is neither an exact seal nor one recoverable unsealed marker"
                ) from unsealed_error
    recipe_has_marker = commit_path_exists(repo_root, recipe_exact, MARKER_PATH)
    if base_has_marker != recipe_has_marker or (
        base_has_marker
        and commit_blob(repo_root, base_exact, MARKER_PATH)
        != commit_blob(repo_root, recipe_exact, MARKER_PATH)
    ):
        raise PresealError("recipe commit changed the retained preseal marker")
    for relative in CANONICAL_LOCK_PATHS:
        if commit_blob(repo_root, recipe_exact, relative) != commit_blob(
            repo_root, base_exact, relative
        ):
            raise PresealError("preseal recipe changed a canonical sealed lock")
    oracle = validate_oracle_at_recipe(
        repo_root,
        recipe_exact,
        NEXT_AUTHORITY_ORACLE
        if next_authority_oracle is None
        else next_authority_oracle,
    )
    return {
        "allowedSealChanges": list(CANONICAL_LOCK_PATHS),
        "authority": False,
        "baseCommit": base_exact,
        "baseTree": tree(repo_root, base_exact),
        "canonicalSealedLocks": lock_rows(repo_root, base_exact),
        "contractName": CONTRACT_NAME,
        "contractVersion": 2,
        "markerPath": MARKER_PATH,
        "nextAuthorityOracle": oracle,
        "packageConsumerClaim": False,
        "publicationAuthorized": False,
        "recipeChanges": diff_rows(repo_root, base_exact, recipe_exact),
        "recipeTree": tree(repo_root, recipe_exact),
        "releaseClaim": False,
    }


def load_marker_bytes(payload: bytes) -> dict[str, Any]:
    try:
        value = json.loads(payload)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise PresealError("preseal marker is not canonical JSON") from exc
    if not isinstance(value, dict) or canonical_json_bytes(value) != payload:
        raise PresealError("preseal marker bytes are not canonical")
    return value


def unwrap_checkout(
    repo_root: Path, *, base: str, head: str, checkout: str
) -> bool:
    base_exact = require_commit(base, "event base")
    head_exact = require_commit(head, "event head")
    checkout_exact = require_commit(checkout, "checkout commit")
    if checkout_exact == head_exact:
        return False
    checkout_parents = parents(repo_root, checkout_exact)
    if checkout_parents != [base_exact, head_exact]:
        raise PresealError("checkout is not the exact pull-request merge topology")
    if tree(repo_root, checkout_exact) != tree(repo_root, head_exact):
        raise PresealError("pull-request merge tree differs from exact head")
    return True


def validate_preseal(
    repo_root: Path, *, base: str, head: str, checkout: str | None = None
) -> dict[str, Any]:
    root = repo_root.resolve()
    head_exact = require_commit(head, "preseal head")
    checkout_exact = checkout or str(git(root, "rev-parse", "HEAD"))
    synthetic = unwrap_checkout(root, base=base, head=head_exact, checkout=checkout_exact)
    require_clean_checkout(root, checkout_exact)
    base_has_marker = commit_path_exists(root, base, MARKER_PATH)
    if not commit_path_exists(root, head_exact, MARKER_PATH) or (
        base_has_marker
        and commit_blob(root, base, MARKER_PATH)
        == commit_blob(root, head_exact, MARKER_PATH)
    ):
        raise PresealError("preseal marker must be added or refreshed exactly once")
    head_parents = parents(root, head_exact)
    if len(head_parents) != 1:
        raise PresealError("preseal marker commit must have one recipe parent")
    recipe = head_parents[0]
    if parents(root, recipe) != [require_commit(base, "preseal base")]:
        raise PresealError("preseal recipe topology is not exact")
    marker_diff = str(
        git(root, "diff-tree", "--no-commit-id", "--name-status", "-r", recipe, head_exact)
    )
    expected_marker_status = "M" if base_has_marker else "A"
    if marker_diff != f"{expected_marker_status}\t{MARKER_PATH}":
        raise PresealError("preseal head is not a marker-only commit")
    payload = commit_bytes(root, head_exact, MARKER_PATH)
    marker = load_marker_bytes(payload)
    expected = expected_marker(root, base, recipe)
    if marker != expected:
        raise PresealError("preseal marker differs from exact recipe authority")
    for row in [*marker["recipeChanges"], *marker["canonicalSealedLocks"]]:
        relative = row["path"]
        if worktree_blob(root, relative) != row["blob"]:
            raise PresealError(f"worktree bytes differ from reviewed preseal input: {relative}")
    if worktree_blob(root, MARKER_PATH) != commit_blob(root, head_exact, MARKER_PATH):
        raise PresealError("worktree marker differs from exact head")
    return {
        "authority": False,
        "baseCommit": require_commit(base, "preseal base"),
        "baseTree": marker["baseTree"],
        "canonicalSealedLocks": marker["canonicalSealedLocks"],
        "consumerVerification": {
            "performed": False,
            "reason": "split-preseal-does-not-authorize-package-consumption",
            "status": "not_run_preseal",
        },
        "contractName": RECEIPT_CONTRACT,
        "contractVersion": 2,
        "generatedAt": datetime.now(UTC)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z"),
        "packageConsumerClaim": False,
        "nextAuthorityOracle": marker["nextAuthorityOracle"],
        "presealHeadCommit": head_exact,
        "presealHeadTree": tree(root, head_exact),
        "presealMarker": {
            "blob": commit_blob(root, head_exact, MARKER_PATH),
            "path": MARKER_PATH,
            "sha256": sha256_bytes(payload),
            "sizeBytes": len(payload),
        },
        "publicationAuthorized": False,
        "recipeCommitObserved": recipe,
        "recipeTree": marker["recipeTree"],
        "releaseClaim": False,
        "reviewedChanges": marker["recipeChanges"],
        "status": "preseal_validated",
        "syntheticPullRequestMerge": synthetic,
    }


def detect_mode(repo_root: Path, *, base: str, head: str) -> str:
    base_has = commit_path_exists(repo_root, base, MARKER_PATH)
    head_has = commit_path_exists(repo_root, head, MARKER_PATH)
    if base_has and not head_has:
        raise PresealError("retained preseal marker was deleted")
    marker_changed = head_has and (
        not base_has
        or commit_blob(repo_root, base, MARKER_PATH)
        != commit_blob(repo_root, head, MARKER_PATH)
    )
    return "preseal" if marker_changed else "sealed"


def resolve_dispatch_base(repo_root: Path, *, head: str) -> str:
    """Resolve the exact event base for a manual run at a linear main HEAD."""

    head_exact = require_commit(head, "dispatch head")
    head_parents = parents(repo_root, head_exact)
    if len(head_parents) != 1:
        raise PresealError("dispatch head is not on exact linear history")
    parent = head_parents[0]
    marker_diff = str(
        git(
            repo_root,
            "diff-tree",
            "--no-commit-id",
            "--name-status",
            "-r",
            parent,
            head_exact,
        )
    )
    if marker_diff in {f"A\t{MARKER_PATH}", f"M\t{MARKER_PATH}"}:
        marker = load_marker_bytes(commit_bytes(repo_root, head_exact, MARKER_PATH))
        return require_commit(str(marker.get("baseCommit", "")), "dispatch marker base")
    return parent


def validate_marker_seal_topology(
    repo_root: Path, *, sealed_commit: str, locked_recipe_commit: str
) -> None:
    """Apply extra strict topology when a retained preseal marker is present."""

    root = repo_root.resolve()
    sealed = require_commit(sealed_commit, "sealed commit")
    if not commit_path_exists(root, sealed, MARKER_PATH):
        if any(commit_path_exists(root, parent, MARKER_PATH) for parent in parents(root, sealed)):
            raise PresealError("sealed commit deleted the retained preseal marker")
        return
    recipe = validate_existing_sealed_marker(root, sealed)
    if recipe != require_commit(locked_recipe_commit, "locked recipe commit"):
        raise PresealError("sealed lock is not bound to the retained preseal recipe")


def write_absent(path: Path, payload: bytes) -> None:
    if path.exists() or path.is_symlink():
        raise PresealError(f"output already exists: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor = os.open(
        path,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
        0o600,
    )
    try:
        offset = 0
        while offset < len(payload):
            written = os.write(descriptor, payload[offset:])
            if written <= 0:
                raise PresealError("output write was partial")
            offset += written
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def write_or_refresh_marker(
    repo_root: Path, *, recipe_commit: str, output: Path, payload: bytes
) -> None:
    root = repo_root.resolve()
    target = output.absolute()
    canonical_target = root / MARKER_PATH
    if target != canonical_target:
        raise PresealError("marker output must be the canonical in-repo marker path")
    recipe = require_commit(recipe_commit, "marker recipe")
    recipe_has_marker = commit_path_exists(root, recipe, MARKER_PATH)
    if not recipe_has_marker:
        write_absent(target, payload)
        return
    if worktree_blob(root, MARKER_PATH) != commit_blob(root, recipe, MARKER_PATH):
        raise PresealError("retained marker worktree bytes differ from recipe")
    temporary = target.with_name(
        f".{target.name}.{os.getpid()}.{secrets.token_hex(8)}.tmp"
    )
    try:
        write_absent(temporary, payload)
        os.replace(temporary, target)
        directory = os.open(target.parent, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0))
        try:
            os.fsync(directory)
        finally:
            os.close(directory)
    finally:
        try:
            temporary.unlink()
        except FileNotFoundError:
            pass


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--base")
    parser.add_argument("--head", required=True)
    parser.add_argument("--checkout")
    parser.add_argument("--receipt-output", type=Path)
    parser.add_argument("--write-marker", type=Path)
    parser.add_argument("--detect", action="store_true")
    parser.add_argument("--resolve-dispatch-base", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = args.repo_root.resolve()
    try:
        if sum(
            (
                args.detect,
                args.resolve_dispatch_base,
                args.write_marker is not None,
                args.receipt_output is not None,
            )
        ) != 1:
            raise PresealError(
                "choose exactly one of --detect, --resolve-dispatch-base, "
                "--write-marker, or --receipt-output"
            )
        if args.resolve_dispatch_base:
            print(resolve_dispatch_base(root, head=args.head))
            return 0
        if args.base is None:
            raise PresealError("--base is required for this operation")
        if args.detect:
            print(detect_mode(root, base=args.base, head=args.head))
            return 0
        if args.write_marker is not None:
            if str(git(root, "rev-parse", "HEAD")) != require_commit(args.head, "recipe head"):
                raise PresealError("marker must be generated from the exact recipe checkout")
            require_clean_checkout(root, args.head)
            marker = expected_marker(root, args.base, args.head)
            write_or_refresh_marker(
                root,
                recipe_commit=args.head,
                output=args.write_marker,
                payload=canonical_json_bytes(marker),
            )
            print(f"split-preseal:marker={args.write_marker.absolute()}")
            return 0
        assert args.receipt_output is not None
        if not args.receipt_output.is_absolute():
            raise PresealError("receipt output must be absolute")
        receipt = validate_preseal(
            root,
            base=args.base,
            head=args.head,
            checkout=args.checkout,
        )
        write_absent(args.receipt_output, canonical_json_bytes(receipt))
        print(f"split-preseal:receipt={args.receipt_output}")
        return 0
    except (PresealError, OSError, subprocess.SubprocessError) as exc:
        print(f"split-preseal:error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
