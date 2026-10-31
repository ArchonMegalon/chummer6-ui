#!/usr/bin/env python3
"""Small, fail-closed Teable coordination client. Python 3.10+, standard library only."""

import argparse
import contextlib
import datetime as dt
import hashlib
import http.client
import ipaddress
import json
import os
from pathlib import Path
import re
import sys
import tempfile
import urllib.error
import urllib.parse
import urllib.request


SCHEMA_PATH = Path(__file__).resolve().parents[1] / "config/agent-coordination.schema.json"
PAGE_SIZE = 100
MAX_BODY = 2 * 1024 * 1024
MAX_OPERATIONS = 2048
REPORT_STATUSES = {"working", "heartbeat", "blocked", "complete-for-review", "stopped"}
TASK_STATUSES = {"assigned", "blocked", "accepted", "cancelled"}


class CoordinationError(Exception):
    """Only static, safe error messages may cross the CLI boundary."""


class UncertainWrite(CoordinationError):
    pass


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True)


def digest(value):
    return hashlib.sha256(canonical(value).encode()).hexdigest()


def identifier(value):
    if not isinstance(value, str) or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}", value):
        raise CoordinationError("Invalid identifier.")
    return value


def generation(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or value < 1 or value != int(value):
        raise CoordinationError("Assignment generation must be a positive integer.")
    return int(value)


def text_value(value, maximum=16000, required=False):
    if not isinstance(value, str) or len(value) > maximum or (required and not value.strip()):
        raise CoordinationError("Invalid or missing text value.")
    return value


def list_value(value):
    if not isinstance(value, list) or len(value) > 200:
        raise CoordinationError("Expected a bounded JSON array.")
    return value


def paths(value):
    for item in list_value(value):
        if (not isinstance(item, str) or not item or len(item) > 512 or "\\" in item
                or item.startswith("/") or ":" in item or any(c in item for c in "*?[]")
                or any(p in ("", ".", "..") for p in (item[:-1] if item.endswith("/") else item).split("/"))):
            raise CoordinationError("Paths must be literal repository-relative files or directories ending in /.")
    return value


def refs(value):
    for item in list_value(value):
        if not isinstance(item, str) or len(item) > 2000:
            raise CoordinationError("Invalid reference URL.")
        parsed = urllib.parse.urlsplit(item)
        if parsed.scheme not in ("https", "http") or not parsed.hostname or parsed.username or parsed.password:
            raise CoordinationError("References must be HTTP(S) links without credentials.")
    return value


def sha(value, optional=False):
    if optional and value == "":
        return value
    if not isinstance(value, str) or not re.fullmatch(r"[a-fA-F0-9]{40}|[a-fA-F0-9]{64}", value):
        raise CoordinationError("Expected a full commit SHA.")
    return value


def read_json(path):
    try:
        with Path(path).open("rb") as stream:
            raw = stream.read(MAX_BODY + 1)
        if len(raw) > MAX_BODY:
            raise CoordinationError("JSON file is too large.")
        return json.loads(raw)
    except (OSError, ValueError):
        raise CoordinationError("Cannot read valid JSON input.") from None


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Teable:
    def __init__(self, env=None, timeout=20):
        env = os.environ if env is None else env
        base = env.get("TEABLE_BASE_URL", "").rstrip("/")
        try:
            url = urllib.parse.urlsplit(base)
            url.port  # Validate a malformed port before constructing any authenticated request.
            loopback = url.hostname == "localhost"
            if url.hostname and not loopback:
                try:
                    loopback = ipaddress.ip_address(url.hostname).is_loopback
                except ValueError:
                    pass
            if (not url.hostname or url.username or url.password or url.query or url.fragment
                    or url.path not in ("", "/") or any(c.isspace() for c in base)
                    or (url.scheme != "https" and not (url.scheme == "http" and loopback))):
                raise ValueError()
        except ValueError:
            raise CoordinationError("TEABLE_BASE_URL must be an HTTPS origin (HTTP only for explicit loopback).") from None
        self.base_url = base
        self.base_id = identifier(env.get("TEABLE_BASE_ID", ""))
        self.key = env.get("TEABLE_API_KEY", "")
        if not self.key or any(ord(c) < 33 or ord(c) > 126 for c in self.key):
            raise CoordinationError("TEABLE_API_KEY is missing or invalid.")
        self.explicit = {"tasks": env.get("TEABLE_TASK_TABLE_ID"), "events": env.get("TEABLE_EVENT_TABLE_ID")}
        self.timeout = timeout
        self.opener = urllib.request.build_opener(NoRedirect())
        self.schema = read_json(SCHEMA_PATH)
        self.tables = {}
        self.fields = {}

    def request(self, method, path, query=None, body=None):
        url = self.base_url + "/api/" + path
        if query:
            url += "?" + urllib.parse.urlencode(query)
        data = None if body is None else canonical(body).encode()
        req = urllib.request.Request(url, data=data, method=method, headers={
            "Authorization": "Bearer " + self.key, "Accept": "application/json", "Content-Type": "application/json"})
        try:
            with self.opener.open(req, timeout=self.timeout) as response:
                raw = response.read(MAX_BODY + 1)
            if len(raw) > MAX_BODY:
                raise ValueError()
            return json.loads(raw)
        except urllib.error.HTTPError as exc:
            code = exc.code
            exc.close()  # Never read or expose error bodies, URLs, headers, or exception text.
            if method != "GET":
                raise UncertainWrite("Write response was not confirmed; reconcile the operation ID.") from None
            raise CoordinationError("Teable read failed (HTTP %d); redirects are refused." % code) from None
        except (OSError, ValueError, http.client.HTTPException):
            if method != "GET":
                raise UncertainWrite("Write response was not confirmed; reconcile the operation ID.") from None
            raise CoordinationError("Teable read failed or returned invalid JSON.") from None

    def table_catalog(self):
        rows = self.request("GET", "base/" + self.base_id + "/table")
        if not isinstance(rows, list) or any(not isinstance(r, dict) for r in rows):
            raise CoordinationError("Invalid table catalog response.")
        return rows

    def bind(self):
        catalog = self.table_catalog()
        for role, spec in self.schema["tables"].items():
            explicit = self.explicit[role]
            matches = [r for r in catalog if (r.get("id") == explicit if explicit else r.get("name") == spec["name"])]
            if len(matches) != 1:
                raise CoordinationError("Missing or ambiguous coordination table binding.")
            table_id = identifier(matches[0].get("id"))
            self.tables[role] = table_id
            fields = self.request("GET", "table/" + table_id + "/field", {"filterHidden": "false"})
            if not isinstance(fields, list) or any(not isinstance(f, dict) for f in fields):
                raise CoordinationError("Invalid field metadata response.")
            mapping = {}
            for expected in spec["fields"]:
                found = [f for f in fields if f.get("name") == expected["name"]]
                if (len(found) != 1 or found[0].get("type") != expected["type"]
                        or found[0].get("isComputed") or found[0].get("isLookup")):
                    raise CoordinationError("Coordination schema is missing, ambiguous, or incompatible.")
                mapping[expected["name"]] = identifier(found[0].get("id"))
            if len(set(mapping.values())) != len(mapping):
                raise CoordinationError("Duplicate field identifiers.")
            self.fields[role] = mapping
        if self.tables["tasks"] == self.tables["events"]:
            raise CoordinationError("Tasks and WorkerEvents must be separate tables.")

    def records(self, role, equals=None):
        query = {"take": PAGE_SIZE, "skip": 0, "fieldKeyType": "id", "cellFormat": "json", "ignoreViewQuery": "true"}
        if equals:
            query["filter"] = canonical({"conjunction": "and", "filterSet": [
                {"fieldId": self.fields[role][k], "operator": "is", "value": v} for k, v in equals.items()]})
        seen = set()
        result = []
        for _ in range(1000):
            payload = self.request("GET", "table/" + self.tables[role] + "/record", query)
            rows = payload.get("records") if isinstance(payload, dict) else None
            if not isinstance(rows, list) or len(rows) > PAGE_SIZE:
                raise CoordinationError("Invalid record page response.")
            for row in rows:
                if not isinstance(row, dict) or not isinstance(row.get("fields"), dict):
                    raise CoordinationError("Invalid record response.")
                record_id = identifier(row.get("id"))
                if record_id in seen:
                    raise CoordinationError("Unstable pagination; repeat the read after controller edits finish.")
                seen.add(record_id)
                fields = {name: row["fields"].get(fid) for name, fid in self.fields[role].items()}
                # Filters are optimization, never an authorization boundary.
                if not equals or all(fields.get(k) == v for k, v in equals.items()):
                    result.append({"id": record_id, "fields": fields})
            if len(rows) < PAGE_SIZE:
                return result
            query["skip"] += len(rows)
        raise CoordinationError("Record page limit exceeded.")

    def create_event(self, fields):
        payload = self.request("POST", "table/" + self.tables["events"] + "/record", body={
            "fieldKeyType": "id", "typecast": False, "records": [{"fields": {
                self.fields["events"][name]: value for name, value in fields.items()}}]})
        rows = payload.get("records") if isinstance(payload, dict) else None
        if not isinstance(rows, list) or len(rows) != 1 or not isinstance(rows[0], dict):
            raise UncertainWrite("Write returned an unexpected response; reconcile the operation ID.")
        try:
            return identifier(rows[0].get("id"))
        except CoordinationError:
            raise UncertainWrite("Write returned no valid record ID; reconcile the operation ID.") from None


def task_fields(fields):
    result = dict(fields)
    for key in ("task_id", "owner", "owner_session_id"):
        identifier(result.get(key))
    result["assignment_generation"] = generation(result.get("assignment_generation"))
    sha(result.get("base_sha"))
    text_value(result.get("title"), 512, True)
    text_value(result.get("acceptance"), required=True)
    if result.get("status") not in TASK_STATUSES:
        raise CoordinationError("Invalid controller task status.")
    try:
        for name in ("allowed_paths", "dependencies", "result_refs", "original_refs"):
            result[name] = list_value(json.loads(result.get(name + "_json") or "[]"))
    except (ValueError, TypeError):
        raise CoordinationError("Invalid task JSON field.") from None
    if not paths(result["allowed_paths"]):
        raise CoordinationError("Task has no allowed paths.")
    refs(result["result_refs"])
    refs(result["original_refs"])
    dependencies = set()
    for dep in result["dependencies"]:
        if not isinstance(dep, dict) or set(dep) != {"task_id", "assignment_generation"}:
            raise CoordinationError("Dependencies require task_id and assignment_generation.")
        dependency = identifier(dep["task_id"])
        generation(dep["assignment_generation"])
        if dependency == result["task_id"] or dependency in dependencies:
            raise CoordinationError("Self or duplicate dependency.")
        dependencies.add(dependency)
    return result


def task_map(client):
    tasks = {}
    for record in client.records("tasks"):
        task = task_fields(record["fields"])
        if task["task_id"] in tasks:
            raise CoordinationError("Duplicate stable task ID; controller repair required.")
        task["record_id"] = record["id"]
        tasks[task["task_id"]] = task
    return tasks


def dependency_blockers(task, tasks):
    return [dep["task_id"] for dep in task["dependencies"] if (
        dep["task_id"] not in tasks or tasks[dep["task_id"]]["status"] != "accepted"
        or tasks[dep["task_id"]]["assignment_generation"] != dep["assignment_generation"])]


def next_tasks(client, worker_id):
    identifier(worker_id)
    tasks = task_map(client)
    assigned = []
    for task in sorted(tasks.values(), key=lambda t: t["task_id"]):
        if task["owner"] == worker_id and task["status"] in {"assigned", "blocked"}:
            events = client.records("events", {"task_id": task["task_id"], "assignment_generation": task["assignment_generation"]})
            conflict = any(e["fields"].get("session_id") != task["owner_session_id"]
                           or e["fields"].get("worker_id") != worker_id for e in events)
            reports = [e["fields"] for e in events if e["fields"].get("kind") == "report"]
            submitted = any(e.get("status") in {"complete-for-review", "stopped"} for e in reports)
            latest = max(reports, key=lambda e: e.get("heartbeat_at") or "", default=None)
            heartbeat = None
            if latest:
                try:
                    stamp = dt.datetime.fromisoformat(latest["heartbeat_at"])
                    age = (dt.datetime.now(dt.timezone.utc) - stamp).total_seconds()
                    heartbeat = "stale" if age > 900 else ("recent" if age >= -60 else "clock-skew")
                except (ValueError, TypeError):
                    heartbeat = "invalid-timestamp"
            assigned.append({**task, "ready": task["status"] == "assigned" and not dependency_blockers(task, tasks)
                             and not conflict and not submitted, "dependency_blockers": dependency_blockers(task, tasks),
                             "session_conflict": conflict, "awaiting_controller": submitted, "latest_report": latest,
                             "heartbeat_observation": heartbeat})
    return {"worker_id": worker_id, "tasks": assigned, "read_only": True}


def default_state_dir():
    if os.name == "nt":
        return Path(os.environ.get("LOCALAPPDATA", Path.home() / "AppData/Local")) / "Chummer/coordination"
    return Path(os.environ.get("XDG_STATE_HOME", Path.home() / ".local/state")) / "chummer/coordination"


class Journal:
    """OS lock covers one command; it is not an agent-session or cross-host lease."""
    def __init__(self, directory, binding, worker_id):
        self.directory = Path(directory)
        self.path = self.directory / (digest([binding, worker_id]) + ".json")
        self.data = {"version": 1, "sessions": {}, "operations": {}}

    @contextlib.contextmanager
    def locked(self):
        self.directory.mkdir(parents=True, exist_ok=True, mode=0o700)
        lock = self.path.with_suffix(".lock")
        fd = os.open(lock, os.O_RDWR | os.O_CREAT, 0o600)
        acquired = False
        try:
            if os.name == "nt":
                import msvcrt
                if os.fstat(fd).st_size == 0:
                    os.write(fd, b"0")
                os.lseek(fd, 0, os.SEEK_SET)
                try:
                    msvcrt.locking(fd, msvcrt.LK_NBLCK, 1)
                except OSError:
                    raise CoordinationError("Another local coordination command is active.") from None
            else:
                import fcntl
                try:
                    fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
                except OSError:
                    raise CoordinationError("Another local coordination command is active.") from None
            acquired = True
            if self.path.exists():
                self.data = read_json(self.path)
                if (not isinstance(self.data, dict) or self.data.get("version") != 1
                        or not isinstance(self.data.get("sessions"), dict)
                        or not isinstance(self.data.get("operations"), dict)
                        or any(not isinstance(v, dict) for v in self.data.get("operations", {}).values())):
                    raise CoordinationError("Invalid resume journal; retain it for controller inspection.")
            yield self
        finally:
            if acquired and os.name == "nt":
                os.lseek(fd, 0, os.SEEK_SET)
                msvcrt.locking(fd, msvcrt.LK_UNLCK, 1)
            os.close(fd)

    def save(self):
        fd, temp = tempfile.mkstemp(prefix=".coordination-", dir=self.directory)
        try:
            with os.fdopen(fd, "w", encoding="utf-8") as stream:
                stream.write(canonical(self.data))
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temp, self.path)
            if os.name != "nt":
                fd = os.open(self.directory, os.O_RDONLY)
                try:
                    os.fsync(fd)
                finally:
                    os.close(fd)
        finally:
            if os.path.exists(temp):
                os.unlink(temp)


def report_input(payload, kind):
    allowed = {"worker_id", "task_id", "assignment_generation", "session_id", "operation_id", "status", "summary",
               "branch", "commit_sha", "tests", "blockers", "result_refs", "requested_paths"}
    if not isinstance(payload, dict) or set(payload) - allowed:
        raise CoordinationError("Unknown report fields.")
    p = dict(payload)
    for key in ("worker_id", "task_id", "session_id"):
        identifier(p.get(key))
    p["assignment_generation"] = generation(p.get("assignment_generation"))
    if kind == "request":
        if p.get("status", "requested") != "requested":
            raise CoordinationError("Shared-code requests use status requested.")
        p["status"] = "requested"
    elif p.get("status") not in REPORT_STATUSES:
        raise CoordinationError("Workers may report progress or complete-for-review, never acceptance.")
    text_value(p.get("summary"), required=True)
    text_value(p.setdefault("branch", ""), 512)
    sha(p.setdefault("commit_sha", ""), optional=True)
    for name in ("tests", "blockers"):
        for entry in list_value(p.setdefault(name, [])):
            text_value(entry, 4000, True)
    refs(p.setdefault("result_refs", []))
    paths(p.setdefault("requested_paths", []))
    if kind == "request" and not p["requested_paths"]:
        raise CoordinationError("Shared-code requests need requested_paths.")
    if kind == "report" and p["requested_paths"]:
        raise CoordinationError("Use request for shared-code path changes.")
    p["kind"] = kind
    operation_id = p.pop("operation_id", None)
    fingerprint = digest(p)
    p["operation_id"] = identifier(operation_id) if operation_id is not None else "op-" + fingerprint
    return p, fingerprint


def event_fields(p):
    fields = {k: v for k, v in p.items() if k not in {"tests", "blockers", "result_refs", "requested_paths"}}
    for name in ("tests", "blockers", "result_refs", "requested_paths"):
        fields[name + "_json"] = canonical(p[name])
    return fields


def reconcile(client, operation_id, expected):
    matches = client.records("events", {"operation_id": operation_id})
    if len(matches) > 1:
        raise CoordinationError("Duplicate operation ID; controller inspection required.")
    if matches:
        actual = matches[0]["fields"]
        if any((actual.get(k) or "") != v for k, v in expected.items()):
            raise CoordinationError("Operation ID already belongs to different event data.")
        return matches[0]["id"]
    return None


def send_report(client, payload, kind, state_dir):
    p, fingerprint = report_input(payload, kind)
    if canonical(client.key)[1:-1] in canonical(p):
        raise CoordinationError("Report contains the configured credential; remove it before reporting.")
    fields = event_fields(p)
    binding = [client.base_url, client.base_id, client.tables]
    journal = Journal(state_dir, binding, p["worker_id"])
    with journal.locked():
        entries = journal.data["operations"]
        prior = entries.get(p["operation_id"])
        if prior and prior.get("fingerprint") != fingerprint:
            raise CoordinationError("Operation ID was reused with different content.")
        # A completed retry remains readable even if the assignment was subsequently revoked.
        record_id = reconcile(client, p["operation_id"], fields)
        if record_id:
            if not prior and len(entries) >= MAX_OPERATIONS:
                raise CoordinationError("Resume journal is full; controller must archive resolved history before continuing.")
            entries[p["operation_id"]] = {"fingerprint": fingerprint, "state": "confirmed", "record_id": record_id}
            journal.save()
            return {"operation_id": p["operation_id"], "record_id": record_id, "outcome": "reconciled"}
        if prior:
            raise UncertainWrite("Prior attempt is not visible. Do not retry or change its ID; controller reconciliation required.")
        if len(entries) >= MAX_OPERATIONS:
            raise CoordinationError("Resume journal is full; controller must archive resolved history before continuing.")
        tasks = task_map(client)
        task = tasks.get(p["task_id"])
        if not task or task["owner"] != p["worker_id"]:
            raise CoordinationError("Task is not assigned to this worker.")
        if task["assignment_generation"] != p["assignment_generation"] or task["owner_session_id"] != p["session_id"]:
            raise CoordinationError("Stale assignment generation or unapproved session.")
        if task["status"] not in {"assigned", "blocked"}:
            raise CoordinationError("Task is terminal; controller must authorize a new assignment.")
        if kind == "report" and p["status"] in {"working", "complete-for-review"}:
            if task["status"] != "assigned" or dependency_blockers(task, tasks):
                raise CoordinationError("Task or dependencies are blocked; report blocked or request controller action.")
        session_key = p["task_id"] + ":" + str(p["assignment_generation"])
        local_session = journal.data["sessions"].get(session_key)
        if local_session and local_session != p["session_id"]:
            raise CoordinationError("A different local session used this assignment; controller review required.")
        existing = client.records("events", {"task_id": p["task_id"], "assignment_generation": p["assignment_generation"]})
        if any(e["fields"].get("session_id") != p["session_id"] or e["fields"].get("worker_id") != p["worker_id"] for e in existing):
            raise CoordinationError("Conflicting worker/session events exist for this assignment.")
        if (kind == "report" and p["status"] != "heartbeat" and any(
                e["fields"].get("status") in {"complete-for-review", "stopped"} for e in existing)):
            raise CoordinationError("Worker submitted terminal evidence; controller must review before a new assignment.")
        # Persist before sending; after any crash this operation is read/reconcile-only.
        journal.data["sessions"][session_key] = p["session_id"]
        entries[p["operation_id"]] = {"fingerprint": fingerprint, "state": "attempted"}
        journal.save()
        fields["heartbeat_at"] = dt.datetime.now(dt.timezone.utc).isoformat()
        try:
            record_id = client.create_event(fields)
        except UncertainWrite:
            expected = {k: v for k, v in fields.items() if k != "heartbeat_at"}
            try:
                record_id = reconcile(client, p["operation_id"], expected)
            except CoordinationError:
                raise UncertainWrite("Write uncertain and reconciliation failed; retain the journal and operation ID.") from None
            if not record_id:
                raise UncertainWrite("Write uncertain and not visible yet; retain the journal and operation ID. No retry was sent.") from None
            outcome = "reconciled"
        else:
            outcome = "created"
        entries[p["operation_id"]].update(state="confirmed", record_id=record_id)
        journal.save()
        return {"operation_id": p["operation_id"], "record_id": record_id, "outcome": outcome}


def controller_plan(seed_path=None):
    schema = read_json(SCHEMA_PATH)
    plan = {"read_only": True, "requires_controller_review": True, "tables": schema["tables"], "seed_records": []}
    if seed_path:
        seeds = read_json(seed_path)
        if not isinstance(seeds, list):
            raise CoordinationError("Seed input must be an array of task field objects.")
        seen = set()
        names = {f["name"] for f in schema["tables"]["tasks"]["fields"]}
        for item in seeds:
            if not isinstance(item, dict) or set(item) != names:
                raise CoordinationError("Seed fields must exactly match the Tasks schema.")
            task = task_fields(item)
            if task["task_id"] in seen:
                raise CoordinationError("Duplicate seed task ID.")
            seen.add(task["task_id"])
            plan["seed_records"].append({"fields": item})
    return plan


def parser():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--state-dir", type=Path, default=default_state_dir())
    sub = p.add_subparsers(dest="command", required=True)
    sub.add_parser("doctor", help="Read-only binding and schema check; cannot verify write permission.")
    n = sub.add_parser("next", help="Read assigned tasks; never claim or execute them.")
    n.add_argument("--worker-id", required=True)
    for command in ("report", "request"):
        r = sub.add_parser(command)
        r.add_argument("--json-file", type=Path)
        for name in ("worker-id", "task-id", "session-id", "operation-id", "status", "summary", "branch", "commit-sha"):
            r.add_argument("--" + name)
        r.add_argument("--assignment-generation", type=int)
        r.add_argument("--requested-path", action="append", dest="requested_paths")
    c = sub.add_parser("controller-plan", help="Offline schema/seed plan only; performs no network calls.")
    c.add_argument("--seed-json", type=Path)
    return p


def main(argv=None):
    args = parser().parse_args(argv)
    key = ""
    try:
        if args.command == "controller-plan":
            result = controller_plan(args.seed_json)
        else:
            client = Teable()
            key = client.key
            client.bind()
            if args.command == "doctor":
                result = {"ok": True, "read_only": True, "schema_version": 1, "tables": client.tables,
                          "write_permission_verified": False, "cross_host_verified": False}
            elif args.command == "next":
                result = next_tasks(client, args.worker_id)
            else:
                payload = read_json(args.json_file) if args.json_file else {}
                if not isinstance(payload, dict):
                    raise CoordinationError("Report JSON must be an object.")
                for name in ("worker_id", "task_id", "session_id", "operation_id", "status", "summary", "branch",
                             "commit_sha", "assignment_generation", "requested_paths"):
                    value = getattr(args, name)
                    if value is not None:
                        if name in payload and payload[name] != value:
                            raise CoordinationError("CLI and JSON report values conflict.")
                        payload[name] = value
                result = send_report(client, payload, args.command, args.state_dir)
        output = canonical(result)
        print(output.replace(canonical(key)[1:-1], "[REDACTED]") if key else output)
        return 0
    except (CoordinationError, OSError, ValueError, TypeError, KeyError, OverflowError):
        exc = sys.exc_info()[1]
        message = str(exc) if isinstance(exc, CoordinationError) else "Local input or journal operation failed."
        if key:
            message = message.replace(key, "[REDACTED]")
        print(canonical({"error": message, "uncertain": isinstance(exc, UncertainWrite)}), file=sys.stderr)
        return 3 if isinstance(exc, UncertainWrite) else 2


if __name__ == "__main__":
    sys.exit(main())
