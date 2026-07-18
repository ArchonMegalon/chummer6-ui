# Verification modes

`scripts/ai/verify.sh` always runs in one explicit mode and writes
`UI_VERIFY_REPORT.generated.json` (or `CHUMMER_VERIFY_REPORT_OUTPUT`). The
report records the mode, result, and every machine-readable skip.

| Mode | Intended use | Package/proof posture |
| --- | --- | --- |
| `scaffold` | early local assembly | may record skips; never release evidence |
| `slice` | default repository verification | may record bounded skips; local owner tree is selected explicitly when no feed is configured |
| `integration` | package-plane integration | no stubs or sibling project references; requires an explicit feed and a new isolated cache |
| `release` | release evidence | integration rules plus no missing, skipped, stale, or fixture proof and an explicit manifest target |

Strict invocation requires a new cache path and a pinned feed:

```bash
CHUMMER_VERIFY_MODE=integration \
CHUMMER_ALLOW_STUB_PACKAGES=0 \
CHUMMER_PUBLISHED_FEED_SOURCES=/absolute/path/to/pinned-feed \
CHUMMER_VERIFY_ISOLATED_CACHE_ROOT=/absolute/new/path \
  bash scripts/ai/verify.sh
```

Release mode additionally requires the current rule-environment proof and an
explicit credential-free HTTPS manifest URL (or a non-fixture absolute local
manifest). A release run fails as soon as any gate attempts to skip.

`scripts/ai/verify_fresh_checkout_package_plane.py` is the stronger automatic
integration lane. It acquires exact owner commits from
`config/package-plane.lock.json`, packs the six contract packages into a new
same-run feed, inventories every package by SHA-256, clones the UI into a
directory with no sibling repositories, builds with a new NuGet cache and only
that feed, and re-hashes the feed after the build. A dirty consumer, changed
lock source, ambient package, or reused/tampered package fails.
