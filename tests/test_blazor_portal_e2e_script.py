import json
import os
import subprocess
from pathlib import Path


def test_portal_e2e_script_auto_selects_a_free_local_port_when_default_is_busy() -> None:
    script = Path("scripts/e2e-portal.sh").read_text(encoding="utf-8")

    assert 'DEFAULT_PORTAL_PORT="${CHUMMER_PORTAL_PORT:-8091}"' in script
    assert "PORTAL_BASE_URL_EXPLICIT=0" in script
    assert "PORTAL_PORT_EXPLICIT=0" in script
    assert "select_available_local_port()" in script
    assert "selected_port=\"$(select_available_local_port \"$DEFAULT_PORTAL_PORT\")\"" in script
    assert 'export CHUMMER_PORTAL_PORT="$selected_port"' in script
    assert "auto-selected free self-host portal port" in script


def test_portal_e2e_script_fail_closes_when_an_explicit_requested_port_is_busy() -> None:
    script = Path("scripts/e2e-portal.sh").read_text(encoding="utf-8")

    assert 'elif [[ "$skip_rebuild" -eq 0 && "$PORTAL_PORT_EXPLICIT" -eq 1 ]] && ! is_local_tcp_port_available "${CHUMMER_PORTAL_PORT}"; then' in script
    assert "requested self-host portal port" in script
    assert "leave it unset for automatic selection" in script


def test_portal_e2e_script_defaults_to_smoke_playwright_scope_and_records_it_in_receipts() -> None:
    script = Path("scripts/e2e-portal.sh").read_text(encoding="utf-8")

    assert 'PORTAL_PLAYWRIGHT_SCOPE="${CHUMMER_PORTAL_PLAYWRIGHT_SCOPE:-smoke}"' in script
    assert 'DEFAULT_PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS="420"' in script
    assert 'if [[ "$PORTAL_PLAYWRIGHT_SCOPE" == "full" ]]; then' in script
    assert 'DEFAULT_PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS="900"' in script
    assert 'PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS="${CHUMMER_PORTAL_E2E_TIMEOUT_SECONDS:-$DEFAULT_PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS}"' in script
    assert 'CHUMMER_PORTAL_PLAYWRIGHT_SCOPE="$PORTAL_PLAYWRIGHT_SCOPE"' in script
    assert '"playwright_scope": playwright_scope' in script
    assert 'if playwright_scope not in {"smoke", "full"}:' in script


def test_portal_e2e_docs_only_mode_skips_runtime_and_emits_nonrelease_receipts(tmp_path: Path) -> None:
    local_proof = tmp_path / "UI_LOCAL_RELEASE_PROOF.generated.json"
    self_host_proof = tmp_path / "BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json"
    env = os.environ.copy()
    env.update(
        {
            "CHUMMER_PORTAL_LOCAL_PROOF_PATH": str(local_proof),
            "CHUMMER_PORTAL_SELF_HOST_WORKBENCH_PROOF_PATH": str(self_host_proof),
            "CHUMMER_PORTAL_E2E_SKIP_EDGE_REBUILD": "1",
            "CHUMMER_PORTAL_PLAYWRIGHT": "0",
            "CHUMMER_PORTAL_E2E_REQUIRE_RUNTIME": "0",
        }
    )

    result = subprocess.run(
        ["bash", "scripts/e2e-portal.sh"],
        check=False,
        capture_output=True,
        text=True,
        env=env,
    )

    assert result.returncode == 0, result.stderr
    assert "portal route probe skipped; emitting failed non-release local proof" in result.stdout
    for path in (local_proof, self_host_proof):
        payload = json.loads(path.read_text(encoding="utf-8"))
        assert payload["status"] == "failed"
        assert payload["runtime_required"] is False
        assert payload["route_probe_executed"] is False
