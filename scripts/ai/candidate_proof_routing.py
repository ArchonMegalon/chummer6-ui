#!/usr/bin/env python3
"""Shared fail-closed routing for candidate-bound proof producers.

The shell producers keep their historical in-repository defaults.  When their
external plane is requested, this module makes the plane all-or-nothing,
validates every canonical receipt before expensive work starts, prevents output
aliases from clobbering inputs, and provides atomic external writes.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import tempfile
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence


PASS_STATUSES = {"pass", "passed", "ready"}
OUTPUT_STATUSES = PASS_STATUSES | {"fail", "failed", "blocked"}
PUBLISHED_PREFIX = Path(".codex-studio/published")
RELEASE_CHANNEL_CONTRACT = "Chummer.Hub.Registry.Contracts"
RELEASE_CHANNEL_STATUS = "published"


class RoutingError(RuntimeError):
    """Raised when an external proof plane is incomplete or unsafe."""


@dataclass(frozen=True)
class ReceiptSpec:
    relative_path: str
    contract_name: str | None
    optional: bool = False


B14_INPUTS: tuple[ReceiptSpec, ...] = (
    ReceiptSpec("CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.chummer5a_desktop_workflow_parity"),
    ReceiptSpec("SR4_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.sr4_desktop_workflow_parity"),
    ReceiptSpec("SR6_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.sr6_desktop_workflow_parity"),
    ReceiptSpec(
        "CHUMMER_SR6_RULESET_UI_SOPHISTICATION_GATE.generated.json",
        "chummer6-ui.chummer_sr6_ruleset_ui_sophistication_gate",
    ),
    ReceiptSpec("SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json", "chummer6-ui.sr4_sr6_desktop_parity_frontier"),
    ReceiptSpec("RULESET_UI_ADAPTATION.generated.json", "chummer6-ui.ruleset_ui_adaptation_frontier"),
    ReceiptSpec("CHUMMER5A_LAYOUT_HARD_GATE.generated.json", "chummer6-ui.chummer5a_layout_hard_gate"),
    ReceiptSpec("DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json", "chummer6-ui.desktop_workflow_execution_gate"),
    ReceiptSpec("UI_LOCALIZATION_RELEASE_GATE.generated.json", "chummer6-ui.localization_release_gate"),
    ReceiptSpec("INTERACTIVE_CONTROL_INVENTORY.generated.json", "chummer6-ui.interactive_control_inventory"),
    ReceiptSpec("RECURSIVE_UI_EVENT_EXIT_GATE.generated.json", "chummer6-ui.recursive_ui_event_exit_gate"),
    ReceiptSpec("STARTUP_WORKBENCH_SURVIVAL.generated.json", "chummer6-ui.startup_workbench_survival"),
    ReceiptSpec("DESIGN_MIRROR_COMPLETENESS.generated.json", "chummer6-ui.design_mirror_completeness"),
    ReceiptSpec("DESIGN_AUTHORIZED_PARITY_SOFTENING.generated.json", "chummer6-ui.design_authorized_parity_softening"),
    ReceiptSpec("VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json", "chummer6-ui.veteran_task_time_evidence_gate"),
    ReceiptSpec("CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json", "chummer6-ui.chummer5a_screenshot_review_gate"),
    ReceiptSpec("CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json", "chummer6-ui.classic_dense_workbench_posture_gate"),
    ReceiptSpec("CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json", "chummer6-ui.chummer5a_legacy_ui_element_parity"),
    ReceiptSpec("CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json", "chummer6-ui.chummer4_legacy_ui_element_parity"),
    ReceiptSpec("SR5_SR6_UI_PARITY_AUDIT.generated.json", "chummer6-ui.sr5_sr6_ui_parity_audit"),
    ReceiptSpec("BLAZOR_BROWSER_LANE_PROOF_SET.generated.json", "chummer6-ui.blazor_browser_lane_proof_set"),
    ReceiptSpec("BLAZOR_PLAY_SURFACE_HORIZON.generated.json", "chummer6-ui.blazor_play_surface_horizon"),
    ReceiptSpec("FLAGSHIP_PRODUCT_READINESS.generated.json", "fleet.flagship_product_readiness"),
    ReceiptSpec("CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json", None),
    ReceiptSpec("DESKTOP_EXECUTABLE_EXIT_GATE.generated.json", "chummer6-ui.desktop_executable_exit_gate"),
    ReceiptSpec("NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json", "chummer6-ui.next90_m141_ui_direct_import_route_proof"),
    ReceiptSpec("NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json", "chummer6-ui.next90_m142_ui_direct_workflow_proof"),
    ReceiptSpec("NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json", "chummer6-ui.next90_m143_ui_direct_output_proof"),
    ReceiptSpec("SECTION_HOST_RULESET_PARITY.generated.json", "chummer6-ui.section_host_ruleset_parity"),
    ReceiptSpec("UI_LOCAL_RELEASE_PROOF.generated.json", "chummer6-ui.local_release_proof"),
    ReceiptSpec("BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json", "chummer6-ui.blazor_self_host_workbench_proof"),
    ReceiptSpec("BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json", "chummer6-ui.blazor_public_edge_workbench_proof"),
    ReceiptSpec("HUMAN_SIDE_RULE_AUTHORITY_GOLD_APPROVAL.generated.json", None, optional=True),
)

DESKTOP_WORKFLOW_INPUTS: tuple[ReceiptSpec, ...] = (
    ReceiptSpec("CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.chummer5a_desktop_workflow_parity"),
    ReceiptSpec("SR4_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.sr4_desktop_workflow_parity"),
    ReceiptSpec("SR6_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.sr6_desktop_workflow_parity"),
    ReceiptSpec("SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json", "chummer6-ui.sr4_sr6_desktop_parity_frontier"),
    ReceiptSpec("RULESET_UI_ADAPTATION.generated.json", "chummer6-ui.ruleset_ui_adaptation_frontier"),
    ReceiptSpec("UI_FLAGSHIP_RELEASE_GATE.generated.json", "chummer6-ui.flagship_ui_release_gate"),
    ReceiptSpec("DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json", "chummer6-ui.desktop_visual_familiarity_exit_gate"),
    ReceiptSpec("CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json", "chummer6-ui.chummer5a_screenshot_review_gate"),
    ReceiptSpec("NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json", "chummer6-ui.next90_m141_ui_direct_import_route_proof"),
    ReceiptSpec("NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json", "chummer6-ui.next90_m142_ui_direct_workflow_proof"),
    ReceiptSpec("HUMAN_SIDE_RULE_AUTHORITY_GOLD_APPROVAL.generated.json", None, optional=True),
)

STATIC_INPUTS: dict[str, tuple[ReceiptSpec, ...]] = {
    "b14": B14_INPUTS,
    "desktop-workflow": DESKTOP_WORKFLOW_INPUTS,
    "chummer5a": (),
    "sr4": (),
    "sr6": (
        ReceiptSpec("SR4_DESKTOP_WORKFLOW_PARITY.generated.json", "chummer6-ui.sr4_desktop_workflow_parity"),
    ),
}

OUTPUT_CONTRACTS = {
    "b14": "chummer6-ui.flagship_ui_release_gate",
    "desktop-workflow": "chummer6-ui.desktop_workflow_execution_gate",
    "chummer5a": "chummer6-ui.chummer5a_desktop_workflow_parity",
    "sr4": "chummer6-ui.sr4_desktop_workflow_parity",
    "sr6": "chummer6-ui.sr6_desktop_workflow_parity",
}

LEDGER_CONFIG = {
    "sr4": ("docs/SR4_WORKFLOW_PARITY_LEDGER.json", "sr4"),
    "sr6": ("docs/SR6_WORKFLOW_PARITY_LEDGER.json", "sr6"),
}

FAMILY_CONTRACTS = {
    "sr4": {
        "parityReceipts": "chummer6-ui.sr4_workflow_family_parity_receipt",
        "verificationReceipts": "chummer6-ui.sr4_workflow_family_verification_receipt",
        "executionReceipts": "chummer6-ui.sr4_workflow_family_execution_receipt",
    },
    "sr6": {
        "parityReceipts": "chummer6-ui.sr6_workflow_family_parity_receipt",
        "verificationReceipts": "chummer6-ui.sr6_workflow_family_verification_receipt",
        "executionReceipts": "chummer6-ui.sr6_workflow_family_execution_receipt",
    },
}


def _normalize(value: object) -> str:
    return str(value or "").strip()


def _load_object(path: Path, label: str) -> dict[str, Any]:
    try:
        loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise RoutingError(f"{label} is not valid JSON: {path}: {exc}") from exc
    if not isinstance(loaded, dict):
        raise RoutingError(f"{label} must be a JSON object: {path}")
    return loaded


def _require_regular_non_symlink(path: Path, label: str) -> None:
    if path.is_symlink():
        raise RoutingError(f"{label} must not be a symbolic link: {path}")
    if not path.is_file():
        raise RoutingError(f"{label} must be an existing regular file: {path}")


def _contract_name(payload: dict[str, Any], path: Path) -> str:
    snake = _normalize(payload.get("contract_name"))
    camel = _normalize(payload.get("contractName"))
    if snake and camel and snake != camel:
        raise RoutingError(f"receipt has conflicting contract aliases: {path}")
    return snake or camel


def _validate_receipt(path: Path, spec: ReceiptSpec) -> None:
    if spec.optional and not path.exists() and not path.is_symlink():
        return
    _require_regular_non_symlink(path, "proof input")
    payload = _load_object(path, "proof input")
    actual_contract = _contract_name(payload, path)
    if spec.contract_name and actual_contract != spec.contract_name:
        raise RoutingError(
            f"proof input contract must be {spec.contract_name}, got "
            f"{actual_contract or '<missing>'}: {path}"
        )
    status = _normalize(payload.get("status")).lower()
    if status not in PASS_STATUSES:
        raise RoutingError(
            f"proof input must be pass/passed/ready, got {status or '<missing>'}: {path}"
        )


def _validate_input_containment(path: Path, input_root: Path) -> None:
    try:
        relative_path = path.relative_to(input_root)
    except ValueError as exc:
        raise RoutingError(f"proof input must stay under explicit input root: {path}") from exc
    current = input_root
    for component in relative_path.parts[:-1]:
        current /= component
        if current.is_symlink():
            raise RoutingError(f"proof input directory must not be a symbolic link: {current}")
    try:
        resolved_root = input_root.resolve(strict=True)
        resolved_path = path.resolve(strict=True)
        resolved_path.relative_to(resolved_root)
    except (OSError, ValueError) as exc:
        raise RoutingError(
            f"proof input resolves outside explicit input root {input_root}: {path}"
        ) from exc


def _validate_release_channel(path: Path) -> None:
    _require_regular_non_symlink(path, "release channel input")
    payload = _load_object(path, "release channel input")
    contract_name = _contract_name(payload, path)
    if contract_name != RELEASE_CHANNEL_CONTRACT:
        raise RoutingError(
            f"release channel input contract must be {RELEASE_CHANNEL_CONTRACT}, got "
            f"{contract_name or '<missing>'}: {path}"
        )
    status = _normalize(payload.get("status")).lower()
    if status != RELEASE_CHANNEL_STATUS:
        raise RoutingError(
            f"release channel input status must be {RELEASE_CHANNEL_STATUS}, got "
            f"{status or '<missing>'}: {path}"
        )
    channel_id_camel = _normalize(payload.get("channelId"))
    channel_id_legacy = _normalize(payload.get("channel"))
    version_plain = _normalize(payload.get("version"))
    version_release = _normalize(payload.get("releaseVersion"))
    published_at_camel = _normalize(payload.get("publishedAt"))
    published_at_snake = _normalize(payload.get("published_at"))
    for label, left, right in (
        ("channelId/channel", channel_id_camel, channel_id_legacy),
        ("version/releaseVersion", version_plain, version_release),
        ("publishedAt/published_at", published_at_camel, published_at_snake),
    ):
        if left and right and left != right:
            raise RoutingError(f"release channel input has conflicting {label} aliases: {path}")
    channel_id = channel_id_camel or channel_id_legacy
    version = version_plain or version_release
    published_at = published_at_camel or published_at_snake
    missing = [
        label
        for label, value in (
            ("channelId/channel", channel_id),
            ("version/releaseVersion", version),
            ("publishedAt/published_at", published_at),
        )
        if not value
    ]
    if missing:
        raise RoutingError(
            f"release channel input is missing required field(s) {', '.join(missing)}: {path}"
        )


def _ledger_specs(repo_root: Path, input_root: Path, edition: str) -> list[ReceiptSpec]:
    ledger_relative, _ = LEDGER_CONFIG[edition]
    ledger_path = repo_root / ledger_relative
    payload = _load_object(ledger_path, f"{edition.upper()} workflow parity ledger")
    specs: dict[str, ReceiptSpec] = {}
    for family in payload.get("requiredFamilies") or []:
        if not isinstance(family, dict):
            continue
        family_id = _normalize(family.get("id"))
        if not family_id:
            continue
        for field, contract_name in FAMILY_CONTRACTS[edition].items():
            for raw in family.get(field) or []:
                value = _normalize(raw).replace("{familyId}", family_id)
                if not value:
                    continue
                relative = Path(value)
                try:
                    mapped = relative.relative_to(PUBLISHED_PREFIX)
                except ValueError as exc:
                    raise RoutingError(
                        f"{edition.upper()} ledger receipt must stay under {PUBLISHED_PREFIX}: {value}"
                    ) from exc
                spec = ReceiptSpec(str(mapped), contract_name)
                specs[spec.relative_path] = spec
    return [specs[key] for key in sorted(specs)]


def required_inputs(
    producer: str,
    repo_root: Path,
    input_root: Path | None,
) -> list[tuple[ReceiptSpec, Path]]:
    if producer not in STATIC_INPUTS:
        raise RoutingError(f"unknown producer: {producer}")
    specs = list(STATIC_INPUTS[producer])
    if producer in {"sr4", "sr6"}:
        if input_root is None:
            raise RoutingError(f"{producer} external plane requires a proof input root")
        specs.extend(_ledger_specs(repo_root, input_root, producer))
    elif producer == "desktop-workflow":
        if input_root is None:
            raise RoutingError("desktop-workflow external plane requires a proof input root")
        specs.extend(_ledger_specs(repo_root, input_root, "sr4"))
        specs.extend(_ledger_specs(repo_root, input_root, "sr6"))

    if specs and input_root is None:
        raise RoutingError(f"{producer} external plane requires a proof input root")
    return [(spec, input_root / spec.relative_path) for spec in specs] if input_root else []


def _paths_alias(left: Path, right: Path) -> bool:
    try:
        if left.resolve(strict=False) == right.resolve(strict=False):
            return True
        return left.exists() and right.exists() and left.samefile(right)
    except OSError as exc:
        raise RoutingError(f"could not compare proof paths {left} and {right}: {exc}") from exc


def _path_contains(container: Path, child: Path) -> bool:
    try:
        container_resolved = container.resolve(strict=False)
        child_resolved = child.resolve(strict=False)
    except OSError as exc:
        raise RoutingError(f"could not resolve proof paths {container} and {child}: {exc}") from exc
    return container_resolved == child_resolved or container_resolved in child_resolved.parents


def _validate_output(path: Path, protected_paths: Iterable[Path], label: str) -> None:
    if path.is_symlink():
        raise RoutingError(f"{label} must not be a symbolic link: {path}")
    if path.exists() and not path.is_file():
        raise RoutingError(f"existing {label} must be a regular file: {path}")
    for protected in protected_paths:
        if _paths_alias(path, protected):
            raise RoutingError(f"{label} must not alias proof input: {protected}")


def preflight_external_plane(
    *,
    producer: str,
    output_path: Path,
    repo_root: Path,
    release_channel_path: Path,
    input_root: Path | None = None,
    sidecar_output: Path | None = None,
) -> list[Path]:
    if producer not in OUTPUT_CONTRACTS:
        raise RoutingError(f"unknown producer: {producer}")
    if input_root is not None:
        if input_root.is_symlink() or not input_root.is_dir():
            raise RoutingError(
                f"proof input root must be an existing non-symlink directory: {input_root}"
            )
    _validate_release_channel(release_channel_path)
    resolved_inputs = required_inputs(producer, repo_root, input_root)
    for spec, path in resolved_inputs:
        if spec.optional and not path.exists() and not path.is_symlink():
            continue
        _validate_receipt(path, spec)
        if input_root is not None:
            _validate_input_containment(path, input_root)
    protected_paths = [release_channel_path, *(path for _, path in resolved_inputs)]
    if input_root is not None:
        protected_paths.append(input_root)
    _validate_output(output_path, protected_paths, "proof output")
    if input_root is not None and (
        _path_contains(input_root, output_path) or _path_contains(output_path, input_root)
    ):
        raise RoutingError("proof output and explicit input root must not overlap")
    if sidecar_output is not None:
        if sidecar_output.is_symlink():
            raise RoutingError(f"sidecar output must not be a symbolic link: {sidecar_output}")
        if sidecar_output.exists() and not sidecar_output.is_dir():
            raise RoutingError(f"existing sidecar output must be a directory: {sidecar_output}")
        for protected in protected_paths:
            if _paths_alias(sidecar_output, protected):
                raise RoutingError(f"sidecar output must not alias proof input: {protected}")
            if _path_contains(sidecar_output, protected):
                raise RoutingError(f"sidecar output must not contain proof input: {protected}")
            if protected == input_root and _path_contains(protected, sidecar_output):
                raise RoutingError("sidecar output and explicit input root must not overlap")
        if _path_contains(output_path, sidecar_output) or _path_contains(sidecar_output, output_path):
            raise RoutingError("proof output and sidecar output must not overlap")
    return protected_paths


def _validate_output_payload(producer: str, payload: dict[str, Any]) -> None:
    contract_name = _contract_name(payload, Path("<generated-payload>"))
    expected = OUTPUT_CONTRACTS[producer]
    if contract_name != expected:
        raise RoutingError(
            f"generated output contract must be {expected}, got {contract_name or '<missing>'}"
        )
    status = _normalize(payload.get("status")).lower()
    if status not in OUTPUT_STATUSES:
        raise RoutingError(f"generated output has unsupported status: {status or '<missing>'}")


def atomic_write_json(
    *,
    producer: str,
    output_path: Path,
    payload: dict[str, Any],
    repo_root: Path,
    release_channel_path: Path,
    input_root: Path | None = None,
) -> None:
    _validate_output_payload(producer, payload)
    preflight_external_plane(
        producer=producer,
        output_path=output_path,
        repo_root=repo_root,
        release_channel_path=release_channel_path,
        input_root=input_root,
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            dir=output_path.parent,
            prefix=f".{output_path.name}.",
            suffix=".tmp",
            delete=False,
        ) as handle:
            temporary_path = Path(handle.name)
            json.dump(payload, handle, indent=2)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        # Revalidate immediately before replacement. os.replace replaces a raced
        # symlink/hardlink directory entry rather than following it.
        preflight_external_plane(
            producer=producer,
            output_path=output_path,
            repo_root=repo_root,
            release_channel_path=release_channel_path,
            input_root=input_root,
        )
        os.replace(temporary_path, output_path)
        temporary_path = None
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)


def atomic_replace_directory(
    *,
    producer: str,
    source: Path,
    output_path: Path,
    repo_root: Path,
    release_channel_path: Path,
    input_root: Path,
) -> None:
    if not source.is_dir() or source.is_symlink():
        raise RoutingError(f"sidecar source must be an existing non-symlink directory: {source}")
    preflight_external_plane(
        producer=producer,
        output_path=output_path.parent / f".{output_path.name}.receipt-probe",
        repo_root=repo_root,
        release_channel_path=release_channel_path,
        input_root=input_root,
        sidecar_output=output_path,
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    staged = Path(tempfile.mkdtemp(prefix=f".{output_path.name}.", dir=output_path.parent))
    backup = output_path.parent / f".{output_path.name}.backup.{uuid.uuid4().hex}"
    moved_existing = False
    try:
        shutil.copytree(source, staged, dirs_exist_ok=True)
        preflight_external_plane(
            producer=producer,
            output_path=output_path.parent / f".{output_path.name}.receipt-probe",
            repo_root=repo_root,
            release_channel_path=release_channel_path,
            input_root=input_root,
            sidecar_output=output_path,
        )
        if output_path.exists():
            os.replace(output_path, backup)
            moved_existing = True
        try:
            os.replace(staged, output_path)
        except BaseException:
            if moved_existing and backup.exists() and not output_path.exists():
                os.replace(backup, output_path)
                moved_existing = False
            raise
        if moved_existing:
            if backup.is_symlink() or backup.is_file():
                backup.unlink()
            else:
                shutil.rmtree(backup)
            moved_existing = False
    finally:
        if staged.exists():
            shutil.rmtree(staged)
        if moved_existing and backup.exists() and not output_path.exists():
            os.replace(backup, output_path)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    for command in ("preflight", "replace-directory"):
        child = subparsers.add_parser(command)
        child.add_argument("--producer", required=True, choices=sorted(OUTPUT_CONTRACTS))
        child.add_argument("--output", required=True, type=Path)
        child.add_argument("--repo-root", required=True, type=Path)
        child.add_argument("--release-channel", required=True, type=Path)
        child.add_argument("--input-root", type=Path)
        child.add_argument("--sidecar-output", type=Path)
        if command == "replace-directory":
            child.add_argument("--source", required=True, type=Path)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        if args.command == "preflight":
            preflight_external_plane(
                producer=args.producer,
                output_path=args.output,
                repo_root=args.repo_root,
                release_channel_path=args.release_channel,
                input_root=args.input_root,
                sidecar_output=args.sidecar_output,
            )
        else:
            if args.input_root is None:
                raise RoutingError("replace-directory requires --input-root")
            atomic_replace_directory(
                producer=args.producer,
                source=args.source,
                output_path=args.output,
                repo_root=args.repo_root,
                release_channel_path=args.release_channel,
                input_root=args.input_root,
            )
    except RoutingError as exc:
        print(f"[candidate-proof-routing] FAIL: {exc}", file=os.sys.stderr)
        return 65
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
