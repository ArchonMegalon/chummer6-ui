# Unsigned native macOS internal build

Use `.github/workflows/unsigned-macos-native-build.yml` when the immediate goal
is to obtain an internal Avalonia macOS build without activating the protected
Developer ID/notarization lane.

The workflow is manual and secretless. It builds and starts each target on a
matching standard GitHub-hosted machine:

| RID | GitHub-hosted label | Native CPU |
| --- | --- | --- |
| `osx-arm64` | `macos-15` | Apple Silicon (`arm64`) |
| `osx-x64` | `macos-15-intel` | Intel (`x86_64`) |

Each job derives its version from the exact UI commit as
`0.0.0-ci.sha<first-12-source-SHA>`, checks out the owner repositories at the
commits pinned by the UI package plane, publishes a self-contained Avalonia
app, creates a DMG with the existing desktop packager, mounts and starts that
DMG through the existing startup-smoke lane, and uploads:

- the exact DMG
- a SHA-256 inventory
- the existing packaging-signing and startup-smoke receipts
- `UNSIGNED_MACOS_NATIVE_BUILD.generated.json`, binding the UI and owner
  commit/tree identities, runner image and architecture, deterministic version,
  artifact digest/size, and release boundary

The native package plane is bound to Core
`6c66477ba8f7e87868192965a9f27111049b3a16` and the hosted-green Hub main
commit `a215dcd3775f4d8520722a5a07dfa4cd0ed3422a`. The builder rejects owner
checkouts at any other commit; candidate PR heads are not build authority.

The DMG filesystem image is not byte-reproducible because `hdiutil` embeds
filesystem metadata. The receipt says so and binds the exact produced bytes by
SHA-256; it does not claim reproducible packaging.

## Run it

After the branch or commit containing the workflow exists on GitHub:

```bash
gh workflow run unsigned-macos-native-build.yml --ref <branch-or-commit-ref>
```

Or select **Unsigned native macOS internal build** in the repository Actions
tab and choose **Run workflow**. Both matrix jobs must pass before treating the
pair as a successful dual-architecture internal build.

## Release boundary

These Actions artifacts are deliberately **unsigned and unnotarized**. They are
for internal installation/smoke testing only. The workflow has only
`contents: read`, references no GitHub environment or secret, does not call
`chummer.run`, does not deploy, and does not create or publish a GitHub release.

Public macOS eligibility still requires the separately governed
`.github/workflows/macos-flagship-evidence.yml` Developer ID, hardened-runtime,
notarization/stapling, predecessor/update, and release-truth gates. A green run
of this internal workflow must not widen the public macOS release claim.
