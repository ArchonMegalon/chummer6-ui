#!/usr/bin/env python3
"""Check the explicit rule-data input used by strict Product.UnitTests.

This emits no release receipt and does not download or compile Core source.
The checkout must already contain the pinned recipe and runtime source objects.
"""

import argparse
import json
import os
import sys
from pathlib import Path

from verify_fresh_checkout_package_plane import (
    EXPECTED_CORE_RUNTIME_FEED_METADATA,
    VerificationError,
    core_projection_content_inventory,
)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--core-root", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = core_projection_content_inventory(
            args.core_root, EXPECTED_CORE_RUNTIME_FEED_METADATA, dict(os.environ))
    except (VerificationError, OSError, ValueError) as exc:
        print(f"Core projection content rejected: {exc}", file=sys.stderr)
        return 2
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
