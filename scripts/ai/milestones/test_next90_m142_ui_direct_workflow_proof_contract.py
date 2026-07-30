#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import os
import shutil
import stat
import subprocess
import tempfile
import time
import unittest
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable


SCRIPT_PATH = Path(__file__).with_name("next90-m142-ui-direct-workflow-proof-check.sh")
PACKAGE_ID = "next90-m142-ui-close-direct-screenshot-and-runtime-proof-for-dense-builder-and-career-fl"
TITLE = "Close direct screenshot and runtime proof for dense builder and career flows, dice or initiative utilities, and contacts or lifestyles or notes workflows."
DO_NOT_REOPEN_REASON = (
    "M142 chummer6-ui dense builder/career, dice/initiative, and contacts/lifestyles/notes direct proof is complete; "
    "future shards must verify the closed-package receipt, focused guard test, route-local gates, canonical registry row, "
    "and queue mirrors instead of reopening this slice."
)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


class Fixture:
    def __init__(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="m142-contract-")
        self.root = Path(self.temporary.name) / "repo"
        self.script = self.root / "scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh"
        self.registry = self.root / "fixture/registry.yaml"
        self.queue = self.root / "fixture/queue.yaml"
        self.design_queue = self.root / "fixture/design-queue.yaml"
        self.release = self.root / "fixture/release.json"
        self.output = self.root / "output/m142.json"
        self._create()

    def close(self) -> None:
        self.temporary.cleanup()

    def __enter__(self) -> Fixture:
        return self

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> None:
        self.close()

    def write_text(self, relative: str | Path, value: str) -> Path:
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(value, encoding="utf-8")
        return path

    def write_json(self, relative: str | Path, value: dict[str, Any]) -> Path:
        return self.write_text(relative, json.dumps(value, indent=2) + "\n")

    def read_json(self, relative: str | Path) -> dict[str, Any]:
        return json.loads((self.root / relative).read_text(encoding="utf-8"))

    def mutate_json(self, path: Path, mutator: Callable[[dict[str, Any]], None]) -> None:
        value = json.loads(path.read_text(encoding="utf-8"))
        mutator(value)
        path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")

    def _proof_paths(self) -> list[str]:
        published_root = self.root
        return [
            f"{published_root}/Chummer.Tests/Compliance/Next90M142DirectWorkflowProofGuardTests.cs",
            f"{published_root}/Chummer.Tests/Chummer.Tests.csproj",
            f"{published_root}/scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh",
            f"{published_root}/scripts/ai/milestones/chummer5a-screenshot-review-gate.sh",
            f"{published_root}/scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh",
            f"{published_root}/scripts/ai/milestones/b14-flagship-ui-release-gate.sh",
            f"{published_root}/scripts/ai/verify.sh",
            f"{published_root}/.codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json",
            f"{published_root}/.codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
            f"{published_root}/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json",
            f"{published_root}/.codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json",
            f"{published_root}/.codex-studio/published/NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json",
            f"{published_root}/.codex-studio/published/NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json",
        ]

    def _registry_evidence(self) -> list[str]:
        published_root = self.root
        return [
            (
                f"{published_root}/scripts/ai/milestones/chummer5a-screenshot-review-gate.sh, "
                f"{published_root}/scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh, and "
                f"{published_root}/scripts/ai/milestones/b14-flagship-ui-release-gate.sh keep dense builder/career, "
                "dice/initiative, and contacts/lifestyles/notes proof bound to direct screenshot-backed and runtime-backed route receipts instead of family prose."
            ),
            (
                f"{published_root}/.codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json, "
                f"{published_root}/.codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json, "
                f"{published_root}/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json, "
                f"{published_root}/.codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json, "
                f"{published_root}/.codex-studio/published/NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json, and "
                f"{published_root}/.codex-studio/published/NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json keep the three milestone-142 parity families aligned to route-local proof."
            ),
            (
                f"{published_root}/Chummer.Tests/Compliance/Next90M142DirectWorkflowProofGuardTests.cs, "
                f"{published_root}/Chummer.Tests/Chummer.Tests.csproj, "
                f"{published_root}/scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh, and "
                f"{published_root}/scripts/ai/verify.sh fail closed when canonical registry rows, queue mirrors, audit evidence, or verify wiring drift from the completed package contract."
            ),
            (
                f"{published_root}/.codex-studio/published/NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json records the closed-package receipt for "
                f"`{PACKAGE_ID}`."
            ),
        ]

    def _create_queue_inputs(self) -> None:
        direct_command = "bash scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh"
        test_command = 'dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M142DirectWorkflowProofGuardTests" --no-restore'
        registry_lines = [
            "- id: '142.1'",
            f"  title: {TITLE}",
            "  status: complete",
            "  completion_action: verify_closed_package_only",
            f"  do_not_reopen_reason: {DO_NOT_REOPEN_REASON}",
            "  evidence:",
        ]
        registry_lines.extend(f"    - {item}" for item in self._registry_evidence())
        registry_lines.extend(["- id: '142.2'", "  status: pending"])
        self.write_text(self.registry.relative_to(self.root), "\n".join(registry_lines) + "\n")

        queue_lines = [
            f"- title: {TITLE}",
            f"  package_id: {PACKAGE_ID}",
            "  status: complete",
            "  completion_action: verify_closed_package_only",
            f"  do_not_reopen_reason: {DO_NOT_REOPEN_REASON}",
            "  frontier_id: 9095697868",
            "  proof:",
        ]
        queue_lines.extend(f"    - {item}" for item in self._proof_paths() + [direct_command, test_command])
        queue_lines.extend(["- title: next fixture row", "  package_id: next"])
        queue_text = "\n".join(queue_lines) + "\n"
        self.write_text(self.queue.relative_to(self.root), queue_text)
        self.write_text(self.design_queue.relative_to(self.root), queue_text)

    def _create(self) -> None:
        self.script.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(SCRIPT_PATH, self.script)
        self.write_text("Chummer.Tests/Compliance/Next90M142DirectWorkflowProofGuardTests.cs", "fixture guard source\n")
        self.write_text("Chummer.Tests/Chummer.Tests.csproj", "<Project />\n")
        self.write_text(
            "scripts/ai/milestones/chummer5a-screenshot-review-gate.sh",
            "\n".join(
                [
                    '"dense_workbench_and_initiative"',
                    '"menu:dice_roller_or_workflow:initiative_screenshot"',
                    '"05-dense-section-light.png"',
                    '"07-loaded-runner-tabs-light.png"',
                ]
            )
            + "\n",
        )
        self.write_text(
            "scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh",
            "\n".join(
                [
                    '"dense_builder_career"',
                    '"initiative_utility"',
                    '"contacts_lifestyles_notes"',
                    '"10-contacts-section-light.png"',
                    '"11-diary-dialog-light.png"',
                    '"14-advancement-dialog-light.png"',
                ]
            )
            + "\n",
        )
        self.write_text(
            "scripts/ai/milestones/b14-flagship-ui-release-gate.sh",
            "\n".join(
                [
                    '"family:dense_builder_and_career_workflows"',
                    '"SECTION_HOST_RULESET_PARITY.generated.json"',
                    '"CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"',
                    '"CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json"',
                    '"UI_LOCAL_RELEASE_PROOF.generated.json"',
                ]
            )
            + "\n",
        )
        self.write_text(
            "scripts/ai/verify.sh",
            "checking next-90 M142 direct workflow proof guard\n"
            "bash scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh\n",
        )
        self._create_queue_inputs()

        generated_at = utc_now()
        release = {
            "schemaVersion": 1,
            "contract_name": "Chummer.Hub.Registry.Contracts",
            "contractName": "Chummer.Hub.Registry.Contracts",
            "status": "published",
            "generatedAt": generated_at,
            "generated_at": generated_at,
            "channelId": "preview",
            "channel": "preview",
            "releaseVersion": "run-fixture-1",
            "version": "run-fixture-1",
        }
        self.write_json(self.release.relative_to(self.root), release)

        family_evidence = {
            "family:dense_builder_and_career_workflows": [
                "SECTION_HOST_RULESET_PARITY.generated.json",
                "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
                "CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json",
                "UI_FLAGSHIP_RELEASE_GATE.generated.json",
                "UI_LOCAL_RELEASE_PROOF.generated.json",
                "VETERAN_TASK_TIME_EVIDENCE_GATE.generated.json",
            ],
            "family:dice_initiative_and_table_utilities": [
                "GENERATED_DIALOG_ELEMENT_PARITY.generated.json",
                "SECTION_HOST_RULESET_PARITY.generated.json",
                "NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json",
            ],
            "family:identity_contacts_lifestyles_history": [
                "SECTION_HOST_RULESET_PARITY.generated.json",
                "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json",
                "UI_FLAGSHIP_RELEASE_GATE.generated.json",
            ],
        }
        audit = {
            "probe_kind": "ui_parity_audit",
            "status": "pass",
            "generated_at": generated_at,
            "visualNoCount": 0,
            "behavioralNoCount": 0,
            "releaseBlockingNoCount": 0,
            "findings": [],
            "coverageGapKeys": [],
            "rows": [
                {
                    "id": family_id,
                    "visual_parity": "yes",
                    "behavioral_parity": "yes",
                    "evidence": evidence,
                }
                for family_id, evidence in family_evidence.items()
            ],
        }
        self.write_json(".codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json", audit)
        screenshot = {
            "contractName": "chummer6-ui.chummer5a_screenshot_review_gate",
            "status": "pass",
            "generatedAt": generated_at,
            "channelId": "preview",
            "channel": "preview",
            "releaseVersion": "run-fixture-1",
            "version": "run-fixture-1",
            "routeLocalReceipts": {
                "dense_workbench_and_initiative": {
                    "status": "pass",
                    "routeIds": [
                        "menu:dice_roller_or_workflow:initiative_screenshot",
                        "dice_roller",
                        "initiative_screenshot",
                    ],
                    "screenshots": ["05-dense-section-light.png", "07-loaded-runner-tabs-light.png"],
                }
            },
        }
        self.write_json(".codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json", screenshot)
        workflow = {
            "contract_name": "chummer6-ui.desktop_workflow_execution_gate",
            "status": "pass",
            "generatedAt": generated_at,
            "channelId": "preview",
            "releaseVersion": "run-fixture-1",
            "evidence": {
                "direct_workflow_runtime_marker_checks": {
                    "dense_builder_career": {"status": "pass"},
                    "initiative_utility": {"status": "pass"},
                    "contacts_lifestyles_notes": {"status": "pass"},
                },
                "direct_workflow_required_screenshot_files": [
                    "05-dense-section-light.png",
                    "07-loaded-runner-tabs-light.png",
                    "10-contacts-section-light.png",
                    "11-diary-dialog-light.png",
                    "14-advancement-dialog-light.png",
                ],
                "direct_workflow_missing_screenshot_files": [],
            },
        }
        self.write_json(".codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json", workflow)
        self.write_json(
            ".codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json",
            {
                "contract_name": "chummer6-ui.generated_dialog_element_parity",
                "status": "pass",
                "generatedAt": generated_at,
                "evidence": {
                    "commandIdsFound": ["dice_roller"],
                    "rebuildableDialogIdsFound": ["dialog.dice_roller"],
                },
            },
        )
        self.write_json(
            ".codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json",
            {
                "contract_name": "chummer6-ui.section_host_ruleset_parity",
                "status": "pass",
                "generatedAt": generated_at,
                "evidence": {"commandIdsFound": ["dice_roller"]},
            },
        )
        self.write_json(
            ".codex-studio/published/NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json",
            {
                "contract_name": "chummer6-ui.next90_m121_ui_gm_runboard_route",
                "status": "pass",
                "generatedAt": generated_at,
                "evidence": {"closedPackage": {"completionAction": "verify_closed_package_only"}},
            },
        )

    def environment(self) -> dict[str, str]:
        environment = os.environ.copy()
        environment.update(
            {
                "CHUMMER_NEXT90_REGISTRY_PATH": str(self.registry),
                "CHUMMER_NEXT90_QUEUE_PATH": str(self.queue),
                "CHUMMER_NEXT90_DESIGN_QUEUE_PATH": str(self.design_queue),
                "CHUMMER_NEXT90_M142_RELEASE_CHANNEL_PATH": str(self.release),
                "CHUMMER_NEXT90_M142_UI_RECEIPT_PATH": str(self.output),
            }
        )
        return environment

    def run(self, extra_environment: dict[str, str] | None = None) -> subprocess.CompletedProcess[str]:
        environment = self.environment()
        if extra_environment:
            environment.update(extra_environment)
        return subprocess.run(
            ["bash", str(self.script)],
            cwd=self.root,
            env=environment,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
            timeout=20,
        )

    def output_payload(self) -> dict[str, Any]:
        return json.loads(self.output.read_text(encoding="utf-8"))


class M142DirectWorkflowProofContractTests(unittest.TestCase):
    def test_valid_fixture_emits_bound_atomic_release_scoped_receipt(self) -> None:
        with Fixture() as fixture:
            release_hash = hashlib.sha256(fixture.release.read_bytes()).hexdigest()
            result = fixture.run()
            self.assertEqual(0, result.returncode, result.stderr)
            payload = fixture.output_payload()
            self.assertEqual(1, payload["schemaVersion"])
            self.assertEqual("chummer6-ui.next90_m142_ui_direct_workflow_proof", payload["contract_name"])
            self.assertEqual(payload["contract_name"], payload["contractName"])
            self.assertEqual("pass", payload["status"])
            self.assertEqual("preview", payload["channelId"])
            self.assertEqual(payload["channelId"], payload["channel"])
            self.assertEqual("run-fixture-1", payload["releaseVersion"])
            self.assertEqual(payload["releaseVersion"], payload["version"])
            uuid.UUID(payload["producerRunId"])
            self.assertEqual(release_hash, payload["releaseEvidence"]["sha256"])
            self.assertTrue(all(payload["releaseEvidence"]["checks"].values()))
            bindings = payload["evidence"]["inputBindings"]
            self.assertIn("releaseChannel", bindings)
            self.assertIn("producerSource", bindings)
            self.assertTrue(all(payload["evidence"]["finalInputRevalidation"].values()))
            self.assertTrue(stat.S_ISREG(fixture.output.stat().st_mode))
            self.assertEqual(0o644, stat.S_IMODE(fixture.output.stat().st_mode))
            leftovers = [entry.name for entry in fixture.output.parent.iterdir() if entry.name != fixture.output.name]
            self.assertEqual([], leftovers)

    def test_missing_and_wrong_release_fail_closed(self) -> None:
        cases: list[tuple[str, Callable[[Fixture], None], str]] = [
            ("missing", lambda fixture: fixture.release.unlink(), "releaseChannel is unavailable"),
            (
                "wrong-status",
                lambda fixture: fixture.mutate_json(fixture.release, lambda payload: payload.__setitem__("status", "pass")),
                "Release channel proof check failed: status_published",
            ),
            (
                "wrong-contract",
                lambda fixture: fixture.mutate_json(
                    fixture.release,
                    lambda payload: (
                        payload.__setitem__("contract_name", "generic.release.receipt"),
                        payload.__setitem__("contractName", "generic.release.receipt"),
                    ),
                ),
                "Release channel proof check failed: contract_exact",
            ),
        ]
        for name, mutate, expected in cases:
            with self.subTest(name=name), Fixture() as fixture:
                mutate(fixture)
                result = fixture.run()
                self.assertEqual(43, result.returncode, result.stderr)
                payload = fixture.output_payload()
                self.assertEqual("fail", payload["status"])
                self.assertTrue(any(expected in item for item in payload["unresolved"]))

    def test_release_alias_conflict_fails_closed(self) -> None:
        with Fixture() as fixture:
            fixture.mutate_json(fixture.release, lambda payload: payload.__setitem__("channel", "stable"))
            result = fixture.run()
            self.assertEqual(43, result.returncode, result.stderr)
            payload = fixture.output_payload()
            self.assertFalse(payload["releaseEvidence"]["checks"]["channel_aliases_agree"])
            self.assertIsNone(payload["channelId"])

    def test_supporting_receipts_require_exact_contract_and_status(self) -> None:
        cases: list[tuple[str, str, Callable[[dict[str, Any]], None], str]] = [
            (
                "wrong-contract",
                ".codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
                lambda payload: payload.__setitem__("contractName", "generic.pass.receipt"),
                "screenshot_contract_exact",
            ),
            (
                "status-synonym",
                ".codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json",
                lambda payload: payload.__setitem__("status", "ready"),
                "workflow_status_pass",
            ),
            (
                "release-mismatch",
                ".codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
                lambda payload: (
                    payload.__setitem__("releaseVersion", "run-unrelated"),
                    payload.__setitem__("version", "run-unrelated"),
                ),
                "screenshot_version_matches_release",
            ),
        ]
        for name, relative, mutate, expected_check in cases:
            with self.subTest(name=name), Fixture() as fixture:
                fixture.mutate_json(fixture.root / relative, mutate)
                result = fixture.run()
                self.assertEqual(43, result.returncode, result.stderr)
                payload = fixture.output_payload()
                self.assertFalse(payload["evidence"]["receiptChecks"][expected_check])

    def test_yaml_wrapped_queue_title_matches_canonical_scalar(self) -> None:
        with Fixture() as fixture:
            wrapped_title = (
                "Close direct screenshot and runtime proof for dense builder and career flows, dice or initiative utilities,\n"
                "    and contacts or lifestyles or notes workflows."
            )
            for path in (fixture.queue, fixture.design_queue):
                text = path.read_text(encoding="utf-8")
                path.write_text(text.replace(TITLE, wrapped_title, 1), encoding="utf-8")

            result = fixture.run()

            self.assertEqual(0, result.returncode, result.stderr)
            queue_checks = fixture.output_payload()["evidence"]["queueChecks"]
            self.assertTrue(queue_checks["queue_title_matches"])
            self.assertTrue(queue_checks["design_queue_title_matches"])

    def test_symlink_and_nonregular_inputs_are_rejected(self) -> None:
        with self.subTest(kind="symlink"), Fixture() as fixture:
            target = fixture.root / "fixture/real-release.json"
            fixture.release.replace(target)
            fixture.release.symlink_to(target)
            result = fixture.run()
            self.assertEqual(43, result.returncode, result.stderr)
            self.assertTrue(any("must not be a symbolic link" in item for item in fixture.output_payload()["unresolved"]))

        with self.subTest(kind="nonregular"), Fixture() as fixture:
            screenshot = fixture.root / ".codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
            screenshot.unlink()
            os.mkfifo(screenshot)
            result = fixture.run()
            self.assertEqual(43, result.returncode, result.stderr)
            self.assertTrue(any("must be a regular file" in item for item in fixture.output_payload()["unresolved"]))

    def test_direct_receipts_cannot_launder_missing_audit_or_workflow_proof(self) -> None:
        with Fixture() as fixture:
            audit_path = fixture.root / ".codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"
            fixture.mutate_json(
                audit_path,
                lambda payload: payload.__setitem__(
                    "rows",
                    [row for row in payload["rows"] if row["id"] != "family:dice_initiative_and_table_utilities"],
                ),
            )
            workflow_path = fixture.root / ".codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"
            fixture.mutate_json(
                workflow_path,
                lambda payload: payload["evidence"]["direct_workflow_runtime_marker_checks"]["initiative_utility"].__setitem__("status", "fail"),
            )
            result = fixture.run()
            self.assertEqual(43, result.returncode, result.stderr)
            payload = fixture.output_payload()
            family = payload["evidence"]["familyChecks"]["family:dice_initiative_and_table_utilities"]
            self.assertFalse(family["row_present_exactly_once"])
            self.assertFalse(payload["evidence"]["receiptChecks"]["workflow_initiative_utility_pass"])

    def test_self_receipt_cannot_supply_direct_family_evidence(self) -> None:
        with Fixture() as fixture:
            audit_path = fixture.root / ".codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"

            def replace_dice_evidence(payload: dict[str, Any]) -> None:
                for row in payload["rows"]:
                    if row["id"] == "family:dice_initiative_and_table_utilities":
                        row["evidence"] = ["NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json"]

            fixture.mutate_json(audit_path, replace_dice_evidence)
            result = fixture.run()
            self.assertEqual(43, result.returncode, result.stderr)
            family = fixture.output_payload()["evidence"]["familyChecks"]["family:dice_initiative_and_table_utilities"]
            self.assertTrue(family["row_present_exactly_once"])
            self.assertFalse(family["required_direct_evidence_present"])

    def test_mutation_after_snapshot_is_rejected_by_final_revalidation(self) -> None:
        with Fixture() as fixture:
            signal = Path(f"{fixture.output}.before-revalidation")
            continuation = Path(f"{fixture.output}.continue")
            environment = fixture.environment()
            environment.update(
                {
                    "CHUMMER_NEXT90_M142_TEST_REVALIDATION_RENDEZVOUS": "1",
                }
            )
            process = subprocess.Popen(
                ["bash", str(fixture.script)],
                cwd=fixture.root,
                env=environment,
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )
            deadline = time.monotonic() + 10.0
            while not signal.exists() and time.monotonic() < deadline:
                time.sleep(0.01)
            self.assertTrue(signal.exists(), "producer did not reach the revalidation rendezvous")
            source = fixture.root / "scripts/ai/verify.sh"
            source.write_text(source.read_text(encoding="utf-8") + "mutated after snapshot\n", encoding="utf-8")
            continuation.parent.mkdir(parents=True, exist_ok=True)
            continuation.write_text("continue\n", encoding="utf-8")
            stdout, stderr = process.communicate(timeout=10)
            self.assertEqual(43, process.returncode, f"stdout={stdout}\nstderr={stderr}")
            payload = fixture.output_payload()
            label = "source:scripts/ai/verify.sh"
            self.assertFalse(payload["evidence"]["finalInputRevalidation"][label])
            self.assertTrue(any(label in item for item in payload["unresolved"]))


if __name__ == "__main__":
    unittest.main(verbosity=2)
