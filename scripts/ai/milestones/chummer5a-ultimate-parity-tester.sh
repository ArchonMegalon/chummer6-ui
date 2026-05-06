#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

fixtures_root="${CHUMMER5A_FIXTURES_ROOT:-/docker/chummer5a/Chummer.Tests/TestFiles}"
receipts_dir="${CHUMMER_FIXTURE_UI_RECONSTRUCTION_RECEIPTS_DIR:-$repo_root/.codex-studio/published/chummer5a-fixture-ui-reconstruction}"
artifacts_dir="${CHUMMER5A_ULTIMATE_PARITY_ARTIFACTS_DIR:-$repo_root/.codex-studio/out/chummer5a-ultimate-parity-tester/live}"
published_receipt="${CHUMMER5A_ULTIMATE_PARITY_PUBLISHED_RECEIPT:-$repo_root/.codex-studio/published/CHUMMER5A_ULTIMATE_PARITY_TESTER.generated.json}"

mkdir -p "$receipts_dir" "$artifacts_dir" "$(dirname "$published_receipt")"

if [[ ! -d "$fixtures_root" ]]; then
  echo "missing fixture root: $fixtures_root" >&2
  exit 2
fi

export CHUMMER_FIXTURE_UI_RECONSTRUCTION_SCOPE="${CHUMMER_FIXTURE_UI_RECONSTRUCTION_SCOPE:-all}"
export CHUMMER_FIXTURE_UI_RECONSTRUCTION_FIXTURES_ROOT="$fixtures_root"
export CHUMMER_FIXTURE_UI_RECONSTRUCTION_RECEIPTS_DIR="$receipts_dir"

bash "$repo_root/scripts/ai/milestones/chummer5a-fixture-ui-reconstruction.sh" >/dev/null

mapfile -d '' fixtures < <(find "$fixtures_root" -maxdepth 1 -name '*.chum5' -print0 | sort -z)
if [[ "${#fixtures[@]}" -eq 0 ]]; then
  echo "no .chum5 fixtures found under $fixtures_root" >&2
  exit 2
fi

fixture_args=()
for fixture_path in "${fixtures[@]}"; do
  fixture_args+=(--fixture "$fixture_path")
done

parity_exit=0
if python3 "$repo_root/scripts/chummer5a_parity_tester.py" \
  "${fixture_args[@]}" \
  --reconstruction-receipts-dir "$receipts_dir" \
  --artifacts "$artifacts_dir" >/dev/null; then
  parity_exit=0
else
  parity_exit=$?
fi

strict_exit="$(
ULTIMATE_FIXTURE_COUNT="${#fixtures[@]}" \
ULTIMATE_PARITY_RUN_METADATA="$artifacts_dir/run-metadata.json" \
ULTIMATE_PARITY_PUBLISHED_RECEIPT="$published_receipt" \
ULTIMATE_PARITY_BASE_EXIT="$parity_exit" \
python3 - <<'PY'
import json
import os
from pathlib import Path

run_metadata_path = Path(os.environ["ULTIMATE_PARITY_RUN_METADATA"])
published_receipt_path = Path(os.environ["ULTIMATE_PARITY_PUBLISHED_RECEIPT"])
fixture_count = int(os.environ["ULTIMATE_FIXTURE_COUNT"])
base_exit = int(os.environ["ULTIMATE_PARITY_BASE_EXIT"])

if not run_metadata_path.is_file():
    payload = {
        "contract_name": "chummer6-ui.chummer5a_ultimate_parity_tester",
        "status": "fail",
        "summary": "Ultimate parity gate failed before run metadata was written.",
        "fixtureCount": fixture_count,
        "reasons": [f"Missing run metadata: {run_metadata_path}"],
    }
    strict_pass = False
else:
    with run_metadata_path.open("r", encoding="utf-8") as handle:
        metadata = json.load(handle)
    proof_scope = metadata.get("proofScope") or {}
    strict_failure_reasons = []
    if base_exit != 0:
        strict_failure_reasons.append(f"Base parity tester exited with code {base_exit}.")
    if proof_scope.get("fixtureScope") != "all_available_fixtures_explicit":
        strict_failure_reasons.append("Fixture scope is not the full available corpus.")
    if int(proof_scope.get("selectedFixtureCount") or 0) != fixture_count:
        strict_failure_reasons.append("Selected fixture count does not match the available fixture count.")
    if int(proof_scope.get("availableFixtureCount") or 0) != fixture_count:
        strict_failure_reasons.append("Available fixture count drifted from the enumerated fixture corpus.")
    for key in (
        "uiReconstructionExecuted",
        "certifiesSelectedFixturesCanBeRebuiltOnlyUsingUi",
        "certifiesEveryFixtureCanBeRebuiltOnlyUsingUi",
        "perFixtureOutputRoutesExecuted",
        "perFixturePdfArtifactsProduced",
        "recursiveSettingsAndElementsCertified",
    ):
        if proof_scope.get(key) is not True:
            strict_failure_reasons.append(f"proofScope.{key} is not true.")
    strict_pass = not strict_failure_reasons
    status = "pass" if strict_pass else "fail"
    payload = {
        "generatedAt": metadata.get("completedAt") or metadata.get("startedAt"),
        "contract_name": "chummer6-ui.chummer5a_ultimate_parity_tester",
        "status": status,
        "summary": (
            f"Ultimate parity gate passed across all {fixture_count} available Chummer5a fixtures with UI-driven reconstruction and recursive proof."
            if strict_pass
            else "Ultimate parity gate failed because at least one full-parity proof lane is still missing."
        ),
        "fixtureCount": fixture_count,
        "proofScope": proof_scope,
        "proofClaims": metadata.get("proofClaims") or [],
        "proofLimitations": metadata.get("proofLimitations") or [],
        "fixtureReconstructionReview": metadata.get("fixtureReconstructionReview") or {},
        "recursiveParityReceiptReview": metadata.get("recursiveParityReceiptReview") or {},
        "strictFailureReasons": strict_failure_reasons,
        "sourceRunMetadata": str(run_metadata_path),
    }

published_receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
print(0 if strict_pass else 1)
PY
)"

if [[ "$strict_exit" -ne 0 ]]; then
  parity_exit="$strict_exit"
fi

if [[ "$parity_exit" -eq 0 ]]; then
  echo "[chummer5a-ultimate-parity-tester] PASS"
else
  echo "[chummer5a-ultimate-parity-tester] FAIL" >&2
fi

exit "$parity_exit"
