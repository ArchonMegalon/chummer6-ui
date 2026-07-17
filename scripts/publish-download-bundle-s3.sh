#!/bin/bash -p
set -euo pipefail

# Object-storage publication used to update fixed artifact and manifest keys in
# place. S3/R2 offers no atomic multi-object cutover for that layout: an upload,
# delete, or second-target failure can leave the old manifest pointing at
# changed bytes. High-level `aws s3 sync` also cannot prove that every remote
# object is byte-for-byte identical to the governed local candidate.
#
# Keep this entry point as an explicit fail-closed boundary until the serving
# topology supports an immutable release namespace and one atomic pointer
# cutover. Do not add an override: re-enabling publication requires the portal,
# release contracts, remote verifier, and failure tests to migrate together.

assert_legacy_release_shelf_target() {
  local target_uri="${1:-object-storage target}"
  printf 'Legacy object-storage writer cannot safely probe %s or %s at %s.\n' \
    '.release-shelf-layout-v1' 'current.json' "$target_uri" >&2
  return 78
}

printf '%s\n' \
  'Object-storage release publication is disabled fail-closed.' \
  '' \
  'The fixed-key S3/R2 downloads topology cannot currently guarantee both:' \
  '  - exact remote SHA-256 and size verification for every governed object; and' \
  '  - preservation of the previously valid shelf after any multi-object failure.' \
  '' \
  'Publish through one of the supported transactional lanes instead:' \
  '  - authenticated HTTP upload sessions: scripts/publish-download-bundle-http.sh' \
  '  - controlled filesystem promotion: scripts/publish-download-bundle.sh' \
  '' \
  'Re-enabling this S3 entry point requires a coordinated storage migration with:' \
  '  1. immutable, versioned artifact and proof object keys;' \
  '  2. forced per-object writes plus checksum-and-size verified remote inventory;' \
  '  3. one atomic canonical pointer cutover understood by the serving portal;' \
  '  4. rollback/old-shelf-validity tests for failure at every write phase; and' \
  '  5. isolated governed provenance validation for the exact immutable candidate.' \
  '' \
  'No resolver, generator, validator, local mirror, or AWS command was invoked.' \
  >&2

# EX_CONFIG: the requested publication topology is intentionally unsupported.
assert_legacy_release_shelf_target "${CHUMMER_PORTAL_DOWNLOADS_S3_URI:-object-storage target}"
exit 78
