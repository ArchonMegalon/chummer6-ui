"""Real Git fixtures for data-only Core input admission (no network or builds)."""
from __future__ import annotations

import importlib.util
import os
import subprocess
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/ai/verify_fresh_checkout_package_plane.py"
spec = importlib.util.spec_from_file_location("core_projection_package_plane", SCRIPT)
assert spec is not None and spec.loader is not None
plane = importlib.util.module_from_spec(spec)
spec.loader.exec_module(plane)


def git(root: Path, *args: str) -> str:
    return subprocess.run([str(plane.TRUSTED_GIT), *args], cwd=root,
                          check=True, text=True, capture_output=True).stdout.strip()


@pytest.fixture
def content(tmp_path: Path):
    root = tmp_path / "owner"
    root.mkdir()
    git(root, "init", "--quiet")
    git(root, "config", "user.name", "Projection Test")
    git(root, "config", "user.email", "projection@example.invalid")
    git(root, "config", "core.autocrlf", "false")
    repository = "https://example.invalid/core.git"
    git(root, "remote", "add", "origin", repository)
    for relative in ("data/settings.xml", "lang/de-de.xml", "customdata/pack/manifest.xml"):
        path = root / "Chummer" / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b"<fixture />\n")
    (root / ".gitignore").write_text("*.ignored\n", encoding="utf-8")
    git(root, "add", ".")
    git(root, "commit", "--quiet", "-m", "semantic source")
    source = git(root, "rev-parse", "HEAD")
    (root / "recipe.txt").write_text("package recipe\n", encoding="utf-8")
    git(root, "add", "recipe.txt")
    git(root, "commit", "--quiet", "-m", "recipe only")
    return root, {"repository": repository, "runtimeSourceCommit": source,
                  "packageRecipeCommit": git(root, "rev-parse", "HEAD")}


def observe(content):
    root, authority = content
    return plane.core_projection_content_inventory(root, authority, dict(os.environ))


def test_recipe_checkout_uses_exact_semantic_content_without_compiling_sources(content):
    first = observe(content)
    assert first == observe(content)
    assert first["fileCount"] == 3
    assert first["checkoutCommit"] == content[1]["packageRecipeCommit"]
    assert first["runtimeSourceCommit"] == content[1]["runtimeSourceCommit"]
    assert first["usage"] == "read-only-rule-data-not-project-reference"
    assert len(first["contentInventorySha256"]) == 64
    assert git(content[0], "status", "--porcelain") == ""


def test_semantic_checkout_is_also_accepted_with_same_content_digest(content):
    before = observe(content)
    git(content[0], "switch", "--detach", content[1]["runtimeSourceCommit"])
    after = observe(content)
    assert after["contentInventorySha256"] == before["contentInventorySha256"]
    assert after["checkoutCommit"] == content[1]["runtimeSourceCommit"]


@pytest.mark.parametrize("kind", ["bytes", "missing", "untracked", "ignored", "empty-directory",
                                  "custom-directory", "file-symlink", "directory-symlink", "parent-symlink"])
def test_mutated_data_or_directory_membership_cannot_pass(content, kind, tmp_path):
    root, _ = content
    settings = root / "Chummer/data/settings.xml"
    if kind == "bytes":
        # Index flags must not conceal changed runtime bytes.
        git(root, "update-index", "--assume-unchanged", "Chummer/data/settings.xml")
        settings.write_bytes(b"<changed />\n")
    elif kind == "missing":
        settings.unlink()
    elif kind in ("untracked", "ignored"):
        (settings.parent / ("extra.ignored" if kind == "ignored" else "extra.xml")).write_bytes(b"extra")
    elif kind in ("empty-directory", "custom-directory"):
        (root / ("Chummer/data/extra" if kind == "empty-directory" else "Chummer/customdata/extra")).mkdir()
    elif kind == "file-symlink":
        outside = tmp_path / "same.xml"
        outside.write_bytes(settings.read_bytes())
        settings.unlink()
        settings.symlink_to(outside)
    else:
        original = root / ("Chummer/data" if kind == "directory-symlink" else "Chummer")
        outside = tmp_path / "same-directory"
        original.rename(outside)
        original.symlink_to(outside, target_is_directory=True)
    with pytest.raises(plane.VerificationError):
        observe(content)


def test_wrong_repository_or_checkout_is_rejected(content):
    root, authority = content
    git(root, "remote", "set-url", "origin", "https://example.invalid/other.git")
    with pytest.raises(plane.VerificationError, match="repository differs"):
        observe(content)
    git(root, "remote", "set-url", "origin", authority["repository"])
    git(root, "commit", "--allow-empty", "--quiet", "-m", "unexpected checkout")
    with pytest.raises(plane.VerificationError, match="checkout differs"):
        observe(content)


def test_relative_or_symlink_root_is_rejected(content, tmp_path):
    root, authority = content
    with pytest.raises(plane.VerificationError, match="explicit, absolute"):
        plane.core_projection_content_inventory(Path("owner"), authority, dict(os.environ))
    alias = tmp_path / "alias"
    alias.symlink_to(root, target_is_directory=True)
    with pytest.raises(plane.VerificationError, match="non-symlink"):
        plane.core_projection_content_inventory(alias, authority, dict(os.environ))


def test_source_committed_symlink_is_not_authorized_content(content, tmp_path):
    root, authority = content
    outside = tmp_path / "outside.xml"
    outside.write_bytes(b"<fixture />")
    (root / "Chummer/data/link.xml").symlink_to(outside)
    git(root, "add", "Chummer/data/link.xml")
    git(root, "commit", "--quiet", "-m", "unsafe content")
    head = git(root, "rev-parse", "HEAD")
    authority.update(runtimeSourceCommit=head, packageRecipeCommit=head)
    with pytest.raises(plane.VerificationError, match="unsafe member"):
        observe(content)


def test_ci_passes_content_explicitly_and_rechecks_before_recording_success():
    source = SCRIPT.read_text(encoding="utf-8")
    execution = source.split("test_executions: list[dict[str, Any]] = []", 1)[1].split(
        "focused_test_assembly_path = consumer / PRODUCT_TEST_ASSEMBLY", 1)[0]
    assert execution.count("core_projection_content_inventory(") == 2
    assert '"coreProjectionContent": core_content' in execution
    assert 'f"ChummerCoreContentRoot={core_content_root}"' in execution
    assert execution.index("Core projection content changed during product tests") < execution.index(
        "test_executions.append(full_test_execution)")
    strict = (ROOT / "scripts/ai/verify.sh").read_text(encoding="utf-8")
    assert "CHUMMER_CORE_PROJECTION_CONTENT_ROOT:?" in strict
    assert strict.count("verify_creation_projection_content.py --core-root") == 2
    assert '--test-parameter "ChummerCoreContentRoot=$core_projection_root"' in strict
    assert f"--minimum-expected-tests {plane.FULL_PRODUCT_TEST_MINIMUM_TESTS}" in strict
    test = (ROOT / "Chummer.Product.UnitTests/CreationWizardCoreProjectionTests.cs").read_text(encoding="utf-8")
    assert 'TestContext.Properties.TryGetValue("ChummerCoreContentRoot"' in test
    assert "Assert.Inconclusive" not in test and "[Ignore" not in test
