# Global flagship release assembler

`scripts/release/assemble_global_flagship_release.py` is a local, fail-closed
coordinator for one immutable Windows, Linux, and macOS release candidate. It
does not upload, deploy, activate, advance a channel pointer, or publish.

The coordinator deliberately composes existing platform contracts instead of
widening the Windows-only preview publication lane:

| Platform | Required existing contract | Additional flagship evidence |
| --- | --- | --- |
| Windows `win-x64` | `chummer6-ui.windows_desktop_exit_gate` and `chummer6-ui.desktop_artifact_signing` v2 | `chummer6-ui.flagship-native-e2e.windows.v1` |
| Linux `linux-x64` | `chummer6-ui.linux_desktop_exit_gate` | `chummer6-ui.flagship-native-e2e.linux.v1` |
| macOS `osx-arm64` | `chummer6-ui.macos_desktop_exit_gate` and `chummer6-ui.desktop_artifact_signing` v2 | `chummer6-ui.flagship-native-e2e.macos.v1` |

Windows signing must pass. macOS signing, notarization, and stapling must pass.
The direct Linux `.deb` is bound by its manifest SHA-256 and native `dpkg`
verification rather than pretending that the current builder emits a Linux
code-signing receipt.

## Candidate contract

The input contract is
`chummer6-ui.global-flagship-candidate.v1`. It carries:

- one candidate ID, generation ID, release version, N-1 release version,
  channel ID, source repository/ref/commit, and producer identity;
- exactly one artifact for each required platform;
- an exact path, SHA-256, and size for each artifact and receipt;
- the existing platform exit-gate receipt;
- the applicable existing signing receipt (Windows and macOS; `null` for
  Linux);
- one platform-native E2E receipt.

Each platform-native E2E receipt must bind the same candidate identity and
artifact bytes. It must prove a clean install, a core
create/save/close/reopen/export workflow, and an N-1-to-candidate update on the
native operating system. Each check points to a separate evidence file whose
path, SHA-256, and size are revalidated.

All candidate paths are relative to the candidate manifest. Symlinks,
traversal, duplicate JSON keys, stale receipts, future-dated receipts, missing
files, mismatched digests, and platform/candidate mismatches fail closed.

## Two-phase use

Create a short-lived proposal:

```bash
python3 scripts/release/assemble_global_flagship_release.py propose \
  --candidate /path/to/GLOBAL_FLAGSHIP_CANDIDATE.generated.json \
  --output /path/to/GLOBAL_FLAGSHIP_RELEASE_PROPOSAL.generated.json
```

The proposal snapshots every artifact and receipt and remains explicitly
non-publishing. Quality, release, and security must then approve the exact
proposal SHA-256. The receipt contract reserves
`.github/workflows/global-flagship-release-approval.yml` in the protected
`global-flagship-release-review` environment as that authority. Provisioning
that workflow/environment is an explicit external prerequisite; this local
assembler does not synthesize approval authority. The three approval actors
must be distinct and must not be the candidate producer or any native evidence
actor.

Finalize only after those receipts exist:

```bash
python3 scripts/release/assemble_global_flagship_release.py finalize \
  --proposal /path/to/GLOBAL_FLAGSHIP_RELEASE_PROPOSAL.generated.json \
  --candidate /path/to/GLOBAL_FLAGSHIP_CANDIDATE.generated.json \
  --approval /path/to/quality-approval.json \
  --approval /path/to/release-approval.json \
  --approval /path/to/security-approval.json \
  --output /path/to/GLOBAL_FLAGSHIP_RELEASE_FINAL.generated.json
```

Finalization re-reads the candidate, artifacts, platform receipts, and raw E2E
evidence. It rejects any mutation since proposal creation. A passing final
receipt is an auditable handoff to a separate publication transaction; its
`publicationAuthorized` field is always `false`.

## External blockers

The assembler cannot manufacture external authority. A proposal stays blocked
until all of these exist:

- a native Windows runner, DigiCert KeyLocker access, and the approved public
  signer certificate/SPKI pins;
- a native Linux runner that can execute clean install, core workflow, package
  verification, and N-1 update evidence;
- a native Apple Silicon macOS runner, Developer ID identity, and notarization
  profile;
- the reserved `.github/workflows/global-flagship-release-approval.yml`
  approval authority in a protected `global-flagship-release-review` GitHub
  environment, plus three distinct authorized approval actors.

Blocked proposal/final receipts include these requirements and the precise
first failed binding. Credentials are never accepted by or written into this
coordinator.
