from __future__ import annotations

import importlib.util
import json
import sys
import threading
from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from types import ModuleType
from typing import Iterator
from urllib.parse import urlsplit


REPO_ROOT = Path(__file__).resolve().parents[1]


def load_script(module_name: str, relative_path: str) -> ModuleType:
    script_path = REPO_ROOT / relative_path
    spec = importlib.util.spec_from_file_location(module_name, script_path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[module_name] = module
    spec.loader.exec_module(module)
    return module


PUBLIC_EDGE = load_script(
    "materialize_blazor_public_edge_workbench_proof",
    "scripts/materialize-blazor-public-edge-workbench-proof.py",
)
VERIFY_PUBLIC_EDGE = load_script(
    "verify_blazor_public_edge_workbench_proof",
    "scripts/verify_blazor_public_edge_workbench_proof.py",
)
BROWSER_LANE = load_script(
    "materialize_blazor_browser_lane_proof_set",
    "scripts/materialize-blazor-browser-lane-proof-set.py",
)


class RouteHandler(BaseHTTPRequestHandler):
    failure_path: str | None = None

    def do_GET(self) -> None:  # noqa: N802 - stdlib handler API
        if urlsplit(self.path).path == self.failure_path:
            self.send_response(503)
            self.end_headers()
            self.wfile.write(b"unavailable")
            return
        self.send_response(200)
        self.send_header("Content-Type", "text/plain")
        self.end_headers()
        self.wfile.write(b"ok")

    def log_message(self, _format: str, *_args: object) -> None:
        return


@contextmanager
def route_server(failure_path: str | None = None) -> Iterator[str]:
    handler = type("ConfiguredRouteHandler", (RouteHandler,), {"failure_path": failure_path})
    server = ThreadingHTTPServer(("127.0.0.1", 0), handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        host, port = server.server_address
        yield f"http://{host}:{port}"
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=5)


def test_public_edge_materializer_probes_every_route_and_verifies_explicit_path(
    tmp_path: Path,
) -> None:
    output = tmp_path / "public-edge.json"
    with route_server() as base_url:
        result = PUBLIC_EDGE.main(
            [
                "--base-url",
                base_url,
                "--output",
                str(output),
                "--timeout-seconds",
                "2",
            ]
        )

    assert result == 0
    payload = json.loads(output.read_text(encoding="utf-8"))
    assert payload["contract_name"] == "chummer6-ui.blazor_public_edge_workbench_proof"
    assert payload["status"] == "passed"
    assert payload["proof_shape"] == "expanded"
    assert payload["route_probe_count"] == len(PUBLIC_EDGE.PROOF_ROUTES)
    assert [probe["route"] for probe in payload["route_probes"]] == PUBLIC_EDGE.PROOF_ROUTES
    assert payload["route_probe_failures"] == []
    assert all(probe["checked"] is True for probe in payload["route_probes"])
    assert all(probe["http_status"] == 200 for probe in payload["route_probes"])
    assert all(probe["ok"] is True for probe in payload["route_probes"])
    assert all(probe["error"] == "" for probe in payload["route_probes"])
    assert VERIFY_PUBLIC_EDGE.main(["--receipt-path", str(output)]) == 0


def test_public_edge_materializer_writes_failed_receipt_on_http_failure(
    tmp_path: Path,
) -> None:
    output = tmp_path / "public-edge-failed.json"
    with route_server(failure_path="/blazor/health") as base_url:
        result = PUBLIC_EDGE.main(
            ["--base-url", base_url, "--output", str(output), "--timeout-seconds", "2"]
        )

    assert result == 1
    payload = json.loads(output.read_text(encoding="utf-8"))
    assert payload["status"] == "failed"
    assert payload["route_probe_count"] == len(PUBLIC_EDGE.PROOF_ROUTES)
    assert payload["route_probe_failures"] == [
        {
            "error": "HTTP 503: Service Unavailable",
            "http_status": 503,
            "route": "/blazor/health",
        }
    ]
    failed_probe = next(
        probe for probe in payload["route_probes"] if probe["route"] == "/blazor/health"
    )
    assert failed_probe["http_status"] == 503
    assert failed_probe["ok"] is False


def test_verifier_rejects_a_claimed_pass_with_a_failed_route_probe(tmp_path: Path) -> None:
    output = tmp_path / "forged-pass.json"
    with route_server() as base_url:
        assert PUBLIC_EDGE.main(["--base-url", base_url, "--output", str(output)]) == 0

    payload = json.loads(output.read_text(encoding="utf-8"))
    payload["route_probes"][0]["http_status"] = 500
    payload["route_probes"][0]["ok"] = False
    payload["route_probes"][0]["error"] = "forced failure"
    output.write_text(json.dumps(payload), encoding="utf-8")

    assert VERIFY_PUBLIC_EDGE.main(["--receipt-path", str(output)]) == 1


def test_browser_lane_materializer_honors_output_environment_override(
    tmp_path: Path, monkeypatch
) -> None:
    output = tmp_path / "browser-lane.json"
    example = tmp_path / "example.json"
    example.write_text("{}", encoding="utf-8")
    monkeypatch.setattr(BROWSER_LANE, "REQUIRED_RECEIPTS", [])
    monkeypatch.setattr(BROWSER_LANE, "EXAMPLE_RECEIPT_PATH", example)
    monkeypatch.setattr(BROWSER_LANE, "EXAMPLE_RECEIPT_TOKENS", [])
    monkeypatch.setenv("CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH", str(output))

    assert BROWSER_LANE.main([]) == 0
    payload = json.loads(output.read_text(encoding="utf-8"))
    assert payload["contract_name"] == "chummer6-ui.blazor_browser_lane_proof_set"
    assert payload["status"] == "passed"
