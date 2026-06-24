#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

python3 "$repo_root/scripts/materialize-blazor-workbench-rules-data-staged-proof.py"
