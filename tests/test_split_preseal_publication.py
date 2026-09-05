from __future__ import annotations

import importlib.util
import json
import subprocess
from pathlib import Path
from types import ModuleType

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "ai" / "verify_split_preseal_publication.py"
PULL_REQUEST_WORKFLOW = REPO_ROOT / ".github" / "workflows" / "pull-request-ci.yml"
MAIN_WORKFLOW = REPO_ROOT / ".github" / "workflows" / "current-main-package-plane.yml"


def load_module() -> ModuleType:
    spec = importlib.util.spec_from_file_location("split_preseal", SCRIPT)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


preseal = load_module()


def test_android_import_responsiveness_inputs_are_explicit_preseal_recipe_paths() -> None:
    assert {
        "Chummer.Desktop.Runtime/GrantBoundDesktopWorkspaceRoamingSync.cs",
        "Chummer.Desktop.Runtime/IDesktopWorkspaceRoamingSync.cs",
        "Chummer.Desktop.Runtime/InProcessChummerClient.cs",
        "Chummer.Desktop.Runtime.Tests/Chummer.Desktop.Runtime.Tests.csproj",
        "Chummer.Tests/InProcessChummerClientRulesetPluginTests.cs",
    } <= preseal.ALLOWED_RECIPE_PATHS


def git(
    repository: Path, *arguments: str, input_text: str | None = None
) -> str:
    return subprocess.run(
        ["git", *arguments],
        cwd=repository,
        check=True,
        capture_output=True,
        text=True,
        input=input_text,
    ).stdout.strip()


def commit(repository: Path, message: str) -> str:
    git(repository, "add", "--all")
    git(repository, "commit", "--quiet", "-m", message)
    return git(repository, "rev-parse", "HEAD")


def synthetic_commit(repository: Path, tree: str, *parents: str) -> str:
    arguments = ["commit-tree", tree]
    for parent in parents:
        arguments.extend(("-p", parent))
    return git(repository, *arguments, input_text="synthetic checkout\n")


def checkout(repository: Path, revision: str) -> None:
    git(repository, "checkout", "--quiet", "--detach", revision)


def write_seal_locks(repository: Path, recipe: str, cycle: int = 1) -> None:
    (repository / "config" / "package-plane.lock.json").write_text(
        json.dumps(
            {
                "cycle": cycle,
                "uiOwnerFeed": {"packageRecipeCommit": recipe},
            }
        )
        + "\n",
        encoding="utf-8",
    )
    (repository / "config" / "ui-owner-package-plane.lock.json").write_text(
        json.dumps({"cycle": cycle, "packageRecipeCommit": recipe}) + "\n",
        encoding="utf-8",
    )


def fixture(tmp_path: Path) -> tuple[Path, str, str, str]:
    repository = tmp_path / "consumer"
    repository.mkdir(parents=True)
    git(repository, "init", "--quiet")
    git(repository, "config", "user.email", "tests@example.invalid")
    git(repository, "config", "user.name", "Tests")
    (repository / "config").mkdir()
    (repository / "config" / "package-plane.lock.json").write_text(
        '{"sealed":"base"}\n', encoding="utf-8"
    )
    (repository / "config" / "ui-owner-package-plane.lock.json").write_text(
        '{"sealed":"base"}\n', encoding="utf-8"
    )
    recipe_path = repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
    recipe_path.parent.mkdir(parents=True)
    recipe_path.write_text("# base recipe\n", encoding="utf-8")
    product_path = repository / "Chummer.Presentation" / "Product.cs"
    product_path.parent.mkdir()
    product_path.write_text("// product\n", encoding="utf-8")
    base = commit(repository, "base")

    recipe_path.write_text("# reviewed recipe\n", encoding="utf-8")
    oracle_path = repository / preseal.ORACLE_FIXTURE_PATH
    oracle_path.write_bytes((REPO_ROOT / preseal.ORACLE_FIXTURE_PATH).read_bytes())
    recipe = commit(repository, "recipe")
    marker = preseal.expected_marker(repository, base, recipe)
    marker_path = repository / preseal.MARKER_PATH
    marker_path.write_bytes(preseal.canonical_json_bytes(marker))
    head = commit(repository, "marker")
    return repository, base, recipe, head


def unsealed_recovery(repository: Path, first_marker: str) -> tuple[str, str]:
    recipe_path = repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
    recipe_path.write_text("# recovered reviewed recipe\n", encoding="utf-8")
    second_recipe = commit(repository, "recovered recipe")
    second_marker = preseal.expected_marker(repository, first_marker, second_recipe)
    marker_path = repository / preseal.MARKER_PATH
    preseal.write_or_refresh_marker(
        repository,
        recipe_commit=second_recipe,
        output=marker_path,
        payload=preseal.canonical_json_bytes(second_marker),
    )
    second_head = commit(repository, "recovered marker")
    return second_recipe, second_head


def test_exact_direct_and_synthetic_preseal_emit_only_nonclaims(tmp_path: Path) -> None:
    repository, base, recipe, head = fixture(tmp_path)
    receipt = preseal.validate_preseal(repository, base=base, head=head)
    assert receipt["status"] == "preseal_validated"
    assert receipt["authority"] is False
    assert receipt["publicationAuthorized"] is False
    assert receipt["packageConsumerClaim"] is False
    assert receipt["releaseClaim"] is False
    assert receipt["consumerVerification"] == {
        "performed": False,
        "reason": "split-preseal-does-not-authorize-package-consumption",
        "status": "not_run_preseal",
    }
    assert receipt["recipeCommitObserved"] == recipe
    marker_bytes = preseal.commit_bytes(repository, head, preseal.MARKER_PATH)
    assert receipt["presealMarker"] == {
        "blob": preseal.commit_blob(repository, head, preseal.MARKER_PATH),
        "path": preseal.MARKER_PATH,
        "sha256": preseal.sha256_bytes(marker_bytes),
        "sizeBytes": len(marker_bytes),
    }
    assert receipt["syntheticPullRequestMerge"] is False
    assert preseal.detect_mode(repository, base=base, head=head) == "preseal"
    assert preseal.resolve_dispatch_base(repository, head=head) == base

    merge = synthetic_commit(repository, git(repository, "rev-parse", "HEAD^{tree}"), base, head)
    checkout(repository, merge)
    synthetic = preseal.validate_preseal(
        repository, base=base, head=head, checkout=merge
    )
    assert synthetic["syntheticPullRequestMerge"] is True


def test_marker_binds_exact_tree_diff_and_unchanged_sealed_locks(tmp_path: Path) -> None:
    repository, base, recipe, head = fixture(tmp_path)
    marker = json.loads((repository / preseal.MARKER_PATH).read_text(encoding="utf-8"))
    assert marker == preseal.expected_marker(repository, base, recipe)
    rows = {row["path"]: row for row in marker["recipeChanges"]}
    assert set(rows) == {
        preseal.ORACLE_FIXTURE_PATH,
        "scripts/ai/verify_fresh_checkout_package_plane.py",
    }
    assert rows["scripts/ai/verify_fresh_checkout_package_plane.py"] == {
        "blob": git(
            repository,
            "rev-parse",
            f"{recipe}:scripts/ai/verify_fresh_checkout_package_plane.py",
        ),
        "path": "scripts/ai/verify_fresh_checkout_package_plane.py",
        "sha256": preseal.sha256_bytes(b"# reviewed recipe\n"),
        "sizeBytes": len(b"# reviewed recipe\n"),
        "status": "M",
    }
    assert rows[preseal.ORACLE_FIXTURE_PATH]["blob"] == (
        preseal.NEXT_AUTHORITY_ORACLE["canonicalLock"]["blob"]
    )
    assert marker["allowedSealChanges"] == list(preseal.CANONICAL_LOCK_PATHS)
    assert marker["baseCommit"] == base
    assert marker["contractVersion"] == 2
    assert marker["nextAuthorityOracle"] == preseal.NEXT_AUTHORITY_ORACLE
    assert marker["recipeTree"] == git(repository, "rev-parse", f"{recipe}^{{tree}}")
    assert preseal.canonical_json_bytes(marker) == preseal.commit_bytes(
        repository, head, preseal.MARKER_PATH
    )


@pytest.mark.parametrize("topology", ("reversed", "unrelated", "extra", "wrong-tree"))
def test_synthetic_checkout_topology_fails_closed(
    tmp_path: Path, topology: str
) -> None:
    repository, base, _, head = fixture(tmp_path)
    head_tree = git(repository, "rev-parse", f"{head}^{{tree}}")
    unrelated_tree = git(repository, "rev-parse", f"{base}^{{tree}}")
    unrelated = synthetic_commit(repository, unrelated_tree)
    if topology == "reversed":
        candidate = synthetic_commit(repository, head_tree, head, base)
    elif topology == "unrelated":
        candidate = synthetic_commit(repository, head_tree, unrelated, head)
    elif topology == "extra":
        candidate = synthetic_commit(repository, head_tree, base, head, unrelated)
    else:
        candidate = synthetic_commit(repository, unrelated_tree, base, head)
    checkout(repository, candidate)
    with pytest.raises(preseal.PresealError):
        preseal.validate_preseal(
            repository, base=base, head=head, checkout=candidate
        )


def test_dirty_or_index_hidden_worktree_fails_closed(tmp_path: Path) -> None:
    repository, base, _, head = fixture(tmp_path)
    recipe_path = repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
    recipe_path.write_text("# dirty\n", encoding="utf-8")
    with pytest.raises(preseal.PresealError, match="dirty"):
        preseal.validate_preseal(repository, base=base, head=head)
    git(repository, "restore", "scripts/ai/verify_fresh_checkout_package_plane.py")

    product_path = repository / "Chummer.Presentation" / "Product.cs"
    git(
        repository,
        "update-index",
        "--skip-worktree",
        "Chummer.Presentation/Product.cs",
    )
    product_path.write_text("// masked dirty\n", encoding="utf-8")
    assert git(repository, "status", "--porcelain") == ""
    with pytest.raises(preseal.PresealError, match="hidden index flags"):
        preseal.validate_preseal(repository, base=base, head=head)
    git(
        repository,
        "update-index",
        "--no-skip-worktree",
        "Chummer.Presentation/Product.cs",
    )


def test_recipe_rejects_product_source_unrelated_or_canonical_lock_change(
    tmp_path: Path,
) -> None:
    for case in ("product", "unrelated", "lock"):
        repository = tmp_path / case
        repository.mkdir()
        git(repository, "init", "--quiet")
        git(repository, "config", "user.email", "tests@example.invalid")
        git(repository, "config", "user.name", "Tests")
        (repository / "config").mkdir()
        for relative in preseal.CANONICAL_LOCK_PATHS:
            (repository / relative).write_text('{"sealed":"base"}\n', encoding="utf-8")
        recipe_path = repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
        recipe_path.parent.mkdir(parents=True)
        recipe_path.write_text("# base recipe\n", encoding="utf-8")
        base = commit(repository, "base")
        if case == "product":
            product = repository / "Chummer.Presentation" / "Product.cs"
            product.parent.mkdir()
            product.write_text("product drift\n", encoding="utf-8")
        elif case == "unrelated":
            (repository / "UNRELATED.md").write_text(
                "unrelated drift\n", encoding="utf-8"
            )
        else:
            (repository / preseal.CANONICAL_LOCK_PATHS[0]).write_text(
                '{"sealed":"stale"}\n', encoding="utf-8"
            )
        recipe = commit(repository, "invalid recipe")
        with pytest.raises(preseal.PresealError):
            preseal.expected_marker(repository, base, recipe)


def test_marker_claim_tamper_or_non_marker_head_fails_closed(tmp_path: Path) -> None:
    repository, base, _, head = fixture(tmp_path)
    marker_path = repository / preseal.MARKER_PATH
    marker = json.loads(marker_path.read_text(encoding="utf-8"))
    marker["authority"] = True
    marker_path.write_bytes(preseal.canonical_json_bytes(marker))
    tampered = commit(repository, "claim authority")
    with pytest.raises(preseal.PresealError):
        preseal.validate_preseal(repository, base=base, head=tampered)

    checkout(repository, head)
    marker = json.loads(marker_path.read_text(encoding="utf-8"))
    marker["nextAuthorityOracle"]["canonicalLock"]["rawSha256"] = "a" * 64
    marker_path.write_bytes(preseal.canonical_json_bytes(marker))
    substituted_oracle = commit(repository, "substitute oracle")
    with pytest.raises(preseal.PresealError):
        preseal.validate_preseal(repository, base=base, head=substituted_oracle)

    checkout(repository, head)
    (repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py").write_text(
        "# extra marker commit change\n", encoding="utf-8"
    )
    extra = commit(repository, "not marker only")
    with pytest.raises(preseal.PresealError, match="topology"):
        preseal.validate_preseal(repository, base=base, head=extra)


def test_marker_cannot_be_reused_as_a_new_preseal(tmp_path: Path) -> None:
    repository, _, _, head = fixture(tmp_path)
    (repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py").write_text(
        "# later recipe\n", encoding="utf-8"
    )
    later = commit(repository, "later")
    assert preseal.detect_mode(repository, base=head, head=later) == "sealed"
    with pytest.raises(preseal.PresealError, match="added or refreshed exactly once"):
        preseal.validate_preseal(repository, base=head, head=later)


def test_exact_unsealed_marker_can_be_superseded_once(tmp_path: Path) -> None:
    repository, _, _, first_marker = fixture(tmp_path)
    second_recipe, second_head = unsealed_recovery(repository, first_marker)

    assert preseal.validate_existing_unsealed_marker(repository, first_marker) == (
        first_marker
    )
    assert preseal.commit_blob(repository, first_marker, preseal.MARKER_PATH) == (
        preseal.commit_blob(repository, second_recipe, preseal.MARKER_PATH)
    )
    for relative in preseal.CANONICAL_LOCK_PATHS:
        assert preseal.commit_blob(repository, first_marker, relative) == (
            preseal.commit_blob(repository, second_recipe, relative)
        )
    assert preseal.detect_mode(
        repository, base=first_marker, head=second_head
    ) == "preseal"
    receipt = preseal.validate_preseal(
        repository, base=first_marker, head=second_head
    )
    assert receipt["baseCommit"] == first_marker
    assert receipt["recipeCommitObserved"] == second_recipe
    assert receipt["authority"] is False
    assert receipt["publicationAuthorized"] is False
    assert receipt["packageConsumerClaim"] is False
    assert receipt["releaseClaim"] is False
    assert preseal.resolve_dispatch_base(repository, head=second_head) == first_marker

    write_seal_locks(repository, second_head, cycle=2)
    second_seal = commit(repository, "recovered seal")
    assert preseal.validate_existing_sealed_marker(repository, second_seal) == (
        second_head
    )
    preseal.validate_marker_seal_topology(
        repository,
        sealed_commit=second_seal,
        locked_recipe_commit=second_head,
    )


@pytest.mark.parametrize("case", ("changed-lock", "extra-change", "tampered-marker"))
def test_unsealed_supersession_rejects_malformed_first_marker(
    tmp_path: Path,
    case: str,
) -> None:
    repository, _, first_recipe, first_marker = fixture(tmp_path)
    marker_path = repository / preseal.MARKER_PATH
    marker_bytes = preseal.commit_bytes(repository, first_marker, preseal.MARKER_PATH)
    checkout(repository, first_recipe)
    if case == "tampered-marker":
        marker = json.loads(marker_bytes)
        marker["authority"] = True
        marker_bytes = preseal.canonical_json_bytes(marker)
    marker_path.write_bytes(marker_bytes)
    if case == "changed-lock":
        (repository / preseal.CANONICAL_LOCK_PATHS[0]).write_text(
            '{"sealed":"changed in marker"}\n', encoding="utf-8"
        )
    elif case == "extra-change":
        (repository / "README.md").write_text("extra marker change\n", encoding="utf-8")
    malformed_marker = commit(repository, f"malformed marker {case}")
    recipe_path = repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
    recipe_path.write_text("# attempted recovery\n", encoding="utf-8")
    recovery_recipe = commit(repository, "attempted recovery recipe")

    with pytest.raises(preseal.PresealError, match="neither an exact seal"):
        preseal.expected_marker(repository, malformed_marker, recovery_recipe)


@pytest.mark.parametrize("case", ("changed-lock", "changed-marker"))
def test_unsealed_supersession_requires_recipe_to_retain_marker_and_locks(
    tmp_path: Path,
    case: str,
) -> None:
    repository, _, _, first_marker = fixture(tmp_path)
    recipe_path = repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
    recipe_path.write_text("# attempted recovery\n", encoding="utf-8")
    if case == "changed-lock":
        (repository / preseal.CANONICAL_LOCK_PATHS[1]).write_text(
            '{"sealed":"changed in recipe"}\n', encoding="utf-8"
        )
    else:
        marker_path = repository / preseal.MARKER_PATH
        marker = json.loads(marker_path.read_text(encoding="utf-8"))
        marker["releaseClaim"] = True
        marker_path.write_bytes(preseal.canonical_json_bytes(marker))
    recovery_recipe = commit(repository, f"invalid recovery recipe {case}")

    with pytest.raises(preseal.PresealError):
        preseal.expected_marker(repository, first_marker, recovery_recipe)


def test_unsealed_supersession_requires_marker_only_refresh(tmp_path: Path) -> None:
    repository, _, _, first_marker = fixture(tmp_path)
    recipe_path = repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
    recipe_path.write_text("# recovered reviewed recipe\n", encoding="utf-8")
    second_recipe = commit(repository, "recovered recipe")
    marker = preseal.expected_marker(repository, first_marker, second_recipe)
    marker_path = repository / preseal.MARKER_PATH
    preseal.write_or_refresh_marker(
        repository,
        recipe_commit=second_recipe,
        output=marker_path,
        payload=preseal.canonical_json_bytes(marker),
    )
    (repository / "README.md").write_text("extra marker change\n", encoding="utf-8")
    invalid_head = commit(repository, "non-marker-only recovery")

    with pytest.raises(preseal.PresealError, match="marker-only"):
        preseal.validate_preseal(
            repository, base=first_marker, head=invalid_head
        )


def test_unsealed_marker_cannot_be_superseded_twice(tmp_path: Path) -> None:
    repository, _, _, first_marker = fixture(tmp_path)
    _, second_head = unsealed_recovery(repository, first_marker)
    recipe_path = repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
    recipe_path.write_text("# forbidden third recipe\n", encoding="utf-8")
    third_recipe = commit(repository, "third recipe")

    with pytest.raises(preseal.PresealError, match="neither an exact seal"):
        preseal.expected_marker(repository, second_head, third_recipe)


def test_retained_marker_can_start_one_later_exact_preseal_cycle(tmp_path: Path) -> None:
    repository, _, first_recipe, first_marker = fixture(tmp_path)
    write_seal_locks(repository, first_marker)
    first_seal = commit(repository, "first seal")
    preseal.validate_marker_seal_topology(
        repository,
        sealed_commit=first_seal,
        locked_recipe_commit=first_marker,
    )

    recipe_path = repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
    recipe_path.write_text("# second reviewed recipe\n", encoding="utf-8")
    second_recipe = commit(repository, "second recipe")
    second_marker = preseal.expected_marker(repository, first_seal, second_recipe)
    marker_path = repository / preseal.MARKER_PATH
    prior_inode = marker_path.stat().st_ino
    preseal.write_or_refresh_marker(
        repository,
        recipe_commit=second_recipe,
        output=marker_path,
        payload=preseal.canonical_json_bytes(second_marker),
    )
    assert marker_path.stat().st_ino != prior_inode
    second_head = commit(repository, "second marker")

    assert preseal.detect_mode(repository, base=first_seal, head=second_head) == "preseal"
    receipt = preseal.validate_preseal(
        repository, base=first_seal, head=second_head
    )
    assert receipt["recipeCommitObserved"] == second_recipe
    assert receipt["authority"] is False
    assert preseal.resolve_dispatch_base(repository, head=second_head) == first_seal


def test_retained_marker_remains_valid_when_next_oracle_rotates(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    repository, _, _, first_marker = fixture(tmp_path)
    write_seal_locks(repository, first_marker)
    first_seal = commit(repository, "first seal")

    oracle_path = repository / preseal.ORACLE_FIXTURE_PATH
    oracle_value = json.loads(oracle_path.read_text(encoding="utf-8"))
    source_files = oracle_value["consumer"]["sourceFiles"]
    source_files[next(iter(sorted(source_files)))] = "0" * 64
    oracle_path.write_bytes(preseal.canonical_json_bytes(oracle_value))
    recipe_path = repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
    recipe_path.write_text("# rotated oracle recipe\n", encoding="utf-8")
    second_recipe = commit(repository, "rotate next authority oracle")

    oracle_bytes = preseal.commit_bytes(
        repository, second_recipe, preseal.ORACLE_FIXTURE_PATH
    )
    rotated = json.loads(json.dumps(preseal.NEXT_AUTHORITY_ORACLE))
    rotated_lock = rotated["canonicalLock"]
    rotated_lock["blob"] = preseal.commit_blob(
        repository, second_recipe, preseal.ORACLE_FIXTURE_PATH
    )
    rotated_lock["rawSha256"] = preseal.sha256_bytes(oracle_bytes)
    rotated_lock["rawSizeBytes"] = len(oracle_bytes)
    canonical = preseal.canonical_json_bytes(json.loads(oracle_bytes))
    rotated_lock["semanticCanonicalSha256"] = preseal.sha256_bytes(canonical)
    rotated_lock["semanticCanonicalSizeBytes"] = len(canonical)
    monkeypatch.setattr(preseal, "NEXT_AUTHORITY_ORACLE", rotated)

    assert preseal.validate_existing_sealed_marker(repository, first_seal) == first_marker
    second_marker = preseal.expected_marker(repository, first_seal, second_recipe)
    assert second_marker["nextAuthorityOracle"] == rotated


def test_marker_refresh_rejects_dirty_prior_bytes_or_noncanonical_output(
    tmp_path: Path,
) -> None:
    repository, _, first_recipe, _ = fixture(tmp_path)
    first_marker = git(repository, "rev-parse", "HEAD")
    write_seal_locks(repository, first_marker)
    first_seal = commit(repository, "first seal")
    recipe_path = repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
    recipe_path.write_text("# second recipe\n", encoding="utf-8")
    second_recipe = commit(repository, "second recipe")
    marker = preseal.expected_marker(repository, first_seal, second_recipe)
    marker_path = repository / preseal.MARKER_PATH
    marker_path.write_text("dirty prior marker\n", encoding="utf-8")
    with pytest.raises(preseal.PresealError, match="worktree bytes differ"):
        preseal.write_or_refresh_marker(
            repository,
            recipe_commit=second_recipe,
            output=marker_path,
            payload=preseal.canonical_json_bytes(marker),
        )
    git(repository, "restore", preseal.MARKER_PATH)
    with pytest.raises(preseal.PresealError, match="canonical in-repo"):
        preseal.write_or_refresh_marker(
            repository,
            recipe_commit=second_recipe,
            output=repository / "config" / "other-marker.json",
            payload=preseal.canonical_json_bytes(marker),
        )


def test_arbitrary_retained_marker_or_wrong_prior_lock_binding_fails_closed(
    tmp_path: Path,
) -> None:
    repository, _, recipe, _ = fixture(tmp_path / "wrong-lock")
    write_seal_locks(repository, "f" * 40)
    wrong_lock_seal = commit(repository, "wrong lock binding")
    with pytest.raises(preseal.PresealError, match="do not bind"):
        preseal.validate_existing_sealed_marker(repository, wrong_lock_seal)

    repository, _, recipe, marker_head = fixture(tmp_path / "arbitrary-marker")
    write_seal_locks(repository, marker_head)
    seal = commit(repository, "valid seal")
    marker_path = repository / preseal.MARKER_PATH
    marker = json.loads(marker_path.read_text(encoding="utf-8"))
    marker["baseCommit"] = "e" * 40
    marker_path.write_bytes(preseal.canonical_json_bytes(marker))
    arbitrary_base = commit(repository, "arbitrary retained marker")
    recipe_path = repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
    recipe_path.write_text("# attempted next recipe\n", encoding="utf-8")
    next_recipe = commit(repository, "attempt next recipe")
    with pytest.raises(preseal.PresealError):
        preseal.expected_marker(repository, arbitrary_base, next_recipe)


def test_retained_marker_allows_only_exact_two_lock_seal(tmp_path: Path) -> None:
    repository, _, recipe, marker = fixture(tmp_path)
    write_seal_locks(repository, marker)
    seal = commit(repository, "seal")
    preseal.validate_marker_seal_topology(
        repository, sealed_commit=seal, locked_recipe_commit=marker
    )
    assert preseal.resolve_dispatch_base(repository, head=seal) == marker
    merge = synthetic_commit(
        repository, git(repository, "rev-parse", f"{seal}^{{tree}}"), marker, seal
    )
    preseal.validate_marker_seal_topology(
        repository, sealed_commit=merge, locked_recipe_commit=marker
    )
    with pytest.raises(preseal.PresealError, match="retained preseal recipe"):
        preseal.validate_marker_seal_topology(
            repository, sealed_commit=seal, locked_recipe_commit=recipe
        )


def test_retained_marker_rejects_incomplete_or_extra_seal(tmp_path: Path) -> None:
    for case in ("incomplete", "extra"):
        repository, _, recipe, marker = fixture(tmp_path / case)
        (repository / preseal.CANONICAL_LOCK_PATHS[0]).write_text(
            json.dumps({"uiOwnerFeed": {"packageRecipeCommit": marker}}) + "\n",
            encoding="utf-8",
        )
        if case == "extra":
            (repository / preseal.CANONICAL_LOCK_PATHS[1]).write_text(
                json.dumps({"packageRecipeCommit": marker}) + "\n",
                encoding="utf-8",
            )
            (repository / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py").write_text(
                "# seal drift\n", encoding="utf-8"
            )
        seal = commit(repository, case)
        with pytest.raises(preseal.PresealError, match="exactly the two"):
            preseal.validate_marker_seal_topology(
                repository, sealed_commit=seal, locked_recipe_commit=marker
            )


def test_retained_marker_deletion_fails_before_or_during_sealed_verification(
    tmp_path: Path,
) -> None:
    repository, _, _, marker = fixture(tmp_path)
    (repository / preseal.MARKER_PATH).unlink()
    deleted = commit(repository, "delete retained marker")
    with pytest.raises(preseal.PresealError, match="deleted"):
        preseal.detect_mode(repository, base=marker, head=deleted)
    with pytest.raises(preseal.PresealError, match="deleted"):
        preseal.validate_marker_seal_topology(
            repository,
            sealed_commit=deleted,
            locked_recipe_commit="f" * 40,
        )


def test_regular_sealed_history_without_marker_keeps_existing_strict_lane(
    tmp_path: Path,
) -> None:
    repository = tmp_path / "regular"
    repository.mkdir()
    git(repository, "init", "--quiet")
    git(repository, "config", "user.email", "tests@example.invalid")
    git(repository, "config", "user.name", "Tests")
    (repository / "README.md").write_text("base\n", encoding="utf-8")
    base = commit(repository, "base")
    (repository / "README.md").write_text("sealed\n", encoding="utf-8")
    sealed = commit(repository, "sealed")
    assert preseal.detect_mode(repository, base=base, head=sealed) == "sealed"
    preseal.validate_marker_seal_topology(
        repository, sealed_commit=sealed, locked_recipe_commit="f" * 40
    )


def test_pull_request_workflow_separates_preseal_from_full_consumer_claim() -> None:
    source = PULL_REQUEST_WORKFLOW.read_text(encoding="utf-8")
    assert source.count("Resolve sealed or split-preseal validation mode") == 2
    assert "Split-preseal contract tests without consumer or release claims" in source
    assert "Validate split-preseal without package consumer or release claims" in source
    assert "Upload non-authoritative split-preseal receipt" in source
    assert "if: steps.package-plane-state.outputs.mode == 'sealed'" in source
    assert "if: steps.package-plane-state.outputs.mode == 'preseal'" in source
    assert "UI_SPLIT_PRESEAL.generated.json" in source
    assert "Required split-preseal topology and nonclaim receipt" in source
    assert "UI_REQUIRED_SPLIT_PRESEAL.generated.json" in source
    assert "Merge method: rebase only" in source


def test_current_main_workflow_emits_exact_sealed_or_nonclaim_receipt() -> None:
    source = MAIN_WORKFLOW.read_text(encoding="utf-8")
    assert "push:" in source and "workflow_dispatch:" in source
    assert "Verify exact sealed current main package consumer" in source
    assert "Validate current main split-preseal without consumer or release claims" in source
    assert "UI_CURRENT_MAIN_PACKAGE_PLANE.generated.json" in source
    assert "UI_CURRENT_MAIN_PRESEAL.generated.json" in source
    assert "--resolve-dispatch-base" in source
    assert 'test "$EVENT_REF" = "refs/heads/main"' in source
    assert 'test "$EVENT_REF_NAME" = "main"' in source
    assert 'test "$EVENT_REF_TYPE" = "branch"' in source
    assert "persist-credentials: false" in source
