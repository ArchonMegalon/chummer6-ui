# Global flagship release assembler

`scripts/release/assemble_global_flagship_release.py` is a local, fail-closed
coordinator for one immutable Windows, Linux, and macOS release candidate. It
does not upload, deploy, activate, advance a channel pointer, or publish.

Production candidate roots are created by the protected, provider-authenticating
lane documented in
[`GLOBAL_FLAGSHIP_CANDIDATE_PRODUCTION.md`](GLOBAL_FLAGSHIP_CANDIDATE_PRODUCTION.md).
The local CLI remains useful for contract tests, but hand-authored local JSON
is not a substitute for that provider-authenticated production artifact.

The coordinator deliberately composes existing platform contracts instead of
widening the Windows-only preview publication lane:

| Platform | Required existing contract | Additional flagship evidence |
| --- | --- | --- |
| Windows `win-x64` | `chummer6-ui.windows_desktop_exit_gate` and `chummer6-ui.desktop_artifact_signing` v2 | `chummer6-ui.flagship-native-e2e.windows.v2` |
| Linux `linux-x64` | `chummer6-ui.linux_desktop_exit_gate` | `chummer6-ui.flagship-native-e2e.linux.v2` |
| macOS `osx-arm64` | `chummer6-ui.macos_desktop_exit_gate` and `chummer6-ui.desktop_artifact_signing` v2 | `chummer6-ui.flagship-native-e2e.macos.v1` |

Windows signing must pass. macOS signing, notarization, and stapling must pass.
The direct Linux `.deb` is bound by its manifest SHA-256 and native `dpkg`
verification rather than pretending that the current builder emits a Linux
code-signing receipt.

The protected candidate producer accepts the Windows gate only from the exact
authenticated export artifact, the Linux gate only from the exact
authenticated lifecycle-evidence artifact, and the macOS gate only from the
exact authenticated encrypted-custody artifact. Linux and macOS native lanes
run their existing canonical gate materializers before upload; the assembler
never derives a gate from lifecycle JSON.

## Candidate contract

The input contract is
`chummer6-ui.global-flagship-candidate.v1`. It carries:

- one candidate ID, generation ID, release version, N-1 release version,
  channel ID, exact protected-main source repository/ref/commit, and producer
  identity;
- the exact candidate producer workflow, run-attempt-one artifact name
  `global-flagship-candidate-payload-CANDIDATE_ID-PRODUCER_RUN_ID-1`, and all
  seven canonical provider actor logins;
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

All three platform adapters and their rich evidence must also bind one exact
live predecessor release-channel URL and byte SHA-256. Platform-specific N-1
and selected-tuple digests remain distinct, but evidence captured against
different live-root bytes cannot be assembled into one flagship proposal.

All candidate paths are relative to the candidate manifest. Symlinks,
traversal, duplicate JSON keys, stale receipts, future-dated receipts, missing
files, mismatched digests, and platform/candidate mismatches fail closed.
Freshness is fixed to 24 hours in the operations CLI; it exposes neither a
clock override nor a wider evidence-age override. Output paths may not alias
or overwrite any candidate, proposal, or approval input.

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
must be distinct and must not be the candidate producer or any of the seven
authenticated provider run actors. Provider roles may share an actor;
comparisons and approval exclusions are case-insensitive while canonical
login spelling is preserved in receipts.

The local proposal and final receipt deliberately declare
`authorityLevel: local-structural-validation-only` and
`provenanceAuthenticated: false`. Local JSON can bind bytes but cannot prove
that a claimed GitHub actor, native runner, workflow run, artifact, protected
environment approval, or signer identity is genuine. A protected workflow
must authenticate those claims through the provider API and preserve the exact
artifact digest. The later publication transaction must reject this local
receipt if that authenticated workflow handoff is absent or mismatched.

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
