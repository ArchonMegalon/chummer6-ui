#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

python3 "$repo_root/scripts/materialize-blazor-workbench-history-undo-staged-proof.py"
