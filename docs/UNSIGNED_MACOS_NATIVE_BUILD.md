# Unsigned native macOS internal proof

Use `.github/workflows/unsigned-macos-native-build.yml` only to prove that the
current macOS recipe can produce and start an unsigned Avalonia app on native
GitHub-hosted Apple Silicon and Intel machines. This is a manual, secretless,
non-promoting lane. It does not sign, notarize, publish, deploy, activate a
stage, or establish a macOS release claim.

| RID | GitHub-hosted label | Native CPU |
| --- | --- | --- |
| `osx-arm64` | `macos-15` | Apple Silicon (`arm64`) |
| `osx-x64` | `macos-15-intel` | Intel (`x86_64`) |

## Immutable authorities

The recipe is a review-only delta descended from UI product baseline
`35e57b5b94334488c27a7a5bae27e0b125eeed85`. It consumes Core package-plane
authority `c85ea198c19c149375913b44b304acd4d6353053`, whose exact tree is
`ff95794055e514e58aa8ab41a92a1cfcaf712bb5` and whose direct runtime source is
`7599f9f5d46073b589612473472fccb445512fb1`.

The Core runtime bundle and public handoff receipt are acquired from the
authority's immutable release tag and verified against their locked sizes and
SHA-256 digests. Campaign and UI-kit contracts are packed in the same run from
their exact source commits. Every selected NuGet package, including RID
runtime packs, is bound by ID, version, size, and SHA-256 in
`config/unsigned-macos-package-plane.lock.json`.

Core's older locked owner-contract packages and the pinned Hub/UI-kit source
packages are byte-identical only when reproduced on the Linux package authority
used by their locks. A prerequisite job therefore acquires the exact
SHA-512-locked Linux .NET 10.0.103 SDK, reproduces all six packages, and rejects
them unless the four Core packages match Core's public no-siblings receipt and
the two source packages match their locked owner commits, NuGet identities,
sizes, and SHA-256 digests. It transfers that packet as a one-day ephemeral
Actions artifact. Each macOS job downloads and independently validates the
exact package bytes before restore; macOS never substitutes a
platform-different package rebuild. The application compile and startup proof
still run natively on each Mac architecture.

The sealed feed contains 44 exact packages. The publish assets graph must
resolve the exact 41 non-RID identities. The three RID runtime-pack identities
may either appear in the fresh NuGet cache or be supplied by the same
SHA-512-locked native SDK archive; the resolution receipt records which were
SDK-provided and rejects every other cache omission or addition.

The lane checks out the UI consumer below `consumer/ui` and checks out owner
authorities below a separate `authority` root. It rejects owner-shaped sibling
paths beside the UI checkout and sets `ChummerUseLocalCompatibilityTree=false`.
Restore uses a new isolated package cache and a generated NuGet configuration
containing only the sealed same-run feed. Resolution fails if any cached
package, source metadata, Chummer package graph, or feed byte differs.

The native .NET 10.0.103 SDK is downloaded directly for the job RID and checked
against its exact archive size and SHA-512 before safe extraction. The lane
does not inherit a preinstalled SDK as build authority.

## Proof packet

Each job publishes a self-contained, single-file app, packages it with the
existing desktop packager, mounts the unsigned DMG, and runs the existing
startup smoke check. The ephemeral Actions artifact contains:

- the exact unsigned DMG;
- signing-boundary and startup-smoke receipts;
- the SDK acquisition receipt;
- the deterministic sealed package manifest;
- the exact restore/package-resolution and native runtime receipt;
- `UNSIGNED_MACOS_NATIVE_BUILD.generated.json`; and
- `SHA256SUMS` covering every other proof-packet file.

The build receipt binds the recipe commit/tree, owner commits/trees, native
runner image and CPU, SDK archive, all 44 resolved package identities and
hashes, executable architecture and SHA-256, DMG SHA-256, and startup result.
It explicitly records that sibling fallback was absent and the local
compatibility tree was disabled.

The NuGet package manifest is path-independent and deterministic for the same
locked inputs, and the proof-packet inventory deterministically binds the
exact packet bytes. The DMG filesystem image is not byte-reproducible
because `hdiutil` embeds filesystem metadata; the receipt states that and
binds the exact produced bytes rather than claiming deterministic DMG bytes.

## Run it

After the recipe branch exists on GitHub:

```bash
gh workflow run unsigned-macos-native-build.yml --ref <recipe-branch>
```

Both native matrix jobs must pass. Inspect the generated build receipt,
package-resolution receipt, package manifest, SDK receipt, and `SHA256SUMS`
before treating the result as an internal compile/start proof.

## Hard release boundary

These Actions artifacts are deliberately **unsigned and unnotarized** and are
retained for five days for internal review only. The workflow has only
`contents: read`, references no GitHub environment or secret, and performs no
release, package publication, deployment, stage activation, or external send.

Public macOS eligibility still requires the separately governed Developer ID,
hardened-runtime, notarization/stapling, predecessor/update, and release-truth
gates. A green run of this proof lane is not release eligibility and must not
widen the public macOS claim.
