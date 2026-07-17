#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
edition="${1:-}"

case "$edition" in
  sr4)
    ledger_path="$repo_root/docs/SR4_WORKFLOW_PARITY_LEDGER.json"
    oracle_path="$repo_root/docs/CHUMMER4_SR4_PARITY_ORACLE.json"
    contract_name="chummer6-ui.sr4_workflow_family_execution_receipt"
    proof_kind="sr4_family_oracle"
    ;;
  sr6)
    ledger_path="$repo_root/docs/SR6_WORKFLOW_PARITY_LEDGER.json"
    oracle_path="$repo_root/docs/SR6_DESKTOP_WORKFLOW_PARITY_ORACLE.json"
    contract_name="chummer6-ui.sr6_workflow_family_execution_receipt"
    proof_kind="sr6_family_release_gated_execution"
    ;;
  *)
    echo "usage: $0 <sr4|sr6>" >&2
    exit 64
    ;;
esac

python3 - <<'PY' "$edition" "$ledger_path" "$oracle_path" "$repo_root" "$contract_name" "$proof_kind"
from __future__ import annotations

import json
import fcntl
import os
import subprocess
import sys
import time
from urllib.parse import urlparse
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
import xml.etree.ElementTree as ET

edition = sys.argv[1].strip().lower()
ledger_path = Path(sys.argv[2])
oracle_path = Path(sys.argv[3])
repo_root = Path(sys.argv[4])
contract_name = sys.argv[5].strip()
proof_kind = sys.argv[6].strip().lower()

if not ledger_path.is_file():
    raise SystemExit(f"missing ledger: {ledger_path}")
if not oracle_path.is_file():
    raise SystemExit(f"missing oracle: {oracle_path}")

ledger = json.loads(ledger_path.read_text(encoding="utf-8"))
oracle = json.loads(oracle_path.read_text(encoding="utf-8"))
families = [item for item in (ledger.get("requiredFamilies") or []) if isinstance(item, dict)]
family_filter_ids = {
    str(value).strip()
    for value in (os.environ.get("CHUMMER_WORKFLOW_FAMILY_FILTER_IDS") or "").split(",")
    if str(value).strip()
}
if family_filter_ids:
    families = [
        item
        for item in families
        if str(item.get("id") or "").strip() in family_filter_ids
    ]

run_root = repo_root / ".codex-studio" / "out" / "workflow-family-parity" / "executed" / edition
run_root.mkdir(parents=True, exist_ok=True)
trx_path = run_root / f"{edition}-workflow-family-execution.trx"
if trx_path.exists():
    trx_path.unlink()
legacy_execution_root = repo_root / ".codex-studio" / "published" / "workflow-family-parity" / "execution" / edition
if legacy_execution_root.is_dir():
    for stale_file in legacy_execution_root.glob("*.generated.json"):
        stale_file.unlink()
lock_dir = repo_root / ".codex-studio" / "locks"
lock_dir.mkdir(parents=True, exist_ok=True)
lock_path = lock_dir / f"workflow-family-dotnet-test-{edition}.lock"
max_test_attempts = max(
    1,
    int(
        os.environ.get("CHUMMER_WORKFLOW_FAMILY_EXECUTION_MAX_TEST_ATTEMPTS")
        or "2"
    ),
)
skip_restore = str(
    os.environ.get("CHUMMER_WORKFLOW_FAMILY_EXECUTION_SKIP_RESTORE") or "0"
).strip().lower() in {"1", "true", "yes"}

unique_tests: list[str] = []
for family in families:
    for test_name in family.get("auditTests") or []:
        value = str(test_name).strip()
        if value and value not in unique_tests:
            unique_tests.append(value)

run_error = ""
run_exit = 0
external_blocker = ""
api_probe: dict[str, object] = {}
dotnet_attempt_count = 0
build_attempt_count = 0
api_server_proc: subprocess.Popen[str] | None = None
api_server_log_path = run_root / f"{edition}-local-api.log"
per_test_trx_paths: dict[str, str] = {}
api_project_override = str(os.environ.get("CHUMMER_API_AUTOSTART_PROJECT") or "").strip()
default_api_project = repo_root / "Chummer.Api" / "Chummer.Api.csproj"
api_project_path = Path(api_project_override) if api_project_override else default_api_project
default_api_build_output = (
    api_project_path.parent / "bin" / "Debug" / "net10.0" / f"{api_project_path.stem}.dll"
)
test_project_path = repo_root / "Chummer.Tests" / "Chummer.Tests.csproj"
test_build_output_dir = repo_root / "Chummer.Tests" / "bin" / "Release" / "net10.0"
test_runner_apphost = test_build_output_dir / "Chummer.Tests"
test_runner_dll = test_build_output_dir / "Chummer.Tests.dll"
test_build_projects = [
    ("Chummer.Avalonia", repo_root / "Chummer.Avalonia" / "Chummer.Avalonia.csproj"),
    ("Chummer.Portal", repo_root / "Chummer.Portal" / "Chummer.Portal.csproj"),
    ("Chummer.Tests", test_project_path),
]


def probe_api_surface(base_url: str, path: str) -> tuple[bool, int, str]:
    target = f"{base_url.rstrip('/')}{path}"
    request = urllib.request.Request(target, method="GET")
    try:
        with urllib.request.urlopen(request, timeout=2) as response:
            return True, int(response.getcode()), ""
    except urllib.error.HTTPError as ex:
        code = int(getattr(ex, "code", 0) or 0)
        # Auth-gated or method mismatch still proves the route exists.
        if code in {401, 403, 405}:
            return True, code, ""
        return False, code, str(ex)
    except Exception as ex:  # noqa: BLE001
        return False, 0, str(ex)


def collect_api_probe(base_url: str) -> tuple[dict[str, object], bool]:
    api_probe_results = []
    for probe_path in api_probe_paths:
        ok, status_code, error = probe_api_surface(base_url, probe_path)
        api_probe_results.append(
            {
                "path": probe_path,
                "ok": bool(ok),
                "statusCode": status_code,
                "error": error,
            }
        )
    return (
        {
            "baseUrl": base_url,
            "results": api_probe_results,
        },
        all(bool(item.get("ok")) for item in api_probe_results),
    )


def warm_api_surface(base_url: str, attempts: int = 5, delay_seconds: float = 0.5) -> tuple[dict[str, object], bool]:
    last_probe: dict[str, object] = {}
    last_ready = False
    for _ in range(max(1, attempts)):
        last_probe, last_ready = collect_api_probe(base_url)
        if last_ready:
            time.sleep(max(0.0, delay_seconds))
            confirm_probe, confirm_ready = collect_api_probe(base_url)
            if confirm_ready:
                confirm_probe["warmed"] = True
                return confirm_probe, True
            last_probe = confirm_probe
            last_ready = confirm_ready
        time.sleep(max(0.0, delay_seconds))
    if last_probe:
        last_probe["warmed"] = False
    return last_probe, last_ready


def can_autostart_local_api(base_url: str) -> bool:
    parsed = urlparse(base_url)
    return parsed.scheme in {"http", "https"} and parsed.hostname in {"127.0.0.1", "localhost"}


def terminate_local_api() -> None:
    global api_server_proc
    if api_server_proc is None:
        return
    if api_server_proc.poll() is None:
        api_server_proc.terminate()
        try:
            api_server_proc.wait(timeout=10)
        except subprocess.TimeoutExpired:
            api_server_proc.kill()
            api_server_proc.wait(timeout=10)
    api_server_proc = None


def ensure_local_api(base_url: str) -> tuple[dict[str, object], bool]:
    global api_server_proc
    initial_probe, initial_ready = collect_api_probe(base_url)
    if initial_ready:
        initial_probe["autostarted"] = False
        return initial_probe, True

    autostart_enabled = str(os.environ.get("CHUMMER_API_AUTOSTART") or "1").strip().lower() not in {"0", "false", "no"}
    if not autostart_enabled or not can_autostart_local_api(base_url):
        initial_probe["autostarted"] = False
        return initial_probe, False
    if not api_project_path.is_file():
        initial_probe["autostarted"] = False
        initial_probe["autostartProjectPath"] = str(api_project_path)
        initial_probe["autostartFailure"] = "autostart_project_missing"
        return initial_probe, False

    api_server_log_path.parent.mkdir(parents=True, exist_ok=True)
    api_log_handle = api_server_log_path.open("w", encoding="utf-8")
    env = dict(os.environ)
    env.setdefault("ASPNETCORE_URLS", base_url)
    build_output_override = str(os.environ.get("CHUMMER_API_AUTOSTART_BUILD_OUTPUT") or "").strip()
    build_output_path = Path(build_output_override) if build_output_override else default_api_build_output
    run_command = [
        "dotnet",
        "run",
        "--project",
        str(api_project_path),
        "--no-restore",
    ]
    if build_output_path.is_file():
        run_command.append("--no-build")
    run_command.extend(["--urls", base_url])
    api_server_proc = subprocess.Popen(
        run_command,
        cwd=repo_root,
        stdout=api_log_handle,
        stderr=subprocess.STDOUT,
        text=True,
        env=env,
    )

    deadline = time.monotonic() + max(
        5,
        int(str(os.environ.get("CHUMMER_API_AUTOSTART_TIMEOUT_SECONDS") or "90").strip() or "90"),
    )
    while time.monotonic() < deadline:
        current_probe, current_ready = collect_api_probe(base_url)
        if current_ready:
            current_probe["autostarted"] = True
            current_probe["autostartLogPath"] = str(api_server_log_path)
            current_probe["autostartPid"] = api_server_proc.pid if api_server_proc else None
            return current_probe, True
        if api_server_proc.poll() is not None:
            break
        time.sleep(1)

    current_probe, current_ready = collect_api_probe(base_url)
    current_probe["autostarted"] = True
    current_probe["autostartLogPath"] = str(api_server_log_path)
    current_probe["autostartPid"] = api_server_proc.pid if api_server_proc else None
    current_probe["autostartBuildOutputPath"] = str(build_output_path)
    current_probe["autostartUsedNoBuild"] = build_output_path.is_file()
    if api_server_proc is not None and api_server_proc.poll() is not None:
        current_probe["autostartExitCode"] = api_server_proc.returncode
    return current_probe, current_ready


def test_result_indicates_missing_api(trx_path: Path, output_text: str) -> bool:
    missing_api_tokens = (
        "Assert.Inconclusive failed. Chummer API runtime is not reachable",
        "Chummer API runtime socket error",
        "Chummer API runtime probe timed out",
    )
    if any(token in output_text for token in missing_api_tokens):
        return True
    if not trx_path.is_file():
        return False
    try:
        trx_text = trx_path.read_text(encoding="utf-8-sig")
    except OSError:
        return False
    return any(token in trx_text for token in missing_api_tokens)


api_probe_paths = ["/api/workspaces?maxCount=1", "/api/shell/bootstrap"]


def discover_docker_presentation_api_base_url() -> str | None:
    try:
        ps_result = subprocess.run(
            [
                "docker",
                "ps",
                "--filter",
                "name=chummer-presentation-api",
                "--format",
                "{{.ID}}",
            ],
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            check=False,
        )
    except OSError:
        return None

    for container_id in [line.strip() for line in (ps_result.stdout or "").splitlines() if line.strip()]:
        inspect_result = subprocess.run(
            [
                "docker",
                "inspect",
                "-f",
                "{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}",
                container_id,
            ],
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            check=False,
        )
        ip_addresses = [token.strip() for token in (inspect_result.stdout or "").split() if token.strip()]
        for ip_address in ip_addresses:
            candidate = f"http://{ip_address}:8080"
            probe, ready = collect_api_probe(candidate)
            if ready:
                return candidate

    return None


configured_api_base_url = str(
    os.environ.get("CHUMMER_API_BASE_URL")
    or os.environ.get("CHUMMER_WEB_BASE_URL")
    or ""
).strip()
api_base_url = configured_api_base_url or "http://127.0.0.1:8088"
if not configured_api_base_url:
    loopback_probe, loopback_ready = collect_api_probe(api_base_url)
    if not loopback_ready and (docker_api_base_url := discover_docker_presentation_api_base_url()):
        api_base_url = docker_api_base_url

api_probe, api_surface_ready = ensure_local_api(api_base_url)
if api_surface_ready:
    api_probe, api_surface_ready = warm_api_surface(api_base_url)
test_process_env = dict(os.environ)
test_process_env["CHUMMER_API_BASE_URL"] = api_base_url
test_process_env["CHUMMER_WEB_BASE_URL"] = api_base_url
test_process_env["CHUMMER_REPO_ROOT"] = str(repo_root)

if unique_tests:
    with lock_path.open("w", encoding="utf-8") as lock_handle:
        fcntl.flock(lock_handle.fileno(), fcntl.LOCK_EX)
        for project_label, project_path in test_build_projects:
            build_attempt_count += 1
            build_proc = subprocess.run(
                [
                    "dotnet",
                    "build",
                    str(project_path),
                    "--configuration",
                    "Release",
                    "--nologo",
                    "-p:UseSharedCompilation=false",
                    "-p:BuildInParallel=false",
                    "-maxcpucount:1",
                ] + (["--no-restore"] if skip_restore else []),
                cwd=repo_root,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                check=False,
                env=test_process_env,
            )
            if build_proc.returncode != 0:
                output_lines = (build_proc.stdout or "").strip().splitlines()
                if output_lines:
                    run_error = output_lines[-1]
                if not run_error:
                    run_error = f"{project_label} release build failed"
                run_exit = int(build_proc.returncode)
                break

        test_runner_command: list[str] = []
        if run_exit == 0:
            if test_runner_apphost.is_file():
                test_runner_command = [str(test_runner_apphost)]
            elif test_runner_dll.is_file():
                test_runner_command = ["dotnet", str(test_runner_dll)]
            else:
                run_exit = 1
                run_error = (
                    f"built test runner was not found at {test_runner_apphost} or {test_runner_dll}"
                )

        observed_attempt_count = 0
        build_failed = run_exit != 0
        for index, test_name in enumerate(unique_tests, start=1):
            if build_failed:
                break
            safe_name = "".join(char if char.isalnum() or char in {"-", "_"} else "_" for char in test_name)
            per_test_trx = run_root / f"{index:02d}-{safe_name}.trx"
            per_test_trx_paths[test_name] = str(per_test_trx)
            proc = None
            for attempt in range(1, max_test_attempts + 1):
                observed_attempt_count = max(observed_attempt_count, attempt)
                if per_test_trx.exists():
                    per_test_trx.unlink()
                proc = subprocess.run(
                    test_runner_command + [
                        "--filter",
                        f"FullyQualifiedName~{test_name}",
                        "--results-directory",
                        str(run_root),
                        "--report-trx",
                        "--report-trx-filename",
                        per_test_trx.name,
                        "--output",
                        "Normal",
                    ],
                    cwd=repo_root,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.STDOUT,
                    text=True,
                    check=False,
                    env=test_process_env,
                )
                if proc.returncode == 0:
                    break
                if test_result_indicates_missing_api(per_test_trx, proc.stdout or ""):
                    api_probe, api_surface_ready = ensure_local_api(api_base_url)
                    if api_surface_ready:
                        api_probe, api_surface_ready = warm_api_surface(api_base_url)

            if proc is None:
                raise SystemExit(f"workflow-family dotnet test process did not start for {test_name}")

            output_lines = (proc.stdout or "").strip().splitlines()
            if output_lines:
                run_error = output_lines[-1]

            if proc.returncode != 0 and run_exit == 0:
                run_exit = int(proc.returncode)
        dotnet_attempt_count = observed_attempt_count
        fcntl.flock(lock_handle.fileno(), fcntl.LOCK_UN)
    if run_exit != 0 and not run_error:
        run_error = "dotnet test failed"

ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
results_by_name: dict[str, list[str]] = {}
for test_name, trx_candidate in per_test_trx_paths.items():
    trx_file = Path(trx_candidate)
    if not trx_file.is_file():
        continue
    root = ET.fromstring(trx_file.read_text(encoding="utf-8"))
    for node in root.findall(".//t:UnitTestResult", ns):
        observed_test_name = (node.attrib.get("testName") or "").strip()
        outcome = (node.attrib.get("outcome") or "").strip()
        if observed_test_name:
            results_by_name.setdefault(observed_test_name, []).append(outcome)

if not api_surface_ready and not results_by_name:
    external_blocker = "missing_api_surface_contract"

sr4_oracle_families = {str(value).strip() for value in (oracle.get("workflowFamilies") or []) if str(value).strip()}
sr6_oracle_map = {
    str(item.get("id") or "").strip(): item
    for item in (oracle.get("requiredFamilies") or [])
    if isinstance(item, dict) and str(item.get("id") or "").strip()
}

execution_signal_tokens = (
    "save",
    "workflow",
    "execute",
    "dialog",
    "download",
    "export",
    "print",
    "roundtrip",
    "click",
)
execution_optional_family_ids = {
    "improvements-explain-result-parity",
}

any_fail = False
for family in families:
    family_id = str(family.get("id") or "").strip()
    if not family_id:
        continue

    audit_tests = [str(value).strip() for value in (family.get("auditTests") or []) if str(value).strip()]
    output_refs = [str(value).strip() for value in (family.get("executionReceipts") or []) if str(value).strip()]
    if not output_refs:
        output_refs = [
            f".codex-studio/published/workflow-family-parity/executed/{edition}/{family_id}.generated.json"
        ]

    reasons = []
    if str(family.get("status") or "").strip().lower() != "ready":
        reasons.append(f"Ledger family is not ready: {family.get('status') or 'missing'}")
    if not audit_tests:
        reasons.append("Missing auditTests for family.")
    elif family_id not in execution_optional_family_ids and not any(
        any(token in test_name.lower() for token in execution_signal_tokens)
        for test_name in audit_tests
    ):
        reasons.append("Audit tests do not include any execution-oriented workflow proof.")

    oracle_detail: dict[str, object] = {}
    if edition == "sr4":
        if family_id not in sr4_oracle_families:
            reasons.append(f"Family is missing from SR4 oracle workflowFamilies: {family_id}")
        source_repo = dict(oracle.get("sourceRepo") or {})
        oracle_detail = {
            "sourceRepoPath": str(source_repo.get("path") or ""),
            "sourceRepoHead": str(source_repo.get("head") or ""),
        }
    else:
        oracle_entry = sr6_oracle_map.get(family_id)
        if not oracle_entry:
            reasons.append(f"Family is missing from SR6 carry-forward oracle requiredFamilies: {family_id}")
        else:
            oracle_detail = {
                "classification": str(oracle_entry.get("classification") or ""),
                "rationale": str(oracle_entry.get("rationale") or ""),
                "releaseGateTests": [str(value).strip() for value in (oracle_entry.get("releaseGateTests") or []) if str(value).strip()],
            }

    missing_tests: list[str] = []
    failed_tests: dict[str, list[str]] = {}
    passed_tests: list[str] = []
    for test_name in audit_tests:
        outcomes: list[str] = []
        for observed_name, observed_outcomes in results_by_name.items():
            if test_name in observed_name:
                outcomes.extend(observed_outcomes)
        if not outcomes:
            missing_tests.append(test_name)
            continue
        lowered = [value.lower() for value in outcomes]
        if any(value not in {"passed", "completed", "passedbutrunaborted"} for value in lowered):
            failed_tests[test_name] = outcomes
        else:
            passed_tests.append(test_name)

    if missing_tests:
        reasons.append("Audit tests not present in executed TRX results: " + ", ".join(missing_tests))
    if failed_tests:
        reasons.append(
            "Audit tests did not pass in executed TRX results: "
            + ", ".join(f"{name}={','.join(values)}" for name, values in sorted(failed_tests.items()))
        )
    if run_exit != 0 and (missing_tests or failed_tests):
        reasons.append(f"dotnet test execution failed (exit {run_exit}): {run_error or 'see TRX/log output'}")
    if external_blocker:
        reasons.append(
            "Dual-head workflow execution requires a chummer-api host exposing /api/workspaces and /api/shell/bootstrap "
            "(external blocker: missing_api_surface_contract)."
        )

    payload = {
        "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "contract_name": contract_name,
        "status": "pass" if not reasons else "fail",
        "summary": (
            f"{edition.upper()} workflow-family execution evidence is explicitly grounded for {family_id}."
            if not reasons
            else f"{edition.upper()} workflow-family execution evidence is incomplete for {family_id}."
        ),
        "reasons": reasons,
        "evidence": {
            "edition": edition,
            "familyId": family_id,
            "proofKind": proof_kind,
            "ledgerPath": str(ledger_path),
            "oraclePath": str(oracle_path),
            "auditTests": audit_tests,
            "oracle": oracle_detail,
            "dotnetTest": {
                "project": "Chummer.Tests/Chummer.Tests.csproj",
                "configuration": "Release",
                "buildAttemptCount": build_attempt_count,
                "runnerCommand": test_runner_command,
                "trxPath": str(trx_path),
                "perTestTrxPaths": per_test_trx_paths,
                "exitCode": run_exit,
                "attemptCount": dotnet_attempt_count,
                "maxAttempts": max_test_attempts,
            },
            "apiProbe": api_probe,
            "external_blocker": external_blocker,
            "matchedPassedTests": passed_tests,
            "missingAuditTests": missing_tests,
            "failedAuditTests": failed_tests,
        },
    }

    for output_ref in output_refs:
        output_ref = output_ref.replace("{familyId}", family_id)
        output_ref = output_ref.replace(
            "workflow-family-parity/execution/",
            "workflow-family-parity/executed/",
        )
        output_path = Path(output_ref)
        if not output_path.is_absolute():
            output_path = repo_root / output_path
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if reasons:
        any_fail = True

if any_fail:
    terminate_local_api()
    raise SystemExit(43)

terminate_local_api()
PY

echo "[materialize-${edition}-workflow-family-execution-receipts] PASS"
