import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import pytest

PATH = Path(__file__).resolve().parents[1] / "scripts/build-core-content-bundle.py"
spec = importlib.util.spec_from_file_location("core_bundle_builder", PATH)
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)

def git(root, *args):
    return subprocess.check_output(["git", "-C", str(root), *args]).decode().strip()

@pytest.fixture
def source(tmp_path):
    root = tmp_path / "source"; root.mkdir(); git(root, "init", "-q")
    data = root / "Chummer/data"; data.mkdir(parents=True)
    for name in ("settings.xml", "settings-all-sources.xml", "lifemodules.xml"):
        (data / name).write_text("<rules/>")
    git(root, "add", "."); git(root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "-c", "core.hooksPath=/dev/null", "commit", "-qm", "Fixture")
    return root, git(root, "rev-parse", "HEAD")

def test_exports_pinned_git_bytes_and_embeds_empty_directories(source, tmp_path):
    root, commit = source
    (root / "Chummer/data/settings.xml").write_text("<developer-override/>")
    (root / "Chummer/data/secret.txt").write_text("fixture-secret-not-for-export")
    output = tmp_path / "bundle"
    digest = builder.build(root, output, approved={commit})
    manifest = (output / builder.MANIFEST).read_bytes()
    assert digest == hashlib.sha256(manifest).hexdigest()
    entries = json.loads(manifest)["files"]
    assert not (output / "Chummer/data/secret.txt").exists()
    assert (output / "Chummer/data/settings.xml").read_text() == "<rules/>"
    assert (output / "Chummer/customdata/.bundle-directory").is_file()
    for item in entries:
        content = (output / item["path"]).read_bytes()
        assert len(content) == item["bytes"] and hashlib.sha256(content).hexdigest() == item["sha256"]
    assert builder.build(root, output, approved={commit}) == digest

def test_unapproved_source_and_changed_existing_output_are_rejected(source, tmp_path):
    root, commit = source; output = tmp_path / "bundle"
    with pytest.raises(ValueError): builder.build(root, output)
    builder.build(root, output, approved={commit})
    (output / "Chummer/data/settings.xml").write_text("tampered")
    with pytest.raises(ValueError): builder.build(root, output, approved={commit})

def test_tracked_symlinks_are_rejected(source, tmp_path):
    root, _ = source; (root / "Chummer/data/link").symlink_to("settings.xml")
    git(root, "add", "."); git(root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "-c", "core.hooksPath=/dev/null", "commit", "-qm", "Symlink")
    with pytest.raises(ValueError): builder.build(root, tmp_path / "bundle", approved={git(root, "rev-parse", "HEAD")})


def test_missing_required_rules_are_rejected(source, tmp_path):
    root, _ = source
    (root / "Chummer/data/lifemodules.xml").unlink()
    git(root, "add", "-u")
    git(root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "-c", "core.hooksPath=/dev/null", "commit", "-qm", "Missing required rules")
    with pytest.raises(ValueError):
        builder.build(root, tmp_path / "bundle", approved={git(root, "rev-parse", "HEAD")})
