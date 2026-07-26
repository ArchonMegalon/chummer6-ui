from __future__ import annotations

import hashlib
import json
import os
import subprocess
import sys
import tempfile
import unittest
from dataclasses import replace
from pathlib import Path
from unittest import mock


REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT / "scripts" / "ai"))

import candidate_proof_routing as routing  # noqa: E402


PRODUCERS: dict[str, dict[str, object]] = {
    "b14": {
        "script": REPO_ROOT / "scripts" / "ai" / "milestones" / "b14-flagship-ui-release-gate.sh",
        "plane": (
            "CHUMMER_B14_OUTPUT_PATH",
            "CHUMMER_B14_SCREENSHOT_OUTPUT_DIR",
            "CHUMMER_B14_PROOF_INPUT_ROOT",
            "CHUMMER_B14_RELEASE_CHANNEL_PATH",
        ),
        "output": "CHUMMER_B14_OUTPUT_PATH",
        "sidecar": "CHUMMER_B14_SCREENSHOT_OUTPUT_DIR",
        "input": "CHUMMER_B14_PROOF_INPUT_ROOT",
        "release": "CHUMMER_B14_RELEASE_CHANNEL_PATH",
        "default_output": "UI_FLAGSHIP_RELEASE_GATE.generated.json",
    },
    "desktop-workflow": {
        "script": REPO_ROOT
        / "scripts"
        / "ai"
        / "milestones"
        / "materialize-desktop-workflow-execution-gate.sh",
        "plane": (
            "CHUMMER_DESKTOP_WORKFLOW_OUTPUT_PATH",
            "CHUMMER_DESKTOP_WORKFLOW_PROOF_INPUT_ROOT",
            "CHUMMER_DESKTOP_WORKFLOW_EXTERNAL_RELEASE_CHANNEL_PATH",
        ),
        "output": "CHUMMER_DESKTOP_WORKFLOW_OUTPUT_PATH",
        "input": "CHUMMER_DESKTOP_WORKFLOW_PROOF_INPUT_ROOT",
        "release": "CHUMMER_DESKTOP_WORKFLOW_EXTERNAL_RELEASE_CHANNEL_PATH",
        "default_output": "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json",
    },
    "chummer5a": {
        "script": REPO_ROOT
        / "scripts"
        / "ai"
        / "milestones"
        / "chummer5a-desktop-workflow-parity-check.sh",
        "plane": (
            "CHUMMER_CHUMMER5A_WORKFLOW_PARITY_OUTPUT_PATH",
            "CHUMMER_CHUMMER5A_WORKFLOW_PARITY_RELEASE_CHANNEL_PATH",
        ),
        "output": "CHUMMER_CHUMMER5A_WORKFLOW_PARITY_OUTPUT_PATH",
        "release": "CHUMMER_CHUMMER5A_WORKFLOW_PARITY_RELEASE_CHANNEL_PATH",
        "default_output": "CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json",
    },
    "sr4": {
        "script": REPO_ROOT
        / "scripts"
        / "ai"
        / "milestones"
        / "sr4-desktop-workflow-parity-check.sh",
        "plane": (
            "CHUMMER_SR4_WORKFLOW_PARITY_OUTPUT_PATH",
            "CHUMMER_SR4_WORKFLOW_PARITY_PROOF_INPUT_ROOT",
            "CHUMMER_SR4_WORKFLOW_PARITY_RELEASE_CHANNEL_PATH",
        ),
        "output": "CHUMMER_SR4_WORKFLOW_PARITY_OUTPUT_PATH",
        "input": "CHUMMER_SR4_WORKFLOW_PARITY_PROOF_INPUT_ROOT",
        "release": "CHUMMER_SR4_WORKFLOW_PARITY_RELEASE_CHANNEL_PATH",
        "default_output": "SR4_DESKTOP_WORKFLOW_PARITY.generated.json",
    },
    "sr6": {
        "script": REPO_ROOT
        / "scripts"
        / "ai"
        / "milestones"
        / "sr6-desktop-workflow-parity-check.sh",
        "plane": (
            "CHUMMER_SR6_WORKFLOW_PARITY_OUTPUT_PATH",
            "CHUMMER_SR6_WORKFLOW_PARITY_PROOF_INPUT_ROOT",
            "CHUMMER_SR6_WORKFLOW_PARITY_RELEASE_CHANNEL_PATH",
        ),
        "output": "CHUMMER_SR6_WORKFLOW_PARITY_OUTPUT_PATH",
        "input": "CHUMMER_SR6_WORKFLOW_PARITY_PROOF_INPUT_ROOT",
        "release": "CHUMMER_SR6_WORKFLOW_PARITY_RELEASE_CHANNEL_PATH",
        "default_output": "SR6_DESKTOP_WORKFLOW_PARITY.generated.json",
    },
}

ALL_PLANE_VARIABLES = {
    variable
    for configuration in PRODUCERS.values()
    for variable in configuration["plane"]
}


class CandidateProofRoutingOverrideTests(unittest.TestCase):
    @staticmethod
    def _write_json(path: Path, payload: dict[str, object]) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    @staticmethod
    def _candidate_scope(
        *,
        release_version: str = "run-20260728-050000",
        owner: str = "chummer-release-operations",
        channel: str = "preview",
        release_target: str = "preview",
        platform: str = "macos",
        rid: str = "osx-arm64",
        signing_requirement: str = "signed",
        fallback_heads: tuple[str, ...] = ("blazor-desktop",),
    ) -> dict[str, object]:
        return {
            "approvedAtUtc": "2026-07-21T06:21:37Z",
            "approvedBy": "Release reviewer",
            "channel": channel,
            "contractName": "chummer.release-scope-decision/v1",
            "contractVersion": 1,
            "decisionId": f"nightly-{platform}-{rid}-20260728",
            "platforms": [
                {
                    "artifactAccessClass": "open_public",
                    "fallbackHeads": list(fallback_heads),
                    "platform": platform,
                    "primaryHead": "avalonia",
                    "rid": rid,
                    "signingRequirement": signing_requirement,
                }
            ],
            "releaseTarget": release_target,
            "releaseVersion": release_version,
            "status": "approved",
            "supportOwner": owner,
        }

    @staticmethod
    def _registry_artifact(
        head: str,
        *,
        platform: str = "macos",
        rid: str = "osx-arm64",
        arch: str = "arm64",
    ) -> dict[str, object]:
        extension = {"linux": "deb", "windows": "exe"}.get(platform, "dmg")
        artifact_id = f"chummer-{head}-{platform}-{arch}.{extension}"
        return {
            "artifactId": artifact_id,
            "head": head,
            "platform": platform,
            "rid": rid,
            "arch": arch,
            "kind": "installer",
            "downloadUrl": f"/downloads/g/generation-1/files/{artifact_id}",
            "sha256": "a" * 64 if head == "avalonia" else "b" * 64,
            "sizeBytes": 4096,
            "compatibilityState": "compatible",
            "promotionState": "promoted",
            "publicationScope": "signed-in-and-public",
            "revokeState": "not_revoked",
            "publicInstallRoute": f"/downloads/install/{artifact_id}",
            "installAccessClass": "open_public",
        }

    @classmethod
    def _registry_seed(
        cls,
        *,
        release_version: str = "run-20260728-050000",
        owner: str = "chummer-release-operations",
        channel: str = "preview",
        rollout_state: str = "promoted_preview",
        supportability_state: str = "preview_supported",
        platform: str = "macos",
        rid: str = "osx-arm64",
        arch: str = "arm64",
        fallback_heads: tuple[str, ...] = ("blazor-desktop",),
    ) -> dict[str, object]:
        artifacts = [
            cls._registry_artifact(
                head,
                platform=platform,
                rid=rid,
                arch=arch,
            )
            for head in ("avalonia", *fallback_heads)
        ]
        return {
            "authorityContract": "chummer.release-authority-snapshot/v2",
            "releaseVersion": release_version,
            "channel": channel,
            "status": "published",
            "rolloutState": rollout_state,
            "supportabilityState": supportability_state,
            "availablePlatforms": [platform],
            "primaryHeadByPlatform": {platform: "avalonia"},
            "artifactCount": len(artifacts),
            "downloadAccessPosture": "open_public",
            "knownIssueSummary": "Preview candidate under review.",
            "manifestSha256": "c" * 64,
            "registryRepository": "ArchonMegalon/chummer6-hub-registry",
            "registryCommit": "d" * 40,
            "releaseDecisionStatus": "review_required",
            "releaseDecisionSha256": "e" * 64,
            "releaseDecisionPath": "RELEASE_DECISION.json",
            "supportOwner": owner,
            "nextActions": ["Complete preview review."],
            "artifacts": artifacts,
            "manifestPath": "RELEASE_CHANNEL.json",
        }

    @classmethod
    def _candidate_context(
        cls,
        root: Path,
        *,
        scope: dict[str, object] | None = None,
        seed: dict[str, object] | None = None,
        expected_release_version: str = "run-20260728-050000",
        bounded_owner: str = "chummer-release-operations",
        next_actions: tuple[str, ...] = ("Capture stable desktop proof.",),
        allow_raw_fail_declaration: bool = True,
    ) -> routing.CampaignOperabilityCandidateContext:
        scope_path = root / "scope.json"
        scope_raw = (
            json.dumps(
                scope or cls._candidate_scope(),
                sort_keys=True,
                separators=(",", ":"),
            )
            + "\n"
        ).encode("utf-8")
        scope_path.write_bytes(scope_raw)
        seed_path = root / "registry-review-seed.json"
        seed_raw = (json.dumps(seed or cls._registry_seed(), indent=2) + "\n").encode(
            "utf-8"
        )
        seed_path.write_bytes(seed_raw)
        return routing.load_campaign_operability_candidate_context(
            approved_scope_path=scope_path,
            expected_scope_sha256=hashlib.sha256(scope_raw).hexdigest(),
            expected_release_version=expected_release_version,
            registry_review_seed_path=seed_path,
            expected_registry_review_seed_sha256=hashlib.sha256(seed_raw).hexdigest(),
            bounded_owner=bounded_owner,
            next_actions=next_actions,
            allow_raw_fail_declaration=allow_raw_fail_declaration,
        )

    @classmethod
    def _candidate_preflight_fixture(
        cls,
        root: Path,
        producer: str,
        *,
        channel: str = "preview",
        release_target: str = "preview",
        rollout_state: str = "promoted_preview",
        supportability_state: str = "preview_supported",
        signing_requirement: str = "signed",
        platform: str = "macos",
        rid: str = "osx-arm64",
        arch: str = "arm64",
        fallback_heads: tuple[str, ...] = ("blazor-desktop",),
        allow_raw_fail_declaration: bool = True,
    ) -> tuple[dict[str, str], Path, Path, Path | None]:
        release_channel_path = root / "authority" / "RELEASE_CHANNEL.json"
        release_channel_path.parent.mkdir(mode=0o700)
        release_channel_raw = (
            json.dumps(
                {
                    "contract_name": routing.RELEASE_CHANNEL_CONTRACT,
                    "status": "published",
                    "channelId": channel,
                    "channel": channel,
                    "version": "run-20260728-050000",
                    "releaseVersion": "run-20260728-050000",
                    "publishedAt": "2026-07-21T06:21:37Z",
                    "published_at": "2026-07-21T06:21:37Z",
                },
                sort_keys=True,
                separators=(",", ":"),
            )
            + "\n"
        ).encode("utf-8")
        release_channel_path.write_bytes(release_channel_raw)
        scope_path = root / "authority" / "scope.json"
        scope_raw = (
            json.dumps(
                cls._candidate_scope(
                    channel=channel,
                    release_target=release_target,
                    platform=platform,
                    rid=rid,
                    signing_requirement=signing_requirement,
                    fallback_heads=fallback_heads,
                ),
                sort_keys=True,
                separators=(",", ":"),
            )
            + "\n"
        ).encode("utf-8")
        scope_path.write_bytes(scope_raw)
        seed = cls._registry_seed(
            channel=channel,
            rollout_state=rollout_state,
            supportability_state=supportability_state,
            platform=platform,
            rid=rid,
            arch=arch,
            fallback_heads=fallback_heads,
        )
        seed["manifestSha256"] = hashlib.sha256(release_channel_raw).hexdigest()
        seed_path = root / "authority" / "registry-review-seed.json"
        seed_raw = (json.dumps(seed, sort_keys=True, separators=(",", ":")) + "\n").encode(
            "utf-8"
        )
        seed_path.write_bytes(seed_raw)
        output_parent = root / "candidate-output"
        output_parent.mkdir(mode=0o700)
        output_path = output_parent / f"{producer}.json"
        input_root: Path | None = None
        if producer == "desktop-workflow":
            input_root = root / "candidate-input"
            input_root.mkdir(mode=0o700)
        environment = {
            routing.CAMPAIGN_OPERABILITY_ENV["mode"]: "1",
            routing.CAMPAIGN_OPERABILITY_ENV["scope_path"]: str(scope_path),
            routing.CAMPAIGN_OPERABILITY_ENV["scope_sha256"]: hashlib.sha256(
                scope_raw
            ).hexdigest(),
            routing.CAMPAIGN_OPERABILITY_ENV[
                "release_version"
            ]: "run-20260728-050000",
            routing.CAMPAIGN_OPERABILITY_ENV["review_seed_path"]: str(seed_path),
            routing.CAMPAIGN_OPERABILITY_ENV[
                "review_seed_sha256"
            ]: hashlib.sha256(seed_raw).hexdigest(),
            routing.CAMPAIGN_OPERABILITY_ENV[
                "bounded_owner"
            ]: "chummer-release-operations",
            routing.CAMPAIGN_OPERABILITY_ENV["next_actions"]: json.dumps(
                ["Capture stable desktop proof."]
            ),
            routing.CAMPAIGN_OPERABILITY_ENV["allow_raw_fail"]: (
                "1" if allow_raw_fail_declaration else "0"
            ),
        }
        producer_environment = routing.CAMPAIGN_OPERABILITY_PRODUCER_ENV[producer]
        environment[producer_environment["output"]] = str(output_path)
        environment[producer_environment["release_channel"]] = str(
            release_channel_path
        )
        if input_root is not None:
            environment[producer_environment["input_root"]] = str(input_root)
        return environment, output_path, release_channel_path, input_root

    def _fixture(
        self,
        root: Path,
        producer: str,
    ) -> tuple[dict[str, str], Path, Path, Path, list[tuple[routing.ReceiptSpec, Path]]]:
        configuration = PRODUCERS[producer]
        input_root = root / "proof-inputs"
        input_root.mkdir(parents=True)
        release_channel = root / "release" / "RELEASE_CHANNEL.generated.json"
        self._write_json(
            release_channel,
            {
                "contract_name": routing.RELEASE_CHANNEL_CONTRACT,
                "status": routing.RELEASE_CHANNEL_STATUS,
                "channelId": "candidate-fixture",
                "channel": "candidate-fixture",
                "version": "6.0.0-fixture",
                "releaseVersion": "6.0.0-fixture",
                "publishedAt": "2026-07-18T13:14:45Z",
                "published_at": "2026-07-18T13:14:45Z",
            },
        )

        routed_input_root = input_root if "input" in configuration else None
        required = routing.required_inputs(producer, REPO_ROOT, routed_input_root)
        for receipt_spec, receipt_path in required:
            self._write_json(
                receipt_path,
                {
                    "contract_name": receipt_spec.contract_name or "fixture.unconstrained",
                    "generatedAt": "2026-07-18T13:14:45Z",
                    "status": "pass",
                },
            )

        output_path = root / "candidate-output" / str(configuration["default_output"])
        environment = {
            str(configuration["output"]): str(output_path),
            str(configuration["release"]): str(release_channel),
        }
        if "input" in configuration:
            environment[str(configuration["input"])] = str(input_root)
        if "sidecar" in configuration:
            environment[str(configuration["sidecar"])] = str(
                root / "candidate-output" / "flagship-screenshots"
            )
        return environment, output_path, release_channel, input_root, required

    @staticmethod
    def _run(
        producer: str,
        environment: dict[str, str],
    ) -> subprocess.CompletedProcess[str]:
        process_environment = os.environ.copy()
        for variable in ALL_PLANE_VARIABLES:
            process_environment.pop(variable, None)
        process_environment["CHUMMER_CANDIDATE_PROOF_ROUTING_PREFLIGHT_ONLY"] = "1"
        process_environment.update(environment)
        return subprocess.run(
            ["bash", str(PRODUCERS[producer]["script"])],
            cwd=REPO_ROOT,
            env=process_environment,
            capture_output=True,
            text=True,
            check=False,
            timeout=20,
        )

    def test_candidate_native_receipts_bind_exact_scope_and_registry_seed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            context = self._candidate_context(Path(temporary_directory))
            for producer, contract_name in routing.CAMPAIGN_OPERABILITY_PRODUCER_CONTRACTS.items():
                with self.subTest(producer=producer):
                    payload = {
                        "contract_name": contract_name,
                        "releaseVersion": context.release_version,
                        "status": "fail",
                        "verdict": "STABLE_PROOF_NOT_READY",
                        "reasons": ["Native candidate proof remains incomplete."],
                    }

                    decorated = routing.decorate_campaign_operability_candidate_payload(
                        producer=producer,
                        payload=payload,
                        context=context,
                    )

                    self.assertEqual("fail", decorated["status"])
                    self.assertEqual("STABLE_PROOF_NOT_READY", decorated["verdict"])
                    binding = decorated["campaign_operability_candidate_binding"]
                    self.assertEqual(
                        {
                            "contract_name",
                            "contract_version",
                            "release_version",
                            "release_scope_decision_sha256",
                            "manifest_sha256",
                            "authority_snapshot_sha256",
                            "release_decision_sha256",
                            "registry_commit",
                            "platform",
                            "rid",
                            "primary_head",
                            "required_heads",
                        },
                        set(binding),
                    )
                    self.assertEqual(
                        context.authority_snapshot_sha256,
                        binding["authority_snapshot_sha256"],
                    )
                    self.assertEqual(
                        context.release_scope_decision_sha256,
                        binding["release_scope_decision_sha256"],
                    )
                    self.assertEqual(context.release_version, binding["release_version"])
                    self.assertEqual("c" * 64, binding["manifest_sha256"])
                    self.assertEqual("e" * 64, binding["release_decision_sha256"])
                    declaration = decorated["campaign_operability_preview"]
                    self.assertEqual(
                        {
                            "contract_name",
                            "contract_version",
                            "status",
                            "release_version",
                            "release_scope_decision_sha256",
                            "bounded_owner",
                            "next_actions",
                        },
                        set(declaration),
                    )
                    self.assertEqual(2, declaration["contract_version"])
                    self.assertEqual("pass", declaration["status"])

    def test_candidate_native_v2_declaration_is_raw_fail_only(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            context = self._candidate_context(root)
            passing = routing.decorate_campaign_operability_candidate_payload(
                producer="desktop-visual",
                payload={
                    "contract_name": routing.CAMPAIGN_OPERABILITY_PRODUCER_CONTRACTS[
                        "desktop-visual"
                    ],
                    "releaseVersion": context.release_version,
                    "status": "pass",
                    "reasons": [],
                },
                context=context,
            )
            self.assertNotIn("campaign_operability_preview", passing)
            with self.assertRaisesRegex(
                routing.RoutingError, "requires explicit failure reasons"
            ):
                routing.decorate_campaign_operability_candidate_payload(
                    producer="desktop-visual",
                    payload={
                        "contract_name": routing.CAMPAIGN_OPERABILITY_PRODUCER_CONTRACTS[
                            "desktop-visual"
                        ],
                        "releaseVersion": context.release_version,
                        "status": "fail",
                        "reasons": [],
                    },
                    context=context,
                )

            no_declaration_context = self._candidate_context(
                root,
                allow_raw_fail_declaration=False,
            )
            failing = routing.decorate_campaign_operability_candidate_payload(
                producer="desktop-executable",
                payload={
                    "contract_name": routing.CAMPAIGN_OPERABILITY_PRODUCER_CONTRACTS[
                        "desktop-executable"
                    ],
                    "releaseVersion": context.release_version,
                    "status": "fail",
                    "reasons": ["Native execution proof is pending."],
                },
                context=no_declaration_context,
            )
            self.assertNotIn("campaign_operability_preview", failing)

    def test_candidate_environment_is_explicit_and_all_or_none(self) -> None:
        self.assertIsNone(
            routing.campaign_operability_candidate_context_from_environment({})
        )
        with self.assertRaisesRegex(routing.RoutingError, "explicit candidate mode"):
            routing.campaign_operability_candidate_context_from_environment(
                {
                    routing.CAMPAIGN_OPERABILITY_ENV[
                        "release_version"
                    ]: "run-20260728-050000"
                }
            )
        with self.assertRaisesRegex(routing.RoutingError, "complete candidate plane"):
            routing.campaign_operability_candidate_context_from_environment(
                {routing.CAMPAIGN_OPERABILITY_ENV["mode"]: "1"}
            )
        with self.assertRaisesRegex(routing.RoutingError, "must be exactly 0 or 1"):
            routing.campaign_operability_candidate_context_from_environment(
                {routing.CAMPAIGN_OPERABILITY_ENV["mode"]: " 1 "}
            )

    def test_candidate_mode_legacy_alias_is_preview_only_and_conflicts_fail(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            environment, _, _, _ = self._candidate_preflight_fixture(
                Path(temporary_directory),
                "desktop-visual",
            )
            environment[routing.CAMPAIGN_OPERABILITY_ENV["legacy_preview_mode"]] = (
                environment.pop(routing.CAMPAIGN_OPERABILITY_ENV["mode"])
            )

            context = (
                routing.campaign_operability_candidate_context_from_environment(
                    environment
                )
            )

            self.assertIsNotNone(context)
            self.assertEqual("preview", context.channel if context else "")
            self.assertEqual("preview", context.release_target if context else "")

        with tempfile.TemporaryDirectory() as temporary_directory:
            stable_environment, _, _, _ = self._candidate_preflight_fixture(
                Path(temporary_directory),
                "desktop-visual",
                channel="public_stable",
                release_target="stable",
                rollout_state="public_release_review_required",
                supportability_state="review_required",
                fallback_heads=(),
                allow_raw_fail_declaration=False,
            )
            stable_environment[
                routing.CAMPAIGN_OPERABILITY_ENV["legacy_preview_mode"]
            ] = stable_environment.pop(routing.CAMPAIGN_OPERABILITY_ENV["mode"])
            with self.assertRaisesRegex(
                routing.RoutingError, "cannot activate a stable candidate"
            ):
                routing.campaign_operability_candidate_context_from_environment(
                    stable_environment
                )

        with self.assertRaisesRegex(routing.RoutingError, "modes conflict"):
            routing.campaign_operability_candidate_context_from_environment(
                {
                    routing.CAMPAIGN_OPERABILITY_ENV["mode"]: "1",
                    routing.CAMPAIGN_OPERABILITY_ENV["legacy_preview_mode"]: "0",
                }
            )

    def test_public_stable_platforms_are_accepted_for_every_producer(
        self,
    ) -> None:
        stable_targets = (
            ("linux", "linux-x64", "x64"),
            ("macos", "osx-arm64", "arm64"),
            ("windows", "win-x64", "x64"),
        )
        for producer in routing.CAMPAIGN_OPERABILITY_PRODUCER_ENV:
            for platform, rid, arch in stable_targets:
                with (
                    self.subTest(producer=producer, platform=platform),
                    tempfile.TemporaryDirectory() as temporary_directory,
                ):
                    environment, output, release_channel, input_root = (
                        self._candidate_preflight_fixture(
                            Path(temporary_directory),
                            producer,
                            channel="public_stable",
                            release_target="stable",
                            rollout_state="public_release_review_required",
                            supportability_state="review_required",
                            platform=platform,
                            rid=rid,
                            arch=arch,
                            fallback_heads=(),
                            allow_raw_fail_declaration=False,
                        )
                    )

                    context = routing.preflight_campaign_operability_candidate(
                        producer=producer,
                        output_path=output,
                        repo_root=REPO_ROOT,
                        release_channel_path=release_channel,
                        input_root=input_root,
                        environ=environment,
                    )

                    self.assertIsNotNone(context)
                    self.assertEqual(
                        "public_stable", context.channel if context else ""
                    )
                    self.assertEqual(
                        "stable", context.release_target if context else ""
                    )
                    self.assertEqual(platform, context.platform if context else "")
                    self.assertEqual(rid, context.rid if context else "")

    def test_candidate_native_scripts_accept_public_stable_preflight_only(
        self,
    ) -> None:
        scripts = {
            "desktop-visual": REPO_ROOT
            / "scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh",
            "desktop-workflow": REPO_ROOT
            / "scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh",
            "desktop-executable": REPO_ROOT
            / "scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh",
        }
        stable_targets = (
            ("linux", "linux-x64", "x64"),
            ("macos", "osx-arm64", "arm64"),
            ("windows", "win-x64", "x64"),
        )
        for producer, script in scripts.items():
            for platform, rid, arch in stable_targets:
                with (
                    self.subTest(producer=producer, platform=platform),
                    tempfile.TemporaryDirectory() as temporary_directory,
                ):
                    environment, output, _, input_root = (
                        self._candidate_preflight_fixture(
                            Path(temporary_directory),
                            producer,
                            channel="public_stable",
                            release_target="stable",
                            rollout_state="public_release_review_required",
                            supportability_state="review_required",
                            platform=platform,
                            rid=rid,
                            arch=arch,
                            fallback_heads=(),
                            allow_raw_fail_declaration=False,
                        )
                    )
                    if input_root is not None:
                        for spec, path in routing.required_inputs(
                            producer,
                            REPO_ROOT,
                            input_root,
                        ):
                            self._write_json(
                                path,
                                {
                                    "contract_name": (
                                        spec.contract_name
                                        or "fixture.unconstrained"
                                    ),
                                    "status": "pass",
                                },
                            )
                    process_environment = os.environ.copy()
                    for variable in routing.CAMPAIGN_OPERABILITY_ENV.values():
                        process_environment.pop(variable, None)
                    for fields in (
                        routing.CAMPAIGN_OPERABILITY_PRODUCER_ENV.values()
                    ):
                        for variable in fields.values():
                            process_environment.pop(variable, None)
                    process_environment.update(environment)
                    process_environment[
                        "CHUMMER_CANDIDATE_PROOF_ROUTING_PREFLIGHT_ONLY"
                    ] = "1"

                    completed = subprocess.run(
                        ["bash", str(script)],
                        cwd=REPO_ROOT,
                        env=process_environment,
                        capture_output=True,
                        text=True,
                        check=False,
                        timeout=20,
                    )

                    self.assertEqual(0, completed.returncode, completed.stderr)
                    self.assertFalse(output.exists())

    def test_candidate_preflight_accepts_only_exact_registry_manifest_bytes(self) -> None:
        for producer in routing.CAMPAIGN_OPERABILITY_PRODUCER_ENV:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                environment, output, release_channel, input_root = (
                    self._candidate_preflight_fixture(
                        Path(temporary_directory),
                        producer,
                    )
                )

                context = routing.preflight_campaign_operability_candidate(
                    producer=producer,
                    output_path=output,
                    repo_root=REPO_ROOT,
                    release_channel_path=release_channel,
                    input_root=input_root,
                    environ=environment,
                )

                self.assertIsNotNone(context)
                self.assertEqual(
                    hashlib.sha256(release_channel.read_bytes()).hexdigest(),
                    context.manifest_sha256 if context is not None else "",
                )

                release_channel.write_bytes(release_channel.read_bytes() + b" ")
                with self.assertRaisesRegex(
                    routing.RoutingError,
                    "release-channel bytes do not match",
                ):
                    routing.preflight_campaign_operability_candidate(
                        producer=producer,
                        output_path=output,
                        repo_root=REPO_ROOT,
                        release_channel_path=release_channel,
                        input_root=input_root,
                        environ=environment,
                    )

    def test_candidate_preflight_binds_manifest_channel_to_approved_scope(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            environment, output, release_channel, _ = (
                self._candidate_preflight_fixture(
                    Path(temporary_directory),
                    "desktop-visual",
                    channel="public_stable",
                    release_target="stable",
                    rollout_state="public_release_review_required",
                    supportability_state="review_required",
                    fallback_heads=(),
                    allow_raw_fail_declaration=False,
                )
            )
            manifest = json.loads(release_channel.read_text(encoding="utf-8"))
            manifest["channel"] = "preview"
            manifest_raw = (
                json.dumps(manifest, sort_keys=True, separators=(",", ":")) + "\n"
            ).encode("utf-8")
            release_channel.write_bytes(manifest_raw)
            seed_path = Path(
                environment[routing.CAMPAIGN_OPERABILITY_ENV["review_seed_path"]]
            )
            seed = json.loads(seed_path.read_text(encoding="utf-8"))
            seed["manifestSha256"] = hashlib.sha256(manifest_raw).hexdigest()
            seed_raw = (
                json.dumps(seed, sort_keys=True, separators=(",", ":")) + "\n"
            ).encode("utf-8")
            seed_path.write_bytes(seed_raw)
            environment[
                routing.CAMPAIGN_OPERABILITY_ENV["review_seed_sha256"]
            ] = hashlib.sha256(seed_raw).hexdigest()

            with self.assertRaisesRegex(
                routing.RoutingError,
                "does not match the approved release",
            ):
                routing.preflight_campaign_operability_candidate(
                    producer="desktop-visual",
                    output_path=output,
                    repo_root=REPO_ROOT,
                    release_channel_path=release_channel,
                    environ=environment,
                )

    def test_candidate_preflight_rejects_unsafe_output_parent(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            environment, output, release_channel, _ = self._candidate_preflight_fixture(
                root,
                "desktop-visual",
            )
            output.parent.chmod(0o755)

            with self.assertRaisesRegex(routing.RoutingError, "caller-owned"):
                routing.preflight_campaign_operability_candidate(
                    producer="desktop-visual",
                    output_path=output,
                    repo_root=REPO_ROOT,
                    release_channel_path=release_channel,
                    environ=environment,
                )

    def test_candidate_preflight_rejects_intermediate_symlink_components(self) -> None:
        for target_kind in ("scope", "seed", "release", "output"):
            with self.subTest(target_kind=target_kind), tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                environment, output, release_channel, _ = self._candidate_preflight_fixture(
                    root,
                    "desktop-visual",
                )
                if target_kind in {"scope", "seed", "release"}:
                    alias_parent = root / f"alias-{target_kind}"
                    alias_parent.symlink_to(root / "authority", target_is_directory=True)
                    if target_kind == "scope":
                        environment[routing.CAMPAIGN_OPERABILITY_ENV["scope_path"]] = str(
                            alias_parent / "scope.json"
                        )
                    elif target_kind == "seed":
                        environment[
                            routing.CAMPAIGN_OPERABILITY_ENV["review_seed_path"]
                        ] = str(alias_parent / "registry-review-seed.json")
                    else:
                        release_channel = alias_parent / "RELEASE_CHANNEL.json"
                        environment[
                            routing.CAMPAIGN_OPERABILITY_PRODUCER_ENV[
                                "desktop-visual"
                            ]["release_channel"]
                        ] = str(release_channel)
                else:
                    alias_parent = root / "alias-output"
                    alias_parent.symlink_to(output.parent, target_is_directory=True)
                    output = alias_parent / output.name
                    environment[
                        routing.CAMPAIGN_OPERABILITY_PRODUCER_ENV[
                            "desktop-visual"
                        ]["output"]
                    ] = str(output)

                with self.assertRaisesRegex(
                    routing.RoutingError,
                    "symlink or non-directory component",
                ):
                    routing.preflight_campaign_operability_candidate(
                        producer="desktop-visual",
                        output_path=output,
                        repo_root=REPO_ROOT,
                        release_channel_path=release_channel,
                        environ=environment,
                    )

    def test_candidate_context_rejects_stale_scope_and_registry_seed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            stale_scope = self._candidate_scope(release_version="run-20260727-050000")
            with self.assertRaisesRegex(routing.RoutingError, "release version differs"):
                self._candidate_context(root, scope=stale_scope)
            stale_seed = self._registry_seed(release_version="run-20260727-050000")
            with self.assertRaisesRegex(routing.RoutingError, "candidate posture"):
                self._candidate_context(root, seed=stale_seed)

    def test_candidate_context_rejects_cross_postures_and_stable_signing_drift(
        self,
    ) -> None:
        stable_scope = self._candidate_scope(
            channel="public_stable",
            release_target="stable",
            fallback_heads=(),
        )
        stable_seed = self._registry_seed(
            channel="public_stable",
            rollout_state="public_release_review_required",
            supportability_state="review_required",
            fallback_heads=(),
        )
        stable_windows_seed = self._registry_seed(
            channel="public_stable",
            rollout_state="public_release_review_required",
            supportability_state="review_required",
            platform="windows",
            rid="win-x64",
            arch="x64",
            fallback_heads=(),
        )
        stable_linux_seed = self._registry_seed(
            channel="public_stable",
            rollout_state="public_release_review_required",
            supportability_state="review_required",
            platform="linux",
            rid="linux-x64",
            arch="x64",
            fallback_heads=(),
        )
        cases = (
            (
                self._candidate_scope(
                    channel="public_stable",
                    release_target="preview",
                    fallback_heads=(),
                ),
                stable_seed,
                "posture is invalid",
            ),
            (
                self._candidate_scope(
                    channel="preview",
                    release_target="stable",
                    fallback_heads=(),
                ),
                self._registry_seed(fallback_heads=()),
                "posture is invalid",
            ),
            (
                stable_scope,
                self._registry_seed(fallback_heads=()),
                "candidate posture",
            ),
            (
                self._candidate_scope(fallback_heads=()),
                stable_seed,
                "candidate posture",
            ),
            (
                self._candidate_scope(
                    channel="public_stable",
                    release_target="stable",
                    signing_requirement="preview_unsigned_allowed",
                    fallback_heads=(),
                ),
                stable_seed,
                "signing requirement",
            ),
            (
                self._candidate_scope(
                    channel="public_stable",
                    release_target="stable",
                    platform="windows",
                    rid="win-x64",
                    signing_requirement="preview_unsigned_allowed",
                    fallback_heads=(),
                ),
                stable_windows_seed,
                "signing requirement",
            ),
            (
                self._candidate_scope(
                    channel="public_stable",
                    release_target="stable",
                    platform="linux",
                    rid="linux-x64",
                    signing_requirement="preview_unsigned_allowed",
                    fallback_heads=(),
                ),
                stable_linux_seed,
                "signing requirement",
            ),
            (
                self._candidate_scope(
                    platform="linux",
                    rid="linux-x64",
                    signing_requirement="signed",
                    fallback_heads=(),
                ),
                self._registry_seed(
                    platform="linux",
                    rid="linux-x64",
                    arch="x64",
                    fallback_heads=(),
                ),
                "not approved for its release posture",
            ),
            (
                stable_scope,
                stable_windows_seed,
                "platform projection differs",
            ),
        )
        for scope, seed, message in cases:
            with (
                self.subTest(
                    channel=scope["channel"],
                    target=scope["releaseTarget"],
                    seed_channel=seed["channel"],
                    signing=scope["platforms"][0]["signingRequirement"],  # type: ignore[index]
                ),
                tempfile.TemporaryDirectory() as temporary_directory,
            ):
                with self.assertRaisesRegex(routing.RoutingError, message):
                    self._candidate_context(
                        Path(temporary_directory),
                        scope=scope,
                        seed=seed,
                        allow_raw_fail_declaration=False,
                    )

    def test_stable_candidates_reject_raw_fail_declarations(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            stable_scope = self._candidate_scope(
                channel="public_stable",
                release_target="stable",
                fallback_heads=(),
            )
            stable_seed = self._registry_seed(
                channel="public_stable",
                rollout_state="public_release_review_required",
                supportability_state="review_required",
                fallback_heads=(),
            )
            stable_context = self._candidate_context(
                Path(temporary_directory),
                scope=stable_scope,
                seed=stable_seed,
                allow_raw_fail_declaration=False,
            )
            decorated = routing.decorate_campaign_operability_candidate_payload(
                producer="desktop-visual",
                payload={
                    "contract_name": (
                        routing.CAMPAIGN_OPERABILITY_PRODUCER_CONTRACTS[
                            "desktop-visual"
                        ]
                    ),
                    "releaseVersion": stable_context.release_version,
                    "status": "fail",
                    "reasons": ["Stable candidate proof is not ready."],
                },
                context=stable_context,
            )
            self.assertNotIn("campaign_operability_preview", decorated)
            with self.assertRaisesRegex(
                routing.RoutingError, "restricted to preview"
            ):
                routing.decorate_campaign_operability_candidate_payload(
                    producer="desktop-visual",
                    payload={
                        "contract_name": (
                            routing.CAMPAIGN_OPERABILITY_PRODUCER_CONTRACTS[
                                "desktop-visual"
                            ]
                        ),
                        "releaseVersion": stable_context.release_version,
                        "status": "fail",
                        "reasons": ["Stable candidate proof is not ready."],
                    },
                    context=replace(
                        stable_context,
                        allow_raw_fail_declaration=True,
                    ),
                )
            with self.assertRaisesRegex(routing.RoutingError, "restricted to preview"):
                self._candidate_context(
                    Path(temporary_directory),
                    scope=stable_scope,
                    seed=stable_seed,
                    allow_raw_fail_declaration=True,
                )

    def test_candidate_context_rejects_digest_mismatch(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            scope_path = root / "scope.json"
            scope_raw = (
                json.dumps(
                    self._candidate_scope(), sort_keys=True, separators=(",", ":")
                )
                + "\n"
            ).encode("utf-8")
            scope_path.write_bytes(scope_raw)
            seed_path = root / "seed.json"
            seed_raw = (json.dumps(self._registry_seed()) + "\n").encode("utf-8")
            seed_path.write_bytes(seed_raw)
            with self.assertRaisesRegex(routing.RoutingError, "scope.*bytes do not match"):
                routing.load_campaign_operability_candidate_context(
                    approved_scope_path=scope_path,
                    expected_scope_sha256="f" * 64,
                    expected_release_version="run-20260728-050000",
                    registry_review_seed_path=seed_path,
                    expected_registry_review_seed_sha256=hashlib.sha256(
                        seed_raw
                    ).hexdigest(),
                    bounded_owner="chummer-release-operations",
                    next_actions=("Capture stable desktop proof.",),
                    allow_raw_fail_declaration=True,
                )
            with self.assertRaisesRegex(
                routing.RoutingError, "review-seed bytes do not match"
            ):
                routing.load_campaign_operability_candidate_context(
                    approved_scope_path=scope_path,
                    expected_scope_sha256=hashlib.sha256(scope_raw).hexdigest(),
                    expected_release_version="run-20260728-050000",
                    registry_review_seed_path=seed_path,
                    expected_registry_review_seed_sha256="f" * 64,
                    bounded_owner="chummer-release-operations",
                    next_actions=("Capture stable desktop proof.",),
                    allow_raw_fail_declaration=True,
                )

    def test_candidate_context_rejects_invalid_registry_decision_digest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            seed = self._registry_seed()
            seed["releaseDecisionSha256"] = "not-a-sha256"

            with self.assertRaisesRegex(
                routing.RoutingError, "releaseDecisionSha256 is invalid"
            ):
                self._candidate_context(Path(temporary_directory), seed=seed)

    def test_candidate_context_rejects_non_registry_artifact_routes(self) -> None:
        cases = (
            (
                "downloadUrl",
                "https://chummer.run/downloads/g/generation-1/files/"
                "chummer-avalonia-macos-arm64.dmg",
            ),
            ("publicInstallRoute", "/downloads/macos"),
        )
        for field, value in cases:
            with self.subTest(field=field), tempfile.TemporaryDirectory() as temporary_directory:
                seed = self._registry_seed()
                seed["artifacts"][0][field] = value  # type: ignore[index]
                with self.assertRaisesRegex(
                    routing.RoutingError,
                    "root-relative route|Registry route schema",
                ):
                    self._candidate_context(Path(temporary_directory), seed=seed)

    def test_candidate_context_rejects_owner_and_action_mismatch(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            with self.assertRaisesRegex(routing.RoutingError, "bounded owner differs"):
                self._candidate_context(root, bounded_owner="other-release-owner")
            seed = self._registry_seed(owner="other-release-owner")
            with self.assertRaisesRegex(
                routing.RoutingError, "review-seed support owner differs"
            ):
                self._candidate_context(root, seed=seed)
            with self.assertRaisesRegex(routing.RoutingError, "next actions"):
                self._candidate_context(root, next_actions=("todo",))
            with self.assertRaisesRegex(routing.RoutingError, "next actions"):
                self._candidate_context(
                    root,
                    next_actions=("Capture proof.", "Capture proof."),
                )

    def test_candidate_context_rejects_alias_conflicts_and_case_shadows(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            context = self._candidate_context(root)
            with self.assertRaisesRegex(routing.RoutingError, "exact candidate release"):
                routing.decorate_campaign_operability_candidate_payload(
                    producer="desktop-workflow",
                    payload={
                        "contract_name": routing.CAMPAIGN_OPERABILITY_PRODUCER_CONTRACTS[
                            "desktop-workflow"
                        ],
                        "releaseVersion": context.release_version,
                        "release_version": "run-other",
                        "status": "pass",
                    },
                    context=context,
                )

            scope_path = root / "shadowed-scope.json"
            canonical = json.dumps(
                self._candidate_scope(), sort_keys=True, separators=(",", ":")
            )
            shadowed = canonical[:-1] + ',"ReleaseVersion":"run-20260728-050000"}\n'
            scope_path.write_text(shadowed, encoding="utf-8")
            seed_path = root / "seed.json"
            seed_raw = (json.dumps(self._registry_seed()) + "\n").encode("utf-8")
            seed_path.write_bytes(seed_raw)
            with self.assertRaisesRegex(
                routing.RoutingError, "duplicate or case-shadowed"
            ):
                routing.load_campaign_operability_candidate_context(
                    approved_scope_path=scope_path,
                    expected_scope_sha256=hashlib.sha256(
                        shadowed.encode("utf-8")
                    ).hexdigest(),
                    expected_release_version="run-20260728-050000",
                    registry_review_seed_path=seed_path,
                    expected_registry_review_seed_sha256=hashlib.sha256(
                        seed_raw
                    ).hexdigest(),
                    bounded_owner="chummer-release-operations",
                    next_actions=("Capture stable desktop proof.",),
                    allow_raw_fail_declaration=True,
                )

    def test_candidate_context_accepts_exact_windows_win_x64_preview_scope(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            scope = self._candidate_scope(
                platform="windows",
                rid="win-x64",
                signing_requirement="preview_unsigned_allowed",
                fallback_heads=(),
            )
            seed = self._registry_seed(
                platform="windows",
                rid="win-x64",
                arch="x64",
                fallback_heads=(),
            )

            context = self._candidate_context(root, scope=scope, seed=seed)

            self.assertEqual("windows", context.platform)
            self.assertEqual("win-x64", context.rid)
            self.assertEqual("avalonia", context.primary_head)
            self.assertEqual(("avalonia",), context.required_heads)

    def test_candidate_context_rejects_unapproved_or_mismatched_windows_scope(
        self,
    ) -> None:
        cases = (
            ("rid", "linux-x64", "platform/RID is not approved"),
            ("signingRequirement", "signed", "signing requirement"),
        )
        for field, value, message in cases:
            with self.subTest(field=field), tempfile.TemporaryDirectory() as temporary_directory:
                scope = self._candidate_scope(
                    platform="windows",
                    rid="win-x64",
                    signing_requirement="preview_unsigned_allowed",
                    fallback_heads=(),
                )
                scope["platforms"][0][field] = value  # type: ignore[index]
                seed = self._registry_seed(
                    platform="windows",
                    rid="win-x64",
                    arch="x64",
                    fallback_heads=(),
                )
                with self.assertRaisesRegex(routing.RoutingError, message):
                    self._candidate_context(
                        Path(temporary_directory),
                        scope=scope,
                        seed=seed,
                    )

    def test_candidate_context_rejects_windows_registry_tuple_drift(self) -> None:
        scope = self._candidate_scope(
            platform="windows",
            rid="win-x64",
            signing_requirement="preview_unsigned_allowed",
            fallback_heads=(),
        )
        cases = (
            ("availablePlatforms", ["macos"], "platform projection differs"),
            (
                "primaryHeadByPlatform",
                {"windows": "blazor-desktop"},
                "platform projection differs",
            ),
            ("artifactRid", "win-arm64", "outside the approved candidate scope"),
            ("artifactArch", "arm64", "outside the approved candidate scope"),
        )
        for field, value, message in cases:
            with self.subTest(field=field), tempfile.TemporaryDirectory() as temporary_directory:
                seed = self._registry_seed(
                    platform="windows",
                    rid="win-x64",
                    arch="x64",
                    fallback_heads=(),
                )
                if field == "artifactRid":
                    seed["artifacts"][0]["rid"] = value  # type: ignore[index]
                elif field == "artifactArch":
                    seed["artifacts"][0]["arch"] = value  # type: ignore[index]
                else:
                    seed[field] = value
                with self.assertRaisesRegex(routing.RoutingError, message):
                    self._candidate_context(
                        Path(temporary_directory),
                        scope=scope,
                        seed=seed,
                    )

    def test_candidate_native_output_cannot_replace_tracked_public_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            environment, _, release_channel, _ = self._candidate_preflight_fixture(
                Path(temporary_directory),
                "desktop-visual",
            )
            public_output = (
                REPO_ROOT
                / ".codex-studio"
                / "published"
                / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
            )
            environment[
                routing.CAMPAIGN_OPERABILITY_PRODUCER_ENV["desktop-visual"][
                    "output"
                ]
            ] = str(public_output)

            with self.assertRaisesRegex(
                routing.RoutingError, "outside the tracked public evidence root"
            ):
                routing.preflight_campaign_operability_candidate(
                    producer="desktop-visual",
                    output_path=public_output,
                    repo_root=REPO_ROOT,
                    release_channel_path=release_channel,
                    environ=environment,
                )

    def test_candidate_scripts_fail_before_tracked_output_mutation_on_incomplete_plane(
        self,
    ) -> None:
        scripts = {
            "desktop-visual": (
                REPO_ROOT
                / "scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh",
                REPO_ROOT
                / ".codex-studio/published/DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json",
            ),
            "desktop-executable": (
                REPO_ROOT
                / "scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh",
                REPO_ROOT
                / ".codex-studio/published/DESKTOP_EXECUTABLE_EXIT_GATE.generated.json",
            ),
            "desktop-workflow": (
                REPO_ROOT
                / "scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh",
                REPO_ROOT
                / ".codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json",
            ),
        }
        for producer, (script, tracked_output) in scripts.items():
            with self.subTest(producer=producer):
                before = (
                    tracked_output.read_bytes(),
                    tracked_output.stat().st_mtime_ns,
                ) if tracked_output.is_file() else None
                environment = os.environ.copy()
                for variable in routing.CAMPAIGN_OPERABILITY_ENV.values():
                    environment.pop(variable, None)
                for variable in routing.CAMPAIGN_OPERABILITY_PRODUCER_ENV[
                    producer
                ].values():
                    environment.pop(variable, None)
                environment[routing.CAMPAIGN_OPERABILITY_ENV["mode"]] = "1"
                environment["CHUMMER_CANDIDATE_PROOF_ROUTING_PREFLIGHT_ONLY"] = "1"

                completed = subprocess.run(
                    ["bash", str(script)],
                    cwd=REPO_ROOT,
                    env=environment,
                    capture_output=True,
                    text=True,
                    check=False,
                    timeout=20,
                )

                self.assertEqual(65, completed.returncode, completed.stderr)
                self.assertIn("complete candidate plane", completed.stderr)
                after = (
                    tracked_output.read_bytes(),
                    tracked_output.stat().st_mtime_ns,
                ) if tracked_output.is_file() else None
                self.assertEqual(before, after)

    def test_candidate_scripts_preflight_before_any_output_or_refresh(self) -> None:
        scripts = {
            "desktop-visual": REPO_ROOT
            / "scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh",
            "desktop-executable": REPO_ROOT
            / "scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh",
            "desktop-workflow": REPO_ROOT
            / "scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh",
        }
        for producer, script in scripts.items():
            with self.subTest(producer=producer):
                text = script.read_text(encoding="utf-8")
                self.assertLess(text.index("campaign-preflight"), text.index("mkdir -p"))
        visual = scripts["desktop-visual"].read_text(encoding="utf-8")
        self.assertIn("refresh_screenshot_pack_when_stale=0", visual)
        self.assertIn("candidate mode requires existing passing prerequisite receipts", visual)
        executable = scripts["desktop-executable"].read_text(encoding="utf-8")
        self.assertIn("skip_dependency_materialize=1", executable)
        self.assertIn("skip_release_gate_lock_wait=1", executable)

    def test_visual_and_executable_candidate_preflight_only_is_side_effect_free(
        self,
    ) -> None:
        scripts = {
            "desktop-visual": REPO_ROOT
            / "scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh",
            "desktop-executable": REPO_ROOT
            / "scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh",
        }
        for producer, script in scripts.items():
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                environment, output, _, _ = self._candidate_preflight_fixture(
                    Path(temporary_directory),
                    producer,
                )
                process_environment = os.environ.copy()
                for variable in routing.CAMPAIGN_OPERABILITY_ENV.values():
                    process_environment.pop(variable, None)
                for producer_fields in routing.CAMPAIGN_OPERABILITY_PRODUCER_ENV.values():
                    for variable in producer_fields.values():
                        process_environment.pop(variable, None)
                process_environment.update(environment)
                process_environment[
                    "CHUMMER_CANDIDATE_PROOF_ROUTING_PREFLIGHT_ONLY"
                ] = "1"

                completed = subprocess.run(
                    ["bash", str(script)],
                    cwd=REPO_ROOT,
                    env=process_environment,
                    capture_output=True,
                    text=True,
                    check=False,
                    timeout=20,
                )

                self.assertEqual(0, completed.returncode, completed.stderr)
                self.assertFalse(output.exists())

    def test_every_producer_accepts_a_complete_plane_without_writing(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                environment, output_path, _, _, _ = self._fixture(
                    Path(temporary_directory), producer
                )

                completed = self._run(producer, environment)

                self.assertEqual(0, completed.returncode, completed.stderr)
                self.assertFalse(output_path.exists())
                sidecar_variable = PRODUCERS[producer].get("sidecar")
                if sidecar_variable:
                    self.assertFalse(Path(environment[str(sidecar_variable)]).exists())

    def test_every_producer_rejects_a_partial_plane(self) -> None:
        for producer, configuration in PRODUCERS.items():
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                output_path = Path(temporary_directory) / "must-not-exist.json"
                environment = {str(configuration["output"]): str(output_path)}

                completed = self._run(producer, environment)

                self.assertEqual(64, completed.returncode, completed.stderr)
                self.assertIn("external plane requires non-blank", completed.stderr)
                self.assertFalse(output_path.exists())

    def test_missing_explicit_input_never_falls_back_to_tracked_defaults(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                environment, output_path, release_channel, _, required = self._fixture(
                    Path(temporary_directory), producer
                )
                missing_path = required[0][1] if required else release_channel
                missing_path.unlink()

                completed = self._run(producer, environment)

                self.assertEqual(65, completed.returncode, completed.stderr)
                self.assertIn(str(missing_path), completed.stderr)
                self.assertFalse(output_path.exists())

    def test_supplied_inputs_are_contract_and_status_validated(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                environment, output_path, release_channel, _, required = self._fixture(
                    Path(temporary_directory), producer
                )
                if required:
                    receipt_spec, receipt_path = required[0]
                    self._write_json(
                        receipt_path,
                        {
                            "contract_name": receipt_spec.contract_name or "fixture.unconstrained",
                            "status": "failed",
                        },
                    )
                    completed = self._run(producer, environment)
                    self.assertEqual(65, completed.returncode, completed.stderr)
                    self.assertIn("must be pass/passed/ready", completed.stderr)
                    self.assertFalse(output_path.exists())

                    if receipt_spec.contract_name:
                        self._write_json(
                            receipt_path,
                            {"contract_name": "wrong.contract", "status": "pass"},
                        )
                        completed = self._run(producer, environment)
                        self.assertEqual(65, completed.returncode, completed.stderr)
                        self.assertIn("proof input contract must be", completed.stderr)
                else:
                    self._write_json(
                        release_channel,
                        {
                            "contract_name": routing.RELEASE_CHANNEL_CONTRACT,
                            "status": routing.RELEASE_CHANNEL_STATUS,
                            "channelId": "candidate-fixture",
                            "publishedAt": "2026-07-18T13:14:45Z",
                        },
                    )
                    completed = self._run(producer, environment)
                    self.assertEqual(65, completed.returncode, completed.stderr)
                    self.assertIn("version/releaseVersion", completed.stderr)

    def test_release_channels_require_expected_contract_and_published_status(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                environment, output_path, release_channel, _, _ = self._fixture(
                    Path(temporary_directory), producer
                )
                release_payload = json.loads(release_channel.read_text(encoding="utf-8"))
                release_payload["contract_name"] = "wrong.contract"
                self._write_json(release_channel, release_payload)

                completed = self._run(producer, environment)

                self.assertEqual(65, completed.returncode, completed.stderr)
                self.assertIn("release channel input contract must be", completed.stderr)
                self.assertFalse(output_path.exists())

                release_payload["contract_name"] = routing.RELEASE_CHANNEL_CONTRACT
                release_payload["status"] = "revoked"
                self._write_json(release_channel, release_payload)
                completed = self._run(producer, environment)
                self.assertEqual(65, completed.returncode, completed.stderr)
                self.assertIn("release channel input status must be published", completed.stderr)
                self.assertFalse(output_path.exists())

    def test_explicit_input_roots_cannot_escape_through_symlinks(self) -> None:
        for producer, configuration in PRODUCERS.items():
            if "input" not in configuration:
                continue
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                environment, output_path, _, input_root, required = self._fixture(root, producer)
                nested = next(
                    (
                        receipt_path
                        for _, receipt_path in required
                        if len(receipt_path.relative_to(input_root).parts) > 1
                    ),
                    None,
                )
                if nested is not None:
                    relative_path = nested.relative_to(input_root)
                    routed_directory = input_root / relative_path.parts[0]
                    outside_directory = root / "tracked-stale-defaults"
                    routed_directory.rename(outside_directory)
                    routed_directory.symlink_to(outside_directory, target_is_directory=True)
                    protected_path = outside_directory.joinpath(*relative_path.parts[1:])
                else:
                    routed_input = required[0][1]
                    protected_path = root / "tracked-stale-default.json"
                    routed_input.rename(protected_path)
                    routed_input.symlink_to(protected_path)
                protected_bytes = protected_path.read_bytes()

                completed = self._run(producer, environment)

                self.assertEqual(65, completed.returncode, completed.stderr)
                self.assertRegex(completed.stderr, r"symlink|symbolic link")
                self.assertFalse(output_path.exists())
                self.assertEqual(protected_bytes, protected_path.read_bytes())

    def test_outputs_and_sidecars_must_be_disjoint_from_explicit_input_roots(self) -> None:
        for producer, configuration in PRODUCERS.items():
            if "input" not in configuration:
                continue
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                environment, _, _, input_root, required = self._fixture(root, producer)
                protected_bytes = required[0][1].read_bytes()
                nested_output = input_root / "new-candidate-output.generated.json"
                environment[str(configuration["output"])] = str(nested_output)

                completed = self._run(producer, environment)

                self.assertEqual(65, completed.returncode, completed.stderr)
                self.assertIn("output and explicit input root must not overlap", completed.stderr)
                self.assertFalse(nested_output.exists())
                self.assertEqual(protected_bytes, required[0][1].read_bytes())

        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            environment, output_path, _, input_root, required = self._fixture(root, "b14")
            nested_sidecar = input_root / "new-candidate-sidecar"
            environment[str(PRODUCERS["b14"]["sidecar"])] = str(nested_sidecar)
            protected_bytes = required[0][1].read_bytes()

            completed = self._run("b14", environment)

            self.assertEqual(65, completed.returncode, completed.stderr)
            self.assertIn("sidecar output and explicit input root must not overlap", completed.stderr)
            self.assertFalse(output_path.exists())
            self.assertFalse(nested_sidecar.exists())
            self.assertEqual(protected_bytes, required[0][1].read_bytes())

    def test_hard_link_output_aliases_are_rejected_without_clobbering_inputs(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                environment, output_path, release_channel, _, _ = self._fixture(
                    Path(temporary_directory), producer
                )
                output_path.parent.mkdir(parents=True)
                expected_release = release_channel.read_bytes()
                os.link(release_channel, output_path)

                completed = self._run(producer, environment)

                self.assertEqual(65, completed.returncode, completed.stderr)
                self.assertIn("must not alias proof input", completed.stderr)
                self.assertTrue(output_path.samefile(release_channel))
                self.assertEqual(expected_release, release_channel.read_bytes())
                self.assertEqual(expected_release, output_path.read_bytes())

    def test_symlink_and_resolved_output_aliases_are_rejected(self) -> None:
        for producer in PRODUCERS:
            for alias_kind in ("symlink", "resolved-parent"):
                with (
                    self.subTest(producer=producer, alias_kind=alias_kind),
                    tempfile.TemporaryDirectory() as temporary_directory,
                ):
                    root = Path(temporary_directory)
                    environment, output_path, release_channel, _, _ = self._fixture(root, producer)
                    expected_release = release_channel.read_bytes()
                    if alias_kind == "symlink":
                        output_path.parent.mkdir(parents=True)
                        output_path.symlink_to(release_channel)
                    else:
                        alias_parent = root / "resolved-release-parent"
                        alias_parent.symlink_to(release_channel.parent, target_is_directory=True)
                        output_path = alias_parent / release_channel.name
                        environment[str(PRODUCERS[producer]["output"])] = str(output_path)

                    completed = self._run(producer, environment)

                    self.assertEqual(65, completed.returncode, completed.stderr)
                    self.assertEqual(expected_release, release_channel.read_bytes())

    def test_atomic_writes_validate_output_contracts_for_every_producer(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                _, output_path, release_channel, input_root, _ = self._fixture(
                    Path(temporary_directory), producer
                )
                routed_input_root = input_root if "input" in PRODUCERS[producer] else None
                payload = {
                    "contract_name": routing.OUTPUT_CONTRACTS[producer],
                    "status": "pass",
                }

                routing.atomic_write_json(
                    producer=producer,
                    output_path=output_path,
                    payload=payload,
                    repo_root=REPO_ROOT,
                    release_channel_path=release_channel,
                    input_root=routed_input_root,
                )

                self.assertEqual(payload, json.loads(output_path.read_text(encoding="utf-8")))
                output_path.unlink()
                for invalid_payload in (
                    {"contract_name": "wrong.contract", "status": "pass"},
                    {"contract_name": routing.OUTPUT_CONTRACTS[producer], "status": "unknown"},
                ):
                    with self.assertRaises(routing.RoutingError):
                        routing.atomic_write_json(
                            producer=producer,
                            output_path=output_path,
                            payload=invalid_payload,
                            repo_root=REPO_ROOT,
                            release_channel_path=release_channel,
                            input_root=routed_input_root,
                        )
                    self.assertFalse(output_path.exists())

    def test_final_prewrite_revalidation_catches_a_late_hard_link_alias(self) -> None:
        for producer in PRODUCERS:
            with self.subTest(producer=producer), tempfile.TemporaryDirectory() as temporary_directory:
                _, output_path, release_channel, input_root, _ = self._fixture(
                    Path(temporary_directory), producer
                )
                routed_input_root = input_root if "input" in PRODUCERS[producer] else None
                expected_release = release_channel.read_bytes()
                original_preflight = routing.preflight_external_plane
                call_count = 0

                def racing_preflight(**kwargs: object) -> list[Path]:
                    nonlocal call_count
                    call_count += 1
                    if call_count == 2:
                        output_path.parent.mkdir(parents=True, exist_ok=True)
                        os.link(release_channel, output_path)
                    return original_preflight(**kwargs)

                with mock.patch.object(
                    routing, "preflight_external_plane", side_effect=racing_preflight
                ):
                    with self.assertRaises(routing.RoutingError):
                        routing.atomic_write_json(
                            producer=producer,
                            output_path=output_path,
                            payload={
                                "contract_name": routing.OUTPUT_CONTRACTS[producer],
                                "status": "pass",
                            },
                            repo_root=REPO_ROOT,
                            release_channel_path=release_channel,
                            input_root=routed_input_root,
                        )

                self.assertEqual(2, call_count)
                self.assertTrue(output_path.samefile(release_channel))
                self.assertEqual(expected_release, release_channel.read_bytes())

    def test_b14_sidecar_replacement_is_atomic_and_cannot_replace_input_root(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            _, output_path, release_channel, input_root, required = self._fixture(root, "b14")
            source = root / "staged-screenshots"
            source.mkdir()
            (source / "new.png").write_bytes(b"new-proof")
            sidecar = root / "candidate-output" / "screenshots"
            sidecar.mkdir(parents=True)
            (sidecar / "old.png").write_bytes(b"old-proof")

            routing.atomic_replace_directory(
                producer="b14",
                source=source,
                output_path=sidecar,
                repo_root=REPO_ROOT,
                release_channel_path=release_channel,
                input_root=input_root,
            )

            self.assertEqual(b"new-proof", (sidecar / "new.png").read_bytes())
            self.assertFalse((sidecar / "old.png").exists())
            protected_bytes = required[0][1].read_bytes()
            with self.assertRaises(routing.RoutingError):
                routing.atomic_replace_directory(
                    producer="b14",
                    source=source,
                    output_path=input_root,
                    repo_root=REPO_ROOT,
                    release_channel_path=release_channel,
                    input_root=input_root,
                )
            self.assertEqual(protected_bytes, required[0][1].read_bytes())
            self.assertFalse(output_path.exists())

    def test_integrations_retain_defaults_and_use_shared_atomic_router(self) -> None:
        for producer, configuration in PRODUCERS.items():
            with self.subTest(producer=producer):
                script_text = Path(configuration["script"]).read_text(encoding="utf-8")
                self.assertIn(str(configuration["default_output"]), script_text)
                self.assertIn("candidate_proof_preflight", script_text)
                self.assertIn("atomic_write_json", script_text)
                for variable in configuration["plane"]:
                    self.assertIn(str(variable), script_text)
        b14_text = Path(PRODUCERS["b14"]["script"]).read_text(encoding="utf-8")
        self.assertIn("replace-directory", b14_text)

        candidate_native_scripts = {
            "desktop-visual": REPO_ROOT
            / "scripts"
            / "ai"
            / "milestones"
            / "materialize-desktop-visual-familiarity-exit-gate.sh",
            "desktop-workflow": PRODUCERS["desktop-workflow"]["script"],
            "desktop-executable": REPO_ROOT
            / "scripts"
            / "ai"
            / "milestones"
            / "materialize-desktop-executable-exit-gate.sh",
        }
        for producer, script in candidate_native_scripts.items():
            with self.subTest(candidate_native_producer=producer):
                script_text = Path(script).read_text(encoding="utf-8")
                self.assertIn("decorate_campaign_operability_from_environment", script_text)
                self.assertIn(f'producer="{producer}"', script_text)
                self.assertIn(
                    "CHUMMER_CAMPAIGN_OPERABILITY_CANDIDATE_MODE",
                    script_text,
                )
                self.assertIn(
                    "CHUMMER_CAMPAIGN_OPERABILITY_PREVIEW_MODE",
                    script_text,
                )
        visual_text = candidate_native_scripts["desktop-visual"].read_text(
            encoding="utf-8"
        )
        executable_text = candidate_native_scripts["desktop-executable"].read_text(
            encoding="utf-8"
        )
        self.assertIn("CHUMMER_DESKTOP_VISUAL_OUTPUT_PATH", visual_text)
        self.assertIn("CHUMMER_DESKTOP_EXECUTABLE_GATE_PATH", executable_text)
        self.assertIn("atomic_write_json", visual_text)
        self.assertIn("campaign_operability_candidate_mode_enabled", visual_text)
        self.assertIn('producer="desktop-visual"', visual_text)
        self.assertIn("atomic_write_json", executable_text)
        self.assertIn("campaign_operability_candidate_mode_enabled", executable_text)
        self.assertIn('producer="desktop-executable"', executable_text)


if __name__ == "__main__":
    unittest.main()
