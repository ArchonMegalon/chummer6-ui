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

Strict invocation requires a new cache path plus the exact feed directory,
single-source `NuGet.Config`, and both SHA-256 authorities:

```bash
CHUMMER_VERIFY_MODE=integration \
CHUMMER_ALLOW_STUB_PACKAGES=0 \
CHUMMER_PUBLISHED_FEED_ROOT=/absolute/path/to/pinned-feed \
CHUMMER_PUBLISHED_FEED_SHA256=<canonical-feed-inventory-sha256> \
CHUMMER_PUBLISHED_NUGET_CONFIG=/absolute/path/to/NuGet.Config \
CHUMMER_PUBLISHED_NUGET_CONFIG_SHA256=<nuget-config-sha256> \
CHUMMER_VERIFY_ISOLATED_CACHE_ROOT=/absolute/new/path \
  bash scripts/ai/verify.sh
```

The config must clear every ambient source, declare only
`same-run-local-feed`, map `*` to that source, and point to the exact feed
directory. `verify_mode_contract.py feed-inventory-sha256` produces the
canonical feed-inventory digest. Integration and release reject caller restore
source/config overrides even when they are supplied as MSBuild properties.
They also reject response files, restore/build bypasses, caller output or
intermediate paths, and ambient or command-line MSBuild import hooks. Each
package-plane invocation creates and removes its own cache below the isolated
cache authority; an inherited `NUGET_PACKAGES` value is never trusted.

Release mode additionally requires the current rule-environment proof and an
explicit credential-free HTTPS manifest URL (or a non-fixture absolute local
manifest). A release run fails as soon as any gate attempts to skip.

`scripts/ai/verify_fresh_checkout_package_plane.py` is the stronger automatic
integration lane. It acquires exact owner commits from
`config/package-plane.lock.json`, downloads the complete hash-locked external
closure, downloads the official Linux x64 .NET SDK 10.0.103 archive into a
private root after checking its fixed SHA-512, packs all 12 owner packages into
a new same-run feed, inventories every
package by SHA-256, and clones the UI into a directory with no sibling
repositories. It then builds Presentation, Desktop Runtime, Avalonia, and
Blazor Desktop—the two heads shipped by the preview stage—and explicitly builds
the Postgres workspace package boundary. It runs the normal
product unit-test project through conventional `dotnet test` with a fresh
per-invocation NuGet cache and only that feed. Normal integration/release
verification uses the same conventional `dotnet test` path. Owner packs and
consumer commands receive a minimal allowlisted child
environment and must each report SDK 10.0.103. Owner restores receive the same
fixed feed/config globals as consumers, and NuGet dependency-version
approximation is fatal. A dirty
consumer, changed source lock, reduced build/test/compile source set, ambient
package or MSBuild property, or reused/tampered package fails.
