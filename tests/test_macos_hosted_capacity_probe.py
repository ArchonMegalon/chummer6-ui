from __future__ import annotations

import importlib.util
import inspect
import json
import re
import subprocess
import sys
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
TOOL = REPO_ROOT / "scripts" / "macos_hosted_capacity_probe.py"
WORKFLOW = (
    REPO_ROOT
    / ".github"
    / "workflows"
    / "macos-hosted-capacity-probe.yml"
)


def load_tool_module():
    spec = importlib.util.spec_from_file_location(
        "macos_hosted_capacity_probe", TOOL
    )
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def make_xcode(applications: Path, name: str) -> Path:
    bundle = applications / name
    (bundle / "Contents" / "Developer").mkdir(parents=True)
    return bundle


def hosted_environment(tmp_path: Path) -> dict[str, str]:
    runner_temp = tmp_path / "runner-temp"
    workspace = tmp_path / "workspace"
    runner_temp.mkdir()
    workspace.mkdir()
    return {
        "CI": "true",
        "GITHUB_ACTIONS": "true",
        "GITHUB_EVENT_NAME": "pull_request",
        "GITHUB_REPOSITORY": "ArchonMegalon/chummer6-ui",
        "GITHUB_WORKSPACE": str(workspace),
        "ImageOS": "macos15",
        "ImageVersion": "20260720.1",
        "RUNNER_ARCH": "ARM64",
        "RUNNER_ENVIRONMENT": "github-hosted",
        "RUNNER_OS": "macOS",
        "RUNNER_TEMP": str(runner_temp),
        "CHUMMER_MACOS_HOSTED_PROBE_RUNNER_IMAGE": "macos-15",
    }


def test_strict_xcode_candidate_accepts_only_real_direct_versioned_bundle(
    tmp_path: Path,
) -> None:
    tool = load_tool_module()
    applications = tmp_path / "Applications"
    applications.mkdir()
    candidate = make_xcode(applications, "Xcode_16.4.app")

    resolved, version = tool.validate_xcode_candidate(
        candidate, applications
    )

    assert resolved == candidate
    assert version == (16, 4)


@pytest.mark.parametrize(
    "relative_name",
    (
        "Xcode_latest.app",
        "Xcode_16.app",
        "Xcode_16.4_beta.app",
        "Xcode_16.4.app.extra",
    ),
)
def test_xcode_candidate_rejects_unbounded_names(
    tmp_path: Path,
    relative_name: str,
) -> None:
    tool = load_tool_module()
    applications = tmp_path / "Applications"
    applications.mkdir()
    candidate = make_xcode(applications, relative_name)

    with pytest.raises(tool.ProbeFailure, match="strict versioned name"):
        tool.validate_xcode_candidate(candidate, applications)


def test_xcode_candidate_rejects_symlink_and_non_applications_path(
    tmp_path: Path,
) -> None:
    tool = load_tool_module()
    applications = tmp_path / "Applications"
    applications.mkdir()
    real = make_xcode(tmp_path, "Xcode_16.4.app")
    symlink = applications / "Xcode_16.4.app"
    symlink.symlink_to(real, target_is_directory=True)

    with pytest.raises(tool.ProbeFailure, match="symlink"):
        tool.validate_xcode_candidate(symlink, applications)
    with pytest.raises(tool.ProbeFailure, match="outside"):
        tool.validate_xcode_candidate(real, applications)


def test_cleanup_plan_preserves_xcode_select_physical_bundle(
    tmp_path: Path,
) -> None:
    tool = load_tool_module()
    applications = tmp_path / "Applications"
    applications.mkdir()
    older = make_xcode(applications, "Xcode_16.3.app")
    active = make_xcode(applications, "Xcode_16.4.app")
    (applications / "Xcode.app").symlink_to(
        active, target_is_directory=True
    )
    selected = applications / "Xcode.app" / "Contents" / "Developer"

    plan = tool.build_xcode_cleanup_plan(
        applications,
        selected,
        size_provider=lambda _: 1024,
    )

    assert plan["active"]["physicalBundle"] == active
    assert [candidate["path"] for candidate in plan["inactive"]] == [
        str(older)
    ]
    preserved = [
        candidate
        for candidate in plan["candidates"]
        if candidate["active"]
    ]
    assert [candidate["path"] for candidate in preserved] == [str(active)]


def test_cleanup_plan_ignores_versioned_symlink_alias_before_deletion(
    tmp_path: Path,
) -> None:
    tool = load_tool_module()
    applications = tmp_path / "Applications"
    applications.mkdir()
    active = make_xcode(applications, "Xcode_16.4.app")
    inactive = make_xcode(applications, "Xcode_16.2.app")
    outside = make_xcode(tmp_path, "Xcode_16.3.app")
    (applications / "Xcode_16.3.app").symlink_to(
        outside, target_is_directory=True
    )

    plan = tool.build_xcode_cleanup_plan(
        applications,
        active / "Contents" / "Developer",
        size_provider=lambda _: 1024,
    )

    assert [candidate["path"] for candidate in plan["inactive"]] == [
        str(inactive)
    ]
    assert plan["ignoredSymlinks"] == [
        {
            "path": str(applications / "Xcode_16.3.app"),
            "reason": "version-alias-symlink-not-deleteable",
            "version": [16, 3],
        }
    ]


def test_cleanup_plan_rejects_ungoverned_symlink_alias(
    tmp_path: Path,
) -> None:
    tool = load_tool_module()
    applications = tmp_path / "Applications"
    applications.mkdir()
    active = make_xcode(applications, "Xcode_16.4.app")
    (applications / "Xcode_latest.app").symlink_to(
        active, target_is_directory=True
    )

    with pytest.raises(tool.ProbeFailure, match="strict versioned name"):
        tool.build_xcode_cleanup_plan(
            applications,
            active / "Contents" / "Developer",
            size_provider=lambda _: 1024,
        )


def test_cleanup_plan_is_count_and_byte_bounded(tmp_path: Path) -> None:
    tool = load_tool_module()
    applications = tmp_path / "Applications"
    applications.mkdir()
    active = make_xcode(applications, "Xcode_26.0.app")
    for index in range(tool.MAX_XCODE_DELETE_COUNT + 1):
        make_xcode(applications, f"Xcode_15.{index}.app")

    with pytest.raises(tool.ProbeFailure, match="count exceeds"):
        tool.build_xcode_cleanup_plan(
            applications,
            active / "Contents" / "Developer",
            size_provider=lambda _: 1024,
        )

    second_root = tmp_path / "SecondApplications"
    second_root.mkdir()
    second_active = make_xcode(second_root, "Xcode_26.0.app")
    make_xcode(second_root, "Xcode_15.0.app")
    with pytest.raises(tool.ProbeFailure, match="bytes exceed"):
        tool.build_xcode_cleanup_plan(
            second_root,
            second_active / "Contents" / "Developer",
            size_provider=lambda path: (
                1024
                if path == second_active
                else tool.MAX_XCODE_DELETE_BYTES + 1
            ),
        )


def test_capacity_gate_is_exactly_twenty_gib() -> None:
    tool = load_tool_module()

    tool.require_capacity(20 * 1024**3)
    with pytest.raises(tool.ProbeFailure, match="less than"):
        tool.require_capacity(20 * 1024**3 - 1)


def test_hosted_context_rejects_self_hosted_and_image_drift(
    tmp_path: Path,
) -> None:
    tool = load_tool_module()
    environment = hosted_environment(tmp_path)
    validated = tool.validate_hosted_context(
        environment,
        system_name="Darwin",
        machine_name="arm64",
    )
    assert validated["runnerImage"] == "macos-15"

    macos_26 = dict(environment)
    macos_26["CHUMMER_MACOS_HOSTED_PROBE_RUNNER_IMAGE"] = "macos-26"
    macos_26["ImageOS"] = "macos26"
    validated = tool.validate_hosted_context(
        macos_26,
        system_name="Darwin",
        machine_name="arm64",
    )
    assert validated["runnerImage"] == "macos-26"

    self_hosted = dict(environment)
    self_hosted["RUNNER_ENVIRONMENT"] = "self-hosted"
    with pytest.raises(tool.ProbeFailure, match="RUNNER_ENVIRONMENT"):
        tool.validate_hosted_context(
            self_hosted,
            system_name="Darwin",
            machine_name="arm64",
        )

    drifted = dict(environment)
    drifted["ImageOS"] = "macos26"
    with pytest.raises(tool.ProbeFailure, match="ImageOS"):
        tool.validate_hosted_context(
            drifted,
            system_name="Darwin",
            machine_name="arm64",
        )


def test_secretless_gate_rejects_release_authority(
    tmp_path: Path,
) -> None:
    tool = load_tool_module()
    environment = hosted_environment(tmp_path)
    tool.assert_secretless_environment(environment)

    environment["CHUMMER_MACOS_NOTARY_KEY_P8_BASE64"] = "not-a-real-key"
    with pytest.raises(tool.ProbeFailure, match="authority"):
        tool.assert_secretless_environment(environment)


def test_receipt_is_nonpublishing_and_does_not_copy_environment(
    tmp_path: Path,
) -> None:
    tool = load_tool_module()
    environment = hosted_environment(tmp_path)
    environment["UNRELATED_SECRET_VALUE"] = "must-not-appear"

    receipt = tool.base_receipt(environment)
    rendered = json.dumps(receipt, sort_keys=True)

    assert receipt["contractName"] == (
        "chummer6-ui.macos-hosted-capacity-probe.v1"
    )
    assert receipt["nonPublishing"] == {
        "artifactBuilt": False,
        "notarizationSubmitted": False,
        "publicationAttempted": False,
        "releaseAuthorityAccepted": False,
        "signingAttempted": False,
    }
    assert "must-not-appear" not in rendered
    assert set(receipt["checks"]) == {
        "capacity",
        "dummyKeychainLifecycle",
        "hostedRunnerContext",
        "secretless",
        "tinyDmgLifecycle",
        "toolchain",
    }


def test_failed_probe_still_emits_a_nonsecret_receipt(
    tmp_path: Path,
) -> None:
    tool = load_tool_module()
    environment = hosted_environment(tmp_path)
    receipt_path = (
        Path(environment["RUNNER_TEMP"])
        / "MACOS_HOSTED_CAPACITY_PROBE.generated.json"
    )

    result = tool.run_probe(receipt_path, environment)

    assert result == 1
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    assert receipt["status"] == "failed"
    assert receipt["failure"]["code"] == "wrong-native-platform"
    assert receipt["nonPublishing"]["signingAttempted"] is False
    assert receipt["nonPublishing"]["publicationAttempted"] is False


def test_probe_calls_context_gate_before_bounded_deletion() -> None:
    tool = load_tool_module()
    run_source = inspect.getsource(tool.run_probe)
    cleanup_source = inspect.getsource(tool.perform_bounded_cleanup)

    assert run_source.index("validate_hosted_context(") < run_source.index(
        "perform_bounded_cleanup("
    )
    assert '("sudo", "-n", "/bin/rm", "-rf", "--"' in cleanup_source
    assert "validate_xcode_candidate(" in cleanup_source
    assert "revalidate_active_xcode(expected_active)" in cleanup_source


def test_probe_never_signs_submits_notarization_or_publishes() -> None:
    text = TOOL.read_text(encoding="utf-8")

    assert 'APPLICATIONS_ROOT = Path("/Applications")' in text
    assert '"security", "import"' not in text
    assert '"codesign", "--sign"' not in text
    assert '"notarytool", "submit"' not in text
    assert "shell=True" not in text
    assert text.count('("sudo", "-n", "/bin/rm", "-rf", "--"') == 1


def test_workflow_is_pinned_secretless_and_uploads_failure_receipt() -> None:
    text = WORKFLOW.read_text(encoding="utf-8")

    assert "pull_request:" in text
    assert "workflow_dispatch:" in text
    assert "pull_request_target:" not in text
    assert "- macos-15" in text
    assert "- macos-26" in text
    assert "macos-latest" not in text
    assert "self-hosted" not in text
    assert "environment:" not in text
    assert "${{ secrets." not in text
    assert "id-token: write" not in text
    assert "contents: read" in text
    assert "contents: write" not in text
    assert "python3 scripts/macos_hosted_capacity_probe.py" in text
    assert "--receipt" in text
    assert "if: ${{ always() }}" in text
    assert "if-no-files-found: error" in text
    assert "MACOS_HOSTED_CAPACITY_PROBE.generated.json" in text
    assert (
        "PROBE_RECEIPT: ${{ runner.temp }}/"
        "MACOS_HOSTED_CAPACITY_PROBE.generated.json"
    ) in text
    assert (
        "path: ${{ runner.temp }}/"
        "MACOS_HOSTED_CAPACITY_PROBE.generated.json"
    ) in text
    job_prefix, steps = text.split("    steps:", maxsplit=1)
    assert "${{ runner.temp }}" not in job_prefix
    assert steps.count("${{ runner.temp }}") == 2
    uses = re.findall(
        r"^\s*uses:\s*[^@\s]+@([0-9a-f]+)\s*$",
        text,
        re.MULTILINE,
    )
    assert uses
    assert all(len(commit) == 40 for commit in uses)


def test_probe_python_compiles() -> None:
    result = subprocess.run(
        (sys.executable, "-m", "py_compile", str(TOOL)),
        cwd=REPO_ROOT,
        check=False,
        capture_output=True,
        text=True,
    )
    assert result.returncode == 0, result.stderr
