"""Offline tests: mocked HTTP transport; never open sockets or read real Teable configuration."""
import contextlib
import copy
import importlib.util
import io
import json
from email.message import Message
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import urllib.parse
import urllib.request
import urllib.response


SPEC = importlib.util.spec_from_file_location("coordination", Path(__file__).resolve().parents[1] / "scripts/agent-coordination.py")
coord = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(coord)
SCHEMA = coord.read_json(coord.SCHEMA_PATH)
FAKE_KEY = "offline-only-fake-key"


def task(task_id="task-1", **changes):
    value = {"task_id": task_id, "title": "Scoped offline fixture", "owner": "windows-1", "assignment_generation": 1,
             "owner_session_id": "session-1", "base_sha": "a" * 40, "allowed_paths_json": '["scripts/example.py"]',
             "acceptance": "Focused test passes", "dependencies_json": "[]", "status": "assigned",
             "result_refs_json": "[]", "original_refs_json": '["https://example.invalid/all62/17"]'}
    value.update(changes)
    return value


def report(**changes):
    value = {"worker_id": "windows-1", "task_id": "task-1", "assignment_generation": 1, "session_id": "session-1",
             "operation_id": "op-1", "status": "working", "summary": "Offline progress only"}
    value.update(changes)
    return value


class Fixture:
    def __init__(self):
        self.catalog = [{"id": "tblTasks", "name": SCHEMA["tables"]["tasks"]["name"]},
                        {"id": "tblEvents", "name": SCHEMA["tables"]["events"]["name"]}]
        self.fields = {role: [{**f, "id": "fld" + role + str(i)} for i, f in enumerate(spec["fields"])]
                       for role, spec in SCHEMA["tables"].items()}
        self.rows = {"tasks": [], "events": []}
        self.calls = []
        self.posts = 0
        self.write_mode = "ok"
        self.read_mode = "ok"
        self.ignore_filter = False
        self.repeat_page = False
        fixture = self

        class Handler(urllib.request.HTTPHandler):
            def reply(self, request, code, payload, headers=None):
                response_headers = Message()
                response_headers["Content-Type"] = "application/json"
                for k, v in (headers or {}).items():
                    response_headers[k] = v
                response = urllib.response.addinfourl(io.BytesIO(json.dumps(payload).encode()), response_headers, request.full_url, code)
                response.msg = "fixture"
                return response

            def http_open(self, request):
                parsed = urllib.parse.urlsplit(request.full_url)
                query = urllib.parse.parse_qs(parsed.query)
                if request.get_method() == "POST":
                    return self.post(request, parsed)
                fixture.calls.append(("GET", parsed.path, query, request.get_header("Authorization")))
                if fixture.read_mode == "redirect":
                    return self.reply(request, 302, {"secret": FAKE_KEY}, {"Location": "https://example.invalid/" + FAKE_KEY})
                if fixture.read_mode == "error":
                    return self.reply(request, 403, {"secret": FAKE_KEY})
                if parsed.path == "/api/base/baseTest/table":
                    return self.reply(request, 200, fixture.catalog)
                role = "tasks" if "/tblTasks/" in parsed.path else "events"
                if parsed.path.endswith("/field"):
                    return self.reply(request, 200, fixture.fields[role])
                rows = fixture.rows[role]
                if "filter" in query and not fixture.ignore_filter:
                    condition = json.loads(query["filter"][0])
                    assert condition["conjunction"] == "and"
                    assert all(c["operator"] == "is" for c in condition["filterSet"])
                    rows = [r for r in rows if all(r["fields"].get(c["fieldId"]) == c["value"] for c in condition["filterSet"])]
                skip, take = int(query["skip"][0]), int(query["take"][0])
                if fixture.repeat_page:
                    skip = 0
                return self.reply(request, 200, {"records": rows[skip:skip + take]})

            def post(self, request, parsed):
                fixture.posts += 1
                payload = json.loads(request.data)
                fixture.calls.append(("POST", parsed.path, payload, request.get_header("Authorization")))
                assert parsed.path == "/api/table/tblEvents/record", "Worker attempted controller-owned mutation"
                assert payload["fieldKeyType"] == "id"
                assert payload["typecast"] is False
                record = {"id": "recEvent" + str(fixture.posts), "fields": payload["records"][0]["fields"]}
                if fixture.write_mode not in {"drop-uncommitted", "error", "redirect"}:
                    fixture.rows["events"].append(record)
                if fixture.write_mode.startswith("drop"):
                    raise coord.http.client.RemoteDisconnected("fixture disconnect " + FAKE_KEY)
                if fixture.write_mode == "timeout-committed":
                    raise TimeoutError("fixture timeout " + FAKE_KEY)
                if fixture.write_mode == "error":
                    return self.reply(request, 503, {"secret": FAKE_KEY})
                if fixture.write_mode == "redirect":
                    return self.reply(request, 307, {"secret": FAKE_KEY}, {"Location": "https://example.invalid/" + FAKE_KEY})
                if fixture.write_mode == "malformed-committed":
                    return self.reply(request, 200, {})
                return self.reply(request, 201, {"records": [record]})

        # Preserve urllib's real redirect and HTTP-error processing, replace only transport.
        build = urllib.request.build_opener
        self.patch = mock.patch.object(coord.urllib.request, "build_opener", side_effect=lambda *handlers: build(Handler(), *handlers))
        self.patch.start()
        self.env = {"TEABLE_BASE_URL": "http://127.0.0.1:12345", "TEABLE_BASE_ID": "baseTest", "TEABLE_API_KEY": FAKE_KEY}

    def close(self):
        self.patch.stop()

    def add(self, role, fields):
        encoded = {f["id"]: fields[f["name"]] for f in self.fields[role] if f["name"] in fields}
        row = {"id": "rec" + role + str(len(self.rows[role])), "fields": encoded}
        self.rows[role].append(row)
        return row

    def event(self, **changes):
        p, _ = coord.report_input(report(**changes), "report")
        fields = coord.event_fields(p)
        fields["heartbeat_at"] = "2020-01-01T00:00:00+00:00"
        return self.add("events", fields)


class ClientTests(unittest.TestCase):
    def setUp(self):
        no_sockets = mock.patch("socket.socket", side_effect=AssertionError("Offline tests must not open sockets"))
        no_sockets.start()
        self.addCleanup(no_sockets.stop)
        self.fixture = Fixture()
        self.addCleanup(self.fixture.close)
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.fixture.add("tasks", task())
        self.client = coord.Teable(self.fixture.env)
        self.client.bind()

    def send(self, payload=None, kind="report"):
        return coord.send_report(self.client, report() if payload is None else payload, kind, self.temp.name)

    def test_binding_auth_and_read_only_next(self):
        result = coord.next_tasks(self.client, "windows-1")
        self.assertTrue(result["tasks"][0]["ready"])
        self.assertEqual(result["tasks"][0]["original_refs"], ["https://example.invalid/all62/17"])
        self.assertEqual(self.fixture.posts, 0)
        self.assertTrue(all(call[3] == "Bearer " + FAKE_KEY for call in self.fixture.calls))

    def test_ambiguous_table_and_missing_table_rejected(self):
        self.fixture.catalog.append({"id": "tblOther", "name": self.fixture.catalog[0]["name"]})
        with self.assertRaisesRegex(coord.CoordinationError, "ambiguous"):
            coord.Teable(self.fixture.env).bind()
        self.fixture.catalog = []
        with self.assertRaises(coord.CoordinationError):
            coord.Teable(self.fixture.env).bind()

    def test_explicit_table_ids_are_verified_in_base(self):
        self.fixture.catalog[0]["name"] = "Renamed dedicated Tasks"
        client = coord.Teable({**self.fixture.env, "TEABLE_TASK_TABLE_ID": "tblTasks"})
        client.bind()
        with self.assertRaises(coord.CoordinationError):
            coord.Teable({**self.fixture.env, "TEABLE_TASK_TABLE_ID": "tblWrongBase"}).bind()

    def test_incompatible_or_ambiguous_field_rejected(self):
        self.fixture.fields["tasks"][0]["type"] = "formula"
        with self.assertRaises(coord.CoordinationError):
            coord.Teable(self.fixture.env).bind()
        self.fixture.fields["tasks"][0]["type"] = "singleLineText"
        self.fixture.fields["tasks"].append(copy.deepcopy(self.fixture.fields["tasks"][0]))
        with self.assertRaises(coord.CoordinationError):
            coord.Teable(self.fixture.env).bind()

    def test_pagination_and_filter_encoding(self):
        for i in range(5):
            self.fixture.event(operation_id="page-" + str(i))
        with mock.patch.object(coord, "PAGE_SIZE", 2):
            rows = self.client.records("events", {"worker_id": "windows-1"})
        self.assertEqual(len(rows), 5)
        calls = [c for c in self.fixture.calls if c[1].endswith("/record")]
        self.assertEqual([c[2]["skip"] for c in calls], [["0"], ["2"], ["4"]])
        self.assertTrue(all(c[2]["ignoreViewQuery"] == ["true"] for c in calls))

    def test_filter_is_not_trusted_as_authorization(self):
        self.fixture.event(operation_id="some-other-id")
        self.fixture.ignore_filter = True
        self.assertEqual(self.client.records("events", {"operation_id": "missing"}), [])

    def test_repeated_page_rejected(self):
        self.fixture.add("tasks", task("task-2"))
        self.fixture.repeat_page = True
        with mock.patch.object(coord, "PAGE_SIZE", 2), self.assertRaisesRegex(coord.CoordinationError, "pagination"):
            coord.task_map(self.client)

    def test_ownership_generation_session_and_terminal_checks(self):
        for changes in ({"worker_id": "other"}, {"assignment_generation": 2}, {"session_id": "other"}, {"task_id": "absent"}):
            with self.subTest(changes=changes), self.assertRaises(coord.CoordinationError):
                self.send(report(**changes))
        self.fixture.rows["tasks"] = []
        self.fixture.add("tasks", task(status="accepted"))
        with self.assertRaises(coord.CoordinationError):
            self.send()
        self.assertEqual(self.fixture.posts, 0)

    def test_dependencies_require_controller_acceptance_of_exact_generation(self):
        self.fixture.rows["tasks"] = []
        self.fixture.add("tasks", task(dependencies_json='[{"task_id":"dep","assignment_generation":2}]'))
        dependency = self.fixture.add("tasks", task("dep", status="assigned", assignment_generation=2))
        self.fixture.event(task_id="dep", assignment_generation=2, status="complete-for-review", operation_id="dep-op")
        self.assertFalse(coord.next_tasks(self.client, "windows-1")["tasks"][1]["ready"])
        with self.assertRaises(coord.CoordinationError):
            self.send()
        status_id = self.client.fields["tasks"]["status"]
        generation_id = self.client.fields["tasks"]["assignment_generation"]
        dependency["fields"].update({status_id: "accepted", generation_id: 1})
        with self.assertRaises(coord.CoordinationError):
            self.send()
        dependency["fields"][generation_id] = 2
        self.assertEqual(self.send()["outcome"], "created")

    def test_missing_dependency_allows_blocker_report_but_not_work(self):
        self.fixture.rows["tasks"] = []
        self.fixture.add("tasks", task(dependencies_json='[{"task_id":"missing","assignment_generation":1}]'))
        with self.assertRaises(coord.CoordinationError):
            self.send()
        self.assertEqual(self.send(report(status="blocked", blockers=["Missing accepted dependency"]))["outcome"], "created")

    def test_duplicate_task_and_invalid_allowed_paths_fail_closed(self):
        self.fixture.add("tasks", task())
        with self.assertRaisesRegex(coord.CoordinationError, "Duplicate"):
            coord.next_tasks(self.client, "windows-1")
        for path in ("../escape", "/absolute", "C:/Windows", "scripts/*", "scripts\\file.py", "a//b"):
            with self.subTest(path=path), self.assertRaises(coord.CoordinationError):
                coord.task_fields(task(allowed_paths_json=json.dumps([path])))

    def test_worker_cannot_accept_or_overwrite_assignment_fields(self):
        for payload in (report(status="accepted"), report(owner="someone"), report(allowed_paths=["anything"])):
            with self.assertRaises(coord.CoordinationError):
                self.send(payload)
        self.assertEqual(self.fixture.posts, 0)

    def test_report_resume_no_duplicate_and_journal_has_no_content(self):
        payload = report(summary="private report summary", tests=["Executed: offline unittest passed"], branch="work/test", commit_sha="b" * 40)
        first = self.send(payload)
        second = self.send(payload)
        self.assertEqual(first["record_id"], second["record_id"])
        self.assertEqual(second["outcome"], "reconciled")
        self.assertEqual(self.fixture.posts, 1)
        journal = next(Path(self.temp.name).glob("*.json")).read_text()
        self.assertNotIn(FAKE_KEY, journal)
        self.assertNotIn("private report summary", journal)
        self.assertNotIn("work/test", journal)
        with self.assertRaises(coord.CoordinationError):
            self.send(report(summary="changed"))

    def test_content_derived_operation_id_is_repeatable(self):
        payload = report()
        del payload["operation_id"]
        first = self.send(payload)
        self.assertEqual(first["operation_id"], self.send(payload)["operation_id"])
        self.assertEqual(self.fixture.posts, 1)

    def test_fresh_journal_reconciles_existing_operation(self):
        first = self.send()
        with tempfile.TemporaryDirectory() as other:
            result = coord.send_report(self.client, report(), "report", other)
        self.assertEqual(result["record_id"], first["record_id"])
        self.assertEqual(self.fixture.posts, 1)

    def test_timeout_after_commit_reconciles_without_retry(self):
        self.fixture.write_mode = "timeout-committed"
        self.client.timeout = 0.04
        result = self.send()
        self.assertEqual(result["outcome"], "reconciled")
        self.assertEqual(self.fixture.posts, 1)

    def test_disconnect_and_malformed_success_reconcile(self):
        for i, mode in enumerate(("drop-committed", "malformed-committed")):
            self.fixture.write_mode = mode
            self.assertEqual(self.send(report(operation_id="recovery-" + str(i)))["outcome"], "reconciled")
        self.assertEqual(self.fixture.posts, 2)

    def test_absent_uncertain_write_is_not_retried_even_after_restart(self):
        self.fixture.write_mode = "drop-uncommitted"
        with self.assertRaises(coord.UncertainWrite):
            self.send()
        self.fixture.write_mode = "ok"
        with self.assertRaises(coord.UncertainWrite):
            self.send()
        self.assertEqual(self.fixture.posts, 1)
        self.fixture.event()  # Models a delayed commit becoming visible.
        self.assertEqual(self.send()["outcome"], "reconciled")
        self.assertEqual(self.fixture.posts, 1)

    def test_crash_after_journal_before_post_is_uncertain(self):
        with mock.patch.object(self.client, "create_event", side_effect=RuntimeError("simulated crash")):
            with self.assertRaises(RuntimeError):
                self.send()
        with self.assertRaises(coord.UncertainWrite):
            self.send()
        self.assertEqual(self.fixture.posts, 0)

    def test_missing_confirmed_record_does_not_cause_recreation(self):
        self.send()
        self.fixture.rows["events"] = []
        with self.assertRaises(coord.UncertainWrite):
            self.send()
        self.assertEqual(self.fixture.posts, 1)

    def test_duplicate_operation_or_mismatched_remote_content_rejected(self):
        self.fixture.event()
        self.fixture.event()
        with self.assertRaisesRegex(coord.CoordinationError, "Duplicate operation"):
            self.send()
        self.fixture.rows["events"] = []
        self.fixture.event(summary="different")
        with self.assertRaisesRegex(coord.CoordinationError, "different event"):
            self.send()
        self.assertEqual(self.fixture.posts, 0)

    def test_conflicting_session_detected_and_no_stealing_stale_work(self):
        self.fixture.event(session_id="second-session", operation_id="other-op")
        item = coord.next_tasks(self.client, "windows-1")["tasks"][0]
        self.assertTrue(item["session_conflict"])
        self.assertFalse(item["ready"])
        self.assertEqual(item["heartbeat_observation"], "stale")
        with self.assertRaisesRegex(coord.CoordinationError, "Conflicting"):
            self.send()
        self.assertEqual(self.fixture.posts, 0)

    def test_complete_for_review_waits_for_controller_and_never_accepts(self):
        self.send(report(status="complete-for-review"))
        item = coord.next_tasks(self.client, "windows-1")["tasks"][0]
        self.assertTrue(item["awaiting_controller"])
        self.assertFalse(item["ready"])
        self.assertEqual(item["status"], "assigned")
        with self.assertRaises(coord.CoordinationError):
            self.send(report(operation_id="op-after-submit"))
        self.send(report(operation_id="heartbeat-after-submit", status="heartbeat"))

    def test_request_cannot_expand_allowed_paths(self):
        payload = report(status="requested", requested_paths=["shared/api.py"], summary="Need controller API owner approval")
        self.send(payload, "request")
        item = coord.next_tasks(self.client, "windows-1")["tasks"][0]
        self.assertEqual(item["allowed_paths"], ["scripts/example.py"])
        fields = self.client.records("events")[0]["fields"]
        self.assertEqual(fields["kind"], "request")

    def test_redirect_refused_and_http_error_body_never_exposed(self):
        for mode in ("redirect", "error"):
            self.fixture.read_mode = mode
            before = len(self.fixture.calls)
            with self.assertRaises(coord.CoordinationError) as caught:
                self.client.table_catalog()
            self.assertNotIn(FAKE_KEY, str(caught.exception))
            self.assertNotIn("example.invalid", str(caught.exception))
            self.assertEqual(len(self.fixture.calls), before + 1)

    def test_write_http_error_single_attempt_and_secret_not_logged(self):
        self.fixture.write_mode = "error"
        with self.assertRaises(coord.UncertainWrite) as caught:
            self.send()
        self.assertNotIn(FAKE_KEY, str(caught.exception))
        with self.assertRaises(coord.UncertainWrite):
            self.send()
        self.assertEqual(self.fixture.posts, 1)

    def test_write_redirect_never_forwards_credentials_or_retries(self):
        self.fixture.write_mode = "redirect"
        with self.assertRaises(coord.UncertainWrite) as caught:
            self.send()
        self.assertNotIn(FAKE_KEY, str(caught.exception))
        self.assertEqual(self.fixture.posts, 1)
        self.assertFalse(self.fixture.rows["events"])

    def test_configured_token_in_report_is_rejected(self):
        with self.assertRaises(coord.CoordinationError):
            self.send(report(summary="accidental " + FAKE_KEY))
        self.client.key = 'fake-"-escaped-credential'
        with self.assertRaises(coord.CoordinationError):
            self.send(report(summary="accidental " + self.client.key))
        self.assertEqual(self.fixture.posts, 0)

    def test_token_redaction_in_server_supplied_task_output(self):
        fake_escaped_key = 'fake-"-escaped-credential'
        self.fixture.rows["tasks"] = []
        self.fixture.add("tasks", task(title="Server echoes " + fake_escaped_key))
        out = io.StringIO()
        with mock.patch.dict(coord.os.environ, {**self.fixture.env, "TEABLE_API_KEY": fake_escaped_key}, clear=True), contextlib.redirect_stdout(out):
            self.assertEqual(coord.main(["next", "--worker-id", "windows-1"]), 0)
        result = json.loads(out.getvalue())
        self.assertEqual(result["tasks"][0]["title"], "Server echoes [REDACTED]")

    def test_reconciled_receipt_persisted_and_not_recreated_if_removed(self):
        self.fixture.event()
        self.assertEqual(self.send()["outcome"], "reconciled")
        journal = json.loads(next(Path(self.temp.name).glob("*.json")).read_text())
        self.assertEqual(journal["operations"]["op-1"]["state"], "confirmed")
        self.fixture.rows["events"] = []
        with self.assertRaises(coord.UncertainWrite):
            self.send()
        self.assertEqual(self.fixture.posts, 0)

    def test_json_cli_and_error_redaction(self):
        path = Path(self.temp.name) / "input.json"
        path.write_text(json.dumps(report()), encoding="utf-8")
        out, err = io.StringIO(), io.StringIO()
        with mock.patch.dict(coord.os.environ, self.fixture.env, clear=True), contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = coord.main(["--state-dir", self.temp.name, "report", "--json-file", str(path)])
        self.assertEqual(code, 0, err.getvalue())
        self.assertEqual(json.loads(out.getvalue())["outcome"], "created")
        self.assertNotIn(FAKE_KEY, out.getvalue() + err.getvalue())

    def test_cli_rejects_conflicting_file_and_flag(self):
        path = Path(self.temp.name) / "input.json"
        path.write_text(json.dumps(report()), encoding="utf-8")
        with mock.patch.dict(coord.os.environ, self.fixture.env, clear=True), contextlib.redirect_stderr(io.StringIO()):
            code = coord.main(["report", "--json-file", str(path), "--worker-id", "different"])
        self.assertEqual(code, 2)
        self.assertEqual(self.fixture.posts, 0)

    def test_doctor_is_read_only_and_does_not_claim_cross_host_verification(self):
        out = io.StringIO()
        with mock.patch.dict(coord.os.environ, self.fixture.env, clear=True), contextlib.redirect_stdout(out):
            self.assertEqual(coord.main(["doctor"]), 0)
        result = json.loads(out.getvalue())
        self.assertFalse(result["cross_host_verified"])
        self.assertFalse(result["write_permission_verified"])
        self.assertEqual(self.fixture.posts, 0)

    def test_local_journal_command_lock_and_capacity(self):
        binding = [self.client.base_url, self.client.base_id, self.client.tables]
        first = coord.Journal(self.temp.name, binding, "windows-1")
        second = coord.Journal(self.temp.name, binding, "windows-1")
        with first.locked(), self.assertRaises(coord.CoordinationError):
            with second.locked():
                pass
        with mock.patch.object(coord, "MAX_OPERATIONS", 0), self.assertRaises(coord.CoordinationError):
            self.send()
        self.assertEqual(self.fixture.posts, 0)


class OfflineValidationTests(unittest.TestCase):
    def test_tls_and_origin_validation(self):
        env = {"TEABLE_BASE_ID": "baseTest", "TEABLE_API_KEY": FAKE_KEY}
        for url in ("http://example.com", "https://user:password@example.com", "https://example.com/api", "https://example.com?key=x",
                    "https://example.com#fragment", "https://example.com:bad", "http://127.0.0.1.evil.test", "file:///tmp/a"):
            with self.subTest(url=url), self.assertRaises(coord.CoordinationError):
                coord.Teable({**env, "TEABLE_BASE_URL": url})
        for url in ("https://example.com", "http://localhost:80", "http://127.0.0.1:80", "http://[::1]:80"):
            coord.Teable({**env, "TEABLE_BASE_URL": url})

    def test_controller_plan_offline_validates_seed_and_preserves_links(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "seed.json"
            path.write_text(json.dumps([task()]), encoding="utf-8")
            with mock.patch.object(coord.urllib.request.OpenerDirector, "open", side_effect=AssertionError("Network forbidden")):
                plan = coord.controller_plan(path)
            self.assertTrue(plan["read_only"])
            self.assertEqual(plan["seed_records"][0]["fields"], task())
            path.write_text(json.dumps([task(), task()]), encoding="utf-8")
            with self.assertRaises(coord.CoordinationError):
                coord.controller_plan(path)


if __name__ == "__main__":
    unittest.main()
