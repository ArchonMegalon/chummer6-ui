from __future__ import annotations

import importlib
import importlib.util
import json
import sys
import threading
from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from types import ModuleType
from typing import Any, Iterator
from urllib.parse import urlsplit


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPTS_ROOT = REPO_ROOT / "scripts"
if str(SCRIPTS_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_ROOT))


def load_script(module_name: str, relative_path: str) -> ModuleType:
    script_path = REPO_ROOT / relative_path
    spec = importlib.util.spec_from_file_location(module_name, script_path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[module_name] = module
    spec.loader.exec_module(module)
    return module


CONTRACT = importlib.import_module("blazor_public_edge_workbench_contract")
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


def identity_body(path_query: str) -> tuple[str, bytes]:
    matching = [
        spec
        for spec in CONTRACT.ROUTE_SPECS
        if spec.expected_final_path_query == path_query
    ]
    if not matching:
        return "text/html; charset=utf-8", b"<html><body>generic page</body></html>"
    spec = matching[0]
    if spec.identity_kind == "health_json":
        payload = {field: expected for field, expected in spec.expected_json_fields}
        return "application/json; charset=utf-8", json.dumps(payload).encode("utf-8")

    attributes = " ".join(
        f'{name}="{value}"' for name, value in spec.expected_attributes
    )
    markers = "\n".join(marker for _check_id, marker in spec.expected_text)
    body = (
        '<!DOCTYPE html><html><head><base href="/blazor/" /></head>'
        f'<body><main {attributes}>{markers}</main></body></html>'
    )
    return "text/html; charset=utf-8", body.encode("utf-8")


class RouteHandler(BaseHTTPRequestHandler):
    failure_path: str | None = None
    redirect_mode: str = "valid"

    def do_GET(self) -> None:  # noqa: N802 - stdlib handler API
        parsed = urlsplit(self.path)
        path_query = parsed.path + (f"?{parsed.query}" if parsed.query else "")
        if parsed.path == "/app":
            if self.redirect_mode == "cross_origin":
                location = "https://example.invalid/blazor/app"
            elif self.redirect_mode == "wrong_page":
                location = "/wrong"
            else:
                location = "/blazor/app" + (f"?{parsed.query}" if parsed.query else "")
            self.send_response(302)
            self.send_header("Location", location)
            self.end_headers()
            return
        if parsed.path == "/blazor/":
            self.send_response(302)
            self.send_header("Location", "/blazor/app?command=character_roster")
            self.end_headers()
            return
        if parsed.path == self.failure_path:
            self.send_response(503)
            self.send_header("Content-Type", "text/plain")
            self.end_headers()
            self.wfile.write(b"unavailable")
            return

        content_type, body = identity_body(path_query)
        self.send_response(200)
        self.send_header("Content-Type", content_type)
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, _format: str, *_args: object) -> None:
        return


@contextmanager
def route_server(
    *, failure_path: str | None = None, redirect_mode: str = "valid"
) -> Iterator[str]:
    handler = type(
        "ConfiguredRouteHandler",
        (RouteHandler,),
        {"failure_path": failure_path, "redirect_mode": redirect_mode},
    )
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


def run_public_edge(base_url: str, output: Path) -> int:
    return PUBLIC_EDGE.main(
        [
            "--base-url",
            base_url,
            "--output",
            str(output),
            "--timeout-seconds",
            "2",
            "--max-redirects",
            "3",
            "--allow-test-origin",
        ]
    )


def test_public_edge_materializer_records_explicit_redirects_and_identity(
    tmp_path: Path,
) -> None:
    output = tmp_path / "public-edge.json"
    with route_server() as base_url:
        assert run_public_edge(base_url, output) == 0

    payload = json.loads(output.read_text(encoding="utf-8"))
    assert payload["status"] == "passed"
    assert payload["proof_shape"] == "expanded"
    assert payload["proof_routes"] == [spec.route for spec in CONTRACT.ROUTE_SPECS]
    app_probe = payload["route_probes"][0]
    assert app_probe["http_status"] == 302
    assert app_probe["final_http_status"] == 200
    assert app_probe["final_path_query"] == "/blazor/app"
    assert app_probe["redirect_count"] == 1
    assert app_probe["redirect_chain"][0]["location"] == "/blazor/app"
    assert app_probe["response_identity"]["passed"] is True
    assert VERIFY_PUBLIC_EDGE.main(
        [
            "--receipt-path",
            str(output),
            "--allow-test-origin",
            "--max-age-seconds",
            "60",
        ]
    ) == 0


def test_public_edge_materializer_writes_failed_receipt_on_http_failure(
    tmp_path: Path,
) -> None:
    output = tmp_path / "public-edge-failed.json"
    with route_server(failure_path="/blazor/health") as base_url:
        assert run_public_edge(base_url, output) == 1

    payload = json.loads(output.read_text(encoding="utf-8"))
    failure = next(
        item for item in payload["route_probe_failures"] if item["route"] == "/blazor/health"
    )
    assert payload["status"] == "failed"
    assert failure["final_http_status"] == 503


def test_public_edge_rejects_cross_origin_redirect(tmp_path: Path) -> None:
    output = tmp_path / "cross-origin.json"
    with route_server(redirect_mode="cross_origin") as base_url:
        assert run_public_edge(base_url, output) == 1

    payload = json.loads(output.read_text(encoding="utf-8"))
    probe = payload["route_probes"][0]
    assert probe["redirect_chain"][0]["to_url"] == "https://example.invalid/blazor/app"
    assert probe["ok"] is False
    assert any("origin" in reason or "scheme" in reason for reason in probe["errors"])


def test_redirect_policy_rejects_userinfo_and_https_downgrade() -> None:
    base_url = "https://chummer.run"
    assert "userinfo" in CONTRACT.validate_same_origin_url(
        "https://operator@chummer.run/blazor/app", base_url
    )
    assert "downgrade" in CONTRACT.validate_same_origin_url(
        "http://chummer.run/blazor/app", base_url
    )


def test_public_edge_rejects_same_origin_wrong_page_and_generic_200(
    tmp_path: Path,
) -> None:
    output = tmp_path / "wrong-page.json"
    with route_server(redirect_mode="wrong_page") as base_url:
        assert run_public_edge(base_url, output) == 1

    payload = json.loads(output.read_text(encoding="utf-8"))
    probe = payload["route_probes"][0]
    assert probe["final_path_query"] == "/wrong"
    assert probe["response_identity"]["passed"] is False
    assert any("unexpected final route identity" in reason for reason in probe["errors"])


def test_verifier_rejects_a_claimed_pass_with_a_forged_identity(tmp_path: Path) -> None:
    output = tmp_path / "forged-pass.json"
    with route_server() as base_url:
        assert run_public_edge(base_url, output) == 0

    payload = json.loads(output.read_text(encoding="utf-8"))
    payload["route_probes"][0]["response_identity"]["checks"][0]["actual"] = "text/plain"
    output.write_text(json.dumps(payload), encoding="utf-8")
    assert VERIFY_PUBLIC_EDGE.main(
        ["--receipt-path", str(output), "--allow-test-origin"]
    ) == 1


def valid_component_payload(spec: dict[str, Any]) -> dict[str, Any]:
    statuses = spec["allowed_statuses"]
    status = "passed" if "passed" in statuses else "ready"
    payload: dict[str, Any] = {"status": status}
    if spec.get("contract_name"):
        payload["contract_name"] = spec["contract_name"]
    payload.update(spec.get("required_fields", {}))
    for field, allowed in spec.get("allowed_fields", {}).items():
        payload[field] = sorted(allowed)[0]
    for field, expected_items in spec.get("required_list_items", {}).items():
        payload[field] = list(expected_items)
    for field, config in spec.get("required_object_ids_from_field", {}).items():
        ids = [
            f"{spec['id']}-{index}"
            for index in range(int(config.get("minimum_source_items", 1)))
        ]
        payload[config["source_field"]] = ids
        payload[field] = [{config["id_field"]: item} for item in ids]
    required_check_ids = spec.get("required_check_ids", [])
    if required_check_ids:
        payload["checks"] = [{"id": item} for item in required_check_ids]
    for field, minimum in spec.get("minimum_lengths", {}).items():
        values = payload.setdefault(field, [])
        while len(values) < minimum:
            values.append({"id": f"{field}-{len(values)}"})
    return payload


def test_browser_lane_uses_external_component_receipts_not_stale_defaults(
    tmp_path: Path,
) -> None:
    component_paths: dict[str, Path] = {}
    args = ["--output", str(tmp_path / "aggregate-pass.json")]
    option_by_id = BROWSER_LANE.RECEIPT_INPUT_OPTIONS
    for spec in BROWSER_LANE.REQUIRED_RECEIPTS:
        path = tmp_path / f"{spec['id']}.json"
        path.write_text(json.dumps(valid_component_payload(spec)), encoding="utf-8")
        component_paths[spec["id"]] = path
        args.extend([option_by_id[spec["id"]][0], str(path)])

    assert BROWSER_LANE.main(args) == 0
    passing = json.loads((tmp_path / "aggregate-pass.json").read_text(encoding="utf-8"))
    assert passing["input_paths"]["hosted_route_entry"] == str(
        component_paths["hosted_route_entry"]
    )
    hosted = next(item for item in passing["receipts"] if item["id"] == "hosted_route_entry")
    assert hosted["path"] == str(component_paths["hosted_route_entry"])
    assert hosted["passed"] is True

    stale_override = valid_component_payload(
        next(spec for spec in BROWSER_LANE.REQUIRED_RECEIPTS if spec["id"] == "hosted_route_entry")
    )
    stale_override["status"] = "failed"
    component_paths["hosted_route_entry"].write_text(
        json.dumps(stale_override), encoding="utf-8"
    )
    args[1] = str(tmp_path / "aggregate-fail.json")
    assert BROWSER_LANE.main(args) == 1
    failing = json.loads((tmp_path / "aggregate-fail.json").read_text(encoding="utf-8"))
    hosted = next(item for item in failing["receipts"] if item["id"] == "hosted_route_entry")
    assert hosted["path"] == str(component_paths["hosted_route_entry"])
    assert hosted["status"] == "failed"
    assert hosted["passed"] is False
