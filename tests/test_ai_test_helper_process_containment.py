from __future__ import annotations

from pathlib import Path


TEST_HELPER = Path("/docker/chummercomplete/chummer-presentation/scripts/ai/test.sh")


def test_mstest_runner_build_disables_build_servers_and_parallel_workers() -> None:
    text = TEST_HELPER.read_text(encoding="utf-8")

    assert 'build_args=(build "$project_path" --disable-build-servers -m:1)' in text
