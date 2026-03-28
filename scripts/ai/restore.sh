#!/usr/bin/env bash
set -euo pipefail
"$(dirname "$0")/with-package-plane.sh" restore "$@"
