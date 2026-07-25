# Desktop native lifecycle evidence

The flagship release requires a passing native lifecycle receipt for each
published desktop tuple. A startup smoke test by itself is not sufficient.

The Windows lane runs on `windows-latest` and proves:

- the candidate and N-1 installers have the pinned Authenticode certificate
  and SPKI identities;
- the candidate's KeyLocker receipt is the exact
  `chummer6-ui.desktop_artifact_signing` version `2` contract for Windows,
  `win-x64`, and the candidate release version, with `signingStatus: "pass"`;
- the receipt's provider-independent `artifactSignatures` entry binds the
  exact installer digest and the same certificate/SPKI identities;
- an immutable N-1 installer and payload are downloaded and hash-verified;
- the N-1 installer performs its normal full registration path;
- startup and mouse-first core workflows pass before and after update;
- the candidate replaces the installed N-1 bytes without changing user state;
- the registered, cached candidate uninstaller removes the install root,
  protocol registration, uninstall registration, and shortcuts.

The Linux lane runs on `ubuntu-latest` and proves:

- the candidate GitHub artifact run, attempt, archive digest, member digest,
  and package metadata are exact;
- an immutable N-1 `.deb` and its generation manifest are downloaded and
  hash-verified;
- normal system `apt`/`dpkg` install and upgrade paths execute;
- startup and mouse-first core workflows pass before and after update;
- `apt remove --purge` removes the package and native launchers while leaving
  user state intact.

Both workflows fail closed. Their original actor and triggering actor must
both be `github-actions[bot]`; the exact run ID and attempt are preserved in
the lifecycle receipt and global adapter under the explicit
`same-actor-only` rerun policy. A human-triggered rerun of either bot-dispatched
lane is rejected. They do not accept Wine, containers, rootless `dpkg`
simulations, missing signer pins, mutable `latest` URLs, stale receipts,
unsigned Windows installers, skipped phases, or edited evidence files.

The N-1 authority must be exact canonical JSON using contract
`chummer6-ui.desktop-native-lifecycle-n-minus-one` version `1`. URLs must be
credential-free HTTPS URLs on `chummer.run`, include the immutable generation
ID as a path segment, and bind the generation manifest and artifact hashes.
The Windows object also binds the separate payload URL, hash, and size.
Each runner downloads the immutable generation manifest, verifies that its
published generation, release version, artifact row, URL, digest, size, and
Windows payload row match the N-1 contract, and preserves those exact manifest
bytes in the evidence bundle.

The Linux candidate authority uses contract
`chummer6-ui.desktop-native-lifecycle-candidate` version `1`. It binds the
producer repository, workflow, ref, commit, actor, run, attempt, artifact ID,
artifact name, artifact archive digest, member path, member digest, and member
size. The native workflow accepts only a successful producer run at the exact
checked-out `main` commit.

These lanes only produce evidence artifacts. They never publish or activate a
release. The global release finalizer must bind their receipt SHA-256 values and
complete the independent approval gate.

Startup and mouse-first receipts are revalidated against the exact N-1 or
candidate release version, artifact digest, platform, RID, native hosted-runner
class, and environment-supplied digest source. Mouse-first evidence must also
prove a live pointer-driven journey with non-empty steps and no error.

## Global flagship adapter

The lifecycle receipt remains the evidence authority. Once the global
candidate ID and generation ID are known, `emit-flagship-adapter` produces the
assembler-facing `chummer6-ui.flagship-native-e2e.windows.v1` or
`chummer6-ui.flagship-native-e2e.linux.v1` contract. It first revalidates the
complete lifecycle receipt and all of its evidence files. The clean-install,
core-workflow, and N-1-update checks then reference that same receipt path,
SHA-256, and size.

The command requires the global candidate ID, generation ID, fixed flagship
artifact ID, source commit, candidate-root-relative lifecycle receipt path,
and output path. It does not infer global IDs. The supplied source commit must
match the source commit already authenticated by the native lifecycle receipt.
