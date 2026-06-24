#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

python3 "$repo_root/scripts/materialize-blazor-career-support-staged-proof.py"
python3 "$repo_root/scripts/materialize-blazor-identity-license-staged-proof.py"
python3 "$repo_root/scripts/materialize-blazor-combat-support-staged-proof.py"
python3 "$repo_root/scripts/materialize-blazor-skill-maintenance-staged-proof.py"
python3 "$repo_root/scripts/materialize-blazor-magic-support-staged-proof.py"
python3 "$repo_root/scripts/materialize-blazor-gear-maintenance-staged-proof.py"
python3 "$repo_root/scripts/materialize-blazor-source-gear-utility-staged-proof.py"
python3 "$repo_root/scripts/materialize-blazor-magic-cleanup-staged-proof.py"
python3 "$repo_root/scripts/materialize-blazor-browser-output-handoff-staged-proof.py"
python3 "$repo_root/scripts/materialize-blazor-workbench-portal-handoff-staged-proof.py"
python3 "$repo_root/scripts/materialize-blazor-legacy-control-coverage-staged-proof.py"
python3 "$repo_root/scripts/materialize-blazor-source-staged-proof-set.py"
