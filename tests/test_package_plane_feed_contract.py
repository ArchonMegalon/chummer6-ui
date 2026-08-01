from __future__ import annotations

import json
import os
import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
HELPER = REPO_ROOT / "scripts" / "ai" / "with-package-plane.sh"


def test_semicolon_separated_published_feeds_are_one_msbuild_property(tmp_path: Path) -> None:
    bin_dir = tmp_path / "bin"
    bin_dir.mkdir()
    captured = tmp_path / "dotnet-args.json"
    dotnet = bin_dir / "dotnet"
    dotnet.write_text(
        "#!/usr/bin/env python3\n"
        "import json, os, sys\n"
        "from pathlib import Path\n"
        "Path(os.environ['CAPTURE_DOTNET_ARGS']).write_text(json.dumps(sys.argv[1:]))\n",
        encoding="utf-8",
    )
    dotnet.chmod(0o755)

    env = dict(os.environ)
    env.update(
        {
            "CAPTURE_DOTNET_ARGS": str(captured),
            "CHUMMER_PACKAGE_PLANE_SERIALIZE": "0",
            "CHUMMER_PUBLISHED_FEED_SOURCES": (
                "/srv/chummer-feed;https://api.nuget.org/v3/index.json"
            ),
            "PATH": f"{bin_dir}:{env['PATH']}",
        }
    )

    completed = subprocess.run(
        ["bash", str(HELPER), "restore", "Chummer.sln"],
        cwd=REPO_ROOT,
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )

    assert completed.returncode == 0, completed.stderr
    arguments = json.loads(captured.read_text(encoding="utf-8"))
    source_arguments = [
        value
        for value in arguments
        if value.startswith("-p:RestoreAdditionalProjectSources=")
    ]
    assert source_arguments == [
        "-p:RestoreAdditionalProjectSources="
        "/srv/chummer-feed%3Bhttps://api.nuget.org/v3/index.json"
    ]
    assert "https://api.nuget.org/v3/index.json" not in arguments


def test_linux_test_wrapper_never_selects_stale_windows_runner(tmp_path: Path) -> None:
    project_dir = tmp_path / "Sample.Tests"
    project_dir.mkdir()
    project = project_dir / "Sample.Tests.csproj"
    project.write_text(
        "<Project Sdk=\"Microsoft.NET.Sdk\">"
        "<PropertyGroup><EnableMSTestRunner>true</EnableMSTestRunner>"
        "</PropertyGroup></Project>",
        encoding="utf-8",
    )

    selected = tmp_path / "selected-runner"
    for framework, exit_code in (("net10.0-windows", 77), ("net10.0", 0)):
        output_dir = project_dir / "bin" / "Debug" / framework
        output_dir.mkdir(parents=True)
        runner = output_dir / "Sample.Tests"
        runner.write_text(
            "#!/usr/bin/env bash\n"
            f"printf '%s' '{framework}' > \"$SELECTED_RUNNER\"\n"
            f"exit {exit_code}\n",
            encoding="utf-8",
        )
        runner.chmod(0o755)

    bin_dir = tmp_path / "bin"
    bin_dir.mkdir()
    dotnet = bin_dir / "dotnet"
    dotnet.write_text("#!/usr/bin/env bash\nexit 0\n", encoding="utf-8")
    dotnet.chmod(0o755)

    env = dict(os.environ)
    env.update(
        {
            "CHUMMER_PACKAGE_PLANE_SERIALIZE": "0",
            "CHUMMER_PUBLISHED_FEED_SOURCES": "/srv/chummer-feed",
            "PATH": f"{bin_dir}:{env['PATH']}",
            "SELECTED_RUNNER": str(selected),
        }
    )
    env.pop("OS", None)

    completed = subprocess.run(
        ["bash", str(REPO_ROOT / "scripts" / "ai" / "test.sh"), str(project)],
        cwd=REPO_ROOT,
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )

    assert completed.returncode == 0, completed.stderr
    assert selected.read_text(encoding="utf-8") == "net10.0"
