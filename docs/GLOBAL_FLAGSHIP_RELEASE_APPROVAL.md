# Global flagship independent approval

The flagship approval lane records review evidence. It cannot sign, upload,
publish, deploy, or activate a release.

## One-time environment setup

Create the protected GitHub environment
`global-flagship-release-review`. Require human review, prevent self-review,
disable administrator bypass of the protection rules, and restrict deployment
branches to `main`.

Protect `main` with strict required status checks, fresh pull-request review
by someone other than the last pusher, admin enforcement, conversation
resolution, and linear history. Disable force pushes and branch deletion.
The workflow verifies through its read-only token that `main` is protected,
its required checks apply to everyone, and its head still equals the exact
approval source SHA. The separate publication verifier must use admin-read
authority to authenticate the detailed branch controls; that broader token
is intentionally unavailable to this approval lane.

Update `.github/global-flagship-reviewer-policy.json` through a reviewed
pull request. Each role array must be nonempty. A login may occur in only
one array, compared case-insensitively, and the three-array union must fit
GitHub's six-reviewer environment limit. Configure that exact union as the
environment human-reviewer set. The workflow cross-binds the protected-main
policy bytes to the live environment reviewers.

Do not create repository, organization, or environment variables with the
old `CHUMMER_FLAGSHIP_*_REVIEWER_ALLOWLIST_JSON` names. Do not add signing,
notarization, publication, Cloudflare, OIDC, or other deployment credentials
to this environment.

## Approval procedure

1. Run the assembler `propose` command against the exact three-platform
   candidate on `main`.
2. Confirm the proposal is at most 45,000 bytes. Compute its lowercase
   SHA-256 and a single-line strict-base64 encoding of the unchanged bytes.
3. Have one allowlisted human independently dispatch
   `global-flagship-release-approval.yml` for each role: `quality`, `release`,
   and `security`. Each dispatch must select the role, supply the exact
   proposal encoding and digest, and set `approval_confirmed` to `true`.
4. Use three different actors and three different workflow run IDs. A rerun
   is never accepted: any failure requires a fresh dispatch and run ID. Each
   protected job also requires one recorded environment approval by a
   different allowlisted human.
5. Retain the three
   `global-flagship-release-approval-ROLE-RUN_ID-RUN_ATTEMPT` artifacts. Each
   contains a read-only v2 approval receipt bound to the proposal bytes,
   candidate identity, source SHA, reviewer-policy digest, actor, role, and
   protected workflow authority.
6. Pass the three receipts to the assembler `finalize` command with the
   unchanged proposal and candidate.

All three approvals must bind the same reviewer-policy digest. If the
proposal expires, `main` advances, the candidate changes, or the reviewer
policy changes, generate a new proposal and collect all three approvals
again.

## Trust boundary

The assembler performs strict local structural validation and deliberately
marks its output `provenanceAuthenticated: false` and
`publicationAuthorized: false`.

Run the separate
[`global-flagship-provider-authentication.yml`](../.github/workflows/global-flagship-provider-authentication.yml)
lane described in
[`GLOBAL_FLAGSHIP_PROVIDER_AUTHENTICATION.md`](GLOBAL_FLAGSHIP_PROVIDER_AUTHENTICATION.md).
It authenticates every workflow run, run attempt, actor, source ref/SHA,
workflow path, environment ID, artifact identity/digest/bytes, and exact run
approval-history reviewer. It also revalidates detailed `main` protection
through a separate single-repository read-only Administration authority that
is unavailable here.

Its immutable handoff authenticates approval and governance provenance only.
It remains nonpublishing, keeps `publicationAuthorized: false`, and explicitly
does not claim that release artifact bytes were authenticated. Approval
artifacts and the provider-authenticated handoff never authorize publication.
