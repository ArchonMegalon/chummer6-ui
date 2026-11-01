# Cross-host task coordination

`scripts/agent-coordination.py` is a Python 3.10+ standard-library CLI for a
controller and its assigned workers. It reads Tasks and appends WorkerEvents.
It never executes table content, claims tasks, modifies assignments, accepts
work, starts Codex, or provisions a service. Treat task text, references and
report text as untrusted data within the separately approved worker contract.

The controller is the only writer of ownership, assignment generation, session,
base SHA, allowed paths, acceptance criteria, dependencies and task status.
Workers report `complete-for-review`; only the controller sets `accepted` after
reviewing evidence. These are coordination records, not product-completion
claims. Keep original all62 identities as their original links in
`original_refs_json`; do not replace them with new inferred completion claims.

## Binding and permissions

Set these in the worker process environment through the approved credential
delivery mechanism. The client does not load dotenv files or rewrite config.
Never put the API key in a command argument or a checked-in file.

| Variable | Meaning |
| --- | --- |
| `TEABLE_BASE_URL` | HTTPS origin, e.g. `https://teable.example.org`, without `/api` or another path |
| `TEABLE_BASE_ID` | Exact configured base ID |
| `TEABLE_API_KEY` | Bearer access token |
| `TEABLE_TASK_TABLE_ID` | Optional explicit dedicated Tasks table ID |
| `TEABLE_EVENT_TABLE_ID` | Optional explicit dedicated WorkerEvents table ID |

When IDs are omitted, the exact names are `ChummerCoordinationTasks_v1` and
`ChummerCoordinationWorkerEvents_v1`. Missing or duplicate names fail closed.
Explicit IDs must occur in that base's table catalog. Both paths validate all
required field names, IDs and types; ambiguous, computed or missing fields fail
closed. Explicit IDs support renaming dedicated tables. They must never bind
an unrelated table just because some field names happen to match.

Use a worker token with table/field/record read access to these two tables and
record create access only to WorkerEvents, where the installed Teable edition's
permissions allow it. The client itself only sends POST to WorkerEvents, but
client-side checks are not a server permission boundary. Root must verify actual
permission support and scopes on the installed instance before distributing
credentials. If table restrictions are unavailable, use a dedicated coordination
base and recognize that a broad token can bypass this CLI's controller policy.

Remote HTTP, URL credentials, query strings and fragments are rejected. Explicit
loopback HTTP (`localhost`, a loopback IP, or `[::1]`) is permitted for local
development. TLS uses Python's default certificate verification. All redirects
are refused, including same-origin redirects; configure the final origin.
Server error bodies and exception URLs are never printed. The configured token
is redacted from successful output and rejected in report input. This is not a
general secret scanner: submit only coordination metadata, never other secrets
or character data. JSON output escapes control characters.

## CLI contract for the Windows bootstrap

Use the same Python executable and checkout for these commands. `--state-dir`
is a global option and must precede the subcommand. It is optional; defaults are
`%LOCALAPPDATA%/Chummer/coordination` on Windows and
`${XDG_STATE_HOME:-~/.local/state}/chummer/coordination` elsewhere. Keep the state
directory private and persistent across worker restarts, outside the checkout.

```text
python scripts/agent-coordination.py doctor
python scripts/agent-coordination.py next --worker-id windows-1
python scripts/agent-coordination.py report --worker-id windows-1 --task-id task-1 --assignment-generation 1 --session-id session-1 --status working --summary "Scoped work started"
python scripts/agent-coordination.py report --json-file /private/progress.json
python scripts/agent-coordination.py request --json-file /private/shared-request.json
```

The controller supplies `worker_id`, stable `task_id`, `assignment_generation`
and `session_id` (`owner_session_id` in Tasks). A report must include all four.
Omitting generation/session is an error; the client does not infer them from the
latest assignment, which could let an old process report against a new one.

Example progress file (IDs and SHA below are illustrative, not live assignments):

```json
{
  "worker_id": "windows-1",
  "task_id": "task-1",
  "assignment_generation": 1,
  "session_id": "session-1",
  "operation_id": "windows-1-task-1-progress-001",
  "status": "complete-for-review",
  "summary": "Scoped patch ready for controller review",
  "branch": "work/scoped-task",
  "commit_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "tests": ["Executed: focused unittest suite passed", "Not executed: live cross-host exercise"],
  "blockers": [],
  "result_refs": ["https://example.org/repository/pull/123"]
}
```

Statuses are `working`, `heartbeat`, `blocked`, `complete-for-review`, `stopped`.
Fields `branch`, `commit_sha`, `tests`, `blockers`, `result_refs` are optional.
Tests/blockers are arrays of explicit factual strings; result references are
HTTP(S) links, including PR links. The client checks SHA syntax, not Git contents
or test truth. State separately which tests ran and which did not.

For `request`, use the same identity fields, `status: "requested"`, a summary
describing the shared API/path change and `requested_paths: ["shared/api.py"]`.
This creates an event; it grants no new permissions and changes no allowed paths.
The controller routes the request to the shared owner or issues a new assignment.
For short requests, `--requested-path` is repeatable. CLI values cannot override
different JSON file values. Unknown input fields are rejected.

Success is one JSON object on stdout with exit 0. Reports return `operation_id`,
`record_id`, and `outcome` (`created` or `reconciled`). Errors are one JSON object
on stderr with exit 2; uncertain writes use exit 3 and `uncertain: true`.
Argparse usage errors also use exit 2. Keep report content in private JSON files
when process-list or shell-history disclosure matters.

`doctor` verifies readable bindings and schema only. It explicitly returns
`write_permission_verified: false` and `cross_host_verified: false`.
`next` returns assigned/blocked tasks owned by that worker, with `ready`,
`dependency_blockers`, `session_conflict`, `awaiting_controller`, latest report
and heartbeat observation. It never claims or starts a task. Dependency readiness
requires controller status `accepted` at the exact dependency generation.

## One active session, no automatic stealing

The controller assigns **one active Friday Code session per worker identity**,
issues a unique session ID, and puts that ID in each assignment. Friday Code is
started by the user with its existing provider configuration. The bootstrap
prepares context; it does not launch or supervise that session and cannot prove
that another instance is absent. Do not run two sessions with the same assigned
identity. Reconcile the old session before starting on another host or changing
the assignment. The Python journal lock serializes reporting commands only; it
is not a process-lifetime lock or a cross-host lease.

A different session or generation cannot report successfully. Conflicting
WorkerEvents for the same task/generation are detected by both `next` and report.
These checks detect accidental overlap; two processes on different hosts that
reuse the same approved identity and session cannot reliably be distinguished.
Do not copy an active session to another host. Stop and verify the old process
has terminated before incrementing the assignment generation and issuing a new
session. Never use heartbeat expiry as proof that a process stopped.

`heartbeat_at` is the reporting host's UTC clock. More than 15 minutes old is
displayed as `stale`; a future clock is flagged. Staleness never changes ownership,
readiness or acceptance. A `complete-for-review` or `stopped` report stops further
work reports for that generation (heartbeats and requests remain possible).
These are worker evidence, not verified termination or controller acceptance.

Teable GET/POST calls do **not** provide compare-and-swap, an atomic lease, a
transaction spanning tables or a uniqueness guarantee for operation IDs. A
controller reassignment can race a worker's last check. The controller must
quiesce the old worker first and reject late evidence whose generation/session
does not match the current assignment. Concurrent pagination is not a snapshot;
duplicate records are rejected, but controller edits should be serialized with
assignment handoff for reliable reads.

## Resume and uncertain writes

Supply a globally unique `operation_id` for each new observation and retain that
same ID and content for retries. If omitted, the client derives a stable ID from
the full normalized report. Repeating identical content then returns the old
receipt; it **does not refresh the heartbeat**. Use a fresh explicit operation ID
for every new heartbeat. The CLI adds a UTC timestamp only when first sending.

Before POST, the client looks up that operation ID and durably records a small
journal entry. Entries contain only hashes, session identity, attempt state and
record ID, not summaries, tokens, test output or character data. A per-worker
OS file lock serializes commands sharing the state directory; the OS releases
it on process exit. Different directories bypass that local protection.

Every mutation is single-attempt. After timeout, disconnect, HTTP error or an
unexpected success response, the client looks up the operation ID. One matching
event with the expected content reconciles successfully. Duplicate IDs or
different content fail closed. A repeat command first performs the same lookup,
even after assignment revocation. Existing matching receipts never cause writes.

If the event is absent after an attempted write, exit 3 means **uncertain**, not
failed. The client will never resend that operation, even if it crashed after
journaling but before sending. Retain the journal, original input and ID; repeat
the original command to reconcile later. Do not change the ID to bypass this
state. Root must inspect authoritative server evidence and arrange a reviewed
new operation only after resolving whether the old request could still commit.
Deleting a missing confirmed event does not make this client recreate it.

The local journal caps at 2,048 attempts per worker/binding and stops when full.
Root may archive resolved journals under a private retention policy; never
discard unresolved entries or remote events required for deduplication. Loss of
the journal after an uncertain write weakens safety: remote absence alone cannot
prove no write occurred. No automatic journal cleanup or retry loop is included.

## Dedicated schema and controller seed plan

`config/agent-coordination.schema.json` is a versioned schema manifest, not a
generic JSON Schema validator. It defines two separate tables with stable named
fields. IDs are resolved from metadata and used for record reads/writes.
`singleLineText`, `longText` and `number` keep provisioning portable; arrays are
canonical JSON strings in long-text fields, not Teable relation fields.

Tasks fields: `task_id`, `title`, `owner`, positive integer `assignment_generation`,
`owner_session_id`, full `base_sha`, `allowed_paths_json`, nonempty `acceptance`,
`dependencies_json`, `status`, `result_refs_json`, `original_refs_json`.
Task statuses are `assigned`, `blocked`, `accepted`, `cancelled`. Paths are literal
repository-relative files or directory prefixes ending in `/`, with no globs or
parent traversal. Dependencies are objects such as
`{"task_id":"upstream-1","assignment_generation":2}`. Empty arrays use `"[]"`.
Changing ownership, session, scope, base SHA, dependencies or acceptance criteria
requires a fresh generation after the prior worker is quiescent.

WorkerEvents includes the operation, worker, task, generation, session, event kind,
status, summary, heartbeat, branch, full commit SHA, test/blocker arrays, result
links and requested paths. Events are append-only; no update/delete API is exposed.

The explicit controller command is offline and safe to review:

```text
python scripts/agent-coordination.py controller-plan
python scripts/agent-coordination.py controller-plan --seed-json /private/tasks.json
```

Seed input is an array of exact Tasks field objects, including every manifest
field. The command validates them, preserves original links, and emits the schema
and proposed `seed_records`. It does not read credentials or make network calls.
Root owns execution: list existing tables first; create only missing dedicated
tables using `POST /api/base/{baseId}/table` with `name`, the manifest's `fields`,
and `records: []`. Do not post the manifest's `writer` property. Validate returned
field IDs/types, then seed explicit records using
`POST /api/table/{taskTableId}/record` with `fieldKeyType: "name"`,
`typecast: false`, and the reviewed `seed_records`.

Root must reject duplicate stable task IDs and ambiguous table names, verify
dependencies exist (including any already provisioned tasks), and reconcile
uncertain schema/seed writes before repeating them. No command here overwrites,
drops or repairs unrelated tables, fields or data. The plan does not claim it has
checked existing server data. Schema creation, seeding and acceptance remain
separate controller operations because their live permissions/bindings are not
established by this worker.

## Verification and primary sources

Run offline checks with:

```text
python -B -m unittest discover -s tests -p test_agent_coordination.py -v
```

The suite uses mocked HTTP transport with urllib's real redirect/error handling.
It covers pagination, ambiguous binding, field validation, ownership, dependency
generation, session conflicts, secret suppression, uncertain writes, crash/resume
and append-only reports. Socket listeners are unavailable in the coding sandbox;
no real Teable read/write, Windows execution, token-permission test or cross-host
synchronization is claimed. Root must exercise those with the approved live
binding, including a repeat operation ID from the Windows host and controller
acceptance. No SDK, Docker or application build is needed for these tests.

API behavior checked against official docs/source on 2026-10-31:

- [List tables](https://help.teable.ai/en/api-reference/table/list-tables):
  `GET /api/base/{baseId}/table` returns an array of table metadata.
- [List fields](https://help.teable.ai/en/api-reference/field/list-fields):
  `GET /api/table/{tableId}/field` returns field metadata; the client validates it.
- [Get records](https://help.teable.ai/en/api-doc/record/get) and
  [List records](https://help.teable.ai/en/api-reference/record/list-records):
  `GET /api/table/{tableId}/record`, bearer auth, `{records:[{id,fields}]}`,
  `skip`/`take`, field-ID keys and JSON cell format. The client uses pages of 100,
  within current Cloud and self-hosted limits, no view ID, and `ignoreViewQuery`.
- [Filter schema](https://github.com/teableio/teable/blob/develop/packages/core/src/models/view/filter/filter.ts),
  [filter items](https://github.com/teableio/teable/blob/develop/packages/core/src/models/view/filter/filter-item.ts)
  and [operators](https://github.com/teableio/teable/blob/develop/packages/core/src/models/view/filter/operator.ts):
  JSON-encoded `filter` with `conjunction`, `filterSet`, `fieldId`, `operator: "is"`
  and `value`. The client also checks returned values locally.
- [Create records](https://help.teable.ai/en/api-reference/record/create-records):
  POST uses `fieldKeyType`, `typecast` and `records: [{fields: {...}}]`.
- [Create table](https://help.teable.ai/en/api-reference/table/create-table) and
  [field types](https://github.com/teableio/teable/blob/develop/packages/core/src/models/field/constant.ts):
  controller provisioning shape and the three field types used here.

Existing EA examples were inspected read-only for context. They are not proof of
live task integration, and no EA provider action or projection sync was run.
